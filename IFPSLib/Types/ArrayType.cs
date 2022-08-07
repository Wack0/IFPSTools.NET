using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IFPSLib.Types
{
    public abstract class ArrayTypeBase : TypeBase
    {
        public abstract IType ElementType { get; internal set; }
    }

    public sealed class ArrayType : ArrayTypeBase
    {
        public override IType ElementType { get; internal set; }

        public override PascalTypeCode BaseType => PascalTypeCode.Array;

        private ArrayType() { }

        public ArrayType(IType Element)
        {
            ElementType = Element;
        }

        internal static new ArrayType Load(BinaryReader br, Script script)
        {
            var ret = new ArrayType();
            var idxType = br.Read<uint>();
            ret.ElementType = script.Types[(int)idxType];
            return ret;
        }

        internal override void SaveCore(BinaryWriter bw, Script.SaveContext ctx)
        {
            bw.Write<int>(ctx.GetTypeIndex(ElementType));
        }

        public override string ToString()
        {
            return string.Format(base.ToString() + "array({0}) {1}", ElementType.Name, Name);
        }
    }

    public sealed class StaticArrayType : ArrayTypeBase
    {
        public override IType ElementType { get; internal set; }

        public override PascalTypeCode BaseType => PascalTypeCode.StaticArray;

        public int Size { get; internal set; }
        public int StartIndex { get; internal set; }

        private const int MAXIMIUM_SIZE = (int)(uint.MaxValue / sizeof(uint));

        private StaticArrayType() { }

        public StaticArrayType(IType Element, int size, int startIndex = 0)
        {
            ElementType = Element;
            if (size > MAXIMIUM_SIZE) throw new ArgumentOutOfRangeException(nameof(size));
            Size = size;
            StartIndex = startIndex;
        }

        internal static new StaticArrayType Load(BinaryReader br, Script script)
        {
            var ret = new StaticArrayType();
            var idxType = br.Read<uint>();
            ret.ElementType = script.Types[(int)idxType];
            ret.Size = br.Read<int>();
            if (ret.Size > MAXIMIUM_SIZE) throw new InvalidDataException();
            if (script.FileVersion >= Script.VERSION_MIN_STATICARRAYSTART)
            {
                ret.StartIndex = br.Read<int>();
            }
            return ret;
        }

        internal override void SaveCore(BinaryWriter bw, Script.SaveContext ctx)
        {
            bw.Write<int>(ctx.GetTypeIndex(ElementType));
            if (Size > MAXIMIUM_SIZE) throw new InvalidDataException(string.Format("Static array length (0x{0:x}) too large, maximum 0x{1:x}", Size, MAXIMIUM_SIZE));
            bw.Write<int>(Size);
            if (ctx.FileVersion >= Script.VERSION_MIN_STATICARRAYSTART)
                bw.Write<int>(StartIndex);
        }

        public override string ToString()
        {
            return string.Format(base.ToString() + "array({0},{1},{2}) {3}", ElementType.Name, Size, StartIndex, Name);
        }
    }
}
