Folder and File Organization  for Large Image Libraries:
==========

*Useful when migrating from Picasa to a professional photo workflow (Lightroom, etc).
*Safe to run on the same set of folders multiple times - will detect if folder has already been renamed.

Tools (Run in this order before migrating your library to Lightroom):
*Picasa Embeder: Read dates from .picasa.ini files and convert them to file create dates
*Folder Renamer: Rename your image folders based on the oldest EXIF date and/or the file creation dates. 
*Folder name parser: Read the date from the folder name and use them for the file create/modify date.



==========

  ![Options](https://raw.github.com/DavidVeksler/ImageLibraryRenamer/master/Screenshots/Options.png)
  
  ![Log](https://raw.github.com/DavidVeksler/ImageLibraryRenamer/master/Screenshots/Log.png)

==========

Picasa Date Embeder:

Convert Picasa ini dates to file/directory dates.

This is mainly useful:

*If you are importing images from sources other than a digital camera (film or scans).
*If you run image optimization apps (JPEGmini, etc)  on your existing library so that it changes the original dates.


The log for this process is not very informative, but it should work.

==========

Works on OS X Too:


  ![Confirm](https://raw.github.com/DavidVeksler/ImageLibraryRenamer/master/Screenshots/MonoConfirm.png)
  
  ![Mono Options](https://raw.github.com/DavidVeksler/ImageLibraryRenamer/master/Screenshots/MonoOptions.png)