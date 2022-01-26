#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ControlzEx.Theming;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog.Events;

namespace PlexServiceTray
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow {
        private bool _maximiseRequired;
        private readonly HubConnection _plexService;
        
        public SettingsWindow(SettingsWindowViewModel settingsViewModel, HubConnection plexService)
        {
            InitializeComponent();
            var icon = IconHelper.GetIcon();
            if (icon != null) Icon = icon;
            _plexService = plexService;
            DataContext = settingsViewModel;
            var theme = settingsViewModel.WorkingSettings.Theme;
            if (theme != null) ThemeManager.Current.ChangeTheme(this, theme);
        }

        private void TitleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                _maximiseRequired = true;
            }
            else
            {
                DragMove();
            }
        }

        private void TitleMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (!_maximiseRequired) {
                return;
            }

            _maximiseRequired = false;
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        
        
        private void ThemeChanged(object sender, SelectionChangedEventArgs e) {
            try {
                var added = e.AddedItems;
                if (added.Count == 0) return;
                var item = (ComboBoxItem) added[0]!;
                var theme = (string) item.Content;
                theme = theme.Replace(" ", ".");
                ThemeManager.Current.ChangeTheme(this, theme);
                var svm = (SettingsWindowViewModel)DataContext;
                svm.WorkingSettings.Theme = theme;
            } catch (Exception ex) {
                _plexService.SendAsync("logMessage","Exception changing theme: " + ex.Message, LogEventLevel.Warning);
            }
        }
    }
}
