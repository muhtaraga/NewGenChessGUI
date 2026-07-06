using ChessGUI.Engine;
using Xunit;

namespace ChessGUI.Engine.Tests;

public class ParsingTests
{
    [Fact]
    public void AnalysisInfo_ParsesFullLine()
    {
        var info = AnalysisInfo.Parse(
            "info depth 20 seldepth 30 multipv 1 score cp 34 nodes 1234567 nps 890123 hashfull 500 time 1387 pv e2e4 e7e5 g1f3");

        Assert.NotNull(info);
        Assert.Equal(20, info!.Depth);
        Assert.Equal(30, info.SelDepth);
        Assert.Equal(1, info.MultiPv);
        Assert.Equal(34, info.ScoreCp);
        Assert.Null(info.ScoreMate);
        Assert.Equal(1234567, info.Nodes);
        Assert.Equal(890123, info.Nps);
        Assert.Equal(new[] { "e2e4", "e7e5", "g1f3" }, info.Pv);
    }

    [Fact]
    public void AnalysisInfo_ParsesTbHits()
    {
        var info = AnalysisInfo.Parse(
            "info depth 30 score cp 0 nodes 500 nps 100 tbhits 42 time 5 pv e2e4");
        Assert.NotNull(info);
        Assert.Equal(42, info!.TbHits);
    }

    [Fact]
    public void AnalysisInfo_ParsesMateScore()
    {
        var info = AnalysisInfo.Parse("info depth 12 multipv 2 score mate -3 pv d1h5 e8e7");
        Assert.NotNull(info);
        Assert.Equal(2, info!.MultiPv);
        Assert.Equal(-3, info.ScoreMate);
        Assert.Null(info.ScoreCp);
    }

    [Fact]
    public void AnalysisInfo_IgnoresStringLines()
    {
        Assert.Null(AnalysisInfo.Parse("info string NNUE evaluation using nn-xxxx.nnue"));
        Assert.Null(AnalysisInfo.Parse("info depth 1 currmove e2e4 currmovenumber 1"));
    }

    [Fact]
    public void UciOption_ParsesSpin()
    {
        var opt = UciOption.Parse("option name Hash type spin default 16 min 1 max 33554432");
        Assert.NotNull(opt);
        Assert.Equal("Hash", opt!.Name);
        Assert.Equal("spin", opt.Type);
        Assert.Equal("16", opt.Default);
        Assert.Equal(1, opt.Min);
        Assert.Equal(33554432, opt.Max);
    }

    [Fact]
    public void UciOption_ParsesComboWithVars()
    {
        var opt = UciOption.Parse("option name Style type combo default Normal var Solid var Normal var Risky");
        Assert.NotNull(opt);
        Assert.Equal("Style", opt!.Name);
        Assert.Equal("combo", opt.Type);
        Assert.Equal("Normal", opt.Default);
        Assert.Equal(new[] { "Solid", "Normal", "Risky" }, opt.Vars);
    }

    [Fact]
    public void UciOption_ParsesMultiWordName()
    {
        var opt = UciOption.Parse("option name Clear Hash type button");
        Assert.NotNull(opt);
        Assert.Equal("Clear Hash", opt!.Name);
        Assert.Equal("button", opt.Type);
    }
}
