using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Max;
using ExplodeScript.UI;
using ManagedServices;
using MaxCustomControls;
using CustomControls;
using Test_ExplodeScript.UI;

namespace Test_ExplodeScript
{
    public class ExplodeView
    {
        private readonly IGlobal m_Global;

        //private readonly BufferedTreeView m_TreeView;
        private MaxFormEx m_MaxForm;
        private ButtonLabel m_ExplodeAndExportButton;
        private HelpPanel m_HelpPanel;

        public IGlobal Global
        {
            get { return m_Global; }
        }

        private Controller m_Controller;

        public ExplodeView(IGlobal global)
        {
            m_Global = global;
            //Pass m_Global to DebugMethod class so it can print to the listener 
            DebugMethods.SetGlobal(m_Global);

            //Create UI elements:
            m_MaxForm = new MaxFormEx
            {
                Size = new Size(302, 500),
                Text = "Explode",
                BackColor = Color.FromArgb(68, 68, 68),
                FormBorderStyle = FormBorderStyle.None
            };

            var treeviewManager = new TreeviewManager();

            //Create Controller
            m_Controller = new Controller(this, treeviewManager);

            //give treeview a controller 
            treeviewManager.Controller = m_Controller;

            //Hook up event
            m_Controller.ExplodeChanged += m_Controller_ExplodeChanged;
            m_Controller.DebugTextChanged += m_Controller_DebugTextChanged;
            
            //Create UI
            m_HelpPanel = new HelpPanel
            {
                Location = new Point(1, 306),
            };

            var addButton = new ButtonLabel
            {
                Size = new Size(125, 25),
                Location = new Point(10, 327),
                BackColor = Color.FromArgb(90, 90, 90),
                ForeColor = Color.FromArgb(220, 220, 220),
                Text = "Add LP",
                TextAlign = ContentAlignment.MiddleCenter,
                MouseDownProperty = false
            };
            addButton.MouseUp += addButton_MouseUp;

            var addHPButton = new ButtonLabel
            {
                Size = new Size(125, 25),
                Location = new Point(151, 327),
                BackColor = Color.FromArgb(90, 90, 90),
                ForeColor = Color.FromArgb(220, 220, 220),
                Text = "Add HP",
                TextAlign = ContentAlignment.MiddleCenter,
                MouseDownProperty = false
            };
            addHPButton.MouseUp += addHPButton_MouseUp;

            var explodeButton = new ButtonLabel
            {
                Size = new Size(125, 25),
                Location = new Point(10, 377),
                BackColor = Color.FromArgb(90, 90, 90),
                ForeColor = Color.FromArgb(220, 220, 220),
                Text = "Explode",
                TextAlign = ContentAlignment.MiddleCenter,
                MouseDownProperty = false
            };
            explodeButton.MouseUp += explodeButton_MouseUp;

            var exportButton = new ButtonLabel
            {
                Size = new Size(125, 25),
                Location = new Point(151, 377),
                BackColor = Color.FromArgb(90, 90, 90),
                ForeColor = Color.FromArgb(220, 220, 220),
                Text = "Export",
                TextAlign = ContentAlignment.MiddleCenter,
                MouseDownProperty = false
            };
            exportButton.MouseUp += exportButton_MouseUp;

            m_ExplodeAndExportButton = new ButtonLabel
            {
                Size = new Size(266, 25),
                Location = new Point(10, 409),
                BackColor = Color.FromArgb(90, 90, 90),
                ForeColor = Color.FromArgb(220, 220, 220),
                Text = "Explode & Export",
                TextAlign = ContentAlignment.MiddleCenter,
                MouseDownProperty = false
            };
            m_ExplodeAndExportButton.MouseUp += explodeAndExportButton_MouseUp;

            //Add Controls
            m_MaxForm.Controls.Add(treeviewManager); 
            m_MaxForm.Controls.Add(m_HelpPanel);
            m_MaxForm.Controls.Add(addButton);
            m_MaxForm.Controls.Add(addHPButton);
            m_MaxForm.Controls.Add(explodeButton);
            m_MaxForm.Controls.Add(exportButton);
            m_MaxForm.Controls.Add(m_ExplodeAndExportButton);

            //Show form
            IntPtr maxHandle = global.COREInterface.MAXHWnd;
            m_MaxForm.Show(new ArbitraryWindow(maxHandle));


#if DEBUG
            Form debugForm = new Form();
            debugForm.Size = new Size(300, 300);
            debugForm.Show(new ArbitraryWindow(maxHandle));
            debugForm.Closing += debugForm_Closing;

            Button button = new Button();
            button.Size = new Size(200, 25);
            button.Location = new Point(10, 10);
            button.Text = "Create BB for Parent Node";
            button.Click +=button_Click;

            Button movebutton = new Button();
            movebutton.Size = new Size(200, 25);
            movebutton.Location = new Point(10, 90);
            movebutton.Text = "movesomething";
            movebutton.Click += movebutton_Click;

            Button getVertInfoFromID = new Button();
            getVertInfoFromID.Size = new Size(200, 25);
            getVertInfoFromID.Location = new Point(10, 50);
            getVertInfoFromID.Text = "Close";
            getVertInfoFromID.Click += getVertInfoFromID_Click;

            debugForm.Controls.Add(button);
            debugForm.Controls.Add(getVertInfoFromID);
            debugForm.Controls.Add(movebutton);
#endif
        }


