using System;
using System.Diagnostics;
using System.Resources;
using AST;
using CodeGeneration;
using IFPSLib.Emit;

namespace ABT {
    public abstract partial class BinaryOp {
        public override sealed Operand CGenAddress(CGenState state, Operand retLoc)
        {
            throw new InvalidOperationException("Cannot get the address of a binary operator.").Attach(this);
        }

        protected Operand CGenPrepare(CGenState state, Operand retLoc)
        {
            var ptr = Type as PointerType;
            Operand op = null;
            if (ptr != null && !ptr.IsRef)
            {
                // This is really an array of pointer. assign VarArrOfPtr, Arr[0] is incorrect.
                // Instead do the following:
                op = Left.CGenValue(state, retLoc);
                return state.FunctionState.PushVar(op);
            }
            op = retLoc != null ? retLoc : state.FunctionState.PushType(state.EmitType(Type));
            var leftOp = Left.CGenValue(state, retLoc);
            if (OperandEquator.Equals(leftOp, retLoc)) return op;
            state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, op, leftOp));
            return op;
        }

        public override bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false)
        {
            if (Type.Kind == ExprTypeKind.POINTER && !(Type as PointerType).IsRef) return false;
            return !retLocKnown;
        }
    }

    public abstract partial class BinaryOpSupportingIntegralOperands {
        /// <summary>
        /// Before calling this method, %eax = Left, %ebx = Right
        /// This method should let %eax = %eax op %ebx
        /// </summary>
        public abstract void OperateLong(CGenState state, Operand dest);

        /// <summary>
        /// Before calling this method, %eax = Left, %ebx = Right
        /// This method should let %eax = %eax op %ebx
        /// </summary>
        public abstract void OperateULong(CGenState state, Operand dest);

        public virtual void OperateString(CGenState state, Operand dest)
        {
            throw new InvalidOperationException("Does not support string").Attach(this);
        }

        private Operand CGenLong(CGenState state, Operand retLoc)
        {
            var dest = CGenPrepare(state, retLoc);
            OperateLong(state, dest);
            return dest;
        }

        private Operand CGenULong(CGenState state, Operand retLoc)
        {
            var dest = CGenPrepare(state, retLoc);
            OperateULong(state, dest);
            return dest;
        }

        /// <summary>
        /// 1. %eax = left, %ebx = right, stack unchanged
        /// 2. Operate{Long, ULong}
        /// </summary>
        protected Operand CGenIntegral(CGenState state, Operand retLoc)
        {
            // %eax = left, %ebx = right, stack unchanged
            var dest = CGenPrepare(state, retLoc);

            if (this.Type is LongType || this.Type is ComVariantType) {
                // %eax = left op right, stack unchanged
                OperateLong(state, dest);
            } else if (this.Type is ULongType) {
                // %eax = left op right, stack unchanged
                OperateULong(state, dest);
            } else if (this.Type is UnicodeStringType || this.Type is AnsiStringType)
            {
                OperateString(state, dest);
            } else {
                throw new InvalidOperationException().Attach(this);
            }
            return dest;
        }
    }

    public abstract partial class BinaryOpSupportingOnlyIntegralOperands {
        public override sealed Operand CGenValue(CGenState state, Operand retLoc)
        {
            return CGenIntegral(state, retLoc);
        }
    }

    public abstract partial class BinaryOpSupportingArithmeticOperands {
        /// <summary>
        /// Before: %st(0) = left, %st(1) = right, stack unchanged.
        /// After: 'left op right' stored in the correct register.
        /// </summary>
        public abstract void OperateFloat(CGenState state, Operand dest);

        /// <summary>
        /// Before: %st(0) = left, %st(1) = right, stack unchanged.
        /// After: 'left op right' stored in the correct register.
        /// </summary>
        public abstract void OperateDouble(CGenState state, Operand dest);

        /// <summary>
        /// 1. %st(0) = left, %st(1) = right, stack unchanged
        /// 2. OperateDouble
        /// </summary>
        public Operand CGenFloat(CGenState state, Operand retLoc)
        {
            var dest = CGenPrepare(state, retLoc);
            OperateFloat(state, dest);
            return dest;
        }

        /// <summary>
        /// 1. %st(0) = left, %st(1) = right, stack unchanged
        /// 2. OperateDouble
        /// </summary>
        public Operand CGenDouble(CGenState state, Operand retLoc)
        {
            var dest = CGenPrepare(state, retLoc);
            OperateDouble(state, dest);
            return dest;
        }

        /// <summary>
        /// 1. %st(0) = left, %st(1) = right, stack unchanged
        /// 2. Operate{Float, Double}
        /// </summary>
        public Operand CGenArithmetic(CGenState state, Operand retLoc)
        {
            if (this.Type is FloatType) {
                return CGenFloat(state, retLoc);
            } else if (this.Type is DoubleType) {
                return CGenDouble(state, retLoc);
            } else {
                return CGenIntegral(state, retLoc);
            }
        }
    }

    public abstract partial class BinaryArithmeticOp {
        public override sealed Operand CGenValue(CGenState state, Operand retLoc)
        {
            return CGenArithmetic(state, retLoc);
        }
    }

    public abstract partial class BinaryComparisonOp {
        public abstract void SetLong(CGenState state, Operand dest);

        public abstract void SetULong(CGenState state, Operand dest);

        public abstract void SetFloat(CGenState state, Operand dest);

        public abstract void SetDouble(CGenState state, Operand dest);

        public override sealed void OperateLong(CGenState state, Operand dest) {
            SetLong(state, dest);
        }

        /// <summary>
        /// <para>Before: %eax = left, %ebx = right, stack unchanged.</para>
        /// <para>After: with SetULong, %eax = left op right, stack unchanged.</para>
        /// </summary>
        public override sealed void OperateULong(CGenState state, Operand dest) {
            SetULong(state, dest);
        }

        /// <summary>
        /// <para>Before: %st(0) = left, %st(1) = right, stack unchanged.</para>
        /// <para>After: with SetFloat, %eax = left op right, stack unchanged.</para>
        /// </summary>
        public override sealed void OperateFloat(CGenState state, Operand dest) {
            SetFloat(state, dest);
        }

        /// <summary>
        /// Before: %st(0) = left, %st(1) = right, stack unchanged.
        /// After: with SetDouble, %eax = left op right, stack unchanged.
        /// </summary>
        public override sealed void OperateDouble(CGenState state, Operand dest) {
            SetDouble(state, dest);
        }

        public override sealed Operand CGenValue(CGenState state, Operand retLoc)
        {
            return CGenArithmetic(state, retLoc);
        }
    }

    public sealed partial class Multiply {
        public override void OperateLong(CGenState state, Operand dest) {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Mul, dest, Right.CGenValue(state, null)));
        }

        public override void OperateULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Mul, dest, Right.CGenValue(state, null)));
        }

        public override void OperateFloat(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Mul, dest, Right.CGenValue(state, null)));
        }

        public override void OperateDouble(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Mul, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class Divide {
        public override void OperateLong(CGenState state, Operand dest) {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Div, dest, Right.CGenValue(state, null)));
        }

        public override void OperateULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Div, dest, Right.CGenValue(state, null)));
        }

        public override void OperateFloat(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Div, dest, Right.CGenValue(state, null)));
        }

        public override void OperateDouble(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Div, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class Modulo {
        public override void OperateLong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Mod, dest, Right.CGenValue(state, null)));
        }

        public override void OperateULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Mod, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class Xor {
        public override void OperateLong(CGenState state, Operand dest) {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Xor, dest, Right.CGenValue(state, null)));
        }

        public override void OperateULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Xor, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class BitwiseOr {
        public override void OperateLong(CGenState state, Operand dest) {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Or, dest, Right.CGenValue(state, null)));
        }

        public override void OperateULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Or, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class BitwiseAnd {
        public override void OperateLong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.And, dest, Right.CGenValue(state, null)));
        }

        public override void OperateULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.And, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class LShift {
        public override void OperateLong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Shl, dest, Right.CGenValue(state, null)));
        }

        public override void OperateULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Shl, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class RShift
    {
        public override void OperateLong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Shr, dest, Right.CGenValue(state, null)));
        }

        public override void OperateULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Shr, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class Add {
        public override void OperateLong(CGenState state, Operand dest) {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Add, dest, Right.CGenValue(state, null)));
        }

        public override void OperateULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Add, dest, Right.CGenValue(state, null)));
        }

        public override void OperateFloat(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Add, dest, Right.CGenValue(state, null)));
        }

        public override void OperateDouble(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Add, dest, Right.CGenValue(state, null)));
        }

        public override void OperateString(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Add, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class Sub {
        public override void OperateLong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Sub, dest, Right.CGenValue(state, null)));
        }

        public override void OperateULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Sub, dest, Right.CGenValue(state, null)));
        }

        public override void OperateFloat(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Sub, dest, Right.CGenValue(state, null)));
        }

        public override void OperateDouble(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Sub, dest, Right.CGenValue(state, null)));
        }
    }



    public sealed partial class GEqual {
        public override void SetLong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Ge, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Ge, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetFloat(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Ge, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetDouble(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Ge, dest, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class Greater {
        public override void SetLong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Gt, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Gt, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetFloat(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Gt, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetDouble(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Gt, dest, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class LEqual {
        public override void SetLong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Le, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Le, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetFloat(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Le, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetDouble(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Le, dest, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class Less {
        public override void SetLong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Lt, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Lt, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetFloat(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Lt, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetDouble(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Lt, dest, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class Equal {
        public override void SetLong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Eq, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Eq, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetFloat(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Eq, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetDouble(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Eq, dest, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class NotEqual {
        public override void SetLong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Ne, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetULong(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Ne, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetFloat(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Ne, dest, dest, Right.CGenValue(state, null)));
        }

        public override void SetDouble(CGenState state, Operand dest)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Ne, dest, dest, Right.CGenValue(state, null)));
        }
    }

    public sealed partial class LogicalAnd {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            Int32 label_finish = state.RequestLabel();

            var dest = retLoc != null ? retLoc : state.FunctionState.PushType(state.TypeU32); // initialises to default(u32) == 0
            var temp = state.FunctionState.PushType(state.TypeU32);

            var ret = this.Left.CGenValue(state, null);
            state.CurrInsns.Add(Instruction.Create(OpCodes.Eq, temp, ret, dest));
            state.CurrInsns.Add(Instruction.Create(OpCodes.JumpNZ, state.FunctionState.Labels[label_finish], temp));

            ret = this.Right.CGenValue(state, null);
            state.CurrInsns.Add(Instruction.Create(OpCodes.Eq, temp, ret, dest));
            state.CurrInsns.Add(Instruction.Create(OpCodes.JumpNZ, state.FunctionState.Labels[label_finish], temp));

            state.CurrInsns.Add(Instruction.Create(OpCodes.Inc, dest));
            state.CurrInsns.Add(Instruction.Create(OpCodes.Jump, state.FunctionState.Labels[label_finish]));

            state.CGenLabel(label_finish);
            state.FunctionState.Pop();

            return dest;
        }
    }

    /// <summary>
    /// Left || Right: can only take scalars (to compare with 0).
    /// 
    /// After semantic analysis, each operand can only be
    /// long, ulong, float, double.
    /// Pointers are casted to ulongs.
    /// 
    /// if Left != 0:
    ///     return 1
    /// else:
    ///     return Right != 0
    /// 
    /// Generate the assembly in this fashion,
    /// then every route would only have one jump.
    /// 
    ///        +---------+   1
    ///        | cmp lhs |-------+
    ///        +---------+       |
    ///             |            |
    ///             | 0          |
    ///             |            |
    ///        +----+----+   1   |
    ///        | cmp rhs |-------+
    ///        +---------+       |
    ///             |            |
    ///             | 0          |
    ///             |            |
    ///        +----+----+       |
    ///        | eax = 0 |       |
    ///        +---------+       |
    ///             |            |
    ///   +---------+            |
    ///   |                      |
    ///   |         +------------+ label_set
    ///   |         |
    ///   |    +---------+
    ///   |    | eax = 1 |
    ///   |    +---------+
    ///   |         |
    ///   +---------+ label_finish
    ///             |
    /// 
    /// </summary>
    public sealed partial class LogicalOr {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            Int32 label_set = state.RequestLabel();
            Int32 label_finish = state.RequestLabel();

            var dest = retLoc != null ? retLoc : state.FunctionState.PushType(state.TypeU32); // initialises to default(u32) == 0
            var temp = state.FunctionState.PushType(state.TypeU32);
            // can't use "just" jnz, it doesn't support all possible types that could come through here

            var ret = this.Left.CGenValue(state, null);
            // temp = ret != dest; // dest == 0
            state.CurrInsns.Add(Instruction.Create(OpCodes.Ne, temp, ret, dest));
            state.CurrInsns.Add(Instruction.Create(OpCodes.JumpNZ, state.FunctionState.Labels[label_set], temp));

            ret = this.Right.CGenValue(state, null);
            state.CurrInsns.Add(Instruction.Create(OpCodes.Ne, temp, ret, dest));
            state.CurrInsns.Add(Instruction.Create(OpCodes.JumpZ, state.FunctionState.Labels[label_finish], temp));

            state.CGenLabel(label_set);

            state.FunctionState.Labels[label_set].Replace(Instruction.Create(OpCodes.Inc, dest));

            state.CurrInsns.Add(Instruction.Create(OpCodes.Jump, state.FunctionState.Labels[label_finish]));

            state.CGenLabel(label_finish);
            state.FunctionState.Pop();

            return dest;
        }
    }
}
