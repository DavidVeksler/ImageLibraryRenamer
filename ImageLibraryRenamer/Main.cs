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

            Logger = new ActivityLogger(lbStatus);
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
            Logger.Clear();

            Logger.Log("Starting folder renamer...");

            tabControl1.SelectTab(1);

            Application.DoEvents();

            var options = new FolderDateRenamer.RenameFoldersParams(txtFileNamePattern.Text,
                                                                    txtDatePattern.Text, chkUseEXIFDataToGetDate.Checked,
                                                                    chkUseFileDateIfNoEXIF.Checked, chkRecusrive.Checked,
                                                                    chkPreview.Checked)
                {
                    SkipTopLevel = chkSkipTopLevel.Checked,
                    SkipNumeric = chkSkipNumeric.Checked,
                    SkipFolders = txtSkipFolders.Text,
                    Logger = Logger,
                    SkipIfFolderHasXmpFile = chkSkipIfXmp.Checked,
                    SkipIfFolderNameAlreadyHasDate = chkSkipIfFolderNameAlreadyHasDate.Checked
                };

            var renamer = new FolderDateRenamer(options);

            Logger.Log("Parsing images in " + txtPath.Text);

            renamer.ParseImageDates(txtPath.Text);

            if (!options.TestMode)
            {
                Logger.Log(renamer.RenameQueue.Count + " folder renames in Queue.  Renaming now..");

                DialogResult response =
                    MessageBox.Show(string.Format("Rename {0} folders now?", renamer.RenameQueue.Count),
                                    "Confirm Rename", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (response == DialogResult.Yes)
                {
                    renamer.RenameFolders();
                }
                else
                {
                    Logger.Log("Cancelled rename");
                }
            }
            else
            {
                Logger.Log(renamer.RenameQueue.Count +
                           " folder renames in Queue.  Skipping rename because preview/test is checked.");
            }


            Logger.Log("Done.");
        }


        private void btnEmbedPicasaProperties_Click(object sender, EventArgs e)
        {
            Logger.Clear();
            tabControl1.SelectTab(1);

            Logger.Log("Starting picasa parser...");
            var embdedder = new EmbdedPicasaProperties();
            embdedder.ParseFolder(txtPath.Text, Logger);
        }

        #endregion UI

    }
}