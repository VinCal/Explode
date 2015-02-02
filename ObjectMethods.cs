using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Autodesk.Max;
using Test_ExplodeScript;

namespace ExplodeScript
{
    class ObjectMethods
    {
        protected static IGlobal m_Global;

        //private static IMatrix3 m_World, m_Local;

        public static void SetGlobal(IGlobal global)
        {
            m_Global = global;
        }

        public static BaseNode CreateNode(IINode iNode, bool isPolyObject, bool isParentNode)
        {
            //We need to create our 'RealBaseNode' 
            BaseNode tempBaseNode;

            if (isParentNode)
                tempBaseNode = new ParentNode(iNode, isPolyObject);
            else
                tempBaseNode = new ChildNode(iNode, isPolyObject);

            IObject baseObjectRef = iNode.ObjectRef.FindBaseObject();

            if (!isPolyObject)
            {
                //Cast base object to triObject because we know it's a triObject
                ITriObject triObj = baseObjectRef as ITriObject;
                if (triObj == null) return null; //if for some reason the cast failed, return

                IMesh triMesh = triObj.Mesh;

                var numFaces = triMesh.NumFaces;

                DebugMethods.Log(isParentNode? String.Format("ParentNode num face {0}", numFaces) : String.Format("ChildNode num face {0}", numFaces));

                //Build FaceID Dictionary.
                for (int index = 0; index < numFaces; index++)
                {
                    IFace face = triMesh.Faces[index];
                    ushort matID = face.MatID;
                    
                    if (tempBaseNode.DoesKeyExist(matID))
                    {
                        tempBaseNode.SetMaterialIDBit(matID, index);
                    }
                    else
                    {
                        tempBaseNode.CreateNewMaterialBitArray(matID, index, numFaces);
                        
                    }
                }
            }
            else
            {
                IPolyObject polyObj = baseObjectRef as IPolyObject;
                if (polyObj == null) return null; //if for some reason the cast failed, return

                IMNMesh polyMesh = polyObj.Mesh;

                var numFaces = polyMesh.FNum;

                DebugMethods.Log(isParentNode ? String.Format("ParentNode num face {0}", numFaces) : String.Format("ChildNode num face {0}", numFaces));

                //Build FaceID Dictionary.
                for (int index = 0; index < numFaces; index++)
                {
                    IMNFace face = polyMesh.F(index);
                    ushort matID = face.Material;

                    if (tempBaseNode.DoesKeyExist(matID))
                    {
                        //Same material ID, just set the bit
                        //tempBaseNode.GetMaterialBitArray(matID).Set(index, true);
                        tempBaseNode.SetMaterialIDBit(matID, index);
                    }
                    else
                    {
                        tempBaseNode.CreateNewMaterialBitArray(matID, index, numFaces);
                    }
                }
            }
            //We should have a 'RealBaseNode' with a filled up dictionary full of IDs & faces!
            //Maybe there is some weird object with 0 faces? 
            if (tempBaseNode.GetMaterialIDCount() == 0) return null;

            return tempBaseNode;
        }

        


        public static void UpdateFaceDictionary(BaseNode node)
        {
            var triMesh = node.Mesh.TriMesh;
            var polyMesh = node.Mesh.PolyMesh;

            //clear the materialFaceDictionary
            node.ClearMaterialBitArray();


            if (node.Mesh.TriMesh != null)
            {
                var numFaces = triMesh.NumFaces;
                var parentNode = node as ParentNode;

                //Build FaceID Dictionary.
                for (int index = 0; index < numFaces; index++)
                {
                    IFace face = triMesh.Faces[index];
                    ushort matID = face.MatID;

                    //This means it's a child node
                    if (parentNode == null)
                    {
                        UpdateNode(node, matID, index, numFaces);
                    }
                    //So it's a parentNode, but we don't want to update delete material IDs so check if that's not true
                    else if (!parentNode.IsMatIDDeleted(matID))
                    {
                        UpdateNode(node, matID, index, numFaces);
                    }
                }
            }
            else
            {
                var numFaces = polyMesh.FNum;
                var parentNode = node as ParentNode;

                //Build FaceID Dictionary.
                for (int index = 0; index < numFaces; index++)
                {
                    IMNFace face = polyMesh.F(index);
                    ushort matID = face.Material;

                    if (parentNode == null)
                    {
                        UpdateNode(node, matID, index, numFaces);
                    }
                    else if (!parentNode.IsMatIDDeleted(matID))
                    {
                        UpdateNode(node, matID, index, numFaces);
                    }
                }
            }
        }

