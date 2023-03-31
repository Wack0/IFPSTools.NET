using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Emit
{
    /// <summary>
    /// Type of a PascalScript operand at the bytecode level.
    /// </summary>
    public enum BytecodeOperandType : byte
    {
        /// <summary>
        /// Operand refers to a local or global variable, or an argument.
        /// </summary>
        Variable,
        /// <summary>
        /// Operand refers to an immediate typed constant.
        /// </summary>
        Immediate,
        /// <summary>
        /// Operand refers to an element of an indexed variable, where the index is specified as an immediate 32-bit constant.
        /// </summary>
        IndexedImmediate,
        /// <summary>
        /// Operand refers to an element of an indexed variable, where the index is specified as a local or global variable, or an argument.
        /// </summary>
        IndexedVariable,
        /// <summary>
        /// Operand is invalid. Stops execution.
        /// </summary>
        Invalid = 0xff
    }
}
