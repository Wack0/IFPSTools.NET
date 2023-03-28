﻿using System;
using System.Collections.Immutable;
using System.Linq;
using ABT;
using static AST.SemanticAnalysis;

namespace AST {
    public interface ISyntaxTreeNode : IStoredLineInfo { }

    /// <summary>
    /// A translation unit consists of a list of external declarations - functions and objects.
    /// </summary>
    public sealed class TranslnUnit : ISyntaxTreeNode {
        public int Line { get; private set; }
        public int Column { get; private set; }

        public void Copy(ILineInfo info)
        {
            Line = info.Line;
            Column = info.Column;
        }
        private TranslnUnit(ImmutableList<IExternDecln> declns) {
            this.Declns = declns;
        }

        public static TranslnUnit Create(ImmutableList<IExternDecln> externDeclns) =>
            new TranslnUnit(externDeclns);

        [SemantMethod]
        public ISemantReturn<ABT.TranslnUnit> GetTranslnUnit() {
            var env = new Env();
            var externDeclns = this.Declns.Aggregate(ImmutableList<Tuple<Env, ABT.IExternDecln>>.Empty, (acc, externDecln) => acc.AddRange(Semant(externDecln.GetExternDecln, ref env))
            );
            return SemantReturn.Create(env, new ABT.TranslnUnit(externDeclns.ToList()));
        }

        public ImmutableList<IExternDecln> Declns { get; }
    }


    public interface IExternDecln : ISyntaxTreeNode {
        [SemantMethod]
        ISemantReturn<ImmutableList<Tuple<Env, ABT.IExternDecln>>> GetExternDecln(Env env);
    }

    /// <summary>
    /// A function definition gives the implementation.
    /// </summary>
    public sealed class FuncDef : IExternDecln {
        public int Line { get; private set; }
        public int Column { get; private set; }

        public void Copy(ILineInfo info)
        {
            Line = info.Line;
            Column = info.Column;
        }
        public FuncDef(DeclnSpecs specs, Declr declr, CompoundStmt stmt) {
            this.Specs = specs;
            this.Declr = declr;
            this.Stmt = stmt;
        }

        public static FuncDef Create(Option<DeclnSpecs> declnSpecs, Declr declr, Stmt body) =>
            new FuncDef(declnSpecs.IsSome ? declnSpecs.Value : DeclnSpecs.Empty, declr, body as CompoundStmt);

        public DeclnSpecs Specs { get; }
        public Declr Declr { get; }
        public CompoundStmt Stmt { get; }
        
        [SemantMethod]
        public ISemantReturn<ImmutableList<Tuple<Env, ABT.IExternDecln>>> GetExternDecln(Env env) {
            var storageClass = this.Specs.GetStorageClass();
            var baseType = Semant(this.Specs.GetExprType, ref env);
            var name = this.Declr.Name;
            var type = Semant(this.Declr.DecorateType, baseType, ref env);

            var funcType = type as FunctionType;
            if (funcType == null) {
                throw new InvalidOperationException("Expected a function Type.").Attach(this);
            }

            switch (storageClass) {
                case StorageClass.AUTO:
                case StorageClass.EXTERN:
                case StorageClass.STATIC:
                    env = env.PushEntry(Env.EntryKind.GLOBAL, name, type);
                    break;
                case StorageClass.TYPEDEF:
                default:
                    throw new InvalidOperationException("Invalid storage class specifier for function definition.").Attach(this);
            }

            env = env.InScope();
            env = env.SetCurrentFunction(funcType);
            var stmt = SemantStmt(this.Stmt.GetStmt, ref env);
            env = env.OutScope();

            ABT.IExternDecln funcDef = new ABT.FuncDef(name, storageClass, funcType, stmt);
            funcDef.Copy(this);

            return SemantReturn.Create(env, ImmutableList.Create(Tuple.Create(env, funcDef)));
        }
    }

}