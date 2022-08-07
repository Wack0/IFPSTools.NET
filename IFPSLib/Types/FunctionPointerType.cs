using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IFPSLib.Types
{
    public class FunctionPointerType : TypeBase
    {
        public override PascalTypeCode BaseType => PascalTypeCode.ProcPtr;

        public bool HasReturnValue;
        public IList<FunctionArgumentType> Arguments;

        public FunctionPointerType(bool hasReturn, IList<FunctionArgumentType> args)
        {
            HasReturnValue = hasReturn;
            Arguments = args;
        }

        internal static FunctionPointerType Load(BinaryReader br)
        {
            var length = br.Read<uint>() & 0xff;
            Span<byte> bytes = stackalloc byte[(int)length];
            br.Read(bytes);
            var hasReturn = bytes[0] != 0;
            var args = new List<FunctionArgumentType>((int)length - 1);
            for (int i = 1; i < length; i++)
            {
                args.Add(bytes[i] == 1 ? FunctionArgumentType.Out : FunctionArgumentType.In);
            }
            return new FunctionPointerType(hasReturn, args);
        }

        internal override void SaveCore(BinaryWriter bw, Script.SaveContext ctx)
        {
            int len = (Arguments.Count + 1);
            if (len > 0xff) throw new InvalidOperationException(string.Format("{0} arguments present, the maximum is 254", Arguments.Count));
            bw.Write<int>(len);
            bw.Write<bool>(HasReturnValue);
            for (int i = 0; i < Arguments.Count; i++)
            {
                bw.Write<bool>(Arguments[i] == FunctionArgumentType.Out);
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder(base.ToString() + "funcptr(");
            sb.Append(HasReturnValue ? "returnsval" : "void");
            sb.Append('(');
            for (int i = 0; i < Arguments.Count; i++)
            {
                sb.Append(i == 0 ? "" : ",");
                sb.Append(Arguments[i] == FunctionArgumentType.In ? "__in" : "__out");
            }
            sb.AppendFormat(")) {0}", Name);
            return sb.ToString();
        }
    }
}
