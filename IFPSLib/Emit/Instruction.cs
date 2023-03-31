using IFPSLib.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections;

namespace IFPSLib.Emit
{
    public sealed class Instruction : IEnumerable<Operand>
    {
        /// <summary>
        /// The opcode
        /// </summary>
        public OpCode OpCode;
        /// <summary>
        /// The operands
        /// </summary>
        public IReadOnlyList<Operand> Operands => m_Operands.AsReadOnly();
        /// <summary>
        /// Offset of the instruction in the function body
        /// </summary>
        public uint Offset;
        /// <summary>
        /// Referenced by an operand of another instruction
        /// </summary>
        public bool Referenced;

        private List<Operand> m_Operands = new List<Operand>();

        private static readonly Operand s_opU32 = Operand.Create<uint>(0);

        /// <summary>
        /// Gets or sets the operand at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the operand to get or set.</param>
        /// <returns>The operand at the specified index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">No operand exists at the given index; or the operand to set is incompatible with the existing operand.</exception>
        public Operand this[int index]
        {
            get => m_Operands[index];
            set
            {
                if (!m_Operands[index].SimilarType(value)) throw new ArgumentOutOfRangeException(nameof(value));
                m_Operands[index] = value;
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the operands.
        /// </summary>
        /// <returns>An enumerator for the operands list.</returns>
        public IEnumerator<Operand> GetEnumerator()
        {
            return m_Operands.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the operands.
        /// </summary>
        /// <returns>An enumerator for the operands list.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_Operands.GetEnumerator();
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        internal Instruction() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="opcode">Opcode</param>
        internal Instruction(OpCode opcode)
        {
            OpCode = opcode;
            m_Operands = new List<Operand>(0);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="operands">Operands</param>
        internal Instruction(OpCode opcode, List<Operand> operands) : this(opcode)
        {
            if (operands.Any((op) => op == null)) throw new ArgumentNullException("operand");
            m_Operands = operands;
        }

        /// <summary>
        /// Creates a new instruction with no operand
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode)
        {
            if (opcode.OperandType != OperandType.InlineNone) throw new ArgumentOutOfRangeException(nameof(opcode), "Must be a no-operand opcode");
            return new Instruction(opcode);
        }


        /// <summary>
        /// Creates a new instruction with one operand
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="operand">Operand</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode, Operand operand)
        {
            if (opcode.OperandType != OperandType.InlineValue && opcode.OperandType != OperandType.InlineValueSF)
                throw new ArgumentOutOfRangeException(nameof(opcode), "Opcode does not have a value operand");
            return new Instruction(opcode, new List<Operand>(1) { operand });
        }


        /// <summary>
        /// Creates a new instruction with an immediate operand
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="val">Immediate operand</param>
        /// <typeparam name="TType">Type of operand, must be a PascalScript primitive</typeparam>
        /// <returns>New instruction</returns>
        public static Instruction Create<TType>(OpCode opcode, TType val)
            => Create(opcode, Operand.Create(val));

        /// <summary>
        /// Creates a new instruction with a variable operand
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="var">Variable operand</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode, IVariable var)
            => Create(opcode, Operand.Create(var));

        /// <summary>
        /// Creates a new branch instruction
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="target">Branch target</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode, Instruction target)
        {
            if (opcode.OperandType != OperandType.InlineBrTarget) throw new ArgumentOutOfRangeException(nameof(opcode), "Opcode does not have an instruction operand");
            target.Referenced = true;
            return new Instruction(opcode, new List<Operand>(1) { Operand.Create(target) });
        }

        /// <summary>
        /// Creates a new instruction with two operands
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="op0">First operand</param>
        /// <param name="op1">Second operand</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode, Operand op0, Operand op1)
        {
            if (opcode.OperandType != OperandType.InlineValueValue) throw new ArgumentOutOfRangeException(nameof(opcode), "Opcode does not have two value operands");
            return new Instruction(opcode, new List<Operand>(2) { op0, op1 });
        }

        /// <summary>
        /// Creates a new instruction with two variable operands
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="op0">First operand</param>
        /// <param name="op1">Second operand</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode, IVariable op0, IVariable op1)
            => Create(opcode, Operand.Create(op0), Operand.Create(op1));

