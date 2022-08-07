using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Emit
{
    public enum OperandType : byte
    {
        /// <summary>
        /// Variable or typed immediate
        /// </summary>
        InlineValue,
        /// <summary>
        /// No operand
        /// </summary>
        InlineNone,
        /// <summary>
        /// Signed 32-bit branch target
        /// </summary>
        InlineBrTarget,
        /// <summary>
        /// Two <see cref="InlineValue"/>s
        /// </summary>
        InlineValueValue,
        /// <summary>
        /// <see cref="InlineBrTarget"/> and <see cref="InlineValue"/>
        /// </summary>
        InlineBrTargetValue,
        /// <summary>
        /// 32-bit function index
        /// </summary>
        InlineFunction,
        /// <summary>
        /// 32-bit type index
        /// </summary>
        InlineType,
        /// <summary>
        /// Three <see cref="InlineValue"/>s
        /// </summary>
        InlineCmpValue,
        /// <summary>
        /// Two <see cref="InlineValue"/>s followed by <see cref="InlineType"/>
        /// </summary>
        InlineCmpValueType,
        /// <summary>
        /// Four unsigned 32-bit branch targets (uint.MaxValue for null)
        /// </summary>
        InlineEH,
        /// <summary>
        /// <see cref="InlineType"/> followed by a variable
        /// </summary>
        InlineTypeVariable,
        /// <summary>
        /// <see cref="InlineValue"/> followed by <see cref="OpCodes.SetFlag"/> second opcode byte
        /// </summary>
        InlineValueSF,
    }
}
