using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessGUI.App.Models;
using ChessGUI.App.Services;

namespace ChessGUI.App.ViewModels;

/// <summary>
/// Ayarlar ekranının görünüm modeli. <see cref="AppSettings"/> alanlarını düzenler ve
/// <see cref="SettingsService.Save"/> ile diske yazar. Değişiklikler canlı yansır (tahta renkleri,
/// ses, animasyon, oto-çevir, Oyna/Analiz tahta senkronu).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _service;
    private readonly ThemeService _themeService;

    [ObservableProperty] private string _theme;
    [ObservableProperty] private string _lightSquareColor;
    [ObservableProperty] private string _darkSquareColor;
    [ObservableProperty] private string _accentColor;
    [ObservableProperty] private bool _showCoordinates;
    [ObservableProperty] private double _pieceScale;
    [ObservableProperty] private bool _soundEnabled;
    [ObservableProperty] private bool _animationEnabled;
    [ObservableProperty] private int _animationDurationMs;
    [ObservableProperty] private bool _autoFlipBoard;
    [ObservableProperty] private bool _syncPlayAndAnalysisBoards;
    [ObservableProperty] private bool _showEngineArrows;
    [ObservableProperty] private string _statusText = "";

    /// <summary>Ayar değiştikçe canlı uygulanması için (BoardControl binding'i dinler).</summary>
    public event EventHandler? LiveChanged;

    public SettingsViewModel(SettingsService service, ThemeService themeService)
    {
        _service = service;
        _themeService = themeService;
        AppSettings s = service.Current;

        _theme = s.Theme;
        _lightSquareColor = s.LightSquareColor;
        _darkSquareColor = s.DarkSquareColor;
        _accentColor = s.AccentColor;
        _showCoordinates = s.ShowCoordinates;
        _pieceScale = s.PieceScale;
        _soundEnabled = s.SoundEnabled;
        _animationEnabled = s.AnimationEnabled;
        _animationDurationMs = s.AnimationDurationMs;
        _autoFlipBoard = s.AutoFlipBoard;
        _syncPlayAndAnalysisBoards = s.SyncPlayAndAnalysisBoards;
        _showEngineArrows = s.ShowEngineArrows;
    }

    partial void OnThemeChanged(string value)
    {
        _themeService.Apply(value);
        Apply();
    }
    partial void OnLightSquareColorChanged(string value) => Apply();
    partial void OnDarkSquareColorChanged(string value) => Apply();
    partial void OnAccentColorChanged(string value) => Apply();
    partial void OnShowCoordinatesChanged(bool value) => Apply();
    partial void OnPieceScaleChanged(double value) => Apply();
    partial void OnSoundEnabledChanged(bool value) => Apply();
    partial void OnAnimationEnabledChanged(bool value) => Apply();
    partial void OnAnimationDurationMsChanged(int value) => Apply();
    partial void OnAutoFlipBoardChanged(bool value) => Apply();
    partial void OnSyncPlayAndAnalysisBoardsChanged(bool value) => Apply();
    partial void OnShowEngineArrowsChanged(bool value) => Apply();

    private void Apply()
    {
        AppSettings s = _service.Current;
        s.Theme = Theme;
        s.LightSquareColor = LightSquareColor;
        s.DarkSquareColor = DarkSquareColor;
        s.AccentColor = AccentColor;
        s.ShowCoordinates = ShowCoordinates;
        s.PieceScale = PieceScale;
        s.SoundEnabled = SoundEnabled;
        s.AnimationEnabled = AnimationEnabled;
        s.AnimationDurationMs = AnimationDurationMs;
        s.AutoFlipBoard = AutoFlipBoard;
        s.SyncPlayAndAnalysisBoards = SyncPlayAndAnalysisBoards;
        s.ShowEngineArrows = ShowEngineArrows;

        LiveChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Save()
    {
        Apply();
        _service.Save();
        StatusText = "Ayarlar kaydedildi.";
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        var defaults = new AppSettings();
        Theme = defaults.Theme;
        LightSquareColor = defaults.LightSquareColor;
        DarkSquareColor = defaults.DarkSquareColor;
        AccentColor = defaults.AccentColor;
        ShowCoordinates = defaults.ShowCoordinates;
        PieceScale = defaults.PieceScale;
        SoundEnabled = defaults.SoundEnabled;
        AnimationEnabled = defaults.AnimationEnabled;
        AnimationDurationMs = defaults.AnimationDurationMs;
        AutoFlipBoard = defaults.AutoFlipBoard;
        SyncPlayAndAnalysisBoards = defaults.SyncPlayAndAnalysisBoards;
        ShowEngineArrows = defaults.ShowEngineArrows;
        Apply();
        StatusText = "Varsayılanlara dönüldü.";
    }
}
