using System;
using Avalonia;

namespace OpControl.Parental.Luna;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LunaLog.Write("UNHANDLED " + e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) =>
            LunaLog.Write("UNOBSERVED " + e.Exception);

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LunaLog.Write("FATAL " + ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
