using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessGUI.App.Models;
using ChessGUI.App.Services;
using ChessGUI.Core.Board;
using ChessGUI.Core.Game;
using ChessGUI.Core.Notation;
using ChessGUI.Engine;

namespace ChessGUI.App.ViewModels;

/// <summary>
/// Bir oyun oturumunu yönetir: iki oyuncu (insan/motor), beyaz/siyah için bağımsız süre kontrolü,
/// sıra mantığı ve bitiş kontrolü. Kendi bağımsız <see cref="BoardViewModel"/>'i (Oyna tahtası)
/// üzerinde çalışır; Analiz'e aktarım yalnızca "Analize aktar" ile veya eşitleme ayarı açıkken olur.
/// </summary>
public sealed partial class PlayController : ObservableObject, IDisposable
{
    private readonly BoardViewModel _board;
    private readonly SoundService _sound;
    private readonly SettingsService _settings;

    private UciEngine? _whiteEngine;
    private UciEngine? _blackEngine;
    private PlayerConfig _whitePlayer = PlayerConfig.Human();
    private PlayerConfig _blackPlayer = PlayerConfig.Human();
    private TimeControl _whiteTimeControl = TimeControl.Unlimited();
    private TimeControl _blackTimeControl = TimeControl.Unlimited();
    private string? _pendingStartFen;

    // StartGame/PrepareSetup her çağrıldığında artar; bir önceki çağrının motor başlatma
    // görevi (LaunchEnginesAndStartAsync) tamamlandığında hâlâ güncel oyun olup olmadığını
    // anlamak için kullanılır (çift-başlatma yarışını önler).
    private int _gameGeneration;

    public ClockViewModel Clock { get; } = new();
    public PositionEditorViewModel Editor { get; } = new();

    [ObservableProperty] private bool _isGameActive;
    [ObservableProperty] private bool _isFinished;
    [ObservableProperty] private bool _isSetup = true;
    [ObservableProperty] private bool _isEditingPosition;
    [ObservableProperty] private bool _hasPendingCustomPosition;
    [ObservableProperty] private string _resultText = "";
    [ObservableProperty] private string _statusText = "Yeni bir oyun kurun.";
    [ObservableProperty] private string _whitePlayerName = "Beyaz";
    [ObservableProperty] private string _blackPlayerName = "Siyah";

    /// <summary>Oyun bitince (kabuğun Analiz sekmesine geçmesi için) tetiklenir.</summary>
    public event EventHandler? RequestSwitchToAnalysis;

    public PlayController(BoardViewModel board, SoundService sound, SettingsService settings)
    {
        _board = board;
        _sound = sound;
        _settings = settings;
        _board.MovePlayed += OnHumanMovePlayed;
        Clock.Flagged += OnFlagged;
        _board.InputLocked = true; // oyun başlamadan tahtada hamle yapılamasın
    }

    // --- Oyun kurma -----------------------------------------------------------

    public void StartGame(PlayerConfig white, PlayerConfig black, TimeControl whiteTimeControl, TimeControl blackTimeControl)
    {
        StopInternal();

        _whitePlayer = white;
        _blackPlayer = black;
        _whiteTimeControl = whiteTimeControl;
        _blackTimeControl = blackTimeControl;
        WhitePlayerName = white.DisplayName;
        BlackPlayerName = black.DisplayName;

        string startFen = _pendingStartFen ?? Position.StartFen;
        _pendingStartFen = null;
        HasPendingCustomPosition = false;

        _board.LoadGame(new GameTree(startFen));
        _board.Tree.Tags["White"] = white.DisplayName;
        _board.Tree.Tags["Black"] = black.DisplayName;
        _board.Tree.Tags["Result"] = "*";

        // İnsan siyah, rakip bilgisayarsa tahtayı çevir (insan her zaman alttan oynasın).
        bool humanIsBlackOnly = black.Kind == Models.PlayerKind.Human && white.Kind == Models.PlayerKind.Engine;
        if (_settings.Current.AutoFlipBoard && humanIsBlackOnly)
        {
            if (_board.Orientation != Controls.BoardOrientation.BlackBottom) _board.Flip();
        }
        else if (_board.Orientation != Controls.BoardOrientation.WhiteBottom)
        {
            _board.Flip(); // önceki oyunda çevrilmiş olabilir; varsayılana dön
        }

        Clock.Setup(whiteTimeControl, blackTimeControl, _board.Position.SideToMove);
        IsGameActive = true;
        IsFinished = false;
        IsSetup = false;
        ResultText = "";
        StatusText = "Oyun başladı.";
        _board.InputLocked = true; // ilk motor hazır olana kadar / sıraya göre aşağıda açılır

        _ = LaunchEnginesAndStartAsync(_gameGeneration);
    }

