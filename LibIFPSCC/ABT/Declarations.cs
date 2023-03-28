using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using CodeGeneration;
using IFPSLib.Emit;
using Decls = IFPSLib.Emit.FDecl;

namespace ABT {
    public enum StorageClass {
        AUTO,
        STATIC,
        EXTERN,
        TYPEDEF
    }

    public sealed class Decln : IExternDecln {
        public int Line { get; private set; }
        public int Column { get; private set; }

        public void Copy(ILineInfo info)
        {
            Line = info.Line;
            Column = info.Column;
        }
        public Decln(String name, StorageClass scs, ExprType type, Option<Initr> initr) {
            this.name = name;
            this.scs = scs;
            this.type = type;
            this.initr = initr;
        }

        public override String ToString() {
            String str = "[" + this.scs + "] ";
            str += this.name;
            str += " : " + this.type;
            return str;
        }

        private static bool TypeNeedsWalking(ExprType type)
        {
            return type is IncompleteArrayType || type is ArrayType || type is StructOrUnionType;
        }

        /// <summary>
        /// Walks through the type tree and emits instructions to get the operand pointing to the element at <paramref name="offset"/> in a variable of type <paramref name="type"/>.
        /// </summary>
        /// <param name="insns">Instructions list to emit the instructions to</param>
        /// <param name="operand">On entry, reference to the global variable; on exit, reference to the operand to write to.</param>
        /// <param name="stackUsage">On exit, the amount of pop instructions to emit to clean up the stack.</param>
        /// <param name="offset">Offset to the variable.</param>
        /// <param name="type">Type of the base variable.</param>
        /// <exception cref="InvalidOperationException">Thrown if operation is unsupported for now.</exception>
        private static void EmitElementWalk(CGenState.IFunctionGen ctor, ref Operand operand, out int stackUsage, int offset, ExprType type)
        {
            stackUsage = 0;
            Operand local = null;
            ExprType lastType = null;
            uint baseOffset = 0;
            while (true)
            {
                var thisOffset = (uint)offset - baseOffset;
                switch (type)
                {
                    case IncompleteArrayType unbounded:
                        {
                            // This is an array, so get the element from the offset.
                            // If this is an array of structures, and the offset points into the middle of the structure, the correct index will still be calculated.
                            uint arrIdx = thisOffset / (uint)unbounded.ElemType.SizeOf;
                            if (TypeNeedsWalking(lastType))
                            {
                                // we need to pushvar or setptr the new operand.
                                if (stackUsage == 0)
                                {
                                    // we need to pushvar the operand
                                    local = ctor.PushVar(operand);
                                    stackUsage++;
                                }
                                else
                                {
                                    ctor.Function.Instructions.Add(Instruction.Create(OpCodes.SetPtr, local, operand));
                                }
                                operand = local;
                            }
                            operand = Operand.Create(operand.Variable, arrIdx);
                            baseOffset += (uint)unbounded.ElemType.SizeOf * arrIdx;
                            type = unbounded.ElemType;
                            break;
                        }
                    case ArrayType arr:
                        {
                            // This is an array, so get the element from the offset.
                            // If this is an array of structures, and the offset points into the middle of the structure, the correct index will still be calculated.
                            uint arrIdx = thisOffset / (uint)arr.ElemType.SizeOf;
                            if (TypeNeedsWalking(lastType))
                            {
                                // we need to pushvar or setptr the new operand.
                                if (stackUsage == 0)
                                {
                                    // we need to pushvar the operand
                                    local = ctor.PushVar(operand);
                                    stackUsage++;
                                }
                                else
                                {
                                    ctor.Function.Instructions.Add(Instruction.Create(OpCodes.SetPtr, local, operand));
                                }
                                operand = local;
                            }
                            operand = Operand.Create(operand.Variable, arrIdx);
                            baseOffset += (uint)arr.ElemType.SizeOf * arrIdx;
                            type = arr.ElemType;
                            break;
                        }
                    case StructOrUnionType record:
                        // This is a struct or a union.
                        // TODO: figure out how to do this for a union.
                        if (!record.IsStruct) throw new InvalidOperationException();
                        if (stackUsage == 0)
                        {
                            // we need to pushvar the operand
                            local = ctor.PushVar(operand);
                            operand = local;
                            stackUsage++;
                        } else if (TypeNeedsWalking(lastType))
                        {
                            ctor.Function.Instructions.Add(Instruction.Create(OpCodes.SetPtr, local, operand));
                            operand = local;
                        }
                        // Is the offset pointing to an element in this substruct?
                        var element = record.Attribs.Select((a, i) => (a, i)).FirstOrDefault((v) => v.a.offset == thisOffset);
                        // if this is pointing to the offset of a substruct then we need to recurse further
                        if (element.a != null && element.a.type.Kind != ExprTypeKind.ARRAY && element.a.type.Kind != ExprTypeKind.STRUCT_OR_UNION)
                        {
                            // found the actual operand!
                            operand = Operand.Create(operand.Variable, (uint)element.i);
                            type = element.a.type;
                            break;
                        }
                        // Find the structure element that contains the offset.
                        element = record.Attribs.Select((a, i) => (a, i)).FirstOrDefault(
                            (v) => v.a.offset > thisOffset && (v.a.offset + v.a.type.SizeOf) <= (thisOffset + type.SizeOf)
                        );
                        if (element.a == null) throw new InvalidOperationException();
                        baseOffset += (uint)element.a.offset;
                        operand = Operand.Create(operand.Variable, (uint)element.i);
                        type = element.a.type;
                        break;
                    default:
                        return;
                }
                lastType = type;
            }
        }

