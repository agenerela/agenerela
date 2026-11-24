using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// How a demo step names a UI element, never by pixel: CSS-like, read left to right, each part a descendant of
    /// the one before.
    /// <list type="bullet">
    /// <item><c>#name</c>: the element's name; <c>.class</c>: a class it has (as many as wanted)</item>
    /// <item><c>'text'</c>: its own text, or any text inside it, contains this (case ignored)</item>
    /// <item><c>:has(selector)</c>: something inside it matches; <c>:nth(2)</c>: the second match, in reading order</item>
    /// </list>
    /// <c>.chronicle .lg:has(.tag.guard) .lg-link</c> is the link of the chronicle's first guarded entry. Hidden
    /// elements, and everything inside them, never match.
    /// </summary>
    public sealed class DemoSelector
    {
        /// <summary>The parts, outermost first.</summary>
        public readonly List<Compound> Parts = new List<Compound>();
        public string Source;

        public sealed class Compound
        {
            public string Name;
            public readonly List<string> Classes = new List<string>();
            public readonly List<string> Texts = new List<string>();
            public readonly List<DemoSelector> Has = new List<DemoSelector>();
            /// <summary>1-based; 0 for every match.</summary>
            public int Nth;
        }

        public static DemoSelector Parse(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) throw new FormatException("An empty selector.");
            int i = 0;
            var s = ParseList(source, ref i, false);
            if (i < source.Length) throw new FormatException($"Selector \"{source}\": unexpected \"{source[i]}\" at {i + 1}.");
            s.Source = source;
            return s;
        }

        private static DemoSelector ParseList(string src, ref int i, bool inner)
        {
            var sel = new DemoSelector();
            while (true)
            {
                while (i < src.Length && src[i] == ' ') i++;
                if (i >= src.Length || inner && src[i] == ')') break;
                sel.Parts.Add(ParseCompound(src, ref i));
            }
            if (sel.Parts.Count == 0) throw new FormatException($"Selector \"{src}\" is empty{(inner ? " inside :has()" : "")}.");
            return sel;
        }

        private static Compound ParseCompound(string src, ref int i)
        {
            var c = new Compound();
            int start = i;
            while (i < src.Length && src[i] != ' ' && src[i] != ')')
            {
                char ch = src[i];
                if (ch == '#') { i++; c.Name = Ident(src, ref i, "a name after #"); }
                else if (ch == '.') { i++; c.Classes.Add(Ident(src, ref i, "a class after .")); }
                else if (ch == '\'')
                {
                    int end = src.IndexOf('\'', i + 1);
                    if (end < 0) throw new FormatException($"Selector \"{src}\": a text in quotes is not closed.");
                    string t = src.Substring(i + 1, end - i - 1);
                    if (t.Length == 0) throw new FormatException($"Selector \"{src}\": an empty text.");
                    c.Texts.Add(t);
                    i = end + 1;
                }
                else if (ch == ':')
                {
                    i++;
                    string fn = Ident(src, ref i, "has or nth after :");
                    if (i >= src.Length || src[i] != '(') throw new FormatException($"Selector \"{src}\": :{fn} needs (...).");
                    i++;
                    if (fn == "has")
                    {
                        c.Has.Add(ParseList(src, ref i, true));
                    }
                    else if (fn == "nth")
                    {
                        int close = src.IndexOf(')', i);
                        if (close < 0 || !int.TryParse(src.Substring(i, close - i), out int n) || n < 1) throw new FormatException($"Selector \"{src}\": :nth takes a number, 1 or more.");
                        c.Nth = n;
                        i = close;
                    }
                    else throw new FormatException($"Selector \"{src}\": :{fn} is not known; :has() and :nth() are.");
                    if (i >= src.Length || src[i] != ')') throw new FormatException($"Selector \"{src}\": :{fn}( is not closed.");
                    i++;
                }
                else throw new FormatException($"Selector \"{src}\": unexpected \"{ch}\" at {i + 1}.");
            }
            if (i == start) throw new FormatException($"Selector \"{src}\": an empty part.");
            if (c.Name == null && c.Classes.Count == 0 && c.Texts.Count == 0 && c.Has.Count == 0) throw new FormatException($"Selector \"{src}\": a part names nothing.");
            return c;
        }

        private static string Ident(string src, ref int i, string what)
        {
            var b = new StringBuilder();
            while (i < src.Length && (char.IsLetterOrDigit(src[i]) || src[i] == '-' || src[i] == '_')) b.Append(src[i++]);
            if (b.Length == 0) throw new FormatException($"Selector \"{src}\": expected {what}.");
            return b.ToString();
        }

        /// <summary>The first visible match under <paramref name="root"/> (not the root itself), in reading order; or default.</summary>
        public T First<T>(T root, IDemoTree<T> tree) where T : class => All(root, tree).FirstOrDefault();

        /// <summary>Every visible match under <paramref name="root"/>, in reading order, each once.</summary>
        public List<T> All<T>(T root, IDemoTree<T> tree) where T : class
        {
            var level = new List<T> { root };
            foreach (var part in Parts)
            {
                var next = new List<T>();
                var seen = new HashSet<T>();
                foreach (var from in level)
                    foreach (var d in Descendants(from, tree))
                        if (Matches(d, part, tree) && seen.Add(d)) next.Add(d);
                if (part.Nth > 0) next = next.Count >= part.Nth ? new List<T> { next[part.Nth - 1] } : new List<T>();
                level = next;
                if (level.Count == 0) break;
            }
            return level;
        }

        private static bool Matches<T>(T n, Compound c, IDemoTree<T> tree) where T : class
        {
            if (c.Name != null && tree.Name(n) != c.Name) return false;
            foreach (var cl in c.Classes) if (!tree.HasClass(n, cl)) return false;
            foreach (var t in c.Texts) if (!SubtreeText(n, tree).Any(x => x != null && x.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)) return false;
            foreach (var h in c.Has) if (h.First(n, tree) == null) return false;
            return true;
        }

        private static IEnumerable<string> SubtreeText<T>(T n, IDemoTree<T> tree) where T : class
        {
            yield return tree.Text(n);
            foreach (var d in Descendants(n, tree)) yield return tree.Text(d);
        }

        /// <summary>Visible descendants in reading order (depth first); a hidden element hides all inside it.</summary>
        private static IEnumerable<T> Descendants<T>(T n, IDemoTree<T> tree) where T : class
        {
            foreach (var c in tree.Children(n))
            {
                if (!tree.Visible(c)) continue;
                yield return c;
                foreach (var d in Descendants(c, tree)) yield return d;
            }
        }

        public override string ToString() => Source ?? string.Join(" ", Parts.Select(p => (p.Name != null ? "#" + p.Name : "") + string.Concat(p.Classes.Select(x => "." + x))));
    }

    /// <summary>What <see cref="DemoSelector"/> reads of a tree: the screen's UI elements, or a test's plain nodes.</summary>
    public interface IDemoTree<T>
    {
        string Name(T node);
        bool HasClass(T node, string cls);
        /// <summary>Its own text (a label's), or null.</summary>
        string Text(T node);
        IEnumerable<T> Children(T node);
        /// <summary>Shown and laid out: displayed, visible, not see-through, with a size.</summary>
        bool Visible(T node);
    }
}
