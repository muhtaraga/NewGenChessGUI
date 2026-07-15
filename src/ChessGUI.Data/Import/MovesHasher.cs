using ChessGUI.Core.Game;

namespace ChessGUI.Data.Import;

/// <summary>
/// Bir oyunun ana hat hamlelerinden (oyuncu adı, tarih, sonuç, yorum ve varyantlardan bağımsız)
/// 64-bit bir imza üretir. Veritabanında aynı oyunun (farklı isimlerle kaydedilmiş olsa bile)
/// tekrar eklenmesini engellemek için kullanılır — bkz. <see cref="PgnImporter"/>.
/// </summary>
public static class MovesHasher
{
    public static long Compute(GameTree tree)
    {
        ulong hash = 14695981039346656037; // FNV-1a offset basis
        for (GameNode? n = tree.Root.MainChild; n != null; n = n.MainChild)
        {
            hash = (hash ^ (byte)n.Move.From) * 1099511628211;
            hash = (hash ^ (byte)n.Move.To) * 1099511628211;
            hash = (hash ^ (byte)n.Move.Promotion) * 1099511628211;
        }
        return unchecked((long)hash);
    }
}