    /// <summary>Kurulum panelini göstermeye hazırlar: çalışan oyunu/motoru durdurur ve kurulum durumuna geçer.</summary>
    public void PrepareSetup()
    {
        StopInternal();
        IsGameActive = false;
        IsFinished = false;
        IsSetup = true;
        StatusText = "Oyun kurun.";
        _board.LoadGame(new GameTree(_pendingStartFen ?? Position.StartFen));
    }

    private async Task LaunchEnginesAndStartAsync(int generation)
    {
        UciEngine? white = null, black = null;
        try
        {
            // Her taraf için ayrı bir motor örneği başlatılır (C/C aynı profili kullanıyorsa bile
            // iki bağımsız süreç olur, böylece biri diğerinin arama durumunu etkilemez).
            if (_whitePlayer.Kind == Models.PlayerKind.Engine && _whitePlayer.Engine != null)
                white = await CreateEngineAsync(_whitePlayer.Engine);
            if (_blackPlayer.Kind == Models.PlayerKind.Engine && _blackPlayer.Engine != null)
                black = await CreateEngineAsync(_blackPlayer.Engine);
        }
        catch (Exception ex)
        {
            white?.Dispose();
            if (!ReferenceEquals(black, white)) black?.Dispose();
            if (generation == _gameGeneration)
            {
                StatusText = $"Motor başlatılamadı: {ex.Message}";
                IsGameActive = false;
            }
            return;
        }

        if (generation != _gameGeneration)
        {
            // Bu motorlar başlatılırken StartGame/PrepareSetup tekrar çağrılmış: artık güncel
            // olmayan bu oyunun motorlarını at, mevcut oyunun durumuna karışma.
            white?.Dispose();
            if (!ReferenceEquals(black, white)) black?.Dispose();
            return;
        }

        _whiteEngine = white;
        _blackEngine = black;
        Clock.Start();
        AdvanceTurn();
    }

    private async Task<UciEngine> CreateEngineAsync(EngineProfile profile)
    {
        var engine = await EngineLauncher.LaunchAsync(profile);
        engine.BestMoveReceived += uci => Application.Current.Dispatcher.BeginInvoke(() => OnEngineBestMove(engine, uci));
        return engine;
    }

    // --- Sıra mantığı ----------------------------------------------------------

    private void AdvanceTurn()
    {
        if (!IsGameActive || IsFinished) return;

        GameStatus status = GameEnd.Evaluate(_board.Position, _board.Tree.ZobristHistory(_board.CurrentNode));
        if (status != GameStatus.Ongoing) { FinishGame(status); return; }

        Color side = _board.Position.SideToMove;
        PlayerConfig current = side == Color.White ? _whitePlayer : _blackPlayer;

        if (current.Kind == Models.PlayerKind.Human)
        {
            _board.InputLocked = false;
            StatusText = side == Color.White ? "Sıra beyazda." : "Sıra siyahta.";
        }
        else
        {
            _board.InputLocked = true;
            StatusText = $"{current.DisplayName} düşünüyor…";
            UciEngine? engine = side == Color.White ? _whiteEngine : _blackEngine;
            if (engine is not { IsRunning: true }) return;

            string fen = _board.Position.ToFen();
            TimeControl currentTc = side == Color.White ? _whiteTimeControl : _blackTimeControl;
            if (currentTc.Kind == TimeControlKind.FixedPerMove)
                engine.GoMoveTime(fen, currentTc.MoveTimeMs);
            else if (currentTc.Kind == TimeControlKind.Unlimited)
                engine.Go(fen, long.MaxValue / 4, long.MaxValue / 4, 0, 0);
            else
                engine.Go(fen, Clock.WhiteMs, Clock.BlackMs, _whiteTimeControl.IncrementMs, _blackTimeControl.IncrementMs);
        }
    }

    private void OnEngineBestMove(UciEngine engine, string uci)
    {
        if (!IsGameActive || IsFinished) return;
        // Yalnızca sıradaki taraf bu motora aitse uygula (geç gelen bestmove'ları yok say).
        Color side = _board.Position.SideToMove;
        UciEngine? expected = side == Color.White ? _whiteEngine : _blackEngine;
        if (!ReferenceEquals(engine, expected)) return;

        Core.Moves.Move? move = UciMove.Parse(_board.Position, uci);
        if (move is null) { StatusText = "Motor geçersiz hamle bildirdi."; return; }

        bool wasLocked = _board.InputLocked;
        _board.InputLocked = false;
        _board.TryMove(move.Value.From, move.Value.To, move.Value.IsPromotion ? move.Value.Promotion : Core.Board.PieceType.Queen);
        _board.InputLocked = wasLocked;
    }

