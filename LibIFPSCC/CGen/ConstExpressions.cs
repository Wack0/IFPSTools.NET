using System;
using CodeGeneration;
using IFPSLib.Emit;

namespace ABT {
    public abstract partial class ConstExpr {
        public override sealed Operand CGenAddress(CGenState state, Operand retLoc)
        {
            throw new InvalidOperationException("Cannot get the address of a constant").Attach(this);
        }
    }

    public sealed partial class ConstLong {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            return Operand.Create((IFPSLib.Types.PrimitiveType)state.EmitType(Type), Value);
        }
    }

    public sealed partial class ConstULong {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            return Operand.Create((IFPSLib.Types.PrimitiveType)state.EmitType(Type), Value);
        }
    }

    public sealed partial class ConstS64
    {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            return Operand.Create((IFPSLib.Types.PrimitiveType)state.EmitType(Type), Value);
        }
    }

    public sealed partial class ConstU64
    {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            return Operand.Create((IFPSLib.Types.PrimitiveType)state.EmitType(Type), Value);
        }
    }

    public sealed partial class ConstShort {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            return Operand.Create((IFPSLib.Types.PrimitiveType)state.EmitType(Type), Value);
        }
    }

    public sealed partial class ConstUShort {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            return Operand.Create((IFPSLib.Types.PrimitiveType)state.EmitType(Type), Value);
        }
    }

    public sealed partial class ConstChar {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            return Operand.Create((IFPSLib.Types.PrimitiveType)state.EmitType(Type), Value);
        }
    }

    public sealed partial class ConstUChar {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            return Operand.Create((IFPSLib.Types.PrimitiveType)state.EmitType(Type), Value);
        }
    }

    public sealed partial class ConstPtr {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            var ptrType = Type as PointerType;
            if (ptrType == null) throw new InvalidProgramException().Attach(this);
            if (!ptrType.IsRef) return Operand.Create((IFPSLib.Types.PrimitiveType)state.EmitType(Type), Value);
            var operand = state.FunctionState.PushType(state.TypeArrayOfPointer);
            state.CGenPushStackSize();
            var dummyForType = state.FunctionState.PushType(state.EmitType(ptrType.RefType));
            state.FunctionState.PushVar(operand);
            state.FunctionState.Push(Operand.Create((IFPSLib.Types.PrimitiveType)state.EmitType(Type), Value));
            state.FunctionState.PushVar(dummyForType);
            state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastRefPointer));
            state.CGenPopStackSize();
            return Operand.Create(operand.Variable, 0);
        }
    }

    public sealed partial class ConstFloat {
        /// <summary>
        /// flds addr
        /// </summary>
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            return Operand.Create((IFPSLib.Types.PrimitiveType)state.EmitType(Type), Value);
        }
    }

    public sealed partial class ConstDouble {
        /// <summary>
        /// fldl addr
        /// </summary>
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            return Operand.Create((IFPSLib.Types.PrimitiveType)state.EmitType(Type), Value);
        }
    }

    public sealed partial class ConstStringLiteral {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            return Operand.Create((IFPSLib.Types.PrimitiveType)state.EmitType(Type), Value);
        }
    }

    public sealed partial class ConstUnicodeStringLiteral
    {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            return Operand.Create((IFPSLib.Types.PrimitiveType)state.EmitType(Type), Value);
        }
    }
}