using System;

namespace LexicalAnalysis {
    /// <summary>
    /// There are four types of integers: signed, unsigned, signed long, unsigned long
    /// </summary>
    public sealed class TokenInt : Token {
        public enum IntSuffix {
            NONE,
            U,
            L,
            LL,
            UL,
            ULL
        };

        public TokenInt(Int64 val, IntSuffix suffix, String raw) {
            this.Val = val;
            this.Suffix = suffix;
            this.Raw = raw;
        }

        public override TokenKind Kind { get; } = TokenKind.INT;

        public override String ToString() {
            String str = this.Kind.ToString();
            switch (this.Suffix) {
                case IntSuffix.L:
                    str += "(long)";
                    break;
                case IntSuffix.LL:
                    str += "(__int64)";
                    break;
                case IntSuffix.U:
                    str += "(unsigned)";
                    break;
                case IntSuffix.UL:
                    str += "(unsigned long)";
                    break;
                case IntSuffix.ULL:
                    str += "(unsigned __int64)";
                    break;
                default:
                    break;
            }
            return str + $" [{Line}:{Column}]: " + this.Val + " \"" + this.Raw + "\"";
        }

        public readonly Int64 Val;
        public readonly String Raw;
        public readonly IntSuffix Suffix;
    }

    public sealed class FSAInt : FSA {
        private enum State {
            START,
            END,
            ERROR,
            Z,
            O,
            D,
            ZX,
            H,
            L,
            LL,
            U,
            UL,
            ULL
        };

        private Int64 _val;
        private String _raw;
        private TokenInt.IntSuffix _suffix;
        private State _state;

        public FSAInt() {
            this._state = State.START;
            this._val = 0;
            this._raw = "";
            this._suffix = TokenInt.IntSuffix.NONE;
        }

        public override void Reset() {
            this._state = State.START;
            this._val = 0;
            this._raw = "";
            this._suffix = TokenInt.IntSuffix.NONE;
        }

        public override FSAStatus GetStatus() {
            switch (this._state) {
                case State.START:
                    return FSAStatus.NONE;
                case State.END:
                    return FSAStatus.END;
                case State.ERROR:
                    return FSAStatus.ERROR;
                default:
                    return FSAStatus.RUNNING;
            }
        }

        public override Token RetrieveToken() {
            return new TokenInt(this._val, this._suffix, this._raw.Substring(0, this._raw.Length - 1));
        }

        public override void ReadChar(Char ch) {
            this._raw += ch;
            switch (this._state) {
                case State.ERROR:
                case State.END:
                    this._state = State.ERROR;
                    break;
                case State.START:
                    if (ch == '0') {
                        this._state = State.Z;
                    } else if (Char.IsDigit(ch)) {
                        this._state = State.D;
                        this._val += ch - '0';
                    } else {
                        this._state = State.ERROR;
                    }
                    break;
                case State.Z:
                    if (ch == 'x' || ch == 'X') {
                        this._state = State.ZX;
                    } else if (Utils.IsOctDigit(ch)) {
                        this._val *= 8;
                        this._val += ch - '0';
                        this._state = State.O;
                    } else if (ch == 'u' || ch == 'U') {
                        this._suffix = TokenInt.IntSuffix.U;
                        this._state = State.U;
                    } else if (ch == 'l' || ch == 'L') {
                        this._suffix = TokenInt.IntSuffix.L;
                        this._state = State.L;
                    } else {
                        this._state = State.END;
                    }
                    break;
                case State.D:
                    if (Char.IsDigit(ch)) {
                        this._val *= 10;
                        this._val += ch - '0';
                        this._state = State.D;
                    } else if (ch == 'u' || ch == 'U') {
                        this._suffix = TokenInt.IntSuffix.U;
                        this._state = State.U;
                    } else if (ch == 'l' || ch == 'L') {
                        this._suffix = TokenInt.IntSuffix.L;
                        this._state = State.L;
                    } else {
                        this._state = State.END;
                    }
                    break;
                case State.ZX:
                    if (Utils.IsHexDigit(ch)) {
                        this._val *= 0x10;
                        this._val += Utils.GetHexDigit(ch);
                        this._state = State.H;
                    } else {
                        this._state = State.ERROR;
                    }
                    break;
                case State.O:
                    if (Utils.IsOctDigit(ch)) {
                        this._val *= 8;
                        this._val += ch - '0';
                        this._state = State.O;
                    } else if (ch == 'u' || ch == 'U') {
                        this._suffix = TokenInt.IntSuffix.U;
                        this._state = State.U;
                    } else if (ch == 'l' || ch == 'L') {
                        this._suffix = TokenInt.IntSuffix.L;
                        this._state = State.L;
                    } else {
                        this._state = State.END;
                    }
                    break;
                case State.L:
                    if (ch == 'u' || ch == 'U')
                    {
                        this._suffix = TokenInt.IntSuffix.UL;
                        this._state = State.UL;
                    } else if (ch == 'l' || ch == 'L')
                    {
                        this._suffix = TokenInt.IntSuffix.LL;
                        this._state = State.LL;
                    } else {
                        this._state = State.END;
                    }
                    break;
                case State.H:
                    if (Utils.IsHexDigit(ch)) {
                        this._val *= 0x10;
                        this._val += Utils.GetHexDigit(ch);
                        this._state = State.H;
                    } else if (ch == 'u' || ch == 'U') {
                        this._suffix = TokenInt.IntSuffix.U;
                        this._state = State.U;
                    } else if (ch == 'l' || ch == 'L') {
                        this._suffix = TokenInt.IntSuffix.L;
                        this._state = State.L;
                    } else {
                        this._state = State.END;
                    }
                    break;
                case State.U:
                    if (ch == 'l' || ch == 'L') {
                        this._suffix = TokenInt.IntSuffix.UL;
                        this._state = State.UL;
                    } else {
                        this._state = State.END;
                    }
                    break;
                case State.LL:
                    if (ch == 'u' || ch == 'U')
                    {
                        this._suffix = TokenInt.IntSuffix.ULL;
                        this._state = State.ULL;
                    }
                    else
                    {
                        this._state = State.END;
                    }
                    break;
                case State.UL:
                    if (ch == 'l' || ch == 'L')
                    {
                        this._suffix = TokenInt.IntSuffix.ULL;
                        this._state = State.ULL;
                    }
                    else
                    {
                        this._state = State.END;
                    }
                    break;
                case State.ULL:
                    this._state = State.END;
                    break;
                default:
                    this._state = State.ERROR;
                    break;
            }
        }

        public override void ReadEOF() {
            switch (this._state) {
                case State.D:
                case State.Z:
                case State.O:
                case State.L:
                case State.H:
                case State.U:
                case State.UL:
                    this._state = State.END;
                    break;
                default:
                    this._state = State.ERROR;
                    break;
            }
        }

    }
}