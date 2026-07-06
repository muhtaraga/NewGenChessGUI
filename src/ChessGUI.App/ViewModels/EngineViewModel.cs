using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessGUI.App.Controls;
using ChessGUI.App.Models;
using ChessGUI.App.Services;
using ChessGUI.Core.Board;
using ChessGUI.Core.Moves;
using ChessGUI.Core.Notation;
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
    private readonly SettingsService _settings;
    private UciEngine? _engine;
    private string _analyzedFen = Position.StartFen;
    private bool _analyzedWhiteToMove = true;
    private bool _suppressMultiPvPersist;

    /// <summary>Kayıtlı motor profilleri (Motor Yönetimi'nden eklenenler).</summary>
    public ObservableCollection<EngineProfile> Engines => _registry.Engines;
    public ObservableCollection<PvLine> Lines { get; } = new();
    private static readonly int[] AllMultiPvOptions = { 1, 2, 3, 4, 5 };

    /// <summary>Sabit liste — motora göre yeniden atanmaz (bkz. <see cref="ApplyMultiPvAndSyzygy"/>).</summary>
    public int[] MultiPvOptions => AllMultiPvOptions;

    [ObservableProperty] private IReadOnlyList<BoardArrow> _arrows = Array.Empty<BoardArrow>();

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

    public EngineViewModel(BoardViewModel board, EngineRegistry registry, SettingsService settings)
    {
        _board = board;
        _registry = registry;
        _settings = settings;
        _board.Changed += (_, _) => RestartIfAnalyzing();

        // Kullanıcının son seçtiği PV derinliği kalıcıdır (motorun desteklediği aralığa göre
        // sonradan ApplyMultiPvAndSyzygy içinde sınırlanır).
        MultiPv = Math.Clamp(settings.Current.MultiPv, 1, AllMultiPvOptions[^1]);
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

        ApplyMultiPv(engine);

        SupportsSyzygy = Has("SyzygyPath");
        if (SupportsSyzygy && SyzygyPath.Length > 0)
            engine.SetOption("SyzygyPath", SyzygyPath);
    }

    /// <summary>
    /// MultiPV seçeneğini motora uygular (üst sınırı aşarsa kırpar). UCI motorları "go" sırasında
    /// gelen setoption'ları yok sayabildiği için bu SADECE motor arama yapmıyorken çağrılmalı
    /// (motor aktivasyonunda hemen, ya da <see cref="RestartSearch"/> içinde stop'tan SONRA).
    /// NOT: MultiPvOptions (ComboBox ItemsSource) kasıtlı olarak burada değiştirilmiyor — her
    /// çağrıda yeni bir dizi atamak WPF'in ComboBox'ı ilk öğeye sıfırlamasına yol açıyordu.
    /// </summary>
    private void ApplyMultiPv(UciEngine engine)
    {
        UciOption? multiPvOption = engine.Options.FirstOrDefault(o =>
            string.Equals(o.Name, "MultiPV", StringComparison.OrdinalIgnoreCase));
        if (multiPvOption == null) return;

        int engineMax = multiPvOption.Max is long max && max > 0
            ? (int)Math.Min(max, AllMultiPvOptions[^1])
            : AllMultiPvOptions[^1];
        if (MultiPv > engineMax)
        {
            // Bu motorun ürettiği geçici bir sınırlama; kullanıcının kalıcı tercihini ezmesin.
            _suppressMultiPvPersist = true;
            MultiPv = engineMax;
            _suppressMultiPvPersist = false;
        }
        engine.SetOption("MultiPV", Math.Min(MultiPv, engineMax).ToString());
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
        // MultiPV, motor arama yapmadığı bu an güvenle (yeniden) uygulanır — "go" sırasında
        // gönderilen setoption'ları bazı motorlar sessizce yok sayar.
        ApplyMultiPv(_engine);
        _engine.AnalyzeFen(_analyzedFen);
    }

    partial void OnMultiPvChanged(int value)
    {
        if (!_suppressMultiPvPersist)
        {
            _settings.Current.MultiPv = value;
            _settings.Save();
        }

        RestartIfAnalyzing();
    }

    private void ResetLines()
    {
        Ui.Invoke(() =>
        {
            Lines.Clear();
            for (int i = 1; i <= MultiPv; i++)
                Lines.Add(new PvLine { Rank = i });
            Arrows = Array.Empty<BoardArrow>();
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

        Move? firstMove = null;
        if (info.Pv.Count > 0)
        {
            try
            {
                Position pos = Position.FromFen(_analyzedFen);
                firstMove = UciMove.Parse(pos, info.Pv[0]);
            }
            catch { /* bozuk FEN -> ok çizilmez */ }
        }

        PvLine line = Lines[idx];
        line.Rank = info.MultiPv;
        line.EvalText = evalText;
        line.Depth = info.Depth;
        line.Moves = PvFormatter.ToSan(_analyzedFen, info.Pv);
        line.FromSquare = firstMove?.From ?? Squares.None;
        line.ToSquare = firstMove?.To ?? Squares.None;
        line.HasData = true;

        RebuildArrows();

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

    private void RebuildArrows()
    {
        Arrows = Lines
            .Where(l => l.HasData && l.FromSquare != Squares.None)
            .OrderBy(l => l.Rank)
            .Select(l => new BoardArrow(l.FromSquare, l.ToSquare, l.Rank))
            .ToArray();
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
