using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Max;
using Autodesk.Max.Plugins;
using ExplodeScript;
using ExplodeScript.UI;
using NewControls;
using Object = Autodesk.Max.Plugins.Object;

namespace Test_ExplodeScript
{

    //class ExplodedObjRestore : IRestoreObj
    //{
    //    private IMesh m_OriginalMesh;
    //    private IINode m_Node;
    //    private IGlobal m_Global;

    //    public ExplodedObjRestore(IMesh originalMesh, IINode node, IGlobal global)
    //    {
    //        m_OriginalMesh = originalMesh;
    //        m_Node = node;
    //        m_Global = global;
    //    }

    //    public string Description
    //    {
    //        get { return "Explode Objects"; }
    //    }

    //    public void EndHold()
    //    {
    //    }

    //    public IntPtr Execute(int cmd, UIntPtr arg1, UIntPtr arg2, UIntPtr arg3)
    //    {
    //        return IntPtr.Zero;
    //    }

    //    public void Redo()
    //    {
    //        IObject baseObjectRef = m_Node.ObjectRef.FindBaseObject();
    //        if (baseObjectRef.ClassID.OperatorEquals(m_Global.Class_ID.Create(0xe44f10b3, 0)) == 1)
    //        {
    //            ITriObject triObj = baseObjectRef as ITriObject;
    //            triObj.Mesh = m_OriginalMesh;
    //        }
    //    }

    //    public void Restore(bool isUndo)
    //    {
    //        if (isUndo)
    //        {
    //            IObject baseObjectRef = m_Node.ObjectRef.FindBaseObject();
    //            if (baseObjectRef.ClassID.OperatorEquals(m_Global.Class_ID.Create(0xe44f10b3, 0)) == 1)
    //            {
    //                ITriObject triObj = baseObjectRef as ITriObject;
    //                triObj.Mesh = m_OriginalMesh;
    //            }
    //        }
    //    }

    //    public int Size
    //    {
    //        get { return 3; }
    //    }

    //    public IBaseInterface GetInterface(IInterface_ID id)
    //    {
    //        return null;
    //    }

    //    public bool Equals(IInterfaceServer other)
    //    {
    //        return false;
    //    }

    //    public void Dispose()
    //    {

    //    }

    //    public IntPtr Handle
    //    {
    //        get { return IntPtr.Zero; }
    //    }
    //}

  
    class NodeEventCallbacks : INodeEventCallback
    {
        private HashSet<uint> m_WatchLPNodeList = new HashSet<uint>();
        private HashSet<uint> m_WatchHPNodeList = new HashSet<uint>();
        private Controller m_Controller;

        public NodeEventCallbacks(Controller controller)
        {
            m_Controller = controller;
        }

        public void SetLPNodeToWatch(uint nodeHandle)
        {
            m_WatchLPNodeList.Add(nodeHandle);
        }

        public void SetHPNodeToWatch(uint nodeHandle)
        {
            m_WatchHPNodeList.Add(nodeHandle);
        }

        public override void TopologyChanged(ITab<UIntPtr> nodes)
        {
            //Material ID
            //DebugMethods.Log("topology changed");

            var global = GlobalInterface.Instance;

            for (int i = 0; i < nodes.Count; i++)
            {
                using (var node = global.NodeEventNamespace.GetNodeByKey(nodes[(IntPtr)i]))
                {
                    m_Controller.UpdateMaterialIDChange(node.Handle);

                    //foreach (var watchHandles in m_WatchLPNodeList)
                    //{
                    //    if (node.Handle == watchHandles)
                    //    {
                    //        IObject baseObjectRef = node.ObjectRef.FindBaseObject();
                    //        bool isPolyObject = baseObjectRef.ClassID.OperatorEquals(global.Class_ID.Create(0x1bf8338d, 0x192f6098)) == 1;
                    //        m_Controller.UpdateMaterialIDChange(node.Handle);
                    //    }
                    //}
                    //foreach (var watchHandles in m_WatchHPNodeList)
                    //{
                    //    if (node.Handle == watchHandles)
                    //    {
                    //        IObject baseObjectRef = node.ObjectRef.FindBaseObject();
                    //        bool isPolyObject = baseObjectRef.ClassID.OperatorEquals(global.Class_ID.Create(0x1bf8338d, 0x192f6098)) == 1;
                    //        m_Controller.UpdateAfterCollapse(node.Handle, isPolyObject, false);
                    //    }
                    //}
                }
            }

            base.GeometryChanged(nodes);
        }

        public override void ModelStructured(ITab<UIntPtr> nodes)
        {
            //Editable poly or edtiable mesh can be tracked here 
            //DebugMethods.Log("Model structure Changed");
            var global = GlobalInterface.Instance;
            
            for (int i = 0; i < nodes.Count; i++)
            {
                using (var node = global.NodeEventNamespace.GetNodeByKey(nodes[(IntPtr)i]))
                {
                    foreach (var watchHandles in m_WatchLPNodeList)
                    {
                        if (node.Handle == watchHandles)
                        {
                            IObject baseObjectRef = node.ObjectRef.FindBaseObject();
                            bool isPolyObject = baseObjectRef.ClassID.OperatorEquals(global.Class_ID.Create(0x1bf8338d, 0x192f6098)) == 1;
                            m_Controller.UpdateAfterCollapse(node.Handle, isPolyObject, true);
                        }
                    }
                    foreach (var watchHandles in m_WatchHPNodeList)
                    {
                        if (node.Handle == watchHandles)
                        {
                            IObject baseObjectRef = node.ObjectRef.FindBaseObject();
                            bool isPolyObject = baseObjectRef.ClassID.OperatorEquals(global.Class_ID.Create(0x1bf8338d, 0x192f6098)) == 1;
                            m_Controller.UpdateAfterCollapse(node.Handle, isPolyObject, false);
                        }
                    }
                }
            }

            base.ModelStructured(nodes);
        }
    }


    public class Controller
    {
        //private Dictionary<uint, Dictionary<ushort, ParentNode>> m_ParentNodeDictionary = new Dictionary<uint, Dictionary<ushort, ParentNode>>();

        private Dictionary<uint, ParentNode> m_RealParentNodeDictionary = new Dictionary<uint, ParentNode>(); 

        private HashSet<IPoint3> m_VertList = new HashSet<IPoint3>();

        private IGlobal m_Global;
        private TreeviewManager m_TreeviewManager;
        private List<BoundingBoxHandleID> m_SortedList;


        private bool m_Exploded = false;

        private NodeEventCallbacks m_NodeEventCallback;

        public delegate void ExplodeHandler(bool exploded);
        public event ExplodeHandler ExplodeChanged;
        public bool Exploded
        {
            get
            {
                return m_Exploded;
            }
            set
            {
                if (m_Exploded != value)
                    ExplodeChanged(value);
            }
        }

        public delegate void DebugTextHandler(string debugString);
        public event DebugTextHandler DebugTextChanged;

        public string DebugText
        {
            set
            {
                DebugTextChanged(value);
            }
        }



        #region Max Datamembers
        private const int TASK_MODE_MODIFY = 2;
        #endregion

        public Controller(ExplodeView view, TreeviewManager treeview)
        {
            m_Global = view.Global;

            ObjectMethods.SetGlobal(m_Global);

            m_TreeviewManager = treeview;
        }

        public void UpdateSubObjectSelection(TreeNodeEx treeNode)
        {
            uint selectedHandle = treeNode.uHandle;
            uint parentHandle = treeNode.ParentHandle;
            ushort matID = treeNode.matID;

            IInterface iface = m_Global.COREInterface;
            IINode node = iface.GetINodeByHandle(selectedHandle);

            //If for some reason there is no node with that handle - return
            if (node == null)
                return;

            //Get current node selection - if it's the same as our node we don't need to reselect
            if (iface.SelNodeCount == 1)
            {
                IINode selectedNode = iface.GetSelNode(0);

                if (!node.Equals(selectedNode as IInterfaceServer))
                    iface.SelectNode(node, true);
            }
            else
                iface.SelectNode(node, true);

            //if (!treeNode.isChild)
            //{
            //    if (m_RealParentNodeDictionary[selectedHandle].IsPlaceHolder(matID))
            //    {
            //        iface.CommandPanelTaskMode = TASK_MODE_MODIFY;
            //        IObject baseObjectRef = node.ObjectRef.FindBaseObject();
            //        m_Global.COREInterface7.SetCurEditObject(baseObjectRef, null);
            //        iface.SetSubObjectLevel(4, true);


            //    }
            //}

            iface.CommandPanelTaskMode = TASK_MODE_MODIFY;
            IObject baseObjectRef = node.ObjectRef.FindBaseObject();
            m_Global.COREInterface7.SetCurEditObject(baseObjectRef, null);
            iface.SetSubObjectLevel(4, true);

            BaseNode baseNode;
            if (!treeNode.isChild)
                baseNode = m_RealParentNodeDictionary[selectedHandle];
            else
                baseNode = m_RealParentNodeDictionary[parentHandle].GetChild(matID, selectedHandle);

            var parentNode = baseNode as ParentNode;
            if (baseNode.Mesh.PolyMesh != null) //editable poly
            {
                var mesh = baseNode.Mesh.PolyMesh;
                if (parentNode != null && parentNode.IsPlaceHolder(matID))
                {
                    IBitArray empty = m_Global.BitArray.Create();
                    mesh.FaceSelect(empty);
                }
                else
                {
                    ConvertBitArrayToIBitArray(baseNode, matID);
                    mesh.FaceSelect(baseNode.GetSelectionBitArray(matID));
                }
            }

            else //mesh
            {
                var mesh = baseNode.Mesh.TriMesh;
                if (parentNode != null && parentNode.IsPlaceHolder(matID))
                {
                    IBitArray empty = m_Global.BitArray.Create();
                    mesh.FaceSel = empty;
                }
                else
                {
                    ConvertBitArrayToIBitArray(baseNode, matID);
                    mesh.FaceSel = baseNode.GetSelectionBitArray(matID);
                }
            }

            //This line makes it so the commandPanel updates again showing the correct Material ID for the selection
            m_Global.COREInterface7.SetCurEditObject(baseObjectRef, null);
            iface.ForceCompleteRedraw(true);
        }

