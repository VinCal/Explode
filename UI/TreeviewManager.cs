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
        public void AddNode(TreeNodeEx lpNode)
        {
            //Only add if it hasn't been added already
            if (!m_NodeDictionary.ContainsKey(new Tuple<uint, ushort>(lpNode.uHandle, lpNode.matID)))
            {
                //Add the lpNode to the m_NodeDictionary
                m_NodeDictionary.Add(new Tuple<uint, ushort>(lpNode.uHandle, lpNode.matID), lpNode);

                InsertParentNode(lpNode);
                //Finally add the the node to the TreeView
                //TreeView.Nodes.Add(lpNode);
            }
        }
        

        public void AddNode(ParentNode parentNode, ushort matID)
        {
            if (!m_NodeDictionary.ContainsKey(new Tuple<uint, ushort>(parentNode.Handle, matID)))
            {
                TreeNodeEx treeLPNode;
                if (parentNode.IsPlaceHolder(matID)) //we can't use parentNode.name because it's a node property, not a materialID one
                {
                    treeLPNode = new TreeNodeEx(parentNode.Handle, matID, String.Format("{0} - MatID: {1}", "Missing LP", matID + 1), false)
                    {
                        ForeColor = Color.FromArgb(235, 63, 63)
                    };
                }
                else
                {
                    treeLPNode = new TreeNodeEx(parentNode.Handle, matID, String.Format("{0} - MatID: {1}", parentNode.Name, matID + 1), false);
                }

                m_NodeDictionary.Add(new Tuple<uint, ushort>(parentNode.Handle, matID), treeLPNode);

                //Get all children of parentNode
                var childHandles = parentNode.GetChildHandles(matID);
                foreach (uint childHandle in childHandles)
                {
                    //Get ChildNode at matId and handle
                    var childNode = parentNode.GetChild(matID, childHandle);

                    //Create TreeNode
                    var treeHPNode = new TreeNodeEx(childNode.Handle, matID, childNode.Name, true)
                    {
                        ParentHandle = parentNode.Handle
                    };

                    //Add HP node to the nodeDictionary
                    m_NodeDictionary.Add(new Tuple<uint, ushort>(childNode.Handle, matID), treeHPNode);

                    //Add hp node to lp Node
                    treeLPNode.Nodes.Add(treeHPNode);
                }

                InsertParentNode(treeLPNode);

                ////this is where we need to sort on ID, so we INSERT the LP node at the correct place
                //int insertIndex = -1;

                //for (int index = 0; index < TreeView.Nodes.Count - 1; index++)
                //{
                //    var lpNodes0 = TreeView.Nodes[index] as TreeNodeEx;
                //    var lpNodes1 = TreeView.Nodes[index + 1] as TreeNodeEx;

                //    //This means the current material ID is smaller than the first one in the list, so we can add it first, no need to look further
                //    if (matID < lpNodes0.matID)
                //    {
                //        insertIndex = index;
                //        break;
                //    }

                //    //Somewhere in between the nodes
                //    if (matID > lpNodes0.matID && matID < lpNodes1.matID)
                //    {
                //        insertIndex = index + 1;
                //    }
                //}
                ////At the end of the list
                //if (insertIndex == -1)
                //    insertIndex = TreeView.Nodes.Count;

                ////Add the LP node to the tree
                //TreeView.Nodes.Insert(insertIndex, treeLPNode);
                ////TreeView.Nodes.Add(treeLPNode);
            }
        }

        public void UpdateNode(ParentNode parentNode, ushort matID)
        {
            if (m_NodeDictionary.ContainsKey(new Tuple<uint, ushort>(parentNode.Handle, matID)))
            {
                //Get the treeNode to update:
                var lpTreeNode = m_NodeDictionary[new Tuple<uint, ushort>(parentNode.Handle, matID)];
                //Default back to white if the parentNode is no longer a PlaceHolder node. 
                if (!parentNode.IsPlaceHolder(matID))
                {
                    lpTreeNode.Text = String.Format("{0} - MatID: {1}", parentNode.Name, matID + 1);
                    lpTreeNode.ForeColor = Color.FromArgb(220, 220, 220);
                }
                else
                {
                    lpTreeNode.Text = String.Format("{0} - MatID: {1}", "Missing LP", matID + 1);
                    lpTreeNode.ForeColor = Color.FromArgb(235, 63, 63);
                }

                //Update the values
                
                lpTreeNode.uHandle = parentNode.Handle;
                lpTreeNode.matID = matID;

                //Get all children of parentNode
                var childHandles = parentNode.GetChildHandles(matID);
                foreach (uint childHandle in childHandles)
                {
                    //Get ChildNode at matId and handle
                    var childNode = parentNode.GetChild(matID, childHandle);

                    var hpTreeNode = m_NodeDictionary[new Tuple<uint, ushort>(childNode.Handle, matID)];
                    hpTreeNode.Name = childNode.Name;
                    hpTreeNode.uHandle = childNode.Handle;
                    hpTreeNode.matID = matID;
                    hpTreeNode.ParentHandle = parentNode.Handle;
                }
            }
        }

        public void AddChildNode(ParentNode parentNode, uint hpHandle, ushort id)
        {
            //Check if the lpNode already exists - otherwise we're trying to add a childNode to a non-existing parentNode
            if (m_NodeDictionary.ContainsKey(new Tuple<uint, ushort>(parentNode.Handle, id)))
            {
                //Get LP TreeNode
                var treeLPNode = m_NodeDictionary[new Tuple<uint, ushort>(parentNode.Handle, id)];

                //Get childNode
                var childNode = parentNode.GetChild(id, hpHandle);

                //Create new HP treeNode
                var treeHPNode = new TreeNodeEx(childNode.Handle, id, childNode.Name, true)
                {
                    ParentHandle = parentNode.Handle
                };

                //Add the newly made HP treeNode to the m_NodeDictionary
                m_NodeDictionary.Add(new Tuple<uint, ushort>(hpHandle, id), treeHPNode);

                //We should sort it here, so that the list is sorted all the way through even when adding new nodes and WHAT NOT
                //TreeView.Nodes.Insert()

                //Add the hpTreeNode to the LPNode
                treeLPNode.Nodes.Add(treeHPNode);
            }
        }


        /// <summary>
        /// Deletes node from Treeview and internal m_NodeDictionary (keeps a list of all nodes Tuple uint, ushort)
        /// </summary>
        public void DeleteNode(uint handle, ushort id)
        {
            //If this key does not exist it means we've already deleted it, usually a second callback from EditablePoly
            if (m_NodeDictionary.ContainsKey(new Tuple<uint, ushort>(handle, id)))
            {
                //Get treenode at given handle, mat id
                var treeNode = m_NodeDictionary[new Tuple<uint, ushort>(handle, id)];

                //Remove the treeNode
                TreeView.Nodes.Remove(treeNode);
                //Remove from Dictionary too
                m_NodeDictionary.Remove(new Tuple<uint, ushort>(handle, id));
            }
        }

        public void DeleteNodes(List<uint> handles, ushort id)
        {
            //If this key does not exist it means we've already deleted it, usually a second callback from EditablePoly
            foreach (uint handle in handles)
            {
                if (m_NodeDictionary.ContainsKey(new Tuple<uint, ushort>(handle, id)))
                {
                    //Get treenode at given handle, mat id
                    var treeNode = m_NodeDictionary[new Tuple<uint, ushort>(handle, id)];

                    //Remove the treeNode
                    TreeView.Nodes.Remove(treeNode);
                    //Remove from Dictionary too
                    m_NodeDictionary.Remove(new Tuple<uint, ushort>(handle, id));
                }
            }
        }


        internal void DeleteChildNode(TreeNodeEx panelClickedNode)
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


        internal void DeleteParentNode(ParentNode parentNode, TreeNodeEx panelClickedNode)
        {
            if (m_NodeDictionary.ContainsKey(new Tuple<uint, ushort>(panelClickedNode.uHandle, panelClickedNode.matID)))
            {
                //Get treenode at given handle, mat id
                var treeNode = m_NodeDictionary[new Tuple<uint, ushort>(panelClickedNode.uHandle, panelClickedNode.matID)];

                //Remove the treeNode
                TreeView.Nodes.Remove(treeNode);

                //Remove from Dictionary too
                m_NodeDictionary.Remove(new Tuple<uint, ushort>(panelClickedNode.uHandle, panelClickedNode.matID));

                //We need to remove all children from Dictionary too
                //Get all children handles for this ID
                var childHandles = parentNode.GetChildHandles(panelClickedNode.matID);
                foreach (uint childHandle in childHandles)
                {
                    //Remove from Dictionary
                    m_NodeDictionary.Remove(new Tuple<uint, ushort>(childHandle, panelClickedNode.matID));
                }
            }
        }

        private void InsertParentNode(TreeNodeEx node)
        {
            //this is where we need to sort on ID, so we INSERT the LP node at the correct place
            int insertIndex = -1;

            for (int index = 0; index < TreeView.Nodes.Count - 1; index++)
            {
                var lpNodes0 = TreeView.Nodes[index] as TreeNodeEx;
                var lpNodes1 = TreeView.Nodes[index + 1] as TreeNodeEx;

                //This means the current material ID is smaller than the first one in the list, so we can add it first, no need to look further
                if (node.matID < lpNodes0.matID)
                {
                    insertIndex = index;
                    break;
                }

                //Somewhere in between the nodes
                if (node.matID > lpNodes0.matID && node.matID < lpNodes1.matID)
                {
                    insertIndex = index + 1;
                }
            }
            //At the end of the list
            if (insertIndex == -1)
                insertIndex = TreeView.Nodes.Count;

            //Add the LP node to the tree
            TreeView.Nodes.Insert(insertIndex, node);
        }

    }
}
