using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LiveMarkdown.Avalonia;

namespace OpControl.Parental.Luna;

public partial class App : Application
{
    public static KidWindowOptions? KidOptions { get; set; }
#if LUNA_DEV
    public static ContentPreviewOptions? ContentPreview { get; set; }
    public static bool LunaOpenChatPreview { get; set; }
    public static string? LunaScreenshotPath { get; set; }
#endif

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
#if LUNA_DEV
            if (ContentPreview is { } preview)
            {
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                desktop.MainWindow = new LunaContentPreviewWindow(preview);
            }
            else
#endif
            if (KidOptions is { } options)
            {
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
#if LUNA_DEV
                IKidDataSource source = options.Preview
                    ? new KidPreviewDataSource(options.Theme ?? "night")
                    : new PipeKidDataSource();
#else
                options = options with { Preview = false };
                IKidDataSource source = new PipeKidDataSource();
#endif
                desktop.MainWindow = new MyOpControlWindow(source, options);
            }
            else
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                desktop.MainWindow = new LunaWindow();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
