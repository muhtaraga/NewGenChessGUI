using System.Collections.Generic;
using ChessGUI.Core.Board;
using ChessGUI.Core.Game;
using ChessGUI.Core.Moves;
using ChessGUI.Core.Notation;
using Xunit;

namespace ChessGUI.Core.Tests;

/// <summary>
/// Motora gönderilen pozisyon geçmişinin testleri.
///
/// Bu dosya gerçek bir hatadan doğdu: GUI motora yalnızca güncel FEN'i gönderiyordu
/// (<c>position fen ...</c>), hamle listesini değil. Motor kökten önceki pozisyonları
/// göremediği için üç-tekrarı tespit edemez hâldeydi; kazanılmış bir piyon sonunda
/// tekrara yürüdü ve GUI — aynı geçmişi kendisi sayarak — beraberlik ilan etti.
/// Ölçüldü: motor o pozisyonu geçmişle 0 (beraberlik), geçmişsiz -667 puanlıyordu.
///
/// Buradaki testlerin işi, GUI'nin bitiş kararında kullandığı geçmiş ile motora
/// gönderilen geçmişin AYNI yolu anlatmaya devam etmesini garantilemek.
/// </summary>
public class EngineHistoryTests
{
    /// <summary>Verilen SAN dizisini ağaca işler; son düğümü döndürür.</summary>
    private static GameNode BuildLine(GameTree tree, params string[] sans)
    {
        Position pos = tree.CreateStartPosition();
        GameNode node = tree.Root;
        foreach (string san in sans)
        {
            Move m = San.Parse(pos, san)
                     ?? throw new Xunit.Sdk.XunitException($"SAN çözülemedi: {san}");
            node = GameTree.AddMove(node, m, pos);
            pos.MakeMove(m);
        }
        return node;
    }

    // At sallanması: Nf3 Nf6 Ng1 Ng8 (x2) -> başlangıç pozisyonu üçüncü kez oluşur.
    private static readonly string[] ThreefoldLine =
        { "Nf3", "Nf6", "Ng1", "Ng8", "Nf3", "Nf6", "Ng1", "Ng8" };

    /// <summary>
    /// ASIL İNVARYANT: <see cref="GameTree.UciMoves"/> (motora giden) ile
    /// <see cref="GameTree.ZobristHistory"/> (GUI'nin beraberlik saydığı) aynı yolu
    /// anlatmalı. UCI listesi baştan oynatıldığında Zobrist zinciri birebir çıkmalı —
    /// yani motor, GUI'nin saydığı pozisyonların TAM OLARAK aynısını görür.
    /// </summary>
    [Fact]
    public void UciMoves_ReplaysToTheSameZobristChainAsHistory()
    {
        var tree = new GameTree();
        GameNode node = BuildLine(tree, ThreefoldLine);

        IReadOnlyList<ulong> keys = tree.ZobristHistory(node);
        IReadOnlyList<string> moves = GameTree.UciMoves(node);

        // ZobristHistory başlangıç pozisyonunu da içerir -> bir fazla eleman.
        Assert.Equal(keys.Count - 1, moves.Count);
        Assert.Equal(ThreefoldLine.Length, moves.Count);

        Position replay = tree.CreateStartPosition();
        Assert.Equal(keys[0], replay.ZobristKey);
        for (int i = 0; i < moves.Count; i++)
        {
            Move m = UciMove.Parse(replay, moves[i])
                     ?? throw new Xunit.Sdk.XunitException($"UCI çözülemedi: {moves[i]}");
            replay.MakeMove(m);
            Assert.Equal(keys[i + 1], replay.ZobristKey);
        }
    }

    /// <summary>
    /// Testin ISIRDIĞININ kanıtı: yukarıdaki hat gerçekten üç-tekrar üretiyor. Bu
    /// olmasaydı ilk test boş bir zinciri doğrulayıp yeşil kalabilirdi.
    /// </summary>
    [Fact]
    public void ThreefoldLine_IsActuallyARepetitionDraw()
    {
        var tree = new GameTree();
        GameNode node = BuildLine(tree, ThreefoldLine);

        Assert.Equal(GameStatus.Repetition,
                     GameEnd.Evaluate(tree.PositionAt(node), tree.ZobristHistory(node)));
    }

    /// <summary>
    /// Özel başlangıç FEN'inden kurulan oyunlarda da hamle listesi o FEN'e göredir;
    /// motora "position fen &lt;StartFen&gt; moves ..." gönderildiğinde pozisyon doğru kurulur.
    /// </summary>
    [Fact]
    public void UciMoves_IsRelativeToStartFen()
    {
        const string fen = "4k3/8/8/8/8/8/8/4K2R w K - 0 1";
        var tree = new GameTree(fen);
        GameNode node = BuildLine(tree, "Rh8+", "Ke7");

        IReadOnlyList<string> moves = GameTree.UciMoves(node);
        Assert.Equal(new[] { "h1h8", "e8e7" }, moves);

        // Baştan oynatınca ağacın o düğümdeki pozisyonuna varılmalı.
        Position replay = Position.FromFen(tree.StartFen);
        foreach (string uci in moves)
            replay.MakeMove(UciMove.Parse(replay, uci)!.Value);
        Assert.Equal(tree.PositionAt(node).ZobristKey, replay.ZobristKey);
    }
}
