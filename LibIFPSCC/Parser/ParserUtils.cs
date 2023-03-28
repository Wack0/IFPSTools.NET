using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using LexicalAnalysis;
using AST;
using System.Collections;
using System.Linq;
using System.Text;

namespace Parsing {

    /// <summary>
    /// A minimal environment solely for parsing, intended to resolve ambiguity.
    /// </summary>
    public sealed class ParserEnvironment {
        private ParserEnvironment(ImmutableStack<Scope> scopes) {
            this.Scopes = scopes;
        }

        public ParserEnvironment() 
            : this(ImmutableStack.Create(new Scope())) { }

        public ParserEnvironment InScope() =>
            new ParserEnvironment(this.Scopes.Push(new Scope()));

        public ParserEnvironment OutScope() =>
            new ParserEnvironment(this.Scopes.Pop());

        public ParserEnvironment AddSymbol(String name, StorageClsSpec storageClsSpec) =>
            new ParserEnvironment(
                this.Scopes.Pop().Push(
                    this.Scopes.Peek().AddSymbol(name, storageClsSpec)
                )
            );

        public Boolean IsTypedefName(String name) {
            foreach (var scope in this.Scopes) {
                if (scope.Symbols.ContainsKey(name)) {
                    return scope.Symbols[name] == StorageClsSpec.TYPEDEF;
                }
            }
            return false;
        }

        private class Scope {
            public Scope()
                : this(ImmutableDictionary<String, StorageClsSpec>.Empty) { }

            private Scope(ImmutableDictionary<String, StorageClsSpec> symbols) {
                this.Symbols = symbols;
            }

            public Scope AddSymbol(String name, StorageClsSpec storageClsSpec) =>
                new Scope(this.Symbols.Add(name, storageClsSpec));
            
            public ImmutableDictionary<String, StorageClsSpec> Symbols { get; }
        }

        private ImmutableStack<Scope> Scopes { get; }
    }

    /// <summary>Bypasses a number of elements in a sequence and then returns the remaining elements.</summary>
    /// <typeparam name="TSource">The type of the elements of the sequence.</typeparam>
    /// <notes>Allows for stacking many Skips at once without overflowing the stack, unlike the version in LINQ</notes>
    public sealed class SkipEnumerable<T> : IEnumerable<T>
    {
        private readonly IEnumerable<T> source;
        private uint count = 0;

