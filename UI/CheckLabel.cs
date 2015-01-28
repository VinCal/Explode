using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CustomControls
{
    class CheckLabel: Label
    {
        public CheckLabel()
        {
            SetStyle(ControlStyles.StandardDoubleClick, false);
        }

        public bool Checked { get; set; }
        public CheckLabel checkLabel { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            BackColor = Checked ? Color.FromArgb(51, 153, 255) : Color.FromArgb(90, 90, 90);

            using (Pen pen = new Pen(Color.FromArgb(30, 30, 30)))
            {
                e.Graphics.DrawRectangle(pen, new Rectangle(ClientRectangle.Left, ClientRectangle.Top, ClientRectangle.Width - 1, ClientRectangle.Height - 1));
            }

            base.OnPaint(e);
        }
    }
}
