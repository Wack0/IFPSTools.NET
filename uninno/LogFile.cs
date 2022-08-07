using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace uninno
{
    public enum RecordType : ushort
    {
        UserDefined = 0x01,

        StartInstall = 0x10,
        EndInstall = 0x11,

        CompiledCode = 0x20,

        Run = 0x80,
        DeleteDirOrFiles = 0x81,
        DeleteFile = 0x82,
        DeleteGroupOrItem = 0x83,
        IniDeleteEntry = 0x84,
        IniDeleteSection = 0x85,
        RegDeleteEntireKey = 0x86,
        RegClearValue = 0x87,
        RegDeleteKeyIfEmpty = 0x88,
        RegDeleteValue = 0x89,
        DecrementSharedCount = 0x8A,
        RefreshFileAssoc = 0x8B,
        MexCheck = 0x8C,
    }

    internal struct BlockHeader
    {
        internal uint Size, NotSize, CRC;

        internal BlockHeader(uint Size, uint CRC)
        {
            this.Size = Size;
            NotSize = ~Size;
            this.CRC = CRC;
        }
    }

    public struct Record
    {
        public byte[] Data;
        private readonly RecordType type;
        private readonly uint ExtraData;

        public Record(RecordType type, byte[] data, uint ExtraData = 0)
        {
            this.type = type;
            Data = data;
            this.ExtraData = ExtraData;
        }

        private const int HEADER_LENGTH = sizeof(RecordType) + sizeof(uint) + sizeof(uint);

        private static void WriteToSpan<T>(ref Span<byte> span, T val) where T : unmanaged
        {
            span.AsOther<T>()[0] = val;
            span = span.Slice(Unsafe.SizeOf<T>());
        }

        private static bool TryWrite(Span<byte> pageBuffer, ReadOnlySpan<byte> source, ref int bufOffset, ref int recOffset, bool body)
        {
            if (bufOffset >= pageBuffer.Length) return true;
            var sourceOffset = recOffset;
            if (body) sourceOffset -= HEADER_LENGTH;
            var realLen = Math.Min(pageBuffer.Length - bufOffset, source.Length - sourceOffset);
            if (realLen == 0) return true;
            var realSource = source.Slice(sourceOffset, realLen);
            realSource.CopyTo(pageBuffer.Slice(bufOffset));
            recOffset += realLen;
            bufOffset += realLen;
            return realLen + sourceOffset != source.Length;
        }

        internal bool FillBuffer(Span<byte> pageBuffer, ref int bufOffset, ref int recOffset)
        {
            if (recOffset < HEADER_LENGTH)
            {
                Span<byte> header = stackalloc byte[HEADER_LENGTH];
                {
                    var part = header;
                    WriteToSpan(ref part, type);
                    WriteToSpan(ref part, ExtraData);
                    WriteToSpan(ref part, Data.Length);
                    //WriteToSpan(ref part, (byte)0xFE); // << 32bit length, negative means unicode
                    //WriteToSpan(ref part, Data.Length * (Unicode ? -1 : 1));
                }
                if (TryWrite(pageBuffer, header, ref bufOffset, ref recOffset, false)) return true;
            }
            return TryWrite(pageBuffer, Data, ref bufOffset, ref recOffset, true);
        }
    }

    public class LogFile
    {
        public LogHeader Header = new LogHeader();
        public List<Record> Records = new List<Record>();

        public static MemoryStream ReadBody(Stream stream)
        {
            var ms = new MemoryStream();
            using (var br = new BinaryReader(stream, Encoding.UTF8, true))
            {
                while (stream.Position < stream.Length)
                {
                    // length
                    var size = br.ReadUInt32();
                    // ~length
                    var notsize = br.ReadUInt32();
                    // crc
                    var crc = br.ReadUInt32();
                    if (~size != notsize) throw new InvalidDataException("Block header incorrect");
                    // read the block
                    var block = new byte[size];
                    stream.Read(block, 0, (int)size);
                    // check the crc
                    if (CRC32.Get(block) != crc) throw new InvalidDataException("Block CRC incorrect");
                    // write to memorystream
                    ms.Write(block, 0, (int)size);
                }
            }
            ms.Position = 0;
            return ms;
        }

        public void Save(BinaryWriter bw)
        {
            // write dummy header
            var pos = bw.BaseStream.Position;
            Header.Unmanaged.NumRecs = Records.Count;
            bw.BaseStream.Position += LogHeader.ByteLength - 1;
            bw.BaseStream.WriteByte(0);

            const int PAGE_LENGTH = 0x1000;
            Span<byte> pageBuffer = stackalloc byte[PAGE_LENGTH];
            int bufOffset = 0, recOffset = 0;

            foreach (var record in Records)
            {
                while (record.FillBuffer(pageBuffer, ref bufOffset, ref recOffset))
                {
                    // Buffer is full. Write this block.
                    var block = new BlockHeader(PAGE_LENGTH, CRC32.Get(pageBuffer));
                    bw.Write(block);
                    bw.Write(pageBuffer);
                    bufOffset = 0;
                }
                recOffset = 0;
            }

            if (bufOffset != 0)
            {
                // Buffer was partially wrote to. Write the final block.
                var partial = pageBuffer.Slice(0, bufOffset);
                var block = new BlockHeader((uint)bufOffset, CRC32.Get(partial));
                bw.Write(block);
                bw.Write(partial);
            }

            // Fill in the file length and write the header.
            var posAfter = bw.BaseStream.Position;
            Header.Unmanaged.EndOffset = (uint)bw.BaseStream.Length;
            bw.BaseStream.Position = pos;
            Header.Save(bw);
            bw.BaseStream.Position = pos;
        }
    }
}
