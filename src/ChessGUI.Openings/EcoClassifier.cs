using System.Reflection;
using ChessGUI.Core.Board;
using ChessGUI.Core.Game;
using ChessGUI.Core.Moves;
using ChessGUI.Core.Notation;

namespace ChessGUI.Openings;

/// <summary>
/// ECO kodu / açılış adı sınıflandırması. Gömülü açılış tablosunu (<c>Data/eco.txt</c>) hamle
/// dizileri olarak okur ve her pozisyonu kendi Zobrist anahtarına göre indeksler. Bir oyun
/// sınıflandırılırken ana hat baştan taranır ve bulunan en derin (en spesifik) eşleşme döndürülür
/// — bu sayede farklı hamle sıralarıyla aynı pozisyona ulaşan (transpozisyon) oyunlar da doğru
/// tanınır. Dış bir .pgn/.bin dosyasına bağımlı değildir.
/// </summary>
public sealed class EcoClassifier
{
    private readonly Dictionary<ulong, OpeningInfo> _byPosition;

    private EcoClassifier(Dictionary<ulong, OpeningInfo> byPosition) => _byPosition = byPosition;

    /// <summary>Derlemeye gömülü varsayılan açılış tablosunu yükler.</summary>
    public static EcoClassifier LoadDefault()
    {
        Assembly asm = typeof(EcoClassifier).Assembly;
        string resourceName = asm.GetManifestResourceNames()
            .First(n => n.EndsWith("eco.txt", StringComparison.OrdinalIgnoreCase));
        using Stream stream = asm.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return new EcoClassifier(Parse(reader));
    }

    private static Dictionary<ulong, OpeningInfo> Parse(TextReader reader)
    {
        var map = new Dictionary<ulong, OpeningInfo>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            string[] parts = line.Split('|');
            if (parts.Length != 3) continue;

            string eco = parts[0].Trim();
            string name = parts[1].Trim();
            string[] sanMoves = parts[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            Position pos = Position.CreateStandard();
            bool ok = true;
            foreach (string san in sanMoves)
            {
                Move? move = San.Parse(pos, san);
                if (move is null) { ok = false; break; }
                pos.MakeMove(move.Value);
            }
            if (!ok) continue; // hatalı/eski satırı sessizce atla

            map[pos.ZobristKey] = new OpeningInfo(eco, name);
        }
        return map;
    }

    /// <summary>Verilen pozisyon (Zobrist) için doğrudan eşleşen açılış kaydı varsa döndürür.</summary>
    public OpeningInfo? Lookup(ulong positionHash) =>
        _byPosition.TryGetValue(positionHash, out OpeningInfo? info) ? info : null;

    /// <summary>Bir oyun ağacının ana hattını baştan tarar; en derin eşleşen açılışı döndürür.</summary>
    public OpeningInfo? Classify(GameTree tree)
    {
        Position pos = tree.CreateStartPosition();
        OpeningInfo? best = Lookup(pos.ZobristKey);
        for (GameNode? n = tree.Root.MainChild; n != null; n = n.MainChild)
        {
            pos.MakeMove(n.Move);
            if (Lookup(pos.ZobristKey) is { } info) best = info;
        }
        return best;
    }

    /// <summary>
    /// Kökten <paramref name="node"/>'a kadar olan yolu tarar (varyant içindeyse o yolu izler);
    /// en derin eşleşen açılışı döndürür. Kullanıcının şu an baktığı hat için uygundur.
    /// </summary>
    public OpeningInfo? Classify(GameTree tree, GameNode node)
    {
        Position pos = tree.CreateStartPosition();
        OpeningInfo? best = Lookup(pos.ZobristKey);
        foreach (GameNode n in GameTree.PathFromRoot(node))
        {
            if (n.IsRoot) continue;
            pos.MakeMove(n.Move);
            if (Lookup(pos.ZobristKey) is { } info) best = info;
        }
        return best;
    }
}