        public static void EmitInitialiser(Initr value, ExprType type, Operand op, CGenState state)
        {
            var prim = type.Kind != ExprTypeKind.STRUCT_OR_UNION && type.Kind != ExprTypeKind.INCOMPLETE_ARRAY && type.Kind != ExprTypeKind.ARRAY;
            var ptr = type as PointerType;
            var isArrOfConst = false;
            {
                var arr = type as IncompleteArrayType;
                PointerType basePtr = null;
                if (arr != null)
                {
                    basePtr = arr.ElemType as PointerType;
                    isArrOfConst = basePtr != null && basePtr.IsForOpenArray;
                }
            }
            var insns = state.CurrInsns;
            value.Iterate(type, (Int32 offset, Expr expr) =>
            {
                var stackUsage = 0;
                var operand = new Operand(op);
                state.CGenPushStackSize();
                if (!prim && !type.EqualType(expr.Type))
                {
                    // We have an offset, we need to emit the instructions required to be able to touch the element which may be in sub-structure.
                    EmitElementWalk(state.FunctionState, ref operand, out stackUsage, offset, type);
                }

                Operand ret = expr.CGenValue(state, operand);
                if (ret == operand)
                {
                    state.CGenPopStackSize();
                    return;
                }
                switch (expr.Type.Kind)
                {

                    case ExprTypeKind.POINTER:
                        if (ptr != null && offset == 0)
                        {
                            Operand ptrPush = null;
                            if (ret.Type == BytecodeOperandType.Variable)
                            {
                                if (ret.Variable.VarType == VariableType.Argument)
                                {
                                    ptrPush = ret;
                                }
                                else
                                {
                                    // we don't know where on the stack this is. do a pointer-to-pointer cast with the pointer initialiser.
                                    IFPSLib.Types.IType refType = null;
                                    if (!ptr.IsRef)
                                    {
                                        refType = state.TypeUByte;
                                    }
                                    else
                                    {
                                        refType = state.EmitType(ptr.RefType);
                                    }
                                    var dummyForType = state.FunctionState.PushType(refType);
                                    var dummyU32 = state.FunctionState.PushType(state.TypeU32);
                                    state.FunctionState.PushVar(ret);
                                    state.FunctionState.PushVar(dummyU32);
                                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastPointerRef));
                                    state.FunctionState.Pop();
                                    state.FunctionState.Pop();
                                    state.FunctionState.PushVar(Operand.Create(state.PointerInitialiser));
                                    state.FunctionState.Push(dummyU32);
                                    state.FunctionState.PushVar(dummyForType);
                                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastRefPointer));
                                    ptrPush = Operand.Create(state.PointerInitialiser, 0);
                                }
                            }
                            else
                            {
                                insns.Add(Instruction.Create(OpCodes.Assign, state.PointerInitialiser, Operand.Create(ret.IndexedVariable)));
                                ptrPush = Operand.Create(state.PointerInitialiser, 0);
                            }
                            state.CGenPopStackSize();
                            if (!ptr.IsRef)
                            {
                                var voidPtr = state.FunctionState.PushType(state.TypeU32);
                                state.CGenPushStackSize();
                                state.FunctionState.PushVar(ptrPush);
                                state.FunctionState.PushVar(voidPtr);
                                state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastPointerRef));
                                state.CGenPopStackSize();

                            }
                            else
                            {
                                state.CurrInsns.Add(Instruction.Create(OpCodes.SetPtr, operand, ptrPush));
                            }
                            state.CGenPushStackSize();
                        }
                        else
                        {
                            var currPtr = expr.Type as PointerType;
                            if (!currPtr.IsRef)
                            {
                                var voidPtr = state.FunctionState.PushType(state.TypeU32);
                                state.CGenPushStackSize();
                                state.FunctionState.PushVar(ret);
                                state.FunctionState.PushVar(voidPtr);
                                state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastPointerRef));
                                state.CGenPopStackSize();
                            }
                            else
                            {
                                insns.Add(Instruction.Create(OpCodes.SetPtr, operand, ret));
                            }
                        }
                        break;
                    case ExprTypeKind.CHAR:
                    case ExprTypeKind.UCHAR:
                    case ExprTypeKind.SHORT:
                    case ExprTypeKind.USHORT:
                    case ExprTypeKind.DOUBLE:
                    case ExprTypeKind.FLOAT:
                    case ExprTypeKind.LONG:
                    case ExprTypeKind.ULONG:
                    case ExprTypeKind.STRUCT_OR_UNION:
                    case ExprTypeKind.ANSI_STRING:
                    case ExprTypeKind.UNICODE_STRING:
                    case ExprTypeKind.S64:
                    case ExprTypeKind.U64:
                    case ExprTypeKind.COM_INTERFACE:
                    case ExprTypeKind.COM_VARIANT:
                        insns.Add(Instruction.Create(isArrOfConst ? OpCodes.Cpval : OpCodes.Assign, operand, ret));
                        break;

                    case ExprTypeKind.ARRAY:
                    case ExprTypeKind.FUNCTION:
                        throw new InvalidProgramException($"How could a {expr.Type.Kind} be in a init list?").Attach(expr);

                    default:
                        throw new InvalidProgramException();
                }

                state.CGenPopStackSize();

            });
        }

        private void EnsureAttributeCountEquals(AST.TypeAttrib attrib, int count)
        {
            if (attrib.Args.Count != count)
                throw new InvalidProgramException(String.Format("{0} attribute on extern function {1} requires {2} {3}",
                    attrib.Name, this.name,
                    count, count == 1 ? "argument" : "arguments")).Attach(attrib);
        }

        private void EnsureAttributeCountAtLeast(AST.TypeAttrib attrib, int count)
        {
            if (attrib.Args.Count < count)
                throw new InvalidProgramException(String.Format("{0} attribute on extern function {1} requires at least {2} {3}",
                    attrib.Name, this.name,
                    count, count == 1 ? "argument" : "arguments")).Attach(attrib);
        }

        private void EnsureAttributeCountAtMost(AST.TypeAttrib attrib, int count)
        {
            if (attrib.Args.Count > count)
                throw new InvalidProgramException(String.Format("{0} attribute on extern function {1} requires at most {2} {3}",
                    attrib.Name, this.name,
                    count, count == 1 ? "argument" : "arguments")).Attach(attrib);
        }

        private T EnsureAttributeIs<T>(AST.TypeAttrib attrib, int index)
            where T : AST.Expr
        {
            var arg = attrib.Args[index] as T;
            if (arg != null) return arg;
            throw new InvalidProgramException(string.Format("{0} attribute on extern function {1} requires argument {2} to be of type {3}",
                attrib.Name, this.name, index, typeof(T).Name)).Attach(attrib);
        }

        private AST.IStringLiteral EnsureAttributeIsString(AST.TypeAttrib attrib, int index)
        {
            var arg = attrib.Args[index] as AST.IStringLiteral;
            if (arg != null) return arg;
            throw new InvalidProgramException(string.Format("{0} attribute on extern function {1} requires argument {2} to be string literal",
                attrib.Name, this.name, index)).Attach(attrib);
        }

        private static readonly ImmutableDictionary<string, NativeCallingConvention> s_CCTable = new Dictionary<string, NativeCallingConvention>()
        {
            { "__fastcall", NativeCallingConvention.Register },
            { "__pascal", NativeCallingConvention.Pascal },
            { "__cdecl", NativeCallingConvention.CDecl },
            { "__stdcall", NativeCallingConvention.Stdcall }
        }.ToImmutableDictionary();


        // * function;
        // * extern function;
        // * static function;
        // * obj;
        // * obj = Init;
        // * static obj;
        // * static obj = Init;
        // * extern obj;
        // * extern obj = Init;
        public void CGenDecln(Env env, CGenState state)
        {
            if (scs == StorageClass.TYPEDEF)
            {
                // For a typedef that can be named, change the name.
                var namableType = type as IExprTypeWithName;
                if (namableType != null)
                {
                    if (string.IsNullOrEmpty(namableType.TypeName) || namableType.TypeName.Contains(' ')) state.ChangeTypeName(namableType, name);
                }
                return;
            }

            if (type.Kind == ExprTypeKind.FUNCTION && env.IsGlobal())
            {
                // This is a global function.
                // We need to add this as the relevant extern...

                var attribs = type.TypeAttribs;

                var externTypes = attribs.Where((a) =>
                {
                    var n = a.Name;
                    return n == "__internal" || n == "__dll" || n == "__class" || n == "__com";
                });

                var ccTypes = attribs.Where((a) => s_CCTable.ContainsKey(a.Name));

                var externType = externTypes.FirstOrDefault();
                if (externType == null)
                {
                    // no extern attribute found
                    // if this is AUTO storage class, assume it's just a prototype for later.
                    if (scs == StorageClass.AUTO)
                    {
                        state.DeclareFunction(name, (FunctionType)type, scs);
                        return;
                    }
                    throw new InvalidProgramException(String.Format("Extern function {0} needs attributes to determine what is being imported", this.name)).Attach(this);
                }
                if (externTypes.Skip(1).Any()) throw new InvalidProgramException(String.Format("More than one extern attribute is present on function {0}", this.name)).Attach(this);


                var ccType = ccTypes.FirstOrDefault();
                if (externType.Name != "__internal")
                {
                    // default = cdecl
                    if (ccType == null) ccType = AST.TypeAttrib.Create((AST.Expr)AST.Variable.Create("__cdecl"));
                    else if (ccTypes.Skip(1).Any()) throw new InvalidProgramException(String.Format("More than one calling convention attribute is present on function {0}", this.name)).Attach(this);
                }

                var ext = new ExternalFunction();
                ext.Exported = true;

                switch (externType.Name)
                {
                    case "__internal":
                        EnsureAttributeCountEquals(externType, 0);
                        ext.Declaration = new Decls.Internal();
                        break;
                    case "__dll":
                        EnsureAttributeCountAtLeast(externType, 2);
                        EnsureAttributeCountAtMost(externType, 4);

                        var dll = new Decls.DLL();
                        dll.DllName = EnsureAttributeIsString(externType, 0).Value;
                        dll.ProcedureName = EnsureAttributeIsString(externType, 1).Value;

                        if (externType.Args.Count > 2)
                        {
                            var nextVal = EnsureAttributeIs<AST.Variable>(externType, 2).Name;
                            switch (nextVal)
                            {
                                case "delayload":
                                    dll.DelayLoad = true;
                                    break;
                                case "alteredsearchpath":
                                    dll.LoadWithAlteredSearchPath = true;
                                    break;
                                default:
                                    throw new InvalidProgramException(String.Format("Unknown DLL attribute value {0} in extern function {1}", nextVal, name)).Attach(externType);
                            }
                            if (externType.Args.Count > 3)
                            {
                                nextVal = EnsureAttributeIs<AST.Variable>(externType, 3).Name;
                                switch (nextVal)
                                {
                                    case "delayload":
                                        dll.DelayLoad = true;
                                        break;
                                    case "alteredsearchpath":
                                        dll.LoadWithAlteredSearchPath = true;
                                        break;
                                    default:
                                        throw new InvalidProgramException(String.Format("Unknown DLL attribute value {0} in extern function {1}", nextVal, name)).Attach(externType);
                                }
                            }
                        }

                        dll.CallingConvention = s_CCTable[ccType.Name];
                        ext.Declaration = dll;
                        break;
                    case "__class":
                        EnsureAttributeCountAtLeast(externType, 2);
                        EnsureAttributeCountAtMost(externType, 3);

                        var cls = new Decls.Class();
                        cls.ClassName = EnsureAttributeIsString(externType, 0).Value;
                        cls.FunctionName = EnsureAttributeIsString(externType, 1).Value;
                        if (externType.Args.Count == 3)
                        {
                            var nextVal = EnsureAttributeIs<AST.Variable>(externType, 2).Name;
                            if (nextVal != "property")
                                throw new InvalidProgramException(string.Format("Unknown class attribute value {0} in extern function {1}", nextVal, name)).Attach(externType);
                            cls.IsProperty = true;
                        }

                        cls.CallingConvention = s_CCTable[ccType.Name];
                        ext.Declaration = cls;
                        break;
                    case "__com":
                        EnsureAttributeCountEquals(externType, 1);

                        var functype = (FunctionType)type;
                        if (functype.Args.Count == 0) throw new InvalidProgramException(string.Format("COM function must have at least one argument in extern function {0}", name)).Attach(externType);
                        if (functype.Args[0].type.Kind != ExprTypeKind.COM_INTERFACE) throw new InvalidProgramException(string.Format("COM function's first argument must be of COM interface type in extern function {0}", name)).Attach(externType);

                        var com = new Decls.COM();
                        var idx = EnsureAttributeIs<AST.IntLiteral>(externType, 0).Value;
                        if (idx < 0) throw new InvalidProgramException(string.Format("COM vtable index must be positive in extern function {0}", name)).Attach(externType);
                        com.VTableIndex = (uint)idx;

                        com.CallingConvention = s_CCTable[ccType.Name];
                        ext.Declaration = com;
                        break;
                }

                if (externType.Name == "__internal" && name.ToLower() == "vartype")
                {
                    // make sure there is exactly ONE definition of VarType :)
                    state.VarType.Name.ToLower();
                    return;
                }
                if (externType.Name == "__internal" && name.ToLower() == "setarraylength")
                {
                    // make sure there is exactly ONE definition of SetArrayLength :)
                    state.SetArrayLength.Name.ToLower();
                    return;
                }

                state.DeclareExternalFunction(name, ext, (FunctionType)type, this);
                if (externType.Name == "__com")
                {
                    // remove the first argument
                    ext.Arguments.RemoveAt(0);
                }
                return;
            }

            if (env.IsGlobal())
            {

                // This is a global, which may be initialised to a value.
                // PascalScript can't do initialised globals, so we need a constructor function to set all of them.

                // First, create the global.
                GlobalVariable global = null;
                int? numElems = null;
                if (type.Kind == ExprTypeKind.INCOMPLETE_ARRAY)
                {
                    numElems = ((IncompleteArrayType)type).DeclaratorElems;
                }
                if (global == null) global = state.CreateGlobal(type, name, this);

                if (this.initr.IsSome) // TODO: need to deal with global pointers, global structures containing pointers
                {
                    // Initialised global.
                    // Emit the code for this.

                    var ctor = state.Constructor;
                    var insns = ctor.Function.Instructions;

                    // If this is a primitive type then we can just emit a single instruction.
                    // Otherwise, we need to emit an instruction for all elements.

                    var isPrim = type.Kind != ExprTypeKind.STRUCT_OR_UNION && type.Kind != ExprTypeKind.INCOMPLETE_ARRAY && type.Kind != ExprTypeKind.ARRAY;
                    var value = initr.Value;

                    if (numElems.HasValue)
                    {
                        // this is an incomplete array, so set its length

                        state.CGenPushStackSize();
                        state.FunctionState.Push(Operand.Create(numElems.Value));
                        state.FunctionState.PushVar(new Operand(global));
                        state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.SetArrayLength));
                        state.CGenPopStackSize();
                    }

                    value.Iterate(this.type, (Int32 offset, Expr expr) =>
                    {
                        if (!expr.IsConstExpr)
                        {
                            throw new InvalidOperationException("Cannot initialize with non-const expression.").Attach(expr);
                        }

                        var operand = new IFPSLib.Emit.Operand(global);
                        var stackUsage = 0;
                        if (!isPrim && !type.EqualType(expr.Type))
                        {
                            // We have an offset, we need to emit the instructions required to be able to touch the element which may be in sub-structure.
                            EmitElementWalk(ctor, ref operand, out stackUsage, offset, type);
                        }

                        switch (expr.Type.Kind)
                        {
                            // TODO: without const char/short, how do I initialize?
                            case ExprTypeKind.CHAR:
                                insns.Add(Instruction.Create(OpCodes.Assign, operand, Operand.Create((sbyte)((ConstLong)expr).Value)));
                                break;
                            case ExprTypeKind.UCHAR:
                                insns.Add(Instruction.Create(OpCodes.Assign, operand, Operand.Create((byte)((ConstLong)expr).Value)));
                                break;
                            case ExprTypeKind.SHORT:
                                insns.Add(Instruction.Create(OpCodes.Assign, operand, Operand.Create((short)((ConstLong)expr).Value)));
                                break;
                            case ExprTypeKind.USHORT:
                                insns.Add(Instruction.Create(OpCodes.Assign, operand, Operand.Create((ushort)((ConstLong)expr).Value)));
                                break;
                            case ExprTypeKind.LONG:
                                insns.Add(Instruction.Create(OpCodes.Assign, operand, Operand.Create(((ConstLong)expr).Value)));
                                break;
                            case ExprTypeKind.ULONG:
                                insns.Add(Instruction.Create(OpCodes.Assign, operand, Operand.Create(((ConstULong)expr).Value)));
                                break;

                            case ExprTypeKind.S64:
                                insns.Add(Instruction.Create(OpCodes.Assign, operand, Operand.Create(((ConstS64)expr).Value)));
                                break;
                            case ExprTypeKind.U64:
                                insns.Add(Instruction.Create(OpCodes.Assign, operand, Operand.Create(((ConstU64)expr).Value)));
                                break;

                            case ExprTypeKind.POINTER:
                                // A global pointer is a Pointer[1]
                                var ptrType = expr.Type as PointerType;
                                if (ptrType == null) throw new InvalidProgramException().Attach(expr);
                                if (!ptrType.IsRef)
                                {
                                    // void*==u32
                                    insns.Add(Instruction.Create(OpCodes.Assign, operand, Operand.Create(((ConstPtr)expr).Value)));
                                    break;
                                }
                                var dummyForType = ctor.PushType(state.EmitType(ptrType.RefType));
                                ctor.PushVar(operand);
                                ctor.Push(Operand.Create(((ConstPtr)expr).Value));
                                ctor.PushVar(dummyForType);
                                insns.Add(Instruction.Create(OpCodes.Call, state.CastRefPointer));
                                ctor.Pop();
                                ctor.Pop();
                                ctor.Pop();
                                ctor.Pop();
                                break;

                            case ExprTypeKind.FLOAT:
                                insns.Add(Instruction.Create(OpCodes.Assign, operand, Operand.Create(((ConstFloat)expr).Value)));
                                break;

                            case ExprTypeKind.DOUBLE:
                                insns.Add(Instruction.Create(OpCodes.Assign, operand, Operand.Create(((ConstDouble)expr).Value)));
                                break;

                            case ExprTypeKind.ANSI_STRING:
                                insns.Add(Instruction.Create(OpCodes.Assign, operand, Operand.Create(new IFPSLib.Types.PrimitiveType(IFPSLib.Types.PascalTypeCode.String), ((ConstStringLiteral)expr).Value)));
                                break;

                            case ExprTypeKind.UNICODE_STRING:
                                insns.Add(Instruction.Create(OpCodes.Assign, operand, Operand.Create(((ConstUnicodeStringLiteral)expr).Value)));
                                break;

                            default:
                                throw new InvalidProgramException().Attach(expr);
                        }

                        for (int i = 0; i < stackUsage; i++) insns.Add(Instruction.Create(OpCodes.Pop));

                    });
                }

            }
            else
            {
                // stack object

                Int32 stack_size = env.StackSize;

                // pos should be equal to stack_size, but whatever...
                var envVar = env.Find(this.name);
                Int32 pos = envVar.Value.Offset;
                var ptr = type as PointerType;
                if (ptr != null && ptr.IsRef)
                {
                    // this is ref type. pushtype Pointer.
                    state.FunctionState.PushType(state.TypePointer);
                }
                else if (type.Kind == ExprTypeKind.INCOMPLETE_ARRAY)
                {
                    var arr = type as IncompleteArrayType;
                    var arr_op = state.FunctionState.PushType(state.EmitType(arr));
                    // emit SetArrayLength(&arr_op, length)
                    state.CGenPushStackSize();
                    state.FunctionState.Push(Operand.Create(arr.DeclaratorElems.Value));
                    state.FunctionState.PushVar(arr_op);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.SetArrayLength));
                    state.CGenPopStackSize();
                }
                else
                {
                    state.FunctionState.PushType(state.EmitType(this.type));
                }

                if (this.initr.IsNone)
                {
                    return;
                }


                var local = LocalVariable.Create(pos);
                Initr value = this.initr.Value;
                EmitInitialiser(value, type, new IFPSLib.Emit.Operand(local), state);

            } // stack object
        }

        private readonly String name;
        private readonly StorageClass scs;
        private readonly ExprType type;
        private readonly Option<Initr> initr;
    }

    

    /// <summary>
    /// 1. Scalar: an expression, optionally enclosed in braces.
    ///    int a = 1;              // valid
    ///    int a = { 1 };          // valid
    ///    int a[] = { { 1 }, 2 }; // valid
    ///    int a = {{ 1 }};        // warning in gcc, a == 1; error in MSVC
    ///    int a = { { 1 }, 2 };   // warning in gcc, a == 1; error in MSVC
    ///    int a = { 1, 2 };       // warning in gcc, a == 1; error in MSVC
    ///    I'm following MSVC: you either put an expression, or add a single layer of brace.
    /// 
    /// 2. Union:
    ///    union A { int a; int b; };
    ///    union A u = { 1 };               // always initialize the first member, i.e. a, not b.
    ///    union A u = {{ 1 }};             // valid
    ///    union A u = another_union;       // valid
    /// 
    /// 3. Struct:
    ///    struct A { int a; int b; };
    ///    struct A = another_struct;       // valid
    ///    struct A = { another_struct };   // error, once you put a brace, the compiler assumes you want to initialize members.
    /// 
    /// From 2 and 3, once seen union or struct, either read expression or brace.
    /// 
    /// 4. Array of characters:
    ///    char a[] = { 'a', 'b' }; // valid
    ///    char a[] = "abc";        // becomes char a[4]: include '\0'
    ///    char a[3] = "abc";       // valid, ignore '\0'
    ///    char a[2] = "abc";       // warning in gcc; error in MSVC
    ///    If the aggregate contains members that are aggregates or unions, or if the first member of a union is an aggregate or union, the rules apply recursively to the subaggregates or contained unions. If the initializer of a subaggregate or contained union begins with a left brace, the initializers enclosed by that brace and its matching right brace initialize the members of the subaggregate or the first member of the contained union. Otherwise, only enough initializers from the list are taken to account for the members of the first subaggregate or the first member of the contained union; any remaining initializers are left to initialize the next member of the aggregate of which the current subaggregate or contained union is a part.
    /// </summary>
    public abstract class Initr {
        public enum Kind {
            EXPR,
            INIT_LIST
        }
        public abstract Kind kind { get; }

        public abstract Initr ConformType(MemberIterator iter);

        public Initr ConformType(ExprType type) => ConformType(new MemberIterator(type));

        public abstract void Iterate(MemberIterator iter, Action<Int32, Expr> action);

        public void Iterate(ExprType type, Action<Int32, Expr> action) => Iterate(new MemberIterator(type), action);
    }

    public class InitExpr : Initr {
        public InitExpr(Expr expr) {
            this.expr = expr;
        }
        public readonly Expr expr;
        public override Kind kind => Kind.EXPR;

        public override Initr ConformType(MemberIterator iter) {
            iter.Locate(this.expr.Type);
            Expr expr = TypeCast.MakeCast(this.expr, iter.CurType);
            return new InitExpr(expr);
        }

        public override void Iterate(MemberIterator iter, Action<Int32, Expr> action) {
            iter.Locate(this.expr.Type);
            Int32 offset = iter.CurOffset;
            Expr expr = this.expr;
            action(offset, expr);
        }
    }

    public class InitList : Initr {
        public InitList(List<Initr> initrs) {
            this.initrs = initrs;
        }
        public override Kind kind => Kind.INIT_LIST;
        public readonly List<Initr> initrs;

        public override Initr ConformType(MemberIterator iter) {
            iter.InBrace();
            List<Initr> initrs = new List<Initr>();
            for (Int32 i = 0; i < this.initrs.Count; ++i) {
                initrs.Add(this.initrs[i].ConformType(iter));
                if (i != this.initrs.Count - 1) {
                    iter.Next();
                }
            }
            iter.OutBrace();
            return new InitList(initrs);
        }

        public override void Iterate(MemberIterator iter, Action<Int32, Expr> action) {
            iter.InBrace();
            for (Int32 i = 0; i < this.initrs.Count; ++i) {
                this.initrs[i].Iterate(iter, action);
                if (i != this.initrs.Count - 1) {
                    iter.Next();
                }
            }
            iter.OutBrace();
        }
    }

    public class MemberIterator {
        public MemberIterator(ExprType type) {
            this.trace = new List<Status> { new Status(type) };
        }

        public class Status {
            public Status(ExprType base_type) {
                this.base_type = base_type;
                this.indices = new List<Int32>();
            }

            public ExprType CurType => GetType(this.base_type, this.indices);

            public Int32 CurOffset => GetOffset(this.base_type, this.indices);

            //public List<Tuple<ExprType, Int32>> GetPath(ExprType base_type, IReadOnlyList<Int32> indices) {
            //    ExprType Type = base_type;
            //    List<Tuple<ExprType, Int32>> path = new List<Tuple<ExprType, int>>();
            //    foreach (Int32 index in indices) {
            //        switch (Type.Kind) {
            //            case ExprType.Kind.ARRAY:
            //                Type = ((ArrayType)Type).ElemType;
            //                break;
            //            case ExprType.Kind.INCOMPLETE_ARRAY:
            //            case ExprType.Kind.STRUCT_OR_UNION:
            //            default:
            //                throw new InvalidProgramException("Not an aggregate Type.");
            //        }
            //    }
            //}

            public static ExprType GetType(ExprType from_type, Int32 to_index) {
                switch (from_type.Kind) {
                    case ExprTypeKind.ARRAY:
                        return ((ArrayType)from_type).ElemType;

                    case ExprTypeKind.INCOMPLETE_ARRAY:
                        return ((IncompleteArrayType)from_type).ElemType;

                    case ExprTypeKind.STRUCT_OR_UNION:
                        return ((StructOrUnionType)from_type).Attribs[to_index].type;

                    default:
                        throw new InvalidProgramException("Not an aggregate Type.");
                }
            }

            public static ExprType GetType(ExprType base_type, IReadOnlyList<Int32> indices) =>
                indices.Aggregate(base_type, GetType);

            public static Int32 GetOffset(ExprType from_type, Int32 to_index) {
                switch (from_type.Kind) {
                    case ExprTypeKind.ARRAY:
                        return to_index * ((ArrayType)from_type).ElemType.SizeOf;

                    case ExprTypeKind.INCOMPLETE_ARRAY:
                        return to_index * ((IncompleteArrayType)from_type).ElemType.SizeOf;

                    case ExprTypeKind.STRUCT_OR_UNION:
                        return ((StructOrUnionType)from_type).Attribs[to_index].offset;

                    default:
                        throw new InvalidProgramException("Not an aggregate Type.");
                }
            }

            public static Int32 GetOffset(ExprType base_type, IReadOnlyList<Int32> indices) {
                Int32 offset = 0;
                ExprType from_type = base_type;
                foreach (Int32 to_index in indices) {
                    offset += GetOffset(from_type, to_index);
                    from_type = GetType(from_type, to_index);
                }
                return offset;
            }

            public List<ExprType> GetTypes(ExprType base_type, IReadOnlyList<Int32> indices) {
                List<ExprType> types = new List<ExprType> { base_type };
                ExprType from_type = base_type;
                foreach (Int32 to_index in indices) {
                    from_type = GetType(from_type, to_index);
                    types.Add(from_type);
                }
                return types;
            }

            public void Next() {

                // From base_type to CurType.
                List<ExprType> types = GetTypes(this.base_type, this.indices);

                // We try to jump as many levels out as we can.
                do {
                    Int32 index = this.indices.Last();
                    this.indices.RemoveAt(this.indices.Count - 1);

                    types.RemoveAt(types.Count - 1);
                    ExprType type = types.Last();

                    switch (type.Kind) {
                        case ExprTypeKind.ARRAY:
                            if (index < ((ArrayType)type).NumElems - 1) {
                                // There are more elements in the array.
                                this.indices.Add(index + 1);
                                return;
                            }
                            break;

                        case ExprTypeKind.INCOMPLETE_ARRAY:
                            this.indices.Add(index + 1);
                            return;

                        case ExprTypeKind.STRUCT_OR_UNION:
                            if (((StructOrUnionType)type).IsStruct && index < ((StructOrUnionType)type).Attribs.Count - 1) {
                                // There are more members in the struct.
                                // (not union, since we can only initialize the first member of a union)
                                this.indices.Add(index + 1);
                                return;
                            }
                            break;

                        default:
                            break;
                    }

                } while (this.indices.Any());
            }

            /// <summary>
            /// Read an expression in the initializer list, locate the corresponding position.
            /// </summary>
            public void Locate(ExprType type) {
                switch (type.Kind) {
                    case ExprTypeKind.STRUCT_OR_UNION:
                        LocateStruct((StructOrUnionType)type);
                        return;
                    default:
                        // Even if the expression is of array Type, treat it as a scalar (pointer).
                        LocateScalar();
                        return;
                }
            }

            /// <summary>
            /// Try to match a scalar.
            /// This step doesn't check what scalar it is. Further steps would perform implicit conversions.
            /// </summary>
            private void LocateScalar() {
                while (!this.CurType.IsScalar) {
                    this.indices.Add(0);
                }
            }

            /// <summary>
            /// Try to match a given struct.
            /// Go down to find the first element of the same struct Type.
            /// </summary>
            private void LocateStruct(StructOrUnionType type) {
                while (!this.CurType.EqualType(type)) {
                    if (this.CurType.IsScalar) {
                        throw new InvalidOperationException("Trying to match a struct or union, but found a scalar.");
                    }

                    // Go down one level.
                    this.indices.Add(0);
                }
            }

            public readonly ExprType base_type;
            public readonly List<Int32> indices;
        }

        public ExprType CurType => this.trace.Last().CurType;

        public Int32 CurOffset => this.trace.Select(_ => _.CurOffset).Sum();

        public void Next() => this.trace.Last().Next();

        public void Locate(ExprType type) => this.trace.Last().Locate(type);

        public void InBrace() {

            /// Push the current position into the stack, so that we can get back by <see cref="OutBrace"/>
            this.trace.Add(new Status(this.trace.Last().CurType));

            // For aggregate types, go inside and locate the first member.
            if (!this.CurType.IsScalar) {
                this.trace.Last().indices.Add(0);
            }
            
        }

        public void OutBrace() => this.trace.RemoveAt(this.trace.Count - 1);

        public readonly List<Status> trace;
    }
}
