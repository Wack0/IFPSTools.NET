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
    /// Elements are typed but have no name internally; in bytecode they are referenced by index using array-index operands.
    /// For quality of life purposes, we allow names to be specified for elements, but they will not be retained when saving out bytecode.
    /// </summary>
    public class RecordType : TypeBase
    {
        public override PascalTypeCode BaseType => PascalTypeCode.Record;

        /// <summary>
        /// Types of the record's elements.
        /// </summary>
        public IList<IType> Elements { get; internal set; } = new List<IType>();

        /// <summary>
        /// Names of the record's elements.
        /// </summary>
        public IList<string> ElementNames { get; internal set; } = null;

        private RecordType() { }

        public RecordType(IList<IType> elements)
        {
            Elements = elements;
            ElementNames = new List<string>();
        }

        public RecordType(IList<IType> elements, IList<string> elementNames) : this(elements)
        {
            ElementNames = elementNames;
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

        private static readonly string[] INVALID_CHARS_FOR_NAME = { " ", ",", "(", ")" };

        public override string ToString()
        {
            var sb = new StringBuilder(base.ToString() + "record(");
            var nameCount = ElementNames == null ? 0 : ElementNames.Count;
            for (int i = 0; i < Elements.Count; i++)
            {
                sb.Append(i == 0 ? "" : ",");
                sb.Append(Elements[i].Name);
                if (i < nameCount)
                {
                    var name = new StringBuilder(ElementNames[i]);
                    foreach (var chr in INVALID_CHARS_FOR_NAME) name = name.Replace(chr, string.Empty);
                    if (name.Length != 0)
                    {
                        sb.Append(' ');
                        sb.Append(name);
                    }
                }
            }
            sb.AppendFormat(") {0}", Name);
            return sb.ToString();
        }
    }
}
