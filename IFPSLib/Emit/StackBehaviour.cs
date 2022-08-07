using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Emit
{
    /// <summary>
    /// Describes how values are pushed onto or popped off a stack.
    /// </summary>
    public enum StackBehaviour : byte
    {
        /// <summary>
        /// No values are popped off the stack
        /// </summary>
        Pop0,
        /// <summary>
        /// One value is popped off the stack
        /// </summary>
        Pop1,
        /// <summary>
        /// Two values are popped off the stack
        /// </summary>
        Pop2,
        /// <summary>
        /// A variable number of values are popped off the stack
        /// </summary>
        Varpop,

        /// <summary>
        /// No values are pushed onto the stack
        /// </summary>
        Push0,
        /// <summary>
        /// One value is pushed onto the stack
        /// </summary>
        Push1
    }
}
