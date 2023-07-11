﻿using System;
using System.Text;

namespace LexicalAnalysis {
    /// <summary>
    /// String literal
    /// </summary>
    public sealed class TokenString : Token {
        public TokenString(String val, String raw) {
            if (val == null) {
                throw new ArgumentNullException(nameof(val));
            }
            this.Val = val;
            this.Raw = raw;
        }

        public override TokenKind Kind { get; } = TokenKind.STRING;
        public String Raw { get; }
        public String Val { get; }

        public override String ToString() =>
            $"{this.Kind} [{Line}:{Column}]: \"{this.Raw}\"\n\"{this.Val}\"";
    }


    public sealed class TokenUnicodeString : Token
    {
        public TokenUnicodeString(String val, String raw)
        {
            if (val == null)
            {
                throw new ArgumentNullException(nameof(val));
            }
            this.Val = val;
            this.Raw = raw;
        }

        public override TokenKind Kind { get; } = TokenKind.STRING;
        public String Raw { get; }
        public String Val { get; }

        public override String ToString() =>
            $"{this.Kind} [{Line}:{Column}]: L\"{this.Raw}\"\n\"{this.Val}\"";
    }

    public sealed class FSAString : FSA {
        private enum State {
            START,
            END,
            ERROR,
            L,
            Q,
            QQ
        };

        private State _state;
        private readonly FSAChar _fsachar;
        private StringBuilder _val;
        private StringBuilder _raw;
        private bool unicode = false;

        public FSAString() {
            this._state = State.START;
            this._fsachar = new FSAChar('\"');
            this._raw = new StringBuilder();
            this._val = new StringBuilder();
        }

        public override void Reset() {
            this._state = State.START;
            this._fsachar.Reset();
            this._raw.Clear();
            this._val.Clear();
            unicode = false;
        }

        public override FSAStatus GetStatus() {
            if (this._state == State.START) {
                return FSAStatus.NONE;
            }
            if (this._state == State.END) {
                return FSAStatus.END;
            }
            if (this._state == State.ERROR) {
                return FSAStatus.ERROR;
            }
            return FSAStatus.RUNNING;
        }

        public override Token RetrieveToken() {
            if (unicode) return new TokenUnicodeString(this._val.ToString(), this._raw.ToString());
            return new TokenString(this._val.ToString(), this._raw.ToString());
        }

        public override void ReadChar(Char ch) {
            switch (this._state) {
                case State.END:
                case State.ERROR:
                    this._state = State.ERROR;
                    break;
                case State.START:
                    switch (ch) {
                        case 'L':
                            unicode = true;
                            this._state = State.L;
                            break;
                        case '\"':
                            this._state = State.Q;
                            this._fsachar.Reset();
                            break;
                        default:
                            this._state = State.ERROR;
                            break;
                    }
                    break;
                case State.L:
                    if (ch == '\"') {
                        this._state = State.Q;
                        this._fsachar.Reset();
                    } else {
                        this._state = State.ERROR;
                    }
                    break;
                case State.Q:
                    if (this._fsachar.GetStatus() == FSAStatus.NONE && ch == '\"') {
                        this._state = State.QQ;
                        this._fsachar.Reset();
                    } else {
                        this._fsachar.ReadChar(ch);
                        switch (this._fsachar.GetStatus()) {
                            case FSAStatus.END:
                                this._state = State.Q;
                                this._val.Append(this._fsachar.RetrieveChar());
                                this._raw.Append(this._fsachar.RetrieveRaw());
                                this._fsachar.Reset();
                                ReadChar(ch);
                                break;
                            case FSAStatus.ERROR:
                                this._state = State.ERROR;
                                break;
                            default:
                                break;
                        }
                    }
                    break;
                case State.QQ:
                    this._state = State.END;
                    break;
                default:
                    this._state = State.ERROR;
                    break;
            }
        }

        public override void ReadEOF() {
            if (this._state == State.QQ) {
                this._state = State.END;
            } else {
                this._state = State.ERROR;
            }
        }

    }
}