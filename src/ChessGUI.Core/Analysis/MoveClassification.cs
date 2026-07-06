namespace ChessGUI.Core.Analysis;

/// <summary>Bir hamlenin kalite sınıfı (en iyiden en kötüye).</summary>
public enum MoveClassification
{
    Best,        // motorun ilk tercihi
    Excellent,   // neredeyse kayıpsız
    Good,        // makul
    Inaccuracy,  // ?!  küçük kayıp
    Mistake,     // ?   ciddi kayıp
    Blunder      // ??  büyük kayıp
}

public static class MoveClassifier
{
    /// <summary>
    /// Oynayan tarafın kazanma yüzdesindeki düşüşe (winBefore − winAfter) göre sınıflandırır.
    /// <paramref name="playedBest"/> true ise motorun ilk tercihi oynanmıştır.
    /// </summary>
    public static MoveClassification Classify(double winBefore, double winAfter, bool playedBest)
    {
        double drop = winBefore - winAfter;
        if (drop >= 20) return MoveClassification.Blunder;
        if (drop >= 10) return MoveClassification.Mistake;
        if (drop >= 5) return MoveClassification.Inaccuracy;
        if (playedBest) return MoveClassification.Best;
        return drop < 2 ? MoveClassification.Excellent : MoveClassification.Good;
    }

    /// <summary>Hamle listesinde/PGN'de gösterilecek sembol (yalnız hatalar için).</summary>
    public static string Symbol(MoveClassification c) => c switch
    {
        MoveClassification.Inaccuracy => "?!",
        MoveClassification.Mistake => "?",
        MoveClassification.Blunder => "??",
        _ => ""
    };

    /// <summary>0 = sembolsüz, 1 = ?!, 2 = ?, 3 = ?? (renklendirme için önem düzeyi).</summary>
    public static int Severity(MoveClassification c) => c switch
    {
        MoveClassification.Inaccuracy => 1,
        MoveClassification.Mistake => 2,
        MoveClassification.Blunder => 3,
        _ => 0
    };
}