        /// <summary>
        /// Creates a new instruction with a variable operand and an immediate operand
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="op0">First operand</param>
        /// <param name="op1">Second operand</param>
        /// <typeparam name="TType">Type of second operand, must be a PascalScript primitive</typeparam>
        /// <returns>New instruction</returns>
        public static Instruction Create<TType>(OpCode opcode, IVariable op0, TType val)
            => Create(opcode, Operand.Create(op0), Operand.Create(val));

        /// <summary>
        /// Creates a new instruction with two variable operands
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="op0">First operand</param>
        /// <param name="op1">Second operand</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode, IVariable op0, Operand op1)
            => Create(opcode, Operand.Create(op0), op1);

        /// <summary>
        /// Creates a new branch instruction with an operand
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="target">Branch target</param>
        /// <param name="operand">Operand</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode, Instruction target, Operand operand)
        {
            if (opcode.OperandType != OperandType.InlineBrTargetValue) throw new ArgumentOutOfRangeException(nameof(opcode), "Opcode does not have an instruction and a value operand");
            return new Instruction(opcode, new List<Operand>(2) { Operand.Create(target), operand });
        }

        /// <summary>
        /// Creates a new branch instruction with a variable operand
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="target">Branch target</param>
        /// <param name="operand">Operand</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode, Instruction target, IVariable var)
            => Create(opcode, target, Operand.Create(var));

        /// <summary>
        /// Creates a new call instruction
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="func">Function</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode, IFunction func)
        {
            if (opcode.OperandType != OperandType.InlineFunction) throw new ArgumentOutOfRangeException(nameof(opcode), "Opcode does not have a function operand");
            return new Instruction(opcode, new List<Operand>(1) { Operand.Create(func) });
        }

