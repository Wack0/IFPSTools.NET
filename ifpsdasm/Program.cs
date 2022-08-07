using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using IFPSLib;
using IFPSLib.Emit;

namespace ifpsdasm
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: {0} <CompiledCode.bin>", Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location));
                Console.WriteLine();
                Console.WriteLine("RemObjects PascalScript disassembler.");
                Console.WriteLine("Writes the output to the *.txt file in the same directory as the passed in script bytecode.");
                return;
            }
            var arg = args[0];
            var ext = Path.GetExtension(arg);
            var dis = arg.Substring(0, arg.Length - ext.Length) + ".txt";
            using (var stream = File.OpenRead(arg))
                File.WriteAllText(dis, Script.Load(stream).Disassemble());
            Console.WriteLine("Disassembled script to {0}", dis);
        }
    }
}
