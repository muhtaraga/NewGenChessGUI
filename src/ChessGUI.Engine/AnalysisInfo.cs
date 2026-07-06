namespace ChessGUI.Engine;

/// <summary>
/// Motorun bir "info ..." satırından ayrıştırılan analiz anlık görüntüsü. Skor, sıradaki
/// tarafın bakış açısındandır (UCI standardı); beyaz bakışına normalizasyon üst katmanda yapılır.
/// </summary>
public sealed class AnalysisInfo
{
    public int Depth { get; init; }
    public int SelDepth { get; init; }
    public int MultiPv { get; init; } = 1;

    /// <summary>Santipiyon cinsinden skor (varsa). Mat skoru ile birbirini dışlar.</summary>
    public int? ScoreCp { get; init; }
    /// <summary>Mata kalan hamle (varsa). Pozitif: sıradaki taraf mat ediyor.</summary>
    public int? ScoreMate { get; init; }

    public long Nodes { get; init; }
    public long Nps { get; init; }
    public long TimeMs { get; init; }
    public int HashFull { get; init; }

    /// <summary>Syzygy sonda tablosu (WDL/DTZ) sorgu sayısı; motor SyzygyPath ile yapılandırıldıysa raporlanır.</summary>
    public long TbHits { get; init; }

    /// <summary>Baş varyant, UCI hamle metinleri dizisi (örn. ["e2e4", "e7e5"]).</summary>
    public IReadOnlyList<string> Pv { get; init; } = Array.Empty<string>();

    public bool HasScore => ScoreCp.HasValue || ScoreMate.HasValue;

    /// <summary>"info depth 20 ... score cp 34 ... pv e2e4 e7e5" satırını ayrıştırır.</summary>
    public static AnalysisInfo? Parse(string line)
    {
        string[] t = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (t.Length < 2 || t[0] != "info") return null;

        int depth = 0, selDepth = 0, multipv = 1, hashFull = 0;
        int? cp = null, mate = null;
        long nodes = 0, nps = 0, time = 0, tbHits = 0;
        string[] pv = Array.Empty<string>();

        for (int i = 1; i < t.Length; i++)
        {
            switch (t[i])
            {
                case "depth": depth = NextInt(t, ref i); break;
                case "seldepth": selDepth = NextInt(t, ref i); break;
                case "multipv": multipv = NextInt(t, ref i); break;
                case "nodes": nodes = NextLong(t, ref i); break;
                case "nps": nps = NextLong(t, ref i); break;
                case "time": time = NextLong(t, ref i); break;
                case "hashfull": hashFull = NextInt(t, ref i); break;
                case "tbhits": tbHits = NextLong(t, ref i); break;
                case "score":
                    if (i + 1 < t.Length && t[i + 1] == "cp") { i += 2; cp = ParseInt(t, i); }
                    else if (i + 1 < t.Length && t[i + 1] == "mate") { i += 2; mate = ParseInt(t, i); }
                    break;
                case "pv":
                    pv = t[(i + 1)..];
                    i = t.Length;
                    break;
            }
        }

        // "info string ..." ve "info depth N currmove ..." gibi skorsuz ve PV'siz ilerleme
        // satırlarını atla; bunlar gösterilebilir bir değerlendirme anlık görüntüsü taşımaz.
        if (!cp.HasValue && !mate.HasValue && pv.Length == 0) return null;

        return new AnalysisInfo
        {
            Depth = depth,
            SelDepth = selDepth,
            MultiPv = multipv,
            ScoreCp = cp,
            ScoreMate = mate,
            Nodes = nodes,
            Nps = nps,
            TimeMs = time,
            HashFull = hashFull,
            TbHits = tbHits,
            Pv = pv
        };
    }

    private static int NextInt(string[] t, ref int i) => i + 1 < t.Length ? ParseInt(t, ++i) : 0;
    private static long NextLong(string[] t, ref int i) => i + 1 < t.Length ? ParseLong(t, ++i) : 0;
    private static int ParseInt(string[] t, int i) => int.TryParse(t[i], out int v) ? v : 0;
    private static long ParseLong(string[] t, int i) => long.TryParse(t[i], out long v) ? v : 0;
}
