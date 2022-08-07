using Cysharp.Collections;
using IFPSLib.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IFPSLib.Emit
{
    namespace FDecl
    {
        public abstract class Base
        {
            internal bool HasReturnArgument;

            /// <summary>
            /// If null, argument information is unknown.
            /// </summary>
            internal IList<FunctionArgument> Arguments { get; set; }

            internal virtual string Name => "";

            public abstract int Size { get; }


            protected const string DllString = "dll:";
            protected const string ClassString = "class:";
            protected const string ComString = "intf:.";

            internal static Base Load(BinaryReader br)
            {
                var fdeclLen = br.Read<uint>();
                using (var fdeclMem = new NativeMemoryArray<byte>(fdeclLen, true))
                {
                    var fdeclSpan = fdeclMem.AsSpan();
                    br.Read(fdeclSpan);
                    using (var brDecl = new BinaryReader(fdeclMem.AsStream(), Encoding.UTF8, true))
                    {
                        if (fdeclSpan.EqualsAsciiString(0, DllString))
                        {
                            brDecl.BaseStream.Position = DllString.Length;
                            return DLL.Load(brDecl);
                        }
                        else if (fdeclSpan.EqualsAsciiString(0, ClassString))
                        {
                            brDecl.BaseStream.Position = ClassString.Length;
                            return Class.Load(brDecl);
                        }
                        else if (fdeclSpan.EqualsAsciiString(0, ComString))
                        {
                            brDecl.BaseStream.Position = ComString.Length;
                            return COM.Load(brDecl);
                        }
                        else
                        {
                            return Internal.Load(brDecl);
                        }
                    }
                }
            }

            internal void Save(BinaryWriter bw, Script.SaveContext ctx)
            {
                using (var ms = new MemoryStream())
                {
                    using (var msbw = new BinaryWriter(ms, Encoding.UTF8, true))
                    {
                        SaveCore(msbw, ctx);
                        if (ms.Length > uint.MaxValue) throw new InvalidOperationException("Declaration length is greater than 4GB");
                        bw.Write<uint>((uint)ms.Length);
                        bw.Write(ms);
                    }
                }
            }

            internal abstract void SaveCore(BinaryWriter bw, Script.SaveContext ctx);

            protected void LoadArguments(BinaryReader br)
            {
                HasReturnArgument = br.ReadByte() != 0;
                Arguments = FunctionArgument.LoadForExternal(br);
            }

            protected void SaveArguments(BinaryWriter bw)
            {
                bw.Write<bool>(HasReturnArgument);
                foreach (var arg in Arguments)
                {
                    bw.Write<bool>(arg.ArgumentType == FunctionArgumentType.Out);
                }
            }

            protected int SizeOfArguments => sizeof(byte) + (sizeof(byte) * Arguments.Count);
        }

        public abstract class BaseCC : Base
        {
            public NativeCallingConvention CallingConvention;

            public override string ToString()
            {
                switch (CallingConvention)
                {
                    case NativeCallingConvention.Register:
                        return "__fastcall";
                    case NativeCallingConvention.Pascal:
                        return "__pascal";
                    case NativeCallingConvention.CDecl:
                        return "__cdecl";
                    case NativeCallingConvention.Stdcall:
                        return "__stdcall";
                    default:
                        return "";
                }
            }
        }

        public sealed class DLL : BaseCC
        {
            public string DllName;
            public string ProcedureName;
            public bool DelayLoad;
            public bool LoadWithAlteredSearchPath;

            internal override string Name => string.Format("{0}!{1}", DllName, ProcedureName);

            internal static new DLL Load(BinaryReader br)
            {
                var ret = new DLL();
                ret.DllName = br.ReadAsciiStringTerminated();
                ret.ProcedureName = br.ReadAsciiStringTerminated();
                ret.CallingConvention = (NativeCallingConvention)br.ReadByte();
                ret.DelayLoad = br.ReadByte() != 0;
                ret.LoadWithAlteredSearchPath = br.ReadByte() != 0;

                ret.LoadArguments(br);

                return ret;
            }

            public override string ToString()
            {
                return string.Format(
                    "dll(\"{0}\",\"{1}\"{2}{3}) {4}",
                    DllName.Replace("\"", "\\\""),
                    ProcedureName.Replace("\"", "\\\""),
                    DelayLoad ? "delayload, " : "",
                    LoadWithAlteredSearchPath ? "alteredsearchpath" : "",
                    base.ToString()
                );
            }

            public override int Size =>
                DllString.Length +
                Encoding.ASCII.GetByteCount(DllName) + sizeof(byte) +
                Encoding.ASCII.GetByteCount(ProcedureName) + sizeof(byte) +
                sizeof(NativeCallingConvention) +
                sizeof(byte) + sizeof(byte) +
                SizeOfArguments;

            internal override void SaveCore(BinaryWriter bw, Script.SaveContext ctx)
            {
                bw.WriteAsciiString(DllString);
                bw.WriteAsciiStringTerminated(DllName);
                bw.WriteAsciiStringTerminated(ProcedureName);
                bw.Write(CallingConvention);
                bw.Write<byte>((byte)(DelayLoad ? 1 : 0));
                bw.Write<byte>((byte)(LoadWithAlteredSearchPath ? 1 : 0));
                SaveArguments(bw);
            }
        }

        public sealed class Class : BaseCC
        {
            public string ClassName;
            public string FunctionName;
            public bool IsProperty = false;

            internal override string Name => string.Format("{0}->{1}", ClassName, FunctionName);


            private const byte TERMINATOR = (byte)'|';

            internal static new Class Load(BinaryReader br)
            {
                var ret = new Class();

                // check for a special type
                if ((br.BaseStream.Length - br.BaseStream.Position) == 1)
                {
                    ret.ClassName = "Class";
                    ret.HasReturnArgument = true;
                    ret.CallingConvention = NativeCallingConvention.Pascal;

                    var special = br.ReadByte();
                    switch (special)
                    {
                        case (byte)'+':
                            ret.FunctionName = "CastToType";
                            ret.Arguments = new List<FunctionArgument>()
                            {
                                new FunctionArgument() {
                                    Type = null,
                                    ArgumentType = FunctionArgumentType.Out
                                },
                                new FunctionArgument()
                                {
                                    Type = null,
                                    ArgumentType = FunctionArgumentType.Out
                                }
                            };
                            break;
                        case (byte)'-':
                            ret.FunctionName = "SetNil";
                            ret.Arguments = new List<FunctionArgument>()
                            {
                                new FunctionArgument() {
                                    Type = null,
                                    ArgumentType = FunctionArgumentType.Out
                                },
                            };
                            break;
                        default:
                            throw new InvalidDataException(string.Format("Unknown special type: 0x{0:x2}", special));
                    }
                    return ret;
                }

                ret.ClassName = br.ReadAsciiStringTerminated(TERMINATOR);
                ret.FunctionName = br.ReadAsciiStringTerminated(TERMINATOR);
                ret.IsProperty = ret.FunctionName[ret.FunctionName.Length - 1] == '@';
                if (ret.IsProperty)
                {
                    ret.FunctionName = ret.FunctionName.Substring(0, ret.FunctionName.Length - 1);
                }
                ret.CallingConvention = (NativeCallingConvention)br.ReadByte();
                ret.LoadArguments(br);
                return ret;
            }

            public override string ToString()
            {
                return string.Format(
                    "class({0}, {1}{2}) {3}",
                    ClassName,
                    FunctionName,
                    IsProperty ? ", property" : "",
                    base.ToString()
                );
            }

            private int SizeBody()
            {
                if (ClassName == "Class" && (FunctionName == "CastToType" || FunctionName == "SetNil")) return 1;

                return Encoding.ASCII.GetByteCount(ClassName) + sizeof(byte) +
                    Encoding.ASCII.GetByteCount(FunctionName) + sizeof(byte) +
                    (IsProperty ? sizeof(byte) : 0) +
                    sizeof(NativeCallingConvention) +
                    SizeOfArguments;
            }

            public override int Size =>
                ClassString.Length + SizeBody();

            internal override void SaveCore(BinaryWriter bw, Script.SaveContext ctx)
            {
                bw.WriteAsciiString(ClassString);

                if (ClassName == "Class")
                {
                    if (FunctionName == "CastToType")
                    {
                        bw.Write<byte>((byte)'+');
                        return;
                    }
                    else if (FunctionName == "SetNil")
                    {
                        bw.Write<byte>((byte)'-');
                        return;
                    }
                }

                bw.WriteAsciiStringTerminated(ClassName, TERMINATOR);
                var sb = new StringBuilder(FunctionName);
                if (IsProperty) sb.Append('@');
                bw.WriteAsciiStringTerminated(sb.ToString(), TERMINATOR);
                bw.Write(CallingConvention);
                SaveArguments(bw);
            }

        }

        public sealed class COM : BaseCC
        {
            public uint VTableIndex;

            internal override string Name => string.Format("CoInterface->vtbl[{0}]", VTableIndex);

            internal static new COM Load(BinaryReader br)
            {
                var ret = new COM();
                ret.VTableIndex = br.Read<uint>();
                ret.CallingConvention = (NativeCallingConvention)br.ReadByte();
                ret.LoadArguments(br);
                return ret;
            }

            public override string ToString()
            {
                return string.Format("com({0}) {1}", VTableIndex, base.ToString());
            }

            public override int Size =>
                ComString.Length + sizeof(uint) + sizeof(NativeCallingConvention) + SizeOfArguments;

            internal override void SaveCore(BinaryWriter bw, Script.SaveContext ctx)
            {
                bw.WriteAsciiString(ComString);
                bw.Write<uint>(VTableIndex);
                bw.Write(CallingConvention);
                SaveArguments(bw);
            }
        }

        public sealed class Internal : Base
        {
            internal static new Internal Load(BinaryReader br)
            {
                var ret = new Internal();
                ret.LoadArguments(br);
                return ret;
            }

            public override string ToString()
            {
                return "internal";
            }

            public override int Size => SizeOfArguments;

            internal override void SaveCore(BinaryWriter bw, Script.SaveContext ctx)
            {
                SaveArguments(bw);
            }
        }
    }

    /// <summary>
    /// A native function that imports from a DLL or an internal implementation
    /// </summary>
    public class ExternalFunction : FunctionBase
    {
        public FDecl.Base Declaration = null;

        private bool ExportsName => string.IsNullOrEmpty(Declaration?.Name);

        public override IType ReturnArgument { get; set; }

        /// <summary>
        /// If null, argument information is unknown.
        /// </summary>
        public override IList<FunctionArgument> Arguments {
            get => Declaration?.Arguments;
            set
            {
                if (Declaration == null) throw new NullReferenceException("No declaration is present");
                Declaration.Arguments = value;
            }
        }

        internal static ExternalFunction Load(BinaryReader br, Script script, bool exported)
        {
            var ret = new ExternalFunction();
            ret.Exported = exported;
            var namelen = br.ReadByte();
            if (namelen != 0)
                ret.Name = br.ReadAsciiString(namelen);
            if (exported)
            {
                ret.Declaration = FDecl.Base.Load(br);
                if (ret.Declaration.HasReturnArgument) ret.ReturnArgument = UnknownType.Instance;
                if (string.IsNullOrEmpty(ret.Name)) ret.Name = ret.Declaration.Name;
            }
            return ret;
        }

        public override string ToString()
        {
            var sb = new StringBuilder(".function");
            if (Exported) sb.Append("(import)");
            sb.Append(" external ");
            if (Declaration != null)
                sb.Append(Declaration);
            sb.Append(' ');
            sb.Append(ReturnArgument != null ? "returnsval " : "void ");
            sb.Append(Name);
            if (Declaration != null)
            {
                sb.Append('(');
                for (int i = 0; i < Arguments.Count; i++)
                {
                    sb.Append(i == 0 ? "" : ",");
                    sb.Append(Arguments[i].ToString());
                }
                sb.Append(')');
            }
            return sb.ToString();
        }

        private byte NameLength() => (byte)Math.Min(0xff, Encoding.ASCII.GetByteCount(Name));

        public override int Size => sizeof(byte) + NameLength() + (Declaration == null ? 0 : Declaration.Size);

        internal override void Save(BinaryWriter bw, Script.SaveContext ctx)
        {
            FunctionFlags flags = FunctionFlags.External;

            if (Exported)
            {
                flags |= FunctionFlags.Exported;
                Declaration.HasReturnArgument = ReturnArgument != null;
            }

            if (Attributes.Count != 0) flags |= FunctionFlags.HasAttributes;

            bw.Write(flags);
            if (ExportsName)
            {
                bw.Write<byte>(NameLength());
                var internalName = Name.ToUpper();
                if (Name.Length > 0xff) internalName = Name.Substring(0, 0xff);
                bw.WriteAsciiString(internalName);
            }
            else bw.Write<byte>(0);

            if (Declaration != null)
            {
                Declaration.Save(bw, ctx);
            }
        }
    }
}
