using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IFPSLib.Types
{
    public class ClassType : TypeBase
    {
        public override PascalTypeCode BaseType => PascalTypeCode.Class;

        public string InternalName { get; set; }

        private ClassType() { }

        public ClassType(string intName)
        {
            InternalName = intName;
        }

        internal static ClassType Load(BinaryReader br)
        {
            var length = br.Read<uint>();
            var type = new ClassType();
            type.InternalName = type.Name = br.ReadAsciiString(length);
            return type;
        }

        internal override void SaveCore(BinaryWriter bw, Script.SaveContext ctx)
        {
            bw.WriteAsciiString(InternalName.ToUpper(), true);
        }

        public override string ToString()
        {
            return string.Format(base.ToString() + "class({0}) {1}", InternalName, Name);
        }
    }
}
