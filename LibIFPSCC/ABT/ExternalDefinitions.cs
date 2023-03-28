using System;
using System.Collections.Generic;
using System.Linq;
using CodeGeneration;

namespace ABT {
    public class TranslnUnit {
        public TranslnUnit(List<Tuple<Env, IExternDecln>> _declns) {
            this.declns = _declns;
        }
        public readonly List<Tuple<Env, IExternDecln>> declns;

        public void CodeGenerate(CGenState state) {
            foreach (Tuple<Env, IExternDecln> decln in this.declns) {
                decln.Item2.CGenDecln(decln.Item1, state);
            }

        }
    }

    public interface IExternDecln : IStoredLineInfo {
        void CGenDecln(Env env, CGenState state);
    }

    public class FuncDef : IExternDecln {
        public int Line { get; private set; }
        public int Column { get; private set; }

        public void Copy(ILineInfo info)
        {
            Line = info.Line;
            Column = info.Column;
        }
        public FuncDef(String name, StorageClass scs, FunctionType type, Stmt stmt) {
            this.name = name;
            this.scs  = scs;
            this.type = type;
            this.stmt = stmt;
        }

        public override String ToString() => $"fn {this.name}: {this.type}";

        public void CGenDecln(Env env, CGenState state) {

            Env.Entry entry = env.Find(this.name).Value;
            switch (entry.Kind) {
            case Env.EntryKind.GLOBAL:
                switch (scs) {
                case StorageClass.AUTO:
                case StorageClass.EXTERN:
                case StorageClass.STATIC:
                    break;
                default:
                    throw new InvalidOperationException().Attach(this);
                }
                break;
            default:
                throw new InvalidOperationException().Attach(this);
            }
            state.CGenFuncStart(this.name, type, scs, this);

            state.InFunction(GotoLabelsGrabber.GrabLabels(this.stmt));

            this.stmt.CGenStmt(env, state);

            //state.CGenLabel(state.ReturnLabel);

            if (state.CurrInsns.LastOrDefault()?.OpCode.Code != IFPSLib.Emit.Code.Ret)
            {
                // remove any pop instructions before ret to save space, ret cleans up stack itself
                while (state.CurrInsns.LastOrDefault()?.OpCode.Code == IFPSLib.Emit.Code.Pop) state.CurrInsns.RemoveAt(state.CurrInsns.Count - 1);
                // if last insn is nop, replace it with ret, otherwise, append
                var ret = IFPSLib.Emit.Instruction.Create(IFPSLib.Emit.OpCodes.Ret);
                var last = state.CurrInsns.LastOrDefault();
                if (last?.OpCode.Code == IFPSLib.Emit.Code.Nop) last.Replace(ret);
                else state.CurrInsns.Add(ret);
            }

            state.OutFunction();
            
        }

        public readonly String      name;
        public readonly StorageClass   scs;
        public readonly FunctionType   type;
        public readonly Stmt        stmt;
    }
}