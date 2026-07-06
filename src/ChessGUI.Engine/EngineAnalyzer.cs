namespace ChessGUI.Engine;

/// <summary>Tek bir pozisyonun analiz sonucu (skor oynayacak tarafın bakışından).</summary>
public readonly record struct SearchResult(int? ScoreCp, int? ScoreMate, string BestMove, int Depth);

/// <summary>
/// Oyun raporu için pozisyonları sırayla (batch) değerlendiren yardımcı. Kendi UCI alt-sürecini
/// yönetir; böylece etkileşimli analizle çakışmaz. Her pozisyonu sabit derinliğe kadar arar.
/// </summary>
public sealed class EngineAnalyzer : IDisposable
{
    private readonly UciEngine _engine = new();

    public async Task StartAsync(string exePath, int threads, int hashMb, CancellationToken ct = default)
    {
        await _engine.StartAsync(exePath, ct);
        if (_engine.Options.Any(o => o.Name.Equals("Threads", StringComparison.OrdinalIgnoreCase)))
            _engine.SetOption("Threads", Math.Max(1, threads).ToString());
        if (_engine.Options.Any(o => o.Name.Equals("Hash", StringComparison.OrdinalIgnoreCase)))
            _engine.SetOption("Hash", Math.Max(16, hashMb).ToString());
        if (_engine.Options.Any(o => o.Name.Equals("MultiPV", StringComparison.OrdinalIgnoreCase)))
            _engine.SetOption("MultiPV", "1");
        await _engine.IsReadyAsync(ct);
        _engine.NewGame();
    }

    /// <summary>Verilen FEN'i <paramref name="moveTimeMs"/> milisaniye kadar arar ve sonucu döndürür.</summary>
    public async Task<SearchResult> EvaluateAsync(string fen, int moveTimeMs, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<SearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        AnalysisInfo? last = null;

        void OnInfo(AnalysisInfo info) { if (info.HasScore && info.MultiPv == 1) last = info; }
        void OnBest(string move) =>
            tcs.TrySetResult(new SearchResult(last?.ScoreCp, last?.ScoreMate, move, last?.Depth ?? 0));

        _engine.InfoReceived += OnInfo;
        _engine.BestMoveReceived += OnBest;
        try
        {
            _engine.GoMoveTime(fen, moveTimeMs);
            using (ct.Register(() =>
            {
                _engine.Stop();
                tcs.TrySetCanceled();
            }))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            _engine.InfoReceived -= OnInfo;
            _engine.BestMoveReceived -= OnBest;
        }
    }

    public void Dispose() => _engine.Dispose();
}
