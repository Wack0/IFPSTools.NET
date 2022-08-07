using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IFPSLib.Types
{
    /// <summary>
    /// Set of an enumeration, implemented internally as a bit vector.
    /// </summary>
    public class SetType : TypeBase
    {
        public override PascalTypeCode BaseType => PascalTypeCode.Set;

        private int m_SizeInBits;

        public int BitSize
        {
            get => m_SizeInBits;
            internal set
            {
                if (value < 0 || value > 0x100) throw new ArgumentOutOfRangeException(nameof(value));
                m_SizeInBits = value;
            }
        }

        public int ByteSize
        {
            get
            {
                var ret = m_SizeInBits / 8; // could be >> 3 except readability -- should get optimised by something
                var extraBits = m_SizeInBits % 8; // could be & 7 except readability -- should get optimised by something
                // the "clever" (branchless) implementation would be: (extraBits / extraBits)
                // instead, have the optimiser do its job
                var hasExtraBits = extraBits != 0;
                if (hasExtraBits) ret++;
                return ret;
            }
        }

        public SetType(int sizeInBits)
        {
            BitSize = sizeInBits;
        }

        internal BitArray Load(BinaryReader br)
        {
            var bytes = new byte[ByteSize];
            br.Read(bytes);
            var ret = new BitArray(bytes);
            ret.Length = BitSize;
            return ret;
        }

        internal void Save(BinaryWriter bw, BitArray val)
        {
            var bytes = new byte[ByteSize];
            val.CopyTo(bytes, 0);
            bw.Write(bytes);
        }

        internal override void SaveCore(BinaryWriter bw, Script.SaveContext ctx)
        {
            bw.Write<int>(BitSize);
        }

        public override string ToString()
        {
            return string.Format(base.ToString() + "set({0}) {1}", BitSize, Name);
        }
    }
}