        internal SkipEnumerable(IEnumerable<T> source)
        {
            this.source = source;
            // Merge any downlevel SkipEnumerables into this one.
            var skip = source as SkipEnumerable<T>;
            while (skip != null)
            {
                this.source = skip.source;
                count += skip.count;
                skip = this.source as SkipEnumerable<T>;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(source.GetEnumerator(), count);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public SkipEnumerable<T> Skip(uint count)
        {
            if (count == 0) return this;
            var ret = new SkipEnumerable<T>(source);
            ret.count = this.count + count;
            return ret;
        }

        public SkipEnumerable<T> Skip(int count)
        {
            if (count <= 0) return this;
            return Skip((uint)count);
        }

        private sealed class Enumerator : IEnumerator<T>
        {
            private readonly IEnumerator<T> source;
            private readonly uint count;

            internal Enumerator(IEnumerator<T> source, uint count)
            {
                this.source = source;
                this.count = count;
                Reset();
            }

            public T Current => source.Current;

            object IEnumerator.Current => Current;

            public void Dispose() => source.Dispose();

            public bool MoveNext() => source.MoveNext();

            public void Reset()
            {
                source.Reset();
                for (int i = 0; i < count; i++) source.MoveNext();
            }
        }
    }

    /// <summary>
    /// The input Type for every parsing function.
    /// </summary>
    public sealed class ParserInput {
        public ParserInput(ParserEnvironment environment, SkipEnumerable<Token> source) {
            this.Environment = environment;
            this.Source = source;
        }

        public ParserInput(ParserEnvironment environment, IEnumerable<Token> source)
            : this(environment, new SkipEnumerable<Token>(source))
        {
        }
        public ParserEnvironment Environment { get; }
        public SkipEnumerable<Token> Source { get; }
        public IParserResult<R> Parse<R>(IParser<R> parser) =>
            parser.Parse(this);
    }

    /// <summary>
    /// A parser result with/without content.
    /// </summary>
    public interface IParserResult : ILineInfo {
        ParserInput ToInput();

        Boolean IsSuccessful { get; }

        ParserEnvironment Environment { get; }

        IEnumerable<Token> Source { get; }

        string Name { get; set; }
    }

    public interface IParserFailed : IParserResult
    {
        IReadOnlyList<IParserFailed> InnerFailures { get; }

        IEnumerable<IParserFailed> WalkInnerFailures(bool named = false);

        IEnumerable<StringBuilder> ToStrings();
    }

    /// <summary>
    /// A failed result.
    /// </summary>
    public sealed class ParserFailed : IParserFailed {
        public ParserInput ToInput() {
            throw new InvalidOperationException("Parser failed, can't construct input.");
        }

        public Boolean IsSuccessful => false;

        public ParserEnvironment Environment {
            get {
                throw new NotSupportedException("Parser failed, can't get environment.");
            }
        }

        public IEnumerable<Token> Source {
            get {
                throw new NotSupportedException("Parser failed, can't get source.");
            }
        }

        public int Line { get; }
        public int Column { get; }

        private string m_Name = string.Empty;
        public string Name
        {
            get => m_Name;
            set
            {
                if (m_Name == string.Empty) m_Name = value;
            }
        }

        public IReadOnlyList<IParserFailed> InnerFailures { get; } = null;

        public IEnumerable<IParserFailed> WalkInnerFailures(bool named = false)
        {
            var inner = InnerFailures;
            if (inner == null) yield break;
            foreach (var item in inner)
            {
                if (named && !string.IsNullOrEmpty(item.Name)) yield return item;
                foreach (var innerItem in item.WalkInnerFailures(named)) yield return innerItem;
            }
            yield break;
        }

        public IEnumerable<StringBuilder> ToStrings()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Name)) sb.AppendFormat("{0}: ", Name);
            sb.AppendFormat("line {0}, column {1}", Line, Column);
            yield return sb;

            if (InnerFailures == null) yield break;

            foreach (var inner in InnerFailures)
            {
                foreach (var innerSb in inner.ToStrings())
                {
                    innerSb.Insert(0, "  ");
                    yield return innerSb;
                }
            }
            yield break;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var innerSb in ToStrings())
            {
                sb.Append(innerSb);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public ParserFailed(int line, int column)
        {
            Line = line;
            Column = column;
        }
        public ParserFailed(IParserResult result) : this(result.Line, result.Column) {
            var failure = result as IParserFailed;
            if (failure != null)
            {
                InnerFailures = new List<IParserFailed>() { failure }.AsReadOnly();
            }
        }

        public ParserFailed(ParserInput input, List<IParserFailed> inner) : this(input)
        {
            InnerFailures = inner.AsReadOnly();
        }
        public ParserFailed(ParserInput input) : this(input.Source) { }

        public ParserFailed(IEnumerable<Token> source) : this(source.First()) { }

        public ParserFailed(Token token) : this(token.Line, token.Column) { }
    }

    /// <summary>
    /// A succeeded result.
    /// </summary>
    public sealed class ParserSucceeded : IParserResult {
        public ParserSucceeded(ParserEnvironment environment, IEnumerable<Token> source) {
            this.Environment = environment;
            this.Source = source;
        }

        public Boolean IsSuccessful => true;

        public ParserInput ToInput() => new ParserInput(this.Environment, this.Source);

        public ParserEnvironment Environment { get; }

        public IEnumerable<Token> Source { get; }

        public int Line => Source.First().Line;
        public int Column => Source.First().Column;

