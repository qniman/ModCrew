using System.Text;

namespace ModCrew.Install;

/// <summary>Minimal INI editing that keeps comments, order and formatting of untouched lines.</summary>
public static class Ini
{
    public static string Set(string text, string section, string key, string value)
    {
        var newline = text.Contains("\r\n") || text.Length == 0 ? "\r\n" : "\n";
        var lines = text.Length == 0 ? new List<string>() : text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);

        string current = "";
        int sectionEnd = section.Length == 0 ? FirstSectionLine(lines) : -1;
        for (int i = 0; i < lines.Count; i++)
        {
            var t = lines[i].Trim();
            if (t.StartsWith('[') && t.EndsWith(']'))
            {
                if (current.Equals(section, StringComparison.OrdinalIgnoreCase) && sectionEnd < 0) sectionEnd = i;
                current = t[1..^1].Trim();
                continue;
            }
            if (!current.Equals(section, StringComparison.OrdinalIgnoreCase)) continue;
            int eq = t.IndexOf('=');
            if (eq <= 0 || t.StartsWith(';') || t.StartsWith('#')) continue;
            if (!t[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            lines[i] = $"{lines[i][..lines[i].IndexOf('=')].TrimEnd()}={value}";
            return Join(lines, newline);
        }

        var entry = $"{key}={value}";
        if (current.Equals(section, StringComparison.OrdinalIgnoreCase) && sectionEnd < 0) sectionEnd = lines.Count;
        if (sectionEnd >= 0)
        {
            // Insert after the last non-empty line of the section.
            int at = sectionEnd;
            while (at > 0 && lines[at - 1].Trim().Length == 0) at--;
            lines.Insert(at, entry);
        }
        else
        {
            if (lines.Count > 0 && lines[^1].Trim().Length > 0) lines.Add("");
            lines.Add($"[{section}]");
            lines.Add(entry);
        }
        return Join(lines, newline);
    }

    static int FirstSectionLine(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var t = lines[i].Trim();
            if (t.StartsWith('[') && t.EndsWith(']')) return i;
        }
        return lines.Count;
    }

    static string Join(List<string> lines, string newline)
    {
        var sb = new StringBuilder();
        foreach (var l in lines) sb.Append(l).Append(newline);
        return sb.ToString();
    }
}
