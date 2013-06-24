using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ImageLibraryRenamer
{
    public class EmbedPicasaProperties
    {
        private const string IniFileName = @".picasa.ini";

        protected ActivityLogger Logger { get; set; }

        public void ParseFolder(string startFolder, ActivityLogger logger)
        {
            Logger = logger;

            Logger.Log(startFolder);

            UpdateFoldersInPath(startFolder);
        }

        private void UpdateFoldersInPath(string folder)
        {
            string[] directories = Directory.GetDirectories(folder);

            if (directories.Length > 0)
                Logger.Log(string.Format("{0} folders in {1}", directories.Length, folder));

            foreach (string directory in directories)
            {
                try
                {

                    string iniPath = Path.Combine(directory, IniFileName);

                    if (File.Exists(iniPath))
                    {
                        // Logger.Log("Parse " + iniPath);

                        foreach (string line in File.ReadAllLines(iniPath))
                        {
                            // date=29586.603900
                            if (!line.StartsWith("date="))
                                continue;

                            string dateString = line.Substring(5);

                            DateTime date = ConverPicasaDateToDateTime(dateString);

                            DateTime originalTime = Directory.GetCreationTime(directory);

                            if (date.Year < 2010 && originalTime.Subtract(date).TotalDays > 365)
                            {
                                Logger.Log(string.Format("{0} to {1}", originalTime, date));
                                Directory.SetCreationTime(directory, date);

                                Directory.SetLastWriteTime(directory, date);
                            }

                            var files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);

                            foreach (string file in files)
                            {
                                Logger.Log(file);

                                DateTime originalFileTime = Directory.GetCreationTime(file);

                                if (originalFileTime.Subtract(date).Days > 365)
                                {
                                    Logger.Log(String.Format("convert {0} to {1}", originalFileTime, date));

                                    File.SetCreationTime(file, date);
                                    File.SetLastWriteTime(file, date);
                                }
                            }


                            break;
                        }
                    }


                    //if (!foundDateInPicasaIniFile) {
                    //    GetFileNameDateFromFolderName (directory);
                    //}

                }
                catch (Exception ex)
                {
                    Logger.FatalLog(ex.ToString());
                }

                try
                {
                    UpdateFoldersInPath(directory);
                }
                catch (Exception ex)
                {
                    Logger.FatalLog(ex.ToString());
                }
            }
        }


        // convert =29586.603900 to date time format
        private DateTime ConverPicasaDateToDateTime(string dateString)
        {
            var startDate = new DateTime(1900, 1, 1);

            DateTime date = startDate.AddDays(Convert.ToDouble(dateString) - 2);

            Logger.Log("new date: " + date);

            return date;
        }
    }
}