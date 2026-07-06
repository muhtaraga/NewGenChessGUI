using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ChessGUI.App.Models;
using ChessGUI.App.Services;

namespace ChessGUI.App.ViewModels;

/// <summary>
/// Motor Yönetimi ekranının görünüm modeli: kayıtlı motorların listesi + seçili motorun jenerik
/// UCI seçenek editörü (spin/check/combo/string/button — motor hangi seçeneği sunuyorsa otomatik
/// çıkar). Düzenlemeler <see cref="EngineProfile.OptionValues"/>'a yazılır; "Kaydet" ile diske yazılır.
/// </summary>
public sealed partial class EngineManagerViewModel : ObservableObject
{
    private readonly EngineRegistry _registry;

    public ObservableCollection<EngineProfile> Engines => _registry.Engines;
    public ObservableCollection<OptionEditorViewModel> OptionEditors { get; } = new();

    [ObservableProperty] private EngineProfile? _selectedEngine;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isAdding;

    public EngineManagerViewModel(EngineRegistry registry)
    {
        _registry = registry;
    }

    partial void OnSelectedEngineChanged(EngineProfile? value)
    {
        OptionEditors.Clear();
        if (value != null)
            foreach (var opt in value.Options)
                OptionEditors.Add(new OptionEditorViewModel(opt, value));
    }

    [RelayCommand]
    private async Task AddEngine()
    {
        var dialog = new OpenFileDialog
        {
            Title = "UCI motoru seç", Filter = "Motor (*.exe)|*.exe|Tüm dosyalar (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        IsAdding = true;
        StatusText = "Motor başlatılıyor, seçenekler okunuyor…";
        try
        {
            EngineProfile profile = await _registry.AddAsync(dialog.FileName);
            SelectedEngine = profile;
            StatusText = $"{profile.Name} eklendi ({profile.Options.Count} seçenek).";
        }
        catch (Exception ex)
        {
            StatusText = $"Motor eklenemedi: {ex.Message}";
        }
        finally
        {
            IsAdding = false;
        }
    }

    [RelayCommand]
    private void RemoveEngine()
    {
        if (SelectedEngine is null) return;
        _registry.Remove(SelectedEngine);
        SelectedEngine = null;
        StatusText = "Motor kaldırıldı.";
    }

    [RelayCommand]
    private void Save()
    {
        _registry.Save();
        StatusText = "Kaydedildi.";
    }
}
