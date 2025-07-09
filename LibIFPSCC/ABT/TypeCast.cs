using System;
using System.Diagnostics;
using CodeGeneration;

namespace ABT {
    public enum TypeCastType {
        NOP,
        INT8_TO_INT16,
        INT8_TO_INT32,

        INT16_TO_INT32,

        INT32_TO_FLOAT,
        INT32_TO_DOUBLE,

        PRESERVE_INT8,
        PRESERVE_INT16,

        UINT8_TO_UINT16,
        UINT8_TO_UINT32,

        UINT16_TO_UINT32,

        FLOAT_TO_INT32,
        FLOAT_TO_DOUBLE,

        DOUBLE_TO_INT32,
        DOUBLE_TO_FLOAT,

        INT32_TO_INT64,
        INT64_TO_INT32,

        PTR_TO_INT32,
        INT32_TO_PTR,
        PTR_TO_PTR,
        ARRAY_TO_PTR,
        FUNC_TO_PTR,

        COM_INTERFACE,
        VARIANT_TO_COM_INTERFACE,
        RUNTIME_IMPLEMENTED,
        COPY_CONSTRUCTOR
    }

    public sealed partial class TypeCast : Expr {
        private TypeCast(TypeCastType kind, Expr expr, ExprType type, Env env) {
            this.Expr = expr;
            this.Kind = kind;
            this.Type = type;
            this.Env = env;
        }

        public TypeCast(TypeCastType kind, Expr expr, ExprType type)
            : this(kind, expr, type, expr.Env) { }

        public readonly Expr Expr;
        public readonly TypeCastType Kind;

        // Note: typecast might introduce environment changes.
        public override Env Env { get; }

        // A typecast cannot be an lvalue.
        // int a;
        // (char)a = 'a'; // error: an lvalue is required.
        public override Boolean IsLValue => false;

        public override ExprType Type { get; }

        public static Boolean EqualType(ExprType t1, ExprType t2) {
            return t1.EqualType(t2);
        }

