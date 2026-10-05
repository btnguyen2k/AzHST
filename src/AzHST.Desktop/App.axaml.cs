using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AzHST.Desktop.Composition;
using AzHST.Desktop.Models;

namespace AzHST.Desktop;

public sealed partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var launchOptions = ApplicationLaunchOptions.Parse(desktop.Args);
            desktop.MainWindow = ApplicationCompositionRoot.CreateMainWindow(
                launchOptions);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
