using System.Windows;
using QuickPrompt.ViewModels;

namespace QuickPrompt;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                ApiKeyBox.Password = vm.ApiKey;
            }
        };
    }

    private void ApiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.ApiKey = ApiKeyBox.Password;
        }
    }
}
