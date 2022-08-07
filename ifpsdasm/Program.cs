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
            foreach (var arg in args)
            {
                var ext = Path.GetExtension(arg);
                var dis = arg.Substring(0, arg.Length - ext.Length) + ".txt";
                using (var stream = File.OpenRead(arg))
                    File.WriteAllText(dis, Script.Load(stream).Disassemble());
            }
        }
    }
}
