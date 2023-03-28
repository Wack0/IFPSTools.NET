using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AST;
using CodeGeneration;
using IFPS = IFPSLib.Types;

namespace ABT {
    /* From 3.1.2.5 Types (modified):
     
     * Types are partitioned into
       1) object types (types that describe objects)
       2) function types (types that describe functions)
       3) incomplete types (types that describe objects but lack information needed to determine their sizes).
     
     * [char] large enough to store any member of the basic execution character set.
       An ANSI character is positive.

     * There are 4 signed integer types:
       [signed char] < [short int] < [int] < [long int].

     * [signed char] occupies the same amount of storage as a "plain" char object.
     
     * [int] has the natural size suggested by the architecture of the execution environment.

     * For each of the signed integer types, there is a corresponding (but different) unsigned integer Type (designated with the keyword unsigned) that uses the same amount of storage (including sign information) and has the same alignment requirements. The range of nonnegative values of a signed integer Type is a subrange of the corresponding unsigned integer Type, and the representation of the same Value in each Type is the same. A computation involving unsigned operands can never overflow, because a result that cannot be represented by the resulting unsigned integer Type is reduced modulo the number that is one greater than the largest Value that can be represented by the resulting unsigned integer Type.

     * There are three floating types: float < double < long double.

     * The Type char, the signed and unsigned integer types, and the floating types are collectively called the basic types.

     * There are three character types: char < signed char < unsigned char.

     * An enumeration comprises a set of named integer constant values. Each distinct enumeration constitutes a different enumerated Type.

     * The void Type comprises an empty set of values; it is an incomplete Type that cannot be completed.

     * Any number of derived types can be constructed from the basic, enumerated, and incomplete types, as follows:

     * An array Type describes a contiguously allocated set of objects with a particular member object Type, called the element Type. Array types are characterized by their element Type and by the number of members of the array. An array Type is said to be derived from its element Type, and if its element Type is T , the array Type is sometimes called "array of T". The construction of an array Type from an element Type is called "array Type derivation".

     * A structure Type describes a sequentially allocated set of member objects, each of which has an optionally specified name and possibly distinct Type.

     * A union Type describes an overlapping set of member objects, each of which has an optionally specified name and possibly distinct Type.

     * A function Type describes a function with specified return Type. A function Type is characterized by its return Type and the number and types of its parameters. A function Type is said to be derived from its return Type, and if its return Type is T, the function Type is sometimes called "function returning T". The construction of a function Type from a return Type is called "function Type derivation".

     * A pointer Type may be derived from a function Type, an object Type, or an incomplete Type, called the referenced Type. A pointer Type describes an object whose Value provides a reference to an entity of the referenced Type. A pointer Type derived from the referenced Type T is sometimes called "pointer to T". The construction of a pointer Type from a referenced Type is called "pointer Type derivation".

     * These methods of constructing derived types can be applied recursively.

     * <integral>   : [char], [signed/unsigned short/int/long], [enum]
     * <arithmetic> : <integral>, [float], [double]
     * <scalar>     : <arithmetic>, <pointer>
     * <aggregate> : <array>, <struct>, <union>

       function

       void

       array

       struct

       union

       scalar
         |
         +--- pointer
         |
         +--- arithmetic
                  |
                  +--- double
                  |
                  +--- float
                  |
                  +--- integral
                          |
                          +--- enum
                          |
                          +--- [signed/unsigned] long
                          |
                          +--- [signed/unsigned] short
                          |
                          +--- [signed/unsigned] char

     * A pointer to void shall have the same representation and alignment requirements as a pointer to a character Type. Other pointer types need not have the same representation or alignment requirements.

     * An array Type of unknown size is an incomplete Type. It is completed, for an identifier of that Type, by specifying the size in a later declaration (with internal or external linkage). A structure or union Type of unknown content is an incomplete Type. It is completed, for all declarations of that Type, by declaring the same structure or union tag with its defining content later in the same scope.

     * Array, function, and pointer types are collectively called derived declarator types. A declarator Type derivation from a Type T is the construction of a derived declarator Type from T by the application of an array, a function, or a pointer Type derivation to T.
     
     */

    public enum ExprTypeKind {
        VOID,
        CHAR,
        UCHAR,
        SHORT,
        USHORT,
        LONG,
        ULONG,
        S64,
        U64,
        FLOAT,
        DOUBLE,
        POINTER,
        FUNCTION,
        ARRAY,
        INCOMPLETE_ARRAY,
        STRUCT_OR_UNION,
        ANSI_STRING,
        UNICODE_STRING,
        COM_INTERFACE,
        COM_VARIANT
    }

