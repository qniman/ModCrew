using System.Text;

namespace ModCrew.Steam;

/// <summary>Reader for Valve's text KeyValues (.vdf / .acf). Values are string or VdfNode.</summary>
public sealed class VdfNode : Dictionary<string, object>
{
    public VdfNode() : base(StringComparer.OrdinalIgnoreCase) { }

    public string? Str(string key) => TryGetValue(key, out var v) ? v as string : null;
    public VdfNode? Node(string key) => TryGetValue(key, out var v) ? v as VdfNode : null;

    public static VdfNode Parse(string text)
    {
        int pos = 0;
        var root = new VdfNode();
        ParseInto(root, text, ref pos, topLevel: true);
        return root;
    }

    static void ParseInto(VdfNode node, string s, ref int pos, bool topLevel)
    {
        while (true)
        {
            var key = NextToken(s, ref pos);
            if (key == null)
            {
                if (topLevel) return;
                throw new FormatException("Unexpected end of VDF");
            }
            if (key == "}")
            {
                if (topLevel) throw new FormatException("Unexpected } in VDF");
                return;
            }
            var value = NextToken(s, ref pos) ?? throw new FormatException("VDF key without value");
            if (value == "{")
            {
                var child = new VdfNode();
                ParseInto(child, s, ref pos, topLevel: false);
                node[key] = child;
            }
            else node[key] = value;
        }
    }

    /// <summary>Returns a token, "{" / "}" for braces, or null at the end. Skips // comments.</summary>
    static string? NextToken(string s, ref int pos)
    {
        while (pos < s.Length)
        {
            char c = s[pos];
            if (char.IsWhiteSpace(c)) { pos++; continue; }
            if (c == '/' && pos + 1 < s.Length && s[pos + 1] == '/')
            {
                while (pos < s.Length && s[pos] != '\n') pos++;
                continue;
            }
            if (c == '{' || c == '}') { pos++; return c.ToString(); }
            if (c == '"')
            {
                pos++;
                var sb = new StringBuilder();
                while (pos < s.Length && s[pos] != '"')
                {
                    if (s[pos] == '\\' && pos + 1 < s.Length)
                    {
                        pos++;
                        sb.Append(s[pos] switch { 'n' => '\n', 't' => '\t', _ => s[pos] });
                    }
                    else sb.Append(s[pos]);
                    pos++;
                }
                pos++; // closing quote
                return sb.ToString();
            }
            int start = pos;
            while (pos < s.Length && !char.IsWhiteSpace(s[pos]) && s[pos] != '{' && s[pos] != '}' && s[pos] != '"') pos++;
            return s[start..pos];
        }
        return null;
    }
}
