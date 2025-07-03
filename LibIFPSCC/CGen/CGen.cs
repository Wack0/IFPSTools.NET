using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using IFPSLib;
using Emit = IFPSLib.Emit;
using Types = IFPSLib.Types;

namespace CodeGeneration {

    internal static class OperandEquator
    {
        internal static bool Equals(Emit.IVariable lhs, Emit.IVariable rhs)
        {
            if (lhs == rhs) return true;
            if (lhs == null || rhs == null) return false;
            if (lhs.VarType != rhs.VarType) return false;
            return lhs.Index == rhs.Index;
        }
        internal static bool Equals(Emit.Operand lhs, Emit.Operand rhs)
        {
            if (lhs == rhs) return true;
            if (lhs == null || rhs == null) return false;
            if (lhs.Type != rhs.Type) return false;
            switch (lhs.Type)
            {
                case Emit.BytecodeOperandType.Immediate:
                    return lhs.Immediate == rhs.Immediate;
                case Emit.BytecodeOperandType.Variable:
                    return Equals(lhs.Variable, rhs.Variable);
                case Emit.BytecodeOperandType.IndexedImmediate:
                    return Equals(lhs.IndexedVariable, rhs.IndexedVariable) && lhs.IndexImmediate == rhs.IndexImmediate;
                case Emit.BytecodeOperandType.IndexedVariable:
                    return Equals(lhs.IndexedVariable, rhs.IndexedVariable) && Equals(lhs.IndexVariable, rhs.IndexVariable);
                default:
                    return false;
            }
        }
    }

    public class CGenState {
        public interface IFunctionGen
        {
            Emit.ScriptFunction Function { get; }
            List<Emit.Instruction> Labels { get; }

            Dictionary<int, int> LabelToIndex { get; }
            Dictionary<int, int> IndexToLabel { get; }

            int LocalsCount { get; }

            Emit.Operand PushType(Types.IType type);
            Emit.Operand PushVar(Emit.Operand op);
            Emit.Operand Push(Emit.Operand op);

            Emit.Operand AddLocal();
            void Pop();
            void PopForRevert();
        }
        private sealed class FunctionGen : IFunctionGen
        {
            public Emit.ScriptFunction Function { get; }
            public List<Emit.Instruction> Labels { get; } = new List<Emit.Instruction>();

            public Dictionary<int, int> LabelToIndex { get; } = new Dictionary<int, int>();
            public Dictionary<int, int> IndexToLabel { get; } = new Dictionary<int, int>();

            public int LocalsCount { get; private set; }

            private Emit.Operand PushCore(Emit.Instruction insn)
            {
                Function.Instructions.Add(insn);
                return AddLocal();
            }

            public Emit.Operand PushType(Types.IType type) => PushCore(Emit.Instruction.Create(Emit.OpCodes.PushType, type));
            public Emit.Operand PushVar(Emit.Operand op) => PushCore(Emit.Instruction.Create(Emit.OpCodes.PushVar, op));
            public Emit.Operand Push(Emit.Operand op) => PushCore(Emit.Instruction.Create(Emit.OpCodes.Push, op));

            public Emit.Operand AddLocal()
            {
                var ret = Emit.Operand.Create(Emit.LocalVariable.Create(LocalsCount));
                LocalsCount++;
                return ret;
            }

            public void Pop()
            {
                Function.Instructions.Add(Emit.Instruction.Create(Emit.OpCodes.Pop));
                LocalsCount--;
            }

            public void PopForRevert()
            {
                LocalsCount--;
            }

            public FunctionGen(string name, ABT.StorageClass scs)
            {
                Function = new Emit.ScriptFunction();
                Function.Name = name;
                Function.Exported = scs != ABT.StorageClass.STATIC;
                // caller must deal with arguments.
            }
        }
        public Script Script { get; } = new Script();
        private IFunctionGen m_Constructor = null;
        public IFunctionGen FunctionState { get; private set; } = null;
        public IList<Emit.Instruction> CurrInsns => FunctionState?.Function.Instructions;
        private Dictionary<string, IFunctionGen> m_Functions = new Dictionary<string, IFunctionGen>();
        private Dictionary<string, Emit.ExternalFunction> m_FunctionsExternal = new Dictionary<string, Emit.ExternalFunction>();
        private Types.IType m_TypePointer = null;
        private Types.IType m_TypeUByte = null;
        private Types.IType m_TypeU16 = null;
        private Types.IType m_TypeU32 = null;
        private Types.IType m_TypeIUnknown = null;
        private Types.IType m_TypeIDispatch = null;