        private void ConvertBitArrayToIBitArray(BaseNode node, ushort matID)
        {
            IBitArray selectionBitArray = node.GetSelectionBitArray(matID);
            BitArray computedBitArray = node.GetMaterialBitArray(matID);

            //If the size of our selection bit array is different than the one we have - set the size!
            if (selectionBitArray.Size != computedBitArray.Count)
                selectionBitArray.SetSize(computedBitArray.Count, 0);

            for (int i = 0; i < computedBitArray.Count; i++)
            {
                if (computedBitArray[i])
                    selectionBitArray.Set(i);
            }
        }


        private GlobalDelegates.Delegate4 m_SceneResetHandler;
        private uint m_NodeEventID;

        void Global_SceneReset(IntPtr obj, IntPtr info)
        {
            DebugMethods.Log("Scene has been reset");
            //Clear our array
            if (m_RealParentNodeDictionary != null) //this could never really be null but, maybe if every node was removed 
                m_RealParentNodeDictionary.Clear();

            //Clear this one too in case
            if (m_SortedList != null)
                m_SortedList.Clear();

            //Unregister our node event callback
            if (m_NodeEventCallback != null)
            {
                m_Global.ISceneEventManager.UnRegisterCallback(m_NodeEventID);
                m_NodeEventCallback = null;
                m_NodeEventID = 0;
            }
            
            //Update our treeview to reflect this
            if (m_TreeviewManager != null)
                m_TreeviewManager.Clear();
            
            //Reset our UI too
            Exploded = false;
        }

        public void Cleanup()
        {
            m_TreeviewManager.Dispose();
            m_TreeviewManager = null;

            if (m_RealParentNodeDictionary != null) //this could never really be null but, maybe if every node was removed 
                m_RealParentNodeDictionary.Clear();

            //Clear this one too in case
            if (m_SortedList != null)
                m_SortedList.Clear();

            //Unregister our node event callback
            if (m_NodeEventCallback != null)
            {
                m_Global.ISceneEventManager.UnRegisterCallback(m_NodeEventID);
                m_NodeEventCallback = null;
                m_NodeEventID = 0;
            }

            //unregister scenereset callback
            m_Global.UnRegisterNotification(m_SceneResetHandler, null);
        }

