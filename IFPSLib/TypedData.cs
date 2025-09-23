using IFPSLib.Types;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using SharpFloat.FloatingPoint;
using System.Runtime.InteropServices;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Globalization;

namespace IFPSLib
{
    /// <summary>
    /// Represents an object with a PascalScript base type.
    /// </summary>
    public class TypedData
    {
        private static readonly CultureInfo s_Culture = new CultureInfo("en");
        public IType Type { get; }
        public object Value { get; }

        internal TypedData(IType type, object value)
        {
            Type = type;
            Value = value;
        }

        public static TypedData Create<TType>(PrimitiveType type, TType value)
        {
            if (!type.BaseType.EqualsType(typeof(TType))) throw new ArgumentOutOfRangeException(nameof(value));
            return new TypedData(type, value);
        }

        public static TypedData Create(FunctionPointerType type, IFunction value)
        {
            return new TypedData(type, value);
        }

        public static TypedData Create(SetType type, BitArray value)
        {
            return new TypedData(type, value);
        }

        public static TypedData Create<TType>(TType value)
            => Create(PrimitiveType.Create<TType>(), value);

        internal static TypedData Create(IType value)
        {
            return new TypedData(TypeType.Instance, value);
        }

        internal static TypedData Create(Emit.Instruction value)
        {
            return new TypedData(InstructionType.Instance, value);
        }

        internal static TypedData Create(IFunction value)
        {
            return new TypedData(ImmediateFunctionType.Instance, value);
        }

        internal static TypedData Create(BitArray bitarr)
        {
            return new TypedData(new SetType(bitarr.Length), bitarr);
        }

        public TType ValueAs<TType>()
        {
            return (TType)Value;
        }

        private static void TrimDecimalString(StringBuilder sb)
        {
            // find the "e" looking from the end
            int idx = sb.Length;
            for (int i = sb.Length - 1; i >= 0; i--)
            {
                if (sb[i] == 'e')
                {
                    idx = i - 1;
                    break;
                }
            }

            // remove while character is '0'
            int length = 0;
            while (idx >= 0 && sb[idx] == '0')
            {
                length++;
                idx--;
            }
            if (sb[idx] == '.') // need at least one zero
            {
                length--;
                idx++;
            }
            if (length == 0) return;
            sb.Remove(idx + 1, length);
        }

