using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessGUI.App.Models;
using ChessGUI.App.Services;

namespace ChessGUI.App.ViewModels;

/// <summary>Basit bir seçim satırı: "İnsan" ya da kayıtlı bir motor.</summary>
public sealed class PlayerOption
{
    public string DisplayName { get; }
    public EngineProfile? Engine { get; }
    public bool IsHuman => Engine is null;

    public PlayerOption(string displayName, EngineProfile? engine)
    {
        DisplayName = displayName;
        Engine = engine;
    }

    public PlayerConfig ToConfig() => Engine is null ? PlayerConfig.Human() : PlayerConfig.ForEngine(Engine);

    public override string ToString() => DisplayName;
}

/// <summary>
/// Oyun kurma diyaloğunun görünüm modeli: beyaz/siyah oyuncu seçimi (insan veya kayıtlı motor),
/// süre kontrolü (preset veya özel) ve başlangıç konumu (şimdilik standart).
/// </summary>
public sealed partial class GameSetupViewModel : ObservableObject
{
    public ObservableCollection<PlayerOption> PlayerOptions { get; } = new();
    public IReadOnlyList<TimeControl> Presets => TimeControl.Presets;

    [ObservableProperty] private PlayerOption? _whiteOption;
    [ObservableProperty] private PlayerOption? _blackOption;
    [ObservableProperty] private TimeControl _selectedPreset;
    [ObservableProperty] private bool _isCustom;
    [ObservableProperty] private int _customMinutes = 5;
    [ObservableProperty] private int _customIncrement = 0;

    [ObservableProperty] private bool _useDifferentTimes;
    [ObservableProperty] private int _whiteCustomMinutes = 5;
    [ObservableProperty] private int _whiteCustomIncrement = 0;
    [ObservableProperty] private int _blackCustomMinutes = 5;
    [ObservableProperty] private int _blackCustomIncrement = 0;

    /// <summary>"Başlat" komutu tetiklendiğinde yükselir (kabuk dinleyip oyunu başlatır).</summary>
    public event Action? StartRequested;

    public GameSetupViewModel(EngineRegistry registry)
    {
        RefreshEngines(registry);
        _selectedPreset = Presets[2]; // varsayılan: 5+0
    }

    /// <summary>Motor listesi güncellendiğinde (Motorlar sekmesinden ekleme/kaldırma) yeniden çağrılır.
    /// Önceki seçimleri (motor adına göre) korumaya çalışır.</summary>
    public void RefreshEngines(EngineRegistry registry)
    {
        string? whiteName = WhiteOption?.Engine?.Name;
        string? blackName = BlackOption?.Engine?.Name;

        PlayerOptions.Clear();
        PlayerOptions.Add(new PlayerOption("İnsan", null));
        foreach (var profile in registry.Engines)
            PlayerOptions.Add(new PlayerOption(profile.Name, profile));

        WhiteOption = (whiteName != null ? PlayerOptions.FirstOrDefault(o => o.Engine?.Name == whiteName) : null)
                      ?? PlayerOptions[0];
        BlackOption = (blackName != null ? PlayerOptions.FirstOrDefault(o => o.Engine?.Name == blackName) : null)
                      ?? (PlayerOptions.Count > 1 ? PlayerOptions[1] : PlayerOptions[0]);
    }

    public TimeControl BuildTimeControl() =>
        IsCustom ? TimeControl.Fischer(CustomMinutes, CustomIncrement) : SelectedPreset.Clone();

    public TimeControl BuildWhiteTimeControl() =>
        UseDifferentTimes ? TimeControl.Fischer(WhiteCustomMinutes, WhiteCustomIncrement) : BuildTimeControl();

    public TimeControl BuildBlackTimeControl() =>
        UseDifferentTimes ? TimeControl.Fischer(BlackCustomMinutes, BlackCustomIncrement) : BuildTimeControl();

    [RelayCommand]
    private void Start() => StartRequested?.Invoke();
}
