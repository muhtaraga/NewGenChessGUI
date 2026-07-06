using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessGUI.App.Models;
using ChessGUI.App.Services;
using ChessGUI.Core.Board;
using ChessGUI.Engine;

namespace ChessGUI.App.ViewModels;

/// <summary>
/// Analiz panosunun görünüm modeli. Bir <see cref="UciEngine"/> örneğini yönetir, tahta
/// değiştikçe sonsuz analizi yeniden başlatır ve gelen "info" verisini beyaz bakışına
/// normalize ederek MultiPV satırlarına, değerlendirme çubuğuna ve derinlik/NPS göstergesine yansıtır.
/// Motor seçimi artık kalıcı <see cref="EngineRegistry"/> profillerinden gelir.
/// </summary>
public sealed partial class EngineViewModel : ObservableObject, IDisposable
{
    private readonly BoardViewModel _board;
    private readonly EngineRegistry _registry;
    private UciEngine? _engine;
    private string _analyzedFen = Position.StartFen;
    private bool _analyzedWhiteToMove = true;

    /// <summary>Kayıtlı motor profilleri (Motor Yönetimi'nden eklenenler).</summary>
    public ObservableCollection<EngineProfile> Engines => _registry.Engines;
    public ObservableCollection<PvLine> Lines { get; } = new();
    public int[] MultiPvOptions { get; } = { 1, 2, 3, 4, 5 };

    [ObservableProperty] private EngineProfile? _selectedEngine;
    [ObservableProperty] private bool _isAnalyzing;
    [ObservableProperty] private bool _engineLoaded;
    [ObservableProperty] private string _engineName = "Motor yok";
    [ObservableProperty] private string _depthText = "";
    [ObservableProperty] private string _npsText = "";
    [ObservableProperty] private double _whiteWinProbability = 0.5;
    [ObservableProperty] private string _evalCaption = "0.0";
    [ObservableProperty] private bool _whiteFavored = true;
    [ObservableProperty] private int _multiPv = 3;
    [ObservableProperty] private string _syzygyPath = "";
    [ObservableProperty] private bool _supportsSyzygy;
    [ObservableProperty] private string _tbHitsText = "";

    public EngineViewModel(BoardViewModel board, EngineRegistry registry)
    {
        _board = board;
        _registry = registry;
        _board.Changed += (_, _) => RestartIfAnalyzing();
    }

    private static Dispatcher Ui => Application.Current.Dispatcher;

    // --- Motor kaydı / aktifleştirme ---------------------------------------

    /// <summary>Yeni bir motoru diskten kaydeder (Motor Yönetimi ekranında da kullanılabilir) ve aktifleştirir.</summary>
    public async void AddEngine(string path)
    {
        var existing = Engines.FirstOrDefault(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing != null) { SelectedEngine = existing; return; }

        try
        {
            EngineProfile profile = await _registry.AddAsync(path);
            SelectedEngine = profile; // seçim değişimi aktifleştirmeyi tetikler
        }
        catch (Exception ex)
        {
            EngineName = $"Yüklenemedi: {ex.Message}";
        }
    }

    partial void OnSelectedEngineChanged(EngineProfile? value)
    {
        if (value != null) _ = ActivateAsync(value);
    }

    private async Task ActivateAsync(EngineProfile profile)
    {
        bool wasAnalyzing = IsAnalyzing;
        StopInternal();
        _engine?.Dispose();

        UciEngine engine;
        try
        {
            engine = await EngineLauncher.LaunchAsync(profile);
        }
        catch (Exception ex)
        {
            EngineName = $"Yüklenemedi: {ex.Message}";
            EngineLoaded = false;
            return;
        }

        engine.InfoReceived += OnInfo;
        engine.BestMoveReceived += _ => { /* analiz modunda bestmove yok sayılır */ };

        ApplyMultiPvAndSyzygy(engine);

        _engine = engine;
        EngineName = engine.Name;
        EngineLoaded = true;

        if (wasAnalyzing) StartAnalysis();
    }

