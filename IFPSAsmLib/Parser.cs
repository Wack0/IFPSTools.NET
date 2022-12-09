using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Collections;

namespace IFPSAsmLib
{
    internal enum ParserSeparatorType : byte
    {
        StartBody,
        NextBody,
        EndBody,
        NextOrEndBody,
        NextOrStartBody,
        Unknown
    }
    internal static class ParserExtensions
    {
        private static bool Matches(this char input, ParserSeparatorType type)
        {
            switch (type)
            {
                case ParserSeparatorType.StartBody:
                    return input == Constants.START_BODY;
                case ParserSeparatorType.NextBody:
                    return input == Constants.NEXT_BODY;
                case ParserSeparatorType.EndBody:
                    return input == Constants.END_BODY;
                case ParserSeparatorType.NextOrEndBody:
                    return input == Constants.NEXT_BODY || input == Constants.END_BODY;
                case ParserSeparatorType.NextOrStartBody:
                    return input == Constants.NEXT_BODY || input == Constants.START_BODY;
                default:
                    return false;
            }
        }

        private static ParserSeparatorType GetSeparator(this char input)
        {
            if (input == Constants.START_BODY) return ParserSeparatorType.StartBody;
            if (input == Constants.NEXT_BODY) return ParserSeparatorType.NextBody;
            if (input == Constants.END_BODY) return ParserSeparatorType.EndBody;
            return ParserSeparatorType.Unknown;
        }

        private static void ThrowEof(ParserLocation location)
        {
            throw new InvalidDataException(string.Format("Unexpected end of file [:{0},{1}]", location.Line, location.Column));
        }
        internal static string ToLiteral(this string input)
        {
            StringBuilder literal = new StringBuilder(input.Length + 2);
            literal.Append(Constants.STRING_CHAR);
            foreach (var c in input)
            {
                switch (c)
                {
                    case '\"': literal.Append("\\\""); break;
                    case '\\': literal.Append(@"\\"); break;
                    case '\0': literal.Append(@"\0"); break;
                    case '\a': literal.Append(@"\a"); break;
                    case '\b': literal.Append(@"\b"); break;
                    case '\f': literal.Append(@"\f"); break;
                    case '\n': literal.Append(@"\n"); break;
                    case '\r': literal.Append(@"\r"); break;
                    case '\t': literal.Append(@"\t"); break;
                    case '\v': literal.Append(@"\v"); break;
                    default:
                        // ASCII printable character
                        if ((c >= 0x20 && c <= 0x7e) || !char.IsControl(c))
                        {
                            literal.Append(c);
                            // As UTF16 escaped character
                        }
                        else if (c < 0x100)
                        {
                            literal.Append(@"\x");
                            literal.Append(((int)c).ToString("x2"));
                        }
                        else
                        {
                            literal.Append(@"\u");
                            literal.Append(((int)c).ToString("x4"));
                        }
                        break;
                }
            }
            literal.Append(Constants.STRING_CHAR);
            return literal.ToString();
        }

        internal static bool AdvanceToNextLine(this ReadOnlySpan<char> str, ref ParserLocation location)
        {
            while (str[location.Offset] != '\n')
            {
                location.AdvanceOffset();
                if (location.Offset >= str.Length) return false;
            }
            location.AdvanceOffset();
            location = location.AdvanceLine();
            return true;
        }

        internal static bool AdvanceWhiteSpace(this ReadOnlySpan<char> str, ref ParserLocation location, bool disallowNewLine)
        {
            if (location.Offset >= str.Length) return false;
            while (char.IsWhiteSpace(str[location.Offset]) || str[location.Offset] == Constants.COMMENT_START)
            {
                var offset = location.Offset;
                if (disallowNewLine && (str[offset] == '\r' || str[offset] == '\n' || str[offset] == Constants.COMMENT_START)) {
                    throw new InvalidDataException(string.Format("Unexpected new line [:{0},{1}]", location.Line, location.Column));
                }
                if (!disallowNewLine && str[offset] == Constants.COMMENT_START) break;
                location.AdvanceOffset();
                if (str[offset] == '\n')
                {
                    location = location.AdvanceLine();
                }
                if (location.Offset >= str.Length)
                {
                    if (!disallowNewLine) return false;
                    ThrowEof(location);
                }
            }
            return true;
        }

