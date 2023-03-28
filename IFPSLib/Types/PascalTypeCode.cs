using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace IFPSLib.Types
{
    /// <summary>
    /// A PascalScript primitive type at the bytecode level.
    /// </summary>
    public enum PascalTypeCode : byte
    {
        ReturnAddress,
        U8,
        S8,
        U16,
        S16,
        U32,
        S32,
        Single,
        Double,
        Extended,
        String,
        Record,
        Array,
        Pointer,
        PChar,
        ResourcePointer,
        Variant,
        S64,
        Char,
        WideString,
        WideChar,
        ProcPtr,
        StaticArray,
        Set,
        Currency,
        Class,
        Interface,
        NotificationVariant,
        UnicodeString,

        PsuedoTypeStart = 0x80,
        Enum = 0x81,
        Type,
        ExtClass,

        // Invalid pseudo-typecode used to specify an unknown type
        Unknown = 0xFD,
        // Invalid pseudo-typecode used for ImmediateFunctionType
        Function = 0xFE,
        // Invalid pseudo-typecode used for InstructionType
        Instruction = 0xFF
    }

    public static partial class EnumHelpers
    {
        private static readonly IReadOnlyDictionary<Type, PascalTypeCode> s_NetTypeToTypeCode = new Dictionary<Type, PascalTypeCode>()
        {
            { typeof(byte), PascalTypeCode.U8 },
            { typeof(sbyte), PascalTypeCode.S8 },
            { typeof(ushort), PascalTypeCode.U16 },
            { typeof(short), PascalTypeCode.S16 },
            { typeof(uint), PascalTypeCode.U32 },
            { typeof(int), PascalTypeCode.S32 },
            { typeof(long), PascalTypeCode.S64 },
            { typeof(float), PascalTypeCode.Single },
            { typeof(double), PascalTypeCode.Double },
            { typeof(decimal), PascalTypeCode.Extended },
            { typeof(CurrencyWrapper), PascalTypeCode.Currency },
            { typeof(VariantWrapper), PascalTypeCode.Variant },

            { typeof(char), PascalTypeCode.WideChar },
            { typeof(string), PascalTypeCode.UnicodeString }, // can also be TypeCode.WideString, at least one known compiledcode.bin uses just UnicodeString though

            { typeof(IntPtr), PascalTypeCode.Pointer },
            { typeof(IFunction), PascalTypeCode.ProcPtr },
            { typeof(IType), PascalTypeCode.Type },
            { typeof(BitArray), PascalTypeCode.Set }
            //{ typeof(Array), TypeCode.Array },
            //{ typeof(ValueType), TypeCode.Record },
            //{ typeof(object), TypeCode.Class }
        };

        private static readonly IReadOnlyDictionary<PascalTypeCode, Type> s_TypeCodeToNetType = new Dictionary<PascalTypeCode, Type>()
        {
            { PascalTypeCode.U8, typeof(byte) },
            { PascalTypeCode.S8, typeof(sbyte) },
            { PascalTypeCode.S16, typeof(short) },
            { PascalTypeCode.U16, typeof(ushort) },
            { PascalTypeCode.S32, typeof(int) },
            { PascalTypeCode.U32, typeof(uint) },
            { PascalTypeCode.S64, typeof(long) },
            { PascalTypeCode.Single, typeof(float) },
            { PascalTypeCode.Double, typeof(double) },
            { PascalTypeCode.Extended, typeof(decimal) },
            { PascalTypeCode.Currency, typeof(CurrencyWrapper) },
            { PascalTypeCode.Variant, typeof(VariantWrapper) },

            { PascalTypeCode.WideChar, typeof(char) },
            { PascalTypeCode.Char, typeof(char) },
            { PascalTypeCode.UnicodeString, typeof(string) },
            { PascalTypeCode.WideString, typeof(string) },
            { PascalTypeCode.PChar, typeof(string) },
            { PascalTypeCode.String, typeof(string) },

            { PascalTypeCode.Pointer, typeof(IntPtr) },
            { PascalTypeCode.ProcPtr, typeof(IFunction) },
            { PascalTypeCode.Type, typeof(IType) },

            { PascalTypeCode.Set, typeof(BitArray) }
        };

        private static readonly ISet<PascalTypeCode> s_PrimitiveTypes = new HashSet<PascalTypeCode>()
        {
            PascalTypeCode.U8,
            PascalTypeCode.S8,
            PascalTypeCode.U16,
            PascalTypeCode.S16,
            PascalTypeCode.U32,
            PascalTypeCode.S32,
            PascalTypeCode.S64,
            PascalTypeCode.Single,
            PascalTypeCode.Double,
            PascalTypeCode.Currency,
            PascalTypeCode.Extended,
            PascalTypeCode.String,
            PascalTypeCode.Pointer,
            PascalTypeCode.PChar,
            PascalTypeCode.Variant,
            PascalTypeCode.Char,
            PascalTypeCode.UnicodeString,
            PascalTypeCode.WideString,
            PascalTypeCode.WideChar
        };

        public static PascalTypeCode ToIFPSTypeCode(this Type type) => s_NetTypeToTypeCode[type];

        public static bool IsPrimitive(this PascalTypeCode code) => s_PrimitiveTypes.Contains(code);

        public static bool EqualsType(this PascalTypeCode code, Type type)
        {
            if (!s_TypeCodeToNetType.TryGetValue(code, out var result)) return false;
            return result == type;
        }
    }
}