        // Additional runtime functions/types required.
        /// <summary>
        /// Imports a stubbed function in ntdll.dll (just returns);
        /// This can be used with fastcall calling convention to get the underlying pointer of a value passed by reference
        /// </summary>
        private Emit.ExternalFunction m_CastPointerRef = null;
        /// <summary>
        /// Imports ntdll!RtlMoveMemory, copying between two pointers passed as by-reference variables
        /// </summary>
        private Emit.ExternalFunction m_RtlMoveMemoryRef = null;
        /// <summary>
        /// Imports ntdll!RtlMoveMemory, copying between two pointers passed as values.
        /// </summary>
        private Emit.ExternalFunction m_RtlMoveMemoryVal = null;

        /// <summary>
        /// 1-length array of pointer, used to work around the inability to create a raw Pointer in the runtime (causes null deref if you try)
        /// </summary>
        private Types.IType m_TypeArrayOfPointer = null;
        /// <summary>
        /// <code>void CreateValidPointer(pointer_as_size_t pPtrValue, pointer_as_size_t pPtrType, ref ArrayOfPointer outPtr);</code>
        /// Creates a valid pointer in *outPtr, setting the pointer value to ptrValue and the pointer type to pPtrType.pType
        /// </summary>
        private Emit.ScriptFunction m_CreateValidPointer = null;
        /// <summary>
        /// <code>void CastRefPointer(ref anytype varForType, size_t ptrValue, ref ArrayOfPointer outPtr)</code>
        /// Creates a valid pointer in *outPtr, setting the pointer value to ptrValue and the pointer type to ((pointer)varForType)->pType
        /// Basically a wrapper for CreateValidPointer(ptrValue, CastPointerRef(ref varForType), ref outPtr)
        /// </summary>
        private Emit.ScriptFunction m_CastRefPointer = null;
        /// <summary>
        /// <code>__interface Cast(__interface self, u32 typeIdx)</code>
        /// Casts a COM interface to another, typeIdx is used for internal class to internal class cast and not for COM interface casting.
        /// </summary>
        private Emit.ExternalFunction m_ComInterfaceCast = null;
        /// <summary>
        /// <code>u16 VarType(__variant self)</code>
        /// Gets the type of a Variant. Used for casting interfaces, we need to know if the type is IUnknown or IDispatch because of lack of support in runtime.
        /// </summary>
        private Emit.ExternalFunction m_VarType = null;
        /// <summary>
        /// <code>void SetArrayLength(TArray* array, s32 length)</code>
        /// Sets the length of an unbounded array at runtime.
        /// </summary>
        private Emit.ExternalFunction m_SetArrayLength = null;

        /// <summary>
        /// When initialising pointers, a global ArrayOfPointer is needed.
        /// The pointer needs to be stored in the expected stack local.
        /// </summary>
        private Emit.GlobalVariable m_PointerInitialiser = null;

        public Emit.GlobalVariable PointerInitialiser
        {
            get
            {
                if (m_PointerInitialiser == null)
                {
                    m_PointerInitialiser = Emit.GlobalVariable.Create(Script.GlobalVariables.Count, TypeArrayOfPointer, "__PointerInitialiser");
                    Script.GlobalVariables.Add(m_PointerInitialiser);
                }
                return m_PointerInitialiser;
            }
        }


        public IFunctionGen Constructor {
            get
            {
                if (m_Constructor == null)
                {
                    m_Constructor = new FunctionGen("__ctor", ABT.StorageClass.AUTO);
                    m_Constructor.Function.Arguments = new List<FunctionArgument>();
                    Script.Functions.Add(m_Constructor.Function);
                    Script.EntryPoint = m_Constructor.Function;
                    var runonce = CreateGlobal(TypeUByte, "__ctor_runonce");
                    var insns = m_Constructor.Function.Instructions;
                    var jumploc = Emit.Instruction.Create<byte>(Emit.OpCodes.Assign, runonce, 1);
                    insns.Add(Emit.Instruction.Create(Emit.OpCodes.JumpZ, runonce, jumploc));
                    insns.Add(Emit.Instruction.Create(Emit.OpCodes.Ret));
                    insns.Add(jumploc);
                }
                return m_Constructor;
            }
        }

        private Types.IType EnsureTypeCreated(ref Types.IType type, Types.PascalTypeCode code)
        {
            if (type == null)
            {
                type = new Types.PrimitiveType(code);
                Script.Types.Add(type);
            }
            return type;
        }

        private Emit.ExternalFunction EnsureDllImportedFunctionCreated(ref Emit.ExternalFunction func, string dllName, string export, Emit.NativeCallingConvention cc, bool hasReturnArgument, string name, IList<FunctionArgument> args)
        {
            if (func == null)
            {
                func = new Emit.ExternalFunction();
                var dll = new Emit.FDecl.DLL();
                dll.DllName = dllName;
                dll.ProcedureName = export;
                dll.CallingConvention = cc;
                func.Declaration = dll;
                func.Name = name;
                func.ReturnArgument = hasReturnArgument ? Types.UnknownType.Instance : null;
                func.Arguments = args;
                func.Exported = true;
                Script.Functions.Add(func);
            }
            return func;
        }

