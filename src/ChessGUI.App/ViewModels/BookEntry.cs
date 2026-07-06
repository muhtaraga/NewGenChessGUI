using ChessGUI.Core.Moves;

namespace ChessGUI.App.ViewModels;

/// <summary>Açılış kitabı panosunda gösterilen tek bir hamle satırı (istatistiklerle).</summary>
public sealed class BookEntry
{
    public string San { get; }
    public Move Move { get; }
    public int Games { get; }
    public double SharePercent { get; }
    public double WhitePercent { get; }
    public double DrawPercent { get; }
    public double BlackPercent { get; }

    public BookEntry(string san, Move move, int games, int totalGames, int whiteWins, int draws, int blackWins)
    {
        San = san;
        Move = move;
        Games = games;
        SharePercent = totalGames > 0 ? 100.0 * games / totalGames : 0;
        WhitePercent = games > 0 ? 100.0 * whiteWins / games : 0;
        DrawPercent = games > 0 ? 100.0 * draws / games : 0;
        BlackPercent = games > 0 ? 100.0 * blackWins / games : 0;
    }

    public string GamesText => $"{Games:N0}";
    public string ShareText => $"{SharePercent:0}%";
    public string ScoreText => $"+{WhitePercent:0} ={DrawPercent:0} -{BlackPercent:0}";
}