        private static void UpdateNode(BaseNode node, ushort matID, int index, int numFaces)
        {
            if (node.DoesKeyExist(matID))
                node.SetMaterialIDBit(matID, index);
            else
                node.CreateNewMaterialBitArray(matID, index, numFaces);
        }


        public static unsafe void BuildBoundingBox(ParentNode parentNode, ushort matID)
        {
            var node = parentNode.INode;

            var world = node.GetObjTMAfterWSM(0, null);
            var local = world;

            if (!node.IsRootNode)
            {
                IMatrix3 m3Parent = node.ParentNode.GetObjTMAfterWSM(0, null);
                local = world.Multiply(m_Global.Inverse(m3Parent));
            }

            if (parentNode.Mesh.TriMesh != null) //mesh
            {
                var mesh = parentNode.Mesh.TriMesh;

                BitArray facesPerID = parentNode.GetMaterialBitArray(matID);

                float? xMaxValue = null, xMinValue = null;
                float? yMaxValue = null, yMinValue = null;
                float? zMaxValue = null, zMinValue = null;

                IPoint3 maxValues, minValues;

                //loop through the bitarray to see which faces are set. For the set faces, save the verts
                for (int i = 0; i < facesPerID.Count; i++)
                {
                    if (facesPerID[i])
                    {
                        //This should be a singular face that has the defined ID
                        IFace face = mesh.Faces[i];

                        IntPtr vertsIndices = face.AllVerts;

                        uint* dwordVertIndices = (uint*)vertsIndices.ToPointer();

                        for (int j = 0; j < 3; j++)
                        {
                            var vertexIndex = (int)dwordVertIndices[j];

                            //Maybe don't hold giant lists of vertex indices - just do it while we loop over them all
                            //TODO very important check if this is necessary
                            //parentNode.MaterialIDVertexList.Add(vertexIndex);

                            var pos = mesh.GetVert(vertexIndex);
                            pos = local.PointTransform(pos);

                            FindMaxMinValues(ref xMaxValue, ref xMinValue,
                                                ref yMaxValue, ref yMinValue,
                                                ref zMaxValue, ref zMinValue, pos);
                        }
                    }
                }

                maxValues = m_Global.Point3.Create(xMaxValue.Value, yMaxValue.Value, zMaxValue.Value);
                minValues = m_Global.Point3.Create(xMinValue.Value, yMinValue.Value, zMinValue.Value);

                var bb = new BoundingBox(maxValues, minValues);

                parentNode.SetBoundingBox(matID, bb);
            }
            else
            {
                var mesh = parentNode.Mesh.PolyMesh;

                //Get the bitArray for each ID
                BitArray facesPerID = parentNode.GetMaterialBitArray(matID);

                float? xMaxValue = null, xMinValue = null;
                float? yMaxValue = null, yMinValue = null;
                float? zMaxValue = null, zMinValue = null;

                IPoint3 maxValues, minValues;

                //loop through the bitarray to see which faces are set. For the set faces, save the verts
                for (int i = 0; i < facesPerID.Count; i++)
                {
                    if (facesPerID[i])
                    {
                        //This should be a singular face that has the defined ID
                        IMNFace face = mesh.F(i);
                        IList<int> vertIndices = face.Vtx;

                        foreach (var vertexIndex in vertIndices)
                        {
                            //parentNode.MaterialIDVertexList.Add(vertexIndex);

                            var pos = mesh.P(vertexIndex);
                            pos = local.PointTransform(pos);

                            FindMaxMinValues(ref xMaxValue, ref xMinValue,
                                ref yMaxValue, ref yMinValue,
                                ref zMaxValue, ref zMinValue, pos);
                        }
                    }
                }

                maxValues = m_Global.Point3.Create(xMaxValue.Value, yMaxValue.Value, zMaxValue.Value);
                minValues = m_Global.Point3.Create(xMinValue.Value, yMinValue.Value, zMinValue.Value);

                var bb = new BoundingBox(maxValues, minValues);

                parentNode.SetBoundingBox(matID, bb);
            }

        }