        private Types.IType EnsureTypeCreated<T>(ref Types.IType type)
        {
            return EnsureTypeCreated(ref type, Types.EnumHelpers.ToIFPSTypeCode(typeof(T)));
        }

        private Types.IType EnsureArrayTypeCreated(ref Types.IType type, Types.IType elem, int count, string name)
        {
            if (type == null)
            {
                type = new Types.StaticArrayType(elem, count);
                type.Name = name;
                Script.Types.Add(type);
            }
            return type;
        }

        public Types.IType TypePointer => EnsureTypeCreated(ref m_TypePointer, Types.PascalTypeCode.Pointer);
        public Types.IType TypeUByte => EnsureTypeCreated<byte>(ref m_TypeUByte);
        public Types.IType TypeU16 => EnsureTypeCreated<ushort>(ref m_TypeU16);
        public Types.IType TypeU32 => EnsureTypeCreated<uint>(ref m_TypeU32);
        public Types.IType TypeArrayOfPointer => EnsureArrayTypeCreated(ref m_TypeArrayOfPointer, TypePointer, 1, "__ArrayOfPointer");

        public Types.IType TypeIUnknown
        {
            get
            {
                if (m_TypeIUnknown == null)
                {
                    m_TypeIUnknown = new Types.ComInterfaceType(GUID_IUNKNOWN) { Name = "IUnknown" };
                    Script.Types.Add(m_TypeIUnknown);
                }
                return m_TypeIUnknown;
            }
        }

        public Types.IType TypeIDispatch
        {
            get
            {
                if (m_TypeIDispatch == null)
                {
                    m_TypeIDispatch = new Types.ComInterfaceType(GUID_IDISPATCH)
                    {
                        Name = "IDISPATCH",
                        Exported = true
                    };
                    Script.Types.Add(m_TypeIDispatch);
                }
                return m_TypeIDispatch;
            }
        }


        private static FunctionArgument CreateImportedFunctionArgument(FunctionArgumentType type) => new FunctionArgument()
        {
            ArgumentType = type,
            Type = Types.UnknownType.Instance
        };

        private static readonly IList<FunctionArgument> ARGS_CASTPOINTERREF = new FunctionArgument[] { CreateImportedFunctionArgument(FunctionArgumentType.Out) };
        private static readonly IList<FunctionArgument> ARGS_RTLMOVEMEMORYREF = new FunctionArgument[]
        {
            CreateImportedFunctionArgument(FunctionArgumentType.Out),
            CreateImportedFunctionArgument(FunctionArgumentType.Out),
            CreateImportedFunctionArgument(FunctionArgumentType.In)
        };
        private static readonly IList<FunctionArgument> ARGS_RTLMOVEMEMORYVAL = new FunctionArgument[]
        {
            CreateImportedFunctionArgument(FunctionArgumentType.In),
            CreateImportedFunctionArgument(FunctionArgumentType.In),
            CreateImportedFunctionArgument(FunctionArgumentType.In)
        };
        private static readonly IList<FunctionArgument> ARGS_COMINTERFACECAST = new FunctionArgument[]
        {
            CreateImportedFunctionArgument(FunctionArgumentType.In),
            CreateImportedFunctionArgument(FunctionArgumentType.In)
        };
        private static readonly IList<FunctionArgument> ARGS_VARTYPE = new FunctionArgument[]
        {
            CreateImportedFunctionArgument(FunctionArgumentType.In),
        };
        private static readonly IList<FunctionArgument> ARGS_EMPTY = new FunctionArgument[0];

        public IFunction CastPointerRef => EnsureDllImportedFunctionCreated(ref m_CastPointerRef, "ntdll.dll", "RtlDebugPrintTimes", Emit.NativeCallingConvention.Register, true, "__CastPointerRef", ARGS_CASTPOINTERREF);
        public IFunction RtlMoveMemoryRef => EnsureDllImportedFunctionCreated(ref m_RtlMoveMemoryRef, "ntdll.dll", "RtlMoveMemory", Emit.NativeCallingConvention.Stdcall, false, "__RtlMoveMemoryRef", ARGS_RTLMOVEMEMORYREF);
        public IFunction RtlMoveMemoryVal => EnsureDllImportedFunctionCreated(ref m_RtlMoveMemoryVal, "ntdll.dll", "RtlMoveMemory", Emit.NativeCallingConvention.Stdcall, false, "__RtlMoveMemoryVal", ARGS_RTLMOVEMEMORYVAL);

