/* * * * *
 * A simple JSON Parser / builder
 * ------------------------------
 * 
 * It mainly has been written as a simple JSON parser. It can build a JSON string
 * from the node-tree, or generate a node tree from any valid JSON string.
 * 
 * Written by Bunny83 
 * 2012-06-09
 * 
 * Changelog now external. See Changelog.txt
 * 
 * The MIT License (MIT)
 * 
 * Copyright (c) 2012-2019 Markus Göbel (Bunny83)
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * 
 * * * * */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SongRequestManagerV2.SimpleJSON
{
    public enum JSONNodeType
    {
        Array = 1,
        Object = 2,
        String = 3,
        Number = 4,
        NullValue = 5,
        Boolean = 6,
        None = 7,
        Custom = 0xFF,
    }
    public enum JSONTextMode
    {
        Compact,
        Indent
    }

    public abstract partial class JSONNode
    {
        #region Enumerators
        public struct Enumerator
        {
            private enum Type { None, Array, Object }
            private readonly Type type;
            private Dictionary<string, JSONNode>.Enumerator m_Object;
            private List<JSONNode>.Enumerator m_Array;
            public bool IsValid => this.type != Type.None;
            public Enumerator(List<JSONNode>.Enumerator aArrayEnum)
            {
                this.type = Type.Array;
                this.m_Object = default(Dictionary<string, JSONNode>.Enumerator);
                this.m_Array = aArrayEnum;
            }
            public Enumerator(Dictionary<string, JSONNode>.Enumerator aDictEnum)
            {
                this.type = Type.Object;
                this.m_Object = aDictEnum;
                this.m_Array = default(List<JSONNode>.Enumerator);
            }
            public KeyValuePair<string, JSONNode> Current
            {
                get
                {
                    if (this.type == Type.Array) {
                        return new KeyValuePair<string, JSONNode>(string.Empty, this.m_Array.Current);
                    }
                    else if (this.type == Type.Object) {
                        return this.m_Object.Current;
                    }

                    return new KeyValuePair<string, JSONNode>(string.Empty, null);
                }
            }
            public bool MoveNext()
            {
                if (this.type == Type.Array) {
                    return this.m_Array.MoveNext();
                }
                else if (this.type == Type.Object) {
                    return this.m_Object.MoveNext();
                }

                return false;
            }
        }
        public struct ValueEnumerator
        {
            private Enumerator m_Enumerator;
            public ValueEnumerator(List<JSONNode>.Enumerator aArrayEnum) : this(new Enumerator(aArrayEnum)) { }
            public ValueEnumerator(Dictionary<string, JSONNode>.Enumerator aDictEnum) : this(new Enumerator(aDictEnum)) { }
            public ValueEnumerator(Enumerator aEnumerator) { this.m_Enumerator = aEnumerator; }
            public JSONNode Current => this.m_Enumerator.Current.Value;
            public bool MoveNext() => this.m_Enumerator.MoveNext();
            public ValueEnumerator GetEnumerator() => this;
        }
        public struct KeyEnumerator
        {
            private Enumerator m_Enumerator;
            public KeyEnumerator(List<JSONNode>.Enumerator aArrayEnum) : this(new Enumerator(aArrayEnum)) { }
            public KeyEnumerator(Dictionary<string, JSONNode>.Enumerator aDictEnum) : this(new Enumerator(aDictEnum)) { }
            public KeyEnumerator(Enumerator aEnumerator) { this.m_Enumerator = aEnumerator; }
            public string Current => this.m_Enumerator.Current.Key;
            public bool MoveNext() => this.m_Enumerator.MoveNext();
            public KeyEnumerator GetEnumerator() => this;
        }

        public class LinqEnumerator : IEnumerator<KeyValuePair<string, JSONNode>>, IEnumerable<KeyValuePair<string, JSONNode>>
        {
            private JSONNode m_Node;
            private Enumerator m_Enumerator;
            internal LinqEnumerator(JSONNode aNode)
            {
                this.m_Node = aNode;
                if (this.m_Node != null) {
                    this.m_Enumerator = this.m_Node.GetEnumerator();
                }
            }
            public KeyValuePair<string, JSONNode> Current => this.m_Enumerator.Current;
            object IEnumerator.Current => this.m_Enumerator.Current;
            public bool MoveNext() => this.m_Enumerator.MoveNext();

            public void Dispose()
            {
                this.m_Node = null;
                this.m_Enumerator = new Enumerator();
            }

            public IEnumerator<KeyValuePair<string, JSONNode>> GetEnumerator() => new LinqEnumerator(this.m_Node);

            public void Reset()
            {
                if (this.m_Node != null) {
                    this.m_Enumerator = this.m_Node.GetEnumerator();
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => new LinqEnumerator(this.m_Node);
        }

        #endregion Enumerators

        #region common interface

        public static bool forceASCII = false; // Use Unicode by default
        public static bool longAsString = false; // lazy creator creates a JSONString instead of JSONNumber
        public static bool allowLineComments = true; // allow "//"-style comments at the end of a line

        public abstract JSONNodeType Tag { get; }

        public virtual JSONNode this[int aIndex] { get => null; set { } }

        public virtual JSONNode this[string aKey] { get => null; set { } }

        public virtual string Value { get => ""; set { } }

        public virtual int Count => 0;

        public virtual bool IsNumber => false;
        public virtual bool IsString => false;
        public virtual bool IsBoolean => false;
        public virtual bool IsNull => false;
        public virtual bool IsArray => false;
        public virtual bool IsObject => false;

        public virtual bool Inline { get => false; set { } }

        public virtual void Add(string aKey, JSONNode aItem)
        {
        }
        public virtual void Add(JSONNode aItem) => this.Add("", aItem);

        public virtual JSONNode Remove(string aKey) => null;

        public virtual JSONNode Remove(int aIndex) => null;

        public virtual JSONNode Remove(JSONNode aNode) => aNode;
        public virtual void Clear() { }

        public virtual JSONNode Clone() => null;

        public virtual IEnumerable<JSONNode> Children
        {
            get
            {
                yield break;
            }
        }

        public IEnumerable<JSONNode> DeepChildren
        {
            get
            {
                foreach (var C in this.Children) {
                    foreach (var D in C.DeepChildren) {
                        yield return D;
                    }
                }
            }
        }

        public virtual bool HasKey(string aKey) => false;

        public virtual JSONNode GetValueOrDefault(string aKey, JSONNode aDefault) => aDefault;

        public override string ToString()
        {
            var sb = new StringBuilder();
            this.WriteToStringBuilder(sb, 0, 0, JSONTextMode.Compact);
            return sb.ToString();
        }

        public virtual string ToString(int aIndent)
        {
            var sb = new StringBuilder();
            this.WriteToStringBuilder(sb, 0, aIndent, JSONTextMode.Indent);
            return sb.ToString();
        }
        internal abstract void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode);

        public abstract Enumerator GetEnumerator();
        public IEnumerable<KeyValuePair<string, JSONNode>> Linq => new LinqEnumerator(this);
        public KeyEnumerator Keys => new KeyEnumerator(this.GetEnumerator());
        public ValueEnumerator Values => new ValueEnumerator(this.GetEnumerator());

        #endregion common interface

        #region typecasting properties


        public virtual double AsDouble
        {
            get
            {
                if (double.TryParse(this.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) {
                    return v;
                }

                return 0.0;
            }
            set => this.Value = value.ToString(CultureInfo.InvariantCulture);
        }

        public virtual int AsInt
        {
            get => (int)this.AsDouble;
            set => this.AsDouble = value;
        }

        public virtual float AsFloat
        {
            get => (float)this.AsDouble;
            set => this.AsDouble = value;
        }

        public virtual bool AsBool
        {
            get
            {
                if (bool.TryParse(this.Value, out var v)) {
                    return v;
                }

                return !string.IsNullOrEmpty(this.Value);
            }
            set => this.Value = (value) ? "true" : "false";
        }

        public virtual long AsLong
        {
            get
            {
                if (long.TryParse(this.Value, out var val)) {
                    return val;
                }

                return 0L;
            }
            set => this.Value = value.ToString();
        }

        public virtual ulong AsULong
        {
            get
            {
                if (ulong.TryParse(this.Value, out var val)) {
                    return val;
                }

                return 0;
            }
            set => this.Value = value.ToString();
        }

        public virtual JSONArray AsArray => this as JSONArray;

        public virtual JSONObject AsObject => this as JSONObject;


        #endregion typecasting properties

        #region operators

        public static implicit operator JSONNode(string s)
        {
            return (s == null) ? (JSONNode)JSONNull.CreateOrGet() : new JSONString(s);
        }
        public static implicit operator string(JSONNode d)
        {
            return (d == null) ? null : d.Value;
        }

        public static implicit operator JSONNode(double n)
        {
            return new JSONNumber(n);
        }
        public static implicit operator double(JSONNode d)
        {
            return (d == null) ? 0 : d.AsDouble;
        }

        public static implicit operator JSONNode(float n)
        {
            return new JSONNumber(n);
        }
        public static implicit operator float(JSONNode d)
        {
            return (d == null) ? 0 : d.AsFloat;
        }

        public static implicit operator JSONNode(int n)
        {
            return new JSONNumber(n);
        }
        public static implicit operator int(JSONNode d)
        {
            return (d == null) ? 0 : d.AsInt;
        }

        public static implicit operator JSONNode(long n)
        {
            if (longAsString) {
                return new JSONString(n.ToString());
            }

            return new JSONNumber(n);
        }
        public static implicit operator long(JSONNode d)
        {
            return (d == null) ? 0L : d.AsLong;
        }

        public static implicit operator JSONNode(ulong n)
        {
            if (longAsString) {
                return new JSONString(n.ToString());
            }

            return new JSONNumber(n);
        }
        public static implicit operator ulong(JSONNode d)
        {
            return (d == null) ? 0 : d.AsULong;
        }

        public static implicit operator JSONNode(bool b)
        {
            return new JSONBool(b);
        }
        public static implicit operator bool(JSONNode d)
        {
            return (d == null) ? false : d.AsBool;
        }

        public static implicit operator JSONNode(KeyValuePair<string, JSONNode> aKeyValue)
        {
            return aKeyValue.Value;
        }

        public static bool operator ==(JSONNode a, object b)
        {
            if (ReferenceEquals(a, b)) {
                return true;
            }

            var aIsNull = a is JSONNull || ReferenceEquals(a, null) || a is JSONLazyCreator;
            var bIsNull = b is JSONNull || ReferenceEquals(b, null) || b is JSONLazyCreator;
            if (aIsNull && bIsNull) {
                return true;
            }

            return !aIsNull && a.Equals(b);
        }

        public static bool operator !=(JSONNode a, object b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj) => ReferenceEquals(this, obj);

        public override int GetHashCode() => base.GetHashCode();

        #endregion operators

        [ThreadStatic]
        private static StringBuilder m_EscapeBuilder;
        internal static StringBuilder EscapeBuilder
        {
            get
            {
                if (m_EscapeBuilder == null) {
                    m_EscapeBuilder = new StringBuilder();
                }

                return m_EscapeBuilder;
            }
        }
        internal static string Escape(string aText)
        {
            var sb = EscapeBuilder;
            sb.Length = 0;
            if (sb.Capacity < aText.Length + aText.Length / 10) {
                sb.Capacity = aText.Length + aText.Length / 10;
            }

            foreach (var c in aText) {
                switch (c) {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    default:
                        if (c < ' ' || (forceASCII && c > 127)) {
                            ushort val = c;
                            sb.Append("\\u").Append(val.ToString("X4"));
                        }
                        else {
                            sb.Append(c);
                        }

                        break;
                }
            }
            var result = sb.ToString();
            sb.Length = 0;
            return result;
        }

        private static JSONNode ParseElement(string token, bool quoted)
        {
            if (quoted) {
                return token;
            }

            if (token.Length <= 5) {
                var tmp = token.ToLower();
                if (tmp == "false" || tmp == "true") {
                    return tmp == "true";
                }

                if (tmp == "null") {
                    return JSONNull.CreateOrGet();
                }
            }
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var val)) {
                return val;
            }
            else {
                return token;
            }
        }

        public static JSONNode Parse(string aJSON)
        {
            var stack = new Stack<JSONNode>();
            JSONNode ctx = null;
            var i = 0;
            var Token = new StringBuilder();
            var TokenName = "";
            var QuoteMode = false;
            var TokenIsQuoted = false;
            var HasNewlineChar = false;
            while (i < aJSON.Length) {
                switch (aJSON[i]) {
                    case '{':
                        if (QuoteMode) {
                            Token.Append(aJSON[i]);
                            break;
                        }
                        stack.Push(new JSONObject());
                        if (ctx != null) {
                            ctx.Add(TokenName, stack.Peek());
                        }
                        TokenName = "";
                        Token.Length = 0;
                        ctx = stack.Peek();
                        HasNewlineChar = false;
                        break;

                    case '[':
                        if (QuoteMode) {
                            Token.Append(aJSON[i]);
                            break;
                        }

                        stack.Push(new JSONArray());
                        if (ctx != null) {
                            ctx.Add(TokenName, stack.Peek());
                        }
                        TokenName = "";
                        Token.Length = 0;
                        ctx = stack.Peek();
                        HasNewlineChar = false;
                        break;

                    case '}':
                    case ']':
                        if (QuoteMode) {

                            Token.Append(aJSON[i]);
                            break;
                        }
                        if (stack.Count == 0) {
                            throw new Exception("JSON Parse: Too many closing brackets");
                        }

                        stack.Pop();
                        if (Token.Length > 0 || TokenIsQuoted) {
                            ctx.Add(TokenName, ParseElement(Token.ToString(), TokenIsQuoted));
                        }

                        if (ctx != null) {
                            ctx.Inline = !HasNewlineChar;
                        }

                        TokenIsQuoted = false;
                        TokenName = "";
                        Token.Length = 0;
                        if (stack.Count > 0) {
                            ctx = stack.Peek();
                        }

                        break;

                    case ':':
                        if (QuoteMode) {
                            Token.Append(aJSON[i]);
                            break;
                        }
                        TokenName = Token.ToString();
                        Token.Length = 0;
                        TokenIsQuoted = false;
                        break;

                    case '"':
                        QuoteMode ^= true;
                        TokenIsQuoted |= QuoteMode;
                        break;

                    case ',':
                        if (QuoteMode) {
                            Token.Append(aJSON[i]);
                            break;
                        }
                        if (Token.Length > 0 || TokenIsQuoted) {
                            ctx.Add(TokenName, ParseElement(Token.ToString(), TokenIsQuoted));
                        }
                        TokenName = "";
                        Token.Length = 0;
                        TokenIsQuoted = false;
                        break;

                    case '\r':
                    case '\n':
                        HasNewlineChar = true;
                        break;

                    case ' ':
                    case '\t':
                        if (QuoteMode) {
                            Token.Append(aJSON[i]);
                        }

                        break;

                    case '\\':
                        ++i;
                        if (QuoteMode) {
                            var C = aJSON[i];
                            switch (C) {
                                case 't':
                                    Token.Append('\t');
                                    break;
                                case 'r':
                                    Token.Append('\r');
                                    break;
                                case 'n':
                                    Token.Append('\n');
                                    break;
                                case 'b':
                                    Token.Append('\b');
                                    break;
                                case 'f':
                                    Token.Append('\f');
                                    break;
                                case 'u': {
                                        var s = aJSON.Substring(i + 1, 4);
                                        Token.Append((char)int.Parse(
                                            s,
                                            System.Globalization.NumberStyles.AllowHexSpecifier));
                                        i += 4;
                                        break;
                                    }
                                default:
                                    Token.Append(C);
                                    break;
                            }
                        }
                        break;
                    case '/':
                        if (allowLineComments && !QuoteMode && i + 1 < aJSON.Length && aJSON[i + 1] == '/') {
                            while (++i < aJSON.Length && aJSON[i] != '\n' && aJSON[i] != '\r') {
                                ;
                            }

                            break;
                        }
                        Token.Append(aJSON[i]);
                        break;
                    case '\uFEFF': // remove / ignore BOM (Byte Order Mark)
                        break;

                    default:
                        Token.Append(aJSON[i]);
                        break;
                }
                ++i;
            }
            if (QuoteMode) {
                throw new Exception("JSON Parse: Quotation marks seems to be messed up.");
            }
            if (ctx == null) {
                return ParseElement(Token.ToString(), TokenIsQuoted);
            }

            return ctx;
        }
    }
    // End of JSONNode

    public partial class JSONArray : JSONNode
    {
        private readonly List<JSONNode> m_List = new List<JSONNode>();
        private bool inline = false;
        public override bool Inline
        {
            get => this.inline;
            set => this.inline = value;
        }

        public override JSONNodeType Tag => JSONNodeType.Array;
        public override bool IsArray => true;
        public override Enumerator GetEnumerator() => new Enumerator(this.m_List.GetEnumerator());

        public override JSONNode this[int aIndex]
        {
            get
            {
                if (aIndex < 0 || aIndex >= this.m_List.Count) {
                    return new JSONLazyCreator(this);
                }

                return this.m_List[aIndex];
            }
            set
            {
                if (value == null) {
                    value = JSONNull.CreateOrGet();
                }

                if (aIndex < 0 || aIndex >= this.m_List.Count) {
                    this.m_List.Add(value);
                }
                else {
                    this.m_List[aIndex] = value;
                }
            }
        }

        public override JSONNode this[string aKey]
        {
            get => new JSONLazyCreator(this);
            set
            {
                if (value == null) {
                    value = JSONNull.CreateOrGet();
                }

                this.m_List.Add(value);
            }
        }

        public override int Count => this.m_List.Count;

        public override void Add(string aKey, JSONNode aItem)
        {
            if (aItem == null) {
                aItem = JSONNull.CreateOrGet();
            }

            this.m_List.Add(aItem);
        }

        public override JSONNode Remove(int aIndex)
        {
            if (aIndex < 0 || aIndex >= this.m_List.Count) {
                return null;
            }

            var tmp = this.m_List[aIndex];
            this.m_List.RemoveAt(aIndex);
            return tmp;
        }

        public override JSONNode Remove(JSONNode aNode)
        {
            this.m_List.Remove(aNode);
            return aNode;
        }

        public override void Clear() => this.m_List.Clear();

        public override JSONNode Clone()
        {
            var node = new JSONArray();
            node.m_List.Capacity = this.m_List.Capacity;
            foreach (var n in this.m_List) {
                if (n != null) {
                    node.Add(n.Clone());
                }
                else {
                    node.Add(null);
                }
            }
            return node;
        }

        public override IEnumerable<JSONNode> Children
        {
            get
            {
                foreach (var N in this.m_List) {
                    yield return N;
                }
            }
        }


        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode)
        {
            aSB.Append('[');
            var count = this.m_List.Count;
            if (this.inline) {
                aMode = JSONTextMode.Compact;
            }

            for (var i = 0; i < count; i++) {
                if (i > 0) {
                    aSB.Append(',');
                }

                if (aMode == JSONTextMode.Indent) {
                    aSB.AppendLine();
                }

                if (aMode == JSONTextMode.Indent) {
                    aSB.Append(' ', aIndent + aIndentInc);
                }

                this.m_List[i].WriteToStringBuilder(aSB, aIndent + aIndentInc, aIndentInc, aMode);
            }
            if (aMode == JSONTextMode.Indent) {
                aSB.AppendLine().Append(' ', aIndent);
            }

            aSB.Append(']');
        }
    }
    // End of JSONArray

    public partial class JSONObject : JSONNode
    {
        private readonly Dictionary<string, JSONNode> m_Dict = new Dictionary<string, JSONNode>();

        private bool inline = false;
        public override bool Inline
        {
            get => this.inline;
            set => this.inline = value;
        }

        public override JSONNodeType Tag => JSONNodeType.Object;
        public override bool IsObject => true;

        public override Enumerator GetEnumerator() => new Enumerator(this.m_Dict.GetEnumerator());


        public override JSONNode this[string aKey]
        {
            get
            {
                if (this.m_Dict.ContainsKey(aKey)) {
                    return this.m_Dict[aKey];
                }
                else {
                    return new JSONLazyCreator(this, aKey);
                }
            }
            set
            {
                if (value == null) {
                    value = JSONNull.CreateOrGet();
                }

                if (this.m_Dict.ContainsKey(aKey)) {
                    this.m_Dict[aKey] = value;
                }
                else {
                    this.m_Dict.Add(aKey, value);
                }
            }
        }

        public override JSONNode this[int aIndex]
        {
            get
            {
                if (aIndex < 0 || aIndex >= this.m_Dict.Count) {
                    return null;
                }

                return this.m_Dict.ElementAt(aIndex).Value;
            }
            set
            {
                if (value == null) {
                    value = JSONNull.CreateOrGet();
                }

                if (aIndex < 0 || aIndex >= this.m_Dict.Count) {
                    return;
                }

                var key = this.m_Dict.ElementAt(aIndex).Key;
                this.m_Dict[key] = value;
            }
        }

        public override int Count => this.m_Dict.Count;

        public override void Add(string aKey, JSONNode aItem)
        {
            if (aItem == null) {
                aItem = JSONNull.CreateOrGet();
            }

            if (aKey != null) {
                if (this.m_Dict.ContainsKey(aKey)) {
                    this.m_Dict[aKey] = aItem;
                }
                else {
                    this.m_Dict.Add(aKey, aItem);
                }
            }
            else {
                this.m_Dict.Add(Guid.NewGuid().ToString(), aItem);
            }
        }

        public override JSONNode Remove(string aKey)
        {
            if (!this.m_Dict.ContainsKey(aKey)) {
                return null;
            }

            var tmp = this.m_Dict[aKey];
            this.m_Dict.Remove(aKey);
            return tmp;
        }

        public override JSONNode Remove(int aIndex)
        {
            if (aIndex < 0 || aIndex >= this.m_Dict.Count) {
                return null;
            }

            var item = this.m_Dict.ElementAt(aIndex);
            this.m_Dict.Remove(item.Key);
            return item.Value;
        }

        public override JSONNode Remove(JSONNode aNode)
        {
            try {
                var item = this.m_Dict.Where(k => k.Value == aNode).First();
                this.m_Dict.Remove(item.Key);
                return aNode;
            }
            catch {
                return null;
            }
        }

        public override void Clear() => this.m_Dict.Clear();

        public override JSONNode Clone()
        {
            var node = new JSONObject();
            foreach (var n in this.m_Dict) {
                node.Add(n.Key, n.Value.Clone());
            }
            return node;
        }

        public override bool HasKey(string aKey) => this.m_Dict.ContainsKey(aKey);

        public override JSONNode GetValueOrDefault(string aKey, JSONNode aDefault)
        {
            if (this.m_Dict.TryGetValue(aKey, out var res)) {
                return res;
            }

            return aDefault;
        }

        public override IEnumerable<JSONNode> Children
        {
            get
            {
                foreach (var N in this.m_Dict) {
                    yield return N.Value;
                }
            }
        }

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode)
        {
            aSB.Append('{');
            var first = true;
            if (this.inline) {
                aMode = JSONTextMode.Compact;
            }

            foreach (var k in this.m_Dict) {
                if (!first) {
                    aSB.Append(',');
                }

                first = false;
                if (aMode == JSONTextMode.Indent) {
                    aSB.AppendLine();
                }

                if (aMode == JSONTextMode.Indent) {
                    aSB.Append(' ', aIndent + aIndentInc);
                }

                aSB.Append('\"').Append(Escape(k.Key)).Append('\"');
                if (aMode == JSONTextMode.Compact) {
                    aSB.Append(':');
                }
                else {
                    aSB.Append(" : ");
                }

                k.Value.WriteToStringBuilder(aSB, aIndent + aIndentInc, aIndentInc, aMode);
            }
            if (aMode == JSONTextMode.Indent) {
                aSB.AppendLine().Append(' ', aIndent);
            }

            aSB.Append('}');
        }
    }
    // End of JSONObject

    public partial class JSONString : JSONNode
    {
        private string m_Data;

        public override JSONNodeType Tag => JSONNodeType.String;
        public override bool IsString => true;

        public override Enumerator GetEnumerator() => new Enumerator();


        public override string Value
        {
            get => this.m_Data;
            set => this.m_Data = value;
        }

        public JSONString(string aData)
        {
            this.m_Data = aData;
        }
        public override JSONNode Clone() => new JSONString(this.m_Data);

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode) => aSB.Append('\"').Append(Escape(this.m_Data)).Append('\"');
        public override bool Equals(object obj)
        {
            if (base.Equals(obj)) {
                return true;
            }

            var s = obj as string;
            if (s != null) {
                return this.m_Data == s;
            }

            var s2 = obj as JSONString;
            if (s2 != null) {
                return this.m_Data == s2.m_Data;
            }

            return false;
        }
        public override int GetHashCode() => this.m_Data.GetHashCode();
        public override void Clear() => this.m_Data = "";
    }
    // End of JSONString

    public partial class JSONNumber : JSONNode
    {
        private double m_Data;

        public override JSONNodeType Tag => JSONNodeType.Number;
        public override bool IsNumber => true;
        public override Enumerator GetEnumerator() => new Enumerator();

        public override string Value
        {
            get => this.m_Data.ToString(CultureInfo.InvariantCulture);
            set
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) {
                    this.m_Data = v;
                }
            }
        }

        public override double AsDouble
        {
            get => this.m_Data;
            set => this.m_Data = value;
        }
        public override long AsLong
        {
            get => (long)this.m_Data;
            set => this.m_Data = value;
        }
        public override ulong AsULong
        {
            get => (ulong)this.m_Data;
            set => this.m_Data = value;
        }

        public JSONNumber(double aData)
        {
            this.m_Data = aData;
        }

        public JSONNumber(string aData)
        {
            this.Value = aData;
        }

        public override JSONNode Clone() => new JSONNumber(this.m_Data);

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode) => aSB.Append(this.Value);
        private static bool IsNumeric(object value) => value is int || value is uint
                || value is float || value is double
                || value is decimal
                || value is long || value is ulong
                || value is short || value is ushort
                || value is sbyte || value is byte;
        public override bool Equals(object obj)
        {
            if (obj == null) {
                return false;
            }

            if (base.Equals(obj)) {
                return true;
            }

            var s2 = obj as JSONNumber;
            if (s2 != null) {
                return this.m_Data == s2.m_Data;
            }

            if (IsNumeric(obj)) {
                return Convert.ToDouble(obj) == this.m_Data;
            }

            return false;
        }
        public override int GetHashCode() => this.m_Data.GetHashCode();
        public override void Clear() => this.m_Data = 0;
    }
    // End of JSONNumber

    public partial class JSONBool : JSONNode
    {
        private bool m_Data;

        public override JSONNodeType Tag => JSONNodeType.Boolean;
        public override bool IsBoolean => true;
        public override Enumerator GetEnumerator() => new Enumerator();

        public override string Value
        {
            get => this.m_Data.ToString();
            set
            {
                if (bool.TryParse(value, out var v)) {
                    this.m_Data = v;
                }
            }
        }
        public override bool AsBool
        {
            get => this.m_Data;
            set => this.m_Data = value;
        }

        public JSONBool(bool aData)
        {
            this.m_Data = aData;
        }

        public JSONBool(string aData)
        {
            this.Value = aData;
        }

        public override JSONNode Clone() => new JSONBool(this.m_Data);

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode) => aSB.Append((this.m_Data) ? "true" : "false");
        public override bool Equals(object obj)
        {
            if (obj == null) {
                return false;
            }

            if (obj is bool) {
                return this.m_Data == (bool)obj;
            }

            return false;
        }
        public override int GetHashCode() => this.m_Data.GetHashCode();
        public override void Clear() => this.m_Data = false;
    }
    // End of JSONBool

    public partial class JSONNull : JSONNode
    {
        private static readonly JSONNull m_StaticInstance = new JSONNull();
        public static bool reuseSameInstance = true;
        public static JSONNull CreateOrGet()
        {
            if (reuseSameInstance) {
                return m_StaticInstance;
            }

            return new JSONNull();
        }
        private JSONNull() { }

        public override JSONNodeType Tag => JSONNodeType.NullValue;
        public override bool IsNull => true;
        public override Enumerator GetEnumerator() => new Enumerator();

        public override string Value
        {
            get => "null";
            set { }
        }
        public override bool AsBool
        {
            get => false;
            set { }
        }

        public override JSONNode Clone() => CreateOrGet();

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj)) {
                return true;
            }

            return (obj is JSONNull);
        }
        public override int GetHashCode() => 0;

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode) => aSB.Append("null");
    }
    // End of JSONNull

    internal partial class JSONLazyCreator : JSONNode
    {
        private JSONNode m_Node = null;
        private readonly string m_Key = null;
        public override JSONNodeType Tag => JSONNodeType.None;
        public override Enumerator GetEnumerator() => new Enumerator();

        public JSONLazyCreator(JSONNode aNode)
        {
            this.m_Node = aNode;
            this.m_Key = null;
        }

        public JSONLazyCreator(JSONNode aNode, string aKey)
        {
            this.m_Node = aNode;
            this.m_Key = aKey;
        }

        private T Set<T>(T aVal) where T : JSONNode
        {
            if (this.m_Key == null) {
                this.m_Node.Add(aVal);
            }
            else {
                this.m_Node.Add(this.m_Key, aVal);
            }

            this.m_Node = null; // Be GC friendly.
            return aVal;
        }

        public override JSONNode this[int aIndex]
        {
            get => new JSONLazyCreator(this);
            set => this.Set(new JSONArray()).Add(value);
        }

        public override JSONNode this[string aKey]
        {
            get => new JSONLazyCreator(this, aKey);
            set => this.Set(new JSONObject()).Add(aKey, value);
        }

        public override void Add(JSONNode aItem) => this.Set(new JSONArray()).Add(aItem);

        public override void Add(string aKey, JSONNode aItem) => this.Set(new JSONObject()).Add(aKey, aItem);

        public static bool operator ==(JSONLazyCreator a, object b)
        {
            if (b == null) {
                return true;
            }

            return System.Object.ReferenceEquals(a, b);
        }

        public static bool operator !=(JSONLazyCreator a, object b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) {
                return true;
            }

            return System.Object.ReferenceEquals(this, obj);
        }

        public override int GetHashCode() => 0;

        public override int AsInt
        {
            get { this.Set(new JSONNumber(0)); return 0; }
            set => this.Set(new JSONNumber(value));
        }

        public override float AsFloat
        {
            get { this.Set(new JSONNumber(0.0f)); return 0.0f; }
            set => this.Set(new JSONNumber(value));
        }

        public override double AsDouble
        {
            get { this.Set(new JSONNumber(0.0)); return 0.0; }
            set => this.Set(new JSONNumber(value));
        }

        public override long AsLong
        {
            get
            {
                if (longAsString) {
                    this.Set(new JSONString("0"));
                }
                else {
                    this.Set(new JSONNumber(0.0));
                }

                return 0L;
            }
            set
            {
                if (longAsString) {
                    this.Set(new JSONString(value.ToString()));
                }
                else {
                    this.Set(new JSONNumber(value));
                }
            }
        }

        public override ulong AsULong
        {
            get
            {
                if (longAsString) {
                    this.Set(new JSONString("0"));
                }
                else {
                    this.Set(new JSONNumber(0.0));
                }

                return 0L;
            }
            set
            {
                if (longAsString) {
                    this.Set(new JSONString(value.ToString()));
                }
                else {
                    this.Set(new JSONNumber(value));
                }
            }
        }

        public override bool AsBool
        {
            get { this.Set(new JSONBool(false)); return false; }
            set => this.Set(new JSONBool(value));
        }

        public override JSONArray AsArray => this.Set(new JSONArray());

        public override JSONObject AsObject => this.Set(new JSONObject());
        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JSONTextMode aMode) => aSB.Append("null");
    }
    // End of JSONLazyCreator

    public static class JSON
    {
        public static JSONNode Parse(string aJSON) => JSONNode.Parse(aJSON);
    }
}