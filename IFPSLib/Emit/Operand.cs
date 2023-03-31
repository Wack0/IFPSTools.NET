using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace IFPSLib.Emit
{
    /// <summary>
    /// A PascalScript bytecode instruction operand.
    /// </summary>
    public class Operand
    {
        private struct IndexedVariableValue
        {
            internal IVariable Variable;
            internal IndexedValue Index;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct IndexedValue
        {
            [FieldOffset(0)]
            internal StrongBox<uint> m_Immediate;
            [FieldOffset(0)]
            internal IVariable Variable;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct Value
        {
            [FieldOffset(0)]
            internal TypedData Immediate;
            [FieldOffset(0)]
            internal IVariable Variable;
            [FieldOffset(0)]
            internal IndexedVariableValue Indexed;
        }

        internal BytecodeOperandType m_Type;
        private Value m_Value;

        public BytecodeOperandType Type => m_Type;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T EnsureType<T>(ref T value, BytecodeOperandType type)
        {
            if (m_Type != type) throw new InvalidOperationException();
            return value;
        }

        private IndexedVariableValue Indexed
        {
            get
            {
                if (m_Type != BytecodeOperandType.IndexedImmediate && m_Type != BytecodeOperandType.IndexedVariable) throw new InvalidOperationException();
                return m_Value.Indexed;
            }
        }

        public TypedData ImmediateTyped => EnsureType(ref m_Value.Immediate, BytecodeOperandType.Immediate);
        public object Immediate => ImmediateTyped.Value;

        public TType ImmediateAs<TType>() => ImmediateTyped.ValueAs<TType>();
        public IVariable Variable => EnsureType(ref m_Value.Variable, BytecodeOperandType.Variable);

        public IVariable IndexedVariable => Indexed.Variable;
        public uint IndexImmediate => EnsureType(ref m_Value.Indexed.Index.m_Immediate.Value, BytecodeOperandType.IndexedImmediate);
        public IVariable IndexVariable => EnsureType(ref m_Value.Indexed.Index.Variable, BytecodeOperandType.IndexedVariable);

        public Operand(TypedData imm)
        {
            m_Type = BytecodeOperandType.Immediate;
            m_Value.Immediate = imm;
        }

        public Operand(IVariable var)
        {
            if (var == null) throw new ArgumentNullException(nameof(var));
            m_Type = BytecodeOperandType.Variable;
            m_Value.Variable = var;
        }

        public Operand(IVariable arr, uint immIdx)
        {
            if (arr == null) throw new ArgumentNullException(nameof(arr));
            m_Type = BytecodeOperandType.IndexedImmediate;
            m_Value.Indexed.Variable = arr;
            m_Value.Indexed.Index.m_Immediate = new StrongBox<uint>(immIdx);
        }

        public Operand(IVariable arr, IVariable varIdx)
        {
            if (arr == null) throw new ArgumentNullException(nameof(arr));
            if (varIdx == null) throw new ArgumentNullException(nameof(varIdx));
            m_Type = BytecodeOperandType.IndexedVariable;
            m_Value.Indexed.Variable = arr;
            m_Value.Indexed.Index.Variable = varIdx;
        }

        public Operand(Operand op)
        {
            m_Type = op.m_Type;
            m_Value = op.m_Value;
        }

        public static Operand Create<TType>(Types.PrimitiveType type, TType value)
        {
            return new Operand(TypedData.Create(type, value));
        }

        public static Operand Create<TType>(TType value)
        {
            // this is supposed to be for only primitives, but derived interfaces can also lead here.
            // check for those derived interfaces.
            switch (value)
            {
                case Types.IType type:
                    return Create(type);
                case IFunction fn:
                    return Create(fn);
                case IVariable var:
                    return Create(var);
            }
            return new Operand(TypedData.Create(value));
        }

        public static Operand Create(Types.IType value)
        {
            return new Operand(TypedData.Create(value));
        }

        public static Operand Create(Instruction value)
        {
            return new Operand(TypedData.Create(value));
        }

        public static Operand Create(IFunction value)
        {
            return new Operand(TypedData.Create(value));
        }

        public static Operand Create(IVariable arr, uint idxValue)
        {
            return new Operand(arr, idxValue);
        }

        public static Operand Create(IVariable var)
        {
            return new Operand(var);
        }

        public static Operand Create(IVariable arr, IVariable varIdx)
        {
            return new Operand(arr, varIdx);
        }

        internal static Operand LoadValue(BinaryReader br, Script script, ScriptFunction function)
        {
            var type = (BytecodeOperandType)br.ReadByte();

            switch (type)
            {
                case BytecodeOperandType.Variable:
                    return new Operand(VariableBase.Load(br, script, function));
                case BytecodeOperandType.Immediate:
                    return new Operand(TypedData.Load(br, script));
                case BytecodeOperandType.IndexedImmediate:
                    {
                        var variable = VariableBase.Load(br, script, function);
                        var idx = br.Read<uint>();
                        return Create(variable, idx);
                    }
                case BytecodeOperandType.IndexedVariable:
                    {
                        var variable = VariableBase.Load(br, script, function);
                        var idx = VariableBase.Load(br, script, function);
                        return new Operand(variable, idx);
                    }
                default:
                    throw new ArgumentOutOfRangeException(string.Format("Invalid operand type {0}", (byte)type));
            }
        }

        public override string ToString()
        {
            switch (m_Type)
            {
                case BytecodeOperandType.Variable:
                    return Variable.Name;
                case BytecodeOperandType.Immediate:
                    return ImmediateTyped.ToString();
                case BytecodeOperandType.IndexedImmediate:
                    return String.Format("{0}[{1}]", IndexedVariable.Name, IndexImmediate);
                case BytecodeOperandType.IndexedVariable:
                    return String.Format("{0}[{1}]", IndexedVariable.Name, IndexVariable.Name);
                default:
                    return "";
            }
        }

        public int Size
        {
            get
            {
                const int HEADER = sizeof(BytecodeOperandType);
                switch (m_Type)
                {
                    case BytecodeOperandType.Variable:
                        return Variable.Size + HEADER;
                    case BytecodeOperandType.Immediate:
                        switch (ImmediateTyped.Type.BaseType)
                        {
                            case Types.PascalTypeCode.Type:
                            case Types.PascalTypeCode.Instruction:
                            case Types.PascalTypeCode.Function:
                                return ImmediateTyped.Size;
                            default:
                                return ImmediateTyped.Size + HEADER;
                        }
                    case BytecodeOperandType.IndexedImmediate:
                        return IndexedVariable.Size + sizeof(uint) + HEADER;
                    case BytecodeOperandType.IndexedVariable:
                        return IndexedVariable.Size + IndexVariable.Size + HEADER;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        internal void Save(BinaryWriter bw, Script.SaveContext ctx)
        {
            bw.Write(m_Type);
            switch (m_Type)
            {
                case BytecodeOperandType.Variable:
                    ((VariableBase)Variable).Save(bw, ctx);
                    break;
                case BytecodeOperandType.Immediate:
                    var baseType = ImmediateTyped.Type.BaseType;
                    if (baseType == Types.PascalTypeCode.Type || baseType == Types.PascalTypeCode.Instruction || baseType == Types.PascalTypeCode.Function)
                        throw new InvalidOperationException(string.Format("Immediate operand is of incorrect type {0}", baseType));
                    ImmediateTyped.Save(bw, ctx);
                    break;
                case BytecodeOperandType.IndexedImmediate:
                    ((VariableBase)IndexedVariable).Save(bw, ctx);
                    bw.Write<uint>(IndexImmediate);
                    break;
                case BytecodeOperandType.IndexedVariable:
                    ((VariableBase)IndexedVariable).Save(bw, ctx);
                    ((VariableBase)IndexVariable).Save(bw, ctx);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Returns true if this operand is compatible with another, such that this operand can be overwritten in an instruction with the other.
        /// </summary>
        /// <param name="value">Other operand</param>
        /// <returns>True if overwriting is fine, false if not.</returns>
        internal bool SimilarType(Operand value)
        {
            // Internal operand type must match.
            if (m_Type != value.m_Type) return false;
            // If immediate, and a psuedo-type, it must match.
            if (m_Type == BytecodeOperandType.Immediate)
            {
                var baseType = ImmediateTyped.Type.BaseType;
                if (baseType == Types.PascalTypeCode.Type || baseType == Types.PascalTypeCode.Instruction || baseType == Types.PascalTypeCode.Function)
                {
                    return baseType == value.ImmediateTyped.Type.BaseType;
                }
                return baseType != Types.PascalTypeCode.Unknown;
            }
            return true;
        }
    }
}
