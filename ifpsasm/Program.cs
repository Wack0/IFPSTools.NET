using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ifpsasm
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: {0} <script.asm>", Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location));
                Console.WriteLine();
                Console.WriteLine("RemObjects PascalScript assembler.");
                Console.WriteLine("Writes the output to the *.bin file in the same directory as the passed in assembly file.");
                return;
            }
            var arg = args[0];
            var ext = Path.GetExtension(arg);
            var bin = arg.Substring(0, arg.Length - ext.Length) + ".bin";
            var script = IFPSAsmLib.Assembler.Assemble(File.ReadAllText(args[0]));
            script.Save(File.OpenWrite(bin));
            Console.WriteLine("Assembled script to {0}", bin);
        }
    }
}
