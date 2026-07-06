using System.Linq;
using ChessGUI.Core.Game;
using ChessGUI.Core.Notation;
using Xunit;

namespace ChessGUI.Core.Tests;

public class PgnTests
{
    private static IEnumerable<string> MainLineSan(GameTree tree)
    {
        for (GameNode? n = tree.Root.MainChild; n != null; n = n.MainChild)
            yield return n.San;
    }

    [Fact]
    public void Parse_SimpleGame_MainLine()
    {
        const string pgn = "1. e4 e5 2. Nf3 Nc6 3. Bb5 a6 1-0";
        var tree = Pgn.Parse(pgn);
        Assert.Equal(new[] { "e4", "e5", "Nf3", "Nc6", "Bb5", "a6" }, MainLineSan(tree).ToArray());
        Assert.Equal("1-0", tree.Tags["Result"]);
    }

    [Fact]
    public void Parse_WithTags()
    {
        const string pgn = "[Event \"Test\"]\n[White \"Kasparov\"]\n[Black \"Karpov\"]\n\n1. d4 d5 *";
        var tree = Pgn.Parse(pgn);
        Assert.Equal("Test", tree.Tags["Event"]);
        Assert.Equal("Kasparov", tree.Tags["White"]);
        Assert.Equal(new[] { "d4", "d5" }, MainLineSan(tree).ToArray());
    }

    [Fact]
    public void Parse_Variation_CreatesSibling()
    {
        // 1.e4 e5 (1...c5 Sicilya) 2.Nf3
        const string pgn = "1. e4 e5 (1... c5 2. Nf3) 2. Nf3 *";
        var tree = Pgn.Parse(pgn);

        GameNode e4 = tree.Root.MainChild!;
        Assert.Equal("e4", e4.San);
        Assert.Equal(2, e4.Children.Count);          // e5 (ana) + c5 (varyant)
        Assert.Equal("e5", e4.Children[0].San);
        Assert.Equal("c5", e4.Children[1].San);
        Assert.Equal("Nf3", e4.Children[1].MainChild!.San); // varyant içinde devam
    }

    [Fact]
    public void Parse_CommentsAndNags()
    {
        const string pgn = "1. e4 {iyi hamle} e5 $1 2. Nf3 *";
        var tree = Pgn.Parse(pgn);
        GameNode e4 = tree.Root.MainChild!;
        Assert.Equal("iyi hamle", e4.Comment);
        GameNode e5 = e4.MainChild!;
        Assert.Contains(1, e5.Nags);
    }

    [Fact]
    public void WriteThenParse_RoundTrip_PreservesMainLine()
    {
        const string pgn = "1. e4 e5 2. Nf3 Nc6 3. Bb5 a6 4. Ba4 Nf6 *";
        var tree = Pgn.Parse(pgn);
        string written = Pgn.Write(tree);
        var reparsed = Pgn.Parse(written);
        Assert.Equal(MainLineSan(tree).ToArray(), MainLineSan(reparsed).ToArray());
    }

    [Fact]
    public void WriteThenParse_RoundTrip_PreservesVariationsAndComments()
    {
        const string pgn = "1. e4 e5 (1... c5 2. Nf3 d6) 2. Nf3 {ana hat} Nc6 *";
        var tree = Pgn.Parse(pgn);
        var reparsed = Pgn.Parse(Pgn.Write(tree));

        GameNode e4 = reparsed.Root.MainChild!;
        Assert.Equal(2, e4.Children.Count);
        Assert.Equal("c5", e4.Children[1].San);
        Assert.Equal("d6", e4.Children[1].MainChild!.MainChild!.San);
        // Yorum "2. Nf3 {ana hat}" ile Nf3 düğümünde: e4 -> e5 -> Nf3.
        Assert.Equal("ana hat", e4.MainChild!.MainChild!.Comment);
    }

    [Fact]
    public void Parse_FromFenTag()
    {
        const string pgn = "[SetUp \"1\"]\n[FEN \"4k3/8/8/8/8/8/4P3/4K3 w - - 0 1\"]\n\n1. e4 *";
        var tree = Pgn.Parse(pgn);
        Assert.StartsWith("4k3", tree.StartFen);
        Assert.Equal("e4", tree.Root.MainChild!.San);
    }
}
