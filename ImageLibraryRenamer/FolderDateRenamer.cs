using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
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
            {
                _directory = directory;
                _searchPattern = searchPattern;
                _datePattern = datePattern;
                _useExifDataToGetDate = useExifDataToGetDate;
                _useFileDateIfNoExif = useFileDateIfNoExif;
                _recursive = Recursive;
                _testMode = TestMode;
                _logger = logger;
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
        }

        public void RenameFolders(RenameFoldersParams options)
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
                options.Logger.Log("Skipping " + options.Directory);
                return;
            }
            
            
                options.Logger.Log("Checking " + options.Directory + " ");
            

            

            var files = directoryInfo.GetFiles(options.SearchPattern, searchOption).ToList();

            DateTime? exifDate = null;
            DateTime? fileCreateDate = null;

            files.ForEach(file =>
                {
                    if (exifDate != null) return;

                    options.Logger.LogAppendToLast(".");

                    if (options.UseExifDataToGetDate)
                    {
                        exifDate = ImageViewer.GetTakenTime(file.FullName);

                        if (exifDate != null)
                        {
                            options.Logger.Log("Found EXIF date: " + exifDate);
                        }
                    }

                    if (exifDate == null && options.UseFileDateIfNoExif)
                    {
                        DateTime created = file.CreationTime;
                        DateTime modified = file.LastWriteTime;

                        fileCreateDate = created > modified ? modified : created;
                    }
                });

            if (exifDate == null && fileCreateDate == null) return;


            DateTime folderDate = exifDate != null ? exifDate.Value : fileCreateDate.Value;

            string pattern = options.DatePattern.Replace("[folder]", "[]");

            string newPath =
                directoryInfo.FullName.Replace(directoryInfo.Name, folderDate.ToString(pattern))
                             .Replace("[]", directoryInfo.Name);

            if (!options.TestMode)
            {
                options.Logger.Log(string.Format("Rename {0} to {1}", directoryInfo.FullName, newPath));
                directoryInfo.MoveTo(newPath);
            }
            else
            {
                options.Logger.Log(string.Format("[TEST MODE] Rename {0} to {1}", directoryInfo.FullName, newPath));
            }

            var directories = directoryInfo.GetDirectories().ToList();

            directories.ForEach(d =>
                {
                    options.Directory = d.FullName;
                    RenameFolders(options);
                    
                });
        }
    }
}