        private static void FindMaxMinValues(   ref float? xMaxValue, ref float? xMinValue,
                                                ref float? yMaxValue, ref float? yMinValue,
                                                ref float? zMaxValue, ref float? zMinValue,
                                                IPoint3 pos)
        {
            //Find max and min X value
            if (!xMaxValue.HasValue || pos.X > xMaxValue.Value)
                xMaxValue = pos.X;
            if (!xMinValue.HasValue || pos.X < xMinValue.Value)
                xMinValue = pos.X;

            //Find max and min Y value
            if (!yMaxValue.HasValue || pos.Y > yMaxValue.Value)
                yMaxValue = pos.Y;
            if (!yMinValue.HasValue || pos.Y < yMinValue.Value)
                yMinValue = pos.Y;

            //Find max and min Z value
            if (!zMaxValue.HasValue || pos.Z > zMaxValue.Value)
                zMaxValue = pos.Z;
            if (!zMinValue.HasValue || pos.Z < zMinValue.Value)
                zMinValue = pos.Z;
        }

        public static void ExpandBoundingBox(ref BoundingBox bb, float scale = 2.0f)
        {
            var worldMaxPos = bb.Max;
            var worldMinPos = bb.Min;

            var worldCenterOfNodeX = (worldMaxPos.X + worldMinPos.X)/2;
            var worldCenterOfNodeY = (worldMaxPos.Y + worldMinPos.Y)/2;
            var worldCenterOfNodeZ = (worldMaxPos.Z + worldMinPos.Z)/2;

            var newWorldMaxXOffset = (worldMaxPos.X - worldCenterOfNodeX) * scale * 0.5;
            var newWorldMaxYOffset = (worldMaxPos.Y - worldCenterOfNodeY) * scale * 0.5;
            var newWorldMaxZOffset = (worldMaxPos.Z - worldCenterOfNodeZ) * scale * 0.5;

            var newWorldMaxXPos = worldMaxPos.X + newWorldMaxXOffset;
            var newWorldMaxYPos = worldMaxPos.Y + newWorldMaxYOffset;
            var newWorldMaxZPos = worldMaxPos.Z + newWorldMaxZOffset;

            var newWorldMaxPos = m_Global.Point3.Create(newWorldMaxXPos, newWorldMaxYPos, newWorldMaxZPos);

            var newWorldMinXOffset = (worldMinPos.X - worldCenterOfNodeX) * scale * 0.5;
            var newWorldMinYOffset = (worldMinPos.Y - worldCenterOfNodeY) * scale * 0.5;
            var newWorldMinZOffset = (worldMinPos.Z - worldCenterOfNodeZ) * scale * 0.5;

            var newWorldMinXPos = worldMinPos.X + newWorldMinXOffset;
            var newWorldMinYPos = worldMinPos.Y + newWorldMinYOffset;
            var newWorldMinZPos = worldMinPos.Z + newWorldMinZOffset;

            var newWorldMinPos = m_Global.Point3.Create(newWorldMinXPos, newWorldMinYPos, newWorldMinZPos);

            bb.Max = newWorldMaxPos;
            bb.Min = newWorldMinPos;
        }