        public IFunction ComInterfaceCast
        {
            get
            {
                if (m_ComInterfaceCast == null)
                {
                    m_ComInterfaceCast = new Emit.ExternalFunction();
                    var cls = new Emit.FDecl.Class();
                    cls.ClassName = "Class";
                    cls.FunctionName = "CastToType";
                    cls.CallingConvention = Emit.NativeCallingConvention.Pascal;
                    m_ComInterfaceCast.Declaration = cls;
                    m_ComInterfaceCast.Name = "ComInterfaceCast";
                    m_ComInterfaceCast.ReturnArgument = Types.UnknownType.Instance;
                    m_ComInterfaceCast.Arguments = ARGS_COMINTERFACECAST;
                    m_ComInterfaceCast.Exported = true;
                    Script.Functions.Add(m_ComInterfaceCast);
                }
                return m_ComInterfaceCast;
            }
        }

        public IFunction VarType
        {
            get
            {
                if (m_FunctionsExternal.TryGetValue("VarType", out var func)) return func;
                if (m_VarType == null)
                {
                    m_VarType = new Emit.ExternalFunction();
                    var decl = new Emit.FDecl.Internal();
                    m_VarType.Declaration = decl;
                    m_VarType.Name = "VarType";
                    m_VarType.ReturnArgument = Types.UnknownType.Instance;
                    m_VarType.Arguments = ARGS_VARTYPE;
                    m_VarType.Exported = true;
                    Script.Functions.Add(m_VarType);
                    m_FunctionsExternal.Add("VarType", m_VarType);
                }
                return m_VarType;
            }
        }

        public IFunction SetArrayLength
        {
            get
            {
                if (m_FunctionsExternal.TryGetValue("SetArrayLength", out var func)) return func;
                if (m_SetArrayLength == null)
                {
                    m_SetArrayLength = new Emit.ExternalFunction();
                    var decl = new Emit.FDecl.Internal();
                    m_SetArrayLength.Declaration = decl;
                    m_SetArrayLength.Name = "SetArrayLength";
                    m_SetArrayLength.ReturnArgument = null;
                    m_SetArrayLength.Arguments = ARGS_EMPTY;
                    m_SetArrayLength.Exported = true;
                    Script.Functions.Add(m_SetArrayLength);
                    m_FunctionsExternal.Add("SetArrayLength", m_SetArrayLength);
                }
                return m_SetArrayLength;
            }
        }

        public IFunction CreateValidPointer
        {
            get
            {
                if (m_CreateValidPointer == null)
                {
                    var gen = new FunctionGen("__CreateValidPointer", ABT.StorageClass.STATIC);
                    m_CreateValidPointer = gen.Function;
                    m_CreateValidPointer.Arguments = new List<FunctionArgument>() {
                        new FunctionArgument()
                        {
                            ArgumentType = FunctionArgumentType.In,
                            Name = "pPtr",
                            Type = TypeU32
                        },
                        new FunctionArgument()
                        {
                            ArgumentType = FunctionArgumentType.In,
                            Name = "pObjTypeOf",
                            Type = TypeU32
                        },
                        new FunctionArgument()
                        {
                            ArgumentType = FunctionArgumentType.Out,
                            Name = "outPtr",
                            Type = TypeArrayOfPointer
                        }
                    };
                    m_CreateValidPointer.ReturnArgument = null;
                    var insns = m_CreateValidPointer.Instructions;
                    var pPtr = Emit.Operand.Create(m_CreateValidPointer.CreateArgumentVariable(0));
                    var pObjTypeOf = Emit.Operand.Create(m_CreateValidPointer.CreateArgumentVariable(1));
                    var outPtr = Emit.Operand.Create(m_CreateValidPointer.CreateArgumentVariable(2));
                    // outPtr[0].pPtr = pPtr
                    gen.Push(Emit.Operand.Create(ABT.ExprType.SIZEOF_POINTER)); // push SIZEOF_POINTER // 1
                    gen.PushVar(pPtr); // pushvar pPtr // 2
                    gen.PushVar(outPtr); // pushvar outPtr // 3
                    insns.Add(Emit.Instruction.Create(Emit.OpCodes.Call, RtlMoveMemoryRef)); // call RtlMoveMemoryRef
                    gen.Pop(); // 2
                    gen.Pop(); // 1
                    // outPtr[0].pType = pObjTypeOf.pType
                    var Var2 = gen.Push(pObjTypeOf).Variable; // push pObjTypeOf // 2
                    insns.Add(Emit.Instruction.Create<uint>(Emit.OpCodes.Sub, Var2, ABT.ExprType.SIZEOF_POINTER)); // sub Var2, SIZEOF_POINTER
                    var Var3 = gen.PushType(TypeU32); // pushtype U32 // 3
                    gen.PushVar(outPtr); // pushvar outPtr // 4
                    gen.PushVar(Var3); // pushvar Var3 // 5
                    insns.Add(Emit.Instruction.Create(Emit.OpCodes.Call, CastPointerRef)); // call CastPointerRef
                    gen.Pop(); // 4
                    gen.Pop(); // 3
                    insns.Add(Emit.Instruction.Create<uint>(Emit.OpCodes.Add, Var3.Variable, ABT.ExprType.SIZEOF_POINTER)); // add Var3, SIZEOF_POINTER
                    insns.Add(Emit.Instruction.Create(Emit.OpCodes.Call, RtlMoveMemoryVal));
                    // return;
                    insns.Add(Emit.Instruction.Create(Emit.OpCodes.Ret));

                    Script.Functions.Add(m_CreateValidPointer);
                }
                return m_CreateValidPointer;
            }
        }

