using System;
using CodeGeneration;
using IFPSLib.Emit;

namespace ABT {

    /// <summary>
    /// Compile-time constant. Cannot get the address.
    /// </summary>
    public abstract partial class ConstExpr : Expr {
        protected ConstExpr(Env env) {
            this.Env = env;
        }

        public override sealed Env Env { get; }

        public override sealed Boolean IsConstExpr => true;

        public override sealed Boolean IsLValue => false;

        public override sealed bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false) => false;
    }

    public sealed partial class ConstLong : ConstExpr {
        public ConstLong(Int32 value, Env env)
            : base(env) {
            this.Value = value;
        }
        public Int32 Value { get; }

        public override String ToString() => $"{this.Value}";

        private static ExprType _type = new LongType(true);
        public override ExprType Type => _type;
    }

    public sealed partial class ConstULong : ConstExpr {
        public ConstULong(UInt32 value, Env env)
            : base(env) {
            this.Value = value;
        }
        public UInt32 Value { get; }

        public override String ToString() => $"{this.Value}u";

        private static ExprType _type = new ULongType(true);
        public override ExprType Type => _type;
    }

    public sealed partial class ConstS64 : ConstExpr
    {
        public ConstS64(long value, Env env)
            : base(env)
        {
            this.Value = value;
        }
        public long Value { get; }

        public override String ToString() => $"{this.Value}ll";

        private static ExprType _type = new S64Type(true);
        public override ExprType Type => _type;
    }

    public sealed partial class ConstU64 : ConstExpr
    {
        public ConstU64(ulong value, Env env)
            : base(env)
        {
            this.Value = value;
        }
        public ulong Value { get; }

        public override String ToString() => $"{this.Value}ull";

        private static ExprType _type = new U64Type(true);
        public override ExprType Type => _type;
    }

    public sealed partial class ConstShort : ConstExpr {
        public ConstShort(Int16 value, Env env)
            : base(env) {
            this.Value = value;
        }

        public Int16 Value { get; }

        private static ExprType _type = new ShortType(true);
        public override ExprType Type => _type;
    }

    public sealed partial class ConstUShort : ConstExpr {
        public ConstUShort(UInt16 value, Env env)
            : base(env) {
            this.Value = value;
        }

        public UInt16 Value { get; }

        private static ExprType _type = new UShortType(true);
        public override ExprType Type => _type;
    }

    public sealed partial class ConstChar : ConstExpr {
        public ConstChar(SByte value, Env env)
            : base(env) {
            this.Value = value;
        }

        public SByte Value { get; }

        private static ExprType _type = new CharType(true);
        public override ExprType Type => _type;
    }

    public sealed partial class ConstUChar : ConstExpr {
        public ConstUChar(Byte value, Env env)
            : base(env) {
            this.Value = value;
        }

        public Byte Value { get; }

        private static ExprType _type = new UCharType(true);
        public override ExprType Type => _type;
    }

    public sealed partial class ConstPtr : ConstExpr {
        public ConstPtr(UInt32 value, ExprType type, Env env)
            : base(env) {
            this.Value = value;
            this.Type = type;
        }

        public UInt32 Value { get; }

        public override ExprType Type { get; }

        public override String ToString() =>
            $"({this.Type} *)0x{this.Value.ToString("X8")}";
    }

    public sealed partial class ConstFloat : ConstExpr {
        public ConstFloat(Single value, Env env)
            : base(env) {
            this.Value = value;
        }

        public Single Value { get; }

        public override String ToString() => $"{this.Value}f";

        private static ExprType _type = new FloatType(true);
        public override ExprType Type => _type;
    }

    public sealed partial class ConstDouble : ConstExpr {
        public ConstDouble(Double value, Env env)
            : base(env) {
            this.Value = value;
        }

        public Double Value { get; }

        public override String ToString() => $"{this.Value}";

        private static ExprType _type = new DoubleType(true);
        public override ExprType Type => _type;
    }

    public sealed partial class ConstStringLiteral : ConstExpr, AST.IStringLiteral {
        public ConstStringLiteral(String value, Env env)
            : base(env) {
            this.Value = value;
        }

        public String Value { get; }

        public override String ToString() => $"\"{this.Value}\"";

        private static ExprType _type = new AnsiStringType(true);
        public override ExprType Type => _type;
    }

    public sealed partial class ConstUnicodeStringLiteral : ConstExpr, AST.IStringLiteral
    {
        public ConstUnicodeStringLiteral(String value, Env env)
            : base(env)
        {
            this.Value = value;
        }

        public String Value { get; }

        public override String ToString() => $"L\"{this.Value}\"";

        private static ExprType _type = new UnicodeStringType(true);
        public override ExprType Type => _type;
    }

}
