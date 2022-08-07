using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Types
{
    /// <summary>
    /// Psuedo-type describing an instruction.
    /// </summary>
    public class InstructionType : TypeBase
    {
        private InstructionType() { }
        public override PascalTypeCode BaseType => PascalTypeCode.Instruction;

        public readonly static InstructionType Instance = new InstructionType();
    }
}
