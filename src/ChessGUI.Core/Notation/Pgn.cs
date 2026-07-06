using System.Text;
using System.Text.RegularExpressions;
using ChessGUI.Core.Board;
using ChessGUI.Core.Game;

namespace ChessGUI.Core.Notation;

/// <summary>
/// PGN (Portable Game Notation) okuma/yazma. Etiketleri, varyantları (parantezler),
/// NAG'ları ($n) ve yorumları ({...}) destekler.
/// </summary>
public static partial class Pgn
{
    private static readonly string[] Roster = { "Event", "Site", "Date", "Round", "White", "Black", "Result" };

    // --- Ayrıştırma ---------------------------------------------------------

    /// <summary>PGN metnindeki ilk oyunu ağaç olarak ayrıştırır.</summary>
    public static GameTree Parse(string pgn)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int movetextStart = 0;

        foreach (Match m in TagRegex().Matches(pgn))
        {
            tags[m.Groups[1].Value] = Unescape(m.Groups[2].Value);
            movetextStart = m.Index + m.Length;
        }

        string startFen = tags.TryGetValue("FEN", out string? fen) && !string.IsNullOrWhiteSpace(fen)
            ? fen.Trim()
            : Position.StartFen;

        var tree = new GameTree(startFen);
        foreach (var kv in tags) tree.Tags[kv.Key] = kv.Value;

