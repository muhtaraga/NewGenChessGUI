using ChessGUI.App.Models;
using ChessGUI.Engine;

namespace ChessGUI.App.Services;

/// <summary>
/// Bir <see cref="EngineProfile"/>'dan çalışan bir <see cref="UciEngine"/> örneği kurar:
/// <c>StartAsync</c> → profildeki her ad/değeri <c>SetOption</c> ile gönder → <c>IsReadyAsync</c> → <c>NewGame</c>.
/// Hem analiz (<c>EngineViewModel</c>) hem oyun (<c>PlayController</c>) bu tek yeri kullanır.
/// </summary>
public static class EngineLauncher
{
    public static async Task<UciEngine> LaunchAsync(EngineProfile profile, CancellationToken ct = default)
    {
        var engine = new UciEngine();
        try
        {
            await engine.StartAsync(profile.Path, ct);

            foreach (var kv in profile.OptionValues)
            {
                bool exists = engine.Options.Any(o => string.Equals(o.Name, kv.Key, StringComparison.OrdinalIgnoreCase));
                if (exists) engine.SetOption(kv.Key, kv.Value);
            }

            await engine.IsReadyAsync(ct);
            engine.NewGame();
            return engine;
        }
        catch
        {
            engine.Dispose();
            throw;
        }
    }
}
