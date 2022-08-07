using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Emit
{
    /// <summary>
    /// A PascalScript opcode at the bytecode level.
    /// </summary>
    public enum Code : ushort
    {
        Assign,
        Calculate,
        Push,
        PushVar,
        Pop,
        Call,
        Jump,
        JumpNZ,
        JumpZ,
        Ret,
        SetStackType,
        PushType,
        Compare,
        CallVar,
        SetPtr, // removed between 2003 and 2005
        SetZ,
        Neg,
        SetFlag,
        JumpF,
        StartEH,
        PopEH,
        Not,
        Cpval,
        Inc,
        Dec,
        PopJump,
        PopPopJump,
        Nop = 0xff,

        Add = (Calculate << 8) | (AluOpCode.Add),
        Sub = (Calculate << 8) | (AluOpCode.Sub),
        Mul = (Calculate << 8) | (AluOpCode.Mul),
        Div = (Calculate << 8) | (AluOpCode.Div),
        Mod = (Calculate << 8) | (AluOpCode.Mod),
        Shl = (Calculate << 8) | (AluOpCode.Shl),
        Shr = (Calculate << 8) | (AluOpCode.Shr),
        And = (Calculate << 8) | (AluOpCode.And),
        Or = (Calculate << 8) | (AluOpCode.Or),
        Xor = (Calculate << 8) | (AluOpCode.Xor),

        Ge = (Compare << 8) | (CmpOpCode.Ge),
        Le = (Compare << 8) | (CmpOpCode.Le),
        Gt = (Compare << 8) | (CmpOpCode.Gt),
        Lt = (Compare << 8) | (CmpOpCode.Lt),
        Ne = (Compare << 8) | (CmpOpCode.Ne),
        Eq = (Compare << 8) | (CmpOpCode.Eq),
        In = (Compare << 8) | (CmpOpCode.In),
        Is = (Compare << 8) | (CmpOpCode.Is),

        SetFlagNZ = (SetFlag << 8) | (SetFlagOpCode.NotZero),
        SetFlagZ = (SetFlag << 8) | (SetFlagOpCode.Zero),

        EndTry = (PopEH << 8) | (PopEHOpCode.EndTry),
        EndFinally = (PopEH << 8) | (PopEHOpCode.EndFinally),
        EndCatch = (PopEH << 8) | (PopEHOpCode.EndCatch),
        EndCF = (PopEH << 8) | (PopEHOpCode.EndHandler),

        UNKNOWN1 = 0xff00,
        UNKNOWN_ALU,
        UNKNOWN_CMP,
        UNKNOWN_SF,
        UNKNOWN_POPEH,
    }
}
