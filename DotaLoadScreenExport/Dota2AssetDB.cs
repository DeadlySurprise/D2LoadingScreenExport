namespace DotaLoadScreenExport
{
    using SteamDatabase.ValvePak;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using ValveResourceFormat;
    using ValveResourceFormat.ResourceTypes;

    public static class Dota2AssetDB
    {
        /// <summary>
        /// Gets all assets of a given type that match the predicate.
        /// </summary>
        /// <param name="assetType">The asset type to search for.</param>
        /// <param name="predicate">The predicate to match.</param>
        /// <param name="apk">The dota 2 apk package containing the assets.</param>
        /// <returns>A list of all matched assets.</returns>
        public static Task<List<PackageEntry>> GetAssets(string assetType, Func<PackageEntry, bool> predicate, Task<Package> apk)
        {
            return Task.Run(async () =>
                {
                    return (await apk).Entries[assetType]
                       .Where(predicate)
                       .ToList();
                });
        }

        /// <summary>
        /// Exports a give texture from a package entry.
        /// </summary>
        /// <param name="apk">The dota 2 package apk.</param>
        /// <param name="entry">The package entry of the texture asset.</param>
        /// <param name="resizeTo">The size the asset should be resized to. Can be null if the asset should not be resized.</param>
        /// <returns></returns>
        public static Task<Bitmap> GetTextureAsset(Package apk, PackageEntry entry, Rectangle? resizeTo = null)
        {
            return Task.Run(() =>
            {
                apk.ReadEntry(entry, out var output);
                var res = new Resource();
                using (var resMem = new MemoryStream(output))
                {
                    res.Read(resMem);

                    if (res.ResourceType != ResourceType.Texture)
                    {
                        throw new NotSupportedException();
                    }

                    var tex = (Texture)res.Blocks[BlockType.DATA];
                    Bitmap bmp2;
                    var bmp = tex.GenerateBitmap();

                    if (resizeTo.HasValue)
                    {
                        bmp2 = bmp;
                        bmp = ResizeImage(bmp2, resizeTo.Value.Width, resizeTo.Value.Height);
                        bmp2.Dispose();
                    }

                    return bmp;
                }
            });
        }

        /// <summary>
        /// Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        private static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

    }
}