        internal static unsafe void ExplodeNode(BaseNode baseNode, IPoint3 moveValue, ushort matID)
        {
            //A node is a part of an IIINode, it's a matID part of the real IINode
            var node = baseNode.INode;

            var world = node.GetObjTMAfterWSM(0, null);
            var local = world;

            if (!node.IsRootNode)
            {
                IMatrix3 m3Parent = node.ParentNode.GetObjTMAfterWSM(0, null);
                local = world.Multiply(m_Global.Inverse(m3Parent));
            }

            //We basically need to keep all the verts - but this could be millions, or we could keep an ID
            //IINode inode = baseNode.Node;

            //string sKey = matID.ToString();
            //string sMoveValue = string.Format("[{0};{1};{2}]", moveValue.X, moveValue.Y, moveValue.Z);

            //inode.SetUserPropString(ref sKey, ref sMoveValue);
            //---------------------------------------------------------------------------------------------

            if (baseNode.Mesh.TriMesh != null) //mesh
            {
                var mesh = baseNode.Mesh.TriMesh;
                BitArray facesPerID = baseNode.GetMaterialBitArray(matID);
                HashSet<int> uniqueVertexIndexList = new HashSet<int>();

                //loop through the bitarray to see which faces are set. For the set faces, save the verts
                for (int i = 0; i < facesPerID.Count; i++)
                {
                    if (facesPerID[i])
                    {
                        //This should be a singular face that has the defined ID
                        IFace face = mesh.Faces[i];

                        IntPtr vertsIndices = face.AllVerts;

                        uint* dwordVertIndices = (uint*)vertsIndices.ToPointer();

                        for (int j = 0; j < 3; j++)
                        {
                            var vertexIndex = (int)dwordVertIndices[j];
                            uniqueVertexIndexList.Add(vertexIndex);
                        }
                    }
                }
                foreach (var vertexIndex in uniqueVertexIndexList)
                {
                    var localPos = mesh.GetVert(vertexIndex);
                    var finalPos = CalculateWorldPosVerts(localPos, moveValue, local);

                    mesh.SetVert(vertexIndex, finalPos);
                }
            }

            else //poly
            {
                var mesh = baseNode.Mesh.PolyMesh;

                //Get the bitArray for each ID
                BitArray facesPerID = baseNode.GetMaterialBitArray(matID);
                HashSet<int> uniqueVertexIndexList = new HashSet<int>();

                //loop through the bitarray to see which faces are set. For the set faces, save the verts
                for (int i = 0; i < facesPerID.Count; i++)
                {
                    if (facesPerID[i])
                    {
                        //This should be a singular face that has the defined ID
                        IMNFace face = mesh.F(i);
                        IList<int> vertIndices = face.Vtx;

                        foreach (var vertexIndex in vertIndices)
                        {
                            uniqueVertexIndexList.Add(vertexIndex);
                        }
                    }
                }
                foreach (var vertexIndex in uniqueVertexIndexList)
                {
                    var localPos = mesh.P(vertexIndex);
                    var finalPos = CalculateWorldPosVerts(localPos, moveValue, local);
                    mesh.V(vertexIndex).P = finalPos;
                }
            }
        }

        private static IPoint3 CalculateWorldPosVerts(IPoint3 localPos, IPoint3 moveValue, IMatrix3 local)
        {
            //Convert to World space
            var worldPos = local.PointTransform(localPos);

            //Make changes
            worldPos.X += moveValue.X;
            worldPos.Y += moveValue.Y;
            worldPos.Z += moveValue.Z;

            //Invert the local matrix
            var m3LocalInverse = m_Global.Inverse(local);
            //Convert back to local space
            return (m3LocalInverse.PointTransform(worldPos));
        }
    }
}

//internal static void OffsetVertex(BaseNode parentNode, int vertexIndex, IPoint3 moveValue)
//{
//    IPoint3 localPos = m_Global.Point3.Create(0, 0, 0);

//    if (parentNode.Mesh.TriMesh != null) //mesh
//    {
//        var mesh = parentNode.Mesh.TriMesh;
//        //Get vertex position in local space
//        localPos = mesh.GetVert(vertexIndex);

//        var finalPos = MoveVerts(localPos, moveValue);
//        mesh.SetVert(vertexIndex, finalPos);
//    }

//    else
//    {
//        var mesh = parentNode.Mesh.PolyMesh;
//        localPos = mesh.P(vertexIndex);

//        var finalPos = MoveVerts(localPos, moveValue);
//        mesh.V(vertexIndex).P = finalPos;
//    }
//}


//public static unsafe void BuildVertexHashSetFromMatID(BaseNode parentNode, Dictionary<ushort, ParentNode> matIDFaceDictionary, out HashSet<int> vertexIndices)
//{
//    vertexIndices = new HashSet<int>();
//    var numIdUsed = matIDFaceDictionary.Keys.ToArray();

//    if (parentNode.Mesh.TriMesh != null) //mesh
//    {
//        var mesh = parentNode.Mesh.TriMesh;
                
//        foreach (var id in numIdUsed)
//        {
//            //Get the bitArray for each ID
//            IBitArray facesPerID = matIDFaceDictionary[id].MaterialIDFaceArray;

//            float? xMaxValue = null, xMinValue = null;
//            float? yMaxValue = null, yMinValue = null;
//            float? zMaxValue = null, zMinValue = null;

