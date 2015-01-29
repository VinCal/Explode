using System;
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
    }
}