    public interface IExprTypeWithName
    {
        string TypeName { get; set; }
    }

    public abstract partial class ExprType {
        protected ExprType(Boolean isConst, Boolean isVolatile) {
            this.IsConst = isConst;
            this.IsVolatile = isVolatile;
        }

        public const Int32 SIZEOF_CHAR = 1;
        public const Int32 SIZEOF_SHORT = 2;
        public const Int32 SIZEOF_LONG = 4;
        public const Int32 SIZEOF_FLOAT = 4;
        public const Int32 SIZEOF_DOUBLE = 8;
        public const Int32 SIZEOF_INT64 = 8;
        public const Int32 SIZEOF_POINTER = 4;
        public const Int32 SIZEOF_VARIANT = 16;

        public const Int32 ALIGN_CHAR = 1;
        public const Int32 ALIGN_SHORT = 2;
        public const Int32 ALIGN_LONG = 4;
        public const Int32 ALIGN_FLOAT = 4;
        public const Int32 ALIGN_DOUBLE = 4;
        public const Int32 ALIGN_POINTER = 4;
        public const Int32 ALIGN_INT64 = 8;
        public const Int32 ALIGN_VARIANT = 4;

        public abstract ExprTypeKind Kind { get; }

        public virtual Boolean IsArith => false;

        public virtual Boolean IsIntegral => false;

        public virtual Boolean IsScalar => false;

        public virtual Boolean IsComplete => true;

        public abstract Boolean EqualType(ExprType other);

        public override sealed String ToString() => Decl();

        public String DumpQualifiers() {
            String str = "";
            if (this.IsConst) {
                str += "const ";
            }
            if (this.IsVolatile) {
                str += "volatile ";
            }
            return str;
        }

        public abstract ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile);

        public abstract Int32 SizeOf { get; }
        public abstract Int32 Alignment { get; }

        public readonly Boolean IsConst;
        public readonly Boolean IsVolatile;

        protected ImmutableList<AST.TypeAttrib> _TypeAttribsImpl { get; set; } = ImmutableList<AST.TypeAttrib>.Empty;

