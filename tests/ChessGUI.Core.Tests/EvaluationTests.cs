using ChessGUI.Core.Analysis;
using Xunit;

namespace ChessGUI.Core.Tests;

public class EvaluationTests
{
    [Fact]
    public void WinPercent_EqualPosition_Is50()
    {
        Assert.Equal(50.0, Evaluation.WinPercent(0), 3);
    }

    [Fact]
    public void WinPercent_Monotonic_And_Bounded()
    {
        Assert.True(Evaluation.WinPercent(300) > Evaluation.WinPercent(100));
        Assert.True(Evaluation.WinPercent(-300) < Evaluation.WinPercent(-100));
        Assert.InRange(Evaluation.WinPercent(100000), 0, 100);
        Assert.InRange(Evaluation.WinPercent(-100000), 0, 100);
    }

    [Fact]
    public void WinPercentFromScore_Mate()
    {
        Assert.Equal(100.0, Evaluation.WinPercentFromScore(null, 3));
        Assert.Equal(0.0, Evaluation.WinPercentFromScore(null, -2));
    }

    [Fact]
    public void MoveAccuracy_NoDrop_IsPerfect()
    {
        Assert.Equal(100.0, Evaluation.MoveAccuracy(60, 65));
        Assert.Equal(100.0, Evaluation.MoveAccuracy(50, 50));
    }

    [Fact]
    public void MoveAccuracy_LargeDrop_IsLow()
    {
        double acc = Evaluation.MoveAccuracy(80, 30);
        Assert.InRange(acc, 0, 30);
    }

    [Fact]
    public void OrientToMover_FlipsForBlack()
    {
        Assert.Equal(70, Evaluation.OrientToMover(70, true));
        Assert.Equal(30, Evaluation.OrientToMover(70, false));
    }

    [Theory]
    [InlineData(60, 35, false, MoveClassification.Blunder)]   // 25 düşüş
    [InlineData(60, 48, false, MoveClassification.Mistake)]   // 12 düşüş
    [InlineData(60, 54, false, MoveClassification.Inaccuracy)]// 6 düşüş
    [InlineData(60, 59.5, true, MoveClassification.Best)]     // en iyi oynandı
    [InlineData(60, 59, false, MoveClassification.Excellent)] // <2 düşüş
    [InlineData(60, 57, false, MoveClassification.Good)]      // 3 düşüş
    public void Classify_ByDrop(double before, double after, bool best, MoveClassification expected)
    {
        Assert.Equal(expected, MoveClassifier.Classify(before, after, best));
    }
}
