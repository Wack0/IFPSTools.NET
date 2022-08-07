using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Types
{
    /// <summary>
    /// Pseudo-type describing an unknown value.
    /// </summary>
    public class UnknownType : TypeBase
    {
        private UnknownType() { }
        public override PascalTypeCode BaseType => PascalTypeCode.Unknown;

        public readonly static UnknownType Instance = new UnknownType();
    }
}
