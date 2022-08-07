using IFPSLib.Types;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Cysharp.Collections;
using IFPSLib.Emit;
using System.Threading;

namespace IFPSLib
{
    public sealed class Script
    {

        private const int VERSION_LOWEST = 12;
        public const int VERSION_HIGHEST = 23;

        internal const int VERSION_MIN_ATTRIBUTES = 21;
        internal const int VERSION_MAX_SETSTACKTYPE = 22; // Is this correct?
        internal const int VERSION_MIN_STATICARRAYSTART = 23;

        /// <summary>
        /// Internal version of this file.
        /// </summary>
        public int FileVersion;

        /// <summary>
        /// Entry point. Can be null.
        /// </summary>
        public IFunction EntryPoint = null;

        /// <summary>
        /// List of types declared in this script by internal index.
        /// </summary>
        public IList<IType> Types = new List<IType>();
        /// <summary>
        /// List of functions declared in this script by internal index.
        /// </summary>
        public IList<IFunction> Functions = new List<IFunction>();
        /// <summary>
        /// List of global variables declared in this script by internal index.
        /// </summary>
        public IList<GlobalVariable> GlobalVariables = new List<GlobalVariable>();

        private struct ScriptHeaderAfterMagic
        {
            // Fields get set directly by memcpy
#pragma warning disable CS0649
            internal int
                Version, NumTypes, NumFuncs, NumVars, IdxEntryPoint, ImportSize;
#pragma warning restore CS0649
        }

        public Script(int version)
        {
            FileVersion = version;
        }

        public Script() : this(VERSION_HIGHEST) { }

        private static Script LoadCore(Stream stream, bool leaveOpen = false)
        {
            using (var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen))
            {
                return LoadCore(br);
            }
        }

        private static Script LoadCore(BinaryReader br)
        {
            {
                Span<byte> magic = stackalloc byte[4];
                br.Read(magic);
                if (magic[0] != 'I' || magic[1] != 'F' || magic[2] != 'P' || magic[3] != 'S') throw new InvalidDataException();
            }

            var header = br.Read<ScriptHeaderAfterMagic>();
            if (header.Version < VERSION_LOWEST || header.Version > VERSION_HIGHEST) throw new InvalidDataException(string.Format("Invalid version: {0}", header.Version));
            var ret = new Script(header.Version);

            ret.Types = new List<IType>(header.NumTypes);
            var typesToNames = new Dictionary<string, IType>();
            var samename = new Dictionary<string, int>();
            for (int i = 0; i < header.NumTypes; i++)
            {
                var type = TypeBase.Load(br, ret);
                if (string.IsNullOrEmpty(type.Name)) type.Name = string.Format("Type{0}", i);
                if (typesToNames.ContainsKey(type.Name))
                {
                    int count = 0;
                    if (samename.TryGetValue(type.Name, out count)) count++;
                    else count = 1;
                    samename[type.Name] = count;
                    type.Name += "_" + (count + 1);
                }

                typesToNames.Add(type.Name, type);
                ret.Types.Add(type);
            }

            ret.Functions = new List<IFunction>(header.NumFuncs);
            samename = new Dictionary<string, int>();
            var funcsToNames = new Dictionary<string, IFunction>();
            for (int i = 0; i < header.NumFuncs; i++)
            {
                var func = FunctionBase.Load(br, ret);
                if (funcsToNames.ContainsKey(func.Name))
                {
                    int count = 0;
                    if (samename.TryGetValue(func.Name, out count)) count++;
                    else count = 1;
                    samename[func.Name] = count;
                    func.Name += "_" + (count + 1);
                }

                funcsToNames.Add(func.Name, func);
                ret.Functions.Add(func);
            }

            ret.GlobalVariables = new List<GlobalVariable>(header.NumVars);
            for (int i = 0; i < header.NumVars; i++)
            {
                ret.GlobalVariables.Add(GlobalVariable.Load(br, ret, i));
            }

            foreach (var func in ret.Functions.OfType<ScriptFunction>())
            {
                func.LoadInstructions(br, ret);
            }

            if (header.IdxEntryPoint >= 0 && header.IdxEntryPoint < header.NumFuncs)
                ret.EntryPoint = ret.Functions[header.IdxEntryPoint];

            return ret;
        }

