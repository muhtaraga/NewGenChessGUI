using System.Diagnostics;

namespace ChessGUI.Engine;

/// <summary>
/// Tek bir UCI motorunu alt-süreç olarak yönetir: standart giriş/çıkış üzerinden komut gönderir,
/// yanıtları eşzamansız okur ve "info"/"bestmove" olaylarını yayınlar. Olaylar okuyucu iş
/// parçacığında tetiklenir; UI'ya sıçratma (Dispatcher) çağıran katmanın sorumluluğundadır.
/// </summary>
public sealed class UciEngine : IDisposable
{
    private readonly object _gate = new();
    private Process? _process;
    private CancellationTokenSource? _readLoopCts;
    private Task? _readLoopTask;
    private readonly List<UciOption> _options = new();
    private TaskCompletionSource<bool>? _uciOkTcs;
    private TaskCompletionSource<bool>? _readyTcs;

    public string Name { get; private set; } = "UCI Motoru";
    public string? Author { get; private set; }
    public IReadOnlyList<UciOption> Options => _options;
    public bool IsRunning => _process is { HasExited: false };

    public event Action<AnalysisInfo>? InfoReceived;
    public event Action<string>? BestMoveReceived; // UCI hamle metni
    public event Action<string>? LineReceived;       // ham satır (loglama/hata ayıklama)

    /// <summary>Motoru başlatır ve UCI el sıkışmasını (uci → uciok) tamamlar.</summary>
    public async Task StartAsync(string exePath, CancellationToken ct = default)
    {
        if (!File.Exists(exePath))
            throw new FileNotFoundException("Motor çalıştırılabilir dosyası bulunamadı.", exePath);

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory
        };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.Start();
        _readLoopCts = new CancellationTokenSource();
        _readLoopTask = ReadLoopAsync(_process.StandardOutput, _readLoopCts.Token);

        _uciOkTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Send("uci");
        using (ct.Register(() => _uciOkTcs.TrySetCanceled()))
            await _uciOkTcs.Task.ConfigureAwait(false);
    }

    private async Task ReadLoopAsync(StreamReader reader, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line == null) break;
                HandleLine(line);
            }
        }
        catch (OperationCanceledException) { /* normal kapanış */ }
        catch (IOException) { /* süreç sonlandı */ }
        catch (ObjectDisposedException) { /* Dispose() akışı kapattı */ }
    }

    private void HandleLine(string line)
    {
        LineReceived?.Invoke(line);

        if (line == "uciok") { _uciOkTcs?.TrySetResult(true); return; }
        if (line == "readyok") { _readyTcs?.TrySetResult(true); return; }

        if (line.StartsWith("id name ", StringComparison.Ordinal))
            Name = line[8..].Trim();
        else if (line.StartsWith("id author ", StringComparison.Ordinal))
            Author = line[10..].Trim();
        else if (line.StartsWith("option ", StringComparison.Ordinal))
        {
            var opt = UciOption.Parse(line);
            if (opt != null) _options.Add(opt);
        }
        else if (line.StartsWith("info ", StringComparison.Ordinal))
        {
            var info = AnalysisInfo.Parse(line);
            if (info != null) InfoReceived?.Invoke(info);
        }
        else if (line.StartsWith("bestmove ", StringComparison.Ordinal))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) BestMoveReceived?.Invoke(parts[1]);
        }
    }

    public void Send(string command)
    {
        lock (_gate)
        {
            if (_process is { HasExited: false })
                _process.StandardInput.WriteLine(command);
        }
    }

    public void SetOption(string name, string value) => Send($"setoption name {name} value {value}");

    /// <summary>isready gönderir ve readyok gelene kadar bekler (senkronizasyon noktası).</summary>
    public async Task IsReadyAsync(CancellationToken ct = default)
    {
        _readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Send("isready");
        using (ct.Register(() => _readyTcs.TrySetCanceled()))
            await _readyTcs.Task.ConfigureAwait(false);
    }

    public void NewGame() => Send("ucinewgame");

    /// <summary>Verilen FEN pozisyonunu ayarlayıp sonsuz analiz başlatır.</summary>
    public void AnalyzeFen(string fen)
    {
        Send($"position fen {fen}");
        Send("go infinite");
    }

    /// <summary>
    /// Oyun saatine göre en iyi hamleyi arattırır: <c>position fen {fen}</c> + <c>go wtime.. btime.. winc.. binc..</c>.
    /// Motor <see cref="BestMoveReceived"/> olayıyla sonucu bildirir. Oyun oynatma (Play) katmanının ana yolu.
    /// </summary>
    public void Go(string fen, long wtimeMs, long btimeMs, long wincMs, long bincMs)
    {
        Send($"position fen {fen}");
        Send($"go wtime {wtimeMs} btime {btimeMs} winc {wincMs} binc {bincMs}");
    }

    /// <summary>Hamle başına sabit süreyle arattırır (<c>go movetime</c>).</summary>
    public void GoMoveTime(string fen, int ms)
    {
        Send($"position fen {fen}");
        Send($"go movetime {ms}");
    }

    public void Stop() => Send("stop");

    public void Dispose()
    {
        lock (_gate)
        {
            try
            {
                _readLoopCts?.Cancel();
                if (_process is { HasExited: false })
                {
                    Send("quit");
                    if (!_process.WaitForExit(500))
                        _process.Kill(entireProcessTree: true);
                }
                // Okuyucu döngüsü tamamen durana kadar bekle, aksi halde aşağıdaki Dispose()
                // akışları kapatırken hâlâ ReadLineAsync içinde olabilir.
                try { _readLoopTask?.Wait(500); } catch { /* iptal/akış kapanışı bekleniyor */ }
            }
            catch { /* kapanışta yut */ }
            finally { _process?.Dispose(); _process = null; }
        }
    }
}
