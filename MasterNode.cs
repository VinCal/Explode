using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media.TextFormatting;
using Autodesk.Max;
using Autodesk.Max.CAssertCB;
using Autodesk.Max.Plugins;
using ExplodeScript;

namespace Test_ExplodeScript
{
    public class BoundingBox
    {
        public BoundingBox(IPoint3 max, IPoint3 min)
        {
            Max = max;
            Min = min;
            CalculateVolume();
        }

        private void CalculateVolume()
        {
            //x
            float length = Max.X - Min.X;
            //y
            float height = Max.Y - Min.Y;
            //z
            float width = Max.Z - Min.Z;

            Volume = length*height*width;
        }

        public static bool operator ==(BoundingBox x, BoundingBox y)
        {
            return  x.Max.X == y.Max.X && x.Max.Y == y.Max.Y && x.Max.Z == y.Max.Z &&
                    x.Min.X == y.Min.X && x.Min.Y == y.Min.Y && x.Min.Z == y.Min.Z;
        }

        public static bool operator !=(BoundingBox x, BoundingBox y)
        {
            return !(x == y);
        }

        public IPoint3 Max { get; set; }
        public IPoint3 Min { get; set; }
        public float Volume { get; set; }
    }

    public class BoundingBoxHandleID
    {
        public BoundingBoxHandleID()
        {
            MovedValues = new List<MoveStruct>();
        }

        public BoundingBox BoundingBox { get; set; }
        public uint Handle { get; set; }
        public ushort ID { get; set; }

        public IPoint3 MoveValue { get; set; }
        public List<MoveStruct> MovedValues;
    }

    public enum Coordinates
    {
        X,
        Y,
        Z
    }

    public class MoveStruct
    {
        public MoveStruct(float moveValue, Coordinates direction)
        {
            MoveValue = moveValue;
            Direction = direction;
        }

        public float MoveValue { get; private set; }
        public Coordinates Direction { get; private set; }
    }

    public class MeshStruct
    {
        public MeshStruct(IGlobal global)
        {
            TriMesh = global.Mesh.Create();
            PolyMesh = global.MNMesh.Create();
        }

        public IMesh TriMesh { get; set; }
        public IMNMesh PolyMesh { get; set; }
    }

    public class ElementNode
    {
        /// <summary>
        /// The Material BitArray for the given ID
        /// </summary>
        public BitArray MaterialIDFaceBitArray { get; set; }

        public IBitArray MaterialIDFaceIBitArraySelection { get; set; }
    }

    public class ElementParentNode : ElementNode
    {
        public ElementParentNode()
        {
            ChildNodeDictionary = new Dictionary<uint, ChildNode>();
        }

        /// <summary>
        /// The ChildNode dictionary for the given ID
        /// </summary>
        public Dictionary<uint, ChildNode> ChildNodeDictionary { get; set; }

        /// <summary>
        /// X, Y, Z coordinates of the Max Values for the given ID
        /// </summary>
        public BoundingBox BoundingBox { get; set; }

        /// <summary>
        /// This makes this sub-node a placeholder or not.
        /// </summary>
        public bool Placeholder { get; set; }
    }


    public class ParentNode : BaseNode
    {
        /// <summary>
        /// Empty uint array to return
        /// </summary>
        private readonly uint[] emptyArray = new uint[0];

        /// <summary>
        /// This Dictionary holds the BitArray for the key (matID) value. 
        /// </summary>
        //private Dictionary<ushort, BitArray> m_MaterialIdFaceDictionary;
        private Dictionary<ushort, ElementParentNode> m_ElementParentNodeDictionary;

        /// <summary>
        /// This hashset keeps track of all our deleted Material IDs 
        /// </summary>
        private HashSet<ushort> m_DeletedMaterialIDs; 

