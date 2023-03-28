﻿using System;
using System.Collections.Immutable;

namespace Parsing {
    public static class ParserCombinator {

        /// <summary>
        /// Parser then Parser = Parser
        /// ( => R1 ) then ( => R2 ) is ( => Tuple[R2, R1] )
        /// </summary>
        public static IParser<Tuple<R2, R1>> Then<R1, R2>(this IParser<R1> firstParser, IParser<R2> secondParser) =>
            new ParserThenParser<R1, R2>(firstParser, secondParser);

        /// <summary>
        /// Parser then Consumer = Parser
        /// ( => R ) then ( => ) is ( => R )
        /// </summary>
        public static IParser<R> Then<R>(this IParser<R> parser, IConsumer consumer) =>
            new ParserThenConsumer<R>(parser, consumer);

        /// <summary>
        /// Parser then Transformer = Parser
        /// ( => R1 ) then ( R1 => R2 ) is ( => R2 )
        /// </summary>
        public static IParser<R2> Then<R1, R2>(this IParser<R1> parser, ITransformer<R1, R2> transformer) =>
            new ParserThenTransformer<R1, R2>(parser, transformer);

        public static IParser<R> Then<S, R>(this IParser<S> parser, Func<S, R> transformer) =>
            parser.Then(new SimpleTransformer<S, R>(transformer));

        public static IParser<R> Then<I1, I2, R>(this IParser<Tuple<I2, I1>> parser, Func<I1, I2, R> transformer) =>
            parser.Then(_ => transformer(_.Item2, _.Item1));

        public static IParser<R> Then<I1, I2, I3, R>(this IParser<Tuple<I3, Tuple<I2, I1>>> parser, Func<I1, I2, I3, R> transformer) =>
            parser.Then(_ => transformer(_.Item2.Item2, _.Item2.Item1, _.Item1));

        public static IParser<R> Then<I1, I2, I3, I4, R>(this IParser<Tuple<I4, Tuple<I3, Tuple<I2, I1>>>> parser, Func<I1, I2, I3, I4, R> transformer) =>
            parser.Then(_ => transformer(_.Item2.Item2.Item2, _.Item2.Item2.Item1, _.Item2.Item1, _.Item1));

        /// <summary>
        /// Consumer then Parser = Parser
        /// ( => ) then ( => R ) is ( => R )
        /// </summary>
        public static IParser<R> Then<R>(this IConsumer consumer, IParser<R> parser) =>
            new ConsumerThenParser<R>(consumer, parser);

        /// <summary>
        /// Consumer then Consumer = Consumer
        /// ( => ) then ( => ) is ( => )
        /// </summary>
        public static IConsumer Then(this IConsumer firstConsumer, IConsumer secondConsumer) =>
            new ConsumerThenConsumer(firstConsumer, secondConsumer);

        /// <summary>
        /// Transformer then Parser = Transformer
        /// ( S => R1 ) then ( => R2 ) is ( S => Tuple[R2, R1] )
        /// </summary>
        public static ITransformer<S, Tuple<R2, R1>> Then<S, R1, R2>(this ITransformer<S, R1> transformer, IParser<R2> parser) =>
            new TransformerThenParser<S, R1, R2>(transformer, parser);

        /// <summary>
        /// Transformer then Consumer = Transformer
        /// ( S => R ) then ( => ) is ( S => R )
        /// </summary>
        public static ITransformer<S, R> Then<S, R>(this ITransformer<S, R> transformer, IConsumer consumer) =>
            new TransformerThenConsumer<S, R>(transformer, consumer);

        /// <summary>
        /// Transformer then Transformer = Transformer
        /// ( S => I ) then ( I => R ) is ( S => R )
        /// </summary>
        public static ITransformer<S, R> Then<S, I, R>(this ITransformer<S, I> firstTransformer, ITransformer<I, R> secondTransformer) =>
            new TransformerThenTransformer<S, I, R>(firstTransformer, secondTransformer);

        public static ITransformer<S, R> Then<S, I, R>(this ITransformer<S, I> firstTransformer, Func<I, R> secondTransformer) =>
            firstTransformer.Then(new SimpleTransformer<I, R>(secondTransformer));

        public static ITransformer<S, R> Then<S, I1, I2, R>(this ITransformer<S, Tuple<I2, I1>> firstTransformer, Func<I1, I2, R> secondTransformer) =>
            firstTransformer.Then(_ => secondTransformer(_.Item2, _.Item1));

        public static ITransformer<S, R> Then<S, I1, I2, I3, R>(this ITransformer<S, Tuple<I3, Tuple<I2, I1>>> firstTransformer, Func<I1, I2, I3, R> secondTransformer) =>
            firstTransformer.Then(_ => secondTransformer(_.Item2.Item2, _.Item2.Item1, _.Item1));

