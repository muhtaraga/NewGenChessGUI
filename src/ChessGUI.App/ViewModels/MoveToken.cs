using ChessGUI.Core.Game;

namespace ChessGUI.App.ViewModels;

/// <summary>Hamle listesinde tek bir görsel jeton (hamle numarası, hamle, parantez ya da yorum).</summary>
public sealed class MoveToken
{
    public GameNode? Node { get; init; }   // yalnız hamle jetonlarında dolu (tıklanınca gidilir)
    public string Text { get; init; } = "";
    public bool IsMove { get; init; }        // tıklanabilir mi
    public bool IsCurrent { get; init; }     // geçerli düğüm mü (vurgulanır)
    public bool IsComment { get; init; }
    public int Depth { get; init; }          // varyant derinliği (stil için)
    public int Severity { get; init; }        // 0 yok, 1 ?!, 2 ?, 3 ?? (rapor sonrası renklendirme)
}