        /// <summary>
        /// Real node constructor
        /// </summary>
        public ParentNode(IINode iNode, bool isPolyObject) : base(iNode, isPolyObject)
        {
            m_ElementParentNodeDictionary = new Dictionary<ushort, ElementParentNode>();
            m_DeletedMaterialIDs = new HashSet<ushort>();
        }

        
        /// <summary>
        /// Updates the Material Bit Array with the data of newParentNode
        /// </summary>
        /// <returns>Returns a list of all the IDs that have been added</returns>
        public List<ushort> UpdateMaterialBitArray(ParentNode newParentNode)
        {
            var addedIDslist = new List<ushort>();

            //We only want to update our m_ElementNodeDictionary if the parentNode is actually already in our list
            if (this.Handle == newParentNode.Handle)
            {
                //Currently active IDs in the m_RealParentNodeDictionary
                var usedIDs = GetUsedMaterialIDsArray();
                //All of the Material IDs in parentNode
                var newIDs = newParentNode.GetUsedMaterialIDsArray();              

                //All of the material IDs in parentNode - currently active IDs
                var toBeAddedIDs = newIDs.Except(usedIDs).ToArray();

                Array.Sort(toBeAddedIDs);
                string ids = string.Empty;

                //Nodes that were deleted with no kids - so no placeholder nodes
                foreach (ushort beAddedID in toBeAddedIDs)
                {
                    //Add the materialIDBitArray at the beAddedID key
                    SetMaterialBitArray(beAddedID, newParentNode.GetMaterialBitArray(beAddedID));

                    //ids += beAddedID + 1 + ", ";
                    addedIDslist.Add(beAddedID);
                }

                
                //Now we check for placeholder nodes - they have children so we need to add them again too 
                var placeHolderIDs = GetPlaceholderIDs();

                var tempChildNodeList = new List<ChildNode>();
                foreach (ushort placeHolderID in placeHolderIDs)
                {
                    //Get the all the children of the placeholder
                    var childHandles = GetChildHandles(placeHolderID);
                    foreach (uint childHandle in childHandles)
                    {
                        tempChildNodeList.Add(GetChild(placeHolderID, childHandle));
                    }

                    //Remove the placeHolder
                    RemovePlaceholderNode(placeHolderID);
                    //Give this node the correct name, instead of 'Missing LP'
                    this.Name = newParentNode.Name;

                    SetMaterialBitArray(placeHolderID, newParentNode.GetMaterialBitArray(placeHolderID));
                    foreach (ChildNode childNode in tempChildNodeList)
                    {
                        SetChild(placeHolderID, childNode);
                    }

                    //ids += placeHolderID + 1 + ", ";
                    addedIDslist.Add(placeHolderID);
                }

                return addedIDslist;
            }
                
            throw new UpdateMaterialBitArrayException(newParentNode);
        }

        /// <summary>
        /// Sets boundingBox for the given ID
        /// </summary>
        /// <param name="matID">Material ID linked with this BB</param>
        public void SetBoundingBox(ushort matID, BoundingBox bb)
        {
            if (!m_ElementParentNodeDictionary.ContainsKey(matID)) //the key does not exists yet //this means the node doesn't have a HP added
            {
                var tempElementparentNode = new ElementParentNode
                {
                    BoundingBox = bb
                };

                m_ElementParentNodeDictionary.Add(matID, tempElementparentNode);
            }
            else
            {
                m_ElementParentNodeDictionary[matID].BoundingBox = bb;
            }
        }

        /// <summary>
        /// Get the bounding box for this material ID
        /// </summary>
        public BoundingBox GetBoundingBox(ushort matID)
        {
            if (!m_ElementParentNodeDictionary.ContainsKey(matID))
                return null;

            return m_ElementParentNodeDictionary[matID].BoundingBox;
        }

        /// <summary>
        /// Sets the sub-node's placeholder value
        /// </summary>
        /// <param name="matID"></param>
        public void SetPlaceHolderID(ushort matID) 
        {
            if (!m_ElementParentNodeDictionary.ContainsKey(matID))
            {
                var tempElementParentNode = new ElementParentNode() {Placeholder = true};

                m_ElementParentNodeDictionary.Add(matID, tempElementParentNode);
                m_ElementNodeDictionary.Add(matID, null);
            }
            else
            {
                //m_ElementParentNodeDictionary[matID].Placeholder = true;
                throw new Exception("We shouldn't set an already existing node to PlaceHolder. (MasterNode.cs)");
            }
        }

        /// <summary>
        /// Returns if the node at matID is a placeholder node
        /// </summary>
        public bool IsPlaceHolder(ushort matID)
        {
            if (m_ElementParentNodeDictionary.ContainsKey(matID))
                return m_ElementParentNodeDictionary[matID].Placeholder;
            return false;
        }

