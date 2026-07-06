using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using ChessGUI.App.Models;
using ChessGUI.Engine;

namespace ChessGUI.App.Services;

/// <summary>
/// Kayıtlı UCI motor profillerini (<c>engines.json</c>) yönetir. Bir motor eklendiğinde
/// bir kez başlatılıp <c>id name</c> + <c>option</c> satırları okunur, profil olarak kaydedilir;
/// böylece sonraki açılışlarda motor yeniden başlatılmadan seçenek arayüzü çizilebilir.
/// </summary>
public sealed class EngineRegistry
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public ObservableCollection<EngineProfile> Engines { get; } = new();

    public EngineRegistry()
    {
        Load();
    }

    private void Load()
    {
        try
        {
            string path = AppPaths.EnginesFile;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<EngineProfile>>(json, JsonOpts);
                if (list != null)
                {
                    Engines.Clear();
                    foreach (var p in list) Engines.Add(p);
                }
            }
        }
        catch
        {
            // Bozuk dosya -> boş listeyle başla.
        }
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(Engines.ToList(), JsonOpts);
            File.WriteAllText(AppPaths.EnginesFile, json);
        }
        catch
        {
            // Diske yazılamazsa sessizce yut.
        }
    }

    /// <summary>
    /// Verilen yoldaki motoru bir kez başlatıp seçeneklerini okur, yeni bir profil olarak
    /// kaydeder ve döndürür. Motoru başlattıktan sonra kapatır (kalıcı örnek tutulmaz).
    /// </summary>
    public async Task<EngineProfile> AddAsync(string path)
    {
        var engine = new UciEngine();
        try
        {
            await engine.StartAsync(path);
            await engine.IsReadyAsync();

            var profile = new EngineProfile
            {
                Name = engine.Name != "UCI Motoru" ? engine.Name : Path.GetFileNameWithoutExtension(path),
                Path = path,
                Options = engine.Options.Select(UciOptionInfo.FromOption).ToList()
            };

            // Varsayılan değerleri kalıcı OptionValues'a kopyala (kullanıcı sonradan değiştirebilir).
            foreach (var opt in profile.Options)
            {
                if (opt.Default != null)
                    profile.OptionValues[opt.Name] = opt.Default;
            }

            Engines.Add(profile);
            Save();
            return profile;
        }
        finally
        {
            engine.Dispose();
        }
    }

    public void Remove(EngineProfile profile)
    {
        Engines.Remove(profile);
        Save();
    }
}
