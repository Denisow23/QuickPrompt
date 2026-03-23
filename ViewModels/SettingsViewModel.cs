using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using QuickPrompt.Helpers;
using QuickPrompt.Models;
using QuickPrompt.Services;

namespace QuickPrompt.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainWindowViewModel;

    private string _baseUrl = string.Empty;
    private string _apiKey = string.Empty;
    private string _defaultModel = string.Empty;
    private double _temperature = 0.2;
    private int _maxTokens = 1000;
    private string _additionalHeadersJson = "{}";

    public SettingsViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;

        var settingsService = new SettingsService();
        var settings = settingsService.Load();

        BaseUrl = settings.BaseUrl;
        ApiKey = settingsService.Unprotect(settings.EncryptedApiKey);
        DefaultModel = settings.DefaultModel;
        Temperature = settings.Temperature;
        MaxTokens = settings.MaxTokens;
        AdditionalHeadersJson = JsonSerializer.Serialize(settings.AdditionalHeaders, new JsonSerializerOptions { WriteIndented = true });

        SaveCommand = new RelayCommand(Save);
    }

    public string BaseUrl
    {
        get => _baseUrl;
        set
        {
            _baseUrl = value;
            OnPropertyChanged();
        }
    }

    public string ApiKey
    {
        get => _apiKey;
        set
        {
            _apiKey = value;
            OnPropertyChanged();
        }
    }

    public string DefaultModel
    {
        get => _defaultModel;
        set
        {
            _defaultModel = value;
            OnPropertyChanged();
        }
    }

    public double Temperature
    {
        get => _temperature;
        set
        {
            _temperature = value;
            OnPropertyChanged();
        }
    }

    public int MaxTokens
    {
        get => _maxTokens;
        set
        {
            _maxTokens = value;
            OnPropertyChanged();
        }
    }

    public string AdditionalHeadersJson
    {
        get => _additionalHeadersJson;
        set
        {
            _additionalHeadersJson = value;
            OnPropertyChanged();
        }
    }

    public RelayCommand SaveCommand { get; }

    private void Save()
    {
        try
        {
            var service = new SettingsService();
            var additionalHeaders =
                JsonSerializer.Deserialize<Dictionary<string, string>>(AdditionalHeadersJson)
                ?? new Dictionary<string, string>();

            var settings = new AppSettings
            {
                BaseUrl = BaseUrl.Trim(),
                EncryptedApiKey = service.Protect(ApiKey),
                DefaultModel = DefaultModel.Trim(),
                Temperature = Temperature,
                MaxTokens = MaxTokens,
                AdditionalHeaders = additionalHeaders
            };

            service.Save(settings);
            _mainWindowViewModel.ApplySettings(settings);
            System.Windows.MessageBox.Show("Настройки сохранены.", "QuickPrompt", MessageBoxButton.OK, MessageBoxImage.Information);

            if (System.Windows.Application.Current.Windows.Count <= 0)
            {
                return;
            }

            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is SettingsWindow)
                {
                    window.Close();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Не удалось сохранить настройки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
