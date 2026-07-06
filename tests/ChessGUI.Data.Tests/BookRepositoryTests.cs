using System.Linq;
using ChessGUI.Core.Board;
using ChessGUI.Core.Notation;
using ChessGUI.Data;
using ChessGUI.Data.Import;
using ChessGUI.Data.Repositories;

namespace ChessGUI.Data.Tests;

public class BookRepositoryTests
{
    // 1.e4: 3 oyun (2 farklı devam), 1.d4: 1 oyun.
    private const string Games = """
        [White "A"] [Black "B"] [Result "1-0"]
        1. e4 e5 2. Nf3 Nc6 1-0

        [White "C"] [Black "D"] [Result "0-1"]
        1. e4 e5 2. Nf3 Nf6 0-1

        [White "E"] [Black "F"] [Result "1/2-1/2"]
        1. e4 c5 1/2-1/2

        [White "G"] [Black "H"] [Result "1-0"]
        1. d4 d5 1-0
        """;

    private static ChessDatabase ImportFixture()
    {
        var db = ChessDatabase.OpenInMemory();
        new PgnImporter(db).Import(new StringReader(Games), new ImportOptions());
        return db;
    }

    [Fact]
    public void GetMoves_FromStartPosition_AggregatesAllFirstMoves()
    {
        using var db = ImportFixture();
        var repo = new BookRepository(db);

        ulong startHash = Position.CreateStandard().ZobristKey;
        var moves = repo.GetMoves(startHash);

        Assert.Equal(2, moves.Count); // e2e4 ve d2d4
        var e4 = moves.Single(m => m.Uci == "e2e4");
        Assert.Equal(3, e4.Total);
        Assert.Equal(1, e4.WhiteWins);
        Assert.Equal(1, e4.Draws);
        Assert.Equal(1, e4.BlackWins);

        var d4 = moves.Single(m => m.Uci == "d2d4");
        Assert.Equal(1, d4.Total);
        Assert.Equal(1, d4.WhiteWins);
    }

    [Fact]
    public void GetMoves_DeeperPosition_ShowsOnlyContinuationsFromThatLine()
    {
        using var db = ImportFixture();
        var repo = new BookRepository(db);

        Position pos = Position.CreateStandard();
        pos.MakeMove(San.Parse(pos, "e4")!.Value);
        pos.MakeMove(San.Parse(pos, "e5")!.Value);

        var moves = repo.GetMoves(pos.ZobristKey);
        Assert.Single(moves);
        Assert.Equal("g1f3", moves[0].Uci);
        Assert.Equal(2, moves[0].Total); // her iki 2.Nf3 devamı da sayılır
    }

    [Fact]
    public void GetMoves_UnknownPosition_ReturnsEmpty()
    {
        using var db = ImportFixture();
        var repo = new BookRepository(db);
        Assert.Empty(repo.GetMoves(0xDEADBEEFCAFEUL));
    }
}
