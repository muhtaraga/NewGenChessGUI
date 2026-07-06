using System.Windows;
using ChessGUI.App.Services;
using ChessGUI.App.ViewModels;

namespace ChessGUI.App;

/// <summary>
/// Uygulama giriş noktası. StartupUri kullanılmaz — servisler (AppPaths, SettingsService,
/// EngineRegistry, SoundService) burada oluşturulur, engines.json/settings.json yüklenir,
/// ShellViewModel kurulur ve MainWindow'un DataContext'i olarak atanıp gösterilir.
/// </summary>
public partial class App : Application
{
    private ShellViewModel? _shell;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsService = new SettingsService();
        var engineRegistry = new EngineRegistry();
        var soundService = new SoundService(settingsService);
        var themeService = new ThemeService();
        themeService.Apply(settingsService.Current.Theme);

        _shell = new ShellViewModel(engineRegistry, settingsService, soundService, themeService);

        var window = new MainWindow { DataContext = _shell };
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shell?.Dispose();
        base.OnExit(e);
    }
}
