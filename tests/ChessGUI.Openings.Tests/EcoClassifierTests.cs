using System.Reflection;
using ChessGUI.Core.Board;
using ChessGUI.Core.Game;
using ChessGUI.Core.Notation;
using ChessGUI.Openings;

namespace ChessGUI.Openings.Tests;

public class EcoClassifierTests
{
    /// <summary>
    /// Gömülü eco.txt'teki HER satırın hamle dizisi gerçekten yasal olmalı; aksi halde
    /// EcoClassifier o satırı sessizce atlar ve o açılış hiçbir zaman tanınmaz. Bu test,
    /// veri dosyasındaki bir yazım/yasallık hatasını (San.Parse başarısız olduğunda) yakalar.
    /// </summary>
    [Fact]
    public void AllEcoLines_HaveLegalMoveSequences()
    {
        Assembly asm = typeof(EcoClassifier).Assembly;
        string resourceName = asm.GetManifestResourceNames().Single(n => n.EndsWith("eco.txt"));
        using Stream stream = asm.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);

        int checkedLines = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            string[] parts = line.Split('|');
            Assert.Equal(3, parts.Length);
            string[] sanMoves = parts[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            Position pos = Position.CreateStandard();
            foreach (string san in sanMoves)
            {
                var move = San.Parse(pos, san);
                Assert.True(move.HasValue, $"Yasadışı hamle '{san}' — satır: {line}");
                pos.MakeMove(move!.Value);
            }
            checkedLines++;
        }

        Assert.True(checkedLines > 50, "Beklenenden az açılış satırı bulundu.");
    }

    private static GameTree BuildGame(params string[] sanMoves)
    {
        var tree = new GameTree();
        Position pos = tree.CreateStartPosition();
        GameNode current = tree.Root;
        foreach (string san in sanMoves)
        {
            var move = San.Parse(pos, san) ?? throw new InvalidOperationException($"Yasadışı hamle: {san}");
            current = GameTree.AddMove(current, move, pos);
            pos.MakeMove(move);
        }
        return tree;
    }

    [Fact]
    public void Classify_RuyLopezBerlin_ReturnsExpectedEco()
    {
        var classifier = EcoClassifier.LoadDefault();
        var tree = BuildGame("e4", "e5", "Nf3", "Nc6", "Bb5", "Nf6");
        OpeningInfo? info = classifier.Classify(tree);

        Assert.NotNull(info);
        Assert.Equal("C65", info!.Eco);
        Assert.Contains("Berlin", info.Name);
    }

    [Fact]
    public void Classify_DeepensAsGameProgresses()
    {
        var classifier = EcoClassifier.LoadDefault();

        // Yalnızca 1.e4 e5 -> C20; 1.e4 e5 2.Nf3 Nc6 3.Bb5 -> C60 (daha spesifik).
        Assert.Equal("C20", classifier.Classify(BuildGame("e4", "e5"))!.Eco);
        Assert.Equal("C60", classifier.Classify(BuildGame("e4", "e5", "Nf3", "Nc6", "Bb5"))!.Eco);
    }

    [Fact]
    public void Classify_Transposition_MatchesByPositionNotMoveOrder()
    {
        var classifier = EcoClassifier.LoadDefault();

        // D30: 1.d4 d5 2.c4 e6 — aynı pozisyona İngiliz sırasıyla da ulaşılır: 1.c4 d5 2.d4 e6.
        var direct = BuildGame("d4", "d5", "c4", "e6");
        var transposed = BuildGame("c4", "d5", "d4", "e6");

        OpeningInfo? a = classifier.Classify(direct);
        OpeningInfo? b = classifier.Classify(transposed);

        Assert.NotNull(a);
        Assert.Equal(a!.Eco, b!.Eco);
        Assert.Equal("D30", a.Eco);
    }

    [Fact]
    public void Classify_UnknownPosition_ReturnsNull()
    {
        var classifier = EcoClassifier.LoadDefault();
        // Alışılmadık, tablomuzda olmayan bir hamle sırası.
        var tree = BuildGame("a4", "h5", "a5", "h4");
        Assert.Null(classifier.Classify(tree));
    }
}
