namespace ChessGUI.Data.Entities;

/// <summary>
/// Bir pozisyondan sonra veritabanında oynanan bir hamlenin istatistiği (açılış kitabı satırı).
/// </summary>
public readonly record struct BookMoveStat(string Uci, int Total, int WhiteWins, int Draws, int BlackWins);