        public virtual ImmutableList<AST.TypeAttrib> TypeAttribs { get => _TypeAttribsImpl; }
        internal ImmutableList<AST.TypeAttrib> TypeAttribsSet { set => _TypeAttribsImpl = value; }

    }

    public partial class VoidType : ExprType {
        public VoidType(Boolean isConst = false, Boolean isVolatile = false)
            : base(isConst, isVolatile) {
        }

        public override ExprTypeKind Kind => ExprTypeKind.VOID;

        public override Int32 SizeOf => SIZEOF_POINTER;

        public override Int32 Alignment => SIZEOF_POINTER;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile) =>
            new VoidType(isConst, isVolatile) { TypeAttribsSet = TypeAttribs };

        public override Boolean EqualType(ExprType other) => other.Kind == ExprTypeKind.VOID;

        public override IFPS.IType Emit(CGenState state)
        {
            // void is not a type. for a function return value, it's marked as null. for a pointer, it would be marked as pointer type.
            return null;
        }
    }

    public abstract class ScalarType : ExprType {
        protected ScalarType(Boolean isConst, Boolean isVolatile)
            : base(isConst, isVolatile) { }
        public override Boolean IsScalar => true;
    }

    public abstract class ArithmeticType : ScalarType {
        protected ArithmeticType(Boolean isConst, Boolean isVolatile)
            : base(isConst, isVolatile) { }
        public override Boolean IsArith => true;
        public override Boolean EqualType(ExprType other) => this.Kind == other.Kind;
    }

    public abstract class IntegralType : ArithmeticType {
        protected IntegralType(Boolean isConst, Boolean isVolatile)
            : base(isConst, isVolatile) { }
        public override Boolean IsIntegral => true;
    }

    public partial class CharType : IntegralType {
        public CharType(Boolean isConst = false, Boolean isVolatile = false)
            : base(isConst, isVolatile) { }

        public override ExprTypeKind Kind => ExprTypeKind.CHAR;

        public override Int32 SizeOf => SIZEOF_CHAR;

        public override Int32 Alignment => ALIGN_CHAR;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile) =>
            new CharType(isConst, isVolatile) { TypeAttribsSet = TypeAttribs };

        public override IFPS.IType Emit(CGenState state)
        {
            return IFPS.PrimitiveType.Create<sbyte>();
        }
    }

    public partial class UCharType : IntegralType {
        public UCharType(Boolean isConst = false, Boolean isVolatile = false)
            : base(isConst, isVolatile) { }

        public override ExprTypeKind Kind => ExprTypeKind.UCHAR;

        public override Int32 SizeOf => SIZEOF_CHAR;

        public override Int32 Alignment => ALIGN_CHAR;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile) =>
            new UCharType(isConst, isVolatile) { TypeAttribsSet = TypeAttribs };

        public override IFPS.IType Emit(CGenState state)
        {
            return IFPS.PrimitiveType.Create<byte>();
        }
    }

    public partial class ShortType : IntegralType {
        public ShortType(Boolean isConst = false, Boolean isVolatile = false)
            : base(isConst, isVolatile) { }

        public override ExprTypeKind Kind => ExprTypeKind.SHORT;

        public override Int32 SizeOf => SIZEOF_SHORT;

        public override Int32 Alignment => ALIGN_SHORT;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile) =>
            new ShortType(isConst, isVolatile) { TypeAttribsSet = TypeAttribs };

        public override IFPS.IType Emit(CGenState state)
        {
            return IFPS.PrimitiveType.Create<short>();
        }
    }

    public partial class UShortType : IntegralType {
        public UShortType(Boolean isConst = false, Boolean isVolatile = false)
            : base(isConst, isVolatile) { }

        public override ExprTypeKind Kind => ExprTypeKind.USHORT;

        public override Int32 SizeOf => SIZEOF_SHORT;

        public override Int32 Alignment => ALIGN_SHORT;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile) =>
            new UShortType(isConst, isVolatile) { TypeAttribsSet = TypeAttribs };
        public override IFPS.IType Emit(CGenState state)
        {
            return IFPS.PrimitiveType.Create<ushort>();
        }
    }

    public partial class LongType : IntegralType {
        public LongType(Boolean isConst = false, Boolean isVolatile = false)
            : base(isConst, isVolatile) { }

        public override ExprTypeKind Kind => ExprTypeKind.LONG;

        public override Int32 SizeOf => SIZEOF_LONG;

        public override Int32 Alignment => ALIGN_LONG;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile) {
            return new LongType(isConst, isVolatile) { TypeAttribsSet = TypeAttribs };
        }

        public override IFPS.IType Emit(CGenState state)
        {
            return IFPS.PrimitiveType.Create<int>();
        }
    }

    public partial class ULongType : IntegralType {
        public ULongType(Boolean isConst = false, Boolean isVolatile = false)
            : base(isConst, isVolatile) { }

        public override ExprTypeKind Kind => ExprTypeKind.ULONG;

        public override Int32 SizeOf => SIZEOF_LONG;

        public override Int32 Alignment => ALIGN_LONG;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile) {
            return new ULongType(isConst, isVolatile) { TypeAttribsSet = TypeAttribs };
        }

        public override IFPS.IType Emit(CGenState state)
        {
            return IFPS.PrimitiveType.Create<uint>();
        }
    }

    public partial class S64Type : IntegralType
    {
        public S64Type(Boolean isConst = false, Boolean isVolatile = false)
            : base(isConst, isVolatile) { }

        public override ExprTypeKind Kind => ExprTypeKind.S64;

        public override Int32 SizeOf => SIZEOF_INT64;

        public override Int32 Alignment => ALIGN_INT64;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile)
        {
            return new S64Type(isConst, isVolatile) { TypeAttribsSet = TypeAttribs };
        }

        public override IFPS.IType Emit(CGenState state)
        {
            return IFPS.PrimitiveType.Create<long>();
        }
    }

    public partial class U64Type : IntegralType
    {
        public U64Type(Boolean isConst = false, Boolean isVolatile = false)
            : base(isConst, isVolatile) { }

        public override ExprTypeKind Kind => ExprTypeKind.U64;

        public override Int32 SizeOf => SIZEOF_INT64;

        public override Int32 Alignment => ALIGN_INT64;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile)
        {
            return new S64Type(isConst, isVolatile) { TypeAttribsSet = TypeAttribs };
        }

        public override IFPS.IType Emit(CGenState state)
        {
            return IFPS.PrimitiveType.Create<ulong>();
        }
    }

    public partial class FloatType : ArithmeticType {
        public FloatType(Boolean isConst = false, Boolean isVolatile = false)
            : base(isConst, isVolatile) { }

        public override ExprTypeKind Kind => ExprTypeKind.FLOAT;

        public override Int32 SizeOf => SIZEOF_FLOAT;

        public override Int32 Alignment => ALIGN_FLOAT;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile) =>
            new FloatType(isConst, isVolatile) { TypeAttribsSet = TypeAttribs };

        public override IFPS.IType Emit(CGenState state)
        {
            return IFPS.PrimitiveType.Create<float>();
        }
    }

    public partial class DoubleType : ArithmeticType {
        public DoubleType(Boolean isConst = false, Boolean isVolatile = false)
            : base(isConst, isVolatile) { }

        public override ExprTypeKind Kind => ExprTypeKind.DOUBLE;

        public override Int32 SizeOf => SIZEOF_DOUBLE;

        public override Int32 Alignment => ALIGN_DOUBLE;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile) =>
            new DoubleType(isConst, isVolatile) { TypeAttribsSet = TypeAttribs };

        public override IFPS.IType Emit(CGenState state)
        {
            return IFPS.PrimitiveType.Create<double>();
        }
    }

    public partial class PointerType : ScalarType {
        public PointerType(ExprType refType, Boolean isConst = false, Boolean isVolatile = false)
            : base(isConst, isVolatile) {
            this.RefType = refType;
        }

        public override ExprTypeKind Kind => ExprTypeKind.POINTER;

        public override Int32 SizeOf => SIZEOF_POINTER;

        public override Int32 Alignment => ALIGN_POINTER;

        public override Int32 Precedence => 1;

        public readonly ExprType RefType;

        public bool IsForOpenArray => TypeAttribs.Any((attr) => attr.Name == "__open");

        private bool m_IsRef = true; // will be set to false if this is anything BUT a local variable or a function argument (not return type)

        public bool IsRef
        {
            get => IsForOpenArray || (RefType.Kind != ExprTypeKind.VOID && RefType.Kind != ExprTypeKind.POINTER && m_IsRef);
            set => m_IsRef = value;
        }

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile) =>
            new PointerType(this.RefType, isConst, isVolatile) { TypeAttribsSet = TypeAttribs };

        public override Boolean EqualType(ExprType other) =>
            other.Kind == ExprTypeKind.POINTER && ((PointerType)other).RefType.EqualType(this.RefType);

        public override IFPS.IType Emit(CGenState state)
        {
            // Pointer is special.
            // There's a pointer type which the runtime requires to be present (null deref otherwise).
            // Therefore, this function should never be called, codegen will have to handle this itself.

            // For a void pointer / non-IsRef, call the base EmitType (will emit u32)
            if (!IsRef) return state.EmitType(this);

            // For a function pointer, emit the pointer type, otherwise, invalid.
            var fptr = RefType as FunctionType;
            if (fptr != null) return fptr.EmitPointer(state);
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Incomplete array: an array with unknown length.
    /// </summary>
    public partial class IncompleteArrayType : ExprType, IExprTypeWithName {
        public IncompleteArrayType(ExprType elemType)
            : base(elemType.IsConst, elemType.IsVolatile) {
            this.ElemType = elemType;
            var ptr = elemType as PointerType;
            if (ptr != null) ptr.IsRef = false;
            TypeName = string.Format("array[] {0}", elemType);
        }

        public override ExprTypeKind Kind => ExprTypeKind.INCOMPLETE_ARRAY;

        public override Int32 SizeOf {
            get {
                throw new InvalidOperationException("Incomplete array. Cannot get sizeof.");
            }
        }

        public override Int32 Alignment => this.ElemType.Alignment;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile) {
            throw new InvalidOperationException("An array has the same cv qualifiers of its elems.");
        }

        public override Boolean EqualType(ExprType other)
            => other.Kind == ExprTypeKind.INCOMPLETE_ARRAY && ((IncompleteArrayType)other).ElemType.EqualType(ElemType);

        public override Boolean IsComplete => true; // in IFPS, open bounded array is a perfectly cromulent type

        public string TypeName { get; set; }

        public ExprType Complete(Int32 numElems) => new ArrayType(this.ElemType, numElems);

        public readonly ExprType ElemType;

        public int? DeclaratorElems { get; private set; } = null;
        public ExprType SetDeclaratorElems(int count)
        {
            return new IncompleteArrayType(ElemType)
            {
                DeclaratorElems = count
            };
        }

        public override IFPS.IType Emit(CGenState state)
        {
            if (ElemType.Kind == ExprTypeKind.POINTER)
            {
                var ptr = ElemType as PointerType;
                if (ptr.TypeAttribs.Any((attr) => attr.Name == "__open"))
                {
                    // this is open array of pointer.

                    return new IFPS.ArrayType(state.TypePointer);
                }
            }
            return new IFPS.ArrayType(state.EmitType(ElemType));
        }
    }

    public partial class ArrayType : ExprType, IExprTypeWithName {
        public ArrayType(ExprType elemType, Int32 numElems)
            : base(elemType.IsConst, elemType.IsVolatile) {
            this.ElemType = elemType;
            this.NumElems = numElems;
            var ptr = elemType as PointerType;
            if (ptr != null) ptr.IsRef = false;
            TypeName = string.Format("array[{0}] {1}", numElems, elemType);
        }

        public override ExprTypeKind Kind => ExprTypeKind.ARRAY;

        public override Int32 SizeOf => this.ElemType.SizeOf * this.NumElems;

        public override Int32 Alignment => this.ElemType.Alignment;

        public string TypeName { get; set; }

        public readonly ExprType ElemType;

        public readonly Int32 NumElems;


        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile) {
            throw new InvalidOperationException("An array has the same cv qualifiers of its elems.");
        }

        public override Boolean EqualType(ExprType other) =>
            other.Kind == ExprTypeKind.ARRAY && ((ArrayType)other).ElemType.EqualType(this.ElemType);

        public override IFPS.IType Emit(CGenState state)
        {
            return new IFPS.StaticArrayType(state.EmitType(ElemType), NumElems);
        }
    }

    public partial class UnicodeStringType : ScalarType
    {
        public UnicodeStringType(Boolean isConst = false, Boolean isVolatile = false) : base(isConst, isVolatile)
        {
        }

        public override ExprTypeKind Kind => ExprTypeKind.UNICODE_STRING;

        public override Int32 SizeOf => SIZEOF_POINTER;

        public override Int32 Alignment => ALIGN_POINTER;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile)
        {
            return new UnicodeStringType(isConst, isVolatile) { TypeAttribsSet = TypeAttribs };
        }

        public override Boolean EqualType(ExprType other) =>
            other.Kind == ExprTypeKind.UNICODE_STRING;

        public override IFPS.IType Emit(CGenState state)
        {
            return new IFPS.PrimitiveType(IFPS.PascalTypeCode.UnicodeString);
        }
    }

    public partial class AnsiStringType : ScalarType
    {
        public AnsiStringType(Boolean isConst = false, Boolean isVolatile = false) : base(isConst, isVolatile)
        {
        }

        public override ExprTypeKind Kind => ExprTypeKind.ANSI_STRING;

        public override Int32 SizeOf => SIZEOF_POINTER;

        public override Int32 Alignment => ALIGN_POINTER;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile)
        {
            return new AnsiStringType(isConst, isVolatile) { TypeAttribsSet = TypeAttribs };
        }

        public override Boolean EqualType(ExprType other) =>
            other.Kind == ExprTypeKind.ANSI_STRING;

        public override IFPS.IType Emit(CGenState state)
        {
            return new IFPS.PrimitiveType(IFPS.PascalTypeCode.String);
        }
    }

    public partial class StructOrUnionType : ExprType, IExprTypeWithName {
        private StructOrUnionType(StructOrUnionLayout layout, Boolean isConst, Boolean isVolatile)
            : base(isConst, isVolatile) {
            this._layout = layout;
        }

        public override ExprTypeKind Kind => ExprTypeKind.STRUCT_OR_UNION;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile) =>
            new StructOrUnionType(this._layout, isConst, isVolatile) { TypeAttribsSet = TypeAttribs };

        public static StructOrUnionType CreateIncompleteStruct(String name, Boolean is_const, Boolean is_volatile) =>
            new StructOrUnionType(new StructOrUnionLayout($"struct {name}"), is_const, is_volatile);

        public static StructOrUnionType CreateIncompleteUnion(String name, Boolean is_const, Boolean is_volatile) =>
            new StructOrUnionType(new StructOrUnionLayout($"union {name}"), is_const, is_volatile);

        public static StructOrUnionType CreateIncompleteType(AST.StructOrUnion structOrUnion, String name) =>
            structOrUnion == AST.StructOrUnion.STRUCT
                ? CreateIncompleteStruct(name, false, false)
                : CreateIncompleteUnion(name, false, false);

        public static StructOrUnionType CreateStruct(String name, IReadOnlyList<Tuple<String, ExprType>> attribs, Boolean is_const, Boolean is_volatile) {
            StructOrUnionLayout layout = new StructOrUnionLayout($"struct {name}");
            layout.DefineStruct(attribs);
            return new StructOrUnionType(layout, is_const, is_volatile);
        }

        public static StructOrUnionType CreateUnion(String name, IReadOnlyList<Tuple<String, ExprType>> attribs, Boolean is_const, Boolean is_volatile) {
            StructOrUnionLayout layout = new StructOrUnionLayout($"union {name}");
            layout.DefineUnion(attribs);
            return new StructOrUnionType(layout, is_const, is_volatile);
        }

        public void DefineStruct(IReadOnlyList<Tuple<String, ExprType>> attribs) => this._layout.DefineStruct(attribs);

        public void DefineUnion(IReadOnlyList<Tuple<String, ExprType>> attribs) => this._layout.DefineUnion(attribs);

        public void Define(
            AST.StructOrUnion structOrUnion,
            ImmutableList<Tuple<Option<String>, ExprType>> members) {
            var _members = members.ConvertAll(_ => Tuple.Create(_.Item1.Value, _.Item2));
            if (structOrUnion == AST.StructOrUnion.STRUCT) {
                DefineStruct(_members);
            } else {
                DefineUnion(_members);
            }
        }

        public String Dump(Boolean dump_attribs) {
            if (!this.IsComplete) {
                return "incompleted Type " + this._layout.TypeName;
            }
            String str = $"{this._layout.TypeName} (size = {this.SizeOf})";
            if (dump_attribs) {
                str += "\n";
                foreach (Utils.StoreEntry attrib in this._layout.Attribs) {
                    str += $"  [base + {attrib.offset}] {attrib.name} : {attrib.type}\n";
                }
            }
            return str;
        }

        public override Boolean EqualType(ExprType other) =>
            other.Kind == ExprTypeKind.STRUCT_OR_UNION && ReferenceEquals(((StructOrUnionType)other)._layout, this._layout);

        public override IFPS.IType Emit(CGenState state)
        {
            if (!IsStruct)
            {
                // This is a union, then we need to hack around the lack of union support.
                // Create a byte array of size sizeof(union).
                // When emitting code we will need to get a pointer to this union and cast it with runtime hacks to the wanted type.
                // Codegen will emit the element types as needed when they are used.
                return new IFPS.StaticArrayType(state.TypeUByte, SizeOf) { Name = "__arr_" + _layout.TypeName };
            }

            // Create a record.
            var numElements = _layout.Attribs.Count;
            var elements = new List<IFPS.IType>(numElements);
            var names = new List<string>(numElements);
            foreach (var elem in _layout.Attribs)
            {
                elements.Add(state.EmitType(elem.type));
                names.Add(elem.name);
            }
            return new IFPS.RecordType(elements, names) { Name = _layout.TypeName };
        }

        public override Boolean IsComplete => this._layout.IsComplete;

        public override Int32 SizeOf => this._layout.SizeOf;

        public override Int32 Alignment => this._layout.Alignment;

        public Boolean IsStruct => this._layout.IsStruct;

        public IReadOnlyList<Utils.StoreEntry> Attribs => this._layout.Attribs;

        private readonly StructOrUnionLayout _layout;

        private string _ExternalTypeName;
        public string TypeName { get => string.IsNullOrEmpty(_ExternalTypeName) ? _layout.TypeName : _ExternalTypeName; set => _ExternalTypeName = value; }

        private class StructOrUnionLayout {

            // Create an incomplete struct.
            public StructOrUnionLayout(String typename) {
                this._attribs = null;
                this._size_of = 0;
                this.TypeName = typename;
            }

            public void DefineStruct(IReadOnlyList<Tuple<String, ExprType>> attribs) {
                if (this.IsComplete) {
                    throw new InvalidOperationException("Cannot redefine a struct.");
                }

                this._attribs = new List<Utils.StoreEntry>();
                Int32 offset = 0;
                Int32 struct_alignment = 0;
                foreach (Tuple<String, ExprType> attrib in attribs) {
                    String name = attrib.Item1;
                    ExprType type = attrib.Item2;

                    var ptr = type as PointerType;
                    if (ptr != null) ptr.IsRef = false;

                    Int32 attrib_alignment = type.Alignment;

                    // All attributes must be aligned.
                    // This means that the alignment of the struct is the largest attribute alignment.
                    struct_alignment = Math.Max(struct_alignment, attrib_alignment);

                    // Make sure all attributes are put into aligned places.
                    offset = Utils.RoundUp(offset, attrib_alignment);

                    this._attribs.Add(new Utils.StoreEntry(name, type, offset));

                    offset += type.SizeOf;
                }

                this._size_of = Utils.RoundUp(offset, struct_alignment);
            }

            public void DefineUnion(IEnumerable<Tuple<String, ExprType>> attribs) {
                if (this.IsComplete) {
                    throw new InvalidOperationException("Redefining a union.");
                }

                this._attribs = attribs
                    .Select(attrib => new Utils.StoreEntry(attrib.Item1, attrib.Item2, 0))
                    .ToList();

                foreach (var attrib in attribs)
                {
                    var ptr = attrib.Item2 as PointerType;
                    if (ptr != null) ptr.IsRef = false;
                }

                this._size_of = this.Attribs.Select(attrib => attrib.type.Alignment).Max();
            }

            public IReadOnlyList<Utils.StoreEntry> Attribs {
                get {
                    if (!this.IsComplete) {
                        throw new InvalidOperationException("Incomplete struct or union. Cannot get attributes.");
                    }
                    return this._attribs;
                }
            }

            // Is this a struct or union.
            public Boolean IsStruct => this.TypeName.StartsWith("struct");

            // Whether the attributes are supplied.
            public Boolean IsComplete => this._attribs != null;

            // Only a complete Type has a valid size.
            public Int32 SizeOf {
                get {
                    if (!this.IsComplete) {
                        throw new InvalidOperationException("Incomplete struct or union. Cannot get size.");
                    }
                    return this._size_of;
                }
            }

            public Int32 Alignment => this.Attribs.Select(_ => _.type.Alignment).Max();

            public String TypeName { get; }

            /// <summary>
            /// Private records of all the Attribs with their names, types, and offsets.
            /// </summary>
            private List<Utils.StoreEntry> _attribs;

            /// <summary>
            /// size_of and alignment can only be changed by defining the layout.
            /// </summary>
            private Int32 _size_of;
        }
    }

    // class FunctionType
    // ===============
    // represents the function Type
    // stores the names, types, and offsets of arguments
    // 
    // calling convention:
    // https://developer.apple.com/library/mac/documentation/DeveloperTools/Conceptual/LowLevelABI/130-IA-32_Function_Calling_Conventions/IA32.html
    // 
    // TODO: name is optional
    public partial class FunctionType : ExprType {
        protected FunctionType(ExprType ret_t, List<Utils.StoreEntry> args, Boolean is_varargs)
            : base(true, false) {
            this.Args = args;
            this.ReturnType = ret_t;
            this.HasVarArgs = is_varargs;


            var ptr = ret_t as PointerType;
            if (ptr != null) ptr.IsRef = false;
        }

        public override ExprTypeKind Kind => ExprTypeKind.FUNCTION;

        public override Int32 SizeOf => SIZEOF_POINTER;

        public override Int32 Alignment => ALIGN_POINTER;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile) {
            return new FunctionType(this.ReturnType, this.Args, this.HasVarArgs) { TypeAttribsSet = TypeAttribs };
        }

        public override Boolean EqualType(ExprType other) {
            return (other is FunctionType)
                && (other as FunctionType).HasVarArgs == this.HasVarArgs

                // same return Type
                && (other as FunctionType).ReturnType.EqualType(this.ReturnType)

                // same number of arguments
                && (other as FunctionType).Args.Count == this.Args.Count

                // same argument types
                && (other as FunctionType).Args.Zip(this.Args, (entry1, entry2) => entry1.type.EqualType(entry2.type)).All(_ => _);
        }

        public static FunctionType Create(ExprType ret_type, List<Tuple<String, ExprType>> args, Boolean is_varargs) {
            Tuple<Int32, IReadOnlyList<Int32>> r_pack = Utils.PackArguments(args.ConvertAll(_ => _.Item2));
            IReadOnlyList<Int32> offsets = r_pack.Item2;
            return new FunctionType(
                ret_type,
                args.Zip(offsets,
                    (name_type, offset) => new Utils.StoreEntry(name_type.Item1, name_type.Item2, offset, true)
                ).ToList(),
                is_varargs
            );
        }

        // TODO: param name should be optional
        public static FunctionType Create(ExprType returnType, ImmutableList<Tuple<Option<String>, ExprType>> args, Boolean hasVarArgs) =>
            Create(returnType, args.Select(_ => Tuple.Create(_.Item1.IsSome ? _.Item1.Value : "", _.Item2)).ToList(), hasVarArgs);

        public static FunctionType Create(ExprType returnType) =>
            Create(returnType, ImmutableList<Tuple<Option<String>, ExprType>>.Empty, true);

        public String Dump(Boolean dump_args = false) {
            String str = "function";
            if (dump_args) {
                str += "\n";
                foreach (Utils.StoreEntry arg in this.Args) {
                    str += $"  [%ebp + {arg.offset}] {arg.name} : {arg.type}\n";
                }
            }
            return str;
        }

        public override IFPS.IType Emit(CGenState state)
        {
            // in pascalscript a function is not a type, unless when it's a function pointer.
            // anyway, we shouldn't call this.
            throw new InvalidOperationException();
        }

        public IFPS.IType EmitPointer(CGenState state)
        {
            // pointertype should call this when it's a function pointer.
            // first: varargs is unsupported
            // technically incorrect, "varargs" is used internally for some internal generic functions (getarraylength/setarraylength)
            //if (HasVarArgs) throw new InvalidOperationException("Varargs function pointers are unsupported in IFPS."); // BUGBUG: probably can do something better?

            var args = new List<IFPSLib.FunctionArgumentType>();
            foreach (var arg in Args)
            {
                args.Add(arg.type is PointerType ? IFPSLib.FunctionArgumentType.Out : IFPSLib.FunctionArgumentType.In);
            }

            return new IFPS.FunctionPointerType(!(ReturnType is VoidType), args);
        }

        // type attributes may be applied to the return type, allow for this
        public override ImmutableList<TypeAttrib> TypeAttribs
        {
            get
            {
                if (!_TypeAttribsImpl.IsEmpty) return _TypeAttribsImpl;

                if (!ReturnType.TypeAttribs.IsEmpty) _TypeAttribsImpl = ReturnType.TypeAttribs;

                return _TypeAttribsImpl;
            }
        }

        public readonly Boolean HasVarArgs;
        public readonly ExprType ReturnType;
        public readonly List<Utils.StoreEntry> Args;
    }

    // class EmptyFunctionType
    // ====================
    // defines an empty function: no arguments, returns void
    // 
    // TODO: remove this
    public class EmptyFunctionType : FunctionType {
        public EmptyFunctionType() : base(new VoidType(), new List<Utils.StoreEntry>(), false) {
        }
    }


    // class ComInterfaceType
    // defines a COM interface
    public partial class ComInterfaceType : ScalarType, IExprTypeWithName
    {
        public ComInterfaceType(Guid guid, Boolean isConst = false, Boolean isVolatile = false) : base(isConst, isVolatile)
        {
            InterfaceGuid = guid;
            TypeName = "__interface " + guid;
        }


        public Guid InterfaceGuid { get; }

        public override ExprTypeKind Kind => ExprTypeKind.COM_INTERFACE;

        public override Int32 SizeOf => SIZEOF_POINTER;

        public override Int32 Alignment => ALIGN_POINTER;

        public string TypeName { get; set; }

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile)
        {
            return new ComInterfaceType(InterfaceGuid, isConst, isVolatile) { TypeAttribsSet = TypeAttribs };
        }

        public override Boolean EqualType(ExprType other)
        {
            if (other.Kind != ExprTypeKind.COM_INTERFACE) return false;
            var rhs = other as ComInterfaceType;
            if (rhs == null) return false;
            return InterfaceGuid == rhs.InterfaceGuid;
        }

        public override IFPS.IType Emit(CGenState state)
        {
            return new IFPS.ComInterfaceType(InterfaceGuid);
        }
    }

    // class ComVariantType
    // defines a COM OleAut VARIANT
    public partial class ComVariantType : ScalarType
    {
        public ComVariantType(Boolean isConst = false, Boolean isVolatile = false) : base(isConst, isVolatile)
        {
        }



        public override ExprTypeKind Kind => ExprTypeKind.COM_VARIANT;

        public override Int32 SizeOf => SIZEOF_VARIANT;

        public override Int32 Alignment => ALIGN_VARIANT;

        public override ExprType GetQualifiedType(Boolean isConst, Boolean isVolatile)
        {
            return new ComVariantType(isConst, isVolatile) { TypeAttribsSet = TypeAttribs };
        }

        public override Boolean EqualType(ExprType other) => other.Kind == ExprTypeKind.COM_VARIANT;

        public override IFPS.IType Emit(CGenState state)
        {
            return new IFPS.PrimitiveType(IFPS.PascalTypeCode.Variant);
        }
    }
}