        void m_Controller_ExplodeChanged(bool exploded)
        {
            m_ExplodeAndExportButton.Text = exploded ? "Collapse" : "Explode";
        }

        void m_Controller_DebugTextChanged(string debugString)
        {
            m_HelpPanel.Push(debugString, true);
        }


        void movebutton_Click(object sender, EventArgs e)
        {
            //m_Controller.DebugMoveSelectNodeVerts();
            //m_Controller.CheckForExtraCollision();
        }

        void debugForm_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            m_MaxForm.Close();
        }

        void getVertInfoFromID_Click(object sender, EventArgs e)
        {
            //m_Controller.PrintVertexPos((ushort)(((sender as Button).Tag) as NumericUpDown).Value);
            m_Controller.Cleanup();

            m_MaxForm.Close();
            ((sender as Button).Parent as Form).Close();
        }

        void button_Click(object sender, EventArgs e)
        {
            m_Controller.CreateDebugBoundingBox();
        }

        void addButton_MouseUp(object sender, MouseEventArgs e)
        {
            if (m_Controller.AddLPObjects())
                m_Controller.PopulateTreeview();
        }

        void addHPButton_MouseUp(object sender, MouseEventArgs e)
        {
            m_Controller.AddHPObject();
            //Todo don't call populateTreeview if nothing was added.
            //m_Controller.PopulateTreeview();
        }

        void explodeButton_MouseUp(object sender, MouseEventArgs e)
        {
            m_HelpPanel.Push("> Explode", true);
        }

        void exportButton_MouseUp(object sender, MouseEventArgs e)
        {
            m_HelpPanel.Push("> Exporting", false);
        }

        void explodeAndExportButton_MouseUp(object sender, MouseEventArgs e)
        {
            //m_MaxForm.Close();
            m_Controller.Explode();
        }


        //void labelIDSplit_Click(object sender, EventArgs e)
        //{
        //    var idLabel = sender as CheckLabel;
        //    var elementLabel = (sender as CheckLabel).checkLabel;

        //    if (!idLabel.Checked)
        //    {
        //        if (elementLabel.Checked)
        //            elementLabel.Checked = false;

        //        idLabel.Checked = true;
        //    }
        //    else
        //    {
        //        idLabel.Checked = false;
        //    }

        //    idLabel.Refresh();
        //    elementLabel.Refresh();

        //   m_MaxForm.Close();
        //    //Send updated values to Controller
        //}

        //void labelElementSplit_Click(object sender, EventArgs e)
        //{
        //    var elementLabel = sender as CheckLabel;
        //    var idLabel = (sender as CheckLabel).checkLabel;

        //    if (!elementLabel.Checked)
        //    {
        //        if (idLabel.Checked)
        //            idLabel.Checked = false;

        //        elementLabel.Checked = true;
        //    }
        //    else
        //    {
        //        elementLabel.Checked = false;
        //    }

        //    elementLabel.Refresh();
        //    idLabel.Refresh();

        //    //Send updated values to Controller
        //}

        
    }
}
