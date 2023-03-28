using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Emit
{
    public static class OpCodes
    {

        private static void FillTable(OpCode[] table, OpCode op)
        {
            for (int i = 0; i < table.Length; i++)
                if (table[i] == null) table[i] = op;
        }

        /// <summary>
        /// All one-byte opcodes
        /// </summary>
        public static readonly OpCode[] OneByteOpCodes = new OpCode[byte.MaxValue + 1];

        /// <summary>
        /// All two-byte ALU opcodes (first byte is 0x01)
        /// </summary>
        public static readonly OpCode[] AluOpCodes = new OpCode[(int)AluOpCode.Xor + 1];

        /// <summary>
        /// All two-byte comparison opcodes (first byte is 0x0C)
        /// </summary>
        public static readonly OpCode[] CmpOpCodes = new OpCode[(int)CmpOpCode.Is + 1];

        /// <summary>
        /// All two-byte exception handler leave opcodes (first byte is 0x14)
        /// </summary>
        public static readonly OpCode[] PopEHOpCodes = new OpCode[(int)PopEHOpCode.EndHandler + 1];

        /// <summary>
        /// All two-byte set flag opcodes (first byte is 0x11).
        /// The second byte is after the operand for some reason.
        /// </summary>
        public static readonly OpCode[] SetFlagOpCodes = new OpCode[(int)SetFlagOpCode.Zero + 1];

        /// <summary>
        /// All non-experimental opcodes, indexed by opcode name.
        /// </summary>
        public static IReadOnlyDictionary<string, OpCode> ByName => m_OpCodesByName;

        internal static readonly Dictionary<string, OpCode> m_OpCodesByName = new Dictionary<string, OpCode>();

#pragma warning disable 1591 // disable XML doc warning                              
        public static readonly OpCode UNKNOWN1 = new OpCode("UNKNOWN1", Code.UNKNOWN1, OperandType.InlineNone, FlowControl.Next, OpCodeType.Experimental, StackBehaviour.Push0, StackBehaviour.Pop0, true);
        public static readonly OpCode UNKNOWN_ALU = new OpCode("UNKNOWN_ALU", Code.UNKNOWN_ALU, OperandType.InlineNone, FlowControl.Next, OpCodeType.Experimental, StackBehaviour.Push0, StackBehaviour.Pop0, true);
        public static readonly OpCode UNKNOWN_CMP = new OpCode("UNKNOWN_CMP", Code.UNKNOWN_CMP, OperandType.InlineNone, FlowControl.Next, OpCodeType.Experimental, StackBehaviour.Push0, StackBehaviour.Pop0, true);
        public static readonly OpCode UNKNOWN_POPEH = new OpCode("UNKNOWN_POPEH", Code.UNKNOWN_POPEH, OperandType.InlineNone, FlowControl.Next, OpCodeType.Experimental, StackBehaviour.Push0, StackBehaviour.Pop0, true);
        public static readonly OpCode UNKNOWN_SF = new OpCode("UNKNOWN_SF", Code.UNKNOWN_SF, OperandType.InlineValueSF, FlowControl.Next, OpCodeType.Experimental, StackBehaviour.Push0, StackBehaviour.Pop0, true);

        /// <summary>
        /// Loads the second value (usually an immediate) into the first value; <c>op0 = op1</c><br/>
        /// If op0 is a pointer, then op1 is written to that pointer; <c>*op0 = op1</c>
        /// </summary>
        public static readonly OpCode Assign = new OpCode("assign", Code.Assign, OperandType.InlineValueValue, FlowControl.Next, OpCodeType.Primitive);

        /// <summary>
        /// Adds the second value to the first value; <c>op0 += op1</c>
        /// </summary>
        public static readonly OpCode Add = new OpCode("add", Code.Add, OperandType.InlineValueValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// Subtracts the second value from the first value; <c>op0 -= op1</c>
        /// </summary>
        public static readonly OpCode Sub = new OpCode("sub", Code.Sub, OperandType.InlineValueValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// Multiplies the second value with the first value; <c>op0 *= op1</c>
        /// </summary>
        public static readonly OpCode Mul = new OpCode("mul", Code.Mul, OperandType.InlineValueValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// Divides the second value from the first value; <c>op0 /= op1</c>
        /// </summary>
        public static readonly OpCode Div = new OpCode("div", Code.Div, OperandType.InlineValueValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// Modulo divides the second value from the first value; <c>op0 %= op1</c>
        /// </summary>
        public static readonly OpCode Mod = new OpCode("mod", Code.Mod, OperandType.InlineValueValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// Shifts the first value left by the second value; <c>op0 &lt;&lt;= op1</c>
        /// </summary>
        public static readonly OpCode Shl = new OpCode("shl", Code.Shl, OperandType.InlineValueValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// Shifts the first value right by the second value; <c>op0 >>= op1</c>
        /// </summary>
        public static readonly OpCode Shr = new OpCode("shr", Code.Shr, OperandType.InlineValueValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// Bitwise ANDs the first value by the second value; <c>op0 &amp;= op1</c>
        /// </summary>
        public static readonly OpCode And = new OpCode("and", Code.And, OperandType.InlineValueValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// Bitwise ORs the first value by the second value; <c>op0 |= op1</c>
        /// </summary>
        public static readonly OpCode Or = new OpCode("or", Code.Or, OperandType.InlineValueValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// Bitwise XORs the first value by the second value; <c>op0 ^= op1</c>
        /// </summary>
        public static readonly OpCode Xor = new OpCode("xor", Code.Xor, OperandType.InlineValueValue, FlowControl.Next, OpCodeType.Macro);

        /// <summary>
        /// Pushes the operand to the top of the stack.
        /// </summary>
        public static readonly OpCode Push = new OpCode("push", Code.Push, OperandType.InlineValue, FlowControl.Next, OpCodeType.Primitive, StackBehaviour.Push1);
        /// <summary>
        /// Pushes a pointer to the operand to the top of the stack.
        /// </summary>
        public static readonly OpCode PushVar = new OpCode("pushvar", Code.PushVar, OperandType.InlineValue, FlowControl.Next, OpCodeType.Primitive, StackBehaviour.Push1);
        /// <summary>
        /// Removes the value currently on top of the stack.<br/>
        /// If that value is a return address, or the stack is empty, the operation is invalid.
        /// </summary>
        public static readonly OpCode Pop = new OpCode("pop", Code.Pop, OperandType.InlineNone, FlowControl.Next, OpCodeType.Primitive, StackBehaviour.Push0, StackBehaviour.Pop1);
        /// <summary>
        /// Calls the function indicated by the provided index.<br/>
        /// Return address gets pushed to the top of the stack (function index, offset to next instruction, stack pointer).<br/><br/>
        /// Calling convention:
        /// <list type="bullet">
        /// <item><description>push arguments onto the stack, from last to first</description></item>
        /// <item><description>if function returns a value, push a pointer to the return value</description></item>
        /// </list>
        /// </summary>
        public static readonly OpCode Call = new OpCode("call", Code.Call, OperandType.InlineFunction, FlowControl.Call, OpCodeType.Primitive);
        /// <summary>
        /// Unconditionally branches to the target <see cref="Instruction"/>.
        /// </summary>
        public static readonly OpCode Jump = new OpCode("jump", Code.Jump, OperandType.InlineBrTarget, FlowControl.Branch, OpCodeType.Primitive);
        /// <summary>
        /// If the value operand is not zero, branch to the target <see cref="Instruction"/>.
        /// </summary>
        public static readonly OpCode JumpNZ = new OpCode("jnz", Code.JumpNZ, OperandType.InlineBrTargetValue, FlowControl.Cond_Branch, OpCodeType.Primitive);
        /// <summary>
        /// If the value operand is zero, branch to the target <see cref="Instruction"/>.
        /// </summary>
        public static readonly OpCode JumpZ = new OpCode("jz", Code.JumpZ, OperandType.InlineBrTargetValue, FlowControl.Cond_Branch, OpCodeType.Primitive);
        /// <summary>
        /// Returns from the current function.<br/>
        /// Any finally blocks inside exception handlers will be executed first.<br/>
        /// Pops the entirety of the current function's stack frame.<br/>
        /// </summary>
        public static readonly OpCode Ret = new OpCode("ret", Code.Ret, OperandType.InlineNone, FlowControl.Return, OpCodeType.Primitive, StackBehaviour.Push0, StackBehaviour.Varpop);
        /// <summary>
        /// <b>Removed between version 20 (1.20) and 22 (1.30), after that point this is an invalid opcode.</b><br/>
        /// Destructs the given variable, creates a new one of the provided type.<br/>
        /// If the provided type is <see cref="Types.PascalTypeCode.ReturnAddress"/> then the operation is invalid.
        /// </summary>
        public static readonly OpCode SetStackType = new OpCode("setstacktype", Code.SetStackType, OperandType.InlineTypeVariable, FlowControl.Next, OpCodeType.Primitive);
        /// <summary>
        /// Pushes a new uninitialised value of the given type to the top of the stack.
        /// </summary>
        public static readonly OpCode PushType = new OpCode("pushtype", Code.PushType, OperandType.InlineType, FlowControl.Next, OpCodeType.Primitive, StackBehaviour.Push1);

        /// <summary>
        /// <c>op0 = op1 >= op2</c>
        /// </summary>
        public static readonly OpCode Ge = new OpCode("ge", Code.Ge, OperandType.InlineCmpValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// <c>op0 = op1 <= op2</c>
        /// </summary>
        public static readonly OpCode Le = new OpCode("le", Code.Le, OperandType.InlineCmpValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// <c>op0 = op1 > op2</c>
        /// </summary>
        public static readonly OpCode Gt = new OpCode("gt", Code.Gt, OperandType.InlineCmpValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// <c>op0 = op1 < op2</c>
        /// </summary>
        public static readonly OpCode Lt = new OpCode("lt", Code.Lt, OperandType.InlineCmpValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// <c>op0 = op1 != op2</c>
        /// </summary>
        public static readonly OpCode Ne = new OpCode("ne", Code.Ne, OperandType.InlineCmpValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// <c>op0 = op1 == op2</c>
        /// </summary>
        public static readonly OpCode Eq = new OpCode("eq", Code.Eq, OperandType.InlineCmpValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// If operand 2 is Variant[], checks if operand 1 when converted to COM VARIANT is in the array.<br/>
        /// If operand 2 is a Set, checks if operand 1 is in the Set.<br/>
        /// Invalid if operand 2 is any other type.<br/>
        /// Result (0 or 1) is placed in operand 0
        /// </summary>
        public static readonly OpCode In = new OpCode("in", Code.In, OperandType.InlineCmpValue, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// Operand 1 must be a <see cref="Types.ClassType"/>, operand 2 is a <see cref="Types.TypeType"/> that must be a <see cref="Types.ClassType"/>
        /// Checks if operand 1 is of the <see cref="Types.ClassType"/> referenced by operand 2.
        /// </summary>
        public static readonly OpCode Is = new OpCode("is", Code.Is, OperandType.InlineCmpValueType, FlowControl.Next, OpCodeType.Macro);

        /// <summary>
        /// Calls the function indicated by the value operand (as a <see cref="Types.FunctionPointerType"/> to the entry point).
        /// </summary>
        public static readonly OpCode CallVar = new OpCode("callvar", Code.CallVar, OperandType.InlineValue, FlowControl.Call, OpCodeType.Primitive, StackBehaviour.Push1);
        /// <summary>
        /// op0 must be a pointer, which may be a null pointer.<br/>
        /// If op1 is not a pointer, loads the equivalent address of op1 into op0; <c>op0 = &amp;op1</c><br/>
        /// If op1 is a pointer, loads op1 into op0; <c>op0 = op1</c>
        /// </summary>
        public static readonly OpCode SetPtr = new OpCode("setptr", Code.SetPtr, OperandType.InlineValueValue, FlowControl.Next, OpCodeType.Primitive);
        /// <summary>
        /// <c>op0 = op0 == 0</c>
        /// </summary>
        public static readonly OpCode SetZ = new OpCode("setz", Code.SetZ, OperandType.InlineValue, FlowControl.Next, OpCodeType.Primitive);
        /// <summary>
        /// <c>op0 = -op0</c>
        /// </summary>
        public static readonly OpCode Neg = new OpCode("neg", Code.Neg, OperandType.InlineValue, FlowControl.Next, OpCodeType.Primitive);

        /// <summary>
        /// <c>jf = op0 != 0</c>
        /// </summary>
        public static readonly OpCode SetFlagNZ = new OpCode("sfnz", Code.SetFlagNZ, OperandType.InlineValueSF, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// <c>jf = op0 == 0</c>
        /// </summary>
        public static readonly OpCode SetFlagZ = new OpCode("sfz", Code.SetFlagZ, OperandType.InlineValueSF, FlowControl.Next, OpCodeType.Macro);

        /// <summary>
        /// If the jump flag (set or unset by <see cref="SetFlagNZ"/> or <see cref="SetFlagZ"/>) is set, branch to the target <see cref="Instruction"/>
        /// </summary>
        public static readonly OpCode JumpF = new OpCode("jf", Code.JumpF, OperandType.InlineBrTarget, FlowControl.Next, OpCodeType.Primitive);
        /// <summary>
        /// Initialises an exception handler.<br/>
        /// The operands are four target <see cref="Instruction"/>s which may be <c>null</c>.<br/>
        /// op0 is the instruction that starts a Finally block.
        /// op1 is the instruction that starts a Catch block.
        /// op2 is the instruction that starts a Finally block after a Catch block.
        /// op3 is the instruction after the last exception handler block.
        /// </summary>
        public static readonly OpCode StartEH = new OpCode("starteh", Code.StartEH, OperandType.InlineEH, FlowControl.Next, OpCodeType.Primitive);

        /// <summary>
        /// Leaves the current Try block and unconditionally branches to the end of the exception handler.
        /// </summary>
        public static readonly OpCode EndTry = new OpCode("endtry", Code.EndTry, OperandType.InlineNone, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// Leaves the current Finally block and unconditionally branches to the end of the exception handler.
        /// </summary>
        public static readonly OpCode EndFinally = new OpCode("endfinally", Code.EndFinally, OperandType.InlineNone, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// Leaves the current Catch block and unconditionally branches to the end of the exception handler.
        /// </summary>
        public static readonly OpCode EndCatch = new OpCode("endcatch", Code.EndCatch, OperandType.InlineNone, FlowControl.Next, OpCodeType.Macro);
        /// <summary>
        /// Leaves the current Finally block after a Catch block and unconditionally branches to the end of the exception handler.
        /// </summary>
        public static readonly OpCode EndCF = new OpCode("endcf", Code.EndCF, OperandType.InlineNone, FlowControl.Next, OpCodeType.Macro);

        /// <summary>
        /// <c>op0 = ~op0</c>
        /// </summary>
        public static readonly OpCode Not = new OpCode("not", Code.Not, OperandType.InlineValue, FlowControl.Next, OpCodeType.Primitive);
        /// <summary>
        /// Copy constructor.<br/>
        /// Operand 0 must be a pointer, which may be a null pointer.<br/>
        /// First, <c>op0</c> is set to newly constructed pointer of <c>op1</c>'s type (if <c>op1</c> is a pointer, then <c>*op1</c>'s type).<br/>
        /// If Operand 1 is a pointer, <c>*op0 = *op1</c><br/>
        /// Otherwise, <c>*op0 = op1</c>
        /// </summary>
        public static readonly OpCode Cpval = new OpCode("cpval", Code.Cpval, OperandType.InlineValueValue, FlowControl.Next, OpCodeType.Primitive);
        /// <summary>
        /// <c>op0++</c>
        /// </summary>
        public static readonly OpCode Inc = new OpCode("inc", Code.Inc, OperandType.InlineValue, FlowControl.Next, OpCodeType.Primitive);
        /// <summary>
        /// <c>op0--</c>
        /// </summary>
        public static readonly OpCode Dec = new OpCode("dec", Code.Dec, OperandType.InlineValue, FlowControl.Next, OpCodeType.Primitive);
        /// <summary>
        /// Pops one value off of the stack (with no type restriction) and branches to the target <see cref="Instruction"/>
        /// </summary>
        public static readonly OpCode PopJump = new OpCode("popjump", Code.PopJump, OperandType.InlineBrTarget, FlowControl.Branch, OpCodeType.Primitive, StackBehaviour.Push0, StackBehaviour.Pop1);
        /// <summary>
        /// Pops two values off of the stack (with no type restriction) and branches to the target <see cref="Instruction"/>
        /// </summary>
        public static readonly OpCode PopPopJump = new OpCode("poppopjump", Code.PopPopJump, OperandType.InlineBrTarget, FlowControl.Branch, OpCodeType.Primitive, StackBehaviour.Push0, StackBehaviour.Pop2);

        /// <summary>
        /// No operation
        /// </summary>
        public static readonly OpCode Nop = new OpCode("nop", Code.Nop, OperandType.InlineNone, FlowControl.Next, OpCodeType.Primitive);



#pragma warning restore
        static OpCodes()
        {
            FillTable(OneByteOpCodes, UNKNOWN1);
            FillTable(AluOpCodes, UNKNOWN_ALU);
            FillTable(CmpOpCodes, UNKNOWN_CMP);
            FillTable(PopEHOpCodes, UNKNOWN_POPEH);
            FillTable(SetFlagOpCodes, UNKNOWN_SF);
        }
    }
}
