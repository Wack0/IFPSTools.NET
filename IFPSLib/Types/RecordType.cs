using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IFPSLib.Types
{
    /// <summary>
    /// Defines a record.
    /// A record in PascalScript (and Pascal) is equivalent to a struct in C-like languages.
    /// In PascalScript, it's usually used for FFI.
    /// Elements are typed but have no name; in bytecode they are referenced by index using array-index operands.
    /// </summary>
    public class RecordType : TypeBase
    {
        public override PascalTypeCode BaseType => PascalTypeCode.Record;

        /// <summary>
        /// Types of the record's elements.
        /// </summary>
        public IList<IType> Elements { get; internal set; } = new List<IType>();

        private RecordType() { }

        public RecordType(IList<IType> elements)
        {
            Elements = elements;
        }

        internal static new RecordType Load(BinaryReader br, Script script)
        {
            var ret = new RecordType();
            var count = (int)br.Read<uint>();
            ret.Elements = new List<IType>(count);
            for (int i = 0; i < count; i++)
            {
                ret.Elements.Add(script.Types[(int)br.Read<uint>()]);
            }
            return ret;
        }

        internal override void SaveCore(BinaryWriter bw, Script.SaveContext ctx)
        {
            bw.Write<int>(Elements.Count);
            for (int i = 0; i < Elements.Count; i++)
            {
                bw.Write<int>(ctx.GetTypeIndex(Elements[i]));
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder(base.ToString() + "record(");
            for (int i = 0; i < Elements.Count; i++)
            {
                sb.Append(i == 0 ? "" : ",");
                sb.Append(Elements[i].Name);
            }
            sb.AppendFormat(") {0}", Name);
            return sb.ToString();
        }
    }
}
