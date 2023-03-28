using System;
using CodeGeneration;
using IFPSLib.Emit;

namespace ABT {
    public sealed partial class TypeCast {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            if (this.Kind == TypeCastType.FUNC_TO_PTR)
            {
                switch (this.Expr)
                {
                    case Variable func:
                        return new Operand(IFPSLib.TypedData.Create(state.EmitType(this.Type) as IFPSLib.Types.FunctionPointerType, state.GetFunction(func.Name)));
                    default:
                        throw new InvalidProgramException().Attach(Expr);
                }
            }
            if (this.Kind == TypeCastType.NOP) return Expr.CGenValue(state, retLoc);

            var ret = this.Expr.CGenValue(state, null);
            Operand op = null;
            PointerType ptrType = null;
            Operand dummyForType = null;
            switch (this.Kind) {
                case TypeCastType.ARRAY_TO_PTR:
                    // easy, pushvar
                    return state.FunctionState.PushVar(ret);

                case TypeCastType.PTR_TO_INT32:
                    op = state.FunctionState.PushType(state.EmitType(Type));
                    state.CGenPushStackSize();
                    state.FunctionState.PushVar(ret);
                    state.FunctionState.PushVar(op);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastPointerRef));
                    state.CGenPopStackSize();
                    return op;

                case TypeCastType.INT32_TO_PTR:
                    op = state.FunctionState.PushType(state.TypeArrayOfPointer);
                    ptrType = Type as PointerType;
                    if (ptrType == null) throw new InvalidProgramException().Attach(Expr);
                    state.CGenPushStackSize();
                    dummyForType = state.FunctionState.PushType(state.EmitType(ptrType.RefType));
                    state.FunctionState.PushVar(op);
                    state.FunctionState.Push(ret);
                    state.FunctionState.PushVar(dummyForType);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastRefPointer));
                    state.CGenPopStackSize();
                    return Operand.Create(op.Variable, 0);

                case TypeCastType.PTR_TO_PTR:
                    // same as PTR_TO_INT32 then INT32_TO_PTR
                    ptrType = Type as PointerType;
                    if (ptrType == null) throw new InvalidProgramException().Attach(Expr);
                    op = state.FunctionState.PushType(state.TypeArrayOfPointer);
                    state.CGenPushStackSize();
                    dummyForType = state.FunctionState.PushType(state.EmitType(ptrType.RefType));
                    var dummyU32 = state.FunctionState.PushType(state.TypeU32);
                    state.CGenPushStackSize();
                    state.FunctionState.PushVar(ret);
                    state.FunctionState.PushVar(dummyU32);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastPointerRef));
                    state.CGenPopStackSize();
                    state.FunctionState.PushVar(op);
                    state.FunctionState.Push(dummyU32);
                    state.FunctionState.PushVar(dummyForType);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastRefPointer));
                    state.CGenPopStackSize();
                    return Operand.Create(op.Variable, 0);

                case TypeCastType.VARIANT_TO_COM_INTERFACE:
                    // this might be interface and might be dispatch.
                    op = retLoc != null ? retLoc : state.FunctionState.PushType(state.EmitType(Type));
                    state.CGenPushStackSize();
                    // u32 type = VarType(variant)
                    var varType = state.FunctionState.PushType(state.TypeU16);
                    state.CGenPushStackSize();
                    state.FunctionState.Push(ret);
                    state.FunctionState.PushVar(varType);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.VarType));
                    state.CGenPopStackSize();
                    // if (varType != vtDispatch) { regular com interface cast }
                    // else { cast to dispatch first }
                    // cast is same for both, just need to provide the "correct" type
                    const ushort VT_DISPATCH = 9;
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Sub, varType, Operand.Create(VT_DISPATCH)));
                    // regular com interface cast
                    state.CGenPushStackSize();
                    // typeNo
                    state.FunctionState.PushType(state.TypeU32);
                    // self

                    var comInterface = state.FunctionState.AddLocal();

                    var insnDispatch = Instruction.Create(OpCodes.PushType, state.TypeIDispatch);
                    var insnEnd = Instruction.Create(OpCodes.Assign, comInterface, ret);

                    state.CurrInsns.Add(Instruction.Create(OpCodes.JumpZ, insnDispatch, varType));

                    state.CurrInsns.Add(Instruction.Create(OpCodes.PushType, state.TypeIUnknown));
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Jump, insnEnd));

                    state.CurrInsns.Add(insnDispatch);

                    state.CurrInsns.Add(insnEnd);

                    // retval
                    state.FunctionState.PushVar(op);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.ComInterfaceCast));
                    state.CGenPopStackSize();
                    return op;

                case TypeCastType.COM_INTERFACE:
                    op = retLoc != null ? retLoc : state.FunctionState.PushType(state.EmitType(Type));
                    state.CGenPushStackSize();
                    // typeNo
                    state.FunctionState.PushType(state.TypeU32);
                    // self
                    state.FunctionState.Push(ret);
                    // retval
                    state.FunctionState.PushVar(op);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.ComInterfaceCast));
                    state.CGenPopStackSize();
                    return op;


                case TypeCastType.NOP:
                    return ret;

                case TypeCastType.COPY_CONSTRUCTOR:
                    if (retLoc == null) throw new InvalidOperationException("Cannot copy construct to an unknown variable").Attach(Expr);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Cpval, retLoc, ret));
                    return retLoc;

                default:
                    op = retLoc != null ? retLoc : state.FunctionState.PushType(state.EmitType(Type));
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, op, ret));
                    return op;
            }
        }

        public override Operand CGenAddress(CGenState state, Operand retLoc)
        {
            throw new InvalidOperationException("Cannot get the address of a cast expression.").Attach(Expr);
        }

        public override bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false)
        {
            if (Kind == TypeCastType.NOP) return Expr.CallerNeedsToCleanStack(state, retLocKnown);
            if (Expr.CallerNeedsToCleanStack(state, false)) return true;
            return !retLocKnown;
        }
    }
}