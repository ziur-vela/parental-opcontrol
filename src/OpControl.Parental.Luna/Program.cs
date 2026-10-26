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
            var kidMode = args.Any(a => string.Equals(a, "--kid-window", StringComparison.OrdinalIgnoreCase));
#if LUNA_DEV
            kidMode |= args.Any(a => string.Equals(a, "--kid-preview", StringComparison.OrdinalIgnoreCase));
            if (args.Any(a => string.Equals(a, "--content-preview", StringComparison.OrdinalIgnoreCase)))
            {
                App.ContentPreview = new ContentPreviewOptions(
                    Arg(args, "--theme") ?? "night",
                    Arg(args, "--sample") ?? "math");
            }
#endif
            if (kidMode)
            {
                App.KidOptions = new KidWindowOptions(
                    Tab: Arg(args, "--tab") ?? "home",
                    MessagesOnly: args.Any(a => string.Equals(a, "--messages-only", StringComparison.OrdinalIgnoreCase)),
                    AboveLock: args.Any(a => string.Equals(a, "--above-lock", StringComparison.OrdinalIgnoreCase)),
                    Theme: Arg(args, "--theme"),
                    Language: Arg(args, "--language"),
#if LUNA_DEV
                    Preview: args.Any(a => string.Equals(a, "--kid-preview", StringComparison.OrdinalIgnoreCase)));
#else
                    Preview: false);
#endif
            }
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

    private static string? Arg(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
            if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                return args[i][(name.Length + 1)..];
        }
        return null;
    }
}
