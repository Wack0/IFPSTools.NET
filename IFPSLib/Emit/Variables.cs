using IFPSLib.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IFPSLib.Emit
{
    /// <summary>
    /// Represents a variable that can be represented in an operand.
    /// </summary>
    public interface IVariable
    {
        IType Type { get; }
        string Name { get; set; }
        VariableType VarType { get; }
        int Index { get; }
        
        int Size { get; }
    }

    public abstract class VariableBase : IVariable
    {
        public IType Type { get; internal set; }
        public string Name { get; set; }

        public abstract VariableType VarType { get; }
        public int Index { get; internal set; }

        public int Size => sizeof(uint);

        internal const int MAX_GLOBALS = 0x40000000;
        internal const int MAX_ARGS = 0x20000000;
        internal static VariableBase Load(BinaryReader reader, Script script, ScriptFunction function)
        {
            var idx = reader.Read<uint>();
            if (idx < MAX_GLOBALS) return script.GlobalVariables[(int)idx];
            int sIdx = ((int)idx - MAX_GLOBALS - MAX_ARGS);
            if (sIdx >= 0) return new LocalVariable(sIdx);
            return new ArgumentVariable((-sIdx) - 1, function.ReturnArgument == null);
        }

        internal abstract void Save(BinaryWriter bw, Script.SaveContext ctx);
    }


    public sealed class LocalVariable : VariableBase
    {
        public override VariableType VarType => VariableType.Local;

        internal override void Save(BinaryWriter bw, Script.SaveContext ctx)
        {
            bw.Write<uint>((uint)(MAX_GLOBALS + MAX_ARGS + Index));
        }

        public static LocalVariable Create(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            return new LocalVariable(index + 1);
        }

        internal LocalVariable(int index)
        {
            Index = index;
            Name = string.Format("Var{0}", index);
        }
    }

    public sealed class ArgumentVariable : VariableBase
    {
        public override VariableType VarType => VariableType.Argument;

        internal static ArgumentVariable Create(int index, bool isVoid)
        {
            if (index < 0 || index >= MAX_ARGS) throw new ArgumentOutOfRangeException(nameof(index));
            return new ArgumentVariable(index, isVoid);
        }

        internal override void Save(BinaryWriter bw, Script.SaveContext ctx)
        {
            bw.Write<uint>((uint)(MAX_GLOBALS + MAX_ARGS - Index - 1));
        }

        internal ArgumentVariable(int index, bool isVoid)
        {
            Index = index;
            if (!isVoid && Index == 0) Name = "RetVal";
            else Name = string.Format("Arg{0}", index + (isVoid ? 1 : 0));
        }
    }

    public sealed class GlobalVariable : VariableBase
    {
        public bool Exported { get; set; }
        public override VariableType VarType => VariableType.Global;

        internal GlobalVariable(int index)
        {
            Index = index;
            Name = string.Format("Global{0}", index);
        }

        public static GlobalVariable Create(int index)
        {
            if (index < 0 || index >= MAX_GLOBALS) throw new ArgumentOutOfRangeException(nameof(index));
            return new GlobalVariable(index);
        }

        public static GlobalVariable Create(int index, IType type)
        {
            var ret = Create(index);
            ret.Type = type;
            return ret;
        }

        public static GlobalVariable Create(int index, IType type, string name)
        {
            var ret = Create(index, type);
            ret.Name = name;
            return ret;
        }

        internal static GlobalVariable Load(BinaryReader br, Script script, int index)
        {
            var ret = new GlobalVariable(index);
            ret.Type = script.Types[(int)br.Read<uint>()];
            var flags = br.ReadByte();
            if ((flags & 1) != 0)
            {
                var len = br.Read<uint>();
                ret.Name = br.ReadAsciiString(len);
                ret.Exported = true;
            }
            return ret;
        }

        public override string ToString()
        {
            return string.Format(".global{0} {1} {2}", Exported ? "(import)" : "", Type.Name, Name);
        }

        internal override void Save(BinaryWriter bw, Script.SaveContext ctx)
        {
            bw.Write<uint>((uint)Index);
        }

        internal void SaveHeader(BinaryWriter bw, Script.SaveContext ctx)
        {
            // type
            bw.Write<int>(ctx.GetTypeIndex(Type));
            // flags
            bw.Write<bool>(Exported);
            if (Exported)
            {
                bw.WriteAsciiString(Name.ToUpper(), true);
            }
        }
    }
}