        internal static TypedData Load(BinaryReader br, Script script)
        {
            var idxType = br.Read<uint>();
            var type = script.Types[(int)idxType];

            switch (type.BaseType)
            {
                case PascalTypeCode.S8:
                    return new TypedData(type, br.Read<sbyte>());
                case PascalTypeCode.U8:
                    return new TypedData(type, br.Read<byte>());
                case PascalTypeCode.Char:
                    return new TypedData(type, (char)br.Read<byte>());

                case PascalTypeCode.S16:
                    return new TypedData(type, br.Read<short>());
                case PascalTypeCode.U16:
                    return new TypedData(type, br.Read<ushort>());
                case PascalTypeCode.WideChar:
                    return new TypedData(type, br.Read<char>());

                case PascalTypeCode.S32:
                    return new TypedData(type, br.Read<int>());
                case PascalTypeCode.U32:
                    return new TypedData(type, br.Read<uint>());

                case PascalTypeCode.S64:
                    return new TypedData(type, br.Read<long>());

                case PascalTypeCode.Single:
                    return new TypedData(type, br.Read<float>());
                case PascalTypeCode.Double:
                    return new TypedData(type, br.Read<double>());
                case PascalTypeCode.Extended:
                    {
                        // BUGBUG: there must be something better than this... but for now, it'll do
                        var sb = new StringBuilder();
                        ExtF80.PrintFloat80(sb, br.Read<ExtF80>(), PrintFloatFormat.ScientificFormat, 19);
                        TrimDecimalString(sb);
                        return new TypedData(type, decimal.Parse(sb.ToString(), NumberStyles.Float, s_Culture));
                    }

                case PascalTypeCode.Currency:
                    return new TypedData(type, new CurrencyWrapper(decimal.FromOACurrency(br.Read<long>())));

                case PascalTypeCode.PChar:
                case PascalTypeCode.String:
                    var asciilen = br.Read<uint>();
                    return new TypedData(type, br.ReadAsciiString(asciilen));

                case PascalTypeCode.WideString:
                case PascalTypeCode.UnicodeString:
                    var unicodelen = br.Read<uint>();
                    return new TypedData(type, br.ReadUnicodeString(unicodelen));

                case PascalTypeCode.ProcPtr:
                    var funcIdx = br.Read<uint>();
                    return new TypedData(type, script.Functions[(int)funcIdx]);

                case PascalTypeCode.Set:
                    return new TypedData(type, ((SetType)type).Load(br));

                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        internal void WriteValue<T>(BinaryWriter bw) where T : unmanaged
        {
            bw.Write(ValueAs<T>());
        }

        internal void Save(BinaryWriter bw, Script.SaveContext ctx)
        {
            bw.Write<int>(ctx.GetTypeIndex(Type));
            switch (Type.BaseType)
            {
                case PascalTypeCode.S8:
                    WriteValue<sbyte>(bw);
                    break;
                case PascalTypeCode.U8:
                    WriteValue<byte>(bw);
                    break;
                case PascalTypeCode.Char:
                    bw.Write<byte>((byte)ValueAs<char>());
                    break;
                case PascalTypeCode.S16:
                    WriteValue<short>(bw);
                    break;
                case PascalTypeCode.U16:
                    WriteValue<ushort>(bw);
                    break;
                case PascalTypeCode.WideChar:
                    WriteValue<char>(bw);
                    break;

                case PascalTypeCode.S32:
                    WriteValue<int>(bw);
                    break;
                case PascalTypeCode.U32:
                    WriteValue<uint>(bw);
                    break;

                case PascalTypeCode.S64:
                    WriteValue<long>(bw);
                    break;

                case PascalTypeCode.Single:
                    WriteValue<float>(bw);
                    break;
                case PascalTypeCode.Double:
                    WriteValue<double>(bw);
                    break;
                case PascalTypeCode.Extended:
                    if (!ExtF80.TryParse(ValueAs<decimal>().ToString(s_Culture), out var extf))
                        throw new ArgumentOutOfRangeException("Value {0} cannot fit into an 80-bit floating point number");
                    bw.Write(extf);
                    break;

                case PascalTypeCode.Currency:
                    bw.Write<long>(decimal.ToOACurrency(ValueAs<CurrencyWrapper>().WrappedObject));
                    break;

                case PascalTypeCode.PChar:
                case PascalTypeCode.String:
                    bw.WriteAsciiString(ValueAs<string>(), true);
                    break;

                case PascalTypeCode.WideString:
                case PascalTypeCode.UnicodeString:
                    bw.Write<int>(Encoding.Unicode.GetByteCount(ValueAs<string>()) / sizeof(short));
                    bw.WriteUnicodeString(ValueAs<string>());
                    break;

                case PascalTypeCode.ProcPtr:
                    bw.Write<int>(ctx.GetFunctionIndex(ValueAs<IFunction>()));
                    break;

                case PascalTypeCode.Set:
                    ((SetType)Type).Save(bw, ValueAs<BitArray>());
                    break;

                default:
                    throw new InvalidOperationException();
            }
        }

        public int Size
        {
            get
            {
                const int HEADER = sizeof(uint); // type index

                switch (Type.BaseType)
                {
                    case PascalTypeCode.S8:
                        return sizeof(sbyte) + HEADER;
                    case PascalTypeCode.U8:
                    case PascalTypeCode.Char:
                        return sizeof(byte) + HEADER;

                    case PascalTypeCode.S16:
                        return sizeof(short) + HEADER;
                    case PascalTypeCode.U16:
                        return sizeof(ushort) + HEADER;
                    case PascalTypeCode.WideChar:
                        return sizeof(char) + HEADER;

                    case PascalTypeCode.S32:
                        return sizeof(int) + HEADER;
                    case PascalTypeCode.U32:
                        return sizeof(uint) + HEADER;

                    case PascalTypeCode.S64:
                        return sizeof(long) + HEADER;

                    case PascalTypeCode.Single:
                        return sizeof(float) + HEADER;
                    case PascalTypeCode.Double:
                        return sizeof(double) + HEADER;
                    case PascalTypeCode.Extended:
                        return Unsafe.SizeOf<ExtF80>() + HEADER;

                    case PascalTypeCode.Currency:
                        return sizeof(long) + HEADER;

                    case PascalTypeCode.PChar:
                    case PascalTypeCode.String:
                        return sizeof(uint) + Encoding.ASCII.GetByteCount(ValueAs<string>()) + HEADER;

                    case PascalTypeCode.WideString:
                    case PascalTypeCode.UnicodeString:
                        return sizeof(uint) + Encoding.Unicode.GetByteCount(ValueAs<string>()) + HEADER;

                    case PascalTypeCode.ProcPtr:
                        return sizeof(uint) + HEADER;

                    case PascalTypeCode.Set:
                        return (Type as SetType).ByteSize + HEADER;

                    case PascalTypeCode.Type:
                        return HEADER; // 32-bit type index only

                    case PascalTypeCode.Instruction:
                        return HEADER; // 32-bit offset

                    case PascalTypeCode.Function:
                        return HEADER; // 32-bit function index only

                    default:
                        throw new InvalidOperationException(string.Format("Data of type {0} cannot be serialised", Type.BaseType));
                }
            }
        }

        public override string ToString()
        {
            switch (Type.BaseType)
            {
                case PascalTypeCode.Type:
                    return ValueAs<IType>().Name;
                case PascalTypeCode.Instruction:
                    if (Value == null) return "null";
                    return string.Format("loc_{0}", ValueAs<Emit.Instruction>().Offset.ToString("x"));
                case PascalTypeCode.ProcPtr:
                    return string.Format("{0}({1})", Type.Name, ValueAs<IFunction>().Name);
                case PascalTypeCode.Function:
                    return ValueAs<IFunction>().Name;
                default:
                    if (Value is string)
                    {
                        return string.Format("{0}({1})", Type.Name, ValueAs<string>().ToLiteral());
                    }
                    if (Value is char)
                    {
                        return string.Format("{0}({1})", Type.Name, new string(ValueAs<char>(), 1).ToLiteral());
                    }
                    if (Value is BitArray)
                    {
                        var val = ValueAs<BitArray>();
                        var sb = new StringBuilder(Type.Name);
                        sb.Append("(0b");
                        for (int i = val.Count - 1; i >= 0; i--)
                        {
                            sb.Append(val[i] ? '1' : '0');
                        }
                        sb.Append(')');
                        return sb.ToString();
                    }
                    return string.Format(s_Culture, "{0}({1})", Type.Name, Value);
            }
        }
    }
}
