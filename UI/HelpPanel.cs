using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ExplodeScript.UI
{
    class HelpPanel : Panel
    {
        private string m_HelpString;
        private Font m_SmallerDefaultFont;
        private Timer m_Timer;
        private int m_Opacity = 255;

        public HelpPanel()
        {
            Size = new Size(284, 16);
            BackColor = Color.FromArgb(30, 30, 30);

            m_SmallerDefaultFont = new Font(SystemFonts.DefaultFont.FontFamily, 8);
            m_Timer = new Timer { Interval = 250 };
            m_Timer.Tick += m_Timer_Tick;

            DoubleBuffered = true;
        }

        void m_Timer_Tick(object sender, EventArgs e)
        {
            m_Opacity -= 10;
            if (m_Opacity <= 0)
            {
                m_Timer.Stop();
                m_Opacity = 0;
            }
            Refresh();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawString(m_HelpString, m_SmallerDefaultFont, new SolidBrush(Color.FromArgb(m_Opacity, Color.FromArgb(220, 220, 220))), 5, 2);

            base.OnPaint(e);
        }

        internal void Push(string p)
        {
            m_HelpString = p;
            m_Opacity = 255;
            m_Timer.Start();
            Refresh();
        }
    }
}