        public IFunction CastRefPointer
        {
            get
            {
                if (m_CastRefPointer == null)
                {
                    var gen = new FunctionGen("__CastRefPointer", ABT.StorageClass.STATIC);
                    m_CastRefPointer = gen.Function;
                    m_CastRefPointer.Arguments = new List<FunctionArgument>() {
                        new FunctionArgument()
                        {
                            ArgumentType = FunctionArgumentType.Out,
                            Name = "pType",
                            Type = TypeU32 // actually anytype, runtime doesn't care about the type :)
                        },
                        new FunctionArgument()
                        {
                            ArgumentType = FunctionArgumentType.In,
                            Name = "pVal",
                            Type = TypeU32
                        },
                        new FunctionArgument()
                        {
                            ArgumentType = FunctionArgumentType.Out,
                            Name = "outPtr",
                            Type = TypeArrayOfPointer
                        }
                    };
                    m_CastRefPointer.ReturnArgument = null;
                    var insns = m_CastRefPointer.Instructions;
                    var pType = Emit.Operand.Create(m_CastRefPointer.CreateArgumentVariable(0));
                    var pVal = Emit.Operand.Create(m_CastRefPointer.CreateArgumentVariable(1));
                    var outPtr = Emit.Operand.Create(m_CastRefPointer.CreateArgumentVariable(2));

                    // CreateValidPointer(pVal, (u32)pType, outPtr);
                    gen.PushVar(outPtr);
                    var Var2 = gen.PushType(TypeU32);
                    gen.PushVar(pType);
                    gen.PushVar(Var2);
                    insns.Add(Emit.Instruction.Create(Emit.OpCodes.Call, CastPointerRef)); // call CastPointerRef
                    gen.Pop();
                    gen.Pop();
                    gen.Push(pVal);
                    insns.Add(Emit.Instruction.Create(Emit.OpCodes.Call, CreateValidPointer)); // call CreateValidPointer
                    // return;
                    insns.Add(Emit.Instruction.Create(Emit.OpCodes.Ret));

                    Script.Functions.Add(m_CastRefPointer);
                }
                return m_CastRefPointer;
            }
        }




        private Dictionary<string, Types.IType> m_TypesCache = new Dictionary<string, Types.IType>();
        private Dictionary<string, ABT.ExprType> m_TypesCacheNamed = new Dictionary<string, ABT.ExprType>();

        private static Guid GUID_IDISPATCH = new Guid(0x00020400, 0x0000, 0x0000, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);
        private static Guid GUID_IUNKNOWN = new Guid(0x00000000, 0x0000, 0x0000, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);
        public static bool TypeIsIDispatch(ABT.ExprType type)
        {
            if (type.Kind != ABT.ExprTypeKind.COM_INTERFACE) return false;
            return ((ABT.ComInterfaceType)type).InterfaceGuid == GUID_IDISPATCH;
        }

        public static bool TypeImplementsIDispatch(ABT.ExprType type)
        {
            if (type.Kind != ABT.ExprTypeKind.COM_INTERFACE) return false;
            return type.TypeAttribs.Any((attr) => attr.Name == "__dispatch");
        }

        public static readonly ABT.ComInterfaceType ExprTypeIDispatch = new ABT.ComInterfaceType(GUID_IDISPATCH);

        // Fixes up any types that the runtime expects to be exported with a specific name
        private static void FixUpTypeForRuntime(ABT.ExprType type, Types.IType emit)
        {
            // openarray is done by name prefix
            if (type.Kind == ABT.ExprTypeKind.INCOMPLETE_ARRAY)
            {
                if (((ABT.IncompleteArrayType)type).ElemType.TypeAttribs.Any((attr) => attr.Name == "__open"))
                {
                    emit.Name = "!OPENARRAY" + emit.Name;
                    emit.Exported = true;
                }
            }
        }

