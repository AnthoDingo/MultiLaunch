using System;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.IO;

namespace MultiLaunch.Statics
{
    public static class IconExtractor
    {
        public static BitmapSource? ExtractIcon(string? exePath)
        {
            if (string.IsNullOrEmpty(exePath))
                return null;

            exePath = exePath.Replace("\"", string.Empty);
            if (!File.Exists(exePath))
                return null;

            using Icon? icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon == null)
                return null;

            using var iconStream = new MemoryStream();
            icon.ToBitmap().Save(iconStream, System.Drawing.Imaging.ImageFormat.Png);
            iconStream.Seek(0, SeekOrigin.Begin);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = iconStream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
