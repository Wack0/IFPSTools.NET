using System;
using System.Collections.Generic;
using System.Linq;
using CodeGeneration;
using IFPSLib.Emit;

namespace ABT {
    public abstract partial class Stmt {
        public abstract void CGenStmt(Env env, CGenState state);

        public void Pop(CGenState state)
        {
            state.CGenPopStackSize();
        }

        public Operand CGenExprStmt(Env env, Expr expr, CGenState state, bool cleanUp = false) {
            state.CGenPushStackSize();
            var ret = expr.CGenValue(state, null);
            if (cleanUp) Pop(state);
            return ret;
        }
    }

    public sealed partial class GotoStmt {
        public override void CGenStmt(Env env, CGenState state) {
            state.JumpTo(state.GotoLabel(this.Label), false);
        }
    }

    public sealed partial class LabeledStmt {
        public override void CGenStmt(Env env, CGenState state) {
            var label = state.GotoLabel(this.Label);
            state.CGenLabel(label);
            this.Stmt.CGenStmt(env, state);
        }
    }

    public sealed partial class ContStmt {
        public override void CGenStmt(Env env, CGenState state) {
            state.JumpTo(state.ContinueLabel, false);
        }
    }

    public sealed partial class BreakStmt {
        public override void CGenStmt(Env env, CGenState state) {
            state.JumpTo(state.BreakLabel, state.IsInSwitch);
        }
    }

    public sealed partial class ExprStmt {
        public override void CGenStmt(Env env, CGenState state) {
            if (this.ExprOpt.IsSome) {
                Int32 stack_size = state.StackSize;
                this.ExprOpt.Value.CGenValue(state, null);
                state.CGenForceStackSizeTo(stack_size);
            }
        }
    }

    public sealed partial class CompoundStmt {
        public override void CGenStmt(Env env, CGenState state) {
            state.CGenPushStackSize();
            foreach (Tuple<Env, Decln> decln in this.Declns) {
                decln.Item2.CGenDecln(decln.Item1, state);
            }
            foreach (Tuple<Env, Stmt> stmt in this.Stmts) {
                stmt.Item2.CGenStmt(stmt.Item1, state);
            }
            state.CGenPopStackSize();
        }
    }

