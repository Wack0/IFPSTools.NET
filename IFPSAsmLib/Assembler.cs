using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using IFPSLib;
using IFPSLib.Types;
using IFPSLib.Emit;
using IFPSLib.Emit.FDecl;
using System.Runtime.InteropServices;
using System.Collections;
using System.ComponentModel;

namespace IFPSAsmLib
{
    internal static class AssemblerExtensions
    {
        private enum LiteralParserState : byte
        {
            Default,
            EscapedChar,
            EscapedHex,
            EscapedUnicode
        }

        private static char NibbleToHex(this char ch0)
        {
            // taken from stackoverflow: https://stackoverflow.com/questions/3408706/hexadecimal-string-to-byte-array-in-c/67799940#67799940
            // Parser already ensured the char is a valid nibble, so the quick bithacking can be done here
            return (char)((ch0 & 0xF) + (ch0 >> 6) | ((ch0 >> 3) & 0x8));
        }

        internal static string FromLiteral(this string input)
        {
            if (input[0] != input[input.Length - 1] || input[0] != Constants.STRING_CHAR) throw new ArgumentOutOfRangeException(nameof(input));
            StringBuilder literal = new StringBuilder(input.Length - 2);
            var state = LiteralParserState.Default;
            byte counter = 0;
            char escapedChar = (char)0;
            foreach (var c in input.Skip(1).Take(input.Length - 2))
            {
                switch (state)
                {
                    case LiteralParserState.Default:
                        if (c != '\\') literal.Append(c);
                        else state = LiteralParserState.EscapedChar;
                        break;
                    case LiteralParserState.EscapedChar:
                        state = LiteralParserState.Default;
                        switch (c)
                        {
                            case '"': literal.Append('"'); break;
                            case '\\': literal.Append('\\'); break;
                            case '0': literal.Append('\0'); break;
                            case 'a': literal.Append('\a'); break;
                            case 'b': literal.Append('\b'); break;
                            case 'f': literal.Append('\f'); break;
                            case 'n': literal.Append('\n'); break;
                            case 'r': literal.Append('\r'); break;
                            case 't': literal.Append('\t'); break;
                            case 'v': literal.Append('\v'); break;
                            case 'x':
                                state = LiteralParserState.EscapedHex;
                                counter = 0;
                                break;
                            case 'u':
                                state = LiteralParserState.EscapedUnicode;
                                counter = 0;
                                break;
                        }
                        break;
                    case LiteralParserState.EscapedHex:
                        escapedChar |= (char)(NibbleToHex(c) << ((2 - 1 - counter) * 4));
                        counter++;
                        if (counter == 2)
                        {
                            literal.Append(escapedChar);
                            counter = 0;
                            escapedChar = (char)0;
                            state = LiteralParserState.Default;
                        }
                        break;
                    case LiteralParserState.EscapedUnicode:
                        escapedChar |= (char)(NibbleToHex(c) << ((4 - 1 - counter) * 4));
                        counter++;
                        if (counter == 4)
                        {
                            literal.Append(escapedChar);
                            counter = 0;
                            escapedChar = (char)0;
                            state = LiteralParserState.Default;
                        }
                        break;
                }
            }
            return literal.ToString();
        }
    }

    public static class Assembler
    {
        private static IEnumerable<ParsedBody> OfType(this IEnumerable<ParsedBody> self, ElementParentType type)
        {
            return self.Where(x => x.Element.ParentType == type);
        }

        private static IEnumerable<ParserElement> OfType(this IEnumerable<ParserElement> self, ElementParentType type)
        {
            return self.Where(x => x.ParentType == type);
        }

        private static ParsedBody ExpectZeroOrOne(List<ParsedBody> parsed, ElementParentType type)
        {
            var elems = parsed.OfType(type);
            var second = elems.Skip(1).FirstOrDefault();
            if (second != null)
            {
                second.Element.ThrowInvalid();
            }
            return elems.FirstOrDefault();
        }

        private static bool IsExported(this ParserElement elem)
        {
            var value = elem.NextChild?.Value;
            return value == Constants.ELEMENT_BODY_EXPORTED || value == Constants.ELEMENT_BODY_IMPORTED;
        }

        private static void EnsureNoNextChild(this ParserElement val)
        {
            if (val.NextChild != null) val.NextChild.ThrowInvalid();
        }

