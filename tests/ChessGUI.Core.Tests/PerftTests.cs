using ChessGUI.Core.Board;
using ChessGUI.Core.Moves;
using Xunit;

namespace ChessGUI.Core.Tests;

/// <summary>
/// Hamle üretimi + make/unmake doğruluğunu, satranç literatüründeki bilinen
/// perft referans sayılarıyla kanıtlar. Bu testler geçiyorsa çekirdek kurallara
/// tam uyumludur (rok, geçerken alma, terfi, pin, açığa çıkan şah dahil).
/// </summary>
public class PerftTests
{
    [Theory]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 1, 20L)]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 2, 400L)]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 3, 8902L)]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 4, 197281L)]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 5, 4865609L)]
    public void Perft_StartPosition(string fen, int depth, long expected)
    {
        var pos = Position.FromFen(fen);
        Assert.Equal(expected, Perft.Count(pos, depth));
    }

    // Kiwipete — rok, geçerken alma ve pin açısından yoğun klasik test pozisyonu.
    [Theory]
    [InlineData(1, 48L)]
    [InlineData(2, 2039L)]
    [InlineData(3, 97862L)]
    public void Perft_Kiwipete(int depth, long expected)
    {
        var pos = Position.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        Assert.Equal(expected, Perft.Count(pos, depth));
    }

    // Pozisyon 3 — geçerken alma ve kenar durumları.
    [Theory]
    [InlineData(1, 14L)]
    [InlineData(2, 191L)]
    [InlineData(3, 2812L)]
    [InlineData(4, 43238L)]
    [InlineData(5, 674624L)]
    public void Perft_Position3(int depth, long expected)
    {
        var pos = Position.FromFen("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1");
        Assert.Equal(expected, Perft.Count(pos, depth));
    }

    // Pozisyon 4 — terfi ve rok haklarını zorlar (ayna simetrik).
    [Theory]
    [InlineData(1, 6L)]
    [InlineData(2, 264L)]
    [InlineData(3, 9467L)]
    [InlineData(4, 422333L)]
    public void Perft_Position4(int depth, long expected)
    {
        var pos = Position.FromFen("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1");
        Assert.Equal(expected, Perft.Count(pos, depth));
    }

    // Pozisyon 5 ve 6 — ek doğrulama.
    [Theory]
    [InlineData("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 3, 62379L)]
    [InlineData("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10", 3, 89890L)]
    public void Perft_ExtraPositions(string fen, int depth, long expected)
    {
        var pos = Position.FromFen(fen);
        Assert.Equal(expected, Perft.Count(pos, depth));
    }
}
