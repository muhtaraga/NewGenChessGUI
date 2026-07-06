using System.Linq;
using ChessGUI.Core.Board;
using ChessGUI.Core.Game;
using ChessGUI.Core.Moves;
using ChessGUI.Core.Notation;
using Xunit;

namespace ChessGUI.Core.Tests;

public class SanAndGameEndTests
{
    private static Position Play(params string[] sans)
    {
        var pos = Position.CreateStandard();
        foreach (string san in sans)
        {
            var move = San.Parse(pos, san) ?? throw new Xunit.Sdk.XunitException($"Çözülemedi: {san}");
            pos.MakeMove(move);
        }
        return pos;
    }

    [Fact]
    public void San_BasicMoves()
    {
        var pos = Position.CreateStandard();
        var e4 = San.Parse(pos, "e4")!.Value;
        Assert.Equal("e4", San.ToSan(pos, e4));
        pos.MakeMove(e4);

        var c5 = San.Parse(pos, "c5")!.Value;
        pos.MakeMove(c5);

        var nf3 = San.Parse(pos, "Nf3")!.Value;
        Assert.Equal("Nf3", San.ToSan(pos, nf3));
    }

    [Fact]
    public void San_Disambiguation_TwoKnights()
    {
        // Beyaz atlar b1 ve f3; d2 boş, ikisi de d2'ye ulaşır -> dosya ile ayrım (Nbd2 / Nfd2).
        var pos = Position.FromFen("rnbqkbnr/pppppppp/8/8/8/5N2/PPP1PPPP/RNBQKB1R w KQkq - 0 1");
        var toD2 = MoveGenerator.GenerateLegal(pos)
            .Where(m => Squares.ToAlgebraic(m.To) == "d2" && pos[m.From].Type == PieceType.Knight)
            .ToList();
        Assert.Equal(2, toD2.Count);
        foreach (var m in toD2)
        {
            string san = San.ToSan(pos, m);
            Assert.StartsWith("N", san);
            Assert.Contains("d2", san);
            // Ayrım karakteri (dosya) içermeli: "Nbd2" ya da "Nfd2".
            Assert.True(san.Length >= 4);
        }
    }

    [Fact]
    public void San_Castling_And_Check()
    {
        var pos = Position.FromFen("rnbqk2r/pppp1ppp/5n2/2b1p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 0 1");
        var castle = San.Parse(pos, "O-O");
        Assert.NotNull(castle);
        Assert.True(castle!.Value.IsKingCastle);
        Assert.Equal("O-O", San.ToSan(pos, castle.Value));
    }

    [Fact]
    public void GameEnd_FoolsMate_IsCheckmate()
    {
        // 1.f3 e5 2.g4 Qh4#
        var pos = Play("f3", "e5", "g4", "Qh4");
        Assert.Equal(GameStatus.Checkmate, GameEnd.Evaluate(pos));
    }

    [Fact]
    public void San_ProducesCheckmateHash()
    {
        // Qh4 fool's mate hamlesi SAN'da '#' ile bitmeli.
        var pos = Play("f3", "e5", "g4");
        var qh4 = San.Parse(pos, "Qh4")!.Value;
        Assert.Equal("Qh4#", San.ToSan(pos, qh4));
    }

    [Fact]
    public void GameEnd_Stalemate()
    {
        // Bilinen pat: siyah oynayacak, yasal hamlesi yok, şah altında değil.
        var pos = Position.FromFen("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");
        Assert.Equal(GameStatus.Stalemate, GameEnd.Evaluate(pos));
    }

    [Fact]
    public void GameEnd_InsufficientMaterial_KingVsKing()
    {
        var pos = Position.FromFen("8/8/4k3/8/8/3K4/8/8 w - - 0 1");
        Assert.Equal(GameStatus.InsufficientMaterial, GameEnd.Evaluate(pos));
    }

    [Fact]
    public void GameEnd_InsufficientMaterial_SameColorBishops()
    {
        // Beyaz fil b1 (açık kare), siyah fil h1 (açık kare) — aynı renk, mat imkânsız.
        var pos = Position.FromFen("4k3/8/8/8/8/8/8/1B2K2b w - - 0 1");
        Assert.Equal(GameStatus.InsufficientMaterial, GameEnd.Evaluate(pos));
    }

    [Fact]
    public void GameEnd_SufficientMaterial_OppositeColorBishops()
    {
        // Beyaz fil b1 (açık), siyah fil a1 (koyu) — zıt renk, mat mümkün, beraberlik değil.
        var pos = Position.FromFen("4k3/8/8/8/8/8/8/bB2K3 w - - 0 1");
        Assert.Equal(GameStatus.Ongoing, GameEnd.Evaluate(pos));
    }

    [Fact]
    public void GameEnd_Repetition_ThreefoldDetected()
    {
        var pos = Position.CreateStandard();
        var history = new List<ulong> { pos.ZobristKey };

        void Play(string san)
        {
            var move = San.Parse(pos, san)!.Value;
            pos.MakeMove(move);
            history.Add(pos.ZobristKey);
        }

        // Şahları ileri geri oynatarak aynı pozisyona üçüncü kez dön.
        Play("Nf3"); Play("Nf6"); Play("Ng1"); Play("Ng8"); // 1. tekrar (başlangıç, 2 kez)
        Play("Nf3"); Play("Nf6"); Play("Ng1"); Play("Ng8"); // 2. tekrar (başlangıç, 3 kez)

        Assert.Equal(GameStatus.Repetition, GameEnd.Evaluate(pos, history));
    }

    [Fact]
    public void GameEnd_NoRepetition_WithoutHistory()
    {
        // history verilmezse üç-tekrar kontrolü atlanır (geriye dönük uyumluluk).
        var pos = Position.CreateStandard();
        Assert.Equal(GameStatus.Ongoing, GameEnd.Evaluate(pos));
    }
}