        public static CustomAttribute ParseAttribute(ParserElement attr, Dictionary<string, IType> types, Dictionary<string, IFunction> functions)
        {
            // name (...)
            CustomAttribute ret = new CustomAttribute(attr.Value);
            for (var next = attr.Next; next != null; next = next.Next)
            {
                if (!types.TryGetValue(next.Value, out var immType)) next.ThrowInvalid(string.Format("In attribute {0}: Invalid type", ret.Name));
                var data = TryParseData(immType, next.NextChild.Value, functions);
                if (data == null) next.NextChild.ThrowInvalid(string.Format("In attribute {0}: Invalid data", ret.Name));
                ret.Arguments.Add(data);
            }
            return ret;
        }

        private static IType ParseType(ParserElement type, Dictionary<string, IType> types)
        {
            // .type [(export)] type(...) name
            IType ret = null;
            var baseType = type.Next;
            ParserElement elemName = baseType.Next;
            var child = baseType.NextChild;
            ParserElement next = child?.Next;
            switch (baseType.Value)
            {
                // primitive(enum_value)
                case Constants.TYPE_PRIMITIVE:
                    child.EnsureNoNextChild();
                    if (!Enum.TryParse<PascalTypeCode>(child.Value, out var typeCode)) child.ThrowInvalid("Invalid primitive type");
                    if (!typeCode.IsPrimitive()) child.ThrowInvalid();
                    if (next != null) next.ThrowInvalid();
                    ret = new PrimitiveType(typeCode);
                    break;
                // array(type) or array(type,length,start)
                case Constants.TYPE_ARRAY:
                    if (next != null) next.ThrowInvalid();
                    next = child.NextChild;
                    if (!types.TryGetValue(child.Value, out var childType)) child.ThrowInvalid("Invalid array type");
                    if (next == null)
                    {
                        ret = new ArrayType(childType);
                    } else
                    {
                        if (next.Next != null) next.Next.ThrowInvalid();
                        if (!int.TryParse(next.Value, out var arrLen)) next.ThrowInvalid("Invalid static array length");
                        int idxStart = 0;
                        next = next.NextChild;
                        if (next != null)
                        {
                            if (next.Next != null) next.Next.ThrowInvalid();
                            if (!int.TryParse(next.Value, out idxStart)) next.ThrowInvalid("Invalid static array start index");
                        }
                        ret = new StaticArrayType(childType, arrLen, idxStart);
                    }
                    break;
                // class(internalName)
                case Constants.TYPE_CLASS:
                    child.EnsureNoNextChild();
                    if (next != null) next.ThrowInvalid();
                    child.ExpectValidName();
                    ret = new ClassType(child.Value);
                    break;
                // interface(guidString)
                case Constants.TYPE_COM_INTERFACE:
                    child.EnsureNoNextChild();
                    if (next != null) next.ThrowInvalid();
                    child.ExpectString();
                    if (!Guid.TryParse(child.Value.FromLiteral(), out var guid)) child.ThrowInvalid("Invalid COM interface GUID");
                    ret = new IFPSLib.Types.ComInterfaceType(guid);
                    break;
                // funcptr(declaration)
                case Constants.TYPE_FUNCTION_POINTER:
                    child = baseType.Next;
                    if (child.NextChild == null) child.ThrowInvalid();
                    if (next != null) next.ThrowInvalid();
                    // returnsval|void
                    bool returnsVal = child.Value == Constants.FUNCTION_RETURN_VAL;
                    if (!returnsVal && child.Value != Constants.FUNCTION_RETURN_VOID) child.ThrowInvalid("Invalid function pointer return type");
                    child = child.NextChild;
                    var argList = new List<FunctionArgumentType>();
                    if (child.Value != "")
                    {
                        for (next = child; next != null; next = next.NextChild)
                        {
                            if (next.Next != null) next.Next.ThrowInvalid();
                            var isInVal = next.Value == Constants.FUNCTION_ARG_IN || next.Value == Constants.FUNCTION_ARG_VAL;
                            if (!isInVal && next.Value != Constants.FUNCTION_ARG_OUT && next.Value != Constants.FUNCTION_ARG_REF)
                                child.ThrowInvalid("Invalid function pointer argument type");
                            argList.Add(isInVal ? FunctionArgumentType.In : FunctionArgumentType.Out);
                        }
                    }
                    ret = new FunctionPointerType(returnsVal, argList);
                    baseType = baseType.Next;
                    break;
                // record(types...)
                case Constants.TYPE_RECORD:
                    if (next != null) next.ThrowInvalid();
                    var typeList = new List<IType>();
                    for (next = child; next != null; next = next.NextChild)
                    {
                        if (!types.TryGetValue(next.Value, out childType)) next.ThrowInvalid("Invalid record element type");
                        typeList.Add(childType);
                    }
                    ret = new RecordType(typeList);
                    break;
                // set(bitsize)
                case Constants.TYPE_SET:
                    child.EnsureNoNextChild();
                    if (!int.TryParse(child.Value, out var setLen) || setLen > 0x100) child.ThrowInvalid("Invalid set bit size");
                    if (next != null) next.ThrowInvalid();
                    ret = new SetType(setLen);
                    break;
                default:
                    baseType.ThrowInvalid();
                    break;
            }

            ret.Exported = type.IsExported();
            ret.Name = baseType.Next.Value;
            if (types.ContainsKey(ret.Name)) baseType.Next.ThrowInvalid("Type already defined");
            types.Add(ret.Name, ret);
            return ret;
        }