    private void ApplyMultiPvAndSyzygy(UciEngine engine)
    {
        bool Has(string name) => engine.Options.Any(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
        if (Has("MultiPV")) engine.SetOption("MultiPV", MultiPv.ToString());

        SupportsSyzygy = Has("SyzygyPath");
        if (SupportsSyzygy && SyzygyPath.Length > 0)
            engine.SetOption("SyzygyPath", SyzygyPath);
    }

    /// <summary>Syzygy sonda tablosu klasörünü ayarlar (motor destekliyorsa); değişiklik kalıcıdır, motor değişse de uygulanır.</summary>
    public void SetSyzygyPath(string path)
    {
        SyzygyPath = path;
        if (_engine is { IsRunning: true } && SupportsSyzygy)
        {
            _engine.SetOption("SyzygyPath", path);
            RestartIfAnalyzing();
        }
    }

    [RelayCommand]
    private void BrowseSyzygyPath()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "Syzygy tablebase klasörü seç" };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            SetSyzygyPath(dialog.SelectedPath);
    }

    // --- Analiz kontrolü ----------------------------------------------------

    [RelayCommand]
    private void ToggleAnalysis()
    {
        if (IsAnalyzing) StopAnalysis();
        else StartAnalysis();
    }

    private void StartAnalysis()
    {
        if (_engine is not { IsRunning: true }) return;
        IsAnalyzing = true;
        RestartSearch();
    }

    private void StopAnalysis()
    {
        IsAnalyzing = false;
        _engine?.Stop();
    }

    private void StopInternal()
    {
        IsAnalyzing = false;
        _engine?.Stop();
    }

    /// <summary>Oyun raporu başlarken CPU'yu boşaltmak için etkileşimli analizi durdurur.</summary>
    public void StopForReport() => StopInternal();

    public string? SelectedEnginePath => SelectedEngine?.Path;

    private void RestartIfAnalyzing()
    {
        if (IsAnalyzing) RestartSearch();
    }

    private void RestartSearch()
    {
        if (_engine is not { IsRunning: true }) return;
        _analyzedFen = _board.Position.ToFen();
        _analyzedWhiteToMove = _board.Position.SideToMove == Color.White;
        ResetLines();
        _engine.Stop();
        _engine.AnalyzeFen(_analyzedFen);
    }

    partial void OnMultiPvChanged(int value)
    {
        value = Math.Clamp(value, 1, 8);
        if (_engine is { IsRunning: true } && _engine.Options.Any(o =>
                string.Equals(o.Name, "MultiPV", StringComparison.OrdinalIgnoreCase)))
        {
            _engine.SetOption("MultiPV", value.ToString());
            RestartIfAnalyzing();
        }
    }

    private void ResetLines()
    {
        Ui.Invoke(() =>
        {
            Lines.Clear();
            for (int i = 1; i <= MultiPv; i++)
                Lines.Add(new PvLine { Rank = i });
            TbHitsText = "";
        });
    }

    // --- Motor verisinin işlenmesi -----------------------------------------

    private void OnInfo(AnalysisInfo info)
    {
        // Okuyucu iş parçacığından UI'ya sıçra.
        Ui.BeginInvoke(() => ApplyInfo(info));
    }

    private void ApplyInfo(AnalysisInfo info)
    {
        if (!IsAnalyzing || !info.HasScore) return;

        int idx = info.MultiPv - 1;
        if (idx < 0 || idx >= Lines.Count) return;

        // Skoru beyaz bakışına normalize et.
        bool whitePos = _analyzedWhiteToMove;
        string evalText;
        double prob;
        bool whiteFav;

        if (info.ScoreMate is int mate)
        {
            int wm = whitePos ? mate : -mate;
            evalText = wm > 0 ? $"#{wm}" : $"-#{-wm}";
            prob = wm > 0 ? 1.0 : 0.0;
            whiteFav = wm > 0;
        }
        else
        {
            int wcp = whitePos ? info.ScoreCp!.Value : -info.ScoreCp!.Value;
            evalText = $"{(wcp >= 0 ? "+" : "")}{wcp / 100.0:0.00}";
            prob = 1.0 / (1.0 + Math.Exp(-wcp / 350.0));
            whiteFav = wcp >= 0;
        }

        PvLine line = Lines[idx];
        line.Rank = info.MultiPv;
        line.EvalText = evalText;
        line.Depth = info.Depth;
        line.Moves = PvFormatter.ToSan(_analyzedFen, info.Pv);
        line.HasData = true;

        // Genel göstergeler baş varyanttan (MultiPV 1) beslenir.
        if (info.MultiPv == 1)
        {
            WhiteWinProbability = prob;
            EvalCaption = evalText;
            WhiteFavored = whiteFav;
            DepthText = $"d{info.Depth}" + (info.SelDepth > 0 ? $"/{info.SelDepth}" : "");
            NpsText = FormatNps(info.Nps);
            TbHitsText = info.TbHits > 0 ? $"TB {info.TbHits:N0}" : "";
        }
    }

    private static string FormatNps(long nps) => nps switch
    {
        >= 1_000_000 => $"{nps / 1_000_000.0:0.1} MN/s",
        >= 1_000 => $"{nps / 1_000.0:0} kN/s",
        _ => $"{nps} N/s"
    };

    public void Dispose()
    {
        _engine?.Dispose();
        _engine = null;
    }
}
