using System;
using System.Windows.Forms;

namespace ImageLibraryRenamer
{
    public partial class Main : Form
    {
        public delegate void StatusUpdated(object sender, EventArgs args);



        public Main()
        {
            InitializeComponent();

            txtPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            this.Logger = new ActivityLogger(this.lbStatus);
        }

        protected ActivityLogger Logger { get; set; }

        private void OnStatusUpdated(object sender, EventArgs args)
        {
            lbStatus.Items.Add(args.ToString());
        }

        #region UI

        private void btnPickFolder_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog1.ShowDialog();

            if (result == DialogResult.OK)
            {
                txtPath.Text = openFileDialog1.FileName;
            }
        }

        private void btnGo_Click(object sender, EventArgs e)
        {
            Logger.Log("Starting...");

            this.tabControl1.SelectTab(1);

            Application.DoEvents();

            var options = new FolderDateRenamer.RenameFoldersParams(txtPath.Text, txtFileNamePattern.Text,
                                                                    txtDatePattern.Text, chkUseEXIFDataToGetDate.Checked,
                                                                    chkUseFileDateIfNoEXIF.Checked, chkRecusrive.Checked,
                                                                    chkPreview.Checked, this.Logger)
                {
                    SkipTopLevel = chkSkipTopLevel.Checked,
                    SkipNumeric = chkSkipNumeric.Checked,
                    SkipFolders = txtSkipFolders.Text
                };

            var renamer = new FolderDateRenamer(options);

            Logger.Log("Parsing images in " + options.Directory);

            renamer.ParseData();

            if (!options.TestMode)
            {
                Logger.Log(renamer.RenameQueue.Count + " folder renames in Queue.  Renaming now..");
                renamer.RenameFolders();    
            }
            else
            {
                Logger.Log(renamer.RenameQueue.Count + " folder renames in Queue.  Skipping rename because preview/test is checked.");
            }
            

            Logger.Log("Finished.");
        }

        #endregion UI
    }
}