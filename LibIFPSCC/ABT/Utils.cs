using System;
using System.Collections.Generic;

namespace ABT {
    public class Utils {

        // class StoreEntry
        // ================
        // the inner storage of entries
        // 
        public class StoreEntry {
            public StoreEntry(String name, ExprType type, Int32 offset, bool arg = false) {
                this.name = name;
                this.type = type;
                this.offset = offset;
                isArg = arg;
            }
            public readonly String name;
            public readonly ExprType type;
            public readonly Int32 offset;
            public readonly bool isArg;
        }

        public static Int32 RoundUp(Int32 value, Int32 alignment) {
            return (value + alignment - 1) & ~(alignment- 1);
        }

        public static Tuple<Int32, IReadOnlyList<Int32>> PackArguments(IReadOnlyList<ExprType> types) {
            List<Int32> offsets = new List<Int32>();
            Int32 offset = 0;
            foreach (ExprType type in types) {
                offsets.Add(offset);
                offset++;
            }
            return new Tuple<Int32, IReadOnlyList<Int32>>(offset, offsets);
        }

    }
}