    private void OnHumanMovePlayed(object? sender, Core.Moves.Move move)
    {
        if (!IsGameActive || IsFinished) return;

        // Hamleyi oynayan taraf, hamleden önceki sıradaki taraftı (şimdi sıra değişti).
        Color moverSide = _board.Position.SideToMove == Color.White ? Color.Black : Color.White;
        Clock.OnMovePlayed(moverSide);
        AdvanceTurn();
    }

    private void OnFlagged(object? sender, Color loser)
    {
        IsFinished = true;
        IsGameActive = false;
        ResultText = loser == Color.White ? "0-1" : "1-0";
        StatusText = $"Süre bitti — {(loser == Color.White ? "Siyah" : "Beyaz")} kazandı.";
        _board.Tree.Tags["Result"] = ResultText;
        _sound.Play(SoundKind.GameEnd);
        StopEngines();
    }

    private void FinishGame(GameStatus status)
    {
        IsFinished = true;
        IsGameActive = false;
        Clock.Pause();

        Color sideToMove = _board.Position.SideToMove;
        ResultText = status switch
        {
            GameStatus.Checkmate => sideToMove == Color.White ? "0-1" : "1-0",
            _ => "1/2-1/2"
        };
        StatusText = status switch
        {
            GameStatus.Checkmate => sideToMove == Color.White ? "Mat — Siyah kazandı." : "Mat — Beyaz kazandı.",
            GameStatus.Stalemate => "Pat — beraberlik.",
            GameStatus.FiftyMoveRule => "50 hamle kuralı — beraberlik.",
            GameStatus.InsufficientMaterial => "Yetersiz materyal — beraberlik.",
            GameStatus.Repetition => "Üç tekrar — beraberlik.",
            _ => "Oyun bitti."
        };
        _board.Tree.Tags["Result"] = ResultText;
        _sound.Play(SoundKind.GameEnd);
        StopEngines();
    }

    // --- Komutlar ---------------------------------------------------------------

    [RelayCommand]
    private void Resign()
    {
        if (!IsGameActive || IsFinished) return;
        Color resigning = _board.Position.SideToMove;
        IsFinished = true;
        IsGameActive = false;
        Clock.Pause();
        ResultText = resigning == Color.White ? "0-1" : "1-0";
        StatusText = $"{(resigning == Color.White ? "Beyaz" : "Siyah")} terk etti.";
        _board.Tree.Tags["Result"] = ResultText;
        StopEngines();
    }

    [RelayCommand]
    private void OfferDraw()
    {
        if (!IsGameActive || IsFinished) return;
        IsFinished = true;
        IsGameActive = false;
        Clock.Pause();
        ResultText = "1/2-1/2";
        StatusText = "Beraberlik kabul edildi.";
        _board.Tree.Tags["Result"] = ResultText;
        StopEngines();
    }

    [RelayCommand] private void Flip() => _board.Flip();

    /// <summary>Kurulum ekranında pozisyon editörünü mevcut tahtadan tohumlayarak açar.</summary>
    [RelayCommand]
    private void OpenPositionEditor()
    {
        if (IsGameActive) return;
        Editor.LoadFromPosition(_board.Position);
        IsEditingPosition = true;
    }

    [RelayCommand]
    private void ApplyPositionEdit()
    {
        if (!Editor.TryValidate(out string error))
        {
            Editor.ValidationError = error;
            return;
        }
        _pendingStartFen = Editor.BuildFen();
        HasPendingCustomPosition = true;
        IsEditingPosition = false;
        _board.LoadGame(new GameTree(_pendingStartFen)); // kurulum tahtasında hemen göster
    }

    [RelayCommand]
    private void CancelPositionEdit() => IsEditingPosition = false;

    [RelayCommand]
    private void Stop()
    {
        if (!IsGameActive) return;
        IsFinished = true;
        IsGameActive = false;
        Clock.Pause();
        ResultText = _board.Tree.Tags.TryGetValue("Result", out string? r) ? r : "*";
        StatusText = "Oyun durduruldu.";
        StopEngines();
    }

    [RelayCommand]
    private void SwitchToAnalysis() => RequestSwitchToAnalysis?.Invoke(this, EventArgs.Empty);

    private void StopInternal()
    {
        _gameGeneration++; // hâlâ süren bir motor başlatma görevi varsa artık geçersiz say
        IsGameActive = false;
        IsFinished = false;
        Clock.Pause();
        StopEngines();
    }

    private void StopEngines()
    {
        _board.InputLocked = true;
        _whiteEngine?.Stop();
        _blackEngine?.Stop();
        _whiteEngine?.Dispose();
        if (!ReferenceEquals(_blackEngine, _whiteEngine)) _blackEngine?.Dispose();
        _whiteEngine = null;
        _blackEngine = null;
    }

    public void Dispose()
    {
        _board.MovePlayed -= OnHumanMovePlayed;
        Clock.Flagged -= OnFlagged;
        StopEngines();
        Clock.Dispose();
    }
}
