using System.Linq;
using ChessGUI.Core.Board;
using ChessGUI.Core.Notation;
using ChessGUI.Data;
using ChessGUI.Data.Import;
using ChessGUI.Data.Repositories;

namespace ChessGUI.Data.Tests;

public class ImportAndSearchTests
{
    private const string Games = """
        [Event "Test Open"]
        [White "Alice"]
        [Black "Bob"]
        [Date "2024.01.05"]
        [Result "1-0"]
        [ECO "C40"]

        1. e4 e5 2. Nf3 Nc6 1-0

        [Event "Test Open"]
        [White "Carol"]
        [Black "Dan"]
        [Date "2024.02.10"]
        [Result "0-1"]
        [ECO "B20"]

        1. e4 c5 2. Nf3 d6 0-1
        """;

    private static ChessDatabase ImportFixture()
    {
        var db = ChessDatabase.OpenInMemory();
        var importer = new PgnImporter(db);
        var result = importer.Import(new StringReader(Games), new ImportOptions());
        Assert.Equal(2, result.GamesImported);
        return db;
    }

    [Fact]
    public void Import_StoresGamesAndPlayers()
    {
        using var db = ImportFixture();
        var repo = new GameRepository(db);
        Assert.Equal(2, repo.GameCount());
        Assert.Equal(4, repo.PlayerCount()); // Alice, Bob, Carol, Dan
    }

    [Fact]
    public void Search_ByPlayer_MatchesEitherColor()
    {
        using var db = ImportFixture();
        var repo = new GameRepository(db);
        var hits = repo.Search(new GameQuery { Player = "Carol" });
        Assert.Single(hits);
        Assert.Equal("Carol", hits[0].White);
        Assert.Equal("B20", hits[0].Eco);
    }

    [Fact]
    public void Search_ByEcoPrefix()
    {
        using var db = ImportFixture();
        var repo = new GameRepository(db);
        Assert.Single(repo.Search(new GameQuery { Eco = "B" }));
        Assert.Equal(2, repo.Search(new GameQuery { Result = null }).Count);
    }

    [Fact]
    public void Search_ByPosition_FindsGamesReachingIt()
    {
        using var db = ImportFixture();
        var repo = new GameRepository(db);

        // 1.e4 sonrası pozisyon — her iki oyunda da geçer.
        ulong afterE4 = KeyAfter("e4");
        Assert.Equal(2, repo.Search(new GameQuery { PositionHash = afterE4 }).Count);

        // 1.e4 c5 sonrası pozisyon — yalnızca ikinci oyunda.
        ulong afterC5 = KeyAfter("e4", "c5");
        var sicilian = repo.Search(new GameQuery { PositionHash = afterC5 });
        Assert.Single(sicilian);
        Assert.Equal("Carol", sicilian[0].White);
        Assert.Equal(2, sicilian[0].MatchPly); // yarım hamle sayısı
    }

    [Fact]
    public void LoadPgn_RoundTripsThroughParser()
    {
        using var db = ImportFixture();
        var repo = new GameRepository(db);
        long id = repo.Search(new GameQuery { White = "Alice" })[0].Id;

        string? pgn = repo.LoadPgn(id);
        Assert.NotNull(pgn);
        var tree = Pgn.Parse(pgn!);
        Assert.Equal("Nf3", tree.Root.MainChild!.MainChild!.MainChild!.San);
    }

    [Fact]
    public void Import_AutoFillsMissingEcoTag()
    {
        const string noEcoTag = """
            [Event "Casual"]
            [White "Eve"]
            [Black "Frank"]
            [Result "1-0"]

            1. e4 e5 2. Nf3 Nc6 3. Bb5 1-0
            """;

        using var db = ChessDatabase.OpenInMemory();
        var result = new PgnImporter(db).Import(new StringReader(noEcoTag), new ImportOptions());
        Assert.Equal(1, result.GamesImported);

        var repo = new GameRepository(db);
        var hit = repo.Search(new GameQuery { White = "Eve" }).Single();
        Assert.Equal("C60", hit.Eco); // Ruy Lopez — PGN'de ECO etiketi yoktu, sınıflandırıcı doldurdu
    }

    [Fact]
    public void Import_SkipsGameWithSameMoves_EvenWithDifferentPlayerNames()
    {
        using var db = ImportFixture(); // Alice-Bob 1.e4 e5 2.Nf3 Nc6, Carol-Dan 1.e4 c5 2.Nf3 d6

        const string sameMovesDifferentNames = """
            [Event "Rematch"]
            [White "Xavier"]
            [Black "Yolanda"]
            [Date "2025.01.01"]
            [Result "1-0"]

            1. e4 e5 2. Nf3 Nc6 1-0
            """;

        var result = new PgnImporter(db).Import(new StringReader(sameMovesDifferentNames), new ImportOptions());
        Assert.Equal(0, result.GamesImported);
        Assert.Equal(1, result.Duplicates);

        var repo = new GameRepository(db);
        Assert.Equal(2, repo.GameCount()); // yeni oyun eklenmedi
    }

    [Fact]
    public void Import_SkipsDuplicateWithinSameFile()
    {
        const string sameGameTwice = """
            [White "A"]
            [Black "B"]
            [Result "1-0"]

            1. e4 e5 2. Nf3 Nc6 1-0

            [White "C"]
            [Black "D"]
            [Result "1-0"]

            1. e4 e5 2. Nf3 Nc6 1-0
            """;

        using var db = ChessDatabase.OpenInMemory();
        var result = new PgnImporter(db).Import(new StringReader(sameGameTwice), new ImportOptions());
        Assert.Equal(1, result.GamesImported);
        Assert.Equal(1, result.Duplicates);
    }

    [Fact]
    public void DeleteGames_RemovesGameAndItsPositionIndex()
    {
        using var db = ImportFixture();
        var repo = new GameRepository(db);
        long id = repo.Search(new GameQuery { White = "Alice" })[0].Id;

        repo.DeleteGames(new[] { id });

        Assert.Equal(1, repo.GameCount());
        Assert.Empty(repo.Search(new GameQuery { White = "Alice" }));
        ulong afterE4 = KeyAfter("e4");
        Assert.Single(repo.Search(new GameQuery { PositionHash = afterE4 })); // yalnızca kalan oyun eşleşir
    }

    private static ulong KeyAfter(params string[] sanMoves)
    {
        Position pos = Position.CreateStandard();
        foreach (string san in sanMoves)
            pos.MakeMove(San.Parse(pos, san)!.Value);
        return pos.ZobristKey;
    }
}
