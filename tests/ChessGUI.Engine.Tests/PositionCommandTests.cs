using ChessGUI.Core.Board;
using ChessGUI.Engine;
using Xunit;

namespace ChessGUI.Engine.Tests;

/// <summary>
/// "position" komutunun kurulumu. Gerçek bir hatadan doğdu: komut yalnızca
/// <c>position fen &lt;güncel&gt;</c> gönderiyordu, hamle listesini değil. Motor kökten
/// önceki pozisyonları göremediği için üç-tekrarı tespit EDEMEZ hâldeydi; kazanılmış
/// bir piyon sonunda tekrara yürüdü, GUI de aynı geçmişi kendisi sayıp beraberlik
/// ilan etti. (Ölçüldü: motor o pozisyonu geçmişle 0, geçmişsiz -667 puanlıyordu.)
/// </summary>
public class PositionCommandTests
{
    [Fact]
    public void CarriesMoveHistory_SoEngineCanSeeRepetition()
    {
        string cmd = UciEngine.BuildPositionCommand(
            Position.StartFen, new[] { "g1f3", "g8f6", "f3g1", "f6g8" });

        // Kritik: "moves" bölümü VAR ve hamleler sırayla.
        Assert.Equal("position startpos moves g1f3 g8f6 f3g1 f6g8", cmd);
    }

    [Fact]
    public void UsesStartposKeyword_ForTheStandardStartFen()
    {
        Assert.Equal("position startpos", UciEngine.BuildPositionCommand(Position.StartFen, null));
    }

    [Fact]
    public void UsesFen_ForCustomStartPosition()
    {
        const string fen = "4k3/8/8/8/8/8/8/4K2R w K - 0 1";
        Assert.Equal($"position fen {fen} moves h1h8",
                     UciEngine.BuildPositionCommand(fen, new[] { "h1h8" }));
    }

    [Fact]
    public void EmptyMoveList_IsTreatedAsNoHistory()
    {
        Assert.Equal("position startpos",
                     UciEngine.BuildPositionCommand(Position.StartFen, new string[0]));
    }
}