        /// <summary>
        /// Creates a new instruction with a type operand
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="type">Type</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode, Types.IType type)
        {
            if (opcode.OperandType != OperandType.InlineType) throw new ArgumentOutOfRangeException(nameof(opcode), "Opcode does not have a function operand");
            return new Instruction(opcode, new List<Operand>(1) { Operand.Create(type) });
        }

        /// <summary>
        /// Creates a new compare instruction
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="op0">Destination</param>
        /// <param name="op1">Left hand side</param>
        /// <param name="op2">Right hand side</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode, Operand op0, Operand op1, Operand op2)
        {
            if (opcode.OperandType != OperandType.InlineCmpValue) throw new ArgumentOutOfRangeException(nameof(opcode), "Opcode does not have three value operands");
            return new Instruction(opcode, new List<Operand>(3) { op0, op1, op2 });
        }

        /// <summary>
        /// Creates a new compare instruction with two variables and an immediate
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="op0">Destination</param>
        /// <param name="op1">Left hand side</param>
        /// <param name="op2">Right hand side</param>
        /// <typeparam name="TType">Type of right hand side, must be a PascalScript primitive</typeparam>
        /// <returns>New instruction</returns>
        public static Instruction Create<TType>(OpCode opcode, IVariable op0, IVariable op1, TType op2)
            => Create(opcode, Operand.Create(op0), Operand.Create(op1), Operand.Create(op2));

        /// <summary>
        /// Creates a new compare instruction with three variables
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="op0">Destination</param>
        /// <param name="op1">Left hand side</param>
        /// <param name="op2">Right hand side</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode, IVariable op0, IVariable op1, IVariable op2)
            => Create(opcode, Operand.Create(op0), Operand.Create(op1), Operand.Create(op2));

        /// <summary>
        /// Creates a new compare-type instruction
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="op0">Destination</param>
        /// <param name="op1">Left hand side</param>
        /// <param name="type">Right hand side (type)</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode, Operand op0, Operand op1, Types.IType type)
        {
            if (opcode.OperandType != OperandType.InlineCmpValueType) throw new ArgumentOutOfRangeException(nameof(opcode), "Opcode does not have two value operands and a type operand");
            return new Instruction(opcode, new List<Operand>(3) { op0, op1, Operand.Create(type) });
        }

        /// <summary>
        /// Creates a new compare-type instruction with two variables
        /// </summary>
        /// <param name="opcode">Opcode</param>
        /// <param name="op0">Destination</param>
        /// <param name="op1">Left hand side</param>
        /// <param name="type">Right hand side (type)</param>
        /// <returns>New instruction</returns>
        public static Instruction Create(OpCode opcode, IVariable op0, IVariable op1, Types.IType type)
            => Create(opcode, Operand.Create(op0), Operand.Create(op1), type);

        /// <summary>
        /// Creates a new StartEH instruction
        /// </summary>
        /// <param name="Finally">Start of first finally block</param>
        /// <param name="Catch">Start of catch block</param>
        /// <param name="CatchFinally">Start of second finally block</param>
        /// <param name="End">End of last exception handler block</param>
        /// <returns>New instruction</returns>
        public static Instruction CreateStartEH(Instruction Finally, Instruction Catch, Instruction CatchFinally, Instruction End)
        {
            return new Instruction(OpCodes.StartEH, new List<Operand>(4) { Operand.Create(Finally), Operand.Create(Catch), Operand.Create(CatchFinally), Operand.Create(End) });
        }

        /// <summary>
        /// Creates a new StartEH instruction for a try-finally block
        /// </summary>
        /// <param name="Finally">Start of finally block</param>
        /// <param name="End">End of finally block</param>
        /// <returns>New instruction</returns>
        public static Instruction CreateTryFinally(Instruction Finally, Instruction End)
            => CreateStartEH(Finally, null, null, End);

        /// <summary>
        /// Create a new StartEH instruction for a try-catch block
        /// </summary>
        /// <param name="Catch">Start of catch block</param>
        /// <param name="End">End of catch block</param>
        /// <returns>New instruction</returns>
        public static Instruction CreateTryCatch(Instruction Catch, Instruction End)
            => CreateStartEH(null, Catch, null, End);

        /// <summary>
        /// Create a new StartEH instruction for a try-catch-finally block
        /// </summary>
        /// <param name="Catch">Start of catch block</param>
        /// <param name="Finally">Start of finally block</param>
        /// <param name="End">End of finally block</param>
        /// <returns>New instruction</returns>
        public static Instruction CreateTryCatchFinally(Instruction Catch, Instruction Finally, Instruction End)
            => CreateStartEH(null, Catch, Finally, End);

        /// <summary>
        /// Creates a SetStackType instruction (removed after version 21 or 22)
        /// </summary>
        /// <param name="type">Type to set variable to</param>
        /// <param name="variable">Variable to set</param>
        /// <returns>New instruction</returns>
        public static Instruction CreateSetStackType(Types.IType type, IVariable variable)
        {
            return new Instruction(OpCodes.SetStackType, new List<Operand>(2) { Operand.Create(type), Operand.Create(variable) });
        }

        /// <summary>
        /// Replaces this instruction with the content of another. Use instead of replacing an instruction in an array in case it is referenced by another instruction.
        /// </summary>
        /// <param name="insn">New instruction</param>
        public void Replace(Instruction insn)
        {
            OpCode = insn.OpCode;
            m_Operands = insn.m_Operands;
            Offset = insn.Offset;
            Referenced = insn.Referenced;
        }

        private static uint FixBranchOffset(BinaryReader br, uint data)
        {
            return data + (uint)br.BaseStream.Position;
        }

        private static uint FixBranchOffsetEH(BinaryReader br, uint data)
        {
            if (data == uint.MaxValue) return uint.MaxValue;
            return FixBranchOffset(br, data);
        }

        private static uint ReadBranchOffset(BinaryReader br)
        {
            return FixBranchOffset(br, br.Read<uint>());
        }

        private static Operand ReadTypeForCmpValueType(BinaryReader br, Script script, ScriptFunction function)
        {
            var operand = Operand.LoadValue(br, script, function);
            if (operand.Type == BytecodeOperandType.Immediate) return Operand.Create(script.Types[(int)operand.ImmediateAs<uint>()]);
            return operand;
        }

        internal static Instruction Load(BinaryReader br, Script script, ScriptFunction function)
        {
            var ret = new Instruction();
            ret.Offset = (uint)br.BaseStream.Position;
            OpCode opcode = null;
            var opFirstByte = (Code)br.ReadByte();
            switch (opFirstByte)
            {
                case Code.Calculate:
                    var calcSecond = (AluOpCode)br.ReadByte();
                    if (calcSecond > AluOpCode.Xor) opcode = OpCodes.UNKNOWN_ALU;
                    else opcode = OpCodes.AluOpCodes[(int)calcSecond];
                    break;
                case Code.Compare:
                    var cmpSecond = (CmpOpCode)br.ReadByte();
                    if (cmpSecond > CmpOpCode.Is) opcode = OpCodes.UNKNOWN_CMP;
                    else opcode = OpCodes.CmpOpCodes[(int)cmpSecond];
                    break;
                case Code.PopEH:
                    var popEhSecond = (PopEHOpCode)br.ReadByte();
                    if (popEhSecond > PopEHOpCode.EndHandler) opcode = OpCodes.UNKNOWN_POPEH;
                    else opcode = OpCodes.PopEHOpCodes[(int)popEhSecond];
                    break;
                case Code.SetFlag:
                    // placeholder
                    opcode = OpCodes.UNKNOWN_SF;
                    break;
                case Code.SetStackType:
                    if (script.FileVersion >= Script.VERSION_MAX_SETSTACKTYPE) opcode = OpCodes.UNKNOWN1;
                    else opcode = OpCodes.SetStackType;
                    break;
                default:
                    opcode = OpCodes.OneByteOpCodes[(int)opFirstByte];
                    break;
            }

            ret.OpCode = opcode;
            switch (opcode.OperandType)
            {
                case OperandType.InlineNone:
                    ret.m_Operands = new List<Operand>(0);
                    break;
                case OperandType.InlineValue:
                    ret.m_Operands = new List<Operand>(1) { Operand.LoadValue(br, script, function) };
                    break;
                case OperandType.InlineBrTarget:
                    ret.m_Operands = new List<Operand>(1) { new Operand(new TypedData(InstructionType.Instance, ReadBranchOffset(br))) };
                    break;
                case OperandType.InlineValueValue:
                    ret.m_Operands = new List<Operand>(2) { Operand.LoadValue(br, script, function), Operand.LoadValue(br, script, function) };
                    break;
                case OperandType.InlineBrTargetValue:
                    var brOffset = br.Read<uint>();
                    var valOp = Operand.LoadValue(br, script, function);
                    ret.m_Operands = new List<Operand>(2) { new Operand(new TypedData(InstructionType.Instance, FixBranchOffset(br, brOffset))), valOp };
                    break;
                case OperandType.InlineFunction:
                    ret.m_Operands = new List<Operand>(1) { Operand.Create(script.Functions[(int)br.Read<uint>()]) };
                    break;
                case OperandType.InlineType:
                    ret.m_Operands = new List<Operand>(1) { Operand.Create(script.Types[(int)br.Read<uint>()]) };
                    break;
                case OperandType.InlineCmpValue:
                    ret.m_Operands = new List<Operand>(3) { Operand.LoadValue(br, script, function), Operand.LoadValue(br, script, function), Operand.LoadValue(br, script, function) };
                    break;
                case OperandType.InlineCmpValueType:
                    ret.m_Operands = new List<Operand>(3) {
                        Operand.LoadValue(br, script, function),
                        Operand.LoadValue(br, script, function),
                        ReadTypeForCmpValueType(br, script, function)
                    };
                    break;
                case OperandType.InlineEH:
                    var br0 = br.Read<uint>();
                    var br1 = br.Read<uint>();
                    var br2 = br.Read<uint>();
                    var br3 = br.Read<uint>();
                    ret.m_Operands = new List<Operand>(4) {
                        new Operand(new TypedData(InstructionType.Instance, FixBranchOffsetEH(br, br0))),
                        new Operand(new TypedData(InstructionType.Instance, FixBranchOffsetEH(br, br1))),
                        new Operand(new TypedData(InstructionType.Instance, FixBranchOffsetEH(br, br2))),
                        new Operand(new TypedData(InstructionType.Instance, FixBranchOffsetEH(br, br3)))
                    };
                    break;
                case OperandType.InlineTypeVariable:
                    ret.m_Operands = new List<Operand>(2) {
                        Operand.Create(script.Types[(int)br.Read<uint>()]),
                        new Operand(VariableBase.Load(br, script, function))
                    };
                    return ret;
                case OperandType.InlineValueSF:
                    ret.m_Operands = new List<Operand>(1) { Operand.LoadValue(br, script, function) };
                    var sfSecond = (SetFlagOpCode)br.ReadByte();
                    if (sfSecond > SetFlagOpCode.Zero) opcode = OpCodes.UNKNOWN_SF;
                    else opcode = OpCodes.SetFlagOpCodes[(int)sfSecond];
                    ret.OpCode = opcode;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(string.Format("Unknown OperandType {0}", opcode.OperandType));
            }
            return ret;
        }



        internal void FixUpBranchTargets(Dictionary<uint, Instruction> table)
        {
            for (int i = 0; i < m_Operands.Count; i++)
            {
                var operand = m_Operands[i];
                if (operand.m_Type != BytecodeOperandType.Immediate) continue;
                if (operand.ImmediateTyped.Type.BaseType != PascalTypeCode.Instruction) continue;
                if (!(operand.Immediate is uint)) continue;
                var target = operand.ImmediateAs<uint>();
                if (OpCode.Code == Code.StartEH && target == uint.MaxValue) m_Operands[i] = Operand.Create((Instruction)null);
                else
                {
                    m_Operands[i] = Operand.Create(table[target]);
                    table[target].Referenced = true;
                }
            }
        }

        /// <summary>
        /// Disassembles the instruction without labelling it
        /// </summary>
        /// <returns>Instruction text</returns>
        public override string ToString()
        {
            return ToString(false);
        }

        /// <summary>
        /// Disassembles the instruction
        /// </summary>
        /// <param name="forDisasm">If true, labels the instruction if it's referenced by another instruction</param>
        /// <returns>Instruction text</returns>
        public string ToString(bool forDisasm)
        {
            var sb = new StringBuilder();
            if (Referenced) sb.AppendLine(string.Format("loc_{0}:", Offset.ToString("x")));

            if (forDisasm) sb.Append('\t');
            sb.Append(OpCode);
            for (int i = 0; i < m_Operands.Count; i++)
            {
                sb.Append(i == 0 ? " " : ", ");
                sb.Append(m_Operands[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Gets the size of the instruction
        /// </summary>
        public int Size
        {
            get
            {
                var ret = OpCode.Size;
                switch (OpCode.OperandType)
                {
                    case OperandType.InlineNone:
                        break;
                    case OperandType.InlineValue:
                    case OperandType.InlineValueSF:
                        ret += m_Operands[0].Size;
                        break;
                    case OperandType.InlineBrTarget:
                        ret += sizeof(uint);
                        break;
                    case OperandType.InlineValueValue:
                        ret += m_Operands[0].Size + m_Operands[1].Size;
                        break;
                    case OperandType.InlineBrTargetValue:
                        ret += sizeof(uint) + m_Operands[1].Size;
                        break;
                    case OperandType.InlineFunction:
                    case OperandType.InlineType:
                        ret += sizeof(int);
                        break;
                    case OperandType.InlineCmpValue:
                        ret += m_Operands[0].Size + m_Operands[1].Size + m_Operands[2].Size;
                        break;
                    case OperandType.InlineCmpValueType:
                        ret += m_Operands[0].Size + m_Operands[1].Size + (m_Operands[2].Type != BytecodeOperandType.Immediate ? m_Operands[2] : s_opU32).Size;
                        break;
                    case OperandType.InlineEH:
                        ret += sizeof(uint) * 4;
                        break;
                    case OperandType.InlineTypeVariable:
                        ret += sizeof(int) + m_Operands[1].Variable.Size;
                        break;
                }
                return ret;
            }
        }

        private void FixUpReference(int index, Dictionary<Instruction, uint> table, bool allowedNull = false)
        {
            var insn = m_Operands[index].ImmediateAs<Instruction>();
            if (insn == null)
            {
                if (!allowedNull) throw new InvalidOperationException("Instruction is null");
                return;
            }
            if (!table.ContainsKey(insn)) throw new InvalidOperationException("Referenced instruction is not in the same function as the referencing instruction");
            insn.Referenced = true;
        }

        internal void FixUpReferences(Dictionary<Instruction, uint> table)
        {
            switch (OpCode.OperandType)
            {
                case OperandType.InlineBrTarget:
                case OperandType.InlineBrTargetValue:
                    FixUpReference(0, table);
                    break;
                case OperandType.InlineEH:
                    FixUpReference(0, table, true);
                    FixUpReference(1, table, true);
                    FixUpReference(2, table, true);
                    FixUpReference(3, table, true);
                    break;
            }
        }

        private uint GetEHOffset(Operand operand, Dictionary<Instruction, uint> table)
        {
            var insn = operand.ImmediateAs<Instruction>();
            if (insn == null) return uint.MaxValue;
            return table[insn] - Offset - (uint)Size;
        }

        internal void Save(BinaryWriter bw, Script.SaveContext ctx, Dictionary<Instruction, uint> table)
        {
            if (OpCode.Size == 2)
            {
                var firstByte = (byte)((int)OpCode.Code >> 8);
                bw.Write<byte>(firstByte);
                if (OpCode.OperandType != OperandType.InlineValueSF)
                {
                    // not setflag, so second byte follows the first
                    bw.Write<byte>((byte)OpCode.Code);
                }
            }
            else if (OpCode.Size == 1)
            {
                bw.Write<byte>((byte)OpCode.Code);
            }
            else throw new InvalidOperationException(string.Format("Invalid opcode size of {0} bytes", OpCode.Size));

            switch (OpCode.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.InlineValue:
                    m_Operands[0].Save(bw, ctx);
                    break;
                case OperandType.InlineBrTarget:
                    bw.Write<uint>(table[m_Operands[0].ImmediateAs<Instruction>()] - Offset - (uint)Size);
                    break;
                case OperandType.InlineValueValue:
                    m_Operands[0].Save(bw, ctx);
                    m_Operands[1].Save(bw, ctx);
                    break;
                case OperandType.InlineBrTargetValue:
                    bw.Write<uint>(table[m_Operands[0].ImmediateAs<Instruction>()] - Offset - (uint)Size);
                    m_Operands[1].Save(bw, ctx);
                    break;
                case OperandType.InlineFunction:
                    bw.Write<int>(ctx.GetFunctionIndex(m_Operands[0].ImmediateAs<IFunction>()));
                    break;
                case OperandType.InlineType:
                    bw.Write<int>(ctx.GetTypeIndex(m_Operands[0].ImmediateAs<IType>()));
                    break;
                case OperandType.InlineCmpValue:
                    m_Operands[0].Save(bw, ctx);
                    m_Operands[1].Save(bw, ctx);
                    m_Operands[2].Save(bw, ctx);
                    break;
                case OperandType.InlineCmpValueType:
                    m_Operands[0].Save(bw, ctx);
                    m_Operands[1].Save(bw, ctx);
                    if (m_Operands[2].Type == BytecodeOperandType.Immediate)
                    {
                        Operand.Create((uint)ctx.GetTypeIndex(m_Operands[2].ImmediateAs<IType>())).Save(bw, ctx);
                    }
                    else
                    {
                        m_Operands[2].Save(bw, ctx);
                    }
                    break;
                case OperandType.InlineEH:
                    bw.Write<uint>(GetEHOffset(m_Operands[0], table));
                    bw.Write<uint>(GetEHOffset(m_Operands[1], table));
                    bw.Write<uint>(GetEHOffset(m_Operands[2], table));
                    bw.Write<uint>(GetEHOffset(m_Operands[3], table));
                    break;
                case OperandType.InlineTypeVariable:
                    bw.Write<int>(ctx.GetTypeIndex(m_Operands[0].ImmediateAs<IType>()));
                    ((VariableBase)m_Operands[1].Variable).Save(bw, ctx);
                    break;
                case OperandType.InlineValueSF:
                    m_Operands[0].Save(bw, ctx);
                    // setflag; second opcode byte is after the operand for some reason
                    bw.Write<byte>((byte)OpCode.Code);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(string.Format("Unknown OperandType {0}", OpCode.OperandType));
            }
        }
    }
}
