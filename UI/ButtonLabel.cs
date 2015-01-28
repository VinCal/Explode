using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CustomControls
{
    class ButtonLabel: Label
    {
        private bool m_MouseDown, m_MouseUp;

        public ButtonLabel()
        {
            //Disable DoubleClick
            SetStyle(ControlStyles.StandardDoubleClick, false);
        }

        public bool MouseDownProperty
        {
            get { return m_MouseDown; }
            set 
            { 
                m_MouseDown = value;
                m_MouseUp = !value;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            MouseDownProperty = true;
            Refresh();

            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            MouseDownProperty = false;
            Refresh();

            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            BackColor = MouseDownProperty ? Color.FromArgb(51, 153, 255) : Color.FromArgb(90, 90, 90);

            using (Pen pen = new Pen(Color.FromArgb(30, 30, 30)))
                e.Graphics.DrawRectangle(pen, new Rectangle(ClientRectangle.Left, ClientRectangle.Top, ClientRectangle.Width - 1, ClientRectangle.Height - 1));
            
            base.OnPaint(e);
        }
    }
}
