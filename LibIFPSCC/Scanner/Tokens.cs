using System;

namespace LexicalAnalysis {
    public enum TokenKind {
        NONE,
        FLOAT,
        INT,
        CHAR,
        STRING,
        IDENTIFIER,
        KEYWORD,
        OPERATOR
    }

    public abstract class Token {
        public override String ToString() {
            return this.Kind.ToString();
        }
        public abstract TokenKind Kind { get; }

        public int Line { get; internal set; }
        public int Column { get; internal set; }
    }

    public sealed class EmptyToken : Token {
        public override TokenKind Kind { get; } = TokenKind.NONE;
    }

    public sealed class FSAComment : FSA
    {
        private enum State
        {
            START,
            END,
            ERROR,
            FIRST,
            COMMENT,
            END_FIRST,
            END_SECOND,
        }

        private State _state;
        private bool CStyle = false;

        public FSAComment()
        {
            this._state = State.START;
        }

        public override void Reset()
        {
            this._state = State.START;
            CStyle = false;
        }

        public override FSAStatus GetStatus()
        {
            if (this._state == State.START)
            {
                return FSAStatus.NONE;
            }
            if (this._state == State.END)
            {
                return FSAStatus.END;
            }
            if (this._state == State.ERROR)
            {
                return FSAStatus.ERROR;
            }
            return FSAStatus.RUNNING;
        }

        // RetrieveToken : () -> Token
        // ===========================
        // Note that this function never gets used, because FSAChar is just an inner FSA for other FSAs.
        // 
        public override Token RetrieveToken()
        {
            return new EmptyToken();
        }

        // ReadChar : Char -> ()
        // =====================
        // Implementation of the FSA
        // 
        public override void ReadChar(Char ch)
        {
            switch (this._state)
            {
                case State.END:
                case State.ERROR:
                    this._state = State.ERROR;
                    break;
                case State.START:
                    if (ch == '/')
                    {
                        this._state = State.FIRST;
                    }
                    else
                    {
                        this._state = State.ERROR;
                    }
                    break;
                case State.FIRST:
                    CStyle = ch == '*';
                    if (CStyle || ch == '/')
                    {
                        _state = State.COMMENT;
                    }
                    else _state = State.ERROR;
                    break;
                case State.COMMENT:
                    if (CStyle && ch == '*')
                    {
                        _state = State.END_FIRST;
                    }
                    else if (!CStyle && ch == '\n') _state = State.END_SECOND;
                    break;
                case State.END_FIRST:
                    if (ch == '/') _state = State.END_SECOND;
                    else if (ch != '*') _state = State.COMMENT;
                    break;
                case State.END_SECOND:
                    _state = State.END;
                    break;
                default:
                    this._state = State.ERROR;
                    break;
            }
        }

        // ReadEOF : () -> ()
        // ==================
        // 
        public override void ReadEOF()
        {
            switch (this._state)
            {
                case State.COMMENT:
                case State.END_FIRST:
                    this._state = State.END;
                    break;
                default:
                    this._state = State.ERROR;
                    break;
            }
        }

    }

    public sealed class FSASpace : FSA {
        private enum State {
            START,
            END,
            ERROR,
            SPACE
        };

        private State _state;

        public FSASpace() {
            this._state = State.START;
        }

        public override void Reset() {
            this._state = State.START;
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
            return new EmptyToken();
        }

        public override void ReadChar(Char ch) {
            switch (this._state) {
                case State.END:
                case State.ERROR:
                    this._state = State.ERROR;
                    break;
                case State.START:
                    if (Utils.IsSpace(ch)) {
                        this._state = State.SPACE;
                    } else {
                        this._state = State.ERROR;
                    }
                    break;
                case State.SPACE:
                    if (Utils.IsSpace(ch)) {
                        this._state = State.SPACE;
                    } else {
                        this._state = State.END;
                    }
                    break;
            }
        }

        public override void ReadEOF() {
            switch (this._state) {
                case State.SPACE:
                    this._state = State.END;
                    break;
                default:
                    this._state = State.ERROR;
                    break;
            }
        }
    }

    public sealed class FSANewLine : FSA {
        private enum State {
            START,
            END,
            ERROR,
            NEWLINE
        };

        private State _state;

        public FSANewLine() {
            this._state = State.START;
        }

        public override void Reset() {
            this._state = State.START;
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
            return new EmptyToken();
        }

        public override void ReadChar(Char ch) {
            switch (this._state) {
                case State.END:
                case State.ERROR:
                    this._state = State.ERROR;
                    break;
                case State.START:
                    if (ch == '\n') {
                        this._state = State.NEWLINE;
                    } else {
                        this._state = State.ERROR;
                    }
                    break;
                case State.NEWLINE:
                    this._state = State.END;
                    break;
            }
        }

        public override void ReadEOF() {
            switch (this._state) {
                case State.NEWLINE:
                    this._state = State.END;
                    break;
                default:
                    this._state = State.ERROR;
                    break;
            }
        }
    }
}