using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageLibraryRenamer.ExifExtractor;

namespace ImageLibraryRenamer
{
    public class FolderDateRenamer
    {
        public struct RenameFoldersParams
        {
            private string _directory;
            private readonly string _searchPattern;
            private readonly string _datePattern;
            private readonly bool _useExifDataToGetDate;
            private readonly bool _useFileDateIfNoExif;
            private readonly bool _recursive;
            private readonly bool _testMode;
            private readonly ActivityLogger _logger;

            public RenameFoldersParams(string directory, string searchPattern, string datePattern,
                                       bool useExifDataToGetDate, bool useFileDateIfNoExif, bool Recursive,
                                       bool TestMode, ActivityLogger logger)
                : this()
            {
                _directory = directory;
                _searchPattern = searchPattern;
                _datePattern = datePattern;
                _useExifDataToGetDate = useExifDataToGetDate;
                _useFileDateIfNoExif = useFileDateIfNoExif;
                _recursive = Recursive;
                _testMode = TestMode;
                _logger = logger;
                SkipTopLevel = true;
                SkipNumeric = false;
                SkipFolders = null;
            }

            public string Directory
            {
                get { return _directory; }
                set { _directory = value; }
            }

            public string SearchPattern
            {
                get { return _searchPattern; }
            }

            public string DatePattern
            {
                get { return _datePattern; }
            }

            public bool UseExifDataToGetDate
            {
                get { return _useExifDataToGetDate; }
            }

            public bool UseFileDateIfNoExif
            {
                get { return _useFileDateIfNoExif; }
            }

            public bool Recursive
            {
                get { return _recursive; }
            }

            public bool TestMode
            {
                get { return _testMode; }
            }

            public ActivityLogger Logger
            {
                get { return _logger; }
            }

            public bool SkipTopLevel { get; set; }

            public bool SkipNumeric { get; set; }

            public string SkipFolders { get; set; }
        }

        public Dictionary<string, string> RenameQueue = new Dictionary<string, string>();
        private RenameFoldersParams options;

        public FolderDateRenamer(RenameFoldersParams options)
        {
            this.options = options;
        }

        public void ParseData()
        {
            if (string.IsNullOrWhiteSpace(options.Directory) || !Directory.Exists(options.Directory))
            {
                options.Logger.FatalLog("Directory does not exist");
                return;
            }

            SearchOption searchOption = SearchOption.TopDirectoryOnly;

            if (options.Recursive) searchOption = SearchOption.AllDirectories;

            var directoryInfo = new DirectoryInfo(options.Directory);

            if (directoryInfo.Name.StartsWith("."))
            {
                options.Logger.Log("Skipping: hidden folder: " + options.Directory);
                return;
            }


            options.Logger.Log("Checking " + options.Directory + " ");

            if (options.SkipTopLevel)
            {
                options.SkipTopLevel = false;
                options.Logger.Log("Skipping top level: " + directoryInfo.Name);
            }
            else if (options.SkipNumeric && IsNumber(directoryInfo.Name))
            {
                options.Logger.Log("Skipping numeric: " + directoryInfo.Name);
            }
            else if (options.SkipFolders.Contains(directoryInfo.Name))
            {
                options.Logger.Log("Skipping: " + directoryInfo.Name);
            }
            else
            {
                var files = directoryInfo.GetFiles(options.SearchPattern, searchOption).ToList();

                DateTime? exifDate = null;
                DateTime? fileCreateDate = null;

                foreach (var file in files)
                {
                    options.Logger.LogAppendToLast(".");

                    if (options.UseExifDataToGetDate)
                    {
                        exifDate = ImageViewer.GetTakenTime(file.FullName);

                        if (exifDate != null)
                        {
                            options.Logger.Log("Found EXIF date: " + exifDate);
                            break;
                        }
                    }

                    if (options.UseFileDateIfNoExif)
                    {
                        if (fileCreateDate == null || file.CreationTime < fileCreateDate)
                        {
                            fileCreateDate = file.CreationTime;
                        }

                        if (fileCreateDate == null || file.LastWriteTime < fileCreateDate)
                        {
                            fileCreateDate = file.LastWriteTime;
                        }


                    }
                }

                if (exifDate == null && fileCreateDate == null)
                {
                    options.Logger.Log("[SKIPPING] No dates found.");
                    return;
                }


                DateTime folderDate = exifDate != null ? exifDate.Value : fileCreateDate.Value;

                string pattern = options.DatePattern.Replace("[folder]", "[]");


                string dateString = folderDate.ToString(pattern);

                if (dateString.Contains(directoryInfo.Name))
                {
                    options.Logger.Log("[SKIPPING] folder name already contains the same date");
                    return;
                }

                string newPath = directoryInfo.FullName.Replace(directoryInfo.Name, dateString).Replace("[]", directoryInfo.Name);


                options.Logger.Log(string.Format("[RENAME] {0} ==> {1}", directoryInfo.FullName, newPath));


                RenameQueue.Add(directoryInfo.FullName, newPath);


            }

            var directories = directoryInfo.GetDirectories().ToList();

            if (options.Recursive)
            {
                directories.ForEach(d =>
                    {
                        options.Directory = d.FullName;
                        ParseData();
                    });
            }
        }

        public bool IsNumber(String value)
        {
            return !value.ToCharArray().Where(x => !Char.IsDigit(x)).Any();
        }

        public void RenameFolders()
        {
            RenameQueue.Reverse().ToList().ForEach(pair =>
                {
                    try
                    {
                        System.IO.Directory.Move(pair.Key, pair.Value);
                    }
                    catch (Exception ex)
                    {
                        this.options.Logger.FatalLog(ex.ToString());
                    }
                });
        }
    }
}