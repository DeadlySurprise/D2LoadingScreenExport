namespace DotaLoadScreenExport
{
    using SteamDatabase.ValvePak;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public class Program
    {
        private const string JsonOutput = "loadingscreens.json";
        private const string JsonDB = "loadingscreens-db.json";

        private static Options options;

        private static ImageFormat UploadFormat = ImageFormat.Jpeg;

        public static void Main(string[] args)
        {
            options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                WriteLineVerbose("Opening apks...");
                var apk = Task.Run(() =>
                {
                    var apkInternal = new Package();
                    apkInternal.Read(Path.Combine(options.Dota2DirPath, @"game\dota\pak01_dir.vpk"));
                    WriteLineVerbose("Opened apks.");
                    return apkInternal;
                });

                Task.Run(async () =>
              {
                  var dbPath = Path.Combine(options.OutDirPath, JsonDB);
                  var db = File.Exists(dbPath) ? File.ReadAllText(dbPath) : null;
                  await ExportLoadingScreens(
                      new DirectoryInfo(options.OutDirPath),
                      apk,
                      ImageFormat.Jpeg,
                      db);
              }).Wait();
            }
        }

        private static async Task ExportLoadingScreens(DirectoryInfo dest, Task<Package> apk_task, ImageFormat imgFormat, string jsonPreviousDB = null)
        {
            try
            {
                DateTime startTime = DateTime.Now;
                // Load previous loading screen db.
                var onlyExportNew = !string.IsNullOrWhiteSpace(jsonPreviousDB);
                var images = onlyExportNew ? LoadingScreenDB.LoadFromJsonDB(jsonPreviousDB) : new List<LoadingScreenDB.LoadingScreenDBInfo>();
                if (images.Count > 0)
                {
                    Console.WriteLine("{0} loading screens in db.", images.Count);
                }

                WriteLineVerbose("Collecting loading screens information...");
                var loadingScreensRes_task = Dota2AssetDB.GetAssets("vtex_c", e => e.DirectoryName.StartsWith("panorama/images/loadingscreens"), apk_task);
                var loadingScreen_task = Task.Run(async () =>
                {
                    var lsItems = (await Dota2ItemDB.GetItems(apk_task, i => i.Type == "loading_screen"));
                    return lsItems.Select(ls => FixupLSWithStyles(ls)).ToList();
                });

                var loadingScreen = await loadingScreen_task;
                var loadingScreenRes = await loadingScreensRes_task;
                WriteLineVerbose($"Found {(await loadingScreen_task).Count} registered loading screen items.");
                WriteLineVerbose($"Found {(await loadingScreensRes_task).Count} loading screen assets.");

                // Create output directories if missing.
                if (!dest.Exists)
                {
                    dest.Create();
                }

                var imgOutDir = dest.GetDirectories().ToList().Find(dir => dir.Name == "out") ?? dest.CreateSubdirectory("out");


                var progressCount = 0;
                var dirPrefixLength = "panorama/images/".Length;
                var entriesWithChanges = GetLSWithChanges(images, loadingScreen, loadingScreenRes, out var skipped, out var notFound, dirPrefixLength).ToList();
                var apk = await apk_task;
                WriteLineVerbose($"Exporting {entriesWithChanges.Count()} loading screens...");
                //Export images
                var exportedImages =
                    entriesWithChanges
                    .OrderBy(key => key.Name)
                    .Select(async ls =>
                    {
                        var pkgEntry = loadingScreenRes.Find(entry => entry.GetFullPath().Substring(dirPrefixLength).StartsWith(ls.Path));
                        var img = Dota2AssetDB.GetTextureAsset(apk, pkgEntry, new Rectangle(0, 0, 1920, 1080));
                        var imgFileName = $"{ls.Name}.{imgFormat.ToString().ToLower()}";

                        (await img).Save(Path.Combine(imgOutDir.FullName, imgFileName), imgFormat);
                        Interlocked.Increment(ref progressCount);
                        WriteLineVerbose($"Exported {imgFileName}");
                        return new LoadingScreenDB.LoadingScreenDBInfo()
                        {
                            Name = ls.Name,
                            ID = ls.ID,
                            ImageLink = imgFileName,
                            Crc32 = pkgEntry.CRC32,
                            FullPath = pkgEntry.GetFullPath(),
                            Size = pkgEntry.Length
                        };
                    });

                var exportTask = Task.WhenAll(exportedImages);
                images.AddRange(await exportTask);
                Console.WriteLine($"Finished exporting {entriesWithChanges.Count} loading screens in {(DateTime.Now - startTime).TotalMilliseconds / 1000f:0.00}s");
                Console.WriteLine($"{notFound} not found and {skipped} skipped.");
                File.WriteAllText(Path.Combine(dest.FullName, JsonDB), LoadingScreenDB.GetJsonDB(images));
                File.WriteAllText(Path.Combine(dest.FullName, JsonOutput), LoadingScreenDB.GetBasicJson(images));
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
                return;
            }
        }

        private static List<Dota2ItemDB.DotaItem> GetLSWithChanges(List<LoadingScreenDB.LoadingScreenDBInfo> images, List<Dota2ItemDB.DotaItem> loadingScreen, List<PackageEntry> loadingScreenRes, out int skipped, out int notFound, int dirPrefixLength)
        {
            var skippedInt = 0;
            var notFoundInt = 0;
            var entries = loadingScreen
                .Where(ls =>
                    {
                        var pkgEntry = loadingScreenRes.Find(entry => entry.GetFullPath().Substring(dirPrefixLength).StartsWith(ls.Path));
                        if (pkgEntry != null)
                        {
                            if (images.Exists(img => img.Matches(pkgEntry)))
                            {
                                skippedInt++;
                                return false;
                            }
                            else
                            {
                                return true;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Error : {LimitString(ls.Name, 30)} not found!");
                            notFoundInt++;
                            return false;
                        }
                    })
                    .ToList();
            skipped = skippedInt;
            notFound = notFoundInt;
            return entries;
        }

        private static Dota2ItemDB.DotaItem FixupLSWithStyles(Dota2ItemDB.DotaItem ls)
        {
            //Loading screen has styles.
            if (ls.Name[0] == '#')
            {
                var match = new Regex(@"#DOTA_Item_(\w+)(_Loading_Screen)?_\w*").Match(ls.Name);
                if (!match.Success)
                {
                    throw new InvalidOperationException();
                }
                ls.Name = match.Groups[1].Value.Replace('_', ' ');
                if (ls.Path.StartsWith("console/"))
                {
                    ls.Path = ls.Path.Substring("console/".Length);
                }
            };

            return ls;
        }

        private static string LimitString(string str, int limit)
        {
            if (str.Length <= limit)
            {
                return str;
            }
            else
            {
                return str.Substring(0, limit) + "...";
            }
        }

        private static void WriteLineVerbose(string value)
        {
            if (options.Verbose)
            {
                Console.WriteLine(value);
            }
        }
    }
}