//            IPoint3 maxValues, minValues;

//            //loop through the bitarray to see which faces are set. For the set faces, save the verts
//            for (int i = 0; i < facesPerID.Size; i++)
//            {
//                if (facesPerID[i] == 1)
//                {
//                    //This should be a singular face that has the defined ID
//                    IFace face = parentNode.Mesh.TriMesh.Faces[i];

//                    IntPtr vertsIndices = face.AllVerts;

//                    uint* dwordVertIndices = (uint*) vertsIndices.ToPointer();

//                    for (int j = 0; j < 3; j++)
//                    {
//                        var vertexIndex = (int) dwordVertIndices[j];

//                        //matIDFaceDictionary[id].MaterialIDVertexList.Add(vertexIndex);
//                        vertexIndices.Add(vertexIndex);

//                        var pos = mesh.GetVert(vertexIndex);
//                        pos = m_Local.PointTransform(pos);

//                        FindMaxMinValues(ref xMaxValue, ref xMinValue,
//                                            ref yMaxValue, ref yMinValue,
//                                            ref zMaxValue, ref zMinValue, pos);
//                    }
//                }
//            }

//            maxValues = m_Global.Point3.Create(xMaxValue.Value, yMaxValue.Value, zMaxValue.Value);
//            minValues = m_Global.Point3.Create(xMinValue.Value, yMinValue.Value, zMinValue.Value);


//            BoundingBox bb = new BoundingBox(maxValues, minValues);

//            //matIDFaceDictionary[id].BoundingBox = bb;
                    
//        }
//    }

//    else //poly
//    {
//        var mesh = parentNode.Mesh.PolyMesh;

//        foreach (var id in numIdUsed)
//        {
//            //Get the bitArray for each ID
//            IBitArray facesPerID = matIDFaceDictionary[id].MaterialIDFaceArray;

//            float? xMaxValue = null, xMinValue = null;
//            float? yMaxValue = null, yMinValue = null;
//            float? zMaxValue = null, zMinValue = null;

//            IPoint3 maxValues, minValues;

//            //Create emty hashset
//            HashSet<int> tempHashSet = new HashSet<int>();

//            //loop through the bitarray to see which faces are set. For the set faces, save the verts
//            for (int i = 0; i < facesPerID.Size; i++)
//            {
//                if (facesPerID[i] == 1)
//                {
//                    //This should be a singular face that has the defined ID
//                    IMNFace face = mesh.F(i);
//                    IList<int> vertIndices = face.Vtx;

//                    foreach (var vertexIndex in vertIndices)
//                    {
//                        //matIDFaceDictionary[id].MaterialIDVertexList.Add(vertexIndex);
//                        vertexIndices.Add(vertexIndex);

//                        var pos = mesh.P(vertexIndex);
//                        pos = m_Local.PointTransform(pos);

//                        FindMaxMinValues(ref xMaxValue, ref xMinValue,
//                                            ref yMaxValue, ref yMinValue,
//                                            ref zMaxValue, ref zMinValue, pos);
//                    }
//                }
//            }

//            maxValues = m_Global.Point3.Create(xMaxValue.Value, yMaxValue.Value, zMaxValue.Value);
//            DebugMethods.Log(string.Format("Max world values X: {0}, Y: {1}, Z: {2}", maxValues.X, maxValues.Y, maxValues.Z));

//            minValues = m_Global.Point3.Create(xMinValue.Value, yMinValue.Value, zMinValue.Value);
//            DebugMethods.Log(string.Format("Max world values X: {0}, Y: {1}, Z: {2}", minValues.X, minValues.Y, minValues.Z));

//            BoundingBox bb = new BoundingBox(maxValues, minValues);

//            matIDFaceDictionary[id].BoundingBox = bb;
//        }
//    }
//}

//internal static Dictionary<ushort, ParentNode> UpdateFaceIDArray(ref ParentNode parentNode)
//{
//    var tempMatIDFaceDictionary = new Dictionary<ushort, ParentNode>();

//    if (parentNode.Mesh.TriMesh != null) //mesh
//    {
//        var mesh = parentNode.Mesh.TriMesh;
//        var numFaces = mesh.NumFaces;