        public Types.IType EmitType(ABT.ExprType type, bool forGlobal = false)
        {
            // Check for specific hardcoded types.
            switch (type)
            {
                case ABT.PointerType ptr:
                    // We can't use type pointer, it's impossible to initialise.
                    // Use type array of pointer instead.
                    // for void*, use u32  (void* is impossible to specify, we can cast it later)
                    if (!ptr.IsRef) return TypeU32;
                    if (!(ptr.RefType is ABT.FunctionType))
                        return TypeArrayOfPointer;
                    break;
                case ABT.UCharType u8:
                    return TypeUByte;
                case ABT.UShortType u16:
                    return TypeU16;
                case ABT.ULongType u32:
                    return TypeU32;
                case ABT.ComInterfaceType com:
                    if (com.InterfaceGuid == GUID_IDISPATCH) return TypeIDispatch;
                    break;
            }

            // Check the named cache.
            if (m_TypesCacheNamed.TryGetValue(type.ToString(), out var named)) type = named;

            // Check the cache.
            if (m_TypesCache.TryGetValue(type.ToString(), out var ret)) return ret;

            ret = type.Emit(this);
            var namable = type as ABT.IExprTypeWithName;
            if (namable != null) ret.Name = namable.TypeName.Replace(' ', '_');
            FixUpTypeForRuntime(type, ret);
            if (ret == null) throw new InvalidOperationException("trying to emit null type");
            Script.Types.Add(ret);
            m_TypesCache.Add(type.ToString(), ret);
            return ret;
        }

        public int EmitTypeAndGetIndex(ABT.ExprType type, bool forGlobal = false)
        {
            return Script.Types.IndexOf(EmitType(type, forGlobal));
        }

        public void ChangeTypeName(ABT.IExprTypeWithName type, string name)
        {
            Types.IType emit = null;
            var realType = (ABT.ExprType)type;
            var old = realType.ToString();
            if (m_TypesCacheNamed.TryGetValue(old, out var named))
            {
                realType = named;
                type = (ABT.IExprTypeWithName)named;
            }
            else m_TypesCacheNamed.Add(old, realType);
            if (m_TypesCache.TryGetValue(old, out emit)) m_TypesCache.Remove(old);
            type.TypeName = name;
            var realName = realType.ToString();
            if (emit != null)
            {
                emit.Name = name.Replace(' ', '_');
                FixUpTypeForRuntime(realType, emit);
                m_TypesCache.Add(realName, emit);
            }
            if (!m_TypesCacheNamed.ContainsKey(realName)) m_TypesCacheNamed.Add(realName, realType);
        }

        private Dictionary<string, Emit.GlobalVariable> m_Globals = new Dictionary<string, Emit.GlobalVariable>();
        public IReadOnlyDictionary<string, Emit.GlobalVariable> Globals => m_Globals;

        private Emit.GlobalVariable CreateGlobal(Types.IType type, string name)
        {
            var ret = Emit.GlobalVariable.Create(Script.GlobalVariables.Count, type, name);
            Script.GlobalVariables.Add(ret);
            m_Globals.Add(name, ret);
            return ret;
        }

        public Emit.GlobalVariable CreateGlobal(ABT.ExprType type, string name, IStoredLineInfo info)
        {
            if (m_Globals.ContainsKey(name)) throw new InvalidOperationException(string.Format("Global with name {0} already exists", name)).Attach(info);
            return CreateGlobal(EmitType(type, true), name);
        }

        public CGenState() {
            this.label_idx = 2;
            this.label_packs = new Stack<LabelPack>();
        }

        public IFunction GetFunction(string name)
        {
            if (m_Functions.TryGetValue(name, out var impl)) return impl.Function;
            if (!m_FunctionsExternal.TryGetValue(name, out var ext)) throw new KeyNotFoundException();
            return ext;
        }

        private void DeclareFunctionArguments(FunctionBase func, ABT.FunctionType type)
        {
            // ensure that script has pointer type
            TypePointer.ToString();

            if (type.ReturnType is ABT.VoidType)
                func.ReturnArgument = null;
            else
                func.ReturnArgument = EmitType(type.ReturnType);

            func.Arguments = new List<FunctionArgument>();
            foreach (var arg in type.Args)
            {
                var farg = new FunctionArgument();
                farg.Name = arg.name;
                switch (arg.type)
                {
                    case ABT.PointerType ptr:
                        // void* == u32
                        if (!ptr.IsRef)
                        {
                            break;
                        }
                        farg.ArgumentType = FunctionArgumentType.Out; //ref
                        farg.Type = EmitType(ptr.RefType);
                        break;
                    default:
                        break;
                }
                if (farg.Type == null)
                {
                    farg.ArgumentType = FunctionArgumentType.In;
                    farg.Type = EmitType(arg.type);
                }
                func.Arguments.Add(farg);
            }
        }

        private FunctionGen DeclareFunctionCore(string name, ABT.FunctionType type, ABT.StorageClass scs)
        {
            if (m_Functions.ContainsKey(name)) return null;
            if (m_FunctionsExternal.ContainsKey(name)) return null;

            var state = new FunctionGen(name, scs);
            

            DeclareFunctionArguments(state.Function, type);

            m_Functions[name] = state;
            Script.Functions.Add(state.Function);
            return state;
        }

