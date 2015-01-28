using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Max;
using Test_ExplodeScript;

namespace ExplodeScript
{
    class PolyMethods : ObjectMethods
    {
        /// <summary>
        /// Builds the MatID dictionary for EditablePoly
        /// </summary>
        public static Dictionary<ushort, ParentNode> BuildParentNodeDictionary(IINode node, IMNMesh mesh)
        {
            var numFaces = mesh.FNum;
            var tempMatIDFaceDictionary = new Dictionary<ushort, ParentNode>();

            //Build FaceID Dictionary.
            //Fill up temp Dictionary for each
            for (int index = 0; index < numFaces; index++)
            {
                IMNFace face = mesh.F(index);
                ushort matID = face.Material;

                if (tempMatIDFaceDictionary.ContainsKey(matID))
                {
                    //Same material ID, just set the bit
                    tempMatIDFaceDictionary[matID].MaterialIDFaceArray.Set(index, true);
                }
                else
                {
                    //We need to create a Parent node for each ID
                    var parentNode = new ParentNode(m_Global, node.Name, node.Handle) {IsPolyObject = true };
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

                //Create emty hashset
                HashSet<int> tempHashSet = new HashSet<int>();


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
                            tempMatIDFaceDictionary[id].MaterialIDVertexList.Add(vertexIndex);

                            var pos = mesh.P(vertexIndex);
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

                maxValues = m_Global.Point3.Create(xMaxValue.Value, yMaxValue.Value, zMaxValue.Value);
                DebugMethods.Log(string.Format("Max world values X: {0}, Y: {1}, Z: {2}", maxValues.X, maxValues.Y, maxValues.Z));

                minValues = m_Global.Point3.Create(xMinValue.Value, yMinValue.Value, zMinValue.Value);
                DebugMethods.Log(string.Format("Max world values X: {0}, Y: {1}, Z: {2}", minValues.X, minValues.Y, minValues.Z));

                BoundingBox bb = new BoundingBox(maxValues, minValues);

                tempMatIDFaceDictionary[id].BoundingBox = bb;
            }

            return tempMatIDFaceDictionary;
        }


        public static Dictionary<ushort, ChildNode> BuildChildNodeDictionary(string name, uint handle, IMNMesh mesh)
        {
            var numFaces = mesh.FNum;
            var tempMatIDFaceDictionary = new Dictionary<ushort, ChildNode>();

            //Build FaceID Dictionary.
            //Fill up temp Dictionary for each
            for (int index = 0; index < numFaces; index++)
            {
                IMNFace face = mesh.F(index);
                ushort matID = face.Material;

                if (tempMatIDFaceDictionary.ContainsKey(matID))
                {
                    //Same material ID, just set the bit
                    tempMatIDFaceDictionary[matID].MaterialIDFaceArray.Set(index, true);
                }
                else
                {
                    //We need to create a Parent node for each ID
                    var parentNode = new ChildNode(m_Global, name, handle) {IsPolyObject = true};
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
                            tempMatIDFaceDictionary[id].MaterialIDVertexList.Add(vertexIndex);
                        }
                    }
                }
            }

            return tempMatIDFaceDictionary;
        }

        /// <summary>
        /// Builds the MatID dictionary for EditablePoly
        /// </summary>
        //public static void BuildFaceMatIDDictionary(IMNMesh mesh, ref MasterNode masterNode)
        //{
        //    var numFaces = mesh.FNum;

        //    for (int index = 0; index < numFaces; index++)
        //    {
        //        IMNFace face = mesh.F(index);
        //        ushort matID = face.Material;

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

        //public static void BuildVertexArray(IMNMesh mesh, MasterNode masterNode)
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
        //                IMNFace face = mesh.F(i);
        //                IList<int> vertIndices = face.Vtx;

        //                for (int j = 0; j < vertIndices.Count; j++)
        //                {
        //                    var vertexIndex = vertIndices[j];

        //                    masterNode.MaterialIDVertexDictionary[id].Add(vertexIndex);

        //                    var vertexPos = mesh.V(j).P;

        //                    //m_VertList.Add(vertexPos);
        //                }
        //            }
        //        }
        //    }
        //}
    }
}
