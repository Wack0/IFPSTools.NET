using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Driver;

namespace ifpscc
{
    internal class Program
    {
        private static readonly char[] OPTION_SEP = { '=' };

        private static void Usage()
        {
            Console.WriteLine("Usage: {0} [-A|--disassemble] [-O=out.bin|--output=out.bin] files...", Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location));
            Console.WriteLine();
            Console.WriteLine("C compiler targeting RemObjects PascalScript.");
            Console.WriteLine("if -A or -disassemble is passed, writes the disassembly of the compiler output to stdout");
            Console.WriteLine("if -O or -output is not passed, the compiled script will be written to [first C file passed].bin");
        }

        static void Main(string[] args)
        {
            var files = new List<string>();
            var options = new Dictionary<string, string>();

            // Parse command line for options and files.
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i][0] == '-')
                {
                    var option = args[i].Split(OPTION_SEP, 2);
                    if (option.Length == 1) options.Add(option[0], string.Empty);
                    else options.Add(option[0], option[1]);
                }
                else
                {
                    files.Add(args[i]);
                }
            }

            if (options.ContainsKey("-?") || options.ContainsKey("--help"))
            {
                Usage();
                return;
            }

            if (files.Count == 0)
            {
                Console.WriteLine("At least one C file must be passed in");
                Usage();
                return;
            }

            var code = string.Empty;

            foreach (var file in files)
            {
                string fileText = string.Empty;
                try
                {
                    fileText = File.ReadAllText(file) + "\r\n";
                } catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Usage();
                    return;
                }

                // TODO: preprocessor?
                code += fileText;
            }

#if DEBUG // TODO: remove this later
            while (!System.Diagnostics.Debugger.IsAttached) System.Threading.Thread.Sleep(100);
#endif
            var script = Compiler.FromSource(code).Script;

            if (options.ContainsKey("--disassemble") || options.ContainsKey("-A"))
            {
                Console.WriteLine(script.Disassemble());
                return;
            }

            var output = string.Empty;

            if (options.ContainsKey("-o")) output = options["-o"];
            if (output == string.Empty && options.ContainsKey("--output")) output = options["--output"];
            if (output == string.Empty) output = Path.ChangeExtension(files[0], "bin");

            script.Save(File.OpenWrite(output));
        }
    }
}
