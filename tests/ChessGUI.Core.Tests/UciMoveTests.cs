using ChessGUI.Core.Board;
using ChessGUI.Core.Notation;
using Xunit;

namespace ChessGUI.Core.Tests;

public class UciMoveTests
{
    [Fact]
    public void Parse_SimpleMove()
    {
        var pos = Position.CreateStandard();
        var move = UciMove.Parse(pos, "e2e4");
        Assert.NotNull(move);
        Assert.Equal("e2e4", move!.Value.ToUci());
        Assert.True(move.Value.IsDoublePawnPush);
    }

    [Fact]
    public void Parse_Promotion()
    {
        var pos = Position.FromFen("8/P7/8/8/8/8/8/k6K w - - 0 1");
        var move = UciMove.Parse(pos, "a7a8q");
        Assert.NotNull(move);
        Assert.True(move!.Value.IsPromotion);
        Assert.Equal(PieceType.Queen, move.Value.Promotion);
    }

    [Fact]
    public void Parse_IllegalReturnsNull()
    {
        var pos = Position.CreateStandard();
        Assert.Null(UciMove.Parse(pos, "e2e5")); // piyon üç kare gidemez
        Assert.Null(UciMove.Parse(pos, "zzzz"));
    }

    [Fact]
    public void Parse_RoundTripThroughUci()
    {
        var pos = Position.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        foreach (var legal in Moves.MoveGenerator.GenerateLegal(pos))
        {
            var parsed = UciMove.Parse(pos, legal.ToUci());
            Assert.Equal(legal, parsed);
        }
    }
}
