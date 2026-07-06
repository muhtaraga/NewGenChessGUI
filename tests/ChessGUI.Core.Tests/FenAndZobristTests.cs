using System;
using ChessGUI.Core.Board;
using ChessGUI.Core.Moves;
using Xunit;

namespace ChessGUI.Core.Tests;

public class FenAndZobristTests
{
    [Fact]
    public void Fen_RejectsOverfullRank()
    {
        // 16 taş bir satırda: dizinin dışına taşıp çökmek yerine FormatException fırlatmalı.
        Assert.Throws<FormatException>(() =>
            Position.FromFen("pppppppppppppppp/8/8/8/8/8/8/RNBQKBNR w KQkq - 0 1"));
    }

    [Fact]
    public void Fen_RejectsUnderfullRank()
    {
        // İlk satırda yalnızca 7 kare: tahtayı sessizce kaydırmak yerine FormatException fırlatmalı.
        Assert.Throws<FormatException>(() =>
            Position.FromFen("7/8/8/8/8/8/8/RNBQKBNR w KQkq - 0 1"));
    }

    [Fact]
    public void Fen_RejectsTooFewRanks()
    {
        Assert.Throws<FormatException>(() =>
            Position.FromFen("8/8/8/8/8/8/8 w KQkq - 0 1"));
    }

    [Theory]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1")]
    [InlineData("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1")]
    [InlineData("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1")]
    [InlineData("rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq c6 0 2")]
    public void Fen_RoundTrip(string fen)
    {
        var pos = Position.FromFen(fen);
        Assert.Equal(fen, pos.ToFen());
    }

    [Fact]
    public void Zobrist_MatchesFullRecompute_AfterEveryMove()
    {
        // Perft benzeri gezinti sırasında artımlı anahtar, sıfırdan hesapla ile daima aynı olmalı.
        var pos = Position.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        VerifyKey(pos, 3);
    }

    private static void VerifyKey(Position pos, int depth)
    {
        Assert.Equal(pos.ComputeKey(), pos.ZobristKey);
        if (depth == 0) return;

        Color us = pos.SideToMove;
        foreach (Move m in MoveGenerator.GeneratePseudoLegal(pos))
        {
            var undo = pos.MakeMove(m);
            if (!pos.IsInCheck(us))
                VerifyKey(pos, depth - 1);
            pos.UnmakeMove(m, undo);
        }
    }

    [Fact]
    public void Zobrist_RestoredAfterUnmake()
    {
        var pos = Position.CreateStandard();
        ulong before = pos.ZobristKey;
        foreach (Move m in MoveGenerator.GenerateLegal(pos))
        {
            var undo = pos.MakeMove(m);
            pos.UnmakeMove(m, undo);
            Assert.Equal(before, pos.ZobristKey);
        }
    }

    [Fact]
    public void Transposition_SameKeyViaDifferentOrder()
    {
        // 1.Nf3 Nf6 2.e3 ile 1.e3 Nf6 2.Nf3 aynı pozisyona ulaşır (çift itiş yok -> ep farkı yok).
        // Yarım hamle sayacı sıralamaya göre değişebilir; o Zobrist'e girmez, sadece tahta+sıra+rok+ep karşılaştırılır.
        var a = PlaySan(Position.CreateStandard(), "Nf3", "Nf6", "e3");
        var b = PlaySan(Position.CreateStandard(), "e3", "Nf6", "Nf3");
        Assert.Equal(a.ZobristKey, b.ZobristKey);
        Assert.Equal(BoardPart(a.ToFen()), BoardPart(b.ToFen()));
    }

    // FEN'in ilk 4 alanı: tahta yerleşimi + sıra + rok + geçerken alma (sayaçlar hariç).
    private static string BoardPart(string fen) => string.Join(' ', fen.Split(' ')[..4]);

    private static Position PlaySan(Position pos, params string[] sans)
    {
        foreach (string san in sans)
        {
            var move = Notation.San.Parse(pos, san) ?? throw new Xunit.Sdk.XunitException($"Yasadışı SAN: {san}");
            pos.MakeMove(move);
        }
        return pos;
    }
}
