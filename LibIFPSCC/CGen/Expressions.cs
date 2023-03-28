using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using AST;
using CodeGeneration;
using IFPSLib.Emit;

namespace ABT {
    public abstract partial class Expr {
        public abstract Operand CGenValue(CGenState state, Operand retLoc);

        public abstract Operand CGenAddress(CGenState state, Operand retLoc);

        /// <summary>
        /// Returns true if this expression modifies the stack in such a way that the callee needs to clean up (ie, expression allocates then returns a new Operand)
        /// </summary>
        /// <param name="state">Code generation state</param>
        /// <param name="retLocKnown"></param>
        /// <returns></returns>
        /// <param name="forAddress">True if being called for CGenAddress, false if being called for CGenValue</param>
        public abstract bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false);
    }

    public sealed partial class ExprInitList
    {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            Operand op = retLoc != null ? retLoc : state.FunctionState.PushType(state.EmitType(Type));
            if (Type.Kind == ExprTypeKind.INCOMPLETE_ARRAY)
            {
                var arr = Type as IncompleteArrayType;
                var arr_op = op;
                // emit SetArrayLength(&arr_op, length)
                state.CGenPushStackSize();
                state.FunctionState.Push(Operand.Create(List.initrs.Count));
                state.FunctionState.PushVar(arr_op);
                state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.SetArrayLength));
                state.CGenPopStackSize();
            }
            Decln.EmitInitialiser(List, Type, op, state);
            return op;
        }

        public override Operand CGenAddress(CGenState state, Operand retLoc)
        {
            throw new InvalidOperationException("Cannot get the address of an initialiser list.").Attach(this);
        }

        public override bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false) => !retLocKnown;
    }

    public sealed partial class Variable {
        public override Operand CGenAddress(CGenState state, Operand retLoc)
        {
            Env.Entry entry = this.Env.Find(this.Name).Value;
            Int32 offset = entry.Offset;

            switch (entry.Kind) {
                case Env.EntryKind.FRAME:
                    return Operand.Create(state.FunctionState.Function.CreateArgumentVariable(offset));
                case Env.EntryKind.STACK:
                    return Operand.Create(LocalVariable.Create(offset));

                case Env.EntryKind.GLOBAL:
                    return Operand.Create(state.Globals[Name]);

                case Env.EntryKind.ENUM:
                case Env.EntryKind.TYPEDEF:
                default:
                    throw new InvalidProgramException("cannot get the address of " + entry.Kind).Attach(this);
            }
        }

        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            Env.Entry entry = this.Env.Find(this.Name).Value;

            Int32 offset = entry.Offset;
            //if (entry.Kind == Env.EntryKind.STACK) {
            //    offset = -offset;
            //}

            IVariable var = null;

            switch (entry.Kind) {
                case Env.EntryKind.ENUM:
                    // 1. If the variable is an enum constant,
                    //    return the Value in %eax.
                    return Operand.Create(offset);

                case Env.EntryKind.FRAME:
                    var = state.FunctionState.Function.CreateArgumentVariable(offset);
                    break;
                case Env.EntryKind.STACK:
                    // 2. If the variable is a function argument or a local variable,
                    //    the address would be offset(%ebp).
                    var = LocalVariable.Create(offset);
                    break;

                case Env.EntryKind.GLOBAL:
                    switch (this.Type.Kind) {
                        case ExprTypeKind.CHAR:
                        case ExprTypeKind.UCHAR:
                        case ExprTypeKind.SHORT:
                        case ExprTypeKind.USHORT:
                        case ExprTypeKind.LONG:
                        case ExprTypeKind.ULONG:
                        case ExprTypeKind.S64:
                        case ExprTypeKind.U64:
                        case ExprTypeKind.POINTER:
                        case ExprTypeKind.FLOAT:
                        case ExprTypeKind.DOUBLE:
                        case ExprTypeKind.ANSI_STRING:
                        case ExprTypeKind.UNICODE_STRING:
                        case ExprTypeKind.COM_INTERFACE:
                        case ExprTypeKind.COM_VARIANT:
                            return Operand.Create(state.Globals[Name]);

                        case ExprTypeKind.STRUCT_OR_UNION:
                        case ExprTypeKind.ARRAY:
                        case ExprTypeKind.INCOMPLETE_ARRAY:
                            return Operand.Create(state.Globals[Name]);

                        //state.LEA(name, Reg.ESI); // source address
                        //state.CGenExpandStackBy(Utils.RoundUp(Type.SizeOf, 4));
                        //state.LEA(0, Reg.ESP, Reg.EDI); // destination address
                        //state.MOVL(Type.SizeOf, Reg.ECX); // nbytes
                        //state.CGenMemCpy();
                        //return Reg.STACK;

                        case ExprTypeKind.FUNCTION:
                            var fptrt = (Type as FunctionType).EmitPointer(state) as IFPSLib.Types.FunctionPointerType;
                            return new Operand(IFPSLib.TypedData.Create(fptrt, state.GetFunction(Name)));

                        case ExprTypeKind.VOID:
                            throw new InvalidProgramException("How could a variable be void?").Attach(this);
                        //state.MOVL(0, Reg.EAX);
                        //return Reg.EAX;


                        default:
                            throw new InvalidProgramException("cannot get the Value of a " + this.Type.Kind).Attach(this);
                    }

                case Env.EntryKind.TYPEDEF:
                default:
                    throw new InvalidProgramException("cannot get the Value of a " + entry.Kind).Attach(this);
            }

            switch (this.Type.Kind)
            {

                case ExprTypeKind.POINTER:
                    if ((this.Type as PointerType).IsRef) return Operand.Create(var);
                    else return Operand.Create(var, 0);
                case ExprTypeKind.CHAR:
                case ExprTypeKind.UCHAR:
                case ExprTypeKind.SHORT:
                case ExprTypeKind.USHORT:
                case ExprTypeKind.LONG:
                case ExprTypeKind.ULONG:
                case ExprTypeKind.S64:
                case ExprTypeKind.U64:
                case ExprTypeKind.FLOAT:
                case ExprTypeKind.DOUBLE:
                case ExprTypeKind.ANSI_STRING:
                case ExprTypeKind.UNICODE_STRING:
                case ExprTypeKind.COM_INTERFACE:
                case ExprTypeKind.COM_VARIANT:
                    return Operand.Create(var);

                case ExprTypeKind.STRUCT_OR_UNION:
                case ExprTypeKind.ARRAY:
                case ExprTypeKind.INCOMPLETE_ARRAY:
                    return Operand.Create(var);

                //state.LEA(offset, Reg.EBP, Reg.ESI); // source address
                //state.CGenExpandStackBy(Utils.RoundUp(Type.SizeOf, 4));
                //state.LEA(0, Reg.ESP, Reg.EDI); // destination address
                //state.MOVL(Type.SizeOf, Reg.ECX); // nbytes
                //state.CGenMemCpy();
                //return Reg.STACK;

                case ExprTypeKind.VOID:
                    throw new InvalidProgramException("How could a variable be void?").Attach(this);
                // %eax = $0
                // state.MOVL(0, Reg.EAX);
                // return Reg.EAX;

                case ExprTypeKind.FUNCTION:
                    throw new InvalidProgramException("How could a variable be a function designator?").Attach(this);
                // %eax = function_name
                // state.MOVL(name, Reg.EAX);
                // return Reg.EAX;

                default:
                    throw new InvalidOperationException($"Cannot get value of {this.Type.Kind}").Attach(this);
            }
        }

        public override bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false) => false;
    }

    public sealed partial class AssignList {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            Operand op = null;
            foreach (Expr expr in this.Exprs) {
                op = expr.CGenValue(state, null);
            }
            return op;
        }

        public override Operand CGenAddress(CGenState state, Operand retLoc)
        {
            throw new InvalidOperationException("Cannot get the address of an assignment list.").Attach(this);
        }

        public override bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false) => Exprs.Any((expr) => expr.CallerNeedsToCleanStack(state, false));
    }

    public sealed partial class Assign {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            var operand = Left.CGenAddress(state, retLoc);
            var ret = Right.CGenValue(state, operand);
            if (OperandEquator.Equals(ret, retLoc)) return ret;

            switch (this.Left.Type.Kind) {
                case ExprTypeKind.CHAR:
                case ExprTypeKind.UCHAR:

                case ExprTypeKind.SHORT:
                case ExprTypeKind.USHORT:

                case ExprTypeKind.LONG:
                case ExprTypeKind.ULONG:
                case ExprTypeKind.S64:
                case ExprTypeKind.U64:

                case ExprTypeKind.FLOAT:

                case ExprTypeKind.DOUBLE:

                case ExprTypeKind.STRUCT_OR_UNION:


                case ExprTypeKind.ANSI_STRING:
                case ExprTypeKind.UNICODE_STRING:

                case ExprTypeKind.COM_INTERFACE:
                case ExprTypeKind.COM_VARIANT:
                    if (OperandEquator.Equals(operand, ret)) return operand;
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, operand, ret));
                    break;


                case ExprTypeKind.POINTER:
                    var lAttr = Left as ABT.Attribute;
                    var rAttr = Right as ABT.Attribute;
                    var lArr = Left as ABT.ArrayIndexDeref;
                    var rArr = Right as ABT.ArrayIndexDeref;

                    var ptr = Left.Type as PointerType;
                    var ptrR = Right.Type as PointerType;

                    var needCast = ptr.IsRef != ptrR.IsRef;
                    bool lIsVoidPointer = lAttr != null || lArr != null; // both Attribute and ArrayIndexDeref CGenAddress gives a u32*
                    var isVoid = !ptr.IsRef;
                    if (needCast) {
                        // assigning to or from a structure or array element.
                        // CGenAddress for both of these returns u32*.
                        // Ptr-to-ptr cast into operand.
                        var rIsUnion = ((StructOrUnionType)(rAttr?.Expr.Type))?.IsStruct == false;
                        bool isVoidR = !ptrR.IsRef;
                        if (lIsVoidPointer)
                        {
                            // *lhs = (u32)rhs;
                            state.CGenPushStackSize();
                            var cast = state.FunctionState.PushType(state.TypeU32);
                            state.CGenPushStackSize();
                            if (rIsUnion) // for a union, we have u32* already
                            {
                                state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, cast, ret));
                            }
                            else
                            {
                                state.FunctionState.PushVar(ret);
                                state.FunctionState.PushVar(cast);
                                state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastPointerRef));
                            }
                            state.CGenPopStackSize();
                            state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, operand, cast));
                            state.CGenPopStackSize();
                            break;
                        }
                        if ((!isVoid && !isVoidR) || rIsUnion)
                        {

                            state.CGenPushStackSize();

                            var dummyForType = state.FunctionState.PushType(state.EmitType(ptr.RefType));
                            var dummyU32 = state.FunctionState.PushType(state.TypeU32);
                            if (ptr.IsRef)
                            {
                                // lhs is byref, deal
                                // do a pointer-to-pointer cast
                                var ptrArr = state.FunctionState.PushType(state.TypeArrayOfPointer);
                                state.CGenPushStackSize();
                                if (rIsUnion) // for a union, we have u32* already
                                {
                                    state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, dummyU32, ret));
                                }
                                else
                                {
                                    state.FunctionState.PushVar(ret);
                                    state.FunctionState.PushVar(dummyU32);
                                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastPointerRef));
                                }
                                state.CGenPopStackSize();
                                state.FunctionState.PushVar(ptrArr);
                                state.FunctionState.Push(dummyU32);
                                state.FunctionState.PushVar(dummyForType);
                                state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastRefPointer));
                                state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, operand, Operand.Create(ptrArr.Variable, 0)));
                            }
                            else
                            {
                                state.CGenPushStackSize();
                                if (rIsUnion) // for a union, we have u32* already
                                {
                                    state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, dummyU32, ret));
                                }
                                else
                                {
                                    state.FunctionState.PushVar(ret);
                                    state.FunctionState.PushVar(dummyU32);
                                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastPointerRef));
                                }
                                state.CGenPopStackSize();
                                state.FunctionState.PushVar(operand);
                                state.FunctionState.Push(dummyU32);
                                state.FunctionState.PushVar(dummyForType);
                                state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastRefPointer));
                            }
                            state.CGenPopStackSize();
                            break;
                        }
                    }
                    
                    state.CurrInsns.Add(Instruction.Create(isVoid ? OpCodes.Assign : OpCodes.SetPtr, operand, ret));
                    break;

                case ExprTypeKind.FUNCTION:
                case ExprTypeKind.VOID:
                case ExprTypeKind.ARRAY:
                case ExprTypeKind.INCOMPLETE_ARRAY:
                default:
                    throw new InvalidProgramException("cannot assign to a " + this.Type.Kind);
            }

            return operand;
        }
    
        public override Operand CGenAddress(CGenState state, Operand retLoc)
        {
            throw new InvalidOperationException("Cannot get the address of an assignment expression.").Attach(this);
        }

        public override bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false)
        {
            return Left.CallerNeedsToCleanStack(state, retLocKnown) || Right.CallerNeedsToCleanStack(state, retLocKnown, true);
        }
    }

    public sealed partial class ConditionalExpr {
        // 
        //          jz false, Cond ---+
        //          true_expr   |
        // +------- jmp finish  |
        // |    false: <--------+
        // |        false_expr
        // +--> finish:
        // 
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            //Int32 stack_size = state.StackSize;
            var ret = this.Cond.CGenValue(state, null);

            Int32 false_label = state.RequestLabel();
            Int32 finish_label = state.RequestLabel();

            state.CurrInsns.Add(Instruction.Create(OpCodes.JumpZ, state.FunctionState.Labels[false_label], ret));

            this.TrueExpr.CGenValue(state, null);

            state.CurrInsns.Add(Instruction.Create(OpCodes.Jump, state.FunctionState.Labels[finish_label], ret));

            state.CGenLabel(false_label);

            var count = state.CurrInsns.Count;

            ret = this.FalseExpr.CGenValue(state, null);

            if (state.CurrInsns.Count > count)
            {
                state.FunctionState.Labels[false_label].Replace(state.CurrInsns[count]);
                state.CurrInsns.RemoveAt(count);
            }

            state.CGenLabel(finish_label);

            return ret;
        }

        public override Operand CGenAddress(CGenState state, Operand retLoc)
        {
            throw new InvalidOperationException("Cannot get the address of a conditional expression.").Attach(this);
        }

        public override bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false)
        {
            return Cond.CallerNeedsToCleanStack(state, retLocKnown) || TrueExpr.CallerNeedsToCleanStack(state, retLocKnown) || FalseExpr.CallerNeedsToCleanStack(state, retLocKnown);
        }
    }

    public sealed partial class FuncCall {
        public override Operand CGenAddress(CGenState state, Operand retLoc)
        {
            throw new InvalidOperationException("Error: cannot get the address of a function call.").Attach(this);
        }

        public override Operand CGenValue(CGenState state, Operand retLoc)
        {

            // PascalScript bytecode calling convention:
            // Arguments pushed last-to-first
            // Then, if function returns a value, pointer to return value is pushed
            // Then call.
            // Callee cleans up stack.

            FunctionType ft = null;
            switch (Func.Type)
            {
                case FunctionType _ft:
                    ft = _ft;
                    break;
                case PointerType _pt:
                    ft = (FunctionType)_pt.RefType;
                    break;
                default:
                    throw new InvalidOperationException().Attach(Func);
            }

            var rt = ft.ReturnType;
            Operand retval = null;
            if (!(rt is VoidType))
            {
                // returns a value
                // prep the stack for retval
                retval = retLoc != null ? retLoc : state.FunctionState.PushType(state.EmitType(rt));
            }

            state.CGenPushStackSize();

            // Push the arguments onto the stack in reverse order
            foreach (var arg in Args.Reverse()) {
                var ptr = arg.Type as PointerType;
                // Get the arg in a way that changes can be reverted, so to know if stack gets touched or not.
                var insnCount = state.CurrInsns.Count;
                var oldStack = state.StackSize;
                var doesPush = arg.CallerNeedsToCleanStack(state, false);
                Operand argop = null;
                /*
                state.CGenPushStackSize();
                var argop = arg.CGenValue(state);
                var doesPush = state.StackSize != oldStack;
                state.CGenPopStackSizeForRevert();
                */
                if (doesPush)
                {
                    // Stack was touched. Revert all changes.
                    //while (state.CurrInsns.Count > insnCount) state.CurrInsns.RemoveAt(state.CurrInsns.Count - 1);
                    Operand _argop = null;
                    if (arg.Type.Kind == ExprTypeKind.POINTER && ptr.IsRef)
                    {
                        // this is a reftype, so, pushtype pointer ; ... ; setptr argop, value ; fix stack
                        argop = state.FunctionState.PushType(state.TypePointer);
                        state.CGenPushStackSize();
                        _argop = arg.CGenValue(state, argop);
                        if (_argop != argop) state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, argop, _argop));
                        state.CGenPopStackSize();
                        continue;
                    }
                    // this is a valtype, so pushtype type ; ... ; assign argop, value ; fix stack
                    argop = state.FunctionState.PushType(state.EmitType(arg.Type));
                    state.CGenPushStackSize();
                    _argop = arg.CGenValue(state, argop);
                    if (_argop != argop) state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, argop, _argop));
                    state.CGenPopStackSize();
                    continue;
                }
                argop = arg.CGenValue(state, null);
                switch (arg.Type.Kind) {
                    case ExprTypeKind.ARRAY:
                    case ExprTypeKind.INCOMPLETE_ARRAY:
                    case ExprTypeKind.CHAR:
                    case ExprTypeKind.UCHAR:
                    case ExprTypeKind.SHORT:
                    case ExprTypeKind.USHORT:
                    case ExprTypeKind.LONG:
                    case ExprTypeKind.ULONG:
                    case ExprTypeKind.S64:
                    case ExprTypeKind.U64:
                    case ExprTypeKind.DOUBLE:
                    case ExprTypeKind.FLOAT:
                    case ExprTypeKind.STRUCT_OR_UNION:
                    case ExprTypeKind.ANSI_STRING:
                    case ExprTypeKind.UNICODE_STRING:
                    case ExprTypeKind.COM_INTERFACE:
                    case ExprTypeKind.COM_VARIANT:
                        state.FunctionState.Push(argop);
                        break;

                    case ExprTypeKind.POINTER:
                        if (!ptr.IsRef) state.FunctionState.Push(argop);
                        else state.FunctionState.PushVar(argop);
                        break;

                    default:
                        throw new InvalidProgramException().Attach(arg);
                }

            }

            if (retval != null) state.FunctionState.PushVar(retval);

            // Get function address
            if (this.Func.Type is FunctionType) {
                state.CurrInsns.Add(Instruction.Create(OpCodes.Call, Func.CGenValue(state, null).ImmediateTyped.ValueAs<IFPSLib.IFunction>()));
            } else if (this.Func.Type is PointerType) {
                state.CurrInsns.Add(Instruction.Create(OpCodes.CallVar, Func.CGenValue(state, null)));
            } else {
                throw new InvalidProgramException().Attach(this.Func);
            }

            // Fix up the stack.
            state.CGenPopStackSize();

            // For a pointer, we've returned arrayofpointer so fix it up.
            var retPtr = rt as PointerType;
            if (retPtr != null)
            {
                if (!retPtr.IsRef) return retval;
                return Operand.Create(retval.Variable, 0);
            }

            return retval;
        }

        public override bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false)
        {
            FunctionType ft = null;
            switch (Func.Type)
            {
                case FunctionType _ft:
                    ft = _ft;
                    break;
                case PointerType _pt:
                    ft = (FunctionType)_pt.RefType;
                    break;
                default:
                    throw new InvalidOperationException().Attach(Func);
            }

            var rt = ft.ReturnType;
            return rt.Kind != ExprTypeKind.VOID && !retLocKnown;
        }
    }

    public sealed partial class Attribute {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {

            if (this.Expr.Type.Kind != ExprTypeKind.STRUCT_OR_UNION) {
                throw new InvalidProgramException().Attach(this.Expr);
            }

            // get the last instruction first in case it emits
            var last = state.CurrInsns.LastOrDefault();
            var op = Expr.CGenValue(state, retLoc);
            // if this is not a variable, we need to save it somewhere
            if (op.Type == BytecodeOperandType.Immediate) throw new InvalidProgramException().Attach(Expr);
            if (op.Type == BytecodeOperandType.IndexedImmediate || op.Type == BytecodeOperandType.IndexedVariable)
            {
                // last instruction should be pushvar or setptr
                // if not, then pushvar it
                switch (last?.OpCode.Code)
                {
                    case Code.PushVar:
                        var pv = LocalVariable.Create(state.FunctionState.LocalsCount - 1);
                        state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, pv, op));
                        op = Operand.Create(pv);
                        break;
                    case Code.SetPtr:
                        state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, last.Operands[0], op));
                        op = last.Operands[0];
                        break;
                    default:
                        if (!CallerNeedsToCleanStack(state, retLoc != null, false)) throw new InvalidOperationException().Attach(Expr);
                        op = state.FunctionState.PushVar(op);
                        break;
                }
            }
            if (op.Type != BytecodeOperandType.Variable)
            {
                // todo, not sure how to do this for now
                throw new InvalidOperationException().Attach(Expr);
            }


            // size of the struct or union
            Int32 struct_size = this.Expr.Type.SizeOf;

            var type = (StructOrUnionType)this.Expr.Type;

            // offset inside the pack
            int attrib_offset = type
                        .Attribs
                        .Select((a, i) => (a, i))
                        .First(_ => _.a.name == this.Name)
                        .i;


            if (!type.IsStruct)
            {
                // this is a union, so:
                // op is a byte array
                // get the pointer to that then do ptr-to-ptr cast
                op = Operand.Create(op.Variable, 0);
                last = state.CurrInsns.LastOrDefault();
                switch (last?.OpCode.Code)
                {
                    case Code.PushVar:
                        var pv = LocalVariable.Create(state.FunctionState.LocalsCount - 1);
                        state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, pv, op));
                        op = Operand.Create(pv);
                        break;
                    case Code.SetPtr:
                        state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, last.Operands[0], op));
                        op = last.Operands[0];
                        break;
                    default:
                        if (!CallerNeedsToCleanStack(state, retLoc != null, false)) throw new InvalidOperationException().Attach(Expr);
                        op = state.FunctionState.PushVar(op);
                        break;
                }
                // use pointerinitialiser as we need to have setptr as last insn
                state.CGenPushStackSize();
                // for a pointer, always cast to u32*
                var ptr = Type as PointerType;
                var dummyForType = state.FunctionState.PushType(ptr != null ? state.TypeU32 : state.EmitType(Type));
                var dummyU32 = state.FunctionState.PushType(state.TypeU32);
                state.CGenPushStackSize();
                state.FunctionState.PushVar(op);
                state.FunctionState.PushVar(dummyU32);
                state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastPointerRef));
                state.CGenPopStackSize();
                state.FunctionState.PushVar(Operand.Create(state.PointerInitialiser));
                state.FunctionState.Push(dummyU32);
                state.FunctionState.PushVar(dummyForType);
                state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastRefPointer));
                state.CGenPopStackSize();
                var ptrInit = Operand.Create(state.PointerInitialiser, 0);
                state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, op, ptrInit));
                return op;
            }

            // can't be a function designator.
            switch (this.Type.Kind) {
                case ExprTypeKind.ARRAY:
                case ExprTypeKind.INCOMPLETE_ARRAY:
                case ExprTypeKind.STRUCT_OR_UNION:
                case ExprTypeKind.CHAR:
                case ExprTypeKind.UCHAR:
                case ExprTypeKind.SHORT:
                case ExprTypeKind.USHORT:
                case ExprTypeKind.LONG:
                case ExprTypeKind.ULONG:
                case ExprTypeKind.S64:
                case ExprTypeKind.U64:
                case ExprTypeKind.FLOAT:
                case ExprTypeKind.DOUBLE:
                case ExprTypeKind.ANSI_STRING:
                case ExprTypeKind.UNICODE_STRING:
                case ExprTypeKind.COM_INTERFACE:
                case ExprTypeKind.COM_VARIANT:
                    return Operand.Create(op.Variable, (uint)attrib_offset);

                case ExprTypeKind.POINTER:
                    var ptr = Type as PointerType;
                    op = Operand.Create(op.Variable, (uint)attrib_offset);
                    if (!ptr.IsRef) return op;
                    // structure element, so we must cast to correct pointer type
                    state.CGenPushStackSize();
                    // we have u32 or u32*, we want (T*)ptr or *(T*)pptr
                    var dummyForType = state.FunctionState.PushType(state.EmitType(ptr.RefType));
                    var dummyU32 = state.FunctionState.PushType(state.TypeU32);
                    state.CGenPushStackSize();
                    state.FunctionState.PushVar(op);
                    state.FunctionState.PushVar(dummyU32);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastPointerRef));
                    state.CGenPopStackSize();
                    state.FunctionState.PushVar(Operand.Create(state.PointerInitialiser));
                    state.FunctionState.Push(dummyU32);
                    state.FunctionState.PushVar(dummyForType);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastRefPointer));
                    state.CGenPopStackSize();
                    var ptrInit = Operand.Create(state.PointerInitialiser, 0);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, op, ptrInit));
                    return op;
                default:
                    throw new InvalidProgramException().Attach(Expr);
            }
        }

        public override Operand CGenAddress(CGenState state, Operand retLoc)
        {
            if (this.Expr.Type.Kind != ExprTypeKind.STRUCT_OR_UNION) {
                throw new InvalidProgramException().Attach(Expr);
            }

            // get the operand of the struct or union
            // get the last instruction first in case it emits
            var last = state.CurrInsns.LastOrDefault();

            var type = ((StructOrUnionType)this.Expr.Type);
            var op = Expr.CGenAddress(state, retLoc);
            var isDeref = Expr is Dereference;
            if (type.IsStruct || !isDeref)
            {
                // if this is not a variable, we need to save it somewhere
                if (op.Type == BytecodeOperandType.Immediate) throw new InvalidProgramException().Attach(Expr);
                if (op.Type == BytecodeOperandType.IndexedImmediate || op.Type == BytecodeOperandType.IndexedVariable)
                {
                    // last instruction should be pushvar or setptr
                    // if not, then pushvar it
                    switch (last?.OpCode.Code)
                    {
                        case Code.PushVar:
                            var pv = LocalVariable.Create(state.FunctionState.LocalsCount - 1);
                            state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, pv, op));
                            op = Operand.Create(pv);
                            break;
                        case Code.SetPtr:
                            state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, last.Operands[0], op));
                            op = last.Operands[0];
                            break;
                        default:
                            if (!CallerNeedsToCleanStack(state, retLoc != null, true)) throw new InvalidOperationException().Attach(Expr);
                            op = state.FunctionState.PushVar(op);
                            break;
                    }
                }
                if (op.Type != BytecodeOperandType.Variable)
                {
                    // todo, not sure how to do this for now
                    throw new InvalidOperationException().Attach(Expr);
                }
            }


            // offset inside the pack
            Int32 offset = type
                        .Attribs
                        .Select((a, i) => (a, i))
                        .First(_ => _.a.name == this.Name)
                        .i;

            if (!type.IsStruct)
            {
                // this is a union, so:
                // op is a byte array
                // get the pointer to that then do ptr-to-ptr cast
                if (!isDeref)
                {
                    op = Operand.Create(op.Variable, 0);
                    last = state.CurrInsns.LastOrDefault();
                    switch (last?.OpCode.Code)
                    {
                        case Code.PushVar:
                            var pv = LocalVariable.Create(state.FunctionState.LocalsCount - 1);
                            state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, pv, op));
                            op = Operand.Create(pv);
                            break;
                        case Code.SetPtr:
                            state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, last.Operands[0], op));
                            op = last.Operands[0];
                            break;
                        default:
                            if (!CallerNeedsToCleanStack(state, retLoc != null, true)) throw new InvalidOperationException().Attach(Expr);
                            op = state.FunctionState.PushVar(op);
                            break;
                    }
                }
                // use pointerinitialiser as we need to have setptr as last insn
                state.CGenPushStackSize();
                // for a pointer, always cast to u32*
                var ptr = Type as PointerType;
                var dummyForType = state.FunctionState.PushType(ptr != null ? state.TypeU32 : state.EmitType(Type));
                var dummyU32 = state.FunctionState.PushType(state.TypeU32);
                if (!isDeref)
                {
                    state.CGenPushStackSize();
                    state.FunctionState.PushVar(op);
                    state.FunctionState.PushVar(dummyU32);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastPointerRef));
                    state.CGenPopStackSize();
                } else
                {
                    // deref union, already byref u32
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, dummyU32, op));
                }
                state.FunctionState.PushVar(Operand.Create(state.PointerInitialiser));
                state.FunctionState.Push(dummyU32);
                state.FunctionState.PushVar(dummyForType);
                state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastRefPointer));
                state.CGenPopStackSize();
                var ptrInit = Operand.Create(state.PointerInitialiser, 0);
                state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, op, ptrInit));
                return op;
            }

            // not a union. any element that happens to be a pointer is already of type u32 anyway
            return Operand.Create(op.Variable, (uint)offset);
        }

        public override bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false)
        {
            if (Expr.CallerNeedsToCleanStack(state, retLocKnown, forAddress)) return true;

            // If Expr is also an Attribute then caller will need to clean stack
            if (Expr is Attribute) return true;
            // Probably same for ArrayIndexDeref
            if (Expr is ArrayIndexDeref) return true;
            return false;
        }
    }

    public sealed partial class Reference {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            // todo: should we pushvar?
            return Expr.CGenAddress(state, retLoc);
        }

        public override Operand CGenAddress(CGenState state, Operand retLoc)
        {
            throw new InvalidOperationException("Cannot get the address of a pointer value.").Attach(Expr);
        }

        public override bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false)
        {
            return Expr.CallerNeedsToCleanStack(state, retLocKnown, true);
        }
    }

    public sealed partial class Dereference {
        public override Operand CGenValue(CGenState state, Operand retLoc)
        {
            // generally we do not need to do anything special to deref
            // however, if our expr is getting a pointer element of a union, we have ptr to ptr and need to deref that
            var op = Expr.CGenValue(state, retLoc);
            var attr = Expr as Attribute;
            if (attr == null) return op;
            bool isUnion = ((StructOrUnionType)Expr.Type)?.IsStruct == false;
            if (!isUnion) return op;
            var ptr = (PointerType)Expr.Type;
            // we have ref u32
            if (!ptr.IsRef) return op;
            // do u32-to-pointer cast
            state.CGenPushStackSize();
            var dummyForType = state.FunctionState.PushType(state.EmitType(ptr.RefType));
            state.FunctionState.PushVar(Operand.Create(state.PointerInitialiser));
            var u32 = state.FunctionState.PushType(state.TypeU32);
            state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, u32, op));
            state.FunctionState.PushVar(dummyForType);
            state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastRefPointer));
            state.CGenPopStackSize();
            // op is on top of the stack. pop it and push PointerInitialiser
            state.FunctionState.Pop();
            op = state.FunctionState.Push(Operand.Create(state.PointerInitialiser));
            return Operand.Create(op.Variable, 0);
        }

        public override Operand CGenAddress(CGenState state, Operand retLoc)
        {
            return Expr.CGenValue(state, retLoc);
        }

        public override bool CallerNeedsToCleanStack(CGenState state, bool retLocKnown, bool forAddress = false)
        {
            if (Expr.CallerNeedsToCleanStack(state, retLocKnown)) return true;

            var attr = Expr as Attribute;
            if (attr == null) return false;
            bool isUnion = ((StructOrUnionType)Expr.Type)?.IsStruct == false;
            if (!isUnion) return false;
            var ptr = (PointerType)Expr.Type;
            // we have ref u32
            if (!ptr.IsRef) return false;

            return true; // expects op to be at top of stack in this case
        }
    }
}
