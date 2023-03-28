using System;
using System.Diagnostics;
using System.Linq;
using CodeGeneration;
using IFPSLib.Emit;

namespace ABT {
    public abstract partial class IncDecExpr {

        protected abstract bool IsPost { get; }

        public abstract void EmitPtr(CGenState state, uint sizeOf, Operand op);

        public abstract void Emit(CGenState state, Operand op);

        public override sealed Operand CGenValue(CGenState state, Operand retLoc)
        {

            // 1. Get an operand that can be used for pushvar.
            var op = Expr.CGenAddress(state, retLoc);

            var op2 = op;

            if (Expr.Type.Kind == ExprTypeKind.POINTER)
            {
                if (IsPost)
                {
                    op2 = state.FunctionState.Push(op);
                }
                var ptrType = Type as PointerType;
                if (ptrType == null) throw new InvalidProgramException().Attach(Expr);
                if (ptrType.IsRef) {
                    // This is a pointer.
                    // Convert to u32.
                    state.CGenPushStackSize();
                    var u32 = state.FunctionState.PushType(state.TypeU32);
                    state.CGenPushStackSize();
                    state.FunctionState.PushVar(op);
                    state.FunctionState.PushVar(u32);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastPointerRef));
                    state.CGenPopStackSize();
                    // Emit instructions for the pointer.
                    EmitPtr(state, (uint)ptrType.RefType.SizeOf, u32);
                    // Convert back to pointer.
                    var arr = state.FunctionState.PushType(state.TypeArrayOfPointer);
                    state.CGenPushStackSize();
                    var dummyForType = state.FunctionState.PushType(state.EmitType(ptrType.RefType));
                    state.FunctionState.PushVar(op);
                    state.FunctionState.Push(u32);
                    state.FunctionState.PushVar(dummyForType);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastRefPointer));
                    state.CGenPopStackSize();
                    state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, op, Operand.Create(arr.Variable, 0)));
                    state.CGenPopStackSize();
                    return op2;
                }
                EmitPtr(state, ExprType.SIZEOF_CHAR, op);
                return op2;
            }
            else
            {
                if (IsPost)
                {
                    var last = state.CurrInsns.LastOrDefault();
                    var attr = Expr as Attribute;
                    if (Expr is Dereference || (attr != null && !((StructOrUnionType)attr.Expr.Type).IsStruct) )
                    {
                        // it's a deref, this is one of the edge cases where we have to do the deref ourselves
                        op2 = state.FunctionState.PushType(state.EmitType(Expr.Type));
                        state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, op2, op));
                    }
                    else
                    {
                        op2 = state.FunctionState.Push(op);
                    }
                }

                // 2. Emit the instruction(s).
                Emit(state, op);

                // Done!
                return op2;
            }


        }

        public override sealed Operand CGenAddress(CGenState state, Operand retLoc)
        {
            throw new InvalidOperationException("Cannot get the address of an increment/decrement expression.").Attach(Expr);
        }

        public override sealed bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false)
        {
            return Expr.CallerNeedsToCleanStack(state, retLocKnown, true) || IsPost;
        }
    }

    public sealed partial class PostIncrement {
        protected override bool IsPost => true;
        public override void EmitPtr(CGenState state, uint sizeOf, Operand op)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Add, op, Operand.Create(sizeOf)));
        }
        public override void Emit(CGenState state, Operand op)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Inc, op));
        }
    }

    public sealed partial class PostDecrement
    {
        protected override bool IsPost => true;
        public override void EmitPtr(CGenState state, uint sizeOf, Operand op)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Sub, op, Operand.Create(sizeOf)));
        }
        public override void Emit(CGenState state, Operand op)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Dec, op));
        }
    }

    public sealed partial class PreIncrement
    {
        protected override bool IsPost => false;
        public override void EmitPtr(CGenState state, uint sizeOf, Operand op)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Add, op, Operand.Create(sizeOf)));
        }

        public override void Emit(CGenState state, Operand op)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Inc, op));
        }
    }

    public sealed partial class PreDecrement
    {
        protected override bool IsPost => false;
        public override void EmitPtr(CGenState state, uint sizeOf, Operand op)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Sub, op, Operand.Create(sizeOf)));
        }

        public override void Emit(CGenState state, Operand op)
        {
            state.CurrInsns.Add(Instruction.Create(OpCodes.Dec, op));
        }
    }

    public abstract partial class UnaryArithOp {
        public override sealed Operand CGenAddress(CGenState state, Operand retLoc)
        {
            throw new InvalidOperationException("Cannot get the address of an unary arithmetic operator.").Attach(Expr);
        }
    }

    public sealed partial class Negative {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            var ret = Expr.CGenValue(state, retLoc);
            if (retLoc == null)
            {
                ret = state.FunctionState.Push(ret);
            }
            else if (!OperandEquator.Equals(retLoc, ret))
            {
                state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, retLoc, ret));
                ret = retLoc;
            }
            state.CurrInsns.Add(Instruction.Create(OpCodes.Neg, ret));
            return ret;
        }

        public override bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false) => !retLocKnown;
    }

    public sealed partial class BitwiseNot {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            var ret = Expr.CGenValue(state, retLoc);
            if (retLoc == null)
            {
                ret = state.FunctionState.Push(ret);
            }
            else if (!OperandEquator.Equals(retLoc, ret))
            {
                state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, retLoc, ret));
                ret = retLoc;
            }
            state.CurrInsns.Add(Instruction.Create(OpCodes.Not, ret));
            return ret;
        }

        public override bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false) => !retLocKnown;
    }

    public sealed partial class LogicalNot {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {

            var ret = Expr.CGenValue(state, retLoc);
            if (retLoc == null)
            {
                ret = state.FunctionState.Push(ret);
            }
            else if (!OperandEquator.Equals(retLoc, ret))
            {
                state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, retLoc, ret));
                ret = retLoc;
            }
            // ret = (ret == 0) ; sets to 1 if false
            // ret = (ret == 0) ; inverts
            state.CurrInsns.Add(Instruction.Create(OpCodes.SetZ, ret));
            state.CurrInsns.Add(Instruction.Create(OpCodes.SetZ, ret));
            return ret;
        }

        public override bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false) => !retLocKnown;
    }
}
