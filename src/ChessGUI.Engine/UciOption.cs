namespace ChessGUI.Engine;

/// <summary>Motorun bildirdiği bir UCI seçeneği (Hash, Threads, MultiPV, SyzygyPath vb.).</summary>
public sealed class UciOption
{
    public required string Name { get; init; }
    public required string Type { get; init; } // check, spin, combo, button, string
    public string? Default { get; init; }
    public long? Min { get; init; }
    public long? Max { get; init; }
    public List<string> Vars { get; } = new();

    /// <summary>"option name Hash type spin default 16 min 1 max 33554432" satırını ayrıştırır.</summary>
    public static UciOption? Parse(string line)
    {
        string[] tok = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tok.Length < 3 || tok[0] != "option" || tok[1] != "name") return null;

        string name = "", type = "", def = "", min = "", max = "";
        var vars = new List<string>();
        string field = "name";

        for (int i = 2; i < tok.Length; i++)
        {
            switch (tok[i])
            {
                case "type": field = "type"; continue;
                case "default": field = "default"; continue;
                case "min": field = "min"; continue;
                case "max": field = "max"; continue;
                case "var": field = "var"; vars.Add(""); continue;
            }

            switch (field)
            {
                case "name": name = Append(name, tok[i]); break;
                case "type": type = tok[i]; break;
                case "default": def = Append(def, tok[i]); break;
                case "min": min = tok[i]; break;
                case "max": max = tok[i]; break;
                case "var": vars[^1] = Append(vars[^1], tok[i]); break;
            }
        }

        return new UciOption
        {
            Name = name,
            Type = type,
            Default = def.Length > 0 ? def : null,
            Min = long.TryParse(min, out long lo) ? lo : null,
            Max = long.TryParse(max, out long hi) ? hi : null
        }.WithVars(vars);
    }

    private UciOption WithVars(List<string> vars) { Vars.AddRange(vars.Where(v => v.Length > 0)); return this; }
    private static string Append(string s, string w) => s.Length == 0 ? w : s + " " + w;
}
