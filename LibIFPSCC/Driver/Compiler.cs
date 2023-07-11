using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using CodeGeneration;
using LexicalAnalysis;
using Parsing;

namespace Driver {
    public class Compiler {
        private static readonly bool SHOW_TIME = false;
        private static System.Diagnostics.Stopwatch StartTimer()
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            return watch;
        }
        private Compiler(String source) {
            this.Source = source;

            // Lexical analysis
            var watch = StartTimer();
            Scanner scanner = new Scanner(source);
            this.Tokens = scanner.Tokens.ToImmutableList();
            watch.Stop();
            if (SHOW_TIME) Console.WriteLine("lexer: {0} ms", watch.ElapsedMilliseconds);

            // Parse
            watch = StartTimer();
            var parserResult = CParsers.Parse(this.Tokens);
            watch.Stop();
            if (SHOW_TIME) Console.WriteLine("parser: {0} ms", watch.ElapsedMilliseconds);
            if (!parserResult.IsSuccessful || parserResult.Source.Count() != 1) {
                throw new InvalidOperationException($"Parsing error:\n{parserResult}");
            }
            this.SyntaxTree = parserResult.Result;

            // Semantic analysis
            watch = StartTimer();
            var semantReturn = this.SyntaxTree.GetTranslnUnit();
            watch.Stop();
            if (SHOW_TIME) Console.WriteLine("semant: {0} ms", watch.ElapsedMilliseconds);
            this.AbstractSyntaxTree = semantReturn.Value;
            this.Environment = semantReturn.Env;

            // Code generation
            watch = StartTimer();
            var state = new CGenState();
            this.AbstractSyntaxTree.CodeGenerate(state);
            state.EmitCallsToCtor();
            this.Script = state.Script;
            watch.Stop();
            if (SHOW_TIME) Console.WriteLine("codegen: {0} ms", watch.ElapsedMilliseconds);
        }

        public static Compiler FromSource(String src) {
            return new Compiler(src);
        }

        public static Compiler FromFile(String fileName) {
            if (File.Exists(fileName)) {
                return new Compiler(File.ReadAllText(fileName));
            }
            throw new FileNotFoundException($"{fileName} does not exist!");
        }

        public readonly String Source;
        public readonly ImmutableList<Token> Tokens;
        public readonly AST.TranslnUnit SyntaxTree;
        public readonly ABT.TranslnUnit AbstractSyntaxTree;
        public readonly ABT.Env Environment;
        public readonly IFPSLib.Script Script;
    }
}