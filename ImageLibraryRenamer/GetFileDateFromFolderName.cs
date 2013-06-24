using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ImageLibraryRenamer
{
    class GetFileDateFromFolderName
    {

        public void ParseFolder(string startFolder, ActivityLogger logger, int daysDiff)
        {
            this.Logger = logger;
            this.minNumDaysDiff = daysDiff;

            Logger.Log(startFolder);

            UpdateFoldersInPath(startFolder);

        }

        protected ActivityLogger Logger { get; set; }
        private int minNumDaysDiff { get; set; }


        private void UpdateFoldersInPath(string directory)
        {
            // look for a date in the folder name and change it if the year does not match
            DateTime existingFolderDate;

            string folderName = new DirectoryInfo(directory).Name;

            if (DateTime.TryParse(folderName.Split(' ').First(), out existingFolderDate))
            {

                Logger.Log("Found date in folder name: " + existingFolderDate + " for " + directory);
                // get date of all photos
                var files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);
                foreach (string file in files)
                {
                    //Logger.Log(file);
                    DateTime originalFileTime = Directory.GetCreationTime(file);
                    if (originalFileTime.Subtract(existingFolderDate).Days > minNumDaysDiff)
                    {
                        Logger.Log(String.Format("Change {0} to {1} for {2}", originalFileTime, existingFolderDate, file));
                        File.SetCreationTime(file, existingFolderDate);
                        File.SetLastWriteTime(file, existingFolderDate);
                    }
                }
            }

            string[] directories = Directory.GetDirectories(directory);


            foreach (string subFolder in directories)
            {
                UpdateFoldersInPath(subFolder);
            }

        }



    }
}
