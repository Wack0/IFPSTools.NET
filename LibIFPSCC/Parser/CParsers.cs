using System;
using System.Collections.Generic;
using System.Linq;
using LexicalAnalysis;
using AST;
using static Parsing.ParserCombinator;
using System.Collections.Immutable;

namespace Parsing {
    public partial class CParsers
    {
        static CParsers()
        {
            SetExpressionRules();
            SetDeclarationRules();
            SetExternalDefinitionRules();
            SetStatementRules();
        }

        public static IParserResult<TranslnUnit> Parse(IEnumerable<Token> tokens) =>
            TranslationUnit.Parse(new ParserInput(new ParserEnvironment(), tokens));

        public class ConstCharParser : IParser<Expr>
        {
            public RuleCombining Combining => RuleCombining.NONE;
            public IParserResult<Expr> Parse(ParserInput input)
            {
                var token = input.Source.First() as TokenCharConst;
                if (token == null)
                {
                    return new ParserFailed<Expr>(input);
                }
                return ParserSucceeded.Create(new IntLiteral(token.Value, TokenInt.IntSuffix.NONE), input.Environment, input.Source.Skip(1));
            }
        }

        public class ConstIntParser : IParser<Expr>
        {
            public RuleCombining Combining => RuleCombining.NONE;
            public IParserResult<Expr> Parse(ParserInput input)
            {
                var token = input.Source.First() as TokenInt;
                if (token == null)
                {
                    return new ParserFailed<Expr>(input);
                }
                return ParserSucceeded.Create(new IntLiteral(token.Val, token.Suffix), input.Environment, input.Source.Skip(1));
            }
        }

        public class ConstFloatParser : IParser<Expr>
        {
            public RuleCombining Combining => RuleCombining.NONE;
            public IParserResult<Expr> Parse(ParserInput input)
            {
                var token = input.Source.First() as TokenFloat;
                if (token == null)
                {
                    return new ParserFailed<Expr>(input);
                }
                return ParserSucceeded.Create(new FloatLiteral(token.Value, token.Suffix), input.Environment, input.Source.Skip(1));
            }
        }

        public class StringLiteralParser : IParser<Expr>
        {
            public RuleCombining Combining => RuleCombining.NONE;
            public IParserResult<Expr> Parse(ParserInput input)
            {
                var token = input.Source.First() as TokenString;
                if (token == null)
                {
                    return new ParserFailed<Expr>(input);
                }
                return ParserSucceeded.Create(new StringLiteral(token.Val), input.Environment, input.Source.Skip(1));
            }
        }

        public class UnicodeStringLiteralParser : IParser<Expr>
        {
            public RuleCombining Combining => RuleCombining.NONE;
            public IParserResult<Expr> Parse(ParserInput input)
            {
                var token = input.Source.First() as TokenUnicodeString;
                if (token == null)
                {
                    return new ParserFailed<Expr>(input);
                }
                return ParserSucceeded.Create(new UnicodeStringLiteral(token.Val), input.Environment, input.Source.Skip(1));
            }
        }

        public class BinaryOperatorBuilder
        {
            public BinaryOperatorBuilder(IConsumer operatorConsumer, Func<Expr, Expr, Expr> nodeCreator)
            {
                this.OperatorConsumer = operatorConsumer;
                this.NodeCreator = nodeCreator;
            }

            public static BinaryOperatorBuilder Create(IConsumer operatorConsumer, Func<Expr, Expr, Expr> nodeCreator) =>
                new BinaryOperatorBuilder(operatorConsumer, nodeCreator);

            public IConsumer OperatorConsumer { get; }
            public Func<Expr, Expr, Expr> NodeCreator { get; }
        }

        public class OperatorParser : IParser<Expr>
        {
            private IParser<Expr> lhsParser;
            private IParser<Expr> rhsParser;
            private readonly ImmutableList<BinaryOperatorBuilder> builders;
            private readonly bool needsOne;

            public OperatorParser(IParser<Expr> operandParser, IEnumerable<BinaryOperatorBuilder> builders) : this(operandParser, operandParser, builders)
            {
                needsOne = false;
            }

            public OperatorParser(IParser<Expr> lhsParser, IParser<Expr> rhsParser, IEnumerable<BinaryOperatorBuilder> builders)
            {
                this.lhsParser = lhsParser;
                this.rhsParser = rhsParser;
                this.builders = builders.ToImmutableList();
                needsOne = true;
            }

            public RuleCombining Combining => RuleCombining.THEN;

            public IParserResult<Expr> Parse(ParserInput input)
            {
                var firstResult = lhsParser.Parse(input);
                if (!firstResult.IsSuccessful)
                {
                    return new ParserFailed<Expr>(firstResult);
                }

                return Transform(firstResult.Result, firstResult.ToInput());
            }

            private IParserResult<Expr> TransformImpl(Expr seed, ParserInput input)
            {
                List<IParserFailed> failed = new List<IParserFailed>();
                foreach (var builder in builders) {
                    var given = ParserSucceeded.Create(seed, input.Environment, input.Source);
                    var result1 = builder.OperatorConsumer.Consume(given.ToInput());
                    if (!result1.IsSuccessful)
                    {
                        failed.Add(new ParserFailed<Expr>(result1));
                        continue;
                    }
                    var result2 = rhsParser.Parse(result1.ToInput());
                    if (!result2.IsSuccessful)
                    {
                        failed.Add(new ParserFailed<Expr>(result2));
                        continue;
                    }

                    var transform = builder.NodeCreator(seed, result2.Result);
                    var ret = ParserSucceeded.Create(transform, result2.Environment, result2.Source);
                    var expr = transform as IStoredLineInfo;
                    if (expr != null)
                    {
                        expr.Copy(ret);
                    }
                    return ret;
                }
                return new ParserFailed<Expr>(input, failed);
            }

            public IParserResult<Expr> Transform(Expr seed, ParserInput input)
            {
                IParserResult<Expr> curResult = needsOne ? TransformImpl(seed, input) : ParserSucceeded.Create(seed, input.Environment, input.Source);

                if (!curResult.IsSuccessful) return new ParserFailed<Expr>(curResult);

                IParserResult<Expr> lastSuccessfulResult;
                do
                {
                    lastSuccessfulResult = curResult;
                    curResult = TransformImpl(lastSuccessfulResult.Result, lastSuccessfulResult.ToInput());
                } while (curResult.IsSuccessful);

                return lastSuccessfulResult;
            }
        }

        public static IParser<Expr> BinaryOperator(IParser<Expr> operandParser, params BinaryOperatorBuilder[] builders)
            => new OperatorParser(operandParser, builders);

        public static IParser<Expr> AssignmentOperator(
            IParser<Expr> lhsParser,
            IParser<Expr> rhsParser,
            params BinaryOperatorBuilder[] builders
        ) => new OperatorParser(lhsParser, rhsParser, builders);
    }
}