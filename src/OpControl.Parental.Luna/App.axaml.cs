using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LiveMarkdown.Avalonia;

namespace OpControl.Parental.Luna;

public partial class App : Application
{
    public override void Initialize()
    {
        MarkdownNode.Register<MathInlineNode>();
        MarkdownNode.Register<MathBlockNode>();
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = new LunaWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
