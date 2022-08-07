using IFPSLib.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IFPSLib
{
    public enum FunctionArgumentType : byte
    {
        Out,
        In
    }
    public sealed class FunctionArgument
    {
        /// <summary>
        /// If null, type is unknown.
        /// </summary>
        public IType Type;

        public FunctionArgumentType ArgumentType;

        public string Name;

        internal static IList<FunctionArgument> LoadForExternal(BinaryReader br)
        {
            var count = (int)(br.BaseStream.Length - br.BaseStream.Position);
            var ret = new List<FunctionArgument>(count);

            for (int i = 0; i < count; i++)
            {
                ret.Add(new FunctionArgument()
                {
                    Type = null,
                    ArgumentType = br.ReadByte() != 0 ? FunctionArgumentType.Out : FunctionArgumentType.In
                });
            }
            return ret;
        }

        public override string ToString()
        {
            var noName = string.IsNullOrEmpty(Name);
            return string.Format("{0} {1}{2}{3}",
                ArgumentType == FunctionArgumentType.Out ? "__out" : "__in",
                Type == null ? "__unknown" : Type.Name,
                noName ? "" : " ",
                noName ? "" : Name
            );
        }
    }
}
