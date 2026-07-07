using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ChessGUI.App.Models;
using ChessGUI.App.Services;
using ChessGUI.Core.Board;
using ChessGUI.Core.Game;
using ChessGUI.Openings;

namespace ChessGUI.App.ViewModels;

/// <summary>Kabuğun gösterdiği görünüm.</summary>
public enum ShellView
{
    Play,
    Analysis,
    Engines,
    Settings
}

/// <summary>
/// Uygulamanın ana kabuk görünüm modeli. Servisleri (EngineRegistry, SettingsService, SoundService)
/// tutar; Oyna (<see cref="PlayBoard"/>) ve Analiz (<see cref="Board"/>) için bağımsız birer
/// <see cref="BoardViewModel"/> kurar — eşitleme ayarı açıkken veya "Analize aktar" ile Oyna'daki
/// oyun Analiz'e PGN üzerinden aktarılır. Üst gezinme barının aktif görünümünü yönetir.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private static readonly EcoClassifier Eco = EcoClassifier.LoadDefault();

    public EngineRegistry EngineRegistry { get; }
    public SettingsService Settings { get; }
    public SoundService Sound { get; }

    public BoardViewModel Board { get; }
    public BoardViewModel PlayBoard { get; }
    public EngineViewModel Engine { get; }
    public MoveListViewModel MoveList { get; }
    public MoveListViewModel PlayMoveList { get; }
    public GameReportViewModel Report { get; }
    public DatabaseViewModel Database { get; }
    public BookViewModel Book { get; }
    public PlayController Play { get; }
    public EngineManagerViewModel EngineManager { get; }
    public SettingsViewModel SettingsVm { get; }
    public GameSetupViewModel Setup { get; }

    [ObservableProperty] private ShellView _activeView = ShellView.Analysis;
    [ObservableProperty] private string _sideToMoveText = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _fen = "";
    [ObservableProperty] private string _openingText = "";

    public ShellViewModel(EngineRegistry engineRegistry, SettingsService settings, SoundService sound, ThemeService theme, UpdateCheckService updateService)
    {
        EngineRegistry = engineRegistry;
        Settings = settings;
        Sound = sound;

        Board = new BoardViewModel();
        PlayBoard = new BoardViewModel();
        Engine = new EngineViewModel(Board, engineRegistry, settings);
        MoveList = new MoveListViewModel(Board);
        PlayMoveList = new MoveListViewModel(PlayBoard);
        Report = new GameReportViewModel(Board, Engine);
        Database = new DatabaseViewModel(Board);
        Book = new BookViewModel(Board, Database);
        Play = new PlayController(PlayBoard, sound, settings);
        EngineManager = new EngineManagerViewModel(engineRegistry);
        SettingsVm = new SettingsViewModel(settings, theme, updateService);
        Setup = new GameSetupViewModel(engineRegistry);

        Play.RequestSwitchToAnalysis += (_, _) => { TransferPlayToAnalysis(); ActiveView = ShellView.Analysis; };
        Setup.StartRequested += OnStartRequested;

        // Oyna tahtası her hamlede değişince, eşitleme ayarı açıksa Analiz tahtasına yansıtılır
        // (bilgisayar-bilgisayar maçını canlı izlemek için).
        PlayBoard.MovePlayed += (_, _) =>
        {
            if (Settings.Current.SyncPlayAndAnalysisBoards) TransferPlayToAnalysis();
        };

        // Ses merkezi olarak burada çalınır: analiz + oyun, insan + motor hamleleri dahil her yerde.
        // Öncelik sırası şah -> alma -> hamle. MovePlayed, TryMove içinde RefreshLegal'dan SONRA
        // tetiklendiği için pozisyon güncel (şah tespiti doğru).
        WireSound(Board);
        WireSound(PlayBoard);

        Board.Changed += (_, _) =>
        {
            UpdateStatus();
            BackCommand.NotifyCanExecuteChanged();
            ForwardCommand.NotifyCanExecuteChanged();
            StartCommand.NotifyCanExecuteChanged();
            EndCommand.NotifyCanExecuteChanged();
        };
        UpdateStatus();
    }

    private void WireSound(BoardViewModel board)
    {
        board.MovePlayed += (_, move) =>
        {
            if (board.Position.IsSideToMoveInCheck()) Sound.Play(SoundKind.Check);
            else if (move.IsCapture) Sound.Play(SoundKind.Capture);
            else Sound.Play(SoundKind.Move);
        };
    }

    /// <summary>Oyna tahtasındaki oyunu (PGN üzerinden) Analiz tahtasına aktarır — özgün başlangıç
    /// FEN'i de PGN'in <c>[FEN]</c> etiketiyle korunur. Oyna'daki oyuna dokunmaz.</summary>
    private void TransferPlayToAnalysis()
    {
        bool wasAtEnd = !Board.CanForward;
        int ply = Board.CurrentNode.Ply;
        Board.LoadPgn(PlayBoard.ExportPgn());
        if (wasAtEnd)
        {
            Board.GoToEnd();
        }
        else
        {
            GameNode node = Board.Tree.Root;
            for (int i = 0; i < ply && node.MainChild is { } next; i++) node = next;
            Board.GoTo(node);
        }
    }

    // --- Navigasyon (üst bar) -----------------------------------------------

    [RelayCommand] private void ShowPlay() => ActiveView = ShellView.Play;
    [RelayCommand] private void ShowAnalysis() => ActiveView = ShellView.Analysis;
    [RelayCommand] private void ShowEngines() => ActiveView = ShellView.Engines;
    [RelayCommand] private void ShowSettings() => ActiveView = ShellView.Settings;

    [RelayCommand]
    private void NewGame()
    {
        Setup.RefreshEngines(EngineRegistry);
        Play.PrepareSetup();
        ActiveView = ShellView.Play;
    }

    private void OnStartRequested()
    {
        PlayerConfig white = Setup.WhiteOption?.ToConfig() ?? PlayerConfig.Human();
        PlayerConfig black = Setup.BlackOption?.ToConfig() ?? PlayerConfig.Human();
        Play.StartGame(white, black, Setup.BuildWhiteTimeControl(), Setup.BuildBlackTimeControl());
    }

    [RelayCommand]
    private void Flip()
    {
        if (ActiveView == ShellView.Play) PlayBoard.Flip();
        else if (ActiveView == ShellView.Analysis) Board.Flip();
    }

    [RelayCommand]
    private void ResetAnalysis() => Board.NewGame();

    [RelayCommand]
    private void LoadEngine()
    {
        var dialog = new OpenFileDialog
        {
            Title = "UCI motoru seç", Filter = "Motor (*.exe)|*.exe|Tüm dosyalar (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
            Engine.AddEngine(dialog.FileName);
    }

    // --- Navigasyon (tahta) --------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanBack))] private void Start() => Board.GoToStart();
    [RelayCommand(CanExecute = nameof(CanBack))] private void Back() => Board.Back();
    [RelayCommand(CanExecute = nameof(CanForward))] private void Forward() => Board.Forward();
    [RelayCommand(CanExecute = nameof(CanForward))] private void End() => Board.GoToEnd();
    private bool CanBack() => Board.CanBack;
    private bool CanForward() => Board.CanForward;

    // --- PGN ------------------------------------------------------------------

    [RelayCommand]
    private void OpenPgn()
    {
        var dialog = new OpenFileDialog { Title = "PGN aç", Filter = "PGN (*.pgn)|*.pgn|Tüm dosyalar (*.*)|*.*" };
        if (dialog.ShowDialog() != true) return;
        TryLoadPgn(File.ReadAllText(dialog.FileName));
    }

    [RelayCommand]
    private void SavePgn()
    {
        var dialog = new SaveFileDialog
        {
            Title = "PGN kaydet", Filter = "PGN (*.pgn)|*.pgn", FileName = "oyun.pgn", DefaultExt = ".pgn"
        };
        if (dialog.ShowDialog() != true) return;
        File.WriteAllText(dialog.FileName, Board.ExportPgn());
        StatusText = "PGN kaydedildi";
    }

    [RelayCommand]
    private void PastePgn()
    {
        if (Clipboard.ContainsText()) TryLoadPgn(Clipboard.GetText());
    }

    [RelayCommand]
    private void CopyPgn()
    {
        Clipboard.SetText(Board.ExportPgn());
        StatusText = "PGN panoya kopyalandı";
    }

    private void TryLoadPgn(string pgn)
    {
        try
        {
            Board.LoadPgn(pgn);
            StatusText = "PGN yüklendi";
        }
        catch (Exception ex)
        {
            StatusText = $"PGN yüklenemedi: {ex.Message}";
        }
    }

    // --- Durum --------------------------------------------------------------

    private void UpdateStatus()
    {
        Position pos = Board.Position;
        SideToMoveText = pos.SideToMove == Color.White ? "Beyaz oynar" : "Siyah oynar";
        Fen = pos.ToFen();

        OpeningInfo? opening = Eco.Classify(Board.Tree, Board.CurrentNode);
        OpeningText = opening != null ? $"{opening.Eco} · {opening.Name}" : "";

        StatusText = GameEnd.Evaluate(pos, Board.Tree.ZobristHistory(Board.CurrentNode)) switch
        {
            GameStatus.Checkmate => pos.SideToMove == Color.White ? "Mat — Siyah kazandı" : "Mat — Beyaz kazandı",
            GameStatus.Stalemate => "Pat — beraberlik",
            GameStatus.FiftyMoveRule => "50 hamle kuralı — beraberlik",
            GameStatus.InsufficientMaterial => "Yetersiz materyal — beraberlik",
            GameStatus.Repetition => "Üç tekrar — beraberlik",
            _ => pos.IsSideToMoveInCheck() ? "Şah!" : "Oyun sürüyor"
        };
    }

    public void Dispose()
    {
        Play.Dispose();
        Engine.Dispose();
        Database.Dispose();
    }
}