    public sealed partial class ReturnStmt {
        public override void CGenStmt(Env env, CGenState state) {
            // remove all pop instructions leading up to here.
            // about to return, runtime cleans up the stack itself on ret
            while (state.CurrInsns.Count > 0 && state.CurrInsns.Last().OpCode.Code == Code.Pop) state.CurrInsns.RemoveAt(state.CurrInsns.Count - 1);

            if (this.ExprOpt.IsSome) {
                var retval = state.FunctionState.Function.CreateReturnVariable();
                // Is this function supposed to return a pointer?
                var ptr = ExprOpt.Value.Type as PointerType;
                var needsPtrCast = ptr != null && ptr.IsRef;
                // Evaluate the Value.
                var retOp = Operand.Create(retval);
                var val = ExprOpt.Value.CGenValue(state, needsPtrCast ? null : retOp);
                // Remove all ending pops to save space. ret instruction cleans up stack itself
                while (state.CurrInsns.LastOrDefault()?.OpCode.Code == Code.Pop) state.CurrInsns.RemoveAt(state.CurrInsns.Count - 1);
                // Is this function supposed to return a pointer?
                if (needsPtrCast)
                {
                    // Ptr-to-ptr cast into retval.
                    // Don't bother cleaning up the stack at all, we are about to return.
                    var dummyForType = state.FunctionState.PushType(state.EmitType(ptr.RefType));
                    var dummyU32 = state.FunctionState.PushType(state.TypeU32);
                    state.FunctionState.PushVar(val);
                    state.FunctionState.PushVar(dummyU32);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastPointerRef));
                    state.FunctionState.PushVar(Operand.Create(retval));
                    state.FunctionState.Push(dummyU32);
                    state.FunctionState.PushVar(dummyForType);
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Call, state.CastRefPointer));
                    state.CurrInsns.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }
                if (val != retOp) state.CurrInsns.Add(Instruction.Create(OpCodes.Assign, retOp, val));
            }
            // Return, runtime will fix up stack itself.
            state.CurrInsns.Add(Instruction.Create(OpCodes.Ret));
        }
    }

    public sealed partial class WhileStmt {
        public override void CGenStmt(Env env, CGenState state) {
            Int32 start_label = state.RequestLabel();
            Int32 finish_label = state.RequestLabel();

            // start:
            state.CGenLabel(start_label);

            // jz finish, Cond

            var ret = CGenExprStmt(env, this.Cond, state);

            state.CurrInsns.Add(Instruction.Create(OpCodes.SetFlagZ, ret));
            Pop(state);
            state.CurrInsns.Add(Instruction.Create(OpCodes.JumpF, state.FunctionState.Labels[finish_label]));

            // Body
            state.InLoop(start_label, finish_label);
            this.Body.CGenStmt(env, state);
            state.OutLabels();

            // jmp start
            state.CurrInsns.Add(Instruction.Create(OpCodes.Jump, state.FunctionState.Labels[start_label]));

            // finish:
            state.CGenLabel(finish_label);

        }
    }

    public sealed partial class DoWhileStmt {
        public override void CGenStmt(Env env, CGenState state) {
            Int32 start_label = state.RequestLabel();
            Int32 finish_label = state.RequestLabel();
            Int32 continue_label = state.RequestLabel();

            // start:
            state.CGenLabel(start_label);

            // Body
            state.InLoop(continue_label, finish_label);
            this.Body.CGenStmt(env, state);
            state.OutLabels();

            state.CGenLabel(continue_label);

            // test Cond
            var ret = CGenExprStmt(env, this.Cond, state);
            state.CurrInsns.Add(Instruction.Create(OpCodes.SetFlagNZ, ret));
            Pop(state);
            state.CurrInsns.Add(Instruction.Create(OpCodes.JumpF, state.FunctionState.Labels[start_label]));

            state.CGenLabel(finish_label);
        }
    }

    public sealed partial class ForStmt {
        public override void CGenStmt(Env env, CGenState state) {
            // Init
            this.Init.Map(_ => CGenExprStmt(env, _, state, true));

            Int32 start_label = state.RequestLabel();
            Int32 finish_label = state.RequestLabel();
            Int32 continue_label = state.RequestLabel();

            // start:
            state.CGenLabel(start_label);

            // test cond
            this.Cond.Map(_ => {
                var ret = CGenExprStmt(env, _, state);
                state.CurrInsns.Add(Instruction.Create(OpCodes.SetFlagZ, ret));
                Pop(state);
                state.CurrInsns.Add(Instruction.Create(OpCodes.JumpF, state.FunctionState.Labels[finish_label]));
                return ret;
            });

            // Body
            state.InLoop(continue_label, finish_label);
            this.Body.CGenStmt(env, state);
            state.OutLabels();

            // continue:
            state.CGenLabel(continue_label);

            // Loop
            this.Loop.Map(_ => CGenExprStmt(env, _, state, true));

            // jmp start
            state.CurrInsns.Add(Instruction.Create(OpCodes.Jump, state.FunctionState.Labels[start_label]));

            // finish:
            state.CGenLabel(finish_label);
        }
    }

    public sealed partial class SwitchStmt {
        public override void CGenStmt(Env env, CGenState state) {

            // Inside a switch statement, the initializations are ignored,
            // but the stack size should be changed.
            List<Tuple<Env, Decln>> declns;
            List<Tuple<Env, Stmt>> stmts;

            var compoundStmt = this.Stmt as CompoundStmt;
            if (compoundStmt == null) {
                throw new NotImplementedException().Attach(Expr);
            }

            declns = compoundStmt.Declns;
            stmts = compoundStmt.Stmts;

            // Track all case values.
            IReadOnlyList<Int32> values = CaseLabelsGrabber.GrabLabels(this);

            // Make sure there are no duplicates.
            if (values.Distinct().Count() != values.Count) {
                throw new InvalidOperationException("case labels not unique.").Attach(Expr);
            }
            // Request labels for these values.
            Dictionary<Int32, Int32> value_to_label = values.ToDictionary(value => value, value => state.RequestLabel());

            Int32 label_finish = state.RequestLabel();

            Int32 num_default_stmts = stmts.Count(_ => _.Item2 is DefaultStmt);
            if (num_default_stmts > 1) {
                throw new InvalidOperationException("duplicate defaults.").Attach(Expr);
            }
            Int32 label_default =
                num_default_stmts == 1 ?
                state.RequestLabel() :
                label_finish;

            //Int32 saved_stack_size = state.StackSize;

            // 1. Evaluate Expr.
            var op = CGenExprStmt(env, this.Expr, state);

            // 2. Expand stack.
            //state.CGenForceStackSizeTo(stack_size);

            // 3. Make the Jump list.
            var cmp = state.FunctionState.PushType(state.TypeU32);
            foreach (KeyValuePair<Int32, Int32> value_label_pair in value_to_label)
            {
                state.CurrInsns.Add(Instruction.Create(OpCodes.Eq, cmp, op, Operand.Create(value_label_pair.Key)));
                state.CurrInsns.Add(Instruction.Create(OpCodes.JumpNZ, state.FunctionState.Labels[value_label_pair.Value], cmp));
            }
            state.CurrInsns.Add(Instruction.Create(OpCodes.Jump, state.FunctionState.Labels[label_default]));

            // 4. List all the statements.
            state.InSwitch(label_finish, label_default, value_to_label);
            foreach (Tuple<Env, Stmt> env_stmt_pair in stmts) {
                env_stmt_pair.Item2.CGenStmt(env_stmt_pair.Item1, state);
            }
            state.OutLabels();

            // 5. finish:
            state.CGenLabel(label_finish);

            // 6. Restore stack size.
            Pop(state);
        }
    }

    public sealed partial class CaseStmt {
        public override void CGenStmt(Env env, CGenState state) {
            Int32 label = state.CaseLabel(this.Value);
            state.CGenLabel(label);
            this.Stmt.CGenStmt(env, state);
        }
    }

    public sealed partial class DefaultStmt {
        public override void CGenStmt(Env env, CGenState state) {
            Int32 label = state.DefaultLabel;
            state.CGenLabel(label);
            this.Stmt.CGenStmt(env, state);
        }
    }

    public sealed partial class IfStmt {
        public override void CGenStmt(Env env, CGenState state) {
            var ret = CGenExprStmt(env, this.Cond, state);
            state.CurrInsns.Add(Instruction.Create(OpCodes.SetFlagZ, ret));
            Pop(state);

            Int32 finish_label = state.RequestLabel();

            state.CurrInsns.Add(Instruction.Create(OpCodes.JumpF, state.FunctionState.Labels[finish_label]));

            this.Stmt.CGenStmt(env, state);

            state.CGenLabel(finish_label);
        }
    }

    public sealed partial class IfElseStmt {
        public override void CGenStmt(Env env, CGenState state) {
            var ret = CGenExprStmt(env, this.Cond, state);
            state.CurrInsns.Add(Instruction.Create(OpCodes.SetFlagZ, ret));
            Pop(state);

            Int32 false_label = state.RequestLabel();
            Int32 finish_label = state.RequestLabel();

            state.CurrInsns.Add(Instruction.Create(OpCodes.JumpF, state.FunctionState.Labels[false_label]));

            this.TrueStmt.CGenStmt(env, state);

            state.CurrInsns.Add(Instruction.Create(OpCodes.Jump, state.FunctionState.Labels[finish_label]));

            state.CGenLabel(false_label);

            this.FalseStmt.CGenStmt(env, state);

            state.CGenLabel(finish_label);
        }
    }
}