        /// <summary>
        /// Create an optional parser.
        /// </summary>
        public static IParser<Option<R>> Optional<R>(this IParser<R> parser) =>
            new OptionalParser<R>(parser);

        public static IParser<R> Optional<R>(this IParser<R> parser, R defaultValue) =>
            new OptionalParserWithDefault<R>(parser, defaultValue);

        public static OrConsumer Either(IConsumer consumer) =>
            new OrConsumer(ImmutableList.Create(consumer));

        public static OrConsumer Or(ImmutableList<IConsumer> firstConsumer, IConsumer secondConsumer) =>
            new OrConsumer(firstConsumer.Add(secondConsumer));

        /// <summary>
        /// Consumer or Consumer
        /// </summary>
        //public static IConsumer Or(this IConsumer firstConsumer, IConsumer secondConsumer) =>
        //    new ConsumerOrConsumer(firstConsumer, secondConsumer);

        /// <summary>
        /// ( => R ) check Predicate[IParserResult[R]] is ( => R)
        /// </summary>
        public static IParser<R> Check<R>(this IParser<R> parser, Predicate<IParserResult<R>> predicate) =>
            new ParserThenCheck<R>(parser, predicate);

        public static IParser<R> TransformResult<R>(this IParser<R> parser, Func<IParserResult<R>, IParserResult<R>> transformFunc) =>
            Then(parser, new ResultTransformer<R>(transformFunc));

        public static IParser<R> TransformEnvironment<R>(this IParser<R> parser,
            Func<ParserEnvironment, ParserEnvironment> transformFunc) =>
                parser.Then(new EnvironmentTransformer(transformFunc));

        public static IConsumer TransformEnvironment(this IConsumer consumer,
            Func<ParserEnvironment, ParserEnvironment> transformFunc) =>
                consumer.Then(new EnvironmentTransformer(transformFunc));

        public static ITransformer<R, R> Optional<R>(this ITransformer<R, R> transformer) =>
            new OptionalTransformer<R>(transformer);

        public static OrTransformer<S, R> Either<S, R>(ITransformer<S, R> transformer) =>
            new OrTransformer<S, R>(ImmutableList.Create(transformer));

        public static OrTransformer<S, R> Or<S, R>(this OrTransformer<S, R> firstTransformer, ITransformer<S, R> secondTransformer) =>
            new OrTransformer<S, R>(firstTransformer.Transformers.Add(secondTransformer));

        //public static ITransformer<S, R> Or<S, R>(this ITransformer<S, R> firstTransformer, ITransformer<S, R> secondTransformer) =>
        //    new TransformerOrTransformer<S, R>(firstTransformer, secondTransformer);

        //public static IParser<R> Or<R>(this IParser<R> firstParser, IParser<R> secondParser) =>
        //    new ParserOrParser<R>(firstParser, secondParser);

        public static OrParser<R> Either<R>(IParser<R> parser) =>
            new OrParser<R>(ImmutableList.Create(parser));

        public static OrParser<R> Or<R>(this OrParser<R> firstParser, IParser<R> secondParser) =>
            new OrParser<R>(firstParser.Parsers.Add(secondParser));

        public static IParser<Boolean> Optional(this IConsumer consumer) =>
            new OptionalConsumer(consumer);
        
        public static ITransformer<R, R> ZeroOrMore<R>(this ITransformer<R, R> transformer) =>
            new ZeroOrMoreTransformer<R>(transformer);

        public static ITransformer<R, R> OneOrMore<R>(this ITransformer<R, R> transformer) =>
            new OneOrMoreTransformer<R>(transformer);

        public static IParser<ImmutableList<R>> ZeroOrMore<R>(this IParser<R> parser) =>
            new ZeroOrMoreParser<R>(parser);

        public static IParser<ImmutableList<R>> OneOrMore<R>(this IParser<R> parser) =>
            new OneOrMoreParser<R>(parser);

        public static IParser<ImmutableList<R>> OneOrMoreForEntireUnit<R>(this IParser<R> parser) =>
            new OneOrMoreParserForEntireUnit<R>(parser);

        public static IParser<ImmutableList<R>> OneOrMore<R>(this IParser<R> elementParser, IConsumer separatorConsumer) =>
            new OneOrMoreParserWithSeparator<R>(separatorConsumer, elementParser);

        public static ITransformer<R, R> Given<R>() =>
            new IdentityTransformer<R>();

        public static ITransformer<Tuple<R2, R1>, Tuple<R2, R1>> Given<R1, R2>() =>
            Given<Tuple<R2, R1>>();
    }
}
