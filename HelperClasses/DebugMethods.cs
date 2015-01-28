﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Max;

namespace Test_ExplodeScript
{
    class DebugMethods
    {
        private static IGlobal m_Global;

        [Conditional("DEBUG")]
        public static void SetGlobal(IGlobal global)
        {
            m_Global = global;
        }

        [Conditional("DEBUG")]
        public static void Log(string message, bool newLine = true)
        {
            m_Global.TheListener.EditStream.Printf(message + (newLine ? Environment.NewLine : String.Empty), null);
        }

        internal static IValue MaxScriptExecute(string str)
        {
            bool res = false;
            return m_Global.ExecuteScript(m_Global.StringStream.Create(str), /*ref */res);
        }

        //[Conditional("DEBUG")]
        //private void DebugList()
        //{
        //    foreach (var masterNode in m_MasterNodeList)
        //    {
        //        var arrayOfAllIDs = masterNode.MaterialIDDictionary.Keys.ToArray();

        //        foreach (ushort id in arrayOfAllIDs)
        //        {
        //            IBitArray IDFacesBitArray = GetFacesByID(id, masterNode);

        //            int faceCount = 0;
        //            for (int j = 0; j < IDFacesBitArray.Size; j++)
        //            {
        //                //bit set
        //                if (IDFacesBitArray[j] == 1)
        //                    ++faceCount;
        //            }

        //            HelperMethods.Log(masterNode.Handle.ToString());
        //            HelperMethods.Log(String.Format("The amount of faces for ID: {0} is: {1}", id, faceCount));
        //            HelperMethods.Log("------------------");
        //        }
        //    }
        //}
    }
}