        public void DeclareFunction(string name, ABT.FunctionType type, ABT.StorageClass scs)
        {
            DeclareFunctionCore(name, type, scs);
        }

        public void DeclareExternalFunction(string name, Emit.ExternalFunction func, ABT.FunctionType type, IStoredLineInfo info)
        {
            if (m_Functions.ContainsKey(name) || m_FunctionsExternal.ContainsKey(name)) throw new InvalidOperationException(string.Format("Already declared a function with name {0}", name)).Attach(info);

            func.Name = name;
            DeclareFunctionArguments(func, type);

            m_FunctionsExternal[name] = func;
            Script.Functions.Add(func);
        }

        public void CGenFuncStart(string name, ABT.FunctionType type, ABT.StorageClass scs, IStoredLineInfo info) {
            if (m_Functions.TryGetValue(name, out var state))
            {
                if (state.Function.Instructions.Count != 0) throw new InvalidOperationException(string.Format("Already emitted instructions for function {0}", name)).Attach(info);
                FunctionState = state;
                return;
            }
            if (m_FunctionsExternal.ContainsKey(name)) throw new InvalidOperationException(string.Format("Already declared an external function with name {0}", name)).Attach(info);
            
            FunctionState = DeclareFunctionCore(name, type, scs);
        }

        // CGenExpandStack
        // ===============
        // 

        public void CGenForceStackSizeTo(Int32 nbytes) {
            if (!FunctionState.IndexToLabel.ContainsKey(CurrInsns.Count - 1))
            {
                var lastop = CurrInsns.LastOrDefault()?.OpCode.Code;
                if (lastop == null || lastop == Emit.Code.Jump || lastop == Emit.Code.Ret) return;
            }
            while (StackSize > nbytes) FunctionState.Pop();
        }

        private Stack<int> stackSizes = new Stack<int>();

        public void CGenPushStackSize()
        {
            stackSizes.Push(StackSize);
        }

        public void CGenPeekStackSize()
        {
            if (stackSizes.Count == 0) return;
            CGenForceStackSizeTo(stackSizes.Peek());
        }

        public void CGenPopStackSize()
        {
            if (stackSizes.Count == 0) return;
            CGenForceStackSizeTo(stackSizes.Pop());
        }

        public void CGenPopStackSizeForRevert()
        {
            if (stackSizes.Count == 0) return;
            var expected = stackSizes.Pop();
            while (StackSize > expected) FunctionState.PopForRevert();
        }

        public void CGenLabel(Int32 label)
        {
            if (FunctionState.IndexToLabel.ContainsKey(CurrInsns.Count - 1))
            {
                var lbl = FunctionState.Labels[label];
                // For each instruction referencing this label: replace the operand with CurrInsns.Last()
                var jump = CurrInsns.Last();
                foreach (var insn in CurrInsns)
                {
                    switch (insn.OpCode.OperandType)
                    {
                        case Emit.OperandType.InlineBrTarget:
                        case Emit.OperandType.InlineBrTargetValue:
                            if (insn[0].ImmediateAs<Emit.Instruction>() == lbl) insn[0] = Emit.Operand.Create(jump);
                            break;
                        case Emit.OperandType.InlineEH:
                            if (insn[0].ImmediateAs<Emit.Instruction>() == lbl) insn[0] = Emit.Operand.Create(jump);
                            if (insn[1].ImmediateAs<Emit.Instruction>() == lbl) insn[1] = Emit.Operand.Create(jump);
                            if (insn[2].ImmediateAs<Emit.Instruction>() == lbl) insn[2] = Emit.Operand.Create(jump);
                            if (insn[3].ImmediateAs<Emit.Instruction>() == lbl) insn[3] = Emit.Operand.Create(jump);
                            break;
                    }
                }
                FunctionState.Labels[label] = jump;
                return;
            }

            var idx = CurrInsns.Count;
            CurrInsns.Add(FunctionState.Labels[label]);
            FunctionState.LabelToIndex.Add(label, idx);
            FunctionState.IndexToLabel.Add(idx, label);
        }

        public Int32 label_idx;


        public Int32 StackSize => FunctionState.LocalsCount;

        public Int32 RequestLabel() {
            this.label_idx = FunctionState.Labels.Count;
            var ret = label_idx;
            FunctionState.Labels.Add(Emit.Instruction.Create(Emit.OpCodes.Nop));
            return ret;
        }


        //private Stack<Int32> _continue_labels;
        //private Stack<Int32> _break_labels;

        private struct LabelPack {
            public LabelPack(Int32 continue_label, Int32 break_label, Int32 default_label, Dictionary<Int32, Int32> value_to_label) {
                this.continue_label = continue_label;
                this.break_label = break_label;
                this.default_label = default_label;
                this.value_to_label = value_to_label;
            }
            public readonly Int32 continue_label;
            public readonly Int32 break_label;
            public readonly Int32 default_label;
            public readonly Dictionary<Int32, Int32> value_to_label;
        }

