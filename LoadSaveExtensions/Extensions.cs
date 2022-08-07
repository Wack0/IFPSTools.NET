using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Text;

internal static class LoadSaveExtensions
{

    private static MethodInfo s_GetBuffer = typeof(MemoryStream).GetMethod("InternalGetBuffer", BindingFlags.Instance | BindingFlags.NonPublic);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] InternalGetBuffer(this MemoryStream stream)
    {
        try
        {
            return stream.GetBuffer();
        }
        catch
        {
            return (byte[])s_GetBuffer.Invoke(stream, Array.Empty<object>());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CheckLength(this Stream stream, Span<byte> span, long position)
    {
        var expectedLength = span.Length;
        var remainingLength = stream.Length - position;
        if (remainingLength < expectedLength) throw new EndOfStreamException();
    }

    private static BinaryReader SliceCore(this MemoryStream stream, long position, long length)
    {
        checked
        {
            if (position > int.MaxValue || length > int.MaxValue) throw new EndOfStreamException();
            var buffer = new byte[(int)length];
            Buffer.BlockCopy(stream.InternalGetBuffer(), (int)position, buffer, 0, (int)length);
            return new BinaryReader(new MemoryStream(buffer, 0, buffer.Length, false, true), Encoding.UTF8, true);
        }
    }

    private static BinaryReader SliceCore(this UnmanagedMemoryStream stream, long position, long length)
    {
        checked
        {
            if ((position + length) > stream.Length) throw new EndOfStreamException();

            unsafe
            {
                return new BinaryReader(new UnmanagedMemoryStream(&stream.PositionPointer[position - stream.Position], length), Encoding.UTF8, true);
            }
        }
    }

    private static string ReadTerminatedCore(this MemoryStream stream, int position, byte terminator)
    {
        long pos64 = position;
        if (position < 0) pos64 = stream.Position;

        var underlyingBuffer = stream.InternalGetBuffer();

        for (long i = 0; i + pos64 < stream.Length; i++)
        {
            if (underlyingBuffer[pos64 + i] == terminator)
            {
                stream.Position += i + 1;
                unsafe
                {
                    fixed (byte* ptr = &underlyingBuffer[pos64])
                        return Encoding.ASCII.GetString(ptr, (int)i);
                }
            }
        }

        return string.Empty;
    }

    private static string ReadTerminatedCore(this UnmanagedMemoryStream stream, int position, byte terminator)
    {
        long pos64 = position;
        if (position < 0) pos64 = stream.Position;

        unsafe
        {
            var ptr = stream.PositionPointer;
            for (long i = 0; i + pos64 < stream.Length; i++)
            {
                if (ptr[i] == terminator)
                {
                    stream.Position += i + 1;
                    return Encoding.ASCII.GetString(ptr, (int)i);
                }
            }
        }

        return string.Empty;
    }

    private static void WriteTerminatedCore(this MemoryStream stream, string str, byte terminator)
    {
        // Ensure that there's enough space available in the buffer.
        var oldPosition = stream.Position;
        var count = Encoding.ASCII.GetByteCount(str);
        stream.Position += count;
        stream.WriteByte(terminator);
        // Write the actual bytes.
        var underlyingBuffer = stream.InternalGetBuffer();

        unsafe
        {
            fixed (byte* ptr = &underlyingBuffer[oldPosition])
            fixed (char* pStr = str)
                Encoding.ASCII.GetBytes(pStr, str.Length, ptr, count);
        }
    }

    private static void WriteAsciiStringCore(this MemoryStream stream, string str, bool withLength)
    {
        var count = Encoding.ASCII.GetByteCount(str);
        // Write the length if needed.
        if (withLength)
        {
            stream.WriteCore(AsSpan(ref count).AsBytes());
        }
        // Ensure that there's enough space available in the buffer.
        var oldPosition = stream.Position;
        stream.Position += count - 1;
        stream.WriteByte(0);
        // Write the actual bytes.
        var underlyingBuffer = stream.InternalGetBuffer();

        unsafe
        {
            fixed (byte* ptr = &underlyingBuffer[oldPosition])
            fixed (char* pStr = str)
                Encoding.ASCII.GetBytes(pStr, str.Length, ptr, count);
        }
    }

    private static void WriteUnicodeStringCore(this MemoryStream stream, string str)
    {
        // Ensure that there's enough space available in the buffer.
        var oldPosition = stream.Position;
        var count = Encoding.Unicode.GetByteCount(str);
        stream.Position += count - 1;
        stream.WriteByte(0);
        // Write the actual bytes.
        var underlyingBuffer = stream.InternalGetBuffer();

        unsafe
        {
            fixed (byte* ptr = &underlyingBuffer[oldPosition])
            fixed (char* pStr = str)
                Encoding.Unicode.GetBytes(pStr, str.Length, ptr, count);
        }
    }

    private static void ReadCore(this MemoryStream stream, Span<byte> span, int position)
    {
        long pos64 = position;
        if (position < 0) pos64 = stream.Position;
        stream.CheckLength(span, pos64);
        var underlyingBuffer = stream.InternalGetBuffer();

        unsafe
        {
            fixed (byte* ptr = &underlyingBuffer[pos64])
                Buffer.MemoryCopy(ptr, Unsafe.AsPointer(ref span[0]), span.Length, span.Length);
        }
        stream.Position += span.Length;
    }

    private static void ReadCore(this UnmanagedMemoryStream stream, Span<byte> span, int position)
    {
        long pos64 = position;
        if (position < 0) pos64 = stream.Position;
        stream.CheckLength(span, pos64);
        unsafe
        {
            Buffer.MemoryCopy(&stream.PositionPointer[pos64 - stream.Position], Unsafe.AsPointer(ref span[0]), span.Length, span.Length);
        }
        stream.Position += span.Length;
    }

    private static void WriteCore(this MemoryStream stream, Span<byte> span)
    {
        // Ensure that there's enough space available in the buffer.
        var oldPosition = stream.Position;
        stream.Position += span.Length - 1;
        stream.WriteByte(0);
        // Write the actual bytes.
        var underlyingBuffer = stream.InternalGetBuffer();

        unsafe
        {
            fixed (byte* ptr = &underlyingBuffer[oldPosition])
                Buffer.MemoryCopy(Unsafe.AsPointer(ref span[0]), ptr, span.Length, span.Length);
        }
    }


    internal static void Read<T>(this BinaryReader br, Span<T> span) where T : unmanaged
    {
        Read(br, span.AsBytes(), -1);
    }

    internal static void Write<T>(this BinaryWriter bw, Span<T> span) where T : unmanaged
    {
        Write(bw, span.AsBytes());
    }

    internal static string ReadAsciiString(this BinaryReader br, uint length, int position = -1)
    {
        if (length == 0) return string.Empty;
        const int MAX_LENGTH = 0x40000000;
        if (length > MAX_LENGTH) throw new ArgumentOutOfRangeException(nameof(length));
        Span<byte> span = stackalloc byte[(int)length];
        br.Read(span, position);
        unsafe
        {
            return Encoding.ASCII.GetString((byte*)Unsafe.AsPointer(ref span[0]), (int)length);
        }
    }

    internal static void WriteAsciiString(this BinaryWriter bw, string str, bool withLength = false)
    {
        if (str.Length == 0) return;
        const int MAX_LENGTH = 0x40000000;
        if (str.Length > MAX_LENGTH) throw new ArgumentOutOfRangeException(nameof(str));

        bw.GetBaseCore().WriteAsciiStringCore(str, withLength);
        return;
    }

    internal static void WriteAsciiStringStatic(this BinaryWriter bw, string str, int length)
    {
        if (length == 0) return;
        var _base = bw.GetBaseCore();

        if (str.Length > length) _base.WriteAsciiStringCore(str.Substring(0, length), false);
        else if (str.Length == length) _base.WriteAsciiStringCore(str, false);
        else
        {
            _base.WriteAsciiStringCore(str, false);
            // write a terminator
            _base.WriteByte(0);
            // write another terminator at the end of the buffer
            _base.Position += (length - str.Length - 2);
            _base.WriteByte(0);
        }
    }

    internal static string ReadAsciiStringTerminated(this BinaryReader br, byte terminator = 0, int position = -1)
    {
        var stream = br.BaseStream;
        switch (stream)
        {
            case MemoryStream ms:
                return ms.ReadTerminatedCore(position, terminator);
            case UnmanagedMemoryStream ums:
                return ums.ReadTerminatedCore(position, terminator);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    internal static void WriteAsciiStringTerminated(this BinaryWriter bw, string str, byte terminator = 0)
    {
        if (str.Length == 0) return;
        const int MAX_LENGTH = 0x40000000;
        if (str.Length > MAX_LENGTH) throw new ArgumentOutOfRangeException(nameof(str));

        bw.GetBaseCore().WriteTerminatedCore(str, terminator);
        return;
    }

    internal static string ParseAsciiString(this Span<byte> bytes, int offset, int length)
    {
        if (offset + length > bytes.Length) return string.Empty;
        unsafe
        {
            return Encoding.ASCII.GetString((byte*)Unsafe.AsPointer(ref bytes[offset]), length);
        }
    }

    internal static bool EqualsAsciiString(this Span<byte> bytes, int offset, string str)
    {
        return bytes.ParseAsciiString(offset, str.Length) == str;
    }

    internal static string ReadUnicodeString(this BinaryReader br, uint length, int position = -1)
    {
        if (length == 0) return string.Empty;
        const int MAX_LENGTH = 0x40000000;
        if (length > MAX_LENGTH) throw new ArgumentOutOfRangeException(nameof(length));
        length *= 2;
        Span<byte> span = stackalloc byte[(int)length];
        br.Read(span, position);
        unsafe
        {
            return Encoding.Unicode.GetString((byte*)Unsafe.AsPointer(ref span[0]), (int)length);
        }
    }

    internal static void WriteUnicodeString(this BinaryWriter bw, string str)
    {
        if (str.Length == 0) return;
        const int MAX_LENGTH = 0x40000000;
        if (str.Length > MAX_LENGTH) throw new ArgumentOutOfRangeException(nameof(str));

        bw.GetBaseCore().WriteUnicodeStringCore(str);
        return;
    }

    internal static void Read(this BinaryReader br, Span<byte> span, int position = -1)
    {
        if (span.Length == 0) return;
        var stream = br.BaseStream;
        switch (stream)
        {
            case MemoryStream ms:
                ms.ReadCore(span, position);
                return;
            case UnmanagedMemoryStream ums:
                ums.ReadCore(span, position);
                return;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static MemoryStream GetBaseCore(this BinaryWriter bw)
    {
        var stream = bw.BaseStream as MemoryStream;
        if (stream == null) throw new ArgumentOutOfRangeException();
        return stream;
    }

    internal static void Write(this BinaryWriter bw, Span<byte> span)
    {
        if (span.Length == 0) return;
        bw.GetBaseCore().WriteCore(span);
    }

    internal static void Write(this BinaryWriter bw, MemoryStream ms)
    {
        bw.Write(new Span<byte>(ms.InternalGetBuffer(), 0, (int)ms.Length));
    }

    internal static byte[] GetBase(this BinaryWriter bw)
    {
        return bw.GetBaseCore().InternalGetBuffer();
    }

    internal static BinaryReader Slice(this BinaryReader br, long position, long length)
    {
        var stream = br.BaseStream;
        switch (stream)
        {
            case MemoryStream ms:
                return ms.SliceCore(position, length);
            case UnmanagedMemoryStream ums:
                return ums.SliceCore(position, length);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    internal static T Read<T>(this BinaryReader br) where T : unmanaged
    {
        var val = default(T);
        var span = AsSpan(ref val);
        br.Read(span);
        return val;
    }

    internal static void Write<T>(this BinaryWriter bw, T val) where T : unmanaged
    {
        var span = AsSpan(ref val);
        bw.Write(span);
    }

    internal static Span<T> AsSpan<T>(ref T val) where T : unmanaged
    {
        unsafe
        {
            return new Span<T>(Unsafe.AsPointer(ref val), 1);
        }
    }

    internal static Span<byte> AsBytes<T>(this Span<T> span) where T : unmanaged
    {
        unsafe
        {
            return new Span<byte>(Unsafe.AsPointer(ref span[0]), span.Length * Unsafe.SizeOf<T>());
        }
    }

    internal static Span<T> AsOther<T>(this Span<byte> span) where T : unmanaged
    {
        unsafe
        {
            return new Span<T>(Unsafe.AsPointer(ref span[0]), span.Length / Unsafe.SizeOf<T>());
        }
    }

    internal static string ToLiteral(this string input)
    {
        StringBuilder literal = new StringBuilder(input.Length + 2);
        literal.Append("\"");
        foreach (var c in input)
        {
            switch (c)
            {
                case '\"': literal.Append("\\\""); break;
                case '\\': literal.Append(@"\\"); break;
                case '\0': literal.Append(@"\0"); break;
                case '\a': literal.Append(@"\a"); break;
                case '\b': literal.Append(@"\b"); break;
                case '\f': literal.Append(@"\f"); break;
                case '\n': literal.Append(@"\n"); break;
                case '\r': literal.Append(@"\r"); break;
                case '\t': literal.Append(@"\t"); break;
                case '\v': literal.Append(@"\v"); break;
                default:
                    // ASCII printable character
                    if ((c >= 0x20 && c <= 0x7e) || !char.IsControl(c))
                    {
                        literal.Append(c);
                        // As UTF16 escaped character
                    }
                    else if (c < 0x100)
                    {
                        literal.Append(@"\x");
                        literal.Append(((int)c).ToString("x2"));
                    }
                    else
                    {
                        literal.Append(@"\u");
                        literal.Append(((int)c).ToString("x4"));
                    }
                    break;
            }
        }
        literal.Append("\"");
        return literal.ToString();
    }
}