using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Test_ExplodeScript;

namespace ExplodeScript.UI
{
    class PanelEx : Panel
    {
        public PanelEx()
        {
            SetStyle(ControlStyles.StandardDoubleClick, false);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (Pen pen = new Pen(Color.FromArgb(90, 90, 90)))
                e.Graphics.DrawRectangle(pen, new Rectangle(ClientRectangle.Left, ClientRectangle.Top, ClientRectangle.Width - 1, ClientRectangle.Height - 1));
            
            base.OnPaint(e);
        }
    }

    public sealed class TreeviewManager : Panel
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, Int32 wMsg, bool wParam, Int32 lParam);
        private const int WM_SETREDRAW = 11;

        private const int m_Xpos = 17;
        private const int m_Ypos = 18;

        private PanelEx[] m_DeletePanelArray;


        //keeps track of all nodes in TreeView for easy look up by handle / ID
        private Dictionary<Tuple<uint, ushort>, TreeNodeEx> m_NodeDictionary;
        
        public TreeView TreeView { get; set; }
        public Controller Controller { private get; set; }

        public TreeviewManager()
        {
            Location = new Point(1, m_Ypos);
            Size = new Size(284, 288);
            BackColor = Color.FromArgb(90, 90, 90);

            TreeView = new TreeView
            {
                Location = new Point(m_Xpos, 0),
                Size = new Size(268,288),
                BackColor = Color.FromArgb(90,90,90),
                ForeColor = Color.FromArgb(220,220,220),
                ItemHeight = 16,
                FullRowSelect = true,
                ShowLines = false,
                BorderStyle = BorderStyle.None,
                HideSelection = false
            };
            TreeView.NodeMouseClick += treeview_NodeMouseClick;
            TreeView.AfterCollapse += TreeView_AfterCollapse;
            TreeView.AfterExpand += TreeView_AfterExpand;

            m_DeletePanelArray = new PanelEx[19];

            for (int i = 0; i < m_DeletePanelArray.Length; i++)
            {
                var panel = new PanelEx
                {
                    Size = new Size(16,16),
                    Location = new Point(1, 16 * i),
                    BackColor = Color.FromArgb(68, 68, 68),
                    Visible = false
                };
               
                panel.Click += panel_Click;

                m_DeletePanelArray[i] = panel;
                Controls.Add(panel);
            }

            Controls.Add(TreeView);

            m_NodeDictionary = new Dictionary<Tuple<uint, ushort>, TreeNodeEx>();
        }

        void panel_Click(object sender, EventArgs e)
        {
            var pointRelativeToTreeview = PointToClient(Cursor.Position);
            var panelClickedNode = TreeView.GetNodeAt(pointRelativeToTreeview) as TreeNodeEx;

            //we want to delete this node from out list
            Controller.DeleteTreeNode(panelClickedNode);

            DebugMethods.Log(panelClickedNode.Text);
        }

        void treeview_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            var hitTest = TreeView.HitTest(e.Location);
            if (hitTest.Location != TreeViewHitTestLocations.PlusMinus) //click on the min plus sign
            {
                TreeView.BeginUpdate();
                SuspendLayout();

                //DebugMethods.Log("Node clicked");

                var selectedNode = e.Node as TreeNodeEx;
                Controller.UpdateSubObjectSelection(selectedNode);

                ResumeLayout();
                TreeView.EndUpdate();
            }
        }

        private void CheckVisibleNodes(TreeNode node, ref int visibleCount)
        {
            foreach (TreeNode nodeFor in node.Nodes)
                if (nodeFor.IsVisible)
                    ++visibleCount;
        }

        public void UpdateDeletePanels()
        {
            int visibleCount = 0;
            //We need to test how many nodes are visible now and adjust our delete buttons accordingly 
            foreach (TreeNode node in TreeView.Nodes)
            {
                if (node.IsVisible)
                    ++visibleCount;

                CheckVisibleNodes(node, ref visibleCount);
            }

            SuspendDrawing();
            for (int i = 0; i < m_DeletePanelArray.Length; i++)
            {
                m_DeletePanelArray[i].Visible = false;
            }

            for (int i = 0; i < visibleCount; i++)
            {
                m_DeletePanelArray[i].Visible = true;
            }
            ResumeDrawing();
        }

        void TreeView_AfterCollapse(object sender, TreeViewEventArgs e)
        {
            int visibleCount = 0;
            //We need to test how many nodes are visible now and adjust our delete buttons accordingly 
            foreach (TreeNode node in TreeView.Nodes)
            {
                if (node.IsVisible)
                    ++visibleCount;

                CheckVisibleNodes(node, ref visibleCount);
            }

            SuspendDrawing();
            for (int i = 0; i < m_DeletePanelArray.Length; i++)
            {
                m_DeletePanelArray[i].Visible = false;
            }

            for (int i = 0; i < visibleCount; i++)
            {
                m_DeletePanelArray[i].Visible = true;
            }
            ResumeDrawing();
        }


        void TreeView_AfterExpand(object sender, TreeViewEventArgs e)
        {
            int visibleCount = 0;
            //We need to test how many nodes are visible now and adjust our delete buttons accordingly 
            foreach (TreeNode node in TreeView.Nodes)
            {
                if (node.IsVisible)
                    ++visibleCount;

                CheckVisibleNodes(node, ref visibleCount);
            }

            SuspendDrawing();
            for (int i = 0; i < m_DeletePanelArray.Length; i++)
            {
                m_DeletePanelArray[i].Visible = false;
            }

            for (int i = 0; i < visibleCount; i++)
            {
                m_DeletePanelArray[i].Visible = true;
            }
            ResumeDrawing();
        }

        public void SuspendDrawing()
        {
            SendMessage(Handle, WM_SETREDRAW, false, 0);
        }

        public void ResumeDrawing()
        {
            SendMessage(Handle, WM_SETREDRAW, true, 0);
            Refresh();
        }

        /// <summary>
        /// Clears everything TreeView related
        /// </summary>
        public void Clear()
        {
            //Clears all the nodes in the treeview
            TreeView.Nodes.Clear();
            //Clears all the delete Panels - Update checks visible nodes, but we just cleared them
            UpdateDeletePanels();
            //Clear the nodeDictionary too
            m_NodeDictionary.Clear();
        }

        /// <summary>
        /// Adds nodes to the treeview and also the internal m_NodeDictionary (keeps a list of all nodes Tuple uint, ushort)
        /// </summary>
        /// <param name="lpNode"></param>
        public void AddNodes(TreeNodeEx lpNode)
        {
            //Only add if it hasn't been added already
            if (!m_NodeDictionary.ContainsKey(new Tuple<uint, ushort>(lpNode.uHandle, lpNode.matID)))
            {
                //Add the lpNode to the m_NodeDictionary
                m_NodeDictionary.Add(new Tuple<uint, ushort>(lpNode.uHandle, lpNode.matID), lpNode);

                //Add all the possible children too
                foreach (TreeNodeEx node in lpNode.Nodes)
                {
                    m_NodeDictionary.Add(new Tuple<uint, ushort>(node.uHandle, node.matID), lpNode);
                }

                //Finally add the the node to the TreeView
                TreeView.Nodes.Add(lpNode);
            }
        }


        /// <summary>
        /// Deletes node from Treeview and internal m_NodeDictionary (keeps a list of all nodes Tuple uint, ushort)
        /// </summary>
        public void DeleteNode(uint lpHandle, ushort lpID)
        {
            //If this key does not exist it means we've already deleted it, usually a second callback from EditablePoly
            if (m_NodeDictionary.ContainsKey(new Tuple<uint, ushort>(lpHandle, lpID)))
            {
                //Get treenode at given handle, mat id
                var treeNode = m_NodeDictionary[new Tuple<uint, ushort>(lpHandle, lpID)];

                //Remove the treeNode
                TreeView.Nodes.Remove(treeNode);
                //Remove from Dictionary too
                m_NodeDictionary.Remove(new Tuple<uint, ushort>(lpHandle, lpID));
            }
        }


        internal void DeleteNode(TreeNodeEx panelClickedNode)
        {
            if (m_NodeDictionary.ContainsKey(new Tuple<uint, ushort>(panelClickedNode.uHandle, panelClickedNode.matID)))
            {
                //Get treenode at given handle, mat id
                var treeNode = m_NodeDictionary[new Tuple<uint, ushort>(panelClickedNode.uHandle, panelClickedNode.matID)];

                //Remove the treeNode
                TreeView.Nodes.Remove(treeNode);
                //Remove from Dictionary too
                m_NodeDictionary.Remove(new Tuple<uint, ushort>(panelClickedNode.uHandle, panelClickedNode.matID));
            }
        }
    }
}