        public static Script Load(byte[] bytes) => LoadCore(new MemoryStream(bytes, 0, bytes.Length, false, true));

        public static Script Load(MemoryStream stream) => LoadCore(stream, true);
        public static Script Load(UnmanagedMemoryStream stream) => LoadCore(stream);

        public static Script Load(Stream stream) => LoadAsync(stream).GetAwaiter().GetResult();

        public static async Task<Script> LoadAsync(Stream stream)
        {
            // use a NativeMemoryArray to allow for full 64-bit length
            using (var buffer = new NativeMemoryArray<byte>(stream.Length, true))
            {
                var ums = buffer.AsStream(FileAccess.ReadWrite);
                await stream.CopyToAsync(ums);
                ums.Position = 0;
                return LoadCore(ums);
            }
        }

        internal class SaveContext
        {
            internal readonly Dictionary<IType, int> tblTypes;
            internal readonly Dictionary<IFunction, int> tblFunctions;
            internal readonly Dictionary<GlobalVariable, int> tblGlobals;
            internal readonly Dictionary<IFunction, long> tblFunctionOffsets;
            internal readonly int FileVersion;

            internal SaveContext(Script script)
            {
                tblTypes = new Dictionary<IType, int>();
                tblFunctions = new Dictionary<IFunction, int>();
                tblGlobals = new Dictionary<GlobalVariable, int>();
                tblFunctionOffsets = new Dictionary<IFunction, long>();

                FileVersion = script.FileVersion;

                for (int i = 0; i < script.Types.Count; i++) tblTypes.Add(script.Types[i], i);
                for (int i = 0; i < script.Functions.Count; i++) tblFunctions.Add(script.Functions[i], i);
                for (int i = 0; i < script.GlobalVariables.Count; i++) tblGlobals.Add(script.GlobalVariables[i], i);
            }

            internal int GetTypeIndex(IType type)
            {
                if (type == null)
                {
                    throw new ArgumentOutOfRangeException(string.Format("Used an unknown type"));
                }
                if (!tblTypes.TryGetValue(type, out var idx))
                {
                    throw new KeyNotFoundException(string.Format("Used unreferenced type {0}, make sure it's added to the Types list.", type.Name));
                }
                return idx;
            }

            internal int GetFunctionIndex(IFunction function)
            {
                if (function == null)
                {
                    throw new ArgumentOutOfRangeException(string.Format("Used an unknown function"));
                }
                if (!tblFunctions.TryGetValue(function, out var idx))
                {
                    throw new KeyNotFoundException(string.Format("Used unreferenced function {0}, make sure it's added to the Functions list.", function.Name));
                }
                return idx;
            }

            internal long GetFunctionOffset(IFunction function)
            {
                if (function == null)
                {
                    throw new ArgumentOutOfRangeException(string.Format("Used an unknown function"));
                }
                if (!tblFunctionOffsets.TryGetValue(function, out var idx))
                {
                    throw new KeyNotFoundException(string.Format("Used unreferenced function {0}, make sure it's added to the Functions list.", function.Name));
                }
                return idx + sizeof(FunctionFlags);
            }
        }

