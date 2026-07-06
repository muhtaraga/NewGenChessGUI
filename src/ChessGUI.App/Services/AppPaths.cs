using System.IO;

namespace ChessGUI.App.Services;

/// <summary>Uygulamanın kalıcı verilerini sakladığı <c>%AppData%\ChessGUI</c> klasörünü yönetir.</summary>
public static class AppPaths
{
    public static string RootFolder
    {
        get
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "ChessGUI");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string EnginesFile => Path.Combine(RootFolder, "engines.json");
    public static string SettingsFile => Path.Combine(RootFolder, "settings.json");
}
