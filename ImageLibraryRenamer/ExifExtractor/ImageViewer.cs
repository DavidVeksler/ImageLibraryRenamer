using System;
using System.Diagnostics;
using ExifLib;

namespace ImageLibraryRenamer
{
    public class ImageViewer
    {
        public static DateTime? GetTakenTime(string fullName)
        {
            ExifReader reader = null;
            try
            {
                reader = new ExifReader(fullName);

                DateTime datePictureTaken;

                if (reader.GetTagValue(ExifTags.DateTimeOriginal, out datePictureTaken))
                {
                    Debug.WriteLine(string.Format("The picture was taken on {0}", datePictureTaken));
                    return datePictureTaken;
                }

                if (reader.GetTagValue(ExifTags.DateTimeDigitized, out datePictureTaken))
                {
                    Debug.WriteLine(string.Format("The picture was taken on {0}", datePictureTaken));
                    return datePictureTaken;
                }

                if (reader.GetTagValue(ExifTags.DateTime, out datePictureTaken))
                {
                    Debug.WriteLine(string.Format("The picture was taken on {0}", datePictureTaken));
                    return datePictureTaken;
                }

                if (reader.GetTagValue(ExifTags.GPSDateStamp, out datePictureTaken))
                {
                    Debug.WriteLine(string.Format("The picture was taken on {0}", datePictureTaken));
                    return datePictureTaken;
                }

                Debug.WriteLine(string.Format("The picture was taken on {0}", datePictureTaken));
                return datePictureTaken;
            }
            catch (Exception ex)
            {
                // Something didn't work!
                Debug.WriteLine(ex.ToString());
                //                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                if (reader != null)
                    reader.Dispose();
            }

            return null;
        }
    }
}