        public bool AddLPObjects()
        {
            IInterface iface = m_Global.COREInterface;

            if (m_NodeEventID == 0)
            {
                m_NodeEventCallback = new NodeEventCallbacks(this);
                m_NodeEventID = m_Global.ISceneEventManager.RegisterCallback(m_NodeEventCallback, false, 0, false);
            }

            //Register sceneReset
            if (m_SceneResetHandler == null)
            {
                m_SceneResetHandler = (Global_SceneReset);
                m_Global.RegisterNotification(m_SceneResetHandler, null, SystemNotificationCode.PostSceneReset);
            }

            //TODO implement Exploded stuff
            //we should read the properties here and determine wheter the object has been exploded before or not
            //let's just set it to false for now
            Exploded = false;

            var succes = false;

            //Loop through each object in the selection
            for (int i = 0; i < iface.SelNodeCount; i++)
            {
                //Get the node
                IINode node = iface.GetSelNode(i);

                //Get base Object
                IObject baseObjectRef = node.ObjectRef.FindBaseObject();
                if (baseObjectRef.SuperClassID != SClass_ID.Geomobject) continue; //skip this one it's not a geomObject
                
                //Debug Log
                DebugMethods.Log(String.Format("LP Node name: {0}", node.Name));
                DebugMethods.Log(String.Format("Node handle: {0}", node.Handle));

                //New ParentNode
                ParentNode parentNode = null;
                //The only time we should need to check what classID our selection is. 
                if (baseObjectRef.ClassID.OperatorEquals(m_Global.Class_ID.Create(0xe44f10b3, 0)) == 1) //Mesh
                {
                    parentNode = ObjectMethods.CreateNode(node, false, true) as ParentNode;
                }
                else if (baseObjectRef.ClassID.OperatorEquals(m_Global.Class_ID.Create(0x1bf8338d, 0x192f6098)) == 1) //Poly
                {
                    parentNode = ObjectMethods.CreateNode(node, true, true) as ParentNode;
                }

                //it wasn't an Editable Poly or Editable Mesh, or something went wrong in CreateNode()
                if (parentNode == null) continue; 

                //Handle of INode
                var nodeHandle = node.Handle;

                //Empty debug string
                string nodeAddedInformation = "Error: The node has already been added.";

                if (!m_RealParentNodeDictionary.ContainsKey(nodeHandle))
                {
                    //don't add it right away, it might be a child of an already existing parentNode... 
                    var usedHandles = m_RealParentNodeDictionary.Keys.ToArray();
                    if (usedHandles.Length == 0) //there are no parentNodes
                    {
                        //Add new parentNode to Dictionary
                        m_RealParentNodeDictionary.Add(nodeHandle, parentNode);

                        //Add this node to the callback hashset
                        m_NodeEventCallback.SetLPNodeToWatch(nodeHandle);

                        //Debug text
                        nodeAddedInformation = string.Format("Successfully added ParentNode {0}", node.Name);
                        succes = true;
                    }
                    else
                    {
                        foreach (var lpHandle in usedHandles)
                        {
                            //If there are no childNodes - just add the node
                            var uniqueChildNodes = m_RealParentNodeDictionary[lpHandle].GetUniqueChildNodes();
                            if (uniqueChildNodes.Count == 0)
                            {
                                m_RealParentNodeDictionary.Add(nodeHandle, parentNode);
                                m_NodeEventCallback.SetLPNodeToWatch(nodeHandle);
                                nodeAddedInformation = string.Format("Node added {0}", node.Name);
                                succes = true;
                            }
                            else
                            {
                                foreach (var uniqueChildNode in uniqueChildNodes)
                                {
                                    //so now we should check the handles of the uniqueChildNodes compared to the nodeHandle
                                    //If they are the same, that means that there is a child in the list somewhere that is now trying
                                    //to be a parent node - this can't happen! 
                                    if (nodeHandle != uniqueChildNode.Handle)
                                    {
                                        m_RealParentNodeDictionary.Add(nodeHandle, parentNode);
                                        m_NodeEventCallback.SetLPNodeToWatch(nodeHandle);
                                        nodeAddedInformation = string.Format("Node added {0}", node.Name);
                                        succes = true;
                                    }
                                    else
                                    {
                                        nodeAddedInformation = string.Format("Node already added as HP node");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    //We don't replace the entire node with parentNode because in that case he would lose his children
                    //We do need to clear the deleted material ID set
                    var ids = m_RealParentNodeDictionary[nodeHandle].UpdateMaterialBitArray(parentNode);
                    
                    if (ids.Any())
                    {
                        m_RealParentNodeDictionary[nodeHandle].ClearDeletedMaterialIDs();
                        nodeAddedInformation = "Materials IDs added: " + ids;
                        succes = true;
                    }
                    else
                    {
                        nodeAddedInformation = "Node has already been added.";
                    }
                }

                DebugMethods.Log(nodeAddedInformation);
                DebugText = nodeAddedInformation;
            }

            return succes;
        }

        public void AddHPObject()
        {
            //loop through selection, check faceID - if it's the same as our LP already we need to add it as a child 
            if (m_RealParentNodeDictionary.Count == 0) return;

            IInterface iface = m_Global.COREInterface;
            //Loop through each object in the selection
            for (int i = 0; i < iface.SelNodeCount; i++)
            {
                //Get the node
                IINode node = iface.GetSelNode(i);

                IObject baseObjectRef = node.ObjectRef.FindBaseObject();
                if (baseObjectRef.SuperClassID != SClass_ID.Geomobject) continue; //skip this one it's not a geomObject
                
                //Debug Log
                DebugMethods.Log(node.Name);
                DebugMethods.Log(String.Format("Node handle: {0}", node.Handle));

                ChildNode childNode = null;
                //The only time we should need to check what classID our selection is. 
                if (baseObjectRef.ClassID.OperatorEquals(m_Global.Class_ID.Create(0xe44f10b3, 0)) == 1) //Mesh
                {
                    childNode = ObjectMethods.CreateNode(node, false, false) as ChildNode;
                        //Cast it to a parentNode because it's our LP
                }
                else if (baseObjectRef.ClassID.OperatorEquals(m_Global.Class_ID.Create(0x1bf8338d, 0x192f6098)) == 1) //Poly
                {
                    childNode = ObjectMethods.CreateNode(node, true, false) as ChildNode; //Cast it to a parentNode because it's our LP
                }

                if (childNode == null) continue; //it wasn't an Editable Poly or Editable Mesh, or something went wrong in 'CreateBaseNode()'
                
                var hpNodeHandle = node.Handle;

                m_NodeEventCallback.SetHPNodeToWatch(hpNodeHandle);

                //Get all used HP IDs
                var usedHpIDs = childNode.GetUsedMaterialIDsArray();

                //Get all lp handles
                var lpHandles = m_RealParentNodeDictionary.Keys.ToArray();

                //Loop through all lp objects
                foreach (uint lpHandle in lpHandles)
                {
                    //Get all lp IDs
                    var usedLpIDs = m_RealParentNodeDictionary[lpHandle].GetUsedMaterialIDsArray();

                    //matching ones:
                    var matchedIDs = usedLpIDs.Intersect(usedHpIDs);
                    //bool? result
                    foreach (ushort hpID in matchedIDs)
                    {
                       if (lpHandle != hpNodeHandle) //you don't want a parent and a child to be the same object
                       {
                           childNode.ParentHandle = lpHandle;
                           var result = m_RealParentNodeDictionary[lpHandle].SetChild(hpID, childNode);
                       } 
                    }
                    //if (result)
                    //    DebugText = String.Format("Successfully added {0} as childNode", childNode.Name);
                    //else
                    //    DebugText = String.Format("{0} has already been added as childNode", childNode.Name);


                    //non matching ones:
                    var placeHolderIDs = usedHpIDs.Except(usedLpIDs);
                    foreach (ushort hpID in placeHolderIDs)
                    {
                        m_RealParentNodeDictionary[lpHandle].SetPlaceHolderID(hpID);
                        m_RealParentNodeDictionary[lpHandle].SetChild(hpID, childNode);
                    }
                }
            }
        }


        public void UpdateMaterialIDChange(uint handle)
        {
            //LP
            if (m_RealParentNodeDictionary.ContainsKey(handle))
            {
                //Get the parentNode at handle
                var parentNode = m_RealParentNodeDictionary[handle];

                //Get all the material IDs before we update our node (this will NOT include deleted IDs)
                var beforeIDChange = parentNode.GetUsedMaterialIDsArray();
                //Update our node
                ObjectMethods.UpdateFaceDictionary(parentNode);
                //Get all the material IDs again, but after we've updated the node
                var afterIDChange = parentNode.GetUsedMaterialIDsArray();
                
                //There are the IDs that should be getting removed from the treeview
                var deleteIDs = beforeIDChange.Except(afterIDChange);
                //IDs that should be added
                var addIDs = afterIDChange.Except(beforeIDChange);

                //Get all the deleted nodes
                var alreadyDeletedIDs = parentNode.GetDeletedMaterialIDsArray();
                
                if (!afterIDChange.Except(alreadyDeletedIDs).Any())
                {
                    //This means that the IDs that should be getting added are already deleted. All of them. So we should remove our node
                    DebugMethods.Log("Empty we should delete node from m_RealParentNode");
                    m_RealParentNodeDictionary.Remove(handle);
                }

                UpdateTreeView(handle, deleteIDs, addIDs);
            }
            else //HP - the difficult part
            {
                //Things we should do
                //If the HP changes we should check LP material IDs so we can re-parent
                //What if the user changed it to a Material ID that is not present in the current LPs?
                //we need a way of showing non-matching HP nodes. 
                //Add them under a 'node' that says Missing - MatID: id in RED

                var lpHandles = m_RealParentNodeDictionary.Keys.ToArray();
                foreach (uint lpHandle in lpHandles)
                {
                    //This will return all the childNodes (unique ones)
                    var childNodes = m_RealParentNodeDictionary[lpHandle].GetUniqueChildNodes();

                    //Now we loop over them and update the faceDictionary
                    foreach (var uniqueChildNode in childNodes)
                    {
                        if (uniqueChildNode.Handle == handle)
                        {
                            //Get all the material IDs before we update our node (this will NOT include deleted IDs)
                            var beforeIDChange = uniqueChildNode.GetUsedMaterialIDsArray();

                            ObjectMethods.UpdateFaceDictionary(uniqueChildNode);

                            //Get all the material IDs again, but after we've updated the node
                            var afterIDChange = uniqueChildNode.GetUsedMaterialIDsArray();

                            var deleteIDs = beforeIDChange.Except(afterIDChange);
                            var newIDs = afterIDChange.Except(beforeIDChange).ToArray();

                            //Get the parentNode
                            var parentNode = m_RealParentNodeDictionary[lpHandle];
                            var usedLpIDs = parentNode.GetUsedMaterialIDsArray();

                            //there is a chance none of them get parented - then we have rogue HP objects, we should have a 
                            //placeholder parent: Missing - MatID: id

                            //We need 3 lists, a delete ID list and a newID list, but it could be that the new ID has a LP ID
                            //Delete List
                            //New new list (with placeHolderNodes)
                            //Add back to LP node list
                            foreach (ushort deleteID in deleteIDs)
                            {
                                parentNode.DeleteChild(uniqueChildNode.Handle, deleteID);
                                //If there are no more children in this parentNode and it's a placeHolder node then we should delete it
                                if (parentNode.IsPlaceHolder(deleteID) && parentNode.GetChildHandles(deleteID).Count() == 0)
                                {
                                    m_RealParentNodeDictionary[parentNode.Handle].RemovePlaceholderNode(deleteID);
                                }
                            }

                            //now either add a new placeholder or set it as a child to an already existing node
                            var matchIDs = newIDs.Intersect(usedLpIDs);
                            if (matchIDs.Any())
                            {
                                foreach (ushort matchID in matchIDs)
                                {
                                    parentNode.SetChild(matchID, uniqueChildNode);
                                }
                                
                            }
                            else
                            {
                                foreach (ushort newID in newIDs)
                                {
                                    //do we need a new parentNode? Or can we just mark this ID as placeholder! this is what we should do
                                    parentNode.SetPlaceHolderID(newID);
                                    parentNode.SetChild(newID, uniqueChildNode);
                                }
                            }
                        }
                    }
                }

                //Need better one, but for now this will do
                PopulateTreeview();
            }
        }

        //maybe we don't need this isLowPoly bool, could we just check if it's a parent, if not assume it's a child?
        public void UpdateAfterCollapse(uint handle, bool isPolyObject, bool isLowPoly)
        {
            if (isLowPoly)
            {
                //If true update the face dictionary, otherwise leave it
                if (m_RealParentNodeDictionary[handle].SetIsPolyObject(isPolyObject))
                    ObjectMethods.UpdateFaceDictionary(m_RealParentNodeDictionary[handle]);
            }
            else
            {
                var lpHandles = m_RealParentNodeDictionary.Keys.ToArray();

                foreach (uint lpHandle in lpHandles)
                {
                    //TODO surely we don't need to loop through all? Just the one where the handle is the HP

                    //This will return all the childNodes (unique ones)
                    var childNodes = m_RealParentNodeDictionary[lpHandle].GetUniqueChildNodes();

                    //Now we loop over them and update the faceDictionary
                    foreach (var uniqueChildNode in childNodes)
                    {
                        if (uniqueChildNode.SetIsPolyObject(isPolyObject))
                            ObjectMethods.UpdateFaceDictionary(uniqueChildNode);
                    }

                    var idUsed = m_RealParentNodeDictionary[lpHandle].GetUsedMaterialIDsArray();
                    foreach (ushort lpID in idUsed)
                    {
                        //now restore our parenting... just copy from above
                        var hpNodeHandle = handle; //easier
                        var childNode = m_RealParentNodeDictionary[lpHandle].GetChild(lpID, hpNodeHandle);

                        //Get all used HP IDs
                        var usedHpIDs = childNode.GetUsedMaterialIDsArray();

                        //Check if they match
                        foreach (var hpID in usedHpIDs)
                        {
                            if (lpID == hpID)
                            {
                                childNode.ParentHandle = lpHandle;
                                m_RealParentNodeDictionary[lpHandle].SetChild(lpID, childNode);
                            }
                        }
                    }
                }
            }
        }

        public void DeleteTreeNode(TreeNodeEx panelClickedNode)
        {
            if (!panelClickedNode.isChild)
            {
                uint lpHandle = panelClickedNode.uHandle;
                ushort id = panelClickedNode.matID;

                //Delete the node in our dictionary
                m_RealParentNodeDictionary[lpHandle].DeleteMaterialID(id);
                //I think we need to delete all the child nodes too
                var hpHandles = m_RealParentNodeDictionary[lpHandle].GetChildHandles(id);
                foreach (uint hpHandle in hpHandles)
                {
                    m_RealParentNodeDictionary[lpHandle].DeleteChild(hpHandle, id);
                }

                //If there are no keys left in this dictionary just delete the main dictionary handle too
                if (m_RealParentNodeDictionary[lpHandle].GetMaterialIDCount() == 0)
                    m_RealParentNodeDictionary.Remove(lpHandle);

                //Delete it from our TreeViewManager
                //m_TreeviewManager.TreeView.Nodes.Remove(panelClickedNode);
                m_TreeviewManager.DeleteNode(panelClickedNode);
            }
            else
            {
                uint lpHandle = panelClickedNode.ParentHandle;  //we need the parent handle to get it out of our dictionary
                ushort id = panelClickedNode.matID;
                uint hpHandle = panelClickedNode.uHandle; //clicked node handle

                //Delete the node in our dictionary
                m_RealParentNodeDictionary[lpHandle].DeleteChild(hpHandle, id);
                
                //We don't have a manager yet for childNodes :D
                m_TreeviewManager.TreeView.Nodes.Remove(panelClickedNode);
            }

            m_TreeviewManager.UpdateDeletePanels();
        }


        public void UpdateTreeView(uint handle, IEnumerable<ushort> deleteIDs, IEnumerable<ushort> addIDs)
        {
            var treeview = m_TreeviewManager.TreeView;
            treeview.BeginUpdate();

            foreach (ushort deleteID in deleteIDs)
            {
                m_TreeviewManager.DeleteNode(handle, deleteID);
                DebugMethods.Log(String.Format("Deleting matID: {0}", deleteID + 1));
            }

            foreach (ushort addID in addIDs)
            {
                var lpNode = new TreeNodeEx(handle, addID, String.Format("{0} - MatID: {1}",
                                            m_RealParentNodeDictionary[handle].Name, addID + 1), false);

                m_TreeviewManager.AddNodes(lpNode);

                DebugMethods.Log(String.Format("Adding matID: {0}", addID + 1));
            }

            treeview.EndUpdate();
            m_TreeviewManager.UpdateDeletePanels();
        }

        /// <summary>
        /// Completely redraws the treeview - use when we changed all our nodes
        /// </summary>
        public void PopulateTreeview()
        {
            var treeview = m_TreeviewManager.TreeView;

            treeview.BeginUpdate();

            //Clear treeview on every update
            m_TreeviewManager.Clear();

            var usedLpHandles = m_RealParentNodeDictionary.Keys.ToArray();
            foreach (var lpHandle in usedLpHandles)
            {
                var usedLpIDs = m_RealParentNodeDictionary[lpHandle].GetUsedMaterialIDsArray();
                Array.Sort(usedLpIDs);

                foreach (ushort lpID in usedLpIDs)
                {
                    TreeNodeEx lpNode;

                    if (m_RealParentNodeDictionary[lpHandle].IsPlaceHolder(lpID))
                    {
                        lpNode = new TreeNodeEx(lpHandle, lpID, String.Format("Missing LP - MatID: {0}", lpID + 1), false);
                        lpNode.ForeColor = Color.FromArgb(235, 63, 63);
                    }
                    else
                    {
                        lpNode = new TreeNodeEx(lpHandle, lpID, String.Format("{0} - MatID: {1}",
                             m_RealParentNodeDictionary[lpHandle].Name, lpID + 1), false); 
                    }
                    
                    var usedHpHandles = m_RealParentNodeDictionary[lpHandle].GetChildHandles(lpID);
                    foreach (uint hpHandle in usedHpHandles)
                    {
                        var hpChildNode = m_RealParentNodeDictionary[lpHandle].GetChild(lpID, hpHandle);
                        if (hpChildNode != null)
                        {
                            var hpNode = new TreeNodeEx(hpHandle, lpID, hpChildNode.Name, true)
                            {
                                ParentHandle = lpHandle
                            };
                            lpNode.Nodes.Add(hpNode);
                        }
                    }

                    m_TreeviewManager.AddNodes(lpNode);
                }
            }

            treeview.EndUpdate();
            m_TreeviewManager.UpdateDeletePanels();
        }

        public void Explode()
        {
            Stopwatch explodeStopwatch = new Stopwatch();
            explodeStopwatch.Start();

            List<BoundingBoxHandleID> allBoundingBoxList = new List<BoundingBoxHandleID>();

            var usedLpHandles = m_RealParentNodeDictionary.Keys.ToArray();
            foreach (uint lpHandle in usedLpHandles)
            {
                var usedLpIDs = m_RealParentNodeDictionary[lpHandle].GetUsedMaterialIDsArray();

                var parentNode = m_RealParentNodeDictionary[lpHandle];
                
                foreach (ushort lpID in usedLpIDs)
                {
                    //Update Bounding Box
                    ObjectMethods.BuildBoundingBox(parentNode, lpID);
                    
                    var tempBB = parentNode.GetBoundingBox(lpID);

                    ObjectMethods.ExpandBoundingBox(ref tempBB);

                    //Move BB in 1 list for easy Collision checks
                    var tempBBHandleID = new BoundingBoxHandleID
                    {
                        BoundingBox = tempBB,
                        Handle = lpHandle,
                        ID = lpID,
                        MoveValue = m_Global.Point3.Create(0, 0, 0)
                    };

                    allBoundingBoxList.Add(tempBBHandleID);
                }
            }


            //Sort them on Volume
            var sortedBBList = allBoundingBoxList.OrderBy(o => o.BoundingBox.Volume).Reverse().ToList();
            m_SortedList = sortedBBList;


            IInterface iface = m_Global.COREInterface;

            //try
            //{
            iface.DisableSceneRedraw();
            IntPtr maxHandle = m_Global.COREInterface.MAXHWnd;
            Win32.SuspendDrawing(maxHandle);

            //Collision check 
            for (int i = 0; i < sortedBBList.Count; i++)
            {
                for (int j = i + 1; j < sortedBBList.Count; j++)
                {
                    var a = sortedBBList[i];
                    var b = sortedBBList[j];

                    if (CollisionCheck(a.BoundingBox, b.BoundingBox))
                    {
                        MoveCollisionBoxWhile(a, b);
                    }
                }
            }


            foreach (var boundingBoxHandleId in sortedBBList)
            {
                var lpHandle = boundingBoxHandleId.Handle;
                var lpID = boundingBoxHandleId.ID;
                var moveValue = boundingBoxHandleId.MoveValue;

                var parentNode = m_RealParentNodeDictionary[lpHandle];

                //put the ParentNode and all his children in 1 list
                var baseNodeList = new List<BaseNode>() { parentNode };

                var usedHpHandles = parentNode.GetChildHandles(lpID);
                foreach (uint hpHandle in usedHpHandles)
                {
                    baseNodeList.Add(parentNode.GetChild(lpID, hpHandle));
                }

                foreach (var baseNode in baseNodeList)
                {
                    ObjectMethods.ExplodeNode(baseNode, moveValue, lpID);
                }
            }

            var tempUniqueHpHandles = new HashSet<uint>();
            var tempUniqueChildNodes = new List<ChildNode>();
            //Invalidates every possible node. 
            var lpHandleArray = m_RealParentNodeDictionary.Keys.ToArray();
            foreach (uint lpHandle in lpHandleArray)
            {
                var parentNode = m_RealParentNodeDictionary[lpHandle];

                //if the object has modifiers we need to set the baseobject again so it gets re-cached. 
                var derivedObject = parentNode.INode.ObjectRef as IIDerivedObject;
                if (derivedObject != null) derivedObject.ReferenceObject(parentNode.INode.ObjectRef.FindBaseObject());

                parentNode.InvalidateMesh();

                var matIDArray = parentNode.GetUsedMaterialIDsArray();

                foreach (ushort matID in matIDArray)
                {
                    var usedHpHandles = parentNode.GetChildHandles(matID);
                    foreach (uint hpHandle in usedHpHandles)
                    {
                        if (tempUniqueHpHandles.Add(hpHandle))
                            tempUniqueChildNodes.Add(parentNode.GetChild(matID, hpHandle));
                    }
                }

            }
            foreach (var uniqueChildNode in tempUniqueChildNodes)
            {
                //if the object has modifiers we need to set the baseobject again so it gets re-cached. 
                var derivedObject = uniqueChildNode.INode.ObjectRef as IIDerivedObject;
                if (derivedObject != null) derivedObject.ReferenceObject(uniqueChildNode.INode.ObjectRef.FindBaseObject());

                uniqueChildNode.InvalidateMesh();
            }


            explodeStopwatch.Stop();
            DebugMethods.Log(String.Format("Time took to explode mesh: {0}ms", explodeStopwatch.ElapsedMilliseconds));

            Exploded = true;

            Win32.ResumeDrawing(maxHandle);
            iface.EnableSceneRedraw();
            iface.ForceCompleteRedraw(true);
        }


        bool CollisionCheck(BoundingBox box1, BoundingBox box2)
        {
            if (box1.Min.X >= box2.Max.X) return false;
            if (box1.Min.Y >= box2.Max.Y) return false;
            if (box1.Min.Z >= box2.Max.Z) return false;
            if (box1.Max.X <= box2.Min.X) return false;
            if (box1.Max.Y <= box2.Min.Y) return false;
            if (box1.Max.Z <= box2.Min.Z) return false;

            return true;
        }

        void MoveCollisionBoxWhile(BoundingBoxHandleID a, BoundingBoxHandleID b)
        {
            //rewrite with while... //this is fine for the first test, but we should now test against all others again...
            while (CollisionCheck(a.BoundingBox, b.BoundingBox))
            {
                var smallestValueStruct = FindSmallestValue(a, b);
                MoveBox(b, smallestValueStruct);

                for (int i = 0; i < m_SortedList.Count; i++)
                {
                    var aNew = m_SortedList[i];

                    if (aNew == b)
                        continue;

                    if (CollisionCheck(aNew.BoundingBox, b.BoundingBox))
                    {
                        a = aNew;
                        break;
                    }
                } 
            }
        }

        private MoveStruct FindSmallestValue(BoundingBoxHandleID a, BoundingBoxHandleID b)
        {
            var box1 = a.BoundingBox;
            var box2 = b.BoundingBox;

            float x1 = (box1.Max.X - box2.Min.X);
            float x2 = (box2.Max.X - box1.Min.X) * -1;

            float y1 = (box1.Max.Y - box2.Min.Y);
            float y2 = (box2.Max.Y - box1.Min.Y) * -1;

            float z1 = (box1.Max.Z - box2.Min.Z);
            float z2 = (box2.Max.Z - box1.Min.Z) * -1;

            var moveValueX1 = new MoveStruct(x1, Coordinates.X);
            var moveValueX2 = new MoveStruct(x2, Coordinates.X);

            var moveValueY1 = new MoveStruct(y1, Coordinates.Y);
            var moveValueY2 = new MoveStruct(y2, Coordinates.Y);

            var moveValueZ1 = new MoveStruct(z1, Coordinates.Z);
            var moveValueZ2 = new MoveStruct(z2, Coordinates.Z);

            MoveStruct[] moveValues = { moveValueX1, moveValueX2, moveValueY1, moveValueY2, moveValueZ1, moveValueZ2 };

            var sortedMoveValues = moveValues.OrderBy(o => Math.Abs(o.MoveValue)).ToArray();

            int index = 0;
            //So we loop over all the values b has been moved already
            foreach (var alreadyMovedValue in b.MovedValues)
            {
                //I care about the direction and the abs value
                //let's check direction first, easier
                if (sortedMoveValues[index].Direction == alreadyMovedValue.Direction) //so the directions match, 
                {
                    //if now also the abs value is the same we need to up our index so find a new move value. 
                    if (Math.Abs(Math.Abs(sortedMoveValues[index].MoveValue) - Math.Abs(alreadyMovedValue.MoveValue)) < 0.01)
                    {
                        //i think at this point we've found that our smallest value at index is in the same direction and has the same
                        //abs value as a previously moved value. This is bad and it will end up looping forever
                        ++ index;
                    }
                }
            }

            //add the new move value to the array
            b.MovedValues.Add(new MoveStruct(sortedMoveValues[index].MoveValue, sortedMoveValues[index].Direction));

            return sortedMoveValues[index];
        }

        private void MoveBox(BoundingBoxHandleID movingBox, MoveStruct smallestValue)
        {
            var box = movingBox.BoundingBox;

            switch (smallestValue.Direction)
            {
                case Coordinates.X:
                    box.Max.X += smallestValue.MoveValue;
                    box.Min.X += smallestValue.MoveValue;

                    movingBox.MoveValue.X += smallestValue.MoveValue;
                    break;

                case Coordinates.Y:
                    box.Max.Y += smallestValue.MoveValue;
                    box.Min.Y += smallestValue.MoveValue;

                    movingBox.MoveValue.Y += smallestValue.MoveValue;
                    break;

                case Coordinates.Z:
                    box.Max.Z += smallestValue.MoveValue;
                    box.Min.Z += smallestValue.MoveValue;

                    movingBox.MoveValue.Z += smallestValue.MoveValue;
                    break;
            }
        }

            //        //Useless - 
            //if (m_PreviousMovesList.Count == 0)
            //{
            //    var temp = new MoveStruct(sortedMoveValues[0].MoveValue, sortedMoveValues[0].Direction);
            //    m_PreviousMovesList.Add(temp);
            //}
            //foreach (var moveStruct in m_PreviousMovesList)
            //{
            //    //so for each moveStruct in our previousMovesList - we check if the sign isn't the oposite
            //    if (Math.Sign(sortedMoveValues[testIndex].MoveValue) != Math.Sign(moveStruct.MoveValue))
            //    {
            //        //then we check if we've moved in that direction once - this could end up in a lock as well
            //        if (sortedMoveValues[testIndex].Direction == moveStruct.Direction)
            //        {
            //            //i think we need to check for for the actual moveValue too, maybe even if the signs are different but the direction
            //            //is the same we could still quite possible move -698 in Z and then after a few loops determine we need to move +800 in Z
            //            //this would now be impossible

            //            ////if the values are the same too, then we need to move up an index because we'd be looping indefinitely
            //            if (Equals(Math.Abs(sortedMoveValues[testIndex].MoveValue), Math.Abs(moveStruct.MoveValue)))
            //            {

            //                var temp = new MoveStruct(sortedMoveValues[testIndex].MoveValue, sortedMoveValues[testIndex].Direction);
            //                m_PreviousMovesList.Add(temp);
            //                ++testIndex;
            //            }
            //        }
            //    }
            //}

        //private void MoveBox(BoundingBoxHandleID movingBox, MoveStruct[] moveValues, int isInverted = 1)
        //{
        //    var box = movingBox.BoundingBox;

        //    for (int i = 0; i < moveValues.Length; i++)
        //    {
        //        if (Math.Sign(moveValues[i].MoveValue) == 1)//Check positive locks for X, Y and Z
        //        {
        //            if (moveValues[i].Direction == Coordinates.X && movingBox.XPositiveLock == false)
        //            {
        //                box.Max.X += moveValues[i].MoveValue * isInverted;
        //                box.Min.X += moveValues[i].MoveValue * isInverted;

        //                movingBox.MoveValue.X += moveValues[i].MoveValue * isInverted;

        //                movingBox.XPositiveLock = true;
        //                DebugMethods.Log("Moving: " + moveValues[i].MoveValue + " in the " + moveValues[i].Direction + " time: " + i);

        //                return;
        //            }
        //            if (moveValues[i].Direction == Coordinates.Y && movingBox.YPositiveLock == false)
        //            {
        //                box.Max.Y += moveValues[i].MoveValue * isInverted;
        //                box.Min.Y += moveValues[i].MoveValue * isInverted;

        //                movingBox.MoveValue.Y += moveValues[i].MoveValue * isInverted;

        //                movingBox.YPositiveLock = true;
        //                DebugMethods.Log("Moving: " + moveValues[i].MoveValue + " in the " + moveValues[i].Direction + " time: " + i);
        //                return;
        //            }
        //            if (moveValues[i].Direction == Coordinates.Z && movingBox.ZPositiveLock == false)
        //            {
        //                box.Max.Z += moveValues[i].MoveValue * isInverted;
        //                box.Min.Z += moveValues[i].MoveValue * isInverted;

        //                movingBox.MoveValue.Z += moveValues[i].MoveValue * isInverted;

        //                movingBox.ZPositiveLock = true;
        //                DebugMethods.Log("Moving: " + moveValues[i].MoveValue + " in the " + moveValues[i].Direction + " time: " + i);
        //                return;
        //            }
        //        }
        //        else //check negatives
        //        {
        //            if (moveValues[i].Direction == Coordinates.X && movingBox.XNegativeLock == false)
        //            {
        //                box.Max.X += moveValues[i].MoveValue * isInverted;
        //                box.Min.X += moveValues[i].MoveValue * isInverted;

        //                movingBox.MoveValue.X += moveValues[i].MoveValue * isInverted;

        //                movingBox.XNegativeLock = true;
        //                DebugMethods.Log("Moving: " + moveValues[i].MoveValue + " in the " + moveValues[i].Direction + " time: " + i);
        //                return;
        //            }
        //            if (moveValues[i].Direction == Coordinates.Y && movingBox.YNegativeLock == false)
        //            {
        //                box.Max.Y += moveValues[i].MoveValue * isInverted;
        //                box.Min.Y += moveValues[i].MoveValue * isInverted;

        //                movingBox.MoveValue.Y += moveValues[i].MoveValue * isInverted;

        //                movingBox.YNegativeLock = true;
        //                DebugMethods.Log("Moving: " + moveValues[i].MoveValue + " in the " + moveValues[i].Direction + " time: " + i);
        //                return;
        //            }
        //            if (moveValues[i].Direction == Coordinates.Z && movingBox.ZNegativeLock == false)
        //            {
        //                box.Max.Z += moveValues[i].MoveValue * isInverted;
        //                box.Min.Z += moveValues[i].MoveValue * isInverted;

        //                movingBox.MoveValue.Z += moveValues[i].MoveValue * isInverted;

        //                movingBox.ZNegativeLock = true;
        //                DebugMethods.Log("Moving: " + moveValues[i].MoveValue + " in the " + moveValues[i].Direction + " time: " + i);
        //                return;
        //            }
        //        }
        //    }
        //}


        ///// <summary>
        ///// Returns the BitArray for the given ID (PolyMesh). If the ID does not exists it returns null
        ///// </summary>
        ///// <param name="id">Material ID</param>
        ///// <param name="node">Node we want to get our faces per ID for</param>
        ///// <returns></returns>
        //public static IBitArray GetFacesBitArrayByID(ushort id, MasterNode node)
        //{
        //    var arrayOfAllIDs = node.MaterialIDFaceDictionary.Keys.ToArray();
        //    return arrayOfAllIDs.Contains(id) ? node.MaterialIDFaceDictionary[id] : null;
        //}

        //[Conditional("DEBUG")]
        //public void PrintVertexPos(ushort id)
        //{
        //    //Basically an array of all the objects in the masterNodeList
        //    var arrayOfAllHandles = m_MasterNodeList.Keys.ToArray();
        //    //first handle:
        //    uint handle = arrayOfAllHandles[0];

        //    //NOT THIS:
        //    //HashSet<int> vertexIndex = m_MasterNodeList[handle].MaterialIDVertexDictionary[id];
        //    //USE this!
        //    HashSet<int> vertexIndex = GetVertIndicesByID(id, m_MasterNodeList[handle]);
        //    if (vertexIndex == null)
        //        MessageBox.Show("Invalid ID.");
        //    else
        //    {
        //        foreach (var index in vertexIndex)
        //        {
        //            DebugMethods.Log(index.ToString());
        //        }
        //    }
        //}


        [Conditional("DEBUG")]
        public void PrintVertexPos()
        {
            foreach (var vertPos in m_VertList)
            {
                DebugMethods.Log(string.Format("Vertex Pos: X: {0} - - Y: {1} - - Z:{2}", vertPos.X, vertPos.Y, vertPos.Z));
            }
        }


        internal void CreateDebugBoundingBox()
        {
            //get the max and min values and construct 8 points, then later loop over our box and assign them 
            var usedLpHandles = m_RealParentNodeDictionary.Keys.ToArray();

            foreach (var lpHandle in usedLpHandles)
            {
                var usedLpIDs = m_RealParentNodeDictionary[lpHandle].GetUsedMaterialIDsArray();

                foreach (var lpID in usedLpIDs)
                {
                    IINode myNode = DebugMethods.MaxScriptExecute("Box lengthsegs:1 widthsegs:1 heightsegs:1 length:25 width:25 height:25 mapcoords:on pos:[0,0,0] isSelected:on").ToNode;
                    DebugMethods.MaxScriptExecute("convertToMesh $");

                    ITriObject triObj = (ITriObject)myNode.EvalWorldState(0, true).Obj.ConvertToType(0, m_Global.TriObjectClassID);
                    var triMesh = triObj.Mesh;

                    List<IPoint3> bbPosList = new List<IPoint3>();

                    var maxValue = m_RealParentNodeDictionary[lpHandle].GetBoundingBox(lpID).Max;
                    var minValue = m_RealParentNodeDictionary[lpHandle].GetBoundingBox(lpID).Min;
                    //var maxValue = m_SortedList[0].BoundingBox.Max;
                    //var minValue = m_SortedList[0].BoundingBox.Min;

                    bbPosList.Add(m_Global.Point3.Create(minValue.X, maxValue.Y, minValue.Z));
                    bbPosList.Add(m_Global.Point3.Create(maxValue.X, maxValue.Y, minValue.Z));
                    bbPosList.Add(m_Global.Point3.Create(minValue.X, minValue.Y, minValue.Z));
                    bbPosList.Add(m_Global.Point3.Create(maxValue.X, minValue.Y, minValue.Z));

                    bbPosList.Add(m_Global.Point3.Create(minValue.X, maxValue.Y, maxValue.Z));
                    bbPosList.Add(m_Global.Point3.Create(maxValue.X, maxValue.Y, maxValue.Z));
                    bbPosList.Add(m_Global.Point3.Create(minValue.X, minValue.Y, maxValue.Z));
                    bbPosList.Add(m_Global.Point3.Create(maxValue.X, minValue.Y, maxValue.Z));

                    IMatrix3 m3World = myNode.GetObjTMAfterWSM(0, null);
                    IMatrix3 m3Local = m3World;

                    if (!myNode.IsRootNode)
                    {
                        IMatrix3 m3Parent = myNode.ParentNode.GetObjTMAfterWSM(0, null);
                        m3Local = m3World.Multiply(m_Global.Inverse(m3Parent));
                    }

                    for (int i = 0; i < bbPosList.Count; i++)
                    {
                        //var pos = triMesh.GetVert(i);

                        //var worldPosMaybe = m3Local.PointTransform(pos);

                        //worldPosMaybe = bbPosList[i];

                        //var m3LocalInverse = m_Global.Inverse(m3Local);
                        //var finalPos = m3LocalInverse.PointTransform(worldPosMaybe);



                        triMesh.SetVert(i, bbPosList[i]);
                    }
                }
            }
        }

        ///// <summary>
        ///// Returns the BitArray for the given ID (TriMesh). If the ID does not exists it returns null
        ///// </summary>
        ///// <param name="id">Material ID</param>
        ///// <param name="node">Node we want to get our faces per ID for</param>
        ///// <returns></returns>
        //private IBitArray GetTriFacesBitArrayByID(ushort id, MasterNode node)
        //{
        //    var arrayOfAllIDs = node.MaterialIDTriFaceDictionary.Keys.ToArray();
        //    return arrayOfAllIDs.Contains(id) ? node.MaterialIDTriFaceDictionary[id] : null;
        //}

        ///// <summary>
        ///// Returns the BitArray for the given ID (PolyMesh). If the ID does not exists it returns null
        ///// </summary>
        ///// <param name="id">Material ID</param>
        ///// <param name="node">Node we want to get our faces per ID for</param>
        ///// <returns></returns>
        //private IBitArray GetPolyFacesBitArrayByID(ushort id, MasterNode node)
        //{
        //    var arrayOfAllIDs = node.MaterialIDPolyFaceDictionary.Keys.ToArray();
        //    return arrayOfAllIDs.Contains(id) ? node.MaterialIDPolyFaceDictionary[id] : null;
        //}

        /// <summary>
        /// Returns the BitArray for the given ID. If the ID does not exists it returns null
        /// </summary>
        /// <param name="id">Material ID</param>
        /// <param name="node">Node we want to get our faces per ID for</param>
        ///// <returns></returns>
        //private HashSet<int> GetVertIndicesByID(ushort id, MasterNode node)
        //{
        //    //Change for both 
        //    var arrayOfAllIDs = node.MaterialIDFaceDictionary.Keys.ToArray();

        //    return arrayOfAllIDs.Contains(id) ? node.MaterialIDVertexDictionary[id] : null;
        //}


        internal void CheckForExtraCollision()
        {
            //List<BoundingBoxHandleID> allBoundingBoxList = new List<BoundingBoxHandleID>();

            //var usedLpHandles = m_ParentNodeDictionary.Keys.ToArray();
            ////Move all BB in 1 list
            //foreach (var lpHandle in usedLpHandles)
            //{
            //    var usedLpIDs = m_ParentNodeDictionary[lpHandle].Keys.ToArray();
            //    foreach (var lpID in usedLpIDs)
            //    {
            //        //Get our node
            //        var parentNode = m_ParentNodeDictionary[lpHandle][lpID];

            //        //Update it - this will set the struct to use the correct IMesh or IMNMesh
            //        parentNode.UpdateMesh();

            //        //Update Bounding Box
            //        ObjectMethods.BuildBoundingBox(parentNode);

            //        //Move BB in 1 list for easy Collision checks
            //        var tempBBHandleID = new BoundingBoxHandleID
            //        {
            //            BoundingBox = m_ParentNodeDictionary[lpHandle][lpID].BoundingBox,
            //            Handle = lpHandle,
            //            ID = lpID,
            //            MoveValue = m_Global.Point3.Create(0, 0, 0)
            //        };
            //        allBoundingBoxList.Add(tempBBHandleID);
            //    }
            //}

            ////Sort them on Volume
            //var sortedBBList = allBoundingBoxList.OrderBy(o => o.BoundingBox.Volume).Reverse().ToList();


            //List<BoundingBoxHandleID> collisions = new List<BoundingBoxHandleID>();

            //IInterface iface = m_Global.COREInterface;



            ////Collision check 
            //for (int i = 0; i < sortedBBList.Count; i++)
            //{
            //    for (int j = i + 1; j < sortedBBList.Count; j++)
            //    {
            //        var a = sortedBBList[i];
            //        var b = sortedBBList[j];

            //        if (CollisionCheck(a.BoundingBox, b.BoundingBox))
            //        {
            //            DebugMethods.Log("Collision");

            //            ////var moveValue = MoveCollisionBox(ref a, ref b);
            //            //MoveCollisionBox(ref a, ref b);

            //            ////our sortedBBList holds the handle and ID it moved (always J)!
            //            ////MoveID(sortedBBList[j].Handle, sortedBBList[j].ID, sortedBBList[j].MoveValue);


            //            //var name = m_ParentNodeDictionary[sortedBBList[j].Handle][sortedBBList[j].ID].Name;
            //            ////DebugMethods.Log(String.Format("Moving: {0} by amount: X: {1}  --   Y: {2}  --   Z: {3}", name, moveValue.X, moveValue.Y, moveValue.Z));

            //            //collisions.Add(b);
            //        }
            //    }
            //}
        }

        internal void DebugMoveSelectNodeVerts()
        {
            //Maybe not the correct way to get vertex positions
            IInterface iface = m_Global.COREInterface;
            IINode node = iface.GetSelNode(0);
            IObject baseObjectRef = node.ObjectRef.FindBaseObject();
            //ITriObject triObj = baseObjectRef as ITriObject;
            //IMesh triMesh = triObj.Mesh;

            IObject obj = node.EvalWorldState(0, true).Obj;

            IClass_ID lol = m_Global.Class_ID.Create(0xe44f10b3, 0);
            ITriObject triObj = null;
            if (obj.CanConvertToType(lol) != 0)
            {
                triObj = (ITriObject)node.EvalWorldState(0, true).Obj.ConvertToType(0, m_Global.TriObjectClassID);
            }
            IMesh triMesh = triObj.Mesh;
               

            IMatrix3 m3World = node.GetObjTMAfterWSM(0, null);
            IMatrix3 m3Local = m3World;

            if (!node.IsRootNode)
            {
                IMatrix3 m3Parent = node.ParentNode.GetObjTMAfterWSM(0, null);
                m3Local = m3World.Multiply(m_Global.Inverse(m3Parent));
            }
            
            int numVert = triMesh.NumVerts;
            for (int i = 0; i < numVert; i++)				//Modify geometry
            {
                //mesh->setVert(i, Point3(i, i, i));
                var pos = triMesh.GetVert(i);

                var worldPosMaybe = m3Local.PointTransform(pos);

                worldPosMaybe.X += 0.0f;
                worldPosMaybe.Y += 50.0f;
                worldPosMaybe.Z += 0.0f;

                var m3LocalInverse = m_Global.Inverse(m3Local);
                var finalPos = m3LocalInverse.PointTransform(worldPosMaybe);

                triMesh.SetVert(i, finalPos);
            }

            triMesh.InvalidateGeomCache();
            triMesh.InvalidateTopologyCache();

            var forever = m_Global.Interval.Create();
            forever.SetInfinite();

            UIntPtr partAll = (UIntPtr)0xffffffff;
            uint all = 0xffffffff;

#if (MAX_2012_DEBUG || MAX_2013_DEBUG || MAX_2014_DEBUG )
            triObj.NotifyDependents(forever, partAll, RefMessage.Change, (SClass_ID)all, true, null);
            triObj.NotifyDependents(forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)all, true, null);

            node.NotifyDependents(forever, partAll, RefMessage.Change, (SClass_ID)all, true, null);
            node.NotifyDependents(forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)all, true, null);

#elif MAX_2015_DEBUG
            triObj.NotifyDependents_(forever, partAll, RefMessage.Change, (SClass_ID)all, true, null);
            triObj.NotifyDependents_(forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)all, true, null);
            
            node.NotifyDependents_(forever, partAll, RefMessage.Change, (SClass_ID)all, true, null);
            node.NotifyDependents_(forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)all, true, null);
#endif

            iface.ForceCompleteRedraw(true);
        }
    }
}

//var forever = m_Global.Interval.Create();
//forever.SetInfinite();

//UIntPtr partAll = (UIntPtr) 0xffffffff;
//uint all = 0xffffffff;

//var handleList = new List<uint> {lpHandle};

////We need to get all the nodes (this includes the child nodes
//foreach (var childNode in m_ParentNodeDictionary[lpHandle][id].ChildNodeList)
//    handleList.Add(childNode.Handle);

//Handle list is stupid // move parent then child 
//Move Parent:
//var parentNode = m_ParentNodeDictionary[lpHandle][id];
//parentNode.UpdateMesh(lpHandle);

//ObjectMethods.BuildMatrix(parentNode);

//HashSet<int> vertexIndices;
////So this method builds the actual hash set we already built while adding the object to the treeview - maybe remove it there
////we need to make sure our Mat ID list is fully up to date before calling this method
//ObjectMethods.BuildVertexHashSetFromMatID(parentNode, m_ParentNodeDictionary[lpHandle], out vertexIndices);

//foreach (var vertexIndex in vertexIndices)
//{
//    ObjectMethods.OffsetVertex(parentNode, vertexIndex, moveValue);
//}

//parentNode.InvalidateMesh();
////Parent node moved - lets move his children

//foreach (var childNode in m_ParentNodeDictionary[lpHandle][id].ChildNodeList)
//{
//    childNode.UpdateMesh(childNode.Handle);

//    ObjectMethods.BuildMatrix(childNode);

//    HashSet<int> vertexChildIndices;
//    //it uses the same materialIDs - this is the whole point of the script
//    ObjectMethods.BuildVertexHashSetFromMatID(childNode, m_ParentNodeDictionary[lpHandle], out vertexChildIndices);

//    foreach (var vertexIndex in vertexChildIndices)
//    {
//        ObjectMethods.OffsetVertex(parentNode, vertexIndex, moveValue);
//    }

//    childNode.InvalidateMesh();
//}


//var baseNodeList = new List<BaseNode>() {m_ParentNodeDictionary[lpHandle][id] };
//baseNodeList.AddRange(m_ParentNodeDictionary[lpHandle][id].ChildNodeList);

//foreach (var baseNode in baseNodeList)
//{
//    baseNode.UpdateMesh(baseNode.Handle);

//    ObjectMethods.BuildMatrix(baseNode);

//    HashSet<int> vertexIndices;
//    //iets bescheten hier mss in den m_parentNodeDictionary mss zijn ze allemaal hetzelfde :p
//    ObjectMethods.BuildVertexHashSetFromMatID(baseNode, m_ParentNodeDictionary[lpHandle], out vertexIndices);

//    foreach (var vertexIndex in vertexIndices)
//     {
//         ObjectMethods.OffsetVertex(baseNode, vertexIndex, moveValue);
//     }

//    baseNode.InvalidateMesh();
//}



//for (int index = 0; index < handleList.Count; index++)
//{
//    var handle = handleList[index];
//    IINode node = iface.GetINodeByHandle(handle);

//    //If for some reason there is no node with that handle - return
//    if (node == null)
//        continue;

//    //Not sure if needed but why not.. right?!
//    iface.CommandPanelTaskMode = TASK_MODE_MODIFY;
//    IObject baseObjectRef = node.ObjectRef.FindBaseObject();

//    IMatrix3 m3World = node.GetObjTMAfterWSM(0, null);
//    IMatrix3 m3Local = m3World;

//    if (!node.IsRootNode)
//    {
//        IMatrix3 m3Parent = node.ParentNode.GetObjTMAfterWSM(0, null);
//        m3Local = m3World.Multiply(m_Global.Inverse(m3Parent));
//    }


//    //Non of this check shit, we should know if the object is poly or mesh with the Property (IsPolyObjecT)
//    // parentNode = m_ParentNodeDictionary[handle][id];

//    //parentNode.UpdateMesh(handle);

//    //HashSet<int> vertexIndices;

//    //ObjectMethods.BuildVertexHashsetFromMatID(parentNode, m_ParentNodeDictionary[handle], out vertexIndices);

//    if (baseObjectRef.ClassID.OperatorEquals(m_Global.Class_ID.Create(0x1bf8338d, 0x192f6098)) == 1)
//        //Editable Poly
//    {
//        //IPolyObject polyObj = (IPolyObject)node.EvalWorldState(0, true).Obj.ConvertToType(0, m_Global.PolyObjectClassID);
//        //var polyMesh = polyObj.Mesh;

//        //IBitArray selectionArray = m_ParentNodeDictionary[handle][treeNode.matID].MaterialIDFaceArray;

//        //if (selectionArray != null)
//        //    polyMesh.FaceSelect(selectionArray);
//        //else
//        //    MessageBox.Show("Selection Array is null");
//    }

//    else if (baseObjectRef.ClassID.OperatorEquals(m_Global.Class_ID.Create(0xe44f10b3, 0)) == 1)
//    {
//        //ITriObject triObjWithMod = (ITriObject)node.EvalWorldState(0, true).Obj.ConvertToType(0, m_Global.TriObjectClassID);

//        //Build faceMatID dictionary for triObjects
//        ITriObject triObj = baseObjectRef as ITriObject;
//        var mesh = triObj.Mesh;


//        HashSet<int> vertList;
//        if (index == 0)
//            vertList = m_ParentNodeDictionary[handle][id].MaterialIDVertexList;
//        else
//            vertList = m_ParentNodeDictionary[lpHandle][id].ChildNodeList[index - 1].MaterialIDVertexList;

//        foreach (var i in vertList)
//        {
//            //Get vertex position in local space
//            var localPos = mesh.GetVert(i);

//            //Convert to World space
//            var worldPos = m3Local.PointTransform(localPos);

//            //Make changes
//            worldPos.X += moveValue.X;
//            worldPos.Y += moveValue.Y;
//            worldPos.Z += moveValue.Z;

//            //Invert the local matrix
//            var m3LocalInverse = m_Global.Inverse(m3Local);
//            //Convert back to local space
//            var finalLocalPos = m3LocalInverse.PointTransform(worldPos);

//            //Set vertex local space
//            mesh.SetVert(i, finalLocalPos);
//        }

//        mesh.InvalidateGeomCache();
//        mesh.InvalidateTopologyCache();

//        triObj.NotifyDependents(forever, partAll, RefMessage.Change, (SClass_ID) all, true, null);
//        triObj.NotifyDependents(forever, (UIntPtr) 0, RefMessage.SubanimStructureChanged, (SClass_ID) all, true, null);
//    }

//    node.NotifyDependents(forever, partAll, RefMessage.Change, (SClass_ID) all, true, null);
//    node.NotifyDependents(forever, (UIntPtr) 0, RefMessage.SubanimStructureChanged, (SClass_ID) all, true, null);

//    iface.CommandPanelTaskMode = TASK_MODE_MODIFY;
//    m_Global.COREInterface7.SetCurEditObject(baseObjectRef, null);
//    iface.SetSubObjectLevel(1, true);
//    iface.SetSubObjectLevel(0, true);
//    iface.DeSelectNode(node);
//}










//float y1 = (box1.Max.Y - box2.Min.Y) * ysign;
//float y2 = (box2.Max.Y - box1.Min.Y) * ysign;

//-----------------------------------------------------------------
//float zsign = 1.0f;

//if (box1.Min.Z < box2.Min.Z && box2.Max.Z > box1.Max.Z)
//    zsign = 1.0f;
//else if (box2.Max.Z < box1.Max.Z && box2.Min.Z < box1.Min.Z)
//    zsign = -1.0f;

//else
//{
//    float z1BetweenBoxes = box1.Min.Z - box2.Min.Z;
//    float z2BetweenBoxes = box2.Max.Z - box1.Max.Z;

//    if (Math.Sign(z1BetweenBoxes) != -1 && Math.Sign(z2BetweenBoxes) != -1)
//    {
//        //We got the boxes the other way around so now we need to change the way we compare values
//        if (Math.Abs(z1BetweenBoxes) > Math.Abs(z2BetweenBoxes))
//            zsign = -1.0f;
//        else
//            zsign = 1.0f;
//    }
//    else
//    {
//        if (Math.Abs(z1BetweenBoxes) < Math.Abs(z2BetweenBoxes))
//            zsign = -1.0f;
//        else
//            zsign = 1.0f;
//    }
//}

//float z1 = (box1.Max.Z - box2.Min.Z) * zsign;
//float z2 = (box2.Max.Z - box1.Min.Z) * zsign;


//-----------------------------------------------------------------
//float ysign = 1.0f;
//float y1 = 0, y2 = 0;

//1 side sticking out of the main box.
//if (box1.Min.Y < box2.Min.Y && box2.Max.Y > box1.Max.Y)
//{
//    y1 = (box1.Max.Y - box2.Min.Y) * 1;
//    y2 = (box2.Max.Y - box1.Min.Y) * -1;

//    DebugMethods.Log("1");
//}

////the other side sticking out. 
//else if (box2.Max.Y < box1.Max.Y && box2.Min.Y < box1.Min.Y)
//{
//    y1 = (box1.Max.Y - box2.Min.Y) * 1;
//    y2 = (box2.Max.Y - box1.Min.Y) * - 1;

//    DebugMethods.Log("2");
//}
//else
//{
//    float y1BetweenBoxes = box1.Min.Y - box2.Min.Y;
//    float y2BetweenBoxes = box2.Max.Y - box1.Max.Y;

//    if (Math.Sign(y1BetweenBoxes) != -1 && Math.Sign(y2BetweenBoxes) != -1)
//    {
//        //This is on the positive side of the hull
//        if (Math.Abs(y1BetweenBoxes) > Math.Abs(y2BetweenBoxes))
//        {
//            y1 = (box1.Max.Y - box2.Min.Y) * 1;
//            y2 = (box2.Max.Y - box1.Min.Y) * - 1;
//            DebugMethods.Log("3");
//        }
//        //This is on the negative side of the hull
//        else
//        {
//            y1 = (box1.Max.Y - box2.Min.Y) * 1;
//            y2 = (box2.Max.Y - box1.Min.Y) * -1;

//            DebugMethods.Log("4");
//        }
//    }
//    else
//    {
//        //Both sides are sticking out
//        if (y1BetweenBoxes < y2BetweenBoxes)
//        {
//            y1 = (box1.Max.Y - box2.Min.Y) * 1;
//            y2 = (box2.Max.Y - box1.Min.Y) * -1;

//            DebugMethods.Log("5");
//        }
//        //Both sides are sticking out
//        else
//        {
//            y1 = (box1.Max.Y - box2.Min.Y) * 1;
//            y2 = (box2.Max.Y - box1.Min.Y) * -1;

//            DebugMethods.Log("6");
//        }
//    }

//}


//-----------------------------------------------------------------
//float xsign = 1.0f;

////
//if (box1.Min.X < box2.Min.X && box2.Max.X > box1.Max.X)
//    xsign = 1.0f;
//else if (box2.Max.X < box1.Max.X && box2.Min.X < box1.Min.X)
//    xsign = -1.0f;
//else
//{
//    float x1BetweenBoxes = box1.Min.X - box2.Min.X;
//    float x2BetweenBoxes = box2.Max.X - box1.Max.X;

//    if (Math.Sign(x1BetweenBoxes) != -1 && Math.Sign(x2BetweenBoxes) != -1)
//    {
//        //We got the boxes the other way around so now we need to change the way we compare values
//        if (Math.Abs(x1BetweenBoxes) > Math.Abs(x2BetweenBoxes))
//            xsign = -1.0f;
//        else
//            xsign = 1.0f;
//    }
//    else
//    {
//        if (Math.Abs(x1BetweenBoxes) < Math.Abs(x2BetweenBoxes))
//            xsign = -1.0f;
//        else
//            xsign = 1.0f;
//    }
//}

//float x1 = (box1.Max.X - box2.Min.X) * xsign;
//float x2 = (box2.Max.X - box1.Min.X) * xsign;