using ChessGUI.Data.Import;

namespace ChessGUI.Data.Tests;

public class PgnSplitterTests
{
    private const string TwoGames = """
        [Event "A"]
        [White "Alice"]
        [Black "Bob"]
        [Result "1-0"]

        1. e4 e5 2. Nf3 Nc6 1-0

        [Event "B"]
        [White "Carol"]
        [Black "Dan"]
        [Result "0-1"]

        1. d4 d5 0-1
        """;

    [Fact]
    public void Split_SeparatesGames()
    {
        var games = PgnSplitter.Split(new StringReader(TwoGames)).ToList();
        Assert.Equal(2, games.Count);
        Assert.Contains("Alice", games[0]);
        Assert.Contains("Carol", games[1]);
        Assert.DoesNotContain("Carol", games[0]);
    }

    [Fact]
    public void Split_SingleGame_NoTrailingBlank()
    {
        const string one = "[White \"X\"]\n[Black \"Y\"]\n\n1. e4 e5 *\n";
        var games = PgnSplitter.Split(new StringReader(one)).ToList();
        Assert.Single(games);
        Assert.Contains("e4", games[0]);
    }

    [Fact]
    public void Split_HandlesMissingBlankLineBetweenGames()
    {
        const string tight =
            "[White \"A\"]\n1. e4 e5 *\n[White \"B\"]\n1. d4 d5 *\n";
        var games = PgnSplitter.Split(new StringReader(tight)).ToList();
        Assert.Equal(2, games.Count);
    }
}
