using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Emit
{

    public enum PopEHOpCode : byte
    {
        EndTry,
        EndFinally,
        EndCatch,
        EndHandler
    }
}