        /// <summary>
        /// Removes placeholder node at given key (matID)
        /// </summary>
        public void RemovePlaceholderNode(ushort matID)
        {
            if (m_ElementParentNodeDictionary.ContainsKey(matID))
            {
                if (m_ElementParentNodeDictionary[matID].Placeholder)
                {
                    m_ElementNodeDictionary.Remove(matID);
                    m_ElementParentNodeDictionary.Remove(matID);


                }
                else
                    throw new Exception("tried to delete placeholder node but the node isn't set as one.");
            }
            else
            {
                throw new Exception("The node isn't present in the dictionary");
            }
        }

        private HashSet<ushort> GetPlaceholderIDs()
        {
            var tempSet = new HashSet<ushort>();

            var usedMatIDS = m_ElementParentNodeDictionary.Keys.ToArray();
            foreach (ushort matID in usedMatIDS.Where(matID => m_ElementParentNodeDictionary[matID].Placeholder))
            {
                tempSet.Add(matID);
            }

            return tempSet;
        }




        /// <summary>
        /// Delete given key from the NodeDictionary
        /// </summary>
        /// <param name="matID"></param>
        public void RemoveMaterialID(ushort matID, bool delete = true)
        {
            if (m_ElementParentNodeDictionary.ContainsKey(matID))//m_ElementNodeDictionary
            {
                m_ElementParentNodeDictionary.Remove(matID);
                m_ElementNodeDictionary.Remove(matID);

                if (delete)
                    m_DeletedMaterialIDs.Add(matID);
            }
        }

        /// <summary>
        /// This retuns the deleted material IDs
        /// </summary>
        /// <returns></returns>
        public ushort[] GetDeletedMaterialIDsArray()
        {
            return m_DeletedMaterialIDs.ToArray();
        }

        /// <summary>
        /// Returns true if the material ID has been deleted before, false otherwise. 
        /// </summary>
        public bool IsMatIDDeleted(ushort matID)
        {
            return m_DeletedMaterialIDs.Any(deletedMaterialID => matID == deletedMaterialID);
        }

        /// <summary>
        /// Clears the deleted material ID list
        /// </summary>
        public void ClearDeletedMaterialIDs()
        {
            m_DeletedMaterialIDs.Clear();
        }

