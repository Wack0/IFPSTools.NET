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
            var arg = args[0];
            var ext = Path.GetExtension(arg);
            var bin = arg.Substring(0, arg.Length - ext.Length) + ".bin";
            var script = IFPSAsmLib.Assembler.Assemble(File.ReadAllText(args[0]));
            script.Save(File.OpenWrite(bin));
        }
    }
}
