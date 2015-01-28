using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Max;
using Test_ExplodeScript;

namespace ExplodeScript
{
    class TriMethods : ObjectMethods
    {
        /// <summary>
        /// Builds the MatID dictionary for EditableMesh
        /// </summary>
        public unsafe static Dictionary<ushort, ParentNode> BuildParentNodeDictionary(IINode node, IMesh mesh)
        {
            var numFaces = mesh.NumFaces;
            var tempMatIDFaceDictionary = new Dictionary<ushort, ParentNode>();

            //Build FaceID Dictionary.
            //Fill up temp Dictionary for each
            for (int index = 0; index < numFaces; index++)
            {
                IFace face = mesh.Faces[index];
                ushort matID = face.MatID;

                if (tempMatIDFaceDictionary.ContainsKey(matID))
                {
                    //Same material ID, just set the bit
                    tempMatIDFaceDictionary[matID].MaterialIDFaceArray.Set(index, true);
                }
                else
                {
                    //We need to create a Parent node for each ID
                    var parentNode = new ParentNode(m_Global, node.Name, node.Handle) {IsPolyObject = false };
                    //new matID found, make empty bitArray and set the bit
                    //IBitArray tempBitArray = m_Global.BitArray.Create(numFaces);
                    BitArray tempBitArray = new BitArray(numFaces);
                    //Set bit (index)
                    tempBitArray.Set(index, true);
                    //Assign BitArray to parentNode
                    parentNode.MaterialIDFaceArray = tempBitArray;
                    //Add to dictionary
                    tempMatIDFaceDictionary.Add(matID, parentNode);
                }
            }
            //parentNode.MaterialIDFaceDictionary should now be all filled up
            //Now we need to fill up the vertexIndex List

            //Build Vertex Hashset
            var numIdUsed = tempMatIDFaceDictionary.Keys.ToArray();

            IMatrix3 m3World = node.GetObjTMAfterWSM(0, null);
            IMatrix3 m3Local = m3World;

            if (!node.IsRootNode)
            {
                IMatrix3 m3Parent = node.ParentNode.GetObjTMAfterWSM(0, null);
                m3Local = m3World.Multiply(m_Global.Inverse(m3Parent));
            }

            foreach (var id in numIdUsed)
            {
                //Get the bitArray for each ID
                BitArray facesPerID = tempMatIDFaceDictionary[id].MaterialIDFaceArray;
                
                float? xMaxValue = null, xMinValue = null;
                float? yMaxValue = null, yMinValue = null;
                float? zMaxValue = null, zMinValue = null;
                
                IPoint3 maxValues, minValues;

                //loop through the bitarray to see which faces are set. For the set faces, save the verts
                for (int i = 0; i < facesPerID.Count; i++)
                {
                    if (facesPerID[i]) //if the face was set
                    {
                        //This should be a singular face that has the defined ID
                        IFace face = mesh.Faces[i];

                        IntPtr vertsIndices = face.AllVerts;

                        uint* dwordVertIndices = (uint*)vertsIndices.ToPointer();

                        for (int j = 0; j < 3; j++)
                        {
                            var vertexIndex = (int)dwordVertIndices[j];

                            tempMatIDFaceDictionary[id].MaterialIDVertexList.Add(vertexIndex);

                            var pos = mesh.GetVert(vertexIndex);
                            pos = m3Local.PointTransform(pos);

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
                    }
                }

                //if (xMaxValue.HasValue && yMaxValue.HasValue && zMaxValue.HasValue)
                maxValues = m_Global.Point3.Create(xMaxValue.Value, yMaxValue.Value, zMaxValue.Value);
                DebugMethods.Log(string.Format("Max world values X: {0}, Y: {1}, Z: {2}", maxValues.X, maxValues.Y, maxValues.Z));
                //if (xMinValue.HasValue && yMinValue.HasValue && zMinValue.HasValue)
                minValues = m_Global.Point3.Create(xMinValue.Value, yMinValue.Value, zMinValue.Value);
                DebugMethods.Log(string.Format("Max world values X: {0}, Y: {1}, Z: {2}", minValues.X, minValues.Y, minValues.Z));

                BoundingBox bb = new BoundingBox(maxValues, minValues);

                tempMatIDFaceDictionary[id].BoundingBox = bb;
            }

            return tempMatIDFaceDictionary;
        }

        public unsafe static Dictionary<ushort, ChildNode> BuildChildNodeDictionary(string name, uint handle, IMesh mesh)
        {
            var numFaces = mesh.NumFaces;
            var tempMatIDFaceDictionary = new Dictionary<ushort, ChildNode>();

            //Build FaceID Dictionary.
            //Fill up temp Dictionary for each
            for (int index = 0; index < numFaces; index++)
            {
                IFace face = mesh.Faces[index];
                ushort matID = face.MatID;

                if (tempMatIDFaceDictionary.ContainsKey(matID))
                {
                    //Same material ID, just set the bit
                    tempMatIDFaceDictionary[matID].MaterialIDFaceArray.Set(index, true);
                }
                else
                {
                    //We need to create a Parent node for each ID
                    var parentNode = new ChildNode(m_Global, name, handle) {IsPolyObject = false};
                    //new matID found, make empty bitArray and set the bit
                    BitArray tempBitArray = new BitArray(numFaces);
                    //Set bit (index)
                    tempBitArray.Set(index, true);
                    //Assign BitArray to parentNode
                    parentNode.MaterialIDFaceArray = tempBitArray;
                    //Add to dictionary
                    tempMatIDFaceDictionary.Add(matID, parentNode);
                }
            }
            //parentNode.MaterialIDFaceDictionary should now be all filled up
            //Now we need to fill up the vertexIndex List

            //Build Vertex Hashset
            var numIdUsed = tempMatIDFaceDictionary.Keys.ToArray();

            foreach (var id in numIdUsed)
            {
                //Get the bitArray for each ID
                BitArray facesPerID = tempMatIDFaceDictionary[id].MaterialIDFaceArray;

                //Create emty hashset
                HashSet<int> tempHashSet = new HashSet<int>();

                //loop through the bitarray to see which faces are set. For the set faces, save the verts
                for (int i = 0; i < facesPerID.Count; i++)
                {
                    if (facesPerID[i] == true)
                    {
                        //This should be a singular face that has the defined ID
                        IFace face = mesh.Faces[i];

                        IntPtr vertsIndices = face.AllVerts;
                        //vertsIndices.ToPointer()

                        uint* dwordVertIndices = (uint*)vertsIndices.ToPointer();

                        for (int j = 0; j < 3; j++)
                        {
                            var vertexIndex = (int)dwordVertIndices[j];

                            tempMatIDFaceDictionary[id].MaterialIDVertexList.Add(vertexIndex);
                        }
                    }
                }
            }

            return tempMatIDFaceDictionary;
        }

        private void BuildBoundingBox()
        {
            
        }


        //public unsafe static void BuildVertexArray(IMesh mesh, MasterNode masterNode)
        //{
        //    var IDArray = masterNode.MaterialIDFaceDictionary.Keys.ToArray();

        //    //Go through each ID 
        //    foreach (var id in IDArray)
        //    {
        //        //Get the bitArray for each ID
        //        IBitArray facesPerID = Controller.GetFacesBitArrayByID(id, masterNode);

        //        //Create emty hashset
        //        HashSet<int> tempHashSet = new HashSet<int>();
        //        masterNode.MaterialIDVertexDictionary.Add(id, tempHashSet);

        //        //loop through the bitarray to see which faces are set. For the set faces, save the verts
        //        for (int i = 0; i < facesPerID.Size; i++)
        //        {
        //            if (facesPerID[i] == 1)
        //            {
        //                //This should be a singular face that has the defined ID
        //                IFace face = mesh.Faces[i];

        //                IntPtr vertsIndices = face.AllVerts;
        //                //vertsIndices.ToPointer()

        //                uint* dwordVertIndices = (uint*)vertsIndices.ToPointer();

        //                for (int j = 0; j < 3; j++)
        //                {
        //                    var vertexIndex = (int)dwordVertIndices[j];

        //                    masterNode.MaterialIDVertexDictionary[id].Add(vertexIndex);

        //                    var vertexPos = mesh.Verts[vertexIndex];
        //                }
        //            }
        //        }
        //    }
        //}

        ///// <summary>
        ///// Builds the MatID dictionary for Editable Mesh
        ///// </summary>
        //public static void BuildFaceMatIDDictionary(IMesh mesh, ref MasterNode masterNode)
        //{
        //    var numFaces = mesh.NumFaces;

        //    for (int index = 0; index < numFaces; index++)
        //    {
        //        IFace face = mesh.Faces[index];
        //        ushort matID = face.MatID;

        //        if (masterNode.MaterialIDFaceDictionary.ContainsKey(matID))
        //        {
        //            //Same material ID, just set the bit
        //            masterNode.MaterialIDFaceDictionary[matID].Set(index);
        //        }
        //        else
        //        {
        //            //new matID found, make empty bitArray and set the bit
        //            IBitArray tempBitArray = m_Global.BitArray.Create(numFaces);
        //            tempBitArray.ClearAll(); // dunno if needed :D
        //            //Set bit (index)
        //            tempBitArray.Set(index);
        //            //Add to dictionary
        //            masterNode.MaterialIDFaceDictionary.Add(matID, tempBitArray);
        //        }
        //    }
        //}
    }
}