        private string m_Name = string.Empty;
        public string Name
        {
            get => m_Name;
            set
            {
                if (m_Name == string.Empty) m_Name = value;
            }
        }

        public static ParserSucceeded Create(ParserEnvironment environment, IEnumerable<Token> source) =>
        new ParserSucceeded(environment, source);

        public static ParserSucceeded<R> Create<R>(R result, ParserEnvironment environment, IEnumerable<Token> source) =>
        new ParserSucceeded<R>(result, environment, source);
    }

    /// <summary>
    /// A parser result with content.
    /// </summary>
    public interface IParserResult<out R> : IParserResult {
        R Result { get; }
    }

    public sealed class ParserFailed<R> : IParserResult<R>, IParserFailed {
        public ParserInput ToInput() {
            throw new InvalidOperationException("Parser failed, can't construct input.");
        }

        public Boolean IsSuccessful => false;

        public R Result {
            get {
                throw new NotSupportedException("Parser failed, can't get result.");
            }
        }

        public ParserEnvironment Environment {
            get {
                throw new NotSupportedException("Parser failed, can't get environment.");
            }
        }

        public IEnumerable<Token> Source {
            get {
                throw new NotSupportedException("Parser failed, can't get source.");
            }
        }

        public int Line { get; }
        public int Column { get; }

        private string m_Name = string.Empty;
        public string Name
        {
            get => m_Name;
            set
            {
                if (m_Name == string.Empty) m_Name = value;
            }
        }

        public IReadOnlyList<IParserFailed> InnerFailures { get; } = null;

        public IEnumerable<IParserFailed> WalkInnerFailures(bool named = false)
        {
            var inner = InnerFailures;
            if (inner == null) yield break;
            foreach (var item in inner)
            {
                if (!named || !string.IsNullOrEmpty(item.Name)) yield return item;
                foreach (var innerItem in item.WalkInnerFailures(named)) yield return innerItem;
            }
            yield break;
        }

        public IEnumerable<StringBuilder> ToStrings()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Name)) sb.AppendFormat("{0}: ", Name);
            sb.AppendFormat("line {0}, column {1}", Line, Column);
            yield return sb;

            if (InnerFailures == null) yield break;

            foreach (var inner in InnerFailures)
            {
                foreach (var innerSb in inner.ToStrings())
                {
                    innerSb.Insert(0, "  ");
                    yield return innerSb;
                }
            }
            yield break;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var innerSb in ToStrings())
            {
                sb.Append(innerSb);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public ParserFailed(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public ParserFailed(IParserResult result) : this(result.Line, result.Column)
        {
            var failure = result as IParserFailed;
            if (failure != null)
            {
                InnerFailures = new List<IParserFailed>() { failure }.AsReadOnly();
            }
        }

        public ParserFailed(ParserInput input, List<IParserFailed> inner) : this(input)
        {
            InnerFailures = inner.AsReadOnly();
        }

        public ParserFailed(ParserInput input) : this(input.Source) { }

        public ParserFailed(IEnumerable<Token> source) : this(source.First()) { }

        public ParserFailed(Token token) : this(token.Line, token.Column) { }
    }

    public sealed class ParserSucceeded<R> : IParserResult<R> {
        public ParserSucceeded(R result, ParserEnvironment environment, IEnumerable<Token> source) {
            this.Result = result;
            this.Environment = environment;
            this.Source = source;
        }

        public ParserInput ToInput() => new ParserInput(this.Environment, this.Source);

        public Boolean IsSuccessful => true;

        public R Result { get; }

        public ParserEnvironment Environment { get; }

        public IEnumerable<Token> Source { get; }

        private string m_Name = string.Empty;
        public string Name
        {
            get => m_Name;
            set
            {
                if (m_Name == string.Empty) m_Name = value;
            }
        }
        public int Line => Source.First().Line;
        public int Column => Source.First().Column;
    }

}
