using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChessGUI.App.Services;

/// <summary>Bir güncelleme denetiminin sonucu.</summary>
public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string? DownloadUrl,
    string? ReleaseUrl,
    string? ErrorMessage);

/// <summary>
/// GitHub Releases API'sinden (muhtaraga/NewGenChessGUI) en güncel sürümü sorgular ve
/// gerekirse kurulum dosyasını indirir. <see cref="ViewModels.SettingsViewModel"/> tarafından
/// "Güncellemeleri denetle" butonu için kullanılır.
/// </summary>
public sealed class UpdateCheckService
{
    private const string ReleasesApiUrl = "https://api.github.com/repos/muhtaraga/NewGenChessGUI/releases/latest";

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ChessGUI", GetCurrentVersion().ToString()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static Version GetCurrentVersion()
    {
        Version v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        // Revision alanını yok say (GitHub etiketleri "1.0.1" gibi 3 parçalı; assembly sürümü 4 parçalı olur).
        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        Version current = GetCurrentVersion();
        try
        {
            await using Stream stream = await Http.GetStreamAsync(ReleasesApiUrl);
            GitHubRelease? release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream);
            if (release?.TagName is null)
            {
                return new UpdateCheckResult(false, current.ToString(3), current.ToString(3), null, null,
                    "Sürüm bilgisi alınamadı.");
            }

            string tag = release.TagName.TrimStart('v', 'V');
            if (!Version.TryParse(tag, out Version? latest))
            {
                return new UpdateCheckResult(false, current.ToString(3), tag, null, release.HtmlUrl,
                    "Sürüm numarası ayrıştırılamadı.");
            }

            string? downloadUrl = release.Assets?
                .FirstOrDefault(a => a.Name is not null && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                ?.BrowserDownloadUrl;

            bool isUpdateAvailable = latest > current;
            return new UpdateCheckResult(isUpdateAvailable, current.ToString(3), latest.ToString(3),
                downloadUrl, release.HtmlUrl, null);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, current.ToString(3), current.ToString(3), null, null, ex.Message);
        }
    }

    /// <summary>Kurulum dosyasını geçici klasöre indirir ve yerel yolunu döner.</summary>
    public async Task<string> DownloadInstallerAsync(string downloadUrl, string latestVersion)
    {
        string path = Path.Combine(Path.GetTempPath(), $"ChessGUISetup-{latestVersion}.exe");
        await using FileStream file = File.Create(path);
        await using Stream stream = await Http.GetStreamAsync(downloadUrl);
        await stream.CopyToAsync(file);
        return path;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
