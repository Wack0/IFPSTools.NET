using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace LexicalAnalysis {
    public sealed class Scanner {
        public Scanner(String source) {
            this.Source = source;
            this.FSAs = ImmutableList.Create<FSA>(
                new FSAComment(),
                new FSAFloat(),
                new FSAInt(),
                new FSAOperator(),
                new FSAIdentifier(),
                new FSASpace(),
                new FSANewLine(),
                new FSACharConst(),
                new FSAString()
                );
            this.Tokens = Lex();
        }

        public static Scanner FromFile(String fileName) {
            if (File.Exists(fileName)) {
                String source = File.ReadAllText(fileName);
                return FromSource(source);
            }
            throw new FileNotFoundException("Source file does not exist.", fileName);
        }

        public static Scanner FromSource(String source) {
            return new Scanner(source);
        }

        private IEnumerable<Token> Lex() {
            var tokens = new List<Token>();
            int line = 1, column = 1, lastColumn = column;
            char lastChr = '\0';
            for (Int32 i = 0; i < this.Source.Length; ++i) {
                if (lastChr == '\n')
                {
                    line++;
                    lastColumn = 1;
                    column = 1;
                }
                else column++;
                bool isRunning = false;
                int endIdx = -1;
                var chr = Source[i];
                for (int fsaIdx = 0; fsaIdx < FSAs.Count; fsaIdx++)
                {
                    var fsa = FSAs[fsaIdx];
                    fsa.ReadChar(chr);
                    var status = fsa.GetStatus();
                    if (status == FSAStatus.RUNNING) isRunning = true;
                    else if (endIdx == -1 && status == FSAStatus.END) endIdx = fsaIdx;
                }

                // if no running
                if (!isRunning) {
                    if (endIdx != -1) {
                        Token token = this.FSAs[endIdx].RetrieveToken();
                        if (token.Kind != TokenKind.NONE) {
                            token.Line = line;
                            token.Column = lastColumn;
                            lastColumn = column;
                            tokens.Add(token);
                        }
                        i--; column--;
                        if (lastChr == '\n') line--;
                        foreach (var fsa in FSAs) fsa.Reset();
                    } else {
                        Console.WriteLine("error");
                    }
                }
                if (!isRunning || endIdx == -1) lastChr = chr;
            }

            var endIdx2 = -1;
            for (int fsaIdx = 0; fsaIdx < FSAs.Count; fsaIdx++)
            {
                var fsa = FSAs[fsaIdx];
                fsa.ReadEOF();
                if (endIdx2 != -1) continue;
                if (fsa.GetStatus() == FSAStatus.END) endIdx2 = fsaIdx;
            }
            // find END
            if (endIdx2 != -1) {
                Token token = this.FSAs[endIdx2].RetrieveToken();
                if (token.Kind != TokenKind.NONE) {
                    token.Line = line;
                    token.Column = column + 1;
                    tokens.Add(token);
                }
            } else {
                Console.WriteLine("error");
            }

            tokens.Add(new EmptyToken());
            return tokens;
        }

        public override String ToString() {
            String str = "";
            foreach (Token token in this.Tokens) {
                str += $"{token}\n";
            }
            return str;
        }

        public String Source { get; }
        private ImmutableList<FSA> FSAs { get; }
        public IEnumerable<Token> Tokens { get; }

    }
}