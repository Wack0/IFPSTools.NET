using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSLib.Types
{
    /// <summary>
    /// Pseudo-type describing a function index.
    /// </summary>
    public class ImmediateFunctionType : TypeBase
    {
        public override PascalTypeCode BaseType => PascalTypeCode.Function;

        private ImmediateFunctionType() { }

        public static readonly ImmediateFunctionType Instance = new ImmediateFunctionType();
    }
}