        private static GlobalVariable ParseGlobal(ParserElement global, Dictionary<string, IType> types, Dictionary<string, GlobalVariable> globals, int index)
        {
            // .global (export) type name
            var next = global.Next;
            if (!types.TryGetValue(next.Value, out var type)) next.ThrowInvalid("Global has unknown type");
            next = next.Next;
            var ret = GlobalVariable.Create(index, type, next.Value);
            if (globals.ContainsKey(ret.Name)) next.ThrowInvalid("Global already defined");
            ret.Exported = global.IsExported();
            globals.Add(ret.Name, ret);
            return ret;
        }

        private static NativeCallingConvention ParseCC(this ParserElement elem)
        {
            switch (elem.Value)
            {
                case Constants.FUNCTION_FASTCALL:
                    return NativeCallingConvention.Register;
                case Constants.FUNCTION_PASCAL:
                    return NativeCallingConvention.Pascal;
                case Constants.FUNCTION_CDECL:
                    return NativeCallingConvention.CDecl;
                case Constants.FUNCTION_STDCALL:
                    return NativeCallingConvention.Stdcall;
                default:
                    elem.ThrowInvalid(); // shouldn't get here, parser should have detected invalid calling convention already
                    return NativeCallingConvention.Stdcall;
            }
        }

        private static IFunction ParseFunction(ParserElement function, Dictionary<string, IType> types, Dictionary<string, IFunction> functions)
        {
            IFunction ret = null;
            var next = function.Next;
            ParserElement child = null;
            bool isExternal = next.Value == Constants.FUNCTION_EXTERNAL;
            if (isExternal)
            {
                next = next.Next;
                var ext = new ExternalFunction();
                ret = ext;
                switch (next.Value)
                {
                    case Constants.FUNCTION_EXTERNAL_INTERNAL:
                        ext.Declaration = new Internal();
                        break;
                    case Constants.FUNCTION_EXTERNAL_DLL:
                        // dll(dllname,procname[,delayload][,alteredsearchpath])
                        child = next.NextChild;
                        var dll = new DLL();
                        dll.DllName = child.Value.FromLiteral();
                        child = child.NextChild;
                        dll.ProcedureName = child.Value.FromLiteral();
                        child = child.NextChild;
                        if (child != null)
                        {
                            if (child.Value == Constants.FUNCTION_EXTERNAL_DLL_DELAYLOAD) dll.DelayLoad = true;
                            else dll.LoadWithAlteredSearchPath = true;
                            child = child.NextChild;
                            if (child != null)
                            {
                                if (child.Value == Constants.FUNCTION_EXTERNAL_DLL_DELAYLOAD) dll.DelayLoad = true;
                                else dll.LoadWithAlteredSearchPath = true;
                            }
                        }
                        // calling convention
                        next = next.Next;
                        dll.CallingConvention = next.ParseCC();

                        ext.Declaration = dll;
                        break;
                    case Constants.FUNCTION_EXTERNAL_CLASS:
                        // class(classname, procname, property)
                        child = next.NextChild;
                        var cls = new Class();
                        cls.ClassName = child.Value;
                        child = child.NextChild;
                        cls.FunctionName = child.Value;
                        cls.IsProperty = child.NextChild != null;
                        // calling convention
                        next = next.Next;
                        cls.CallingConvention = next.ParseCC();

                        ext.Declaration = cls;
                        break;
                    case Constants.FUNCTION_EXTERNAL_COM:
                        // com(vtbl_index)
                        child = next.NextChild;
                        var com = new COM();
                        com.VTableIndex = uint.Parse(child.Value);
                        // calling convention
                        next = next.Next;
                        com.CallingConvention = next.ParseCC();

                        ext.Declaration = com;
                        break;
                }
                next = next.Next;
            } else
            {
                ret = new ScriptFunction();
            }

            // void|returnsval/type
            // external cannot be type
            if (next.Value != Constants.FUNCTION_RETURN_VOID)
            {
                if (isExternal) ret.ReturnArgument = UnknownType.Instance;
                else
                {
                    if (!types.TryGetValue(next.Value, out var retArg)) next.ThrowInvalid(string.Format("In function \"{0}\": Unknown type", ret.Name));
                    ret.ReturnArgument = retArg;
                }
            }
            next = next.Next;

            // name
            ret.Name = next.Value;
            if (functions.ContainsKey(ret.Name)) next.ThrowInvalid("Function already defined");

            // arguments
            ret.Arguments = new List<FunctionArgument>();
            if (next.NextChild.Value != "")
            {
                for (child = next.NextChild; child != null; child = child.NextChild)
                {
                    // __in|__val|__out|__ref type|__unknown name
                    var arg = new FunctionArgument();
                    bool isInVal = child.Value == Constants.FUNCTION_ARG_IN || child.Value == Constants.FUNCTION_ARG_VAL;
                    if (!isInVal && child.Value != Constants.FUNCTION_ARG_OUT && child.Value != Constants.FUNCTION_ARG_REF)
                        child.ThrowInvalid(string.Format("In function \"{0}\": Unknown argument type", ret.Name));
                    arg.ArgumentType = isInVal ? FunctionArgumentType.In : FunctionArgumentType.Out;
                    next = child.Next;
                    if (next == null) child.ThrowInvalid();
                    // An external function does not have types here, a script function does.
                    if (isExternal) arg.Type = UnknownType.Instance;
                    else
                    {
                        if (!types.TryGetValue(next.Value, out var argType)) next.ThrowInvalid(string.Format("In function \"{0}\": Unknown type", ret.Name));
                        arg.Type = argType;
                    }
                    if (next.Next != null)
                    {
                        next = next.Next;

                        next.ExpectValidName();
                        arg.Name = next.Value;
                    }

                    ret.Arguments.Add(arg);
                }
            }

            ret.Exported = function.IsExported();
            functions.Add(ret.Name, ret);
            return ret;
        }