        /// <summary>
        /// Set the child for this Node
        /// </summary>
        /// <param name="matID">Material ID of the parentNode</param>
        /// <param name="childNode">ChildNode to be added</param>
        public bool SetChild(ushort matID, ChildNode childNode)
        {
            if (childNode == null) throw new Exception("Childnode was null in SetChild method");

            if (!m_ElementParentNodeDictionary.ContainsKey(matID)) //the first time a HP is being added to this MatID
            {
                var tempElementparentNode = new ElementParentNode();
                tempElementparentNode.ChildNodeDictionary.Add(childNode.Handle, childNode);

                m_ElementParentNodeDictionary.Add(matID, tempElementparentNode);
            }

            else
            {
                //this means there is already a HP added, let's see if it's not the same one again
                if (!m_ElementParentNodeDictionary[matID].ChildNodeDictionary.ContainsKey(childNode.Handle))
                {
                    m_ElementParentNodeDictionary[matID].ChildNodeDictionary.Add(childNode.Handle, childNode);
                }
                else
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the childNode
        /// </summary>
        /// <param name="handle">Handle of the HP</param>
        /// <param name="matID">Material ID of the parentNode</param>
        /// <returns></returns>
        public ChildNode GetChild(ushort matID, uint hpHandle)
        {
            if (m_ElementParentNodeDictionary.ContainsKey(matID))
                if (m_ElementParentNodeDictionary[matID].ChildNodeDictionary.ContainsKey(hpHandle))
                    return m_ElementParentNodeDictionary[matID].ChildNodeDictionary[hpHandle];

            return null;
        }

        /// <summary>
        /// Deletes Child from Child Dictionary
        /// </summary>
        /// <param name="handle">Handle of the HP</param>
        /// <param name="matID">Mat ID of the HP</param>
        public void DeleteChild(uint hpHandle, ushort matID)
        {
            if (m_ElementParentNodeDictionary[matID].ChildNodeDictionary.ContainsKey(hpHandle))
                m_ElementParentNodeDictionary[matID].ChildNodeDictionary.Remove(hpHandle);
        }

        /// <summary>
        /// Gets the childNode
        /// </summary>
        /// <param name="handle">Handle of the parentNode</param>
        /// <param name="matID">Material ID of the parentNode</param>
        /// <returns></returns>
        public uint[] GetChildHandles(ushort matID)
        {
            if (!m_ElementParentNodeDictionary.ContainsKey(matID))
                return emptyArray;
            if (m_ElementParentNodeDictionary[matID].ChildNodeDictionary == null)
                return emptyArray;

            return m_ElementParentNodeDictionary[matID].ChildNodeDictionary.Keys.ToArray();
        }

        public List<ChildNode> GetUniqueChildNodes()
        {
            //we need to loop over each lp ID and keep unique handles / unique childNodes
            var tempChildNodes = new List<ChildNode>();
            var tempHpHandleChildNodes = new HashSet<uint>();

            var usedLpIDs = GetUsedMaterialIDsArray();

            foreach (ushort lpID in usedLpIDs)
            {
                if (!m_ElementParentNodeDictionary.ContainsKey(lpID)) continue;

                var usedHpHandles = m_ElementParentNodeDictionary[lpID].ChildNodeDictionary.Keys.ToArray();

                foreach (uint hpHandle in usedHpHandles)
                {
                    if (tempHpHandleChildNodes.Add(hpHandle))
                        tempChildNodes.Add(m_ElementParentNodeDictionary[lpID].ChildNodeDictionary[hpHandle]);
                }
            }
            return tempChildNodes;
        }
    }

    public class ChildNode : BaseNode
    {
        public ChildNode(IINode iNode, bool isPolyObject) : base(iNode, isPolyObject) { }

        public uint ParentHandle { get; set; }
    }
    

    public class BaseNode
    {
        //this one should hold the information about the actual node / mesh
        //if it's editable mesh or poly, how many material ids it has
        //it should also be responsible for updating the meshes
        //maybe this baseNode should hold a dictionary instead

        //so we would have a dictionary with RealBaseNodes in it, who in turn hold a dictionary of all the mat ids
        //no more double dictionary -- easier. Here we could also store all the material ids used! I think this is a good idea,
        //but requires a full rewrite.... But will make it easier!

        //What should this class hold?
        //NumMatsUsed
        //IsPolyObject 
        //A dictionary that holds all the material ID BitArrays

        //--Const values--
        private const uint  All = 0xffffffff;
        //------------------

        private readonly IInterval m_Forever;
        private readonly UIntPtr PartAll = (UIntPtr)0xffffffff;

        #region Private Datamembers
        /// <summary>
        /// This bool keeps track if our node is a Editable Poly or Editable Mesh. 
        /// </summary>
        private bool? m_IsPolyObject;

        /// <summary>
        /// They keeps the facecount of an object
        /// </summary>
        private int m_PreviousFaceCount;


        /// <summary>
        /// Keeps track of our previousBaseObject
        /// </summary>
        private IObject m_PreviousBaseObject;

        /// <summary>
        /// This Dictionary holds the BitArray for the key (matID) value. 
        /// </summary>
        //private Dictionary<ushort, BitArray> m_MaterialIdFaceDictionary;
        protected Dictionary<ushort, ElementNode> m_ElementNodeDictionary;
        #endregion

        #region Properties
        /// <summary>
        /// Global object
        /// </summary>
        private IGlobal m_Global;

        /// <summary>
        /// The actual IINode
        /// </summary>
        public IINode INode { get; private set; }

        /// <summary>
        /// Name of the node
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        /// Handle of the node (not sure if we need this!)
        /// </summary>
        public uint Handle { get; private set; }

        /// <summary>
        /// Hold a reference to the Editable Mesh or Poly for this handle. 
        /// </summary>
        public MeshStruct Mesh { get; private set; }

        /// <summary>
        /// This keeps track if it's a placeholder node - that means you won't be able to click it, text is red, ...
        /// </summary>
        //public bool Placeholder{ get; private set; }

        #endregion

        /// <summary>
        /// Creates a real BaseNode
        /// </summary>
        public BaseNode(IINode iNode, bool isPolyObject)
        {
            m_Global = GlobalInterface.Instance;

            //Initialize our mesh Struct
            Mesh = new MeshStruct(m_Global);

            //Initialize our dictionary
            m_ElementNodeDictionary = new Dictionary<ushort, ElementNode>();

            INode = iNode;
            Name = iNode.Name;
            Handle = iNode.Handle;

            SetIsPolyObject(isPolyObject);

            m_Forever = m_Global.Interval.Create();
            m_Forever.SetInfinite();
        }

        /// <summary>
        /// Sets the m_IsPolyObjec to param
        /// </summary>
        /// <param name="isPolyObject"></param>
        /// <returns>true if something drastic has changed - either mesh / poly change or polyCount change</returns>
        public bool SetIsPolyObject(bool isPolyObject)
        {
            if (m_IsPolyObject != isPolyObject)
            {
                m_IsPolyObject = isPolyObject;
                UpdateMesh();
                return true;
            }

            m_PreviousFaceCount = getFaceCount();
            UpdateMesh();
            var currentFaceCount = getFaceCount();

            return m_PreviousFaceCount != currentFaceCount;
        }

        /// <summary>
        /// Creates new BitArray at given key (matID) in m_ElementNodeDictionary. Set the bit at index
        /// </summary>
        /// <param name="matID">key for m_ElementNodeDictionary</param>
        /// <param name="index">index of the bit that will be set when this bitArray is created</param>
        /// <param name="size">size of the bitArray</param>
        public void CreateNewMaterialBitArray(ushort matID, int index, int size)
        {
            if (!m_ElementNodeDictionary.ContainsKey(matID))
            {
                var tempBitArray = new BitArray(size);
                tempBitArray.Set(index, true);
                var tempElementNode = new ElementNode()
                {
                    MaterialIDFaceBitArray = tempBitArray,
                    MaterialIDFaceIBitArraySelection = m_Global.BitArray.Create()
                };

                m_ElementNodeDictionary.Add(matID, tempElementNode);
            }

            else
                throw new CreateMatIDBitArrayException(matID);
        }

        /// <summary>
        /// Set bit for matID
        /// </summary>
        /// <param name="matID"></param>
        /// <param name="index"></param>
        public void SetMaterialIDBit(ushort matID, int index)
        {
            if (m_ElementNodeDictionary.ContainsKey(matID))
            {
                m_ElementNodeDictionary[matID].MaterialIDFaceBitArray.Set(index, true);
            }
            else
                throw new SetMaterialBitException(matID, index);
        }

        /// <summary>
        /// Set the material FaceBitArray for the given material ID
        /// </summary>
        protected void SetMaterialBitArray(ushort matID, BitArray faceBitArray)
        {
            if (!m_ElementNodeDictionary.ContainsKey(matID))
            {
                var tempElementNode = new ElementNode
                {
                    MaterialIDFaceBitArray = faceBitArray,
                    MaterialIDFaceIBitArraySelection = m_Global.BitArray.Create()
                };

                m_ElementNodeDictionary.Add(matID, tempElementNode);
            }

            else
                throw new SetMaterialBitArrayException(matID);
        }
        
        /// <summary>
        /// Returns true if matID exists in m_ElementNodeDictionary, false if not.
        /// </summary>
        public bool DoesKeyExist(ushort matID)
        {
            return m_ElementNodeDictionary.ContainsKey(matID);
        }

        /// <summary>
        /// Get the value of the Material ID Dictionary
        /// Returns null if the key is not present
        /// </summary>
        public BitArray GetMaterialBitArray(ushort matID)
        {
            if (!m_ElementNodeDictionary.ContainsKey(matID))
                return null;

            return m_ElementNodeDictionary[matID].MaterialIDFaceBitArray;
        }

        /// <summary>
        /// Clears all element nodes from the dictionary
        /// </summary>
        public void ClearMaterialBitArray()
        {
           m_ElementNodeDictionary.Clear();
        }

        /// <summary>
        /// This returns the amount of material IDs in use
        /// </summary>
        /// <returns></returns>
        public int GetMaterialIDCount()
        {
            return m_ElementNodeDictionary.Keys.Count;
        }

        /// <summary>
        /// This returns the material IDs in use
        /// </summary>
        /// <returns></returns>
        public ushort[] GetUsedMaterialIDsArray()
        {
            return m_ElementNodeDictionary.Keys.ToArray();
        }
        

        /// <summary>
        /// Get the IBitArray so we can convert our BitArray to IBitArray
        /// Only used for selection because [] operator is gone after 2013 SDK...
        /// </summary>
        /// <param name="matID"></param>
        /// <returns></returns>
        public IBitArray GetSelectionBitArray(ushort matID)
        {
            if (!m_ElementNodeDictionary.ContainsKey(matID))
                return null;

            return m_ElementNodeDictionary[matID].MaterialIDFaceIBitArraySelection;
        }

        private void UpdateMesh()
        {
            IObject baseObjectRef = INode.ObjectRef.FindBaseObject();
            m_PreviousBaseObject = baseObjectRef;

            if (!m_IsPolyObject.Value) //mesh
            {
                var triObj = baseObjectRef as ITriObject;

                Mesh.TriMesh = triObj.Mesh;
                Mesh.PolyMesh = null;
            }
            else
            {
                var polyObj = baseObjectRef as IPolyObject;

                Mesh.PolyMesh = polyObj.Mesh;
                Mesh.TriMesh = null;
            }
        }
        
        public void InvalidateMesh()
        {
            IObject baseObjectRef = INode.ObjectRef.FindBaseObject();

            if (!m_IsPolyObject.Value) //mesh
            {
                Mesh.TriMesh.InvalidateGeomCache();
                Mesh.TriMesh.InvalidateTopologyCache();

                var triObj = baseObjectRef as ITriObject;

#if (MAX_2012_DEBUG || MAX_2013_DEBUG || MAX_2014_DEBUG )
                triObj.NotifyDependents(m_Forever, PartAll, RefMessage.Change, (SClass_ID)All, true, null);
                triObj.NotifyDependents(m_Forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)All, true, null);
          
#elif MAX_2015_DEBUG
                triObj.NotifyDependents_(m_Forever, PartAll, RefMessage.Change, (SClass_ID)All, true, null);
                triObj.NotifyDependents_(m_Forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)All, true, null);
#endif 
            }

            else
            {
                Mesh.PolyMesh.InvalidateGeomCache();
                Mesh.PolyMesh.InvalidateTopoCache(false);

                var polyObj = baseObjectRef as IPolyObject;

#if (MAX_2012_DEBUG || MAX_2013_DEBUG || MAX_2014_DEBUG )
                polyObj.NotifyDependents(m_Forever, PartAll, RefMessage.Change, (SClass_ID)All, true, null);
                polyObj.NotifyDependents(m_Forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)All, true, null);

#elif MAX_2015_DEBUG
                polyObj.NotifyDependents_(m_Forever, PartAll, RefMessage.Change, (SClass_ID)All, true, null);
                polyObj.NotifyDependents_(m_Forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)All, true, null);
#endif
            }

#if (MAX_2012_DEBUG || MAX_2013_DEBUG || MAX_2014_DEBUG )
            INode.NotifyDependents(m_Forever, PartAll, RefMessage.Change, (SClass_ID)All, true, null);
            INode.NotifyDependents(m_Forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)All, true, null);