        private void SaveCore(BinaryWriter bw)
        {
            // Set up save context.
            var ctx = new SaveContext(this);

            bw.Write((byte)'I');
            bw.Write((byte)'F');
            bw.Write((byte)'P');
            bw.Write((byte)'S');

            {
                var header = default(ScriptHeaderAfterMagic);
                header.Version = FileVersion;
                header.NumTypes = Types.Count;
                header.NumFuncs = Functions.Count;
                header.NumVars = GlobalVariables.Count;
                var idxEntryPoint = -1;
                if (EntryPoint != null) idxEntryPoint = ctx.GetFunctionIndex(EntryPoint);
                header.IdxEntryPoint = idxEntryPoint;
                header.ImportSize = 0;
                bw.Write(header);
            }

            // Write types.
            foreach (var type in Types.OfType<TypeBase>()) type.Save(bw, ctx);

            // Write functions.
            foreach (var func in Functions.OfType<FunctionBase>())
            {
                ctx.tblFunctionOffsets.Add(func, bw.BaseStream.Position);
                func.Save(bw, ctx);
                if (func.Attributes.Count != 0) CustomAttribute.SaveList(bw, func.Attributes, ctx);
            }

            // Write global variables.
            foreach (var global in GlobalVariables)
            {
                global.SaveHeader(bw, ctx);
            }

            // Write function bodies.
            foreach (var func in Functions.OfType<ScriptFunction>())
            {
                func.SaveInstructions(bw, ctx);
            }
        }

        private void SaveCore(Stream stream, bool leaveOpen = false)
        {
            using (var bw = new BinaryWriter(stream, Encoding.UTF8, leaveOpen))
            {
                SaveCore(bw);
            }
        }

        public byte[] Save()
        {
            using (var ms = new MemoryStream())
            {
                Save(ms);
                return ms.ToArray();
            }
        }

        public void Save(MemoryStream stream) => SaveCore(stream, true);

        public void Save(Stream stream) => SaveAsync(stream).GetAwaiter().GetResult();

        public async Task SaveAsync(Stream stream)
        {
            using (var ms = new MemoryStream())
            {
                // ensure stream can be written to before saving to memory
                ms.CopyTo(stream);
                Save(ms);
                ms.Position = 0;
                await ms.CopyToAsync(stream);
            }
        }


        /// <summary>
        /// Disassembles the script.
        /// </summary>
        /// <returns>Disassembly string</returns>
        public string Disassemble()
        {
            var sb = new StringBuilder();

            sb.AppendFormat(".version {0}", FileVersion).AppendLine().AppendLine();
            if (EntryPoint != null) sb.AppendFormat(".entry {0}", EntryPoint.Name).AppendLine().AppendLine();

            foreach (var type in Types)
            {
                foreach (var attr in type.Attributes)
                {
                    sb.AppendLine(attr.ToString());
                }
                sb.AppendLine(type.ToString());
            }
            if (Types.Any()) sb.AppendLine();

            foreach (var global in GlobalVariables)
            {
                sb.AppendLine(global.ToString());
            }
            if (GlobalVariables.Any()) sb.AppendLine();

            foreach (var func in Functions)
            {
                foreach (var attr in func.Attributes)
                {
                    sb.AppendLine(attr.ToString());
                }
                sb.AppendLine(func.ToString());
                var sf = func as ScriptFunction;
                if (sf != null)
                {
                    var stackcount = 0;
                    bool changed = true;
                    foreach (var insn in sf.Instructions)
                    {
                        sb.Append(insn.ToString(true));
                        changed = true;
                        switch (insn.OpCode.StackBehaviourPush)
                        {
                            case StackBehaviour.Push1:
                                stackcount++;
                                break;
                            default:
                                changed = false;
                                break;
                        }
                        if (!changed)
                        {
                            changed = true;
                            switch (insn.OpCode.StackBehaviourPop)
                            {
                                case StackBehaviour.Pop1:
                                    stackcount--;
                                    break;
                                case StackBehaviour.Pop2:
                                    stackcount -= 2;
                                    break;
                                default:
                                    changed = false;
                                    break;
                            }
                        }

                        if (changed) sb.AppendFormat(" ; StackCount = {0}", stackcount);
                        sb.AppendLine();
                    }
                }
                sb.AppendLine();
            }
            sb.AppendLine();

            return sb.ToString();
        }
    }
}
