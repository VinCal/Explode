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
            ChildNodeDictionary = new Dictionary<uint, RealChildNode>();
        }

        /// <summary>
        /// The ChildNode dictionary for the given ID
        /// </summary>
        public Dictionary<uint, RealChildNode> ChildNodeDictionary { get; set; }

        /// <summary>
        /// X, Y, Z coordinates of the Max Values for the given ID
        /// </summary>
        public BoundingBox BoundingBox { get; set; }

        /// <summary>
        /// This makes this sub-node a placeholder or not.
        /// </summary>
        public bool Placeholder { get; set; }
    }


    public class RealParentNode : RealBaseNode
    {
        private readonly uint[] emptyArray = new uint[0];

        public RealParentNode(IINode iNode, bool isPolyObject) : base(iNode, isPolyObject)
        {
            m_ElementParentNodeDictionary = new Dictionary<ushort, ElementParentNode>();
        }

        public RealParentNode()
        {
            m_ElementParentNodeDictionary = new Dictionary<ushort, ElementParentNode>();
        }

        /// <summary>
        /// This Dictionary holds the BitArray for the key (matID) value. 
        /// </summary>
        //private Dictionary<ushort, BitArray> m_MaterialIdFaceDictionary;
        private Dictionary<ushort, ElementParentNode> m_ElementParentNodeDictionary;


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
                m_ElementParentNodeDictionary[matID].Placeholder = true;
            }
        }

        public bool IsPlaceHolder(ushort matID)
        {
            if (m_ElementParentNodeDictionary.ContainsKey(matID))
                return m_ElementParentNodeDictionary[matID].Placeholder;
            return false;
        }

        /// <summary>
        /// Set the child for this Node
        /// </summary>
        /// <param name="handle">Handle of the HP</param>
        /// <param name="matID">Material ID of the parentNode</param>
        /// <param name="childNode">ChildNode to be added</param>
        public bool SetChild(ushort matID, RealChildNode childNode)
        {
            if (childNode == null) throw new Exception("Childnode was null in SetChild method");

            if (!m_ElementParentNodeDictionary.ContainsKey(matID)) //the first time a HP is being added to this MatID
            {
                var tempElementparentNode = new ElementParentNode();
                tempElementparentNode.ChildNodeDictionary.Add(childNode.Handle, childNode);
                //Add the elementNode to the Dictionary
                m_ElementParentNodeDictionary.Add(matID, tempElementparentNode);
            }

            else
            {
                //this means there is already a HP added, let's see if it's not the same one again
                if (!m_ElementParentNodeDictionary[matID].ChildNodeDictionary.ContainsKey(childNode.Handle))
                    m_ElementParentNodeDictionary[matID].ChildNodeDictionary.Add(childNode.Handle, childNode);
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
        public RealChildNode GetChild(ushort matID, uint hpHandle)
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

        //public bool DoesChildKeyExist(ushort matID, uint hpHandle)
        //{
        //    return m_ElementParentNodeDictionary[matID].ChildeNodeDictionary.ContainsKey(hpHandle);
        //}

        public HashSet<RealChildNode> GetUniqueChildNodes()
        {
            //we need to loop over each lp ID and keep unique handles / unique childNodes
            HashSet<RealChildNode> tempChildNodes = new HashSet<RealChildNode>();

            var usedLpIDs = GetUsedMaterialIDsArray();

            foreach (ushort lpID in usedLpIDs)
            {
                if (!m_ElementParentNodeDictionary.ContainsKey(lpID)) continue;

                var usedHpHandles = m_ElementParentNodeDictionary[lpID].ChildNodeDictionary.Keys.ToArray();

                foreach (uint hpHandle in usedHpHandles)
                {
                    //so now we can loop over every childNode, but we only want to keep the unique ones
                    //if (tempUniqueHandles.Add(hpHandle))
                        tempChildNodes.Add(m_ElementParentNodeDictionary[lpID].ChildNodeDictionary[hpHandle]);
                }
            }
            return tempChildNodes;
        }
    }

    public class RealChildNode : RealBaseNode
    {
        public RealChildNode(IINode iNode, bool isPolyObject) : base(iNode, isPolyObject) { }

        public uint ParentHandle { get; set; }
    }

    class SetMaterialBitArrayException : Exception{}

    class CreateMatIDBitArrayException : Exception
    {
        public CreateMatIDBitArrayException(string message):base(message){}
    }

    class SetMaterialBitArrayBitException : Exception
    {
        public SetMaterialBitArrayBitException(string message): base(message){}
    }

    //we should definitely change this :p
    public class RealBaseNode
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

        private IInterval m_Forever;
        private UIntPtr PartAll = (UIntPtr)0xffffffff;

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

        /// <summary>
        /// This hashset keeps track of all our deleted Material IDs 
        /// </summary>
        private HashSet<ushort> m_DeletedMaterialIDs; 
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
        public string Name { get; private set; }

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
        public bool Placeholder{ get; private set; }

        #endregion

        /// <summary>
        /// Creates a real BaseNode
        /// </summary>
        public RealBaseNode(IINode iNode, bool isPolyObject)
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

            m_DeletedMaterialIDs = new HashSet<ushort>();

            Placeholder = false;
        }

        /// <summary>
        /// Creates a placeholder node
        /// </summary>
        public RealBaseNode()
        {
            m_Global = GlobalInterface.Instance;

            //Initialize our dictionary
            m_ElementNodeDictionary = new Dictionary<ushort, ElementNode>();

            Name = "Missing";

            m_DeletedMaterialIDs = new HashSet<ushort>();

            Placeholder = true;
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

            if (m_PreviousFaceCount != currentFaceCount)
                return true;

            return false;
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
                throw new CreateMatIDBitArrayException("Tried to create a Material BitArray with a key that is already used!");
        }



        /// <summary>
        /// Set bit for matID
        /// </summary>
        /// <param name="matID"></param>
        /// <param name="index"></param>
        public void SetMaterialIDBitArrayBit(ushort matID, int index)
        {
            if (m_ElementNodeDictionary.ContainsKey(matID))
            {
                m_ElementNodeDictionary[matID].MaterialIDFaceBitArray.Set(index, true);
            }
            else
                throw new SetMaterialBitArrayBitException("Tried to set a bit but the m_ElementNodeDictionary does not contain this matID!");
        }

        /// <summary>
        /// Set the material FaceBitArray for the given material ID
        /// </summary>
        /// <param name="matID"></param>
        /// <param name="faceBitArray"></param>
        public void SetMaterialBitArray(ushort matID, BitArray faceBitArray)
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
                throw new SetMaterialBitArrayException();
        }
        
        /// <summary>
        /// Returns true if matID exists in m_ElementNodeDictionary, false if not.
        /// </summary>
        public bool DoesKeyExist(ushort matID)
        {
            return m_ElementNodeDictionary.ContainsKey(matID);
        }

        /// <summary>
        /// Returns true if the material ID has been deleted before, false otherwise. 
        /// </summary>
        public bool IsMatIDDeleted(ushort matID)
        {
            return m_DeletedMaterialIDs.Any(deletedMaterialID => matID == deletedMaterialID);
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
        /// Clears the deleted material ID list
        /// </summary>
        public void ClearDeletedMaterialIDs()
        {
            m_DeletedMaterialIDs.Clear();
        }

        /// <summary>
        /// Delete given key from the NodeDictionary
        /// </summary>
        /// <param name="matID"></param>
        public void DeleteMaterialID(ushort matID)
        {
            if (m_ElementNodeDictionary.ContainsKey(matID))
            {
                m_ElementNodeDictionary.Remove(matID);
                //We need to keep track of this deleted MatID
                m_DeletedMaterialIDs.Add(matID);
            }
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
        /// This retuns the deleted material IDs
        /// </summary>
        /// <returns></returns>
        public ushort[] GetDeletedMaterialIDsArray()
        {
            return m_DeletedMaterialIDs.ToArray();
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

    public class BaseNode
    {
        private const int MESH = 0;
        private const int POLY = 1;

        private const int TASK_MODE_MODIFY = 2;

        private IInterval m_Forever;

        private UIntPtr PartAll = (UIntPtr) 0xffffffff;
        private const uint All = 0xffffffff;

        private IGlobal m_Global;

        //private IBitArray[] m_MaterialIDFaceBitArray;
        private BitArray[] m_MaterialIDFaceBitArray;

        private IMesh m_TriMesh;
        private IMNMesh m_PolyMesh;


        public BaseNode(IGlobal global, string name, uint handle)
        {
            m_Global = global;
            Name = name;
            Handle = handle;

            //m_MaterialIDFaceBitArray = new IBitArray[2];
            m_MaterialIDFaceBitArray = new BitArray[2];
            MaterialIDSelectionBitArray = m_Global.BitArray.Create(0);
            MaterialIDVertexList = new HashSet<int>();
            Mesh = new MeshStruct(global);

            m_Forever = m_Global.Interval.Create();
            m_Forever.SetInfinite();
        }

        /// <summary>
        /// Name of IIINode
        /// </summary>
        public string Name { get; private set; }

        public IBitArray MaterialIDSelectionBitArray { get; set; }

        /// <summary>
        /// True if EditablePoly, false if EditableMesh
        /// </summary>
        public bool? IsPolyObject
        {
            get
            {
                return m_IsPolyObject;
            }
            set
            {
                m_IsPolyObject = value;
                UpdateMesh();
            }
        }
        private bool? m_IsPolyObject;

        public MeshStruct Mesh { get; private set; }

        public IINode Node { get; private set; }

        public uint Handle { get; private set; }

        /// <summary>
        /// Keep list of vertex indices - let's maybe remove this one!!
        /// </summary>
        public HashSet<int> MaterialIDVertexList { get; set; }

        //public IBitArray MaterialIDFaceArray form some reason [] operator is gone in 2014 - 2015
        public BitArray MaterialIDFaceArray
        {
            get
            {
                switch (IsPolyObject)
                {
                    case true:
                        return m_MaterialIDFaceBitArray[POLY];
                    case false:
                        return m_MaterialIDFaceBitArray[MESH];
                    case null:
                        MessageBox.Show("You should've set IsPolyObject!");
                        return null;
                }
                return null;
            }
            set
            {
                switch (IsPolyObject)
                {
                    case true:
                        m_MaterialIDFaceBitArray[POLY] = value;
                        break;
                    case false:
                        m_MaterialIDFaceBitArray[MESH] = value;
                        break;
                }
            }
        }
        
        internal void UpdateMesh()
        {
            IInterface iface = m_Global.COREInterface;
            IINode node = iface.GetINodeByHandle(Handle);
                        
            if (node == null)
                return;

            Node = node;

            IObject baseObjectRef = node.ObjectRef.FindBaseObject();

            if (!IsPolyObject.Value) //mesh
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

        internal void InvalidateMesh(bool hasModifiers = false)
        {
            IInterface iface = m_Global.COREInterface;

            IObject baseObjectRef = Node.ObjectRef.FindBaseObject();

            if (!IsPolyObject.Value) //mesh
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
            Node.NotifyDependents(m_Forever, PartAll, RefMessage.Change, (SClass_ID)All, true, null);
            Node.NotifyDependents(m_Forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)All, true, null);

#elif MAX_2015_DEBUG
            Node.NotifyDependents_(m_Forever, PartAll, RefMessage.Change, (SClass_ID)All, true, null);
            Node.NotifyDependents_(m_Forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)All, true, null);
#endif

            if (hasModifiers)
            {
                iface.CommandPanelTaskMode = TASK_MODE_MODIFY;
                m_Global.COREInterface7.SetCurEditObject(baseObjectRef, null);
                iface.SetSubObjectLevel(1, true);
                iface.SetSubObjectLevel(0, true);
                iface.DeSelectNode(Node);
            }
        }

        internal void InvalidateModifiers()
        {
            IInterface iface = m_Global.COREInterface;

            IObject baseObjectRef = Node.ObjectRef.FindBaseObject();

            iface.CommandPanelTaskMode = TASK_MODE_MODIFY;
            m_Global.COREInterface7.SetCurEditObject(baseObjectRef, null);
            iface.SetSubObjectLevel(1, true);
            iface.SetSubObjectLevel(0, true);
            iface.DeSelectNode(Node);
        }
    }

    public class ChildNode : BaseNode
    {
        public ChildNode(IGlobal global, string name, uint handle) : base(global, name, handle) { }

        //public uint ParentHandle { get; set; }
    }

    public class ParentNode : BaseNode
    {
        public ParentNode(IGlobal global, string name, uint handle): base(global, name, handle)
        {
            ChildeNodeDictionaty = new Dictionary<uint, ChildNode>();
        }

        /// <summary>
        /// Holds a list of all childNodes 
        /// </summary>
        //public List<ChildNode> ChildNodeList { get; set; }

        public Dictionary<uint, ChildNode> ChildeNodeDictionaty { get; set; } 

        /// <summary>
        /// X, Y, Z coordinates of the Max Values for the given ID
        /// </summary>
        public BoundingBox BoundingBox { get; set; }
    }














    //public class MasterNode
    //{
    //    private readonly Dictionary<ushort, IBitArray>[] m_MaterialIDFaceDictionary;
    //    private const int Mesh = 0;
    //    private const int Poly = 1;

    //    public MasterNode()
    //    {
    //        //Initialize our Array of Dictionaries...
    //        m_MaterialIDFaceDictionary = new[]
    //        {
    //            new Dictionary<ushort, IBitArray>(),
    //            new Dictionary<ushort, IBitArray>()
    //        };
    //    }

    //    /// <summary>
    //    /// Name of the object
    //    /// </summary>
    //    /// REMOVE
    //    //public string Name { get; set; }

    //    /// <summary>
    //    /// True if EditablePoly, false if EditableMesh
    //    /// </summary>
    //    public bool? IsPolyObject { private get; set; }


    //    /// <summary>
    //    /// Returns the correct 
    //    /// </summary>
    //    public Dictionary<ushort, IBitArray> MaterialIDFaceDictionary
    //    {
    //        get
    //        {
    //            switch (IsPolyObject)
    //            {
    //                case true:
    //                    return m_MaterialIDFaceDictionary[Poly];
    //                case false:
    //                    return m_MaterialIDFaceDictionary[Mesh];
    //                case null:
    //                    MessageBox.Show("You should've set IsPolyObject!");
    //                    return null;
    //            }
    //            return null;
    //        }
    //        set
    //        {
    //            switch (IsPolyObject)
    //            {
    //                case true:
    //                    m_MaterialIDFaceDictionary[Poly] = value;
    //                    break;
    //                case false:
    //                    m_MaterialIDFaceDictionary[Mesh] = value;
    //                    break;
    //            }
    //        }
    //    }




    //    /// <summary>
    //    /// Hold Vertex Index per Material ID
    //    /// </summary>
    //    public Dictionary<ushort, HashSet<int>> MaterialIDVertexDictionary { get; set; }
    //}

    //public class VisualMatIDNode
    //{
    //    /// <summary>
    //    /// Name of the object
    //    /// </summary>
    //    public string Name { get; set; }

    //    /// <summary>
    //    /// Holds the Handle to the object this visual node is tied to. 
    //    /// </summary>
    //    public uint Handle { get; set; }

    //    /// <summary>
    //    /// Holds the ID
    //    /// </summary>
    //    public ushort ID { get; set; }

    //    /// <summary>
    //    /// Holds the BitArray linked to the ID
    //    /// </summary>
    //    public IBitArray MatIDBitArray { get; set; }

    //    /// <summary>
    //    /// List of all visualNodes that share the same ID. 
    //    /// </summary>
    //    public List<MasterNode> ChildrenVisualMatIdNodes { get; set; } 
    //}
}



//public class ParentNode
//{
//    private const int MESH = 0;
//    private const int POLY = 1;

//    private const int TASK_MODE_MODIFY = 2;

//    private IInterval m_Forever;

//    private UIntPtr PartAll = (UIntPtr)0xffffffff;
//    private const uint All = 0xffffffff;

//    private IGlobal m_Global;

//    private IBitArray[] m_MaterialIDFaceBitArray;

//    private IMesh m_TriMesh;
//    private IMNMesh m_PolyMesh;

//    public ParentNode(IGlobal global)
//    {
//        m_MaterialIDFaceBitArray = new IBitArray[2];
//        MaterialIDVertexList = new HashSet<int>();
//        ChildNodeList = new List<ChildNode>();

//        m_Global = global;

//        m_Forever = m_Global.Interval.Create();
//        m_Forever.SetInfinite();
//    }

//    /// <summary>
//    /// Name of IIINode
//    /// </summary>
//    public string Name { get; set; }

//    /// <summary>
//    /// True if EditablePoly, false if EditableMesh
//    /// </summary>
//    public bool? IsPolyObject { private get; set; }

//    public IBitArray MaterialIDFaceArray
//    {
//        get
//        {
//            switch (IsPolyObject)
//            {
//                case true:
//                    return m_MaterialIDFaceBitArray[POLY];
//                case false:
//                    return m_MaterialIDFaceBitArray[MESH];
//                case null:
//                    MessageBox.Show("You should've set IsPolyObject!");
//                    return null;
//            }
//            return null;
//        }
//        set
//        {
//            switch (IsPolyObject)
//            {
//                case true:
//                    m_MaterialIDFaceBitArray[POLY] = value;
//                    break;
//                case false:
//                    m_MaterialIDFaceBitArray[MESH] = value;
//                    break;
//            }
//        }
//    }

//    public Mesh Mesh
//    {
//        get;
//        private set;
//    }

//    public IINode Node
//    {
//        get;
//        set;
//    }

//    /// <summary>
//    /// Keep list of vertex indices
//    /// </summary>
//    public HashSet<int> MaterialIDVertexList { get; set; }

//    /// <summary>
//    /// Holds a list of all childNodes 
//    /// </summary>
//    public List<ChildNode> ChildNodeList { get; set; }

//    /// <summary>
//    /// X, Y, Z coordinates of the Max Values for the given ID
//    /// </summary>
//    public BoundingBox BoundingBox { get; set; }

//    //public IMesh TriMesh { get; set; }

//    internal void UpdateMesh(uint handle)
//    {
//        IInterface iface = m_Global.COREInterface;
//        IINode node = iface.GetINodeByHandle(handle);

//        if (node == null)
//            return;

//        Node = node;

//        IObject baseObjectRef = node.ObjectRef.FindBaseObject();

//        if (!IsPolyObject.Value) //mesh
//        {
//            var triObj = baseObjectRef as ITriObject;
//            Mesh.TriMesh = triObj.Mesh;
//        }
//        else
//        {
//            var polyObj = baseObjectRef as IPolyObject;
//            Mesh.PolyMesh = polyObj.Mesh;
//        }
//    }

//    internal void InvalidateMesh()
//    {
//        IInterface iface = m_Global.COREInterface;

//        IObject baseObjectRef = Node.ObjectRef.FindBaseObject();

//        if (!IsPolyObject.Value) //mesh
//        {
//            Mesh.TriMesh.InvalidateGeomCache();
//            Mesh.TriMesh.InvalidateTopologyCache();

//            var triObj = baseObjectRef as ITriObject;
//            triObj.NotifyDependents(m_Forever, PartAll, RefMessage.Change, (SClass_ID)All, true, null);
//            triObj.NotifyDependents(m_Forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)All, true, null);
//        }

//        else
//        {
//            Mesh.PolyMesh.InvalidateGeomCache();
//            Mesh.PolyMesh.InvalidateTopoCache(false);

//            var polyObj = baseObjectRef as IPolyObject;
//            polyObj.NotifyDependents(m_Forever, PartAll, RefMessage.Change, (SClass_ID)All, true, null);
//            polyObj.NotifyDependents(m_Forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)All, true, null);
//        }


//        Node.NotifyDependents(m_Forever, PartAll, RefMessage.Change, (SClass_ID)All, true, null);
//        Node.NotifyDependents(m_Forever, (UIntPtr)0, RefMessage.SubanimStructureChanged, (SClass_ID)All, true, null);

//        iface.CommandPanelTaskMode = TASK_MODE_MODIFY;
//        m_Global.COREInterface7.SetCurEditObject(baseObjectRef, null);
//        iface.SetSubObjectLevel(1, true);
//        iface.SetSubObjectLevel(0, true);
//        iface.DeSelectNode(Node);
//    }
//}