#elif MAX_2015_DEBUG
            INode.NotifyDependents_(m_Forever, PartAll, RefMessage.Change, (SClass_ID)All, true, null);
            INode.NotifyDependents_(m_Forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)All, true, null);
#endif

            //if (hasModifiers)
            //{
            //    iface.CommandPanelTaskMode = TASK_MODE_MODIFY;
            //    m_Global.COREInterface7.SetCurEditObject(baseObjectRef, null);
            //    iface.SetSubObjectLevel(1, true);
            //    iface.SetSubObjectLevel(0, true);
            //}
            //iface.DeSelectNode(INode);
        }

        private int getFaceCount()
        {
            if (!m_IsPolyObject.Value) //mesh
            {
                if (m_PreviousBaseObject != null)
                {
                    var triObj = m_PreviousBaseObject as ITriObject;
                    return triObj.Mesh.NumFaces;
                }
            }

            if (m_PreviousBaseObject != null)
            {
                var polyObj = m_PreviousBaseObject as IPolyObject;
                return polyObj.Mesh.Numf;
            }

            return 0;
        }

        //Debug methods
        //[Conditional("DEBUG")]
        //public static void PrintNumIDsAndNumFaces()
        //{
        //    var matIDused = m_ElementNodeDictionary.Keys.ToArray();
        //    foreach (ushort matID in matIDused)
        //    {
        //        int faceCount = 0;
        //        for (int i = 0; i < m_ElementNodeDictionary[matID].MaterialIDFaceBitArray.Count; i++)
        //        {
        //            if (m_ElementNodeDictionary[matID].MaterialIDFaceBitArray[i])
        //                ++faceCount;
        //        }
        //        DebugMethods.Log(String.Format("MatID: {0} holds {1} faces.", matID + 1, faceCount));
        //    }
        //}
    }

}