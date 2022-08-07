using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Emit
{
    public enum OpCodeType : byte
    {
        Macro,
        Prefix,
        Primitive,
        Experimental
    }
}
