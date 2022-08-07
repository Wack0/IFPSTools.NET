using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Emit
{
    /// <summary>
    /// Type of a set flag operation for the "setflag" instruction at the bytecode level.
    /// </summary>
    public enum SetFlagOpCode : byte
    {
        /// <summary>
        /// Sets the jump flag if the variable is not zero.
        /// </summary>
        NotZero,
        /// <summary>
        /// Sets the jump flag if the variable is zero.
        /// </summary>
        Zero
    }
}