//        //Build FaceID Dictionary.
//        //Go over each face again
//        for (int index = 0; index < numFaces; index++)
//        {
//            IFace face = mesh.Faces[index];
//            ushort matID = face.MatID;

//            //If we already added it just set the bit
//            if (tempMatIDFaceDictionary.ContainsKey(matID))
//            {
//                //Same material ID, just set the bit
//                tempMatIDFaceDictionary[matID].MaterialIDFaceArray.Set(index);
//            }
//            else
//            {
//                //new matID found, make empty bitArray and set the bit
//                IBitArray tempBitArray = m_Global.BitArray.Create(numFaces);
//                //Set bit (index)
//                tempBitArray.Set(index);
//                //Assign BitArray to parentNode
//                parentNode.MaterialIDFaceArray = tempBitArray;
//                //Add to dictionary
//                tempMatIDFaceDictionary.Add(matID, parentNode);
//            }
//        }
//    }
//    else
//    {
//        var mesh = parentNode.Mesh.PolyMesh;
//        var numFaces = mesh.FNum;

//        //Build FaceID Dictionary.
//        //Fill up temp Dictionary for each
//        for (int index = 0; index < numFaces; index++)
//        {
//            IMNFace face = mesh.F(index);
//            ushort matID = face.Material;

//            if (tempMatIDFaceDictionary.ContainsKey(matID))
//            {
//                //Same material ID, just set the bit
//                tempMatIDFaceDictionary[matID].MaterialIDFaceArray.Set(index);
//            }
//            else
//            {
//                //new matID found, make empty bitArray and set the bit
//                IBitArray tempBitArray = m_Global.BitArray.Create(numFaces);
//                //Set bit (index)
//                tempBitArray.Set(index);
//                //Replace our old materialIdFaceArray - it's no longer valid (Collapsed happened)
//                parentNode.MaterialIDFaceArray = tempBitArray;
//                //Add to dictionary
//                tempMatIDFaceDictionary.Add(matID, parentNode);
//            }
//        }
//    }

//    return tempMatIDFaceDictionary;
//}



//public static void UpdateFaceIDArray(BaseNode baseNode)
//{
//    if (baseNode.Mesh.TriMesh != null) //mesh
//    {
//        var mesh = baseNode.Mesh.TriMesh;
//        var numFaces = mesh.NumFaces;
//        var tempMatIDFaceDictionary = new Dictionary<ushort, ParentNode>();

//        //Build FaceID Dictionary.
//        //Go over each face again
//        for (int index = 0; index < numFaces; index++)
//        {
//            IFace face = mesh.Faces[index];
//            ushort matID = face.MatID;

//            //If we already added it just set the bit
//            if (tempMatIDFaceDictionary.ContainsKey(matID))
//            {
//                //Same material ID, just set the bit
//                tempMatIDFaceDictionary[matID].MaterialIDFaceArray.Set(index);
//            }
//            else
//            {
//                //new matID found, make empty bitArray and set the bit
//                IBitArray tempBitArray = m_Global.BitArray.Create(numFaces);
//                //Set bit (index)
//                tempBitArray.Set(index);
//                //Replace our old materialIdFaceArray - it's no longer valid (Collapsed happened)
//                baseNode.MaterialIDFaceArray = tempBitArray;
//            }
//        }
//    }
//    else
//    {
//        var mesh = baseNode.Mesh.PolyMesh;
//        var numFaces = mesh.FNum;
//        var tempMatIDFaceDictionary = new Dictionary<ushort, ParentNode>();

//        //Build FaceID Dictionary.
//        //Fill up temp Dictionary for each
//        for (int index = 0; index < numFaces; index++)
//        {
//            IMNFace face = mesh.F(index);
//            ushort matID = face.Material;

//            if (tempMatIDFaceDictionary.ContainsKey(matID))
//            {
//                //Same material ID, just set the bit
//                tempMatIDFaceDictionary[matID].MaterialIDFaceArray.Set(index);
//            }
//            else
//            {
//                //new matID found, make empty bitArray and set the bit
//                IBitArray tempBitArray = m_Global.BitArray.Create(numFaces);
//                //Set bit (index)
//                tempBitArray.Set(index);
//                //Replace our old materialIdFaceArray - it's no longer valid (Collapsed happened)
//                baseNode.MaterialIDFaceArray = tempBitArray;
//            }
//        }
//    }
//}