        internal static bool AdvanceWhiteSpaceUntilNewLine(this ReadOnlySpan<char> str, ref ParserLocation location)
        {
            while (char.IsWhiteSpace(str[location.Offset]) || str[location.Offset] == Constants.COMMENT_START)
            {
                var offset = location.Offset;
                if (str[offset] == Constants.COMMENT_START) return true;
                location.AdvanceOffset();
                if (str[offset] == '\r' || str[offset] == '\n')
                {
                    if (str[offset] == '\n')
                    {
                        location = location.AdvanceLine();
                    }
                    return true;
                }
                if (location.Offset >= str.Length)
                    ThrowEof(location);
            }
            return false;
        }

        private static bool IsValidNibble(this char input)
        {
            return
                    (input >= '0' && input <= '9') ||
                    (input >= 'a' && input <= 'f') ||
                    (input >= 'A' && input <= 'F')
                    ;
        }

        internal static ParserEntity GetEntity(this ReadOnlySpan<char> str, ref ParserLocation location, ParserSeparatorType? separatorType = null, bool optional = true)
        {
            var origLoc = location.Clone();
            var start = location.Offset;
            var length = 0;
            bool inQuotes = false;
            byte escapingCount = 0;
            byte escapingState = 0;
            for (var chr = str[location.Offset]; inQuotes || !char.IsWhiteSpace(chr); location.AdvanceOffset(), length++)
            {
                if (location.Offset >= str.Length) ThrowEof(location);
                chr = str[location.Offset];
                // allow for escaped data in quotes
                if (inQuotes)
                {
                    if (escapingState == 1)
                    {
                        switch (chr)
                        {
                            case '\"':
                            case '\\':
                            case '0':
                            case 'a':
                            case 'b':
                            case 'f':
                            case 'n':
                            case 'r':
                            case 't':
                            case 'v':
                                escapingCount = escapingState = 0;
                                continue;
                            case 'x': // 2 hexits
                                escapingCount += 2;
                                break;
                            case 'u': // 4 hexits
                                escapingCount += 4;
                                break;
                            default:
                                throw new InvalidDataException(string.Format("Invalid escape character '{0}' [:{1},{2}]", chr, location.Line, location.Column));
                        }
                        escapingState++;
                    }
                    else if (escapingState == (escapingCount - 1))
                    {
                        escapingCount = escapingState = 0;
                    }
                    else if (escapingState == 0)
                    {
                        if (chr == '\\')
                        {
                            escapingState = escapingCount = 1;
                        }
                        else if (chr == '\"')
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        if (!chr.IsValidNibble())
                            throw new InvalidDataException(string.Format("Invalid hex digit '{0}' [:{1},{2}]", chr, location.Line, location.Column));
                        escapingState++;
                    }
                    continue;
                } else if (chr == '\"')
                {
                    inQuotes = true;
                    continue;
                }

                if (!separatorType.HasValue) continue;
                if (chr.Matches(separatorType.Value)) break;
            }
            if (char.IsWhiteSpace(str[start + length - 1])) length--;
            // Allow for whitespace before the separator, do not allow cr or lf.
            var found = ParserSeparatorType.Unknown;
            if (separatorType.HasValue)
            {
                for (var chr = str[location.Offset]; char.IsWhiteSpace(chr) && chr != '\r' && chr != '\n'; location.AdvanceOffset())
                {
                    // no operation
                }
                // Separator must be at this point, if not optional.
                found = str[location.Offset].GetSeparator();
                var matches = str[location.Offset].Matches(separatorType.Value);
                if (!optional && !matches)
                {
                    throw new InvalidDataException(string.Format("After {0}: expected '{1}', got '{2}' {3}", str.Slice(start, length).ToString(), separatorType.Value, found, location));
                }
                if (matches) location.AdvanceOffset();
            }
            return new ParserEntity(str.Slice(start, length), origLoc, found);
        }

        internal static void EnsureBodyEmpty(this ReadOnlySpan<char> str, ref ParserLocation location)
        {
            if (location.Offset >= str.Length) ThrowEof(location);
            if (str[location.Offset] != Constants.END_BODY) throw new InvalidDataException(string.Format("Expected empty declaration body {0}", location));
            location.AdvanceOffset();
        }
    }

    internal class ParserLocation
    {
        internal int Offset;
        internal int Line;
        internal int Column;

        internal ParserLocation Clone()
        {
            return new ParserLocation()
            {
                Offset = Offset,
                Line = Line,
                Column = Column
            };
        }

        internal ParserLocation AdvanceLine()
        {
            return new ParserLocation()
            {
                Offset = Offset,
                Line = Line + 1,
                Column = 0
            };
        }

        internal void AdvanceOffset()
        {
            Offset++;
            Column++;
        }

        public override string ToString()
        {
            return string.Format("[:{0},{1}]", Line, Column);
        }

        internal string ToStringWithOffset(int offset)
        {
            return string.Format("[:{0},{1}]", Line, Column + offset);
        }
    }

