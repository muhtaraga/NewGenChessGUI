using ChessGUI.Core.Board;
using ChessGUI.Core.Moves;
using ChessGUI.Core.Notation;

namespace ChessGUI.Core.Game;

/// <summary>
/// Bir oyunun tamamı: PGN etiketleri, başlangıç FEN'i ve hamle ağacı.
/// Ağaç, kökten (başlangıç pozisyonu) dallanan varyantları tutar.
/// </summary>
public sealed class GameTree
{
    public Dictionary<string, string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string StartFen { get; }
    public GameNode Root { get; }

    public GameTree(string startFen = Position.StartFen)
    {
        StartFen = startFen;
        Root = new GameNode { IsRoot = true, Ply = 0 };
    }

    public Position CreateStartPosition() => Position.FromFen(StartFen);

    /// <summary>Kökten <paramref name="node"/>'a kadar olan yol (kök dahil, sırayla).</summary>
    public static IReadOnlyList<GameNode> PathFromRoot(GameNode node)
    {
        var list = new List<GameNode>();
        for (GameNode? n = node; n != null; n = n.Parent) list.Add(n);
        list.Reverse();
        return list;
    }

    /// <summary>Bir düğümdeki pozisyonu, başlangıçtan hamleleri yeniden oynayarak kurar.</summary>
    public Position PositionAt(GameNode node)
    {
        Position pos = CreateStartPosition();
        foreach (GameNode n in PathFromRoot(node))
            if (!n.IsRoot) pos.MakeMove(n.Move);
        return pos;
    }

    /// <summary>
    /// Kökten <paramref name="node"/>'a kadar (o dahil) her pozisyonun Zobrist anahtarı, sırayla.
    /// Üç-tekrar beraberliği tespiti için <see cref="GameEnd.Evaluate"/>'e verilir.
    /// </summary>
    public IReadOnlyList<ulong> ZobristHistory(GameNode node)
    {
        Position pos = CreateStartPosition();
        var keys = new List<ulong> { pos.ZobristKey };
        foreach (GameNode n in PathFromRoot(node))
        {
            if (n.IsRoot) continue;
            pos.MakeMove(n.Move);
            keys.Add(pos.ZobristKey);
        }
        return keys;
    }

    /// <summary>
    /// Kökten <paramref name="node"/>'a kadar oynanan hamleler, UCI metni olarak sırayla.
    /// <see cref="ZobristHistory"/>'nin MOTOR tarafındaki eşidir ve onunla birlikte
    /// kullanılmalıdır: GUI üç-tekrarı Zobrist geçmişinden kendisi sayar, motor ise aynı
    /// bilgiyi yalnızca "position ... moves ..." komutundaki bu listeden görebilir.
    /// Motora salt FEN gönderilirse kökten önceki pozisyonlar motor için YOK hükmündedir;
    /// o zaman motor kazandığı bir pozisyonda tekrarı göremeden beraberliğe yürür ve GUI
    /// onu sayıp beraberlik ilan eder (ölçüldü: aynı pozisyonu geçmişle 0, geçmişsiz
    /// -667 değerlendiriyordu).
    /// </summary>
    public static IReadOnlyList<string> UciMoves(GameNode node)
    {
        var moves = new List<string>();
        foreach (GameNode n in PathFromRoot(node))
            if (!n.IsRoot) moves.Add(n.Move.ToUci());
        return moves;
    }

    /// <summary>
    /// <paramref name="parent"/> düğümüne bir hamle ekler. Aynı hamle zaten varsa mevcut çocuk
    /// döndürülür (giriş transpozisyonu). Aksi halde yeni düğüm eklenir; ilk çocuk yoksa ana hat,
    /// varsa varyant olur. <paramref name="positionBefore"/> hamleden önceki pozisyondur (SAN + numara için).
    /// </summary>
    public static GameNode AddMove(GameNode parent, Move move, Position positionBefore)
    {
        foreach (GameNode child in parent.Children)
            if (child.Move == move) return child;

        var node = new GameNode
        {
            Move = move,
            Parent = parent,
            San = San.ToSan(positionBefore, move),
            WhiteMoved = positionBefore.SideToMove == Color.White,
            MoveNumber = positionBefore.FullmoveNumber,
            Ply = parent.Ply + 1
        };
        parent.Children.Add(node);
        return node;
    }

    /// <summary>Bir düğümü (ve alt ağacını) ebeveyninden siler.</summary>
    public static void Remove(GameNode node)
    {
        node.Parent?.Children.Remove(node);
    }

    /// <summary>Düğümden köke kadar yürüyerek bu hattı (ve içindeki her ata düğümü) ana hat yapar.</summary>
    public static void PromoteToMainLine(GameNode node)
    {
        for (GameNode? n = node; n?.Parent != null; n = n.Parent)
        {
            List<GameNode> siblings = n.Parent.Children;
            if (siblings[0] != n)
            {
                siblings.Remove(n);
                siblings.Insert(0, n);
            }
        }
    }
}
