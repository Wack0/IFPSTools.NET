using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Emit
{
    /// <summary>
    /// Type of a compare operation for the "cmp" instruction at the bytecode level.
    /// </summary>
    public enum CmpOpCode : byte
    {
        /// <summary>
        /// Greater than or equal to
        /// </summary>
        Ge,
        /// <summary>
        /// Less than or equal to
        /// </summary>
        Le,
        /// <summary>
        /// Greater than
        /// </summary>
        Gt,
        /// <summary>
        /// Less than
        /// </summary>
        Lt,
        /// <summary>
        /// Not equal
        /// </summary>
        Ne,
        /// <summary>
        /// Equal
        /// </summary>
        Eq,
        /// <summary>
        /// If operand 2 is Variant[], checks if operand 1 when converted to COM VARIANT is in the array.
        /// If operand 2 is a Set, checks if operand 1 is in the Set.
        /// Invalid if operand 2 is any other type
        /// </summary>
        In,
        /// <summary>
        /// Operand 1 must be a Class, operand 2 is u32 type index.
        /// The type referenced by operand 2 must be a Class.
        /// Checks if operand 1 is of the Class type referenced by operand 2.
        /// </summary>
        Is
    }
}
