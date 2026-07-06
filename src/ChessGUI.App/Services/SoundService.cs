using System.IO;
using System.Media;
using ChessGUI.App.Models;

namespace ChessGUI.App.Services;

public enum SoundKind
{
    Move,
    Capture,
    Check,
    GameEnd
}

/// <summary>
/// Hamle/alma/şah/oyun-sonu seslerini çalar (<c>Assets/Sounds/*.wav</c>).
/// Ayardan kapalıysa veya ilgili .wav dosyası yoksa güvenli şekilde sessiz kalır (no-op).
/// </summary>
public sealed class SoundService
{
    private readonly SettingsService _settings;
    private readonly string _assetsFolder;

    public SoundService(SettingsService settings)
    {
        _settings = settings;
        _assetsFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");
    }

    public void Play(SoundKind kind)
    {
        if (!_settings.Current.SoundEnabled) return;

        string fileName = kind switch
        {
            SoundKind.Move => "move.wav",
            SoundKind.Capture => "capture.wav",
            SoundKind.Check => "check.wav",
            SoundKind.GameEnd => "gameend.wav",
            _ => ""
        };
        if (fileName.Length == 0) return;

        string path = Path.Combine(_assetsFolder, fileName);
        if (!File.Exists(path)) return; // asset yoksa no-op

        try
        {
            using var player = new SoundPlayer(path);
            player.Play();
        }
        catch
        {
            // Ses çalınamazsa sessizce yut.
        }
    }
}
