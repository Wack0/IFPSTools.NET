﻿using System;

namespace LexicalAnalysis {
    public enum FSAStatus {
        NONE,
        END,
        RUNNING,
        ERROR
    }

    public abstract class FSA {
        public abstract FSAStatus GetStatus();
        public abstract void ReadChar(Char ch);
        public abstract void Reset();
        public abstract void ReadEOF();
        public abstract Token RetrieveToken();
    }
}