using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IFPSLib
{
    /// <summary>
    /// Represents additional type/function metadata, similar to .NET attributes.
    /// </summary>
    public class CustomAttribute
    {
        /// <summary>
        /// Attribute type name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Attribute arguments (indexed by constructor argument number).
        /// </summary>

        public IList<TypedData> Arguments { get; internal set; } = new List<TypedData>();

        public CustomAttribute(string name)
        {
            Name = name;
        }

        internal static IList<CustomAttribute> LoadList(BinaryReader br, Script script)
        {
            var listLen = br.Read<int>();
            var ret = new List<CustomAttribute>(listLen);
            for (int idxAttr = 0; idxAttr < listLen; idxAttr++)
            {
                var nameLen = br.Read<uint>();
                var attr = new CustomAttribute(br.ReadAsciiString(nameLen));
                var argsLen = br.Read<int>();
                attr.Arguments = new List<TypedData>(argsLen);
                for (int idxArg = 0; idxArg < argsLen; idxArg++)
                {
                    attr.Arguments.Add(TypedData.Load(br, script));
                }
                ret.Add(attr);
            }
            return ret;
        }

        internal static void SaveList(BinaryWriter bw, IList<CustomAttribute> list, Script.SaveContext ctx)
        {
            bw.Write<int>(list.Count);
            for (int idxAttr = 0; idxAttr < list.Count; idxAttr++)
            {
                var attr = list[idxAttr];
                bw.WriteAsciiString(attr.Name.ToUpper(), true);
                bw.Write<int>(attr.Arguments.Count);
                for (int idxArg = 0; idxArg < attr.Arguments.Count; idxArg++)
                {
                    attr.Arguments[idxArg].Save(bw, ctx);
                }
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder("[");
            sb.Append(Name);
            sb.Append('(');
            for (int i = 0; i < Arguments.Count; i++)
            {
                if (i != 0) sb.Append(", ");
                sb.Append(Arguments[i]);
            }
            sb.Append(")]");
            return sb.ToString();
        }
    }
}
