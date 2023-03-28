using IFPSLib.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace IFPSLib.Emit
{
    /// <summary>
    /// Internal function implemented by script bytecode.
    /// </summary>
    public sealed class ScriptFunction : FunctionBase
    {
        public override IType ReturnArgument { get; set; }

        /// <summary>
        /// If null, argument information is unknown.
        /// </summary>
        public override IList<FunctionArgument> Arguments { get; set; }

        public IList<Instruction> Instructions = new List<Instruction>();

        internal uint CodeOffset;
        internal uint CodeLength;

        private const char ARGUMENT_TYPE_IN = '@';
        private const char ARGUMENT_TYPE_OUT = '!';

        internal static ScriptFunction Load(BinaryReader br, Script script, bool exported)
        {

            var ret = new ScriptFunction();
            ret.Exported = exported;
            ret.CodeOffset = br.Read<uint>();
            ret.CodeLength = br.Read<uint>();

            if (exported)
            {
                var nameLen = br.Read<uint>();
                ret.Name = br.ReadAsciiString(nameLen);
                // Function declaration for a scriptfunction is ascii strings split by space
                var decLen = br.Read<uint>();
                var decl = br.ReadAsciiString(decLen).Split(' ');
                // First one is return type, or -1 for void
                if (!int.TryParse(decl[0], out var retType) || retType == -1) ret.ReturnArgument = null;
                else ret.ReturnArgument = script.Types[retType];

                // Next are arguments. First char is the argument type ('@' means in, otherwise '!'), followed by the type index
                ret.Arguments = new List<FunctionArgument>(decl.Length - 1);
                for (int i = 1; i < decl.Length; i++)
                {
                    var arg = new FunctionArgument();
                    arg.ArgumentType = (decl[i][0] == ARGUMENT_TYPE_IN ? FunctionArgumentType.In : FunctionArgumentType.Out);
                    if (!int.TryParse(decl[i].Substring(1), out var idxType) || idxType < 0) arg.Type = null;
                    else arg.Type = script.Types[idxType];
                    arg.Name = string.Format("Arg{0}", i);
                    ret.Arguments.Add(arg);
                }
            }
            else
            {
                ret.Name = string.Format("func_{0:x}", ret.CodeOffset);
                ret.Arguments = new List<FunctionArgument>();
            }

            return ret;
        }

        internal void LoadInstructions(BinaryReader br, Script script)
        {
            br = br.Slice(CodeOffset, CodeLength);

            // Load all instructions
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                Instructions.Add(Instruction.Load(br, script, this));
            }

            // Create a table of offset->instruction
            var table = new Dictionary<uint, Instruction>();
            foreach (var i in Instructions) table.Add(i.Offset, i);

            // Walk each instruction and fix up branch targets
            foreach (var i in Instructions)
            {
                switch (i.OpCode.OperandType)
                {
                    case OperandType.InlineBrTarget:
                    case OperandType.InlineBrTargetValue:
                    case OperandType.InlineEH:
                        i.FixUpBranchTargets(table);
                        break;
                }
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder(".function");

            if (Exported) sb.Append("(export)");
            sb.Append(' ');

            if (ReturnArgument == null) sb.Append("void ");
            else sb.AppendFormat("{0} ", ReturnArgument.Name);

            sb.AppendFormat("{0}(", Name);
            for (int i = 0; i < Arguments.Count; i++)
            {
                sb.Append(i == 0 ? "" : ",");
                sb.Append(Arguments[i].ToString());
            }
            sb.Append(')');

            return sb.ToString();
        }

        

        private string ExportDeclString(Script.SaveContext ctx)
        {
            if (!Exported) return string.Empty;
            int typeIdx;
            if (ReturnArgument == null) typeIdx = -1;
            else typeIdx = ctx.GetTypeIndex(ReturnArgument);
            var sb = new StringBuilder(typeIdx.ToString());
            foreach (var arg in Arguments)
            {
                sb.Append(' ');
                sb.Append(arg.ArgumentType == FunctionArgumentType.In ? ARGUMENT_TYPE_IN : ARGUMENT_TYPE_OUT);
                sb.Append(ctx.GetTypeIndex(arg.Type));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Gets the size of a not exported ScriptFunction.
        /// </summary>
        public override int Size => sizeof(uint) * 2;

        /// <summary>
        /// Gets the size of all instructions.
        /// </summary>
        public int InstructionsSize => Instructions.Sum(x => x.Size);

        public ArgumentVariable CreateArgumentVariable(int index)
        {
            var isVoid = ReturnArgument == null;
            var ret = ArgumentVariable.Create(index + (isVoid ? 0 : 1), isVoid);
            ret.Name = Arguments[index].Name;
            return ret;
        }

        public ArgumentVariable CreateArgumentVariable(int index, string name)
        {
            var ret = CreateArgumentVariable(index);
            ret.Name = name;
            return ret;
        }

        public ArgumentVariable CreateReturnVariable()
        {
            if (ReturnArgument == null) throw new InvalidOperationException();
            return ArgumentVariable.Create(0, false);
        }

        internal override void Save(BinaryWriter bw, Script.SaveContext ctx)
        {
            FunctionFlags flags = 0;
            var EDecl = ExportDeclString(ctx);
            if (Exported)
            {
                flags |= FunctionFlags.Exported;
            }
            if (Attributes.Count != 0) flags |= FunctionFlags.HasAttributes;
            bw.Write(flags);

            // save dummy offset and length, will be overwritten later.
            bw.Write<uint>(0);
            bw.Write<uint>(0);

            if (Exported)
            {
                bw.WriteAsciiString(Name.ToUpper(), true);
                bw.WriteAsciiString(EDecl, true);
            }
        }

        public uint UpdateInstructionOffsets()
        {
            uint offset = 0;
            var count = Instructions.Count;
            for (int i = 0; i < count; i++)
            {
                Instructions[i].Offset = offset;
                offset += (uint)Instructions[i].Size;
            }

            return offset;
        }

        private Dictionary<Instruction, uint> UpdateInstructionCrossReferencesBeforeSave()
        {
            do
            {
                // Build the table.
                var table = new Dictionary<Instruction, uint>();
                var knownOffsets = new HashSet<uint>();
                foreach (var insn in Instructions)
                {
                    if (!knownOffsets.Add(insn.Offset))
                    {
                        UpdateInstructionOffsets();
                        continue;
                    }
                    table.Add(insn, insn.Offset);
                }

                // Fix up the targets.
                foreach (var insn in Instructions)
                    insn.FixUpReferences(table);

                return table;
            } while (true);
        }

        public void UpdateInstructionCrossReferences()
        {
            UpdateInstructionCrossReferencesBeforeSave();
        }

        internal void SaveInstructions(BinaryWriter bw, Script.SaveContext ctx)
        {
            UpdateInstructionOffsets();
            var table = UpdateInstructionCrossReferencesBeforeSave();

            var insnOffset = bw.BaseStream.Position;
            if (insnOffset > uint.MaxValue) throw new InvalidOperationException("Instruction offset must be in the first 4GB");
            var insnOffset32 = (uint)insnOffset;

            foreach (var insn in Instructions)
            {
                insn.Save(bw, ctx, table);
            }

            var insnAfter = bw.BaseStream.Position;
            var insnsLength = insnAfter - insnOffset;
            if (insnsLength > uint.MaxValue) throw new InvalidOperationException("Instructions length cannot be over 4GB");

            bw.BaseStream.Position = ctx.GetFunctionOffset(this);
            bw.Write<uint>(insnOffset32);
            bw.Write<uint>((uint)insnsLength);
            bw.BaseStream.Position = insnAfter;
        }
    }
}