    internal readonly ref struct ParserEntity
    {
        internal readonly ReadOnlySpan<char> Value;
        internal readonly ParserLocation Location;
        internal readonly ParserSeparatorType? SeparatorType;

        internal bool FoundSeparator => SeparatorType.HasValue;

        internal ParserEntity(ReadOnlySpan<char> val, ParserLocation loc, ParserSeparatorType type)
        {
            Value = val;
            Location = loc;
            SeparatorType = null;
            if (type != ParserSeparatorType.Unknown) SeparatorType = type;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        internal string ToStringForError()
        {
            return string.Format("{0} {1}", Value.ToString().ToLiteral(), Location);
        }

        internal string ToStringForError(int offset)
        {
            return string.Format("{0} {1}", Value.ToString().ToLiteral(), Location.ToStringWithOffset(offset));
        }

        internal void ThrowInvalid()
        {
            throw new InvalidDataException(string.Format("Invalid token {0}", ToStringForError()));
        }

        internal bool Equals(string str)
        {
            if (Value.Length != str.Length) return false;
            for (int i = 0; i < Value.Length; i++)
            {
                if (Value[i] != str[i]) return false;
            }
            return true;
        }

        internal void ExpectToken(string token)
        {
            if (!Equals(token)) ThrowInvalid();
        }

        internal void ExpectValidName()
        {
            var indexOf = Value.IndexOfAny(Constants.STRING_CHAR, Constants.START_BODY, Constants.END_BODY);
            if (indexOf == -1) return;
            ThrowInvalid();
        }

        internal void ExpectString()
        {
            if (Value.Length < 2) ThrowInvalid();
            if (Value[0] != Constants.STRING_CHAR) ThrowInvalid();
            if (Value[Value.Length - 1] != Constants.STRING_CHAR) ThrowInvalid();
        }

        internal void ExpectSeparatorType(ParserSeparatorType type)
        {
            var actualType = SeparatorType.HasValue ? SeparatorType.Value : ParserSeparatorType.Unknown;
            if (type == ParserSeparatorType.NextOrEndBody)
            {
                if (actualType == ParserSeparatorType.NextBody || actualType == ParserSeparatorType.EndBody) type = actualType;
            }
            if (type == ParserSeparatorType.NextOrStartBody)
            {
                if (actualType == ParserSeparatorType.NextBody || actualType == ParserSeparatorType.StartBody) type = actualType;
            }

            if (type != actualType)
                throw new InvalidDataException(string.Format("After {0}: expected '{1}', got '{2}' {3}", Value.ToString(), type, actualType, Location));
        }
    }

    public enum ElementParentType : byte
    {
        Unknown,
        Obfuscate,
        FileVersion,
        EntryPoint,
        Attribute,
        Type,
        GlobalVariable,
        Function,
        Alias,
        Define,
        Instruction,
        Label,
    }

    public class ParserElement
    {
        /// <summary>
        /// Value of this element
        /// </summary>
        public readonly string Value;
        /// <summary>
        /// The next element on this line; null if not present.
        /// </summary>
        public ParserElement Next { get; internal set; } = null;
        /// <summary>
        /// The next child element (elements inside brackets); null if not present.
        /// </summary>
        public ParserElement NextChild { get; internal set; } = null;
        /// <summary>
        /// Line number of this element
        /// </summary>
        public readonly int Line;
        /// <summary>
        /// Column index of this element
        /// </summary>
        public readonly int Column;
        /// <summary>
        /// Parent element type
        /// </summary>
        public ElementParentType ParentType;

        internal ParserElement(ParserEntity entity, ElementParentType type)
        {
            Value = entity.ToString();
            Line = entity.Location.Line;
            Column = entity.Location.Column;
            ParentType = type;
        }

        public IEnumerable<ParserElement> Children
        {
            get
            {
                for (var next = NextChild; next != null; next = next.NextChild) yield return next;
            }
        }

        public IEnumerable<ParserElement> Tokens
        {
            get
            {
                yield return this;
                for (var next = Next; next != null; next = next.Next) yield return next;
            }
        }

        internal string ToStringForError()
        {
            return string.Format("{0} [:{1},{2}]", Value.ToLiteral(), Line, Column);
        }

        internal void ThrowInvalid()
        {
            ThrowInvalid("Invalid token");
        }

        internal void ThrowInvalid(string error)
        {
            throw new InvalidDataException(string.Format("{0} {1}", error, ToStringForError()));
        }

        internal void ExpectValidName()
        {
            var indexOf = Value.AsSpan().IndexOfAny(Constants.STRING_CHAR, Constants.START_BODY, Constants.END_BODY);
            if (indexOf == -1) return;
            ThrowInvalid();
        }

        internal void ExpectString()
        {
            if (Value.Length < 2) ThrowInvalid();
            if (Value[0] != Constants.STRING_CHAR) ThrowInvalid();
            if (Value[Value.Length - 1] != Constants.STRING_CHAR) ThrowInvalid();
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public class ParsedBody
    {
        /// <summary>
        /// The main element; type, function, global, or entry point.
        /// </summary>
        public readonly ParserElement Element;
        /// <summary>
        /// Children instruction elements of a function element.
        /// </summary>
        public readonly IReadOnlyList<ParserElement> Children;

        private static IReadOnlyList<ParserElement> s_EmptyList = new List<ParserElement>(0).AsReadOnly();

        internal ParsedBody(ParserElement elem) : this(elem, s_EmptyList)
        {

        }

        internal ParsedBody(ParserElement elem, List<ParserElement> list) : this(elem, list == null ? s_EmptyList : list.AsReadOnly())
        {

        }

        private ParsedBody(ParserElement elem, IReadOnlyList<ParserElement> list)
        {
            Element = elem;
            Children = list;
        }
    }

    public static class Parser
    {
        private static void ParseTokenWithUnknownBody(
            ReadOnlySpan<char> str,
            ref ParserLocation location,
            ElementParentType parentType,
            ParserElement current,
            ref ParserElement next,
            bool baseType
        )
        {
            ParserElement currentChild, nextChild;
            str.AdvanceWhiteSpace(ref location, true);
            var typeNext = str.GetEntity(ref location, ParserSeparatorType.StartBody, false);
            typeNext.ExpectValidName();
            current.Next = next = new ParserElement(typeNext, parentType);
            if (baseType && parentType == ElementParentType.Type && next.Value == Constants.TYPE_FUNCTION_POINTER)
            {
                ParseTokenWithUnknownBody(str, ref location, parentType, next, ref next, false);
                typeNext = str.GetEntity(ref location, ParserSeparatorType.NextOrEndBody, false);
                typeNext.ExpectToken(string.Empty);
                return;
            }
            str.AdvanceWhiteSpace(ref location, true);
            typeNext = str.GetEntity(ref location, ParserSeparatorType.NextOrEndBody, true);
            currentChild = nextChild = next.NextChild = new ParserElement(typeNext, parentType);
            while (!typeNext.FoundSeparator || typeNext.SeparatorType.Value != ParserSeparatorType.EndBody)
            {
                bool nextIsChild = typeNext.FoundSeparator;
                str.AdvanceWhiteSpace(ref location, true);
                typeNext = str.GetEntity(ref location, ParserSeparatorType.NextOrEndBody, true);
                if (nextIsChild)
                {
                    currentChild.NextChild = new ParserElement(typeNext, parentType);
                    currentChild = nextChild = currentChild.NextChild;
                }
                else
                {
                    nextChild.Next = new ParserElement(typeNext, parentType);
                    nextChild = nextChild.Next;
                }
            }
        }

        internal static ParserElement Parse(this ReadOnlySpan<char> str, ref ParserLocation location)
        {
            ParserElement current = null, next = null, nextChild = null;
            var parentType = default(ElementParentType);
            while (true)
            {
                if (!str.AdvanceWhiteSpace(ref location, false)) return null;
                var chr = str[location.Offset];
                // comment
                if (chr == Constants.COMMENT_START)
                {
                    if (!str.AdvanceToNextLine(ref location)) return null;
                    continue;
                }
                // element
                if (chr == Constants.ELEMENT_START)
                {
                    var type = str.GetEntity(ref location, ParserSeparatorType.StartBody, true);
                    var typeString = type.ToString();
                    switch (typeString)
                    {
                        case Constants.ELEMENT_OBFUSCATE:
                            // .obfuscate
                            if (type.FoundSeparator) str.EnsureBodyEmpty(ref location);
                            parentType = ElementParentType.Obfuscate;
                            current = new ParserElement(type, parentType);
                            if (!str.AdvanceWhiteSpaceUntilNewLine(ref location)) current.Next.ThrowInvalid();
                            return current;
                        case Constants.ELEMENT_VERSION:
                            // .version int
                            if (type.FoundSeparator) str.EnsureBodyEmpty(ref location);
                            parentType = ElementParentType.FileVersion;
                            current = new ParserElement(type, parentType);
                            str.AdvanceWhiteSpace(ref location, true);
                            var typeNext = str.GetEntity(ref location);
                            if (!int.TryParse(typeNext.Value.ToString(), out var __int))
                                typeNext.ThrowInvalid();
                            current.Next = new ParserElement(typeNext, parentType);
                            if (!str.AdvanceWhiteSpaceUntilNewLine(ref location)) current.Next.ThrowInvalid();
                            return current;
                        case Constants.ELEMENT_ENTRYPOINT:
                            // .entry function_name
                            if (type.FoundSeparator) str.EnsureBodyEmpty(ref location);
                            parentType = ElementParentType.EntryPoint;
                            current = new ParserElement(type, parentType);
                            str.AdvanceWhiteSpace(ref location, true);
                            typeNext = str.GetEntity(ref location);
                            typeNext.ExpectValidName();
                            current.Next = new ParserElement(typeNext, parentType);
                            if (!str.AdvanceWhiteSpaceUntilNewLine(ref location)) current.Next.ThrowInvalid();
                            return current;
                        case Constants.ELEMENT_TYPE:
                            // .type [(export)] type(...) name
                            parentType = ElementParentType.Type;
                            current = new ParserElement(type, parentType);
                            if (type.FoundSeparator)
                            {
                                str.AdvanceWhiteSpace(ref location, true);
                                var typeChild = str.GetEntity(ref location, ParserSeparatorType.EndBody, false);
                                if (!typeChild.Value.IsEmpty)
                                {
                                    if (!typeChild.Equals(Constants.ELEMENT_BODY_IMPORTED))
                                        typeChild.ExpectToken(Constants.ELEMENT_BODY_EXPORTED);
                                    current.NextChild = new ParserElement(typeChild, parentType);
                                }
                            }

                            // type(...), will be looked at later
                            ParseTokenWithUnknownBody(str, ref location, parentType, current, ref next, true);

                            // name
                            str.AdvanceWhiteSpace(ref location, true);
                            typeNext = str.GetEntity(ref location);
                            typeNext.ExpectValidName();
                            next.Next = new ParserElement(typeNext, parentType);
                            if (!str.AdvanceWhiteSpaceUntilNewLine(ref location)) next.Next.ThrowInvalid();
                            return current;
                        case Constants.ELEMENT_ALIAS:
                            // .alias varname additionalname
                            if (type.FoundSeparator) str.EnsureBodyEmpty(ref location);
                            parentType = ElementParentType.Alias;
                            current = new ParserElement(type, parentType);
                            str.AdvanceWhiteSpace(ref location, true);
                            typeNext = str.GetEntity(ref location);
                            typeNext.ExpectValidName();
                            current.Next = next = new ParserElement(typeNext, parentType);
                            str.AdvanceWhiteSpace(ref location, true);
                            typeNext = str.GetEntity(ref location);
                            typeNext.ExpectValidName();
                            next.Next = new ParserElement(typeNext, parentType);
                            if (!str.AdvanceWhiteSpaceUntilNewLine(ref location)) next.Next.ThrowInvalid();
                            return current;
                        case Constants.ELEMENT_DEFINE:
                            // .define immediate_operand defname
                            if (type.FoundSeparator) str.EnsureBodyEmpty(ref location);
                            parentType = ElementParentType.Define;
                            current = new ParserElement(type, parentType);
                            str.AdvanceWhiteSpace(ref location, true);
                            typeNext = str.GetEntity(ref location, ParserSeparatorType.StartBody, false);
                            typeNext.ExpectValidName();
                            current.Next = next = new ParserElement(typeNext, parentType);
                            // operand is an immediate value: type(data)
                            str.AdvanceWhiteSpace(ref location, true);
                            typeNext = str.GetEntity(ref location, ParserSeparatorType.EndBody, false);
                            next.NextChild = new ParserElement(typeNext, parentType);
                            // name
                            str.AdvanceWhiteSpace(ref location, true);
                            typeNext = str.GetEntity(ref location);
                            typeNext.ExpectValidName();
                            next.Next = next = new ParserElement(typeNext, parentType);
                            if (!str.AdvanceWhiteSpaceUntilNewLine(ref location)) next.ThrowInvalid();
                            return current;
                        case Constants.ELEMENT_GLOBALVARIABLE:
                            // .global [(export)] typename varname
                            parentType = ElementParentType.GlobalVariable;
                            current = next = new ParserElement(type, parentType);
                            if (type.FoundSeparator)
                            {
                                str.AdvanceWhiteSpace(ref location, true);
                                var typeChild = str.GetEntity(ref location, ParserSeparatorType.EndBody, false);
                                if (!typeChild.Value.IsEmpty)
                                {
                                    if (!typeChild.Equals(Constants.ELEMENT_BODY_IMPORTED))
                                        typeChild.ExpectToken(Constants.ELEMENT_BODY_EXPORTED);
                                    current.NextChild = new ParserElement(typeChild, parentType);
                                }
                            }

                            // typename
                            str.AdvanceWhiteSpace(ref location, true);
                            typeNext = str.GetEntity(ref location);
                            typeNext.ExpectValidName();
                            next.Next = new ParserElement(typeNext, parentType);
                            next = next.Next;

                            // varname
                            str.AdvanceWhiteSpace(ref location, true);
                            typeNext = str.GetEntity(ref location);
                            typeNext.ExpectValidName();
                            next.Next = new ParserElement(typeNext, parentType);
                            if (!str.AdvanceWhiteSpaceUntilNewLine(ref location)) next.Next.ThrowInvalid();
                            return current;
                        case Constants.ELEMENT_FUNCTION:
                            // function
                            // .function [(export)] external callingconv internal|com(vtblindex)|class(...)|dll(...) returnsval|void name (...)
                            // .function [(export)] void|typename name(...)

                            parentType = ElementParentType.Function;
                            current = next = new ParserElement(type, parentType);
                            if (type.FoundSeparator)
                            {
                                str.AdvanceWhiteSpace(ref location, true);
                                var typeChild = str.GetEntity(ref location, ParserSeparatorType.EndBody, false);
                                if (!typeChild.Value.IsEmpty)
                                {
                                    if (!typeChild.Equals(Constants.ELEMENT_BODY_IMPORTED))
                                        typeChild.ExpectToken(Constants.ELEMENT_BODY_EXPORTED);
                                    current.NextChild = new ParserElement(typeChild, parentType);
                                }
                            }

                            // external|void|typename
                            str.AdvanceWhiteSpace(ref location, true);
                            typeNext = str.GetEntity(ref location);
                            typeNext.ExpectValidName();
                            current.Next = new ParserElement(typeNext, parentType);
                            next = next.Next;

                            str.AdvanceWhiteSpace(ref location, true);
                            if (next.Value == Constants.FUNCTION_EXTERNAL)
                            {
                                str.AdvanceWhiteSpace(ref location, true);
                                typeNext = str.GetEntity(ref location, ParserSeparatorType.StartBody, true);
                                typeNext.ExpectValidName();
                                if (typeNext.Equals(Constants.FUNCTION_EXTERNAL_INTERNAL))
                                {
                                    typeNext.ExpectSeparatorType(ParserSeparatorType.Unknown);
                                    next.Next = next = new ParserElement(typeNext, parentType);
                                }
                                else
                                {
                                    if (typeNext.Equals(Constants.FUNCTION_EXTERNAL_COM))
                                    {
                                        // com(vtblindex)
                                        typeNext.ExpectSeparatorType(ParserSeparatorType.StartBody);
                                        next.Next = next = new ParserElement(typeNext, parentType);
                                        str.AdvanceWhiteSpace(ref location, true);
                                        typeNext = str.GetEntity(ref location, ParserSeparatorType.EndBody, false);
                                        if (!int.TryParse(typeNext.Value.ToString(), out __int))
                                            typeNext.ThrowInvalid();
                                        next.NextChild = new ParserElement(typeNext, parentType);
                                    }
                                    else if (typeNext.Equals(Constants.FUNCTION_EXTERNAL_CLASS))
                                    {
                                        // class(classname,funcname[,property])
                                        typeNext.ExpectSeparatorType(ParserSeparatorType.StartBody);
                                        next.Next = next = new ParserElement(typeNext, parentType);
                                        // classname
                                        str.AdvanceWhiteSpace(ref location, true);
                                        typeNext = str.GetEntity(ref location, ParserSeparatorType.NextBody, false);
                                        typeNext.ExpectValidName();
                                        next.NextChild = nextChild = new ParserElement(typeNext, parentType);
                                        // funcname
                                        str.AdvanceWhiteSpace(ref location, true);
                                        typeNext = str.GetEntity(ref location, ParserSeparatorType.NextOrEndBody, false);
                                        typeNext.ExpectValidName();
                                        nextChild.NextChild = nextChild = new ParserElement(typeNext, parentType);
                                        // property
                                        if (typeNext.SeparatorType == ParserSeparatorType.NextBody)
                                        {
                                            str.AdvanceWhiteSpace(ref location, true);
                                            typeNext = str.GetEntity(ref location, ParserSeparatorType.EndBody, false);
                                            typeNext.ExpectToken(Constants.FUNCTION_EXTERNAL_CLASS_PROPERTY);
                                            nextChild.NextChild = nextChild = new ParserElement(typeNext, parentType);
                                        }
                                    }
                                    else if (typeNext.Equals(Constants.FUNCTION_EXTERNAL_DLL))
                                    {
                                        // dll(dllname,procname[,delayload][,alteredsearchpath])
                                        typeNext.ExpectSeparatorType(ParserSeparatorType.StartBody);
                                        next.Next = next = new ParserElement(typeNext, parentType);
                                        // dllname
                                        str.AdvanceWhiteSpace(ref location, true);
                                        typeNext = str.GetEntity(ref location, ParserSeparatorType.NextBody, false);
                                        typeNext.ExpectString();
                                        next.NextChild = nextChild = new ParserElement(typeNext, parentType);
                                        // procname
                                        str.AdvanceWhiteSpace(ref location, true);
                                        typeNext = str.GetEntity(ref location, ParserSeparatorType.NextOrEndBody, false);
                                        typeNext.ExpectString();
                                        nextChild.NextChild = nextChild = new ParserElement(typeNext, parentType);
                                        bool firstWasDelayLoad = false;
                                        // delayload|alteredsearchpath
                                        if (typeNext.SeparatorType == ParserSeparatorType.NextBody)
                                        {
                                            str.AdvanceWhiteSpace(ref location, true);
                                            typeNext = str.GetEntity(ref location, ParserSeparatorType.NextOrEndBody, false);
                                            if (!typeNext.Equals(Constants.FUNCTION_EXTERNAL_DLL_DELAYLOAD))
                                            {
                                                if (!typeNext.Equals(Constants.FUNCTION_EXTERNAL_DLL_ALTEREDSEARCHPATH)) typeNext.ThrowInvalid();
                                            }
                                            else firstWasDelayLoad = true;
                                            nextChild.NextChild = nextChild = new ParserElement(typeNext, parentType);
                                        }
                                        // other delayload|alteredsearchpath
                                        if (typeNext.SeparatorType == ParserSeparatorType.NextBody)
                                        {
                                            str.AdvanceWhiteSpace(ref location, true);
                                            typeNext = str.GetEntity(ref location, ParserSeparatorType.EndBody, false);
                                            typeNext.ExpectToken(
                                                firstWasDelayLoad ?
                                                Constants.FUNCTION_EXTERNAL_DLL_ALTEREDSEARCHPATH :
                                                Constants.FUNCTION_EXTERNAL_DLL_DELAYLOAD
                                            );
                                            nextChild.NextChild = nextChild = new ParserElement(typeNext, parentType);
                                        }
                                    }
                                    else
                                        typeNext.ThrowInvalid();

                                    str.AdvanceWhiteSpace(ref location, true);
                                    // calling convention
                                    typeNext = str.GetEntity(ref location);
                                    if (
                                        !typeNext.Equals(Constants.FUNCTION_FASTCALL) &&
                                        !typeNext.Equals(Constants.FUNCTION_PASCAL) &&
                                        !typeNext.Equals(Constants.FUNCTION_CDECL) &&
                                        !typeNext.Equals(Constants.FUNCTION_STDCALL)
                                    )
                                        typeNext.ThrowInvalid();
                                    next.Next = next = new ParserElement(typeNext, parentType);
                                }
                                str.AdvanceWhiteSpace(ref location, true);
                                // returnsval|void
                                typeNext = str.GetEntity(ref location);
                                if (!typeNext.Equals(Constants.FUNCTION_RETURN_VAL) && !typeNext.Equals(Constants.FUNCTION_RETURN_VOID)) typeNext.ThrowInvalid();
                                next.Next = next = new ParserElement(typeNext, parentType);
                            }

                            // name(...), will be looked at later
                            ParseTokenWithUnknownBody(str, ref location, parentType, next, ref next, false);

                            if (!str.AdvanceWhiteSpaceUntilNewLine(ref location)) next.ThrowInvalid();
                            return current;
                        default:
                            type.ThrowInvalid();
                            break;
                    }
                }
                // attribute
                if (chr == Constants.START_ATTRIBUTE)
                {
                    parentType = ElementParentType.Attribute;
                    location.AdvanceOffset();
                    str.AdvanceWhiteSpace(ref location, true);
                    // name(operands)
                    var type = str.GetEntity(ref location, ParserSeparatorType.StartBody, false);
                    type.ExpectValidName();
                    current = new ParserElement(type, parentType);
                    str.AdvanceWhiteSpace(ref location, true);
                    // operands
                    next = current;
                    if (str[location.Offset] != Constants.END_BODY)
                    {
                        do
                        {
                            // must be immediate value: type(data)
                            type = str.GetEntity(ref location, ParserSeparatorType.StartBody, false);
                            type.ExpectValidName();
                            next.Next = next = new ParserElement(type, parentType);
                            str.AdvanceWhiteSpace(ref location, true);
                            type = str.GetEntity(ref location, ParserSeparatorType.EndBody, false);
                            next.NextChild = new ParserElement(type, parentType);
                            // next token may be a comma or end, let's find out
                            str.AdvanceWhiteSpace(ref location, true);
                            // expecting a blank token
                            type = str.GetEntity(ref location, ParserSeparatorType.NextOrEndBody, false);
                            type.ExpectToken(string.Empty);
                        } while (type.SeparatorType == ParserSeparatorType.NextBody);
                    }
                    else location.AdvanceOffset();
                    str.AdvanceWhiteSpace(ref location, true);
                    if (str[location.Offset] != Constants.END_ATTRIBUTE)
                    {
                        type.ThrowInvalid();
                    }
                    location.AdvanceOffset();
                    if (!str.AdvanceWhiteSpaceUntilNewLine(ref location)) type.ThrowInvalid();
                    return current;
                }

                // instruction or label
                parentType = ElementParentType.Label;
                var data = str.GetEntity(ref location);
                data.ExpectValidName();
                if (data.Value[data.Value.Length - 1] == Constants.LABEL_SPECIFIER) // label
                {
                    data = new ParserEntity(
                        data.Value.Slice(0, data.Value.Length - 1),
                        data.Location,
                        data.SeparatorType.HasValue ? data.SeparatorType.Value : ParserSeparatorType.Unknown
                    );
                    var label = new ParserElement(data, parentType);
                    if (label.Value == Constants.LABEL_NULL) label.ThrowInvalid();
                    return label;
                }
                // instruction
                parentType = ElementParentType.Instruction;
                // opcode ...
                current = new ParserElement(data, parentType);
                if (str.AdvanceWhiteSpaceUntilNewLine(ref location)) return current;
                // operands
                next = current;
                do
                {
                    data = str.GetEntity(ref location, ParserSeparatorType.NextOrStartBody, true);
                    data.ExpectValidName();
                    next.Next = next = new ParserElement(data, parentType);
                    if (data.FoundSeparator && data.SeparatorType == ParserSeparatorType.StartBody)
                    {
                        // operand is an immediate value: type(data)
                        str.AdvanceWhiteSpace(ref location, true);
                        data = str.GetEntity(ref location, ParserSeparatorType.EndBody, false);
                        next.NextChild = new ParserElement(data, parentType);
                        // next token may be a comma or newline, let's find out
                        if (str.AdvanceWhiteSpaceUntilNewLine(ref location)) break;
                        // expecting a blank token with NextBody
                        data = str.GetEntity(ref location, ParserSeparatorType.NextBody, false);
                        data.ExpectToken(string.Empty);
                    }
                } while (!str.AdvanceWhiteSpaceUntilNewLine(ref location));
                return current;
            }
        }

        public static List<ParsedBody> Parse(ReadOnlySpan<char> str)
        {
            var location = new ParserLocation();

            var ret = new List<ParsedBody>();
            ParserElement last = null;
            List<ParserElement> list = null;
            List<ParserElement> attributes = null;
            do
            {
                var elem = str.Parse(ref location);
                if (elem == null)
                {
                    if (last != null)
                    {
                        ret.Add(new ParsedBody(last, list));
                    }
                    break;
                }

                switch (elem.ParentType)
                {
                    case ElementParentType.Unknown:
                        elem.ThrowInvalid();
                        break;
                    case ElementParentType.Attribute:
                        if (attributes == null) attributes = new List<ParserElement>();
                        attributes.Add(elem);
                        break;
                    case ElementParentType.Instruction:
                    case ElementParentType.Label:
                    case ElementParentType.Alias:
                        if (attributes != null) elem.ThrowInvalid();
                        if (list == null) list = new List<ParserElement>();
                        if (last == null) elem.ThrowInvalid();
                        list.Add(elem);
                        break;
                    case ElementParentType.Function:
                        if (last != null)
                        {
                            ret.Add(new ParsedBody(last, list));
                            list = null;
                        }
                        list = attributes;
                        attributes = null;
                        last = elem;
                        break;
                    default:
                        if (last != null)
                        {
                            ret.Add(new ParsedBody(last, list));
                            last = null;
                            list = null;
                        }
                        if (elem.ParentType != ElementParentType.Type && attributes != null) elem.ThrowInvalid();
                        ret.Add(new ParsedBody(elem, attributes));
                        attributes = null;
                        break;
                }
            } while (true);
            return ret;
        }

        public static List<ParsedBody> Parse(string str) => Parse(str.AsSpan());
    }
}