        /// <summary>
        /// From:
        ///     char, short, long
        /// To:
        ///     char, uchar, short, ushort, long, ulong, float double
        /// </summary>
        public static Expr SignedIntegralToArith(Expr expr, ExprType type) {
            ExprTypeKind from = expr.Type.Kind;
            ExprTypeKind to = type.Kind;

            Env env = expr.Env;

            switch (from) {
                case ExprTypeKind.CHAR:
                    switch (to) {
                        case ExprTypeKind.SHORT:
                        case ExprTypeKind.USHORT:
                            return new TypeCast(TypeCastType.INT8_TO_INT16, expr, type);

                        case ExprTypeKind.LONG:
                        case ExprTypeKind.ULONG:
                            return new TypeCast(TypeCastType.INT8_TO_INT32, expr, type);

                        case ExprTypeKind.S64:
                        case ExprTypeKind.U64:
                            return new TypeCast(TypeCastType.INT32_TO_INT64, new TypeCast(TypeCastType.INT8_TO_INT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.UCHAR:
                            return new TypeCast(TypeCastType.NOP, expr, type);

                        case ExprTypeKind.FLOAT:
                            // char -> long -> float
                            return new TypeCast(TypeCastType.INT32_TO_FLOAT, new TypeCast(TypeCastType.INT8_TO_INT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.DOUBLE:
                            // char -> long -> double
                            return new TypeCast(TypeCastType.INT32_TO_DOUBLE, new TypeCast(TypeCastType.INT8_TO_INT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.VOID:
                        case ExprTypeKind.POINTER:
                        case ExprTypeKind.FUNCTION:
                        case ExprTypeKind.ARRAY:
                        case ExprTypeKind.INCOMPLETE_ARRAY:
                        case ExprTypeKind.STRUCT_OR_UNION:
                        case ExprTypeKind.CHAR:
                        default:
                            throw new InvalidProgramException($"Cannot cast from {from} to {to}").Attach(expr);
                    }

                case ExprTypeKind.SHORT:
                    switch (to) {
                        case ExprTypeKind.CHAR:
                        case ExprTypeKind.UCHAR:
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.USHORT:
                            return new TypeCast(TypeCastType.NOP, expr, type);

                        case ExprTypeKind.LONG:
                        case ExprTypeKind.ULONG:
                            return new TypeCast(TypeCastType.INT16_TO_INT32, expr, type);

                        case ExprTypeKind.S64:
                        case ExprTypeKind.U64:
                            return new TypeCast(TypeCastType.INT32_TO_INT64, new TypeCast(TypeCastType.INT16_TO_INT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.FLOAT:
                            // short -> long -> float
                            return new TypeCast(TypeCastType.INT32_TO_FLOAT, new TypeCast(TypeCastType.INT16_TO_INT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.DOUBLE:
                            // short -> long -> double
                            return new TypeCast(TypeCastType.INT32_TO_DOUBLE, new TypeCast(TypeCastType.INT16_TO_INT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.VOID:
                        case ExprTypeKind.SHORT:
                        case ExprTypeKind.POINTER:
                        case ExprTypeKind.FUNCTION:
                        case ExprTypeKind.ARRAY:
                        case ExprTypeKind.INCOMPLETE_ARRAY:
                        case ExprTypeKind.STRUCT_OR_UNION:
                        default:
                            throw new InvalidProgramException($"Cannot cast from {from} to {to}").Attach(expr);
                    }

                case ExprTypeKind.LONG:
                    switch (to) {
                        case ExprTypeKind.CHAR:
                            if (expr.IsConstExpr) {
                                return new ConstChar((SByte)((ConstLong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.UCHAR:
                            if (expr.IsConstExpr) {
                                return new ConstUChar((Byte)((ConstLong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.SHORT:
                            if (expr.IsConstExpr) {
                                return new ConstShort((Int16)((ConstLong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, expr, type);

                        case ExprTypeKind.USHORT:
                            if (expr.IsConstExpr) {
                                return new ConstUShort((UInt16)((ConstLong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, expr, type);

                        case ExprTypeKind.ULONG:
                            if (expr.IsConstExpr) {
                                return new ConstULong((UInt32)((ConstLong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.NOP, expr, type);

                        case ExprTypeKind.S64:
                            if (expr.IsConstExpr)
                            {
                                return new ConstS64((UInt32)((ConstLong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_INT64, expr, type);
                        case ExprTypeKind.U64:

                            if (expr.IsConstExpr)
                            {
                                return new ConstU64((UInt32)((ConstLong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_INT64, expr, type);

                        case ExprTypeKind.FLOAT:
                            if (expr.IsConstExpr) {
                                return new ConstFloat(((ConstLong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_FLOAT, expr, type);

                        case ExprTypeKind.DOUBLE:
                            if (expr.IsConstExpr) {
                                return new ConstDouble(((ConstLong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_DOUBLE, expr, type);

                        case ExprTypeKind.VOID:
                        case ExprTypeKind.LONG:
                        case ExprTypeKind.POINTER:
                        case ExprTypeKind.FUNCTION:
                        case ExprTypeKind.ARRAY:
                        case ExprTypeKind.INCOMPLETE_ARRAY:
                        case ExprTypeKind.STRUCT_OR_UNION:
                        default:
                            throw new InvalidProgramException($"Cannot cast from {from} to {to}").Attach(expr);
                    }

                case ExprTypeKind.S64:
                    switch (to)
                    {
                        case ExprTypeKind.CHAR:
                            if (expr.IsConstExpr)
                            {
                                return new ConstChar((SByte)((ConstS64)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.UCHAR:
                            if (expr.IsConstExpr)
                            {
                                return new ConstUChar((Byte)((ConstS64)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.SHORT:
                            if (expr.IsConstExpr)
                            {
                                return new ConstShort((Int16)((ConstS64)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, expr, type);

                        case ExprTypeKind.USHORT:
                            if (expr.IsConstExpr)
                            {
                                return new ConstUShort((UInt16)((ConstS64)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, expr, type);

                        case ExprTypeKind.ULONG:
                            if (expr.IsConstExpr)
                            {
                                return new ConstULong((UInt32)((ConstS64)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.INT64_TO_INT32, expr, type);

                        case ExprTypeKind.U64:

                            if (expr.IsConstExpr)
                            {
                                return new ConstU64((UInt32)((ConstS64)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.NOP, expr, type);

                        case ExprTypeKind.FLOAT:
                            if (expr.IsConstExpr)
                            {
                                return new ConstFloat(((ConstS64)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_FLOAT, expr, type);

                        case ExprTypeKind.DOUBLE:
                            if (expr.IsConstExpr)
                            {
                                return new ConstDouble(((ConstS64)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_DOUBLE, expr, type);

                        case ExprTypeKind.VOID:
                        case ExprTypeKind.LONG:
                        case ExprTypeKind.POINTER:
                        case ExprTypeKind.FUNCTION:
                        case ExprTypeKind.ARRAY:
                        case ExprTypeKind.INCOMPLETE_ARRAY:
                        case ExprTypeKind.STRUCT_OR_UNION:
                        default:
                            throw new InvalidProgramException($"Cannot cast from {from} to {to}").Attach(expr);
                    }

                default:
                    throw new InvalidProgramException().Attach(expr);
            }
        }

        /// <summary>
        /// From:
        ///     uchar, ushort, ulong
        /// To:
        ///     char, uchar, short, ushort, long, ulong, float, double
        /// </summary>
        /// <remarks>
        /// Aaccording to MSDN "Conversions from Unsigned Integral Types",
        ///   unsigned long converts directly to double.
        /// However, I just treat unsigned long as long.
        /// </remarks>
        public static Expr UnsignedIntegralToArith(Expr expr, ExprType type) {
            ExprTypeKind from = expr.Type.Kind;
            ExprTypeKind to = type.Kind;

            Env env = expr.Env;

            switch (from) {
                case ExprTypeKind.UCHAR:
                    switch (to) {
                        case ExprTypeKind.CHAR:
                            return new TypeCast(TypeCastType.NOP, expr, type);

                        case ExprTypeKind.SHORT:
                        case ExprTypeKind.USHORT:
                            return new TypeCast(TypeCastType.UINT8_TO_UINT16, expr, type);

                        case ExprTypeKind.LONG:
                        case ExprTypeKind.ULONG:
                            return new TypeCast(TypeCastType.UINT8_TO_UINT32, expr, type);


                        case ExprTypeKind.S64:
                        case ExprTypeKind.U64:
                            return new TypeCast(TypeCastType.INT32_TO_INT64, new TypeCast(TypeCastType.UINT8_TO_UINT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.FLOAT:
                            // uchar -> ulong -> long -> float
                            return new TypeCast(TypeCastType.INT32_TO_FLOAT, new TypeCast(TypeCastType.UINT8_TO_UINT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.DOUBLE:
                            // uchar -> ulong -> long -> double
                            return new TypeCast(TypeCastType.INT32_TO_DOUBLE, new TypeCast(TypeCastType.UINT8_TO_UINT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        default:
                            throw new InvalidProgramException($"Cannot cast from {from} to {to}").Attach(expr);
                    }

                case ExprTypeKind.USHORT:
                    switch (to) {
                        case ExprTypeKind.CHAR:
                        case ExprTypeKind.UCHAR:
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.USHORT:
                            return new TypeCast(TypeCastType.NOP, expr, type);

                        case ExprTypeKind.LONG:
                        case ExprTypeKind.ULONG:
                            return new TypeCast(TypeCastType.UINT16_TO_UINT32, expr, type);

                        case ExprTypeKind.S64:
                        case ExprTypeKind.U64:
                            return new TypeCast(TypeCastType.INT32_TO_INT64, new TypeCast(TypeCastType.UINT8_TO_UINT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.FLOAT:
                            // ushort -> ulong -> long -> float
                            return new TypeCast(TypeCastType.INT32_TO_FLOAT, new TypeCast(TypeCastType.UINT16_TO_UINT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.DOUBLE:
                            // ushort -> ulong -> long -> double
                            return new TypeCast(TypeCastType.INT32_TO_DOUBLE, new TypeCast(TypeCastType.UINT16_TO_UINT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        default:
                            throw new InvalidProgramException($"Cannot cast from {from} to {to}").Attach(expr);
                    }

                case ExprTypeKind.ULONG:
                    switch (to) {
                        case ExprTypeKind.CHAR:
                            if (expr.IsConstExpr) {
                                return new ConstLong((SByte)((ConstULong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.UCHAR:
                            if (expr.IsConstExpr) {
                                return new ConstULong((Byte)((ConstULong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.SHORT:
                            if (expr.IsConstExpr) {
                                return new ConstLong((Int16)((ConstULong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, expr, type);

                        case ExprTypeKind.USHORT:
                            if (expr.IsConstExpr) {
                                return new ConstULong((UInt16)((ConstULong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, expr, type);

                        case ExprTypeKind.LONG:
                            if (expr.IsConstExpr) {
                                return new ConstLong((Int32)((ConstULong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.NOP, expr, type);


                        case ExprTypeKind.ULONG:
                            if (expr.IsConstExpr)
                                return expr;
                            return new TypeCast(TypeCastType.NOP, expr, type);

                        case ExprTypeKind.S64:
                            if (expr.IsConstExpr)
                            {
                                return new ConstS64((UInt32)((ConstULong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_INT64, expr, type);
                        case ExprTypeKind.U64:

                            if (expr.IsConstExpr)
                            {
                                return new ConstU64((UInt32)((ConstULong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_INT64, expr, type);

                        case ExprTypeKind.FLOAT:
                            if (expr.IsConstExpr) {
                                return new ConstFloat(((ConstULong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_FLOAT, expr, type);

                        case ExprTypeKind.DOUBLE:
                            if (expr.IsConstExpr) {
                                return new ConstDouble(((ConstULong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_DOUBLE, expr, type);

                        default:
                            throw new InvalidProgramException($"Cannot cast from {from} to {to}").Attach(expr);
                    }

                case ExprTypeKind.U64:
                    switch (to)
                    {
                        case ExprTypeKind.CHAR:
                            if (expr.IsConstExpr)
                            {
                                return new ConstLong((SByte)((ConstU64)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.UCHAR:
                            if (expr.IsConstExpr)
                            {
                                return new ConstULong((Byte)((ConstU64)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.SHORT:
                            if (expr.IsConstExpr)
                            {
                                return new ConstLong((Int16)((ConstU64)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, expr, type);

                        case ExprTypeKind.USHORT:
                            if (expr.IsConstExpr)
                            {
                                return new ConstULong((UInt16)((ConstU64)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, expr, type);

                        case ExprTypeKind.LONG:
                            if (expr.IsConstExpr)
                            {
                                return new ConstLong((Int32)((ConstU64)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_INT64, expr, type);

                        case ExprTypeKind.S64:
                            if (expr.IsConstExpr)
                            {
                                return new ConstS64((UInt32)((ConstU64)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.NOP, expr, type);

                        case ExprTypeKind.FLOAT:
                            if (expr.IsConstExpr)
                            {
                                return new ConstFloat(((ConstULong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_FLOAT, expr, type);

                        case ExprTypeKind.DOUBLE:
                            if (expr.IsConstExpr)
                            {
                                return new ConstDouble(((ConstULong)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_DOUBLE, expr, type);

                        default:
                            throw new InvalidProgramException($"Cannot cast from {from} to {to}").Attach(expr);
                    }


                default:
                    throw new InvalidProgramException($"Cannot cast from {from} to {to}").Attach(expr);
            }
        }

        /// <summary>
        /// From:
        ///     float, double
        /// To:
        ///     char, uchar, short, ushort, long, ulong, float, double
        /// </summary>
        /// <remarks>
        /// According to MSDN "Conversions from Floating-Point Types",
        ///   float cannot convert to unsigned char.
        /// I don't know why, but I follow it.
        /// </remarks>
        public static Expr FloatToArith(Expr expr, ExprType type) {

            ExprTypeKind from = expr.Type.Kind;
            ExprTypeKind to = type.Kind;
            Env env = expr.Env;

            switch (from) {
                case ExprTypeKind.FLOAT:
                    switch (to) {
                        case ExprTypeKind.CHAR:
                            if (expr.IsConstExpr) {
                                return new ConstLong((SByte)((ConstFloat)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT8, new TypeCast(TypeCastType.FLOAT_TO_INT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.SHORT:
                            if (expr.IsConstExpr) {
                                return new ConstLong((Int16)((ConstFloat)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, new TypeCast(TypeCastType.FLOAT_TO_INT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.USHORT:
                            if (expr.IsConstExpr) {
                                return new ConstULong((UInt16)((ConstFloat)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, new TypeCast(TypeCastType.FLOAT_TO_INT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.LONG:
                            if (expr.IsConstExpr) {
                                return new ConstLong((Int32)((ConstFloat)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.FLOAT_TO_INT32, expr, type);

                        case ExprTypeKind.ULONG:
                            if (expr.IsConstExpr) {
                                return new ConstULong((UInt32)((ConstFloat)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.FLOAT_TO_INT32, expr, type);

                        case ExprTypeKind.S64:
                            if (expr.IsConstExpr)
                            {
                                return new ConstS64((Int32)((ConstFloat)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.FLOAT_TO_INT32, expr, type);

                        case ExprTypeKind.U64:
                            if (expr.IsConstExpr)
                            {
                                return new ConstU64((UInt32)((ConstFloat)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.FLOAT_TO_INT32, expr, type);

                        case ExprTypeKind.DOUBLE:
                            if (expr.IsConstExpr) {
                                return new ConstDouble(((ConstFloat)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.FLOAT_TO_DOUBLE, expr, type);

                        default:
                            throw new InvalidProgramException($"Cannot cast from {from} to {to}").Attach(expr);
                    }

                case ExprTypeKind.DOUBLE:
                    switch (to) {
                        case ExprTypeKind.CHAR:
                            // double -> float -> char
                            if (expr.IsConstExpr) {
                                return new ConstLong((SByte)((ConstDouble)expr).Value, env);
                            }
                            return FloatToArith(FloatToArith(expr, new FloatType(type.IsConst, type.IsVolatile)), new CharType(type.IsConst, type.IsVolatile));

                        case ExprTypeKind.SHORT:
                            // double -> float -> short
                            if (expr.IsConstExpr) {
                                return new ConstLong((Int16)((ConstDouble)expr).Value, env);
                            }
                            return FloatToArith(FloatToArith(expr, new FloatType(type.IsConst, type.IsVolatile)), new ShortType(type.IsConst, type.IsVolatile));

                        case ExprTypeKind.LONG:
                            // double -> float -> short
                            if (expr.IsConstExpr) {
                                return new ConstLong((Int32)((ConstDouble)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.DOUBLE_TO_INT32, expr, type);

                        case ExprTypeKind.ULONG:
                            if (expr.IsConstExpr) {
                                return new ConstULong((UInt32)((ConstDouble)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.DOUBLE_TO_INT32, expr, type);

                        case ExprTypeKind.S64:
                            // double -> float -> short
                            if (expr.IsConstExpr)
                            {
                                return new ConstS64((Int32)((ConstDouble)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.DOUBLE_TO_INT32, expr, type);

                        case ExprTypeKind.U64:
                            if (expr.IsConstExpr)
                            {
                                return new ConstU64((UInt32)((ConstDouble)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.DOUBLE_TO_INT32, expr, type);

                        case ExprTypeKind.USHORT:
                            // double -> long -> ushort
                            if (expr.IsConstExpr) {
                                return new ConstULong((UInt16)((ConstDouble)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, new TypeCast(TypeCastType.DOUBLE_TO_INT32, expr, new LongType(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.FLOAT:
                            if (expr.IsConstExpr) {
                                return new ConstFloat((Single)((ConstDouble)expr).Value, env);
                            }
                            return new TypeCast(TypeCastType.DOUBLE_TO_FLOAT, expr, type);

                        default:
                            throw new InvalidProgramException($"Cannot cast from {from} to {to}").Attach(expr);
                    }

                default:
                    throw new InvalidProgramException($"Cannot cast from {from} to {to}").Attach(expr);
            }
        }

        /// <summary>
        /// From:
        ///     pointer
        /// To:
        ///     pointer, integral
        /// </summary>
        public static Expr FromPointer(Expr expr, ExprType type, Env env) {
            ExprTypeKind from = expr.Type.Kind;
            ExprTypeKind to = type.Kind;

            if (from != ExprTypeKind.POINTER) {
                throw new InvalidOperationException("Expected a pointer.").Attach(expr);
            }

            bool fromVoid = !((PointerType)expr.Type).IsRef;

            // if we are casting to another pointer, do a nop
            if (to == ExprTypeKind.POINTER) {
                if (expr.IsConstExpr) {
                    return new ConstPtr(((ConstPtr)expr).Value, type, env);
                }
                bool toVoid = !((PointerType)type).IsRef;
                if (fromVoid && toVoid)
                    return new TypeCast(TypeCastType.NOP, expr, new ULongType(type.IsConst, type.IsVolatile), env);

                if (fromVoid)
                    return new TypeCast(TypeCastType.INT32_TO_PTR, expr, type, env);
                if (toVoid)
                    return new TypeCast(TypeCastType.PTR_TO_INT32, expr, new ULongType(type.IsConst, type.IsVolatile), env);
                return new TypeCast(TypeCastType.PTR_TO_PTR, expr, type, env);
            }

            // if we are casting to an integral
            if (type.IsIntegral) {
                // pointer -> ulong -> whatever integral
                if (expr.IsConstExpr) {
                    expr = new ConstULong(((ConstPtr)expr).Value, env);
                } else {
                    if (!((PointerType)expr.Type).IsRef)
                        expr = new TypeCast(TypeCastType.NOP, expr, new ULongType(type.IsConst, type.IsVolatile), env);
                    else 
                        expr = new TypeCast(TypeCastType.PTR_TO_INT32, expr, new ULongType(type.IsConst, type.IsVolatile), env);
                }
                return MakeCast(expr, type, env);
            }

            throw new InvalidOperationException("Casting from a pointer to an unsupported Type.").Attach(expr);
        }

        /// <summary>
        /// From:
        ///     pointer, integral
        /// To:
        ///     pointer
        /// </summary>
        public static Expr ToPointer(Expr expr, ExprType type, Env env) {
            ExprTypeKind from = expr.Type.Kind;
            ExprTypeKind to = type.Kind;

            if (to != ExprTypeKind.POINTER) {
                throw new InvalidOperationException("Error: expected casting to pointer.").Attach(expr);
            }

            var toPtr = (PointerType)type;
            if (toPtr.IsForOpenArray)
            {
                // This is for an open array, so cpval will be used here.
                // In theory everything is allowed here, but function pointer isn't actually a pointer, so:
                if (expr.Type is FunctionType)
                {
                    throw new InvalidOperationException("Function pointer cannot be used in OpenArrayOfConst").Attach(expr);
                }
                return new TypeCast(TypeCastType.COPY_CONSTRUCTOR, expr, type, env);
            }


            bool toVoid = !toPtr.IsRef;

            if (from == ExprTypeKind.POINTER) {
                if (expr.IsConstExpr) {
                    return new ConstPtr(((ConstPtr)expr).Value, type, env);
                }
                bool fromVoid = !((PointerType)expr.Type).IsRef;
                if (fromVoid && toVoid)
                    return new TypeCast(TypeCastType.NOP, expr, new ULongType(type.IsConst, type.IsVolatile), env);
                if (fromVoid)
                    return new TypeCast(TypeCastType.INT32_TO_PTR, expr, type, env);
                if (toVoid)
                    return new TypeCast(TypeCastType.PTR_TO_INT32, expr, new ULongType(type.IsConst, type.IsVolatile), env);
                return new TypeCast(TypeCastType.PTR_TO_PTR, expr, type, env);
            }

            if (expr.Type.IsIntegral) {
                // if we are casting from an integral

                // whatever integral -> ulong
                switch (expr.Type.Kind) {
                    case ExprTypeKind.CHAR:
                    case ExprTypeKind.SHORT:
                    case ExprTypeKind.LONG:
                        expr = SignedIntegralToArith(expr, new ULongType(type.IsConst, type.IsVolatile));
                        break;
                    case ExprTypeKind.UCHAR:
                    case ExprTypeKind.USHORT:
                    case ExprTypeKind.ULONG:
                        expr = UnsignedIntegralToArith(expr, new ULongType(type.IsConst, type.IsVolatile));
                        break;
                    default:
                        break;
                }

                // ulong -> pointer
                if (expr.IsConstExpr) {
                    return new ConstPtr(((ConstULong)expr).Value, type, env);
                }
                if (!((PointerType)type).IsRef)
                    return new TypeCast(TypeCastType.NOP, expr, type, env);
                return new TypeCast(TypeCastType.INT32_TO_PTR, expr, type, env);
            }
            if (expr.Type is FunctionType) {
                if (!expr.Type.EqualType(((PointerType)type).RefType)) {
                    throw new InvalidOperationException("Casting from an incompatible function.").Attach(expr);
                }

                // TODO: only allow compatible Type?
                return new TypeCast(TypeCastType.FUNC_TO_PTR, expr, type, env);

            }
            if (expr.Type is ArrayType) {

                // TODO: allow any pointer Type to cast to?
                if (!toPtr.IsRef) 
                    return new TypeCast(TypeCastType.PTR_TO_INT32, expr, type, env);

                return new TypeCast(TypeCastType.ARRAY_TO_PTR, expr, type, env);
            }

            throw new InvalidOperationException("Error: casting from an unsupported Type to pointer.").Attach(expr);
        }

        // MakeCast
        // ========
        // input: Expr, Type
        // output: TypeCast
        // converts Expr to Type
        // 
        public static Expr MakeCast(Expr expr, ExprType type, Env env) {

            // for an init list, if this is the first cast (will always happen instantly for an rvalue), coerce it to the "correct" type and return
            var initList = expr as ExprInitList;
            if (initList != null && initList.Type == null)
            {
                initList.CoerceType(type);
                return expr;
            }

            // if two types are equal, return Expr
            if (EqualType(expr.Type, type)) {
                return expr;
            }

            // from pointer
            if (expr.Type.Kind == ExprTypeKind.POINTER) {
                return FromPointer(expr, type, env);
            }

            // to pointer
            if (type.Kind == ExprTypeKind.POINTER) {
                return ToPointer(expr, type, env);
            }

            // if VARIANT, then we can basically just do a cast to anything and runtime will error if needed
            if (expr.Type.Kind == ExprTypeKind.COM_VARIANT || type.Kind == ExprTypeKind.COM_VARIANT)
            {
                // if one side is interface, then we might need to do a cast.
                // cast from variant to idispatch-implemented type?
                if (CGenState.TypeImplementsIDispatch(type))
                {
                    var castToDispatch = new TypeCast(TypeCastType.RUNTIME_IMPLEMENTED, expr, CGenState.ExprTypeIDispatch, env);
                    return new TypeCast(TypeCastType.COM_INTERFACE, castToDispatch, type, env);
                }
                // cast from idispatch-implemented type to variant?
                if (CGenState.TypeImplementsIDispatch(expr.Type))
                {
                    expr = new TypeCast(TypeCastType.COM_INTERFACE, expr, CGenState.ExprTypeIDispatch, env);
                }
                // runtime doesn't implement from variant to interface not IDispatch for some reason

                if (expr.Type.Kind == ExprTypeKind.COM_VARIANT && type.Kind == ExprTypeKind.COM_INTERFACE)
                {
                    var castToUnknown = new TypeCast(TypeCastType.VARIANT_TO_COM_INTERFACE, expr, CGenState.ExprTypeIDispatch, env);
                    return new TypeCast(TypeCastType.COM_INTERFACE, castToUnknown, type, env);
                }
                return new TypeCast(TypeCastType.RUNTIME_IMPLEMENTED, expr, type, env);
            }

            // if both types are interface, then the runtime function needs to be called for this, which turns into QueryInterface
            if (expr.Type.Kind == ExprTypeKind.COM_INTERFACE && type.Kind == ExprTypeKind.COM_INTERFACE)
            {
                return new TypeCast(TypeCastType.COM_INTERFACE, expr, type, env);
            }

            switch (expr.Type.Kind) {
                // from signed integral
                case ExprTypeKind.CHAR:
                case ExprTypeKind.SHORT:
                case ExprTypeKind.LONG:
                case ExprTypeKind.S64:
                    return SignedIntegralToArith(expr, type);

                // from unsigned integral
                case ExprTypeKind.UCHAR:
                case ExprTypeKind.USHORT:
                case ExprTypeKind.ULONG:
                case ExprTypeKind.U64:
                    return UnsignedIntegralToArith(expr, type);

                // from float
                case ExprTypeKind.FLOAT:
                case ExprTypeKind.DOUBLE:
                    return FloatToArith(expr, type);

                case ExprTypeKind.VOID:
                case ExprTypeKind.POINTER:
                case ExprTypeKind.FUNCTION:
                case ExprTypeKind.ARRAY:
                case ExprTypeKind.INCOMPLETE_ARRAY:
                case ExprTypeKind.STRUCT_OR_UNION:
                default:
                    throw new InvalidOperationException("Error: expression Type not supported for casting from.").Attach(expr);
            }

        }

        public static Expr MakeCast(Expr expr, ExprType type) {
            var ret = MakeCast(expr, type, expr.Env);
            ret.Copy(expr);
            return ret;
        }

        /// <summary>
        /// <para>Perform the usual arithmetic conversion on two expressions.</para>
        /// <para>If either expression is <see cref="DoubleType"/>, then both are converted to <see cref="DoubleType"/>.</para>
        /// <para>Else, if either expression is <see cref="FloatType"/>, then both are converted to <see cref="FloatType"/>.</para>
        /// <para>Else, if either expression is <see cref="ULongType"/>, then both are converted to <see cref="ULongType"/>.</para>
        /// <para>Else, both are converted to <see cref="LongType"/>.</para>
        /// </summary>
        /// <param name="e1">The first expression to be casted. Must have <see cref="ArithmeticType"/>.</param>
        /// <param name="e2">The second expression to be casted. Must have <see cref="ArithmeticType"/>.</param>
        /// <returns>
        /// The two casted expressions, and an <see cref="ExprTypeKind"/>.
        /// </returns>
        public static Tuple<Expr, Expr, ExprTypeKind> UsualArithmeticConversion(Expr e1, Expr e2) {
            ExprType t1 = e1.Type;
            ExprType t2 = e2.Type;

            Boolean c1 = t1.IsConst;
            Boolean v1 = t1.IsVolatile;
            Boolean c2 = t2.IsConst;
            Boolean v2 = t2.IsVolatile;

            // ifps: if either Expr is unicodestring or ansistring, the other must be the same
            if (t1.Kind == ExprTypeKind.ANSI_STRING || t1.Kind == ExprTypeKind.UNICODE_STRING)
            {
                if (t1.Kind != t2.Kind) throw new InvalidOperationException("strings must be of same type");
                return new Tuple<Expr, Expr, ExprTypeKind>(e1, e2, t1.Kind);
            }

            // 1. if either Expr is double: both are converted to double
            if (t1.Kind == ExprTypeKind.DOUBLE || t2.Kind == ExprTypeKind.DOUBLE) {
                return new Tuple<Expr, Expr, ExprTypeKind>(MakeCast(e1, new DoubleType(c1, v1)), MakeCast(e2, new DoubleType(c2, v2)), ExprTypeKind.DOUBLE);
            }

            // 2. if either Expr is float: both are converted to float
            if (t1.Kind == ExprTypeKind.FLOAT || t2.Kind == ExprTypeKind.FLOAT) {
                return new Tuple<Expr, Expr, ExprTypeKind>(MakeCast(e1, new FloatType(c1, v1)), MakeCast(e2, new FloatType(c2, v2)), ExprTypeKind.FLOAT);
            }

            // 3. if either Expr is unsigned long: both are converted to unsigned long
            if (t1.Kind == ExprTypeKind.ULONG || t2.Kind == ExprTypeKind.ULONG) {
                return new Tuple<Expr, Expr, ExprTypeKind>(MakeCast(e1, new ULongType(c1, v1)), MakeCast(e2, new ULongType(c2, v2)), ExprTypeKind.ULONG);
            }

            // if either Expr is u64, convert both to u64
            if (t1.Kind == ExprTypeKind.U64 || t2.Kind == ExprTypeKind.U64)
            {
                return new Tuple<Expr, Expr, ExprTypeKind>(MakeCast(e1, new U64Type(c1, v1)), MakeCast(e2, new U64Type(c2, v2)), ExprTypeKind.U64);
            }
            // if either Expr is s64, convert both to s64
            if (t1.Kind == ExprTypeKind.S64 || t2.Kind == ExprTypeKind.S64)
            {
                return new Tuple<Expr, Expr, ExprTypeKind>(MakeCast(e1, new S64Type(c1, v1)), MakeCast(e2, new S64Type(c2, v2)), ExprTypeKind.S64);
            }

            // 4. both are converted to long
            return new Tuple<Expr, Expr, ExprTypeKind>(MakeCast(e1, new LongType(c1, v1)), MakeCast(e2, new LongType(c2, v2)), ExprTypeKind.LONG);

        }

        /// <summary>
        /// First, convert pointers to <see cref="ULongType"/>'s, then do <see cref="UsualArithmeticConversion"/>.
        /// </summary>
        /// <param name="e1">
        /// The first expression to be casted.
        /// </param>
        /// <param name="e2">
        /// The second expression to be casted.
        /// </param>
        /// <returns>
        /// The two converted expressions and an <see cref="ExprTypeKind"/>.
        /// Possible return types: <see cref="DoubleType"/>, <see cref="FloatType"/>, <see cref="ULongType"/>, <see cref="LongType"/>.
        /// </returns>
        public static Tuple<Expr, Expr, ExprTypeKind> UsualScalarConversion(Expr e1, Expr e2) {
            if (e1.Type.Kind == ExprTypeKind.POINTER) {
                e1 = FromPointer(e1, new ULongType(e1.Type.IsConst, e1.Type.IsVolatile), e2.Env);
            }
            if (e2.Type.Kind == ExprTypeKind.POINTER) {
                e2 = FromPointer(e2, new ULongType(e2.Type.IsConst, e2.Type.IsVolatile), e2.Env);
            }
            return UsualArithmeticConversion(e1, e2);
        }

        /// <summary>
        /// All integrals are converted to <see cref="LongType"/> or <see cref="ULongType"/>.
        /// </summary>
        /// <param name="expr">
        /// The integral expression to be casted.
        /// </param>
        /// <returns>
        /// The casted expression and an <see cref="ExprTypeKind"/>.
        /// Possible return types: <see cref="LongType"/>, <see cref="ULongType"/>.
        /// </returns>
        public static Tuple<Expr, ExprTypeKind> IntegralPromotion(Expr expr) {
            if (!expr.Type.IsIntegral) {
                throw new InvalidProgramException().Attach(expr);
            }

            switch (expr.Type.Kind) {
                case ExprTypeKind.CHAR:
                case ExprTypeKind.SHORT:
                case ExprTypeKind.LONG:
                    return Tuple.Create(MakeCast(expr, new LongType(expr.Type.IsConst, expr.Type.IsVolatile)), ExprTypeKind.LONG);

                case ExprTypeKind.UCHAR:
                case ExprTypeKind.USHORT:
                case ExprTypeKind.ULONG:
                    return Tuple.Create(MakeCast(expr, new ULongType(expr.Type.IsConst, expr.Type.IsVolatile)), ExprTypeKind.ULONG);

                case ExprTypeKind.S64:
                    return Tuple.Create(MakeCast(expr, new S64Type(expr.Type.IsConst, expr.Type.IsVolatile)), ExprTypeKind.S64);
                case ExprTypeKind.U64:
                    return Tuple.Create(MakeCast(expr, new U64Type(expr.Type.IsConst, expr.Type.IsVolatile)), ExprTypeKind.U64);

                default:
                    throw new InvalidProgramException().Attach(expr);
            }
        }

    }
}