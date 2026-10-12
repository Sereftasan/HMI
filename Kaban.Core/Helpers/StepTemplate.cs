using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// A HELP PAGE WRITTEN AS A TEMPLATE (2 October): the words in a file a person
    /// can read and change, the machine's numbers filled in when the page is
    /// opened. The cylinder step-by-step pages are written this way - one template
    /// per valve and feedback pair, per language (Help/Templates/EN, TR).
    ///
    ///     {{Name}}                a value - already safe for HTML
    ///     {{#Name}} .. {{/Name}}  shown when Name is set / true; a list repeats it
    ///     {{^Name}} .. {{/Name}}  shown when Name is not set / false
    ///     {{>Name}}               a phrase: &lt;!--def Name--&gt; .. &lt;!--/def--&gt;
    ///     {{[SEP}} a {{|}} b {{]}} the parts that are not empty, joined by SEP
    ///     {{! .. }}               a note, left out
    ///     &lt;!--doc .. --&gt;          a note for whoever edits the file, left out
    ///
    /// A line holding only section tags or a note is left out whole, so a
    /// template can put each tag on a line of its own. The phrases are also what
    /// the program builds its own small pieces from (a sensor's name, "Step 4",
    /// ON / OFF), so every word of the page is in the template.
    /// </summary>
    public sealed class StepTemplate
    {
        private readonly Dictionary<string, string> _defs = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Node>> _parsedDefs = new Dictionary<string, List<Node>>(StringComparer.Ordinal);
        private readonly List<Node> _body;

        /// <summary>The template's language: how a first letter is made capital (Turkish i / \u0130).</summary>
        public readonly CultureInfo Culture;

        /// <summary>The file it was read from ("" when made from text).</summary>
        public string Path = "";

        /// <summary>Names the template asked for that the page did not give (for the tests).</summary>
        public readonly HashSet<string> Missing = new HashSet<string>(StringComparer.Ordinal);

        public StepTemplate(string text, CultureInfo culture = null)
        {
            Culture = culture ?? CultureInfo.InvariantCulture;
            text = (text ?? "").Replace("\r\n", "\n");
            // notes for the editor
            text = Regex.Replace(text, @"<!--doc.*?-->[ \t]*\n?", "", RegexOptions.Singleline);
            // the phrases
            text = Regex.Replace(text, @"<!--def\s+([A-Za-z0-9_]+)-->(.*?)<!--/def-->[ \t]*\n?", m =>
            {
                _defs[m.Groups[1].Value] = m.Groups[2].Value;
                return "";
            }, RegexOptions.Singleline);
            _body = Parse(Standalone(text));
            foreach (var kv in _defs) _parsedDefs[kv.Key] = Parse(kv.Value);
        }

        public static StepTemplate Load(string path, CultureInfo culture = null)
        {
            return new StepTemplate(File.ReadAllText(path, Encoding.UTF8), culture) { Path = path };
        }

        /// <summary>True when the template defines the phrase.</summary>
        public bool HasPhrase(string name) { return _defs.ContainsKey(name); }

        /// <summary>The phrases' names (for the tests).</summary>
        public IEnumerable<string> PhraseNames { get { return _defs.Keys; } }

        /// <summary>A phrase, filled from the values given (innermost first).</summary>
        public string Phrase(string name, params IDictionary<string, object>[] contexts)
        {
            List<Node> nodes;
            if (!_parsedDefs.TryGetValue(name, out nodes)) { Missing.Add(">" + name); return ""; }
            var stack = new List<IDictionary<string, object>>();
            for (int i = contexts.Length - 1; i >= 0; i--) if (contexts[i] != null) stack.Add(contexts[i]);
            var sb = new StringBuilder();
            RenderNodes(nodes, stack, sb);
            return sb.ToString();
        }

        /// <summary>A phrase of "key = value" lines as a table (words the program translates: "Up = Yukar\u0131").</summary>
        public Dictionary<string, string> Table(string name)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string body;
            if (!_defs.TryGetValue(name, out body)) return d;
            foreach (var raw in body.Split('\n'))
            {
                int eq = raw.IndexOf(" = ", StringComparison.Ordinal);
                if (eq <= 0) continue;
                var key = System.Net.WebUtility.HtmlDecode(raw.Substring(0, eq).Trim());
                var value = raw.Substring(eq + 3).Trim();
                if (key.Length > 0) d[key] = value;
            }
            return d;
        }

        /// <summary>The page, filled from the values given.</summary>
        public string Render(IDictionary<string, object> model)
        {
            var sb = new StringBuilder();
            RenderNodes(_body, new List<IDictionary<string, object>> { model }, sb);
            return sb.ToString();
        }

        /// <summary>
        /// The first letter made capital, after any tags, spaces and character
        /// references it starts with - a line may begin with a picture
        /// (&lt;span class="ic"&gt;&amp;#128065;&lt;/span&gt;) - in the template's language.
        /// </summary>
        public string Cap(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;
            int i = 0;
            while (i < html.Length)
            {
                if (html[i] == '<') { int close = html.IndexOf('>', i); if (close < 0) break; i = close + 1; }
                else if (html[i] == '&') { int semi = html.IndexOf(';', i); if (semi < 0 || semi - i > 10) break; i = semi + 1; }
                else if (char.IsWhiteSpace(html[i])) i++;
                else break;
            }
            return i < html.Length && char.IsLower(html[i]) ? html.Substring(0, i) + char.ToUpper(html[i], Culture) + html.Substring(i + 1) : html;
        }

        // ================================================================

        private enum Kind { Text, Var, Section, Inverted, Partial, Join, Sep }

        private sealed class Node
        {
            public Kind Kind;
            public string Text = "";       // the text, the name, or a join's separator
            public List<Node> Children;
        }

        private const char SepMark = '\u0001';

        /// <summary>A line with only section tags (or a note) on it is left out, newline and all.</summary>
        private static string Standalone(string text)
        {
            var lines = text.Split('\n');
            var sb = new StringBuilder();
            var only = new Regex(@"^[ \t]*(\{\{[#^/!][^}]*\}\}[ \t]*)+$");
            for (int i = 0; i < lines.Length; i++)
            {
                // the tags stay, the line they stood on goes
                if (only.IsMatch(lines[i])) { sb.Append(lines[i].Trim()); continue; }
                sb.Append(lines[i]);
                if (i < lines.Length - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        private static List<Node> Parse(string text)
        {
            int pos = 0;
            var root = new List<Node>();
            var stack = new Stack<KeyValuePair<Node, List<Node>>>();
            var current = root;
            while (pos < text.Length)
            {
                int open = text.IndexOf("{{", pos, StringComparison.Ordinal);
                if (open < 0) { current.Add(new Node { Kind = Kind.Text, Text = text.Substring(pos) }); break; }
                if (open > pos) current.Add(new Node { Kind = Kind.Text, Text = text.Substring(pos, open - pos) });
                int close = text.IndexOf("}}", open + 2, StringComparison.Ordinal);
                if (close < 0) throw new FormatException("A tag is not closed: " + Snippet(text, open));
                var tag = text.Substring(open + 2, close - open - 2);
                pos = close + 2;
                if (tag.Length == 0) continue;
                char c = tag[0];
                var name = tag.Substring(1).Trim();
                switch (c)
                {
                    case '!':
                        break;
                    case '#':
                    case '^':
                    {
                        var n = new Node { Kind = c == '#' ? Kind.Section : Kind.Inverted, Text = name, Children = new List<Node>() };
                        current.Add(n);
                        stack.Push(new KeyValuePair<Node, List<Node>>(n, current));
                        current = n.Children;
                        break;
                    }
                    case '[':
                    {
                        var n = new Node { Kind = Kind.Join, Text = tag.Substring(1), Children = new List<Node>() };
                        current.Add(n);
                        stack.Push(new KeyValuePair<Node, List<Node>>(n, current));
                        current = n.Children;
                        break;
                    }
                    case '/':
                    case ']':
                    {
                        if (stack.Count == 0) throw new FormatException("Nothing to close: {{" + tag + "}} at " + Snippet(text, open));
                        var top = stack.Pop();
                        bool ok = c == ']' ? top.Key.Kind == Kind.Join : top.Key.Kind != Kind.Join && top.Key.Text == name;
                        if (!ok) throw new FormatException("{{" + tag + "}} closes " + (top.Key.Kind == Kind.Join ? "a join" : "{{" + top.Key.Text + "}}") + " at " + Snippet(text, open));
                        current = top.Value;
                        break;
                    }
                    case '|':
                        current.Add(new Node { Kind = Kind.Sep });
                        break;
                    case '>':
                        current.Add(new Node { Kind = Kind.Partial, Text = name });
                        break;
                    default:
                        current.Add(new Node { Kind = Kind.Var, Text = tag.Trim() });
                        break;
                }
            }
            if (stack.Count > 0) throw new FormatException("{{" + (stack.Peek().Key.Kind == Kind.Join ? "[" : "#" + stack.Peek().Key.Text) + "}} is not closed");
            return root;
        }

        private static string Snippet(string text, int at)
        {
            int line = 1;
            for (int i = 0; i < at && i < text.Length; i++) if (text[i] == '\n') line++;
            return "line " + line + ": " + text.Substring(at, Math.Min(60, text.Length - at)).Replace("\n", " ");
        }

        private bool Lookup(List<IDictionary<string, object>> stack, string name, out object value)
        {
            for (int i = stack.Count - 1; i >= 0; i--)
                if (stack[i].TryGetValue(name, out value)) return true;
            value = null;
            return false;
        }

        private static bool Truthy(object v)
        {
            if (v == null) return false;
            if (v is bool) return (bool)v;
            var s = v as string;
            if (s != null) return s.Length > 0;
            var list = v as ICollection;
            if (list != null) return list.Count > 0;
            return true;
        }

        private void RenderNodes(List<Node> nodes, List<IDictionary<string, object>> stack, StringBuilder sb)
        {
            foreach (var n in nodes)
            {
                switch (n.Kind)
                {
                    case Kind.Text:
                        sb.Append(n.Text);
                        break;
                    case Kind.Var:
                    {
                        object v;
                        if (!Lookup(stack, n.Text, out v)) Missing.Add(n.Text);
                        if (v is string) sb.Append((string)v);
                        else if (v != null && !(v is bool) && !(v is IEnumerable)) sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                        break;
                    }
                    case Kind.Section:
                    case Kind.Inverted:
                    {
                        object v;
                        if (!Lookup(stack, n.Text, out v)) Missing.Add(n.Text);
                        bool truthy = Truthy(v);
                        if (n.Kind == Kind.Inverted) { if (!truthy) RenderNodes(n.Children, stack, sb); break; }
                        if (!truthy) break;
                        var items = v as IEnumerable<IDictionary<string, object>>;
                        var one = v as IDictionary<string, object>;
                        if (one != null) items = new[] { one };
                        if (items != null)
                            foreach (var item in items)
                            {
                                stack.Add(item);
                                RenderNodes(n.Children, stack, sb);
                                stack.RemoveAt(stack.Count - 1);
                            }
                        else RenderNodes(n.Children, stack, sb);
                        break;
                    }
                    case Kind.Partial:
                    {
                        List<Node> def;
                        if (_parsedDefs.TryGetValue(n.Text, out def)) RenderNodes(def, stack, sb);
                        else Missing.Add(">" + n.Text);
                        break;
                    }
                    case Kind.Sep:
                        sb.Append(SepMark);
                        break;
                    case Kind.Join:
                    {
                        var inner = new StringBuilder();
                        RenderNodes(n.Children, stack, inner);
                        var parts = inner.ToString().Split(SepMark).Where(p => p.Trim().Length > 0);
                        sb.Append(string.Join(n.Text, parts));
                        break;
                    }
                }
            }
        }
    }
}
