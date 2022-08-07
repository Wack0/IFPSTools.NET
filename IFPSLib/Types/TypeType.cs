using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Types
{
    /// <summary>
    /// Pseudo-type describing a type.
    /// </summary>
    public class TypeType : TypeBase
    {
        private TypeType() { }
        public override PascalTypeCode BaseType => PascalTypeCode.Type;

        public readonly static TypeType Instance = new TypeType();
    }
}
