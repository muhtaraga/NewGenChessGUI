using System.IO;
using System.Text.Json;
using ChessGUI.App.Models;

namespace ChessGUI.App.Services;

/// <summary>
/// <see cref="AppSettings"/> örneğini yükler/kaydeder (<c>settings.json</c>) ve değişince
/// abonelere haber verir. Uygulama başlangıcında bir kez oluşturulur.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public AppSettings Current { get; private set; }

    /// <summary>Ayarlar değişip kaydedildiğinde tetiklenir.</summary>
    public event EventHandler? Changed;

    public SettingsService()
    {
        Current = Load();
    }

    private static AppSettings Load()
    {
        try
        {
            string path = AppPaths.SettingsFile;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Bozuk dosya -> varsayılanlara dön.
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(Current, JsonOpts);
            File.WriteAllText(AppPaths.SettingsFile, json);
        }
        catch
        {
            // Diske yazılamazsa sessizce yut; ayarlar bellek içinde kalır.
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
