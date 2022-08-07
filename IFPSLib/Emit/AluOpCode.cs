using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Emit
{
    /// <summary>
    /// Type of an ALU operation for the "calculate" instruction at the bytecode level.
    /// </summary>
    public enum AluOpCode : byte
    {
        Add,
        Sub,
        Mul,
        Div,
        Mod,
        Shl,
        Shr,
        And,
        Or,
        Xor,
    }
}
