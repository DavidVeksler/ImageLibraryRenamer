using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using ImageLibraryRenamer.ExifExtractor.EXIF;

namespace ImageLibraryRenamer.ExifExtractor
{
    public class ImageViewer
    {
        public static DateTime? GetTakenTime(string imageFilePath)
        {
            try
            {
                var bmp = new Bitmap(imageFilePath);
                var exif = new EXIFextractor(ref bmp, "n");

                if (exif["Date Time"] != null)
                {
                    var str = ((string)exif["Date Time"]).TrimEnd('\0');

                    var parts = str.Split(' ');
                    var date = parts[0].Split(':').Select(part => Convert.ToInt32(part)).ToArray();
                    var time = parts[1].Split(':').Select(part => Convert.ToInt32(part)).ToArray();

                    return new DateTime(date[0], date[1], date[2], time[0], time[1], time[2]);
                }

                return null;

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return null;
            }
        }
    }
}
