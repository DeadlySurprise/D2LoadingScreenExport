namespace DotaLoadScreenExport
{
    using SteamDatabase.ValvePak;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    class Program
    {
        private const string AlbumTitle = "DOTA 2 Loading Screens";
        private const string JsonOutput = "loadingscreens.json";
        private const string JsonDB = "loadingscreens-db.json";

        private static ImageFormat UploadFormat = ImageFormat.Jpeg;

        public static void Main(string[] args)
        {
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {

                Console.WriteLine("Opening apks...");
                var apk = Task.Run(() =>
                {
                    var apkInternal = new Package();
                    apkInternal.Read(options.Dota2DirPath);
                    Console.WriteLine("Opened apks.");
                    return apkInternal;
                });
                var dbPath = Path.Combine(options.OutDirPath, JsonDB);
                var db = File.Exists(dbPath) ? File.ReadAllText(dbPath) : null;

                ExportLoadingScreens(
                    new DirectoryInfo(options.OutDirPath),
                    apk,
                    ImageFormat.Jpeg,
                    db);
            }
        }

        private static async void ExportLoadingScreens(DirectoryInfo dest, Task<Package> apk_task, ImageFormat imgFormat, string jsonPreviousDB = null)
        {
            DateTime startTime = DateTime.Now;
            // Load previous loading screen db.
            var onlyExportNew = !string.IsNullOrWhiteSpace(jsonPreviousDB);
            var images = onlyExportNew ? LoadingScreenDB.LoadFromJsonDB(jsonPreviousDB) : new List<LoadingScreenDB.LoadingScreenDBInfo>();
            if (images.Count > 0)
            {
                Console.WriteLine("{0} loading screens in db.", images.Count);
            }

            Console.WriteLine("Collecting loading screens information...");
            var loadingScreensRes_task = Dota2AssetDB.GetAssets("vtex_c", e => e.DirectoryName.StartsWith("panorama/images/loadingscreens"), apk_task);
            var loadingScreen_task = Task.Run(async () =>
            {
                var lsItems = (await Dota2ItemDB.GetItems(apk_task, i => i.Type == "loading_screen"));
                return lsItems.Select(ls => FixupLSWithStyles(ls)).ToList();
            });

            var loadingScreen = await loadingScreen_task;
            var loadingScreenRes = await loadingScreensRes_task;
            Console.WriteLine($"Found {(await loadingScreen_task).Count} registered loading screen items.");
            Console.WriteLine($"Found {(await loadingScreensRes_task).Count} loading screen assets.");

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
            Console.WriteLine($"Exporting {entriesWithChanges.Count()} loading screens...");
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
                    Console.WriteLine("Exported {0}", imgFileName);
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
    }
}
