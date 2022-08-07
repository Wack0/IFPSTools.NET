using IFPSLib.Emit;
using IFPSLib.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IFPSLib
{
    [Flags]
    public enum FunctionFlags : byte
    {
        External = (1 << 0),
        ExternalWithDeclaration = External | (1 << 1),
        Exported = (1 << 1),
        HasAttributes = (1 << 2)
    }
    /// <summary>
    /// Represents a function.
    /// </summary>
    public interface IFunction
    {
        IList<CustomAttribute> Attributes { get; }

        string Name { get; set; }

        /// <summary>
        /// If null, function returns void.
        /// </summary>
        IType ReturnArgument { get; set; }

        IList<FunctionArgument> Arguments { get; set; }
        bool Exported { get; set; }

        int Size { get; }
    }

    /// <summary>
    /// Base implementation of a function.
    /// </summary>
    public abstract class FunctionBase : IFunction
    {
        public IList<CustomAttribute> Attributes { get; internal set; } = new List<CustomAttribute>();
        public string Name { get; set; }

        public bool Exported { get; set; }

        public abstract IType ReturnArgument { get; set; }

        /// <summary>
        /// If null, argument information is unknown.
        /// </summary>
        public abstract IList<FunctionArgument> Arguments { get; set; }

        public abstract int Size { get; }

        internal static FunctionBase Load(BinaryReader br, Script script)
        {
            var flags = (FunctionFlags)br.ReadByte();

            FunctionBase ret = null;

            var exported = (flags & FunctionFlags.Exported) != 0;

            if ((flags & FunctionFlags.External) != 0)
            {
                ret = ExternalFunction.Load(br, script, exported);
            } else
            {
                ret = ScriptFunction.Load(br, script, exported);
            }
            if ((flags & FunctionFlags.HasAttributes) != 0)
                ret.Attributes = CustomAttribute.LoadList(br, script);
            return ret;
        }

        internal abstract void Save(BinaryWriter bw, Script.SaveContext ctx);
    }
}
