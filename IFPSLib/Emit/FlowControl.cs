using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Emit
{
    /// <summary>
    /// Describes how an instruction alters control flow.
    /// </summary>
    public enum FlowControl
    {
        /// <summary>
        /// Instruction branches to another block.
        /// </summary>
        Branch,
        /// <summary>
        /// Instruction calls a function.
        /// </summary>
        Call,
        /// <summary>
        /// Instruction branches to another block if a condition passes.
        /// </summary>
        Cond_Branch,
        /// <summary>
        /// Normal flow of control; to the next instruction.
        /// </summary>
        Next,
        /// <summary>
        /// Returns to the caller.
        /// </summary>
        Return
    }
}
