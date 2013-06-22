using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ImageLibraryRenamer
{
    public class ActivityLogger
    {
        private readonly ListBox _status;

        public ActivityLogger(ListBox status)
        {
            this._status = status;
        }

        #region ActivityLogger

        public void FatalLog(string status)
        {
          //  MessageBox.Show(status, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log(status);
            Application.DoEvents();
        }

        public void Log(string status)
        {
            _status.Items.Add(DateTime.Now.ToShortTimeString() + " " + status);
            Application.DoEvents();
        }

        public void LogAppendToLast(string status)
        {
            _status.Items[_status.Items.Count - 1] += status;
            Application.DoEvents();
        }

        #endregion

        public void Clear()
        {
            _status.Items.Clear();
        }
    }
}
