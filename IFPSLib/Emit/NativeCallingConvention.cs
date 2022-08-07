using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Emit
{
    public enum NativeCallingConvention : byte
    {
        Register,
        Pascal,
        CDecl,
        Stdcall
    }
}
