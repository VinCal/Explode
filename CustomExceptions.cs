using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test_ExplodeScript;

namespace ExplodeScript
{
    class SetMaterialBitArrayException : Exception
    {
        public SetMaterialBitArrayException(ushort matID)
            : base(String.Format("Unable to set BitArray for matID: {0} because the matID already exists.", matID)) { }
    }

    class UpdateMaterialBitArrayException : Exception
    {
        public UpdateMaterialBitArrayException(ParentNode parentNode)
            : base(String.Format("The node with handle: {0} and name: {1} isn't part of the m_ElementNodeDictionary so why are we trying to update it?"
            , parentNode.Handle, parentNode.Name)) { }
    }

    class CreateMatIDBitArrayException : Exception
    {
        public CreateMatIDBitArrayException(ushort matID) 
            : base(String.Format("Unable to create BitArray for {0} because it already exists", matID)) { }
    }

    class SetMaterialBitException : Exception
    {
        public SetMaterialBitException(ushort matID, int index) 
            : base(String.Format("Unable to set {0}th bit of the BitArray for matID: {1} because the BitArray does not exist for this matID.", index, matID)) { }
    }
}