using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Emit
{
    public class OpCode
    {
        /// <summary>
        /// Gets the size of the opcode, either 1 or 2 bytes.
        /// </summary>
        public int Size
        {
            get
            {
                var firstByte = (Code)(((short)Code) >> 8);
                if (firstByte == Code.Compare || firstByte == Code.Calculate || firstByte == Code.PopEH || firstByte == Code.SetFlag)
                    return sizeof(short);
                else return sizeof(byte);
            }
        }

        public OpCode(string name, byte first, byte second, OperandType operandType, FlowControl flowControl, StackBehaviour push, StackBehaviour pop)
            : this(name, (Code)((first << 8) | second), operandType, flowControl, OpCodeType.Experimental, push, pop, true)
        { }

        internal OpCode(string name, Code code, OperandType operandType, FlowControl flowControl, OpCodeType opCodeType,
            StackBehaviour push = StackBehaviour.Push0, StackBehaviour pop = StackBehaviour.Pop0, bool experimental = false
        )
        {
            Name = name;
            Code = code;
            OperandType = operandType;
            FlowControl = flowControl;
            OpcodeType = opCodeType;
            StackBehaviourPush = push;
            StackBehaviourPop = pop;
            if (!experimental)
            {
                OpCode[] arr = null;
                switch ((Code)(((short)Code) >> 8))
                {
                    case Code.Calculate:
                        arr = OpCodes.AluOpCodes;
                        break;
                    case Code.Compare:
                        arr = OpCodes.CmpOpCodes;
                        break;
                    case Code.PopEH:
                        arr = OpCodes.PopEHOpCodes;
                        break;
                    case Code.SetFlag:
                        arr = OpCodes.SetFlagOpCodes;
                        break;
                    case 0:
                        arr = OpCodes.OneByteOpCodes;
                        break;
                }
                if (arr != null) arr[(short)Code & 0xff] = this;
                OpCodes.m_OpCodesByName[name] = this;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// The opcode name
        /// </summary>
        public readonly string Name;
        /// <summary>
        /// The opcode as a <see cref="Code"/> enum
        /// </summary>
        public readonly Code Code;
        /// <summary>
        /// Operand type
        /// </summary>
        public readonly OperandType OperandType;
        /// <summary>
        /// Flow control info
        /// </summary>
        public readonly FlowControl FlowControl;
        /// <summary>
        /// Opcode type
        /// </summary>
        public readonly OpCodeType OpcodeType;
        /// <summary>
        /// Push stack behaviour
        /// </summary>
        public readonly StackBehaviour StackBehaviourPush;
        /// <summary>
        /// Pop stack behaviour
        /// </summary>
        public readonly StackBehaviour StackBehaviourPop;
    }
}
