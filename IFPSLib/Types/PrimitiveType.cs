using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IFPSLib.Types
{
    public class PrimitiveType : TypeBase
    {
        public override PascalTypeCode BaseType { get; }

        public PrimitiveType(PascalTypeCode baseType)
        {
            if (!baseType.IsPrimitive()) throw new ArgumentOutOfRangeException(nameof(baseType));
            BaseType = baseType;
            Name = baseType.ToString();
        }

        public static PrimitiveType Create<TType>()
        {
            return new PrimitiveType(typeof(TType).ToIFPSTypeCode());
        }

        internal override void SaveCore(BinaryWriter bw, Script.SaveContext ctx)
        {
        }

        public override string ToString()
        {
            return string.Format(base.ToString() + "primitive({0}) {1}", BaseType, Name);
        }
    }
}