        private readonly Stack<LabelPack> label_packs;

        public Int32 ContinueLabel => this.label_packs.First(_ => _.continue_label != -1).continue_label;

        public Int32 BreakLabel => this.label_packs.First(_ => _.break_label != -1).break_label;

        public Int32 DefaultLabel {
            get {
                Int32 ret = this.label_packs.First().default_label;
                if (ret == -1) {
                    throw new InvalidOperationException("Not in a switch statement.");
                }
                return ret;
            }
        }

        public bool IsInSwitch => label_packs.Count > 0 ? label_packs.Peek().value_to_label != null : false;

        public Int32 CaseLabel(Int32 value) => this.label_packs.First(_ => _.value_to_label != null).value_to_label[value];
        // label_packs.First().value_to_label[Value];

        public void InLoop(Int32 continue_label, Int32 break_label) {
            this.label_packs.Push(new LabelPack(continue_label, break_label, -1, null));
            //_continue_labels.Push(continue_label);
            //_break_labels.Push(break_label);
        }

        public void InSwitch(Int32 break_label, Int32 default_label, Dictionary<Int32, Int32> value_to_label) {
            this.label_packs.Push(new LabelPack(-1, break_label, default_label, value_to_label));
        }

        public void OutLabels() {
            this.label_packs.Pop();
            //_continue_labels.Pop();
            //_break_labels.Pop();
        }

        private readonly Dictionary<String, Int32> _goto_labels = new Dictionary<String, Int32>();

        public Int32 GotoLabel(String label) {
            return this._goto_labels[label];
        }

        public void JumpTo(int label, bool ignoreStack)
        {
            if (FunctionState.IndexToLabel.TryGetValue(CurrInsns.Count - 1, out var lastLabel))
            {
                var lbl = CurrInsns.Last();
                // For each instruction referencing this label: replace the operand with CurrInsns.Last()
                var jump = FunctionState.Labels[label];
                foreach (var insn in CurrInsns)
                {
                    switch (insn.OpCode.OperandType)
                    {
                        case Emit.OperandType.InlineBrTarget:
                        case Emit.OperandType.InlineBrTargetValue:
                            if (insn[0].ImmediateAs<Emit.Instruction>() == lbl) insn[0] = Emit.Operand.Create(jump);
                            break;
                        case Emit.OperandType.InlineEH:
                            if (insn[0].ImmediateAs<Emit.Instruction>() == lbl) insn[0] = Emit.Operand.Create(jump);
                            if (insn[1].ImmediateAs<Emit.Instruction>() == lbl) insn[1] = Emit.Operand.Create(jump);
                            if (insn[2].ImmediateAs<Emit.Instruction>() == lbl) insn[2] = Emit.Operand.Create(jump);
                            if (insn[3].ImmediateAs<Emit.Instruction>() == lbl) insn[3] = Emit.Operand.Create(jump);
                            break;
                    }
                }
                FunctionState.Labels[lastLabel] = jump;
                return;
            }
            if (!ignoreStack) CGenPeekStackSize();
            CurrInsns.Add(Emit.Instruction.Create(Emit.OpCodes.Jump, FunctionState.Labels[label]));
        }


        public void InFunction(IReadOnlyList<String> goto_labels) {
            this._goto_labels.Clear();
            foreach (String goto_label in goto_labels) {
                this._goto_labels.Add(goto_label, RequestLabel());
            }
        }

        public void OutFunction() {
            this._goto_labels.Clear();

            // Optimisation: replace any nop instruction at label with instruction after.
            foreach (var label in FunctionState.LabelToIndex.OrderByDescending((l) => l.Value))
            {
                if (CurrInsns.Count > label.Value && CurrInsns[label.Value].OpCode.Code == Emit.Code.Nop)
                {
                    CurrInsns[label.Value].Replace(CurrInsns[label.Value + 1]);
                    CurrInsns.RemoveAt(label.Value + 1);
                }
            }

            FunctionState.Function.UpdateInstructionOffsets();
            FunctionState.Function.UpdateInstructionCrossReferences();
        }

        public void EmitCallsToCtor()
        {
            // if the constructor function wasn't created (ie, no initialised global variables), nothing needs to be done
            if (m_Constructor == null) return;
            // Emit the return instruction for the constructor.
            m_Constructor.Function.Instructions.Add(Emit.Instruction.Create(Emit.OpCodes.Ret));
            // Enumerate through all public functions (non-static).
            var publicFunctions = m_Functions.Values.Select(f => f.Function).Where(f => f.Exported);

            foreach (var f in publicFunctions)
            {
                // Add an instruction to the start to call the constructor.
                f.Instructions.Insert(0, Emit.Instruction.Create(Emit.OpCodes.Call, m_Constructor.Function as IFunction));
            }
        }
    }
}