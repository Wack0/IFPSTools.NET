using System;
using LexicalAnalysis;

namespace AST {

    public abstract class Literal : Expr { }

	/// <summary>
	/// May be a float or double
	/// </summary>
	public class FloatLiteral : Literal {
		public FloatLiteral(Double value, TokenFloat.FloatSuffix floatSuffix) {
			this.Value = value;
			this.FloatSuffix = floatSuffix;
		}

		public TokenFloat.FloatSuffix FloatSuffix { get; }

		public Double Value { get; }

        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            switch (this.FloatSuffix) {
                case TokenFloat.FloatSuffix.F:
                    return new ABT.ConstFloat((Single)this.Value, env);

                case TokenFloat.FloatSuffix.NONE:
                case TokenFloat.FloatSuffix.L:
                    return new ABT.ConstDouble(this.Value, env);

                default:
                    throw new InvalidOperationException();
            }
        }
	}

	/// <summary>
	/// May be signed or unsigned
    /// C doesn't have char constant, only int constant
	/// </summary>
	public class IntLiteral : Literal {
		public IntLiteral(Int64 value, TokenInt.IntSuffix suffix) {
			this.Value = value;
			this.Suffix = suffix;
		}

		public TokenInt.IntSuffix Suffix { get; }
		public Int64 Value { get; }

        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            switch (this.Suffix) {
                case TokenInt.IntSuffix.ULL:
                    return new ABT.ConstU64((ulong)this.Value, env);
                case TokenInt.IntSuffix.LL:
                    return new ABT.ConstS64(this.Value, env);

                case TokenInt.IntSuffix.U:
                case TokenInt.IntSuffix.UL:
                    return new ABT.ConstULong((UInt32)this.Value, env);

                case TokenInt.IntSuffix.NONE:
                case TokenInt.IntSuffix.L:
                    return new ABT.ConstLong((Int32)this.Value, env);

                default:
                    throw new InvalidOperationException();
            }
        }
    }

    internal interface IStringLiteral
    {
        string Value { get; }
    }

	/// <summary>
	/// String Literal
	/// </summary>
	public class StringLiteral : Expr, IStringLiteral {
		public StringLiteral(String value) {
			this.Value = value;
		}

		public String Value { get; }

		protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
			return new ABT.ConstStringLiteral(this.Value, env);
		}
	}

    public class UnicodeStringLiteral : Expr, IStringLiteral
    {
        public UnicodeStringLiteral(String value)
        {
            this.Value = value;
        }

        public String Value { get; }

        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            return new ABT.ConstUnicodeStringLiteral(this.Value, env);
        }
    }

}