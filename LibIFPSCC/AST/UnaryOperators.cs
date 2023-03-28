﻿using System;
using static AST.SemanticAnalysis;

namespace AST {

    public abstract class UnaryExprOperator : Expr {
        protected UnaryExprOperator(Expr expr) {
            this.Expr = expr;
        }
        public Expr Expr { get; }
    }

    /// <summary>
    /// Postfix increment: x++
    /// </summary>
    // TODO: Check lvalue
    public sealed class PostIncrement : UnaryExprOperator {
        public PostIncrement(Expr expr)
            : base(expr) { }
        public static Expr Create(Expr expr) => new PostIncrement(expr);

        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            ABT.Expr expr = this.Expr.GetExpr(env, info);

            if (!expr.Type.IsScalar) {
                throw new InvalidOperationException("Expected a scalar.").Attach(expr);
            }

            return new ABT.PostIncrement(expr);
        }
    }

    /// <summary>
    /// Postfix decrement: x--
    /// </summary>
    // TODO: Check lvalue
    public sealed class PostDecrement : UnaryExprOperator {
        public PostDecrement(Expr expr)
            : base(expr) { }
        public static Func<Expr, Expr> Create { get; } = expr => new PostDecrement(expr);

        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            ABT.Expr expr = this.Expr.GetExpr(env, info);

            if (!expr.Type.IsScalar) {
                throw new InvalidOperationException("Expected a scalar.").Attach(expr);
            }

            return new ABT.PostDecrement(expr);
        }
    }

    /// <summary>
    /// sizeof(Type)
    /// </summary>
    [Checked]
    public sealed class SizeofType : Expr {
        public SizeofType(TypeName typeName) {
            this.TypeName = typeName;
        }
        public TypeName TypeName { get; }
        public static Expr Create(TypeName typeName) =>
            new SizeofType(typeName);

        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            //Tuple<AST.Env, AST.ExprType> type_env = this.TypeName.GetTypeEnv(env);
            //env = type_env.Item1;
            //AST.ExprType Type = type_env.Item2;

            var type = Semant(this.TypeName.GetExprType, ref env);

            return new ABT.ConstULong((UInt32)type.SizeOf, env);
        }
    }

    /// <summary>
    /// sizeof(Expr)
    /// </summary>
    [Checked]
    public sealed class SizeofExpr : UnaryExprOperator {
        public SizeofExpr(Expr expr)
            : base(expr) { }
        public static Expr Create(Expr expr) =>
            new SizeofExpr(expr);

        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            ABT.Expr expr = this.Expr.GetExpr(env, info);
            return new ABT.ConstULong((UInt32)expr.Type.SizeOf, env);
        }
    }

    /// <summary>
    /// Prefix increment: ++x
    /// </summary>
    [Checked]
    public sealed class PreIncrement : UnaryExprOperator {
        public PreIncrement(Expr expr)
            : base(expr) { }
        public static Expr Create(Expr expr) =>
            new PreIncrement(expr);

        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            ABT.Expr expr = this.Expr.GetExpr(env, info);

            if (!expr.Type.IsScalar) {
                throw new InvalidOperationException("Expected a scalar.").Attach(expr);
            }

            return new ABT.PreIncrement(expr);
        }
    }

    /// <summary>
    /// Prefix decrement: --x
    /// </summary>
    [Checked]
    public sealed class PreDecrement : UnaryExprOperator {
        public PreDecrement(Expr expr)
            : base(expr) { }
        public static Expr Create(Expr expr) =>
            new PreDecrement(expr);

        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            ABT.Expr expr = this.Expr.GetExpr(env, info);

            if (!expr.Type.IsScalar) {
                throw new InvalidOperationException("Expected a scalar.").Attach(expr);
            }

            return new ABT.PreDecrement(expr);
        }
    }

    /// <summary>
    /// Reference: &expr
    /// </summary>
    [Checked]
    public sealed class Reference : UnaryExprOperator {
        public Reference(Expr expr)
            : base(expr) { }
        public static Expr Create(Expr expr) =>
            new Reference(expr);

        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            ABT.Expr expr = this.Expr.GetExpr(env, info);
            return new ABT.Reference(expr);
        }
    }

    /// <summary>
    /// Dereference: *Expr
    /// 
    /// Note that Expr might have an **incomplete** Type.
    /// We need to search the environment
    /// </summary>
    [Checked]
    public sealed class Dereference : UnaryExprOperator {
        public Dereference(Expr expr)
            : base(expr) { }
        public static Expr Create(Expr expr) => new Dereference(expr);

        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            ABT.Expr expr = this.Expr.GetExpr(env, info);

            if (expr is ABT.ArrayIndexDeref) return new ABT.Dereference(expr, expr.Type);

            if (expr.Type.Kind != ABT.ExprTypeKind.POINTER) {
                throw new InvalidOperationException("Expected a pointer.").Attach(expr);
            }

            ABT.ExprType type = ((ABT.PointerType)expr.Type).RefType;
            if (type.Kind == ABT.ExprTypeKind.STRUCT_OR_UNION && !((ABT.StructOrUnionType)type).IsComplete) {
                throw new InvalidOperationException("Cannot dereference incomplete Type.").Attach(expr);
            }

            return new ABT.Dereference(expr, type);
        }
    }

    /// <summary>
    /// Merely a check on arithmetic Type.
    /// </summary>
    [Checked]
    public sealed class Positive : UnaryExprOperator {
        public Positive(Expr expr)
            : base(expr) { }
        public static Expr Create(Expr expr) =>
            new Positive(expr);
        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            ABT.Expr expr = this.Expr.GetExpr(env, info);

            if (!expr.Type.IsArith) {
                throw new InvalidOperationException("Expected arithmetic Type.").Attach(expr);
            }

            return expr;
        }
    }

    /// <summary>
    /// Negative: requires arithmetic Type.
    /// </summary>
    [Checked]
    public sealed class Negative : UnaryExprOperator {
        public Negative(Expr expr)
            : base(expr) { }
        public static Expr Create(Expr expr) =>
            new Negative(expr);
        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            ABT.Expr expr = this.Expr.GetExpr(env, info);

            if (!expr.Type.IsArith) {
                throw new InvalidOperationException("Expected arithmetic Type.").Attach(expr);
            }

            if (expr.Type.IsIntegral) {
                expr = ABT.TypeCast.IntegralPromotion(expr).Item1;
            }

            if (expr.IsConstExpr) {
                switch (expr.Type.Kind) {
                    case ABT.ExprTypeKind.LONG:
                        return new ABT.ConstLong(-((ABT.ConstLong)expr).Value, env);

                    case ABT.ExprTypeKind.ULONG:
                        return new ABT.ConstLong(-(Int32)((ABT.ConstULong)expr).Value, env);

                    case ABT.ExprTypeKind.S64:
                        return new ABT.ConstS64(-((ABT.ConstS64)expr).Value, env);

                    case ABT.ExprTypeKind.U64:
                        return new ABT.ConstS64(-(long)((ABT.ConstU64)expr).Value, env);

                    case ABT.ExprTypeKind.FLOAT:
                        return new ABT.ConstFloat(-((ABT.ConstFloat)expr).Value, env);

                    case ABT.ExprTypeKind.DOUBLE:
                        return new ABT.ConstDouble(-((ABT.ConstDouble)expr).Value, env);

                    default:
                        throw new InvalidOperationException();
                }
            }

            return new ABT.Negative(expr);
        }
    }

    /// <summary>
    /// Bitwise not: requires integral.
    /// </summary>
    [Checked]
    public sealed class BitwiseNot : UnaryExprOperator {
        public BitwiseNot(Expr expr)
            : base(expr) { }
        public static Expr Create(Expr expr) =>
            new BitwiseNot(expr);
        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            ABT.Expr expr = this.Expr.GetExpr(env, info);

            if (!expr.Type.IsIntegral) {
                throw new InvalidOperationException("Expected integral Type.").Attach(expr);
            }

            expr = ABT.TypeCast.IntegralPromotion(expr).Item1;

            if (expr.IsConstExpr) {
                switch (expr.Type.Kind) {
                    case ABT.ExprTypeKind.LONG:
                        return new ABT.ConstLong(~((ABT.ConstLong)expr).Value, env);
                    case ABT.ExprTypeKind.ULONG:
                        return new ABT.ConstULong(~((ABT.ConstULong)expr).Value, env);
                    case ABT.ExprTypeKind.S64:
                        return new ABT.ConstS64(~((ABT.ConstS64)expr).Value, env);
                    case ABT.ExprTypeKind.U64:
                        return new ABT.ConstU64(~((ABT.ConstU64)expr).Value, env);
                    default:
                        throw new InvalidOperationException();
                }
            }

            return new ABT.BitwiseNot(expr);
        }
    }

    /// <summary>
    /// Logical not
    /// </summary>
    [Checked]
    public sealed class LogicalNot : UnaryExprOperator {
        public LogicalNot(Expr expr)
            : base(expr) { }
        public static Expr Create(Expr expr) =>
            new LogicalNot(expr);

        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            var expr = this.Expr.GetExpr(env, info);

            if (!expr.Type.IsArith) {
                throw new InvalidOperationException("Expected arithmetic Type.").Attach(expr);
            }

            if (expr.Type.IsIntegral) {
                expr = ABT.TypeCast.IntegralPromotion(expr).Item1;
            }

            if (expr.IsConstExpr) {
                Boolean isZero;
                switch (expr.Type.Kind) {
                    case ABT.ExprTypeKind.LONG:
                        isZero = ((ABT.ConstLong)expr).Value == 0;
                        break;
                    case ABT.ExprTypeKind.ULONG:
                        isZero = ((ABT.ConstULong)expr).Value == 0;
                        break;
                    case ABT.ExprTypeKind.S64:
                        isZero = ((ABT.ConstS64)expr).Value == 0;
                        break;
                    case ABT.ExprTypeKind.U64:
                        isZero = ((ABT.ConstU64)expr).Value == 0;
                        break;
                    case ABT.ExprTypeKind.FLOAT:
                        isZero = ((ABT.ConstFloat)expr).Value == 0;
                        break;
                    case ABT.ExprTypeKind.DOUBLE:
                        isZero = ((ABT.ConstDouble)expr).Value == 0;
                        break;
                    default:
                        throw new InvalidOperationException();
                }
                return new ABT.ConstLong(Convert.ToInt32(isZero), env);
            }

            return new ABT.LogicalNot(expr);
        }
    }

    /// <summary>
    /// User-specified explicit Type cast
    /// </summary>
    [Checked]
    public sealed class TypeCast : Expr {
        public TypeCast(TypeName typeName, Expr expr) {
            this.TypeName = typeName;
            this.Expr = expr;
        }

        public TypeName TypeName { get; }
        public Expr Expr { get; }

        public static Expr Create(TypeName typeName, Expr expr) =>
            new TypeCast(typeName, expr);

        protected override ABT.Expr GetExprImpl(ABT.Env env, ILineInfo info)
        {
            ABT.ExprType type = Semant(this.TypeName.GetExprType, ref env);
            ABT.Expr expr = this.Expr.GetExpr(env, info);
            return ABT.TypeCast.MakeCast(expr, type);
        }
    }

}