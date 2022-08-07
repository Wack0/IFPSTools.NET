using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IFPSLib.Types
{

    public interface IType
    {
        PascalTypeCode BaseType { get; }
        string Name { get; set; }
        bool Exported { get; set; }
        IList<CustomAttribute> Attributes { get; }
    }

    public abstract class TypeBase : IType
    {
        public abstract PascalTypeCode BaseType { get; }
        public string Name { get; set; }

        public bool Exported { get; set; }
        public IList<CustomAttribute> Attributes { get; internal set; } = new List<CustomAttribute>();

        private const byte EXPORTED_FLAG = 0x80;

        internal static TypeBase Load(BinaryReader br, Script script)
        {
            byte baseType = br.ReadByte();

            bool isExported = (baseType & EXPORTED_FLAG) != 0;
            var typeCode = (PascalTypeCode) (baseType & ~EXPORTED_FLAG);

            var ret = LoadCore(br, script, typeCode);

            if (isExported)
            {
                var length = br.Read<uint>();
                ret.Name = br.ReadAsciiString(length);
                ret.Exported = true;
            }

            if (script.FileVersion >= Script.VERSION_MIN_ATTRIBUTES)
            {
                ret.Attributes = CustomAttribute.LoadList(br, script);
            }

            return ret;
        }

        private static TypeBase LoadCore(BinaryReader br, Script script, PascalTypeCode typeCode)
        {
            if (typeCode.IsPrimitive()) return new PrimitiveType(typeCode);

            switch (typeCode)
            {
                case PascalTypeCode.Class:
                    return ClassType.Load(br);
                case PascalTypeCode.ProcPtr:
                    return FunctionPointerType.Load(br);
                case PascalTypeCode.Interface:
                    return new ComInterfaceType(br.Read<Guid>());
                case PascalTypeCode.Set:
                    return new SetType((int)br.Read<uint>());
                case PascalTypeCode.StaticArray:
                    return StaticArrayType.Load(br, script);
                case PascalTypeCode.Array:
                    return ArrayType.Load(br, script);
                case PascalTypeCode.Record:
                    return RecordType.Load(br, script);
                default:
                    throw new InvalidDataException(string.Format("Invalid type: ", typeCode));
            }
        }

        internal void Save(BinaryWriter bw, Script.SaveContext ctx)
        {
            var code = (byte)BaseType;
            if (Exported) code |= EXPORTED_FLAG;
            bw.Write(code);
            SaveCore(bw, ctx);

            if (Exported)
            {
                bw.WriteAsciiString(Name.ToUpper(), true);
            }

            if (ctx.FileVersion >= Script.VERSION_MIN_ATTRIBUTES)
            {
                CustomAttribute.SaveList(bw, Attributes, ctx);
            }
        }

        internal virtual void SaveCore(BinaryWriter bw, Script.SaveContext ctx)
        {
            throw new InvalidOperationException();
        }

        public override string ToString()
        {
            return string.Format(".type{0} ", Exported ? "(export)" : "");
        }
    }
}
