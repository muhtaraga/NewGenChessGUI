using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ChessGUI.App.Models;
using ChessGUI.Core.Board;

namespace ChessGUI.App.ViewModels;

/// <summary>
/// İki taraf için kalan süreyi tutar. <see cref="DispatcherTimer"/> (200ms) ile sıradaki tarafın
/// süresini azaltır; hamle oynandığında artırım ekler ve sırayı çevirir. Süre biterse
/// <see cref="Flagged"/> olayı (bayrak düşen taraf) tetiklenir.
/// </summary>
public sealed partial class ClockViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;
    private DateTime _lastTick;
    private TimeControl _whiteTc = TimeControl.Unlimited();
    private TimeControl _blackTc = TimeControl.Unlimited();

    [ObservableProperty] private long _whiteMs;
    [ObservableProperty] private long _blackMs;
    [ObservableProperty] private Color _activeSide = Color.White;
    [ObservableProperty] private bool _isRunning;

    public string WhiteClockText => Format(WhiteMs);
    public string BlackClockText => Format(BlackMs);

    /// <summary>Süresi biten taraf (kaybeden).</summary>
    public event EventHandler<Color>? Flagged;

    public ClockViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += OnTick;
    }

    /// <summary>Beyaz ve siyah için bağımsız süre kontrolleriyle saatleri kurar.</summary>
    public void Setup(TimeControl whiteTc, TimeControl blackTc, Color sideToMove)
    {
        _whiteTc = whiteTc;
        _blackTc = blackTc;
        WhiteMs = whiteTc.Kind == TimeControlKind.Unlimited ? long.MaxValue : whiteTc.BaseMs;
        BlackMs = blackTc.Kind == TimeControlKind.Unlimited ? long.MaxValue : blackTc.BaseMs;
        ActiveSide = sideToMove;
        OnPropertyChanged(nameof(WhiteClockText));
        OnPropertyChanged(nameof(BlackClockText));
    }

    public void Start()
    {
        if (_whiteTc.Kind == TimeControlKind.Unlimited && _blackTc.Kind == TimeControlKind.Unlimited) return;
        _lastTick = DateTime.UtcNow;
        IsRunning = true;
        _timer.Start();
    }

    public void Pause()
    {
        IsRunning = false;
        _timer.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        DateTime now = DateTime.UtcNow;
        double elapsed = (now - _lastTick).TotalMilliseconds;
        _lastTick = now;

        if (ActiveSide == Color.White)
        {
            if (_whiteTc.Kind == TimeControlKind.Unlimited) return;
            WhiteMs = Math.Max(0, WhiteMs - (long)elapsed);
            OnPropertyChanged(nameof(WhiteClockText));
            if (WhiteMs <= 0) { Pause(); Flagged?.Invoke(this, Color.White); }
        }
        else
        {
            if (_blackTc.Kind == TimeControlKind.Unlimited) return;
            BlackMs = Math.Max(0, BlackMs - (long)elapsed);
            OnPropertyChanged(nameof(BlackClockText));
            if (BlackMs <= 0) { Pause(); Flagged?.Invoke(this, Color.Black); }
        }
    }

    /// <summary>Bir hamle oynandığında çağrılır: artırımı (hareket eden tarafın kendi süre kontrolünden) ekler ve sırayı çevirir.</summary>
    public void OnMovePlayed(Color moverSide)
    {
        if (moverSide == Color.White) { if (_whiteTc.Kind == TimeControlKind.Fischer) WhiteMs += _whiteTc.IncrementMs; }
        else { if (_blackTc.Kind == TimeControlKind.Fischer) BlackMs += _blackTc.IncrementMs; }

        ActiveSide = moverSide == Color.White ? Color.Black : Color.White;
        _lastTick = DateTime.UtcNow;
        OnPropertyChanged(nameof(WhiteClockText));
        OnPropertyChanged(nameof(BlackClockText));
    }

    private static string Format(long ms)
    {
        if (ms == long.MaxValue) return "∞";
        if (ms < 0) ms = 0;
        var ts = TimeSpan.FromMilliseconds(ms);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes:00}:{ts.Seconds:00}";
    }

    public void Dispose() => _timer.Stop();
}