        string movetext = pgn[movetextStart..];
        BuildTree(tree, movetext);
        return tree;
    }

    private static void BuildTree(GameTree tree, string movetext)
    {
        Position curPos = tree.CreateStartPosition();
        GameNode current = tree.Root;
        var stack = new Stack<(GameNode Node, Position Pos)>();
        string? result = null;

        int i = 0, n = movetext.Length;
        while (i < n)
        {
            char c = movetext[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '{')
            {
                int j = movetext.IndexOf('}', i + 1);
                if (j < 0) j = n;
                string comment = movetext[(i + 1)..Math.Min(j, n)].Trim();
                AttachComment(current, comment);
                i = j < n ? j + 1 : n;
                continue;
            }

            if (c == '(')
            {
                // Varyant, current'a götüren hamlenin alternatifidir: dallanma noktası = ebeveyn.
                stack.Push((current, curPos));
                GameNode branch = current.IsRoot ? current : current.Parent!;
                current = branch;
                curPos = tree.PositionAt(branch);
                i++;
                continue;
            }

            if (c == ')')
            {
                if (stack.Count > 0) (current, curPos) = stack.Pop();
                i++;
                continue;
            }

            if (c == '$')
            {
                int j = i + 1;
                while (j < n && char.IsDigit(movetext[j])) j++;
                if (!current.IsRoot && int.TryParse(movetext[(i + 1)..j], out int nag)) current.Nags.Add(nag);
                i = j;
                continue;
            }

            if (c == ';') // satır sonu yorumu
            {
                int j = movetext.IndexOf('\n', i);
                i = j < 0 ? n : j + 1;
                continue;
            }

            // Sözcük belirtecini oku.
            int k = i;
            while (k < n && !char.IsWhiteSpace(movetext[k]) && movetext[k] is not ('{' or '}' or '(' or ')' or ';'))
                k++;
            string tok = movetext[i..k];
            i = k;

            if (IsResult(tok)) { result = tok; continue; }

            string san = StripMoveNumber(tok);
            if (san.Length == 0) continue;

            var move = San.Parse(curPos, san);
            if (move is null) continue; // toleranslı: tanınmayan belirteci atla

            current = GameTree.AddMove(current, move.Value, curPos);
            curPos.MakeMove(move.Value);
        }

        if (result != null && !tree.Tags.ContainsKey("Result"))
            tree.Tags["Result"] = result;
    }

    private static void AttachComment(GameNode node, string comment)
    {
        if (comment.Length == 0) return;
        node.Comment = string.IsNullOrEmpty(node.Comment) ? comment : node.Comment + " " + comment;
    }

    private static bool IsResult(string tok) =>
        tok is "1-0" or "0-1" or "1/2-1/2" or "*";

    private static string StripMoveNumber(string tok)
    {
        int p = 0;
        while (p < tok.Length && char.IsDigit(tok[p])) p++;
        if (p == 0) return tok; // rakamla başlamıyor -> SAN (O-O dahil)

        int d = p;
        while (d < tok.Length && tok[d] == '.') d++;
        if (d > p) return tok[d..]; // "12." / "12...e4" -> numara ön eki
        return tok;                 // "0-0" gibi -> olduğu gibi SAN
    }

    // --- Yazma --------------------------------------------------------------

    public static string Write(GameTree tree)
    {
        var sb = new StringBuilder();

        foreach (string tag in Roster)
            sb.AppendLine($"[{tag} \"{Escape(tree.Tags.GetValueOrDefault(tag, DefaultTag(tag)))}\"]");

        if (tree.StartFen != Position.StartFen)
        {
            sb.AppendLine("[SetUp \"1\"]");
            sb.AppendLine($"[FEN \"{Escape(tree.StartFen)}\"]");
        }

        foreach (var kv in tree.Tags)
            if (!Roster.Contains(kv.Key) && !kv.Key.Equals("FEN", StringComparison.OrdinalIgnoreCase)
                                         && !kv.Key.Equals("SetUp", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"[{kv.Key} \"{Escape(kv.Value)}\"]");

        sb.AppendLine();

        var body = new StringBuilder();
        if (!string.IsNullOrEmpty(tree.Root.Comment)) body.Append($"{{{tree.Root.Comment}}} ");
        WriteMoves(body, tree.Root, forceNumber: true);
        body.Append(tree.Tags.GetValueOrDefault("Result", "*"));

        sb.Append(WrapText(body.ToString(), 80));
        return sb.ToString();
    }

    private static void WriteMoves(StringBuilder sb, GameNode node, bool forceNumber)
    {
        if (!node.HasChildren) return;

        GameNode main = node.Children[0];
        WriteMoveToken(sb, main, forceNumber);
        bool hasVariations = node.Children.Count > 1;

        for (int idx = 1; idx < node.Children.Count; idx++)
        {
            sb.Append("( ");
            WriteMoveToken(sb, node.Children[idx], forceNumber: true);
            WriteMoves(sb, node.Children[idx], forceNumber: false);
            TrimTrailingSpace(sb);
            sb.Append(") ");
        }

        bool forceNext = hasVariations || !string.IsNullOrEmpty(main.Comment) || main.Nags.Count > 0;
        WriteMoves(sb, main, forceNext);
    }

    private static void WriteMoveToken(StringBuilder sb, GameNode node, bool forceNumber)
    {
        if (node.WhiteMoved) sb.Append($"{node.MoveNumber}. ");
        else if (forceNumber) sb.Append($"{node.MoveNumber}... ");

        sb.Append(node.San);
        foreach (int nag in node.Nags) sb.Append($" ${nag}");
        if (!string.IsNullOrEmpty(node.Comment)) sb.Append($" {{{node.Comment}}}");
        sb.Append(' ');
    }

    private static void TrimTrailingSpace(StringBuilder sb)
    {
        while (sb.Length > 0 && sb[^1] == ' ') sb.Length--;
        sb.Append(' ');
    }

    private static string DefaultTag(string tag) => tag switch
    {
        "Date" => "????.??.??",
        "Result" => "*",
        _ => "?"
    };

    private static string WrapText(string text, int width)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        int lineLen = 0;
        foreach (string w in words)
        {
            if (lineLen > 0 && lineLen + 1 + w.Length > width) { sb.Append('\n'); lineLen = 0; }
            else if (lineLen > 0) { sb.Append(' '); lineLen++; }
            sb.Append(w);
            lineLen += w.Length;
        }
        return sb.ToString();
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    private static string Unescape(string s) => s.Replace("\\\"", "\"").Replace("\\\\", "\\");

    [GeneratedRegex("""\[\s*(\w+)\s*"((?:[^"\\]|\\.)*)"\s*\]""")]
    private static partial Regex TagRegex();
}