        private static bool TryParse<T>(string str, out T val)
        {
            try
            {
                val = str.Parse<T>();
                return true;
            } catch
            {
                val = default;
                return false;
            }
        }

        private static T Parse<T>(this string val)
        {
            return (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(val);
        }

        private static TypedData TryParseData(IType itype, string val, Dictionary<string, IFunction> functions)
        {
            var type = itype as PrimitiveType;
            switch (itype.BaseType)
            {
                case PascalTypeCode.S8:
                    if (!TryParse<sbyte>(val, out var _sbyte)) return null;
                    return TypedData.Create(type, _sbyte);
                case PascalTypeCode.U8:
                    if (!TryParse<byte>(val, out var _byte)) return null;
                    return TypedData.Create(type, _byte);
                case PascalTypeCode.Char:
                    val = val.FromLiteral();
                    if (val.Length != 1) return null;
                    if (val[0] > 0xff) return null;
                    return TypedData.Create(type, val[0]);

                case PascalTypeCode.S16:
                    if (!TryParse<short>(val, out var _short)) return null;
                    return TypedData.Create(type, _short);
                case PascalTypeCode.U16:
                    if (!TryParse<ushort>(val, out var _ushort)) return null;
                    return TypedData.Create(type, _ushort);
                case PascalTypeCode.WideChar:
                    val = val.FromLiteral();
                    if (val.Length != 1) return null;
                    return TypedData.Create(type, val[0]);

                case PascalTypeCode.S32:
                    if (!TryParse<int>(val, out var _int)) return null;
                    return TypedData.Create(type, _int);
                case PascalTypeCode.U32:
                    if (!TryParse<uint>(val, out var _uint)) return null;
                    return TypedData.Create(type, _uint);

                case PascalTypeCode.S64:
                    if (!TryParse<long>(val, out var _long)) return null;
                    return TypedData.Create(type, _long);

                case PascalTypeCode.Single:
                    if (!float.TryParse(val, out var _float)) return null;
                    return TypedData.Create(type, _float);
                case PascalTypeCode.Double:
                    if (!double.TryParse(val, out var _double)) return null;
                    return TypedData.Create(type, _double);
                case PascalTypeCode.Extended:
                    if (!decimal.TryParse(val, out var _decimal)) return null;
                    return TypedData.Create(type, _decimal);

                case PascalTypeCode.Currency:
                    if (!decimal.TryParse(val, out var _currency)) return null;
                    return TypedData.Create(type, new CurrencyWrapper(_currency));

                case PascalTypeCode.PChar:
                case PascalTypeCode.String:
                    val = val.FromLiteral();
                    if (val.Any(x => x > 0xff)) return null;
                    return TypedData.Create(type, val);

                case PascalTypeCode.WideString:
                case PascalTypeCode.UnicodeString:
                    return TypedData.Create(type, val.FromLiteral());

                case PascalTypeCode.ProcPtr:
                    if (!functions.TryGetValue(val, out var func)) return null;
                    return TypedData.Create(itype as FunctionPointerType, func);

                case PascalTypeCode.Set:
                    if (!val.StartsWith(Constants.INTEGER_BINARY)) return null;
                    var ba = new BitArray(val.Length - 2);
                    for (int i = ba.Length - 1; i >= 0; i--)
                    {
                        var chr = val[i + 2];
                        bool bit = chr == Constants.BINARY_TRUE;
                        if (!bit && chr != Constants.BINARY_FALSE) return null;
                        ba[i] = bit;
                    }
                    return TypedData.Create(type, ba);

                default:
                    return null;
            }
        }

        private static IVariable ParseVariable(
            string value,
            ScriptFunction function,
            Dictionary<string, GlobalVariable> globals,
            Dictionary<string, string> aliases
        )
        {
            if (aliases.TryGetValue(value, out var realVar)) value = realVar;
            if (globals.TryGetValue(value, out var global)) return global;
            for (int i = 0; i < function.Arguments.Count; i++)
            {
                if (function.Arguments[i].Name != value) continue;
                return function.CreateArgumentVariable(i);
            }
            value = value.ToLower();
            if (value == Constants.VARIABLE_RET) return function.CreateReturnVariable();
            if (!value.StartsWith(Constants.VARIABLE_LOCAL_PREFIX)) return null;
            if (!int.TryParse(value.Substring(Constants.VARIABLE_LOCAL_PREFIX.Length), out var idx)) return null;
            if (idx < 0) return null;
            return LocalVariable.Create(idx - 1);
        }

        private static Operand ParseOperandValue(
            ParserElement value,
            ScriptFunction function,
            Dictionary<string, IType> types,
            Dictionary<string, GlobalVariable> globals,
            Dictionary<string, IFunction> functions,
            Dictionary<string, string> aliases,
            Dictionary<string, ParserElement> defines
        )
        {
            // first: check define
            if (value.NextChild == null && defines.TryGetValue(value.Value, out var defined))
                value = defined;

            // immediate: type(value)
            // variable: name
            // immediate index: name[int_elem]
            // variable index: name[elem_var]
            if (value.NextChild != null)
            {
                // immediate
                if (!types.TryGetValue(value.Value, out var immType)) value.ThrowInvalid(string.Format("In function {0}: Invalid type", function.Name));
                var data = TryParseData(immType, value.NextChild.Value, functions);
                if (data == null) value.NextChild.ThrowInvalid(string.Format("In function {0}: Invalid data", function.Name));
                return new Operand(data);
            }

            var val = value.Value;
            var indexOfStart = val.AsSpan().IndexOf(Constants.START_ARRAY_INDEX);
            if (val[val.Length - 1] != Constants.END_ARRAY_INDEX || indexOfStart == -1)
            {
                // variable
                var variable = ParseVariable(val, function, globals, aliases);
                if (variable == null) value.ThrowInvalid(string.Format("In function {0}: Used nonexistant variable", function.Name));
                return new Operand(variable);
            }

            var baseName = val.Substring(0, indexOfStart);
            string element = val.Substring(indexOfStart + 1, val.Length - indexOfStart - 2);
            var baseVar = ParseVariable(baseName, function, globals, aliases);
            if (baseVar == null) value.ThrowInvalid(string.Format("In function {0}: Used nonexistant variable", function.Name));
            if (uint.TryParse(element, out var elementIdx)) return new Operand(baseVar, elementIdx);
            var elemVar = ParseVariable(element, function, globals, aliases);
            if (elemVar == null) value.ThrowInvalid(string.Format("In function {0}: Used nonexistant variable", function.Name));
            return new Operand(baseVar, elemVar);
        }

        private static Instruction ParseInstructionFirstPass(
            ParserElement insn,
            ScriptFunction function,
            Dictionary<string, IType> types,
            Dictionary<string, GlobalVariable> globals,
            Dictionary<string, IFunction> functions,
            Dictionary<string, string> aliases,
            Dictionary<ParserElement, int> placeholderTable,
            Dictionary<string, ParserElement> defines
        )
        {
            ParserElement next = null;

            if (!OpCodes.ByName.TryGetValue(insn.Value, out var opcode)) insn.ThrowInvalid(string.Format("In function \"{0}\": Unknown opcode", function.Name));

            switch (opcode.OperandType)
            {
                case OperandType.InlineNone:
                    return Instruction.Create(opcode);
                case OperandType.InlineBrTarget:
                case OperandType.InlineBrTargetValue:
                case OperandType.InlineEH:
                    placeholderTable.Add(insn, function.Instructions.Count);
                    return Instruction.Create(OpCodes.Nop);
                case OperandType.InlineValue:
                case OperandType.InlineValueSF:
                    if (insn.Next == null) insn.ThrowInvalid();
                    return Instruction.Create(opcode, ParseOperandValue(insn.Next, function, types, globals, functions, aliases, defines));
                case OperandType.InlineValueValue:
                    {
                        next = insn.Next;
                        if (next == null) insn.ThrowInvalid();
                        var op0 = ParseOperandValue(next, function, types, globals, functions, aliases, defines);
                        if (next.Next == null) next.ThrowInvalid();
                        next = next.Next;
                        var op1 = ParseOperandValue(next, function, types, globals, functions, aliases, defines);
                        if (next.Next != null) next.Next.ThrowInvalid();
                        return Instruction.Create(opcode, op0, op1);
                    }
                case OperandType.InlineFunction:
                    next = insn.Next;
                    if (next == null) insn.ThrowInvalid();
                    next.ExpectValidName();
                    next.EnsureNoNextChild();
                    if (!functions.TryGetValue(next.Value, out var funcOp)) insn.ThrowInvalid(string.Format("In function \"{0}\": Referenced unknown function", function.Name));
                    if (next.Next != null) next.Next.ThrowInvalid();
                    return Instruction.Create(opcode, funcOp);
                case OperandType.InlineType:
                    {
                        next = insn.Next;
                        if (next == null) insn.ThrowInvalid();
                        next.ExpectValidName();
                        next.EnsureNoNextChild();
                        if (!types.TryGetValue(next.Value, out var typeOp)) insn.ThrowInvalid(string.Format("In function \"{0}\": Referenced unknown type", function.Name));
                        if (next.Next != null) next.Next.ThrowInvalid();
                        return Instruction.Create(opcode, typeOp);
                    }
                case OperandType.InlineCmpValue:
                    {
                        next = insn.Next;
                        if (next == null) insn.ThrowInvalid();
                        var op0 = ParseOperandValue(next, function, types, globals, functions, aliases, defines);
                        if (next.Next == null) next.ThrowInvalid();
                        next = next.Next;
                        var op1 = ParseOperandValue(next, function, types, globals, functions, aliases, defines);
                        if (next.Next == null) next.ThrowInvalid();
                        next = next.Next;
                        var op2 = ParseOperandValue(next, function, types, globals, functions, aliases, defines);
                        if (next.Next != null) next.Next.ThrowInvalid();
                        return Instruction.Create(opcode, op0, op1, op2);
                    }
                case OperandType.InlineCmpValueType:
                    {
                        next = insn.Next;
                        if (next == null) insn.ThrowInvalid();
                        var op0 = ParseOperandValue(next, function, types, globals, functions, aliases, defines);
                        if (next.Next == null) next.ThrowInvalid();
                        next = next.Next;
                        var op1 = ParseOperandValue(next, function, types, globals, functions, aliases, defines);
                        if (next.Next == null) next.ThrowInvalid();
                        next = next.Next;
                        next.ExpectValidName();
                        next.EnsureNoNextChild();
                        if (!types.TryGetValue(next.Value, out var typeOp)) insn.ThrowInvalid(string.Format("In function \"{0}\": Referenced unknown type", function.Name));
                        if (next.Next != null) next.Next.ThrowInvalid();
                        return Instruction.Create(opcode, op0, op1, typeOp);
                    }
                case OperandType.InlineTypeVariable:
                    {
                        if (opcode.Code != Code.SetStackType) insn.ThrowInvalid(string.Format("In function \"{0}\": Unknown opcode", function.Name));
                        next = next.Next;
                        next.ExpectValidName();
                        next.EnsureNoNextChild();
                        if (!types.TryGetValue(next.Value, out var typeOp)) insn.ThrowInvalid(string.Format("In function \"{0}\": Referenced unknown type", function.Name));
                        next = next.Next;
                        next.ExpectValidName();
                        var variable = ParseVariable(next.Value, function, globals, aliases);
                        if (variable == null) next.ThrowInvalid(string.Format("In function {0}: Used nonexistant variable", function.Name));
                        if (next.Next != null) next.Next.ThrowInvalid();
                        return Instruction.CreateSetStackType(typeOp, variable);
                    }
                default:
                    insn.ThrowInvalid(string.Format("In function \"{0}\": Unknown opcode", function.Name));
                    return null;
            }
        }

        private static void ParseLabelEH(ScriptFunction function, ParserElement next, Dictionary<string, int> labels, out int idx)
        {
            if (next.Value == Constants.LABEL_NULL) idx = -1;
            else if (!labels.TryGetValue(next.Value, out idx)) next.ThrowInvalid(string.Format("In function \"{0}\": Referenced unknown label", function.Name));
        }

        private static void ParseInstructions(
            ScriptFunction function,
            IReadOnlyList<ParserElement> instructions,
            Dictionary<string, IType> types,
            Dictionary<string, GlobalVariable> globals,
            Dictionary<string, IFunction> functions,
            Dictionary<string, ParserElement> defines
        )
        {
            // element=>index table for any instruction that references another
            var placeholderTable = new Dictionary<ParserElement, int>();
            var labels = new Dictionary<string, int>();
            var aliases = new Dictionary<string, string>();

            // first pass: set up all possible instructions, placeholders for those that reference labels.
            // for a label, put it in the table and move on
            // for an alias, put it in the table and move on
            foreach (var insn in instructions)
            {
                if (insn.ParentType == ElementParentType.Attribute) continue;
                if (insn.ParentType == ElementParentType.Label)
                {
                    if (labels.ContainsKey(insn.Value)) insn.ThrowInvalid(string.Format("In function \"{0}\": Label already used", function.Name));
                    labels.Add(insn.Value, function.Instructions.Count);
                    continue;
                }
                if (insn.ParentType == ElementParentType.Alias)
                {
                    if (aliases.ContainsKey(insn.Next.Value)) insn.Next.ThrowInvalid(string.Format("In function \"{0}\": Alias already used", function.Name));
                    aliases.Add(insn.Next.Value, insn.Next.Next.Value);
                    continue;
                }
                if (insn.ParentType != ElementParentType.Instruction) insn.ThrowInvalid();

                function.Instructions.Add(ParseInstructionFirstPass(insn, function, types, globals, functions, aliases, placeholderTable, defines));
            }

            // second pass: fix up instructions that reference labels.
            ParserElement next = null;
            int labelIdx = default;
            foreach (var placeholder in placeholderTable)
            {
                var insn = placeholder.Key;

                var opcode = OpCodes.ByName[insn.Value]; // already checked earlier, no chance of KeyNotFoundException

                switch (opcode.OperandType)
                {
                    case OperandType.InlineBrTarget:
                        next = insn.Next;
                        if (next == null) insn.ThrowInvalid();
                        next.ExpectValidName();
                        next.EnsureNoNextChild();
                        if (!labels.TryGetValue(next.Value, out labelIdx)) insn.ThrowInvalid(string.Format("In function \"{0}\": Referenced unknown label", function.Name));
                        if (next.Next != null) next.Next.ThrowInvalid();
                        function.Instructions[placeholder.Value].Replace(Instruction.Create(opcode, function.Instructions[labelIdx]));
                        function.Instructions[labelIdx].Referenced = true;
                        break;
                    case OperandType.InlineBrTargetValue:
                        next = insn.Next;
                        if (next == null) insn.ThrowInvalid();
                        next.ExpectValidName();
                        next.EnsureNoNextChild();
                        if (!labels.TryGetValue(next.Value, out labelIdx)) insn.ThrowInvalid(string.Format("In function \"{0}\": Referenced unknown label", function.Name));
                        if (next.Next == null) next.ThrowInvalid();
                        next = next.Next;
                        var op1 = ParseOperandValue(next, function, types, globals, functions, aliases, defines);
                        if (next.Next != null) next.Next.ThrowInvalid();
                        function.Instructions[placeholder.Value].Replace(Instruction.Create(opcode, function.Instructions[labelIdx], op1));
                        function.Instructions[labelIdx].Referenced = true;
                        break;
                    case OperandType.InlineEH:
                        {
                            Span<int> labelIdxes = stackalloc int[4];
                            next = insn.Next;
                            if (next == null) insn.ThrowInvalid();
                            next.ExpectValidName();
                            next.EnsureNoNextChild();
                            ParseLabelEH(function, next, labels, out labelIdxes[0]);
                            var catchStart = next;
                            if (next.Next == null) next.ThrowInvalid();
                            next = next.Next;
                            next.ExpectValidName();
                            next.EnsureNoNextChild();
                            ParseLabelEH(function, next, labels, out labelIdxes[1]);
                            var finallyStart = next;
                            if (next.Next == null) next.ThrowInvalid();
                            next = next.Next;
                            next.ExpectValidName();
                            next.EnsureNoNextChild();
                            ParseLabelEH(function, next, labels, out labelIdxes[2]);
                            var finallyCatch = next;
                            if (next.Next == null) next.ThrowInvalid();
                            next = next.Next;
                            next.ExpectValidName();
                            next.EnsureNoNextChild();
                            if (!labels.TryGetValue(next.Value, out labelIdxes[3])) insn.ThrowInvalid(string.Format("In function \"{0}\": Referenced unknown label", function.Name));
                            if (next.Next != null) next.Next.ThrowInvalid();
                            if (labelIdxes[0] == -1 && labelIdxes[1] == -1)
                            {
                                catchStart.ThrowInvalid(string.Format("In function \"{0}\": Finally start and catch start can not both be", function.Name));
                            }
                            function.Instructions[placeholder.Value].Replace(Instruction.CreateStartEH(
                                labelIdxes[0] == -1 ? null : function.Instructions[labelIdxes[0]],
                                labelIdxes[1] == -1 ? null : function.Instructions[labelIdxes[1]],
                                labelIdxes[2] == -1 ? null : function.Instructions[labelIdxes[2]],
                                labelIdxes[3] == -1 ? null : function.Instructions[labelIdxes[3]]
                            ));
                            for (int i = 0; i < labelIdxes.Length; i++)
                            {
                                if (labelIdxes[i] == -1) continue;
                                function.Instructions[labelIdxes[i]].Referenced = true;
                            }
                            break;
                        }
                    default:
                        insn.ThrowInvalid(string.Format("In function \"{0}\": Unknown opcode", function.Name));
                        break;
                }
            }
        }

        public static Script Assemble(List<ParsedBody> parsed)
        {
            int version = Script.VERSION_HIGHEST;
            {
                var elemVersion = ExpectZeroOrOne(parsed, ElementParentType.FileVersion)?.Element.Next?.Value;
                if (elemVersion != null) version = int.Parse(elemVersion);
            }
            var ret = new Script(version);

            string entryPoint = ExpectZeroOrOne(parsed, ElementParentType.EntryPoint)?.Element.Next?.Value;

            // types
            var typesTable = new Dictionary<string, IType>();
            foreach (var typeElem in parsed.OfType(ElementParentType.Type))
            {
                var type = ParseType(typeElem.Element, typesTable);
                foreach (var attr in typeElem.Children.OfType(ElementParentType.Attribute))
                    type.Attributes.Add(ParseAttribute(attr, typesTable, null));
                ret.Types.Add(type);
            }

            // globals
            var globalsTable = new Dictionary<string, GlobalVariable>();
            {
                var i = 0;
                foreach (var globalElem in parsed.OfType(ElementParentType.GlobalVariable))
                {
                    ret.GlobalVariables.Add(ParseGlobal(globalElem.Element, typesTable, globalsTable, i));
                    i++;
                }
            }

            // defines
            var definesTable = new Dictionary<string, ParserElement>();
            foreach (var define in parsed.OfType(ElementParentType.Define))
            {
                var name = define.Element.Next.Next;
                if (definesTable.ContainsKey(name.Value)) name.ThrowInvalid("Immediate value already defined");
                definesTable.Add(define.Element.Next.Next.Value, define.Element.Next);
            }

            // functions
            var functionsTable = new Dictionary<string, IFunction>();
            var funcElemTable = new Dictionary<ParsedBody, ScriptFunction>();
            foreach (var funcElem in parsed.OfType(ElementParentType.Function))
            {
                var func = ParseFunction(funcElem.Element, typesTable, functionsTable);
                foreach (var attr in funcElem.Children.OfType(ElementParentType.Attribute))
                    func.Attributes.Add(ParseAttribute(attr, typesTable, functionsTable));
                ret.Functions.Add(func);
                var sf = func as ScriptFunction;
                if (sf == null) continue;
                funcElemTable.Add(funcElem, sf);
            }

            foreach (var funcElem in parsed.OfType(ElementParentType.Function).Where(x => x.Children.Any()))
            {
                ParseInstructions(funcElemTable[funcElem], funcElem.Children, typesTable, globalsTable, functionsTable, definesTable);
                if (funcElemTable[funcElem].Instructions.Count == 0)
                {
                    funcElem.Element.Tokens.Last().ThrowInvalid(string.Format("In function {0}: no instructions specified", funcElemTable[funcElem].Name));
                }
            }

            if (entryPoint != null) ret.EntryPoint = functionsTable[entryPoint];

            return ret;
        }

        public static Script Assemble(string str) => Assemble(Parser.Parse(str));
    }
}
