namespace ChessGUI.Data.Import;

/// <summary>İçe aktarma sırasında ilerleme bildirimi.</summary>
public readonly record struct ImportProgress(int GamesImported, int PositionsIndexed, int Skipped, int Duplicates);

/// <summary>İçe aktarma bitince özet.</summary>
public readonly record struct ImportResult(int GamesImported, int PositionsIndexed, int Skipped, int Duplicates, TimeSpan Elapsed)
{
    public double GamesPerSecond => Elapsed.TotalSeconds > 0 ? GamesImported / Elapsed.TotalSeconds : 0;
}

/// <summary>
/// İçe aktarma bir toplu-işlem (batch) sınırından sonra hata ile durduğunda fırlatılır.
/// Önceki toplu işlemler zaten veritabanına <c>Commit</c> edilmiş olduğundan, çağıran taraf
/// bu istisnayı yakalayıp <see cref="PartialResult"/>'taki oyunların gerçekten aktarıldığını
/// kullanıcıya bildirmelidir — aksi halde kısmi başarı tam başarısızlık gibi görünür.
/// </summary>
public sealed class PartialImportException : Exception
{
    public ImportResult PartialResult { get; }

    public PartialImportException(ImportResult partialResult, Exception inner)
        : base($"{partialResult.GamesImported} oyun aktarıldıktan sonra hata: {inner.Message}", inner)
    {
        PartialResult = partialResult;
    }
}

/// <summary>İçe aktarma davranış ayarları.</summary>
public sealed class ImportOptions
{
    /// <summary>Pozisyon araması için her oyunun ana hattındaki Zobrist anahtarlarını indeksle.</summary>
    public bool IndexPositions { get; set; } = true;

    /// <summary>İndekslenecek en fazla yarım hamle (0 = tüm oyun). Büyük veritabanını sınırlamak için.</summary>
    public int MaxIndexPly { get; set; } = 0;

    /// <summary>Kaç oyunda bir transaction işlenip yeni transaction açılacağı.</summary>
    public int BatchSize { get; set; } = 2000;
}
