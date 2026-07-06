namespace ChessGUI.App.Controls;

/// <summary>Tahtada çizilecek bir motor önerisi oku (kaynak/hedef kare + öncelik sırası).</summary>
public readonly record struct BoardArrow(int From, int To, int Rank);
