using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using System.Runtime.CompilerServices;

namespace uninno
{
    [Flags]
    public enum LogFlags : uint
    {
        AdminInstalled = 1 << 0,
        DisableRecordChecksums = 1 << 1,
        ModernStyle = 1 << 2,
        AlwaysRestart = 1 << 3,
        ChangesEnvironment = 1 << 4,
        Win64 = 1 << 5,
        PowerUserInstalled = 1 << 6,
        AdminInstallMode = 1 << 7,
    }
    public class LogHeader
    {
        private const string HEADER_32 = "Inno Setup Uninstall Log (b)";
        private const string HEADER_64 = "Inno Setup Uninstall Log (b) 64-bit";

        [StructLayout(LayoutKind.Explicit, Size = sizeof(int) * 27)]
        public struct ReservedData
        {

        }

        public struct UnmanagedPart
        {
#pragma warning disable CS0649 //  Most of this is don't care, some gets set by reference anyway...
            internal int Version, NumRecs;
            internal uint EndOffset;
            internal LogFlags Flags;
            internal ReservedData Reserved;
            internal int CRC;
#pragma warning restore CS0649
        }
        public bool Is64Bit;
        public string AppId;
        public string AppName;

        public UnmanagedPart Unmanaged;

        public LogHeader()
        {
            // This value has been the same for over 10 years.
            // For a unicode uninstaller, it will rectify this automatically.
            Unmanaged.Version = 48;

        }

        public static int ByteLength => 0x40 + 0x80 + 0x80 + Unsafe.SizeOf<UnmanagedPart>();

        internal static LogHeader LoadForMimic(Stream stream)
        {
            byte[] buffer = new byte[0x80];
            var br = new BinaryReader(new MemoryStream(buffer, 0, 0x80, true, true), Encoding.UTF8, true);
            var ret = new LogHeader();
            stream.Read(buffer, 0, 0x40);
            var header = br.ReadAsciiStringTerminated();
            ret.Is64Bit = header == HEADER_64;
            if (!ret.Is64Bit && header != HEADER_32) throw new InvalidDataException("Invalid uninstall config header");
            br.BaseStream.Position = 0;
            stream.Read(buffer, 0, 0x80);
            ret.AppId = br.ReadAsciiStringTerminated();
            br.BaseStream.Position = 0;
            stream.Read(buffer, 0, 0x80);
            ret.AppName = br.ReadAsciiStringTerminated();
            return ret;
        }

        private void SaveCore(BinaryWriter bw)
        {
            if (Is64Bit) Unmanaged.Flags = LogFlags.Win64;
            bw.WriteAsciiStringStatic(Is64Bit ? HEADER_64 : HEADER_32, 0x40);
            bw.WriteAsciiStringStatic(AppId, 0x80);
            bw.WriteAsciiStringStatic(AppName, 0x80);
            bw.Write(Unmanaged);
        }

        internal void Save(BinaryWriter bw)
        {
            CRC32.Save(bw, SaveCore);
        }
    }
}
