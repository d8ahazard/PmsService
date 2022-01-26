using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using ControlzEx.Theming;

namespace PlexServiceTray; 

/// <summary>
/// Interaction logic for AboutWindow.xaml
/// </summary>
public partial class AboutWindow
{
    public string Version
    {
        get => (string)GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    // Using a DependencyProperty as the backing store for Version.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty VersionProperty =
        DependencyProperty.Register("Version", typeof(string), typeof(AboutWindow), new PropertyMetadata(string.Empty));


    public string Help
    {
        get => (string)GetValue(HelpProperty);
        set => SetValue(HelpProperty, value);
    }

    // Using a DependencyProperty as the backing store for Help.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty HelpProperty =
        DependencyProperty.Register("Help", typeof(string), typeof(AboutWindow), new PropertyMetadata(string.Empty));


    public string HelpLink
    {
        get => (string)GetValue(HelpLinkProperty);
        set => SetValue(HelpLinkProperty, value);
    }

    // Using a DependencyProperty as the backing store for HelpLink.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty HelpLinkProperty =
        DependencyProperty.Register("HelpLink", typeof(string), typeof(AboutWindow), new PropertyMetadata("https://github.com/cjmurph/PmsService/issues"));


    // Using a DependencyProperty as the backing store for HelpLinkDisplayText.  This enables animation, styling, binding, etc...


    public string File
    {
        get => (string)GetValue(FileProperty);
        set => SetValue(FileProperty, value);
    }

    // Using a DependencyProperty as the backing store for File.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty FileProperty =
        DependencyProperty.Register("File", typeof(string), typeof(AboutWindow), new PropertyMetadata(string.Empty));


    // Using a DependencyProperty as the backing store for DialogueResult.  This enables animation, styling, binding, etc...


    public AboutWindow(string theme)
    {
        InitializeComponent();
        var icon = IconHelper.GetIcon();
        if (icon != null) Icon = icon;
        ThemeManager.Current.ChangeTheme(this, theme);
        File = "LICENCE.rtf";
        Version = $"PMS Service {Assembly.GetExecutingAssembly().GetName().Version}";
        Help = "Please report any bugs or issues to:";
        DataContext = this;
    }

    private void Title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    #region OkCommand
    RelayCommand? _okCommand;
    public RelayCommand? OkCommand
    {
        get { return _okCommand ??= new RelayCommand(OnOk, CanOk); }
    }

    private static bool CanOk(object parameter)
    {
        return true;
    }

    private void OnOk(object parameter)
    {
        DialogResult = true;
    }

    #endregion OkCommand

    public static bool Shown {get; private set;}

    public static void ShowAboutDialog(string theme)
    {
        Shown = true;
        new AboutWindow(theme).ShowDialog();
        Shown = false;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri));
        e.Handled = true;
    }
}