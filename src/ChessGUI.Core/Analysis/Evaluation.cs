namespace ChessGUI.Core.Analysis;

/// <summary>
/// Değerlendirme matematiği: santipiyon ↔ kazanma yüzdesi ve hamle doğruluğu (accuracy).
/// Lichess'in yaygın formüllerini kullanır; motordan bağımsız ve test edilebilirdir.
/// </summary>
public static class Evaluation
{
    /// <summary>Santipiyon skorunu (bir tarafın bakışından) kazanma yüzdesine çevirir (0..100).</summary>
    public static double WinPercent(double centipawns)
    {
        double cp = Math.Clamp(centipawns, -1000, 1000);
        return 50.0 + 50.0 * (2.0 / (1.0 + Math.Exp(-0.00368208 * cp)) - 1.0);
    }

    /// <summary>Skoru (cp veya mat) kazanma yüzdesine çevirir. Skor, ilgili tarafın bakışındandır.</summary>
    public static double WinPercentFromScore(int? cp, int? mate)
    {
        if (mate is int m) return m > 0 ? 100.0 : 0.0;
        return WinPercent(cp ?? 0);
    }

    /// <summary>
    /// Bir hamlenin doğruluğu (0..100). Oynayan tarafın hamleden önceki/sonraki kazanma
    /// yüzdelerinden hesaplanır. Skor düşmediyse tam puan.
    /// </summary>
    public static double MoveAccuracy(double winBefore, double winAfter)
    {
        if (winAfter >= winBefore) return 100.0;
        double raw = 103.1668 * Math.Exp(-0.04354 * (winBefore - winAfter)) - 3.1669;
        return Math.Clamp(raw, 0.0, 100.0);
    }

    /// <summary>Beyaz bakışındaki kazanma yüzdesini istenen tarafın bakışına çevirir.</summary>
    public static double OrientToMover(double whiteWinPercent, bool moverIsWhite) =>
        moverIsWhite ? whiteWinPercent : 100.0 - whiteWinPercent;
}
