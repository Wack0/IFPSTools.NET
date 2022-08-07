using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IFPSLib.Types
{
    public class ComInterfaceType : TypeBase
    {
        public override PascalTypeCode BaseType => PascalTypeCode.Interface;

        public Guid Guid;

        public ComInterfaceType(Guid guid)
        {
            Guid = guid;
        }

        internal override void SaveCore(BinaryWriter bw, Script.SaveContext ctx)
        {
            bw.Write(Guid);
        }
        public override string ToString()
        {
            return string.Format(base.ToString() + "interface(\"{0}\") {1}", Guid, Name);
        }
    }
}
