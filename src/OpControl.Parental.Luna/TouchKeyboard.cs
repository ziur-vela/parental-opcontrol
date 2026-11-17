// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace OpControl.Parental.Luna;

/// <summary>
/// Shows the Windows touch keyboard when a text box is tapped with a finger or pen
/// on a 2-in-1 in tablet posture (or a touch PC with no keyboard). Luna is a topmost
/// tool window, so Windows does not always auto-invoke the keyboard for it; this
/// activates the window and asks the shell input pane directly, the same way WPF does.
/// </summary>
internal static class TouchKeyboard
{
    private const int SmConvertibleSlateMode = 0x2003;
    private const int SmDigitizer = 94;
    private const int NidIntegratedTouch = 0x01;
    private static readonly Guid IidInputPaneInterop = new("75CF2C57-9195-4931-8332-F0B409E916AF");
    private static readonly Guid IidInputPane2 = new("8A6B3F26-7090-4793-944C-C3F2CDE26276");
    private static DateTime _lastLog = DateTime.MinValue;

    /// <summary>Attach once to a root control; covers every TextBox inside it.</summary>
    public static void Attach(Control root)
    {
        root.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return;
            if (e.Pointer.Type is not (PointerType.Touch or PointerType.Pen)) return;
            if (e.Source is not Visual source) return;
            var box = source as TextBox ?? source.FindAncestorOfType<TextBox>();
            if (box is null || !box.IsEnabled || box.IsReadOnly) return;
            var top = TopLevel.GetTopLevel(box);
            var hwnd = top?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hwnd == IntPtr.Zero) return;

            var tablet = IsTabletPosture();
            if (top is Window w && !w.IsActive) w.Activate();
            SetForegroundWindow(hwnd);
            box.Focus(NavigationMethod.Pointer);
            if (!tablet)
            {
                Log($"touch_keyboard tap tablet=false (left to Windows)");
                return;
            }

            // Let focus settle, then ask the shell to show the keyboard.
            DispatcherTimer.RunOnce(() =>
            {
                var shown = TryShow(hwnd);
                Log($"touch_keyboard tap tablet=true try_show={shown}");
            }, TimeSpan.FromMilliseconds(80));
        }
        catch (Exception ex)
        {
            Log("touch_keyboard error " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>True on a convertible in slate mode, or a touch PC that reports slate mode.</summary>
    public static bool IsTabletPosture()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var hasTouch = (GetSystemMetrics(SmDigitizer) & NidIntegratedTouch) != 0;
        return hasTouch && GetSystemMetrics(SmConvertibleSlateMode) == 0;
    }

    public static bool TryShow(IntPtr hwnd)
    {
        IntPtr hstr = IntPtr.Zero, factory = IntPtr.Zero, pane = IntPtr.Zero;
        try
        {
            const string cls = "Windows.UI.ViewManagement.InputPane";
            if (WindowsCreateString(cls, cls.Length, out hstr) < 0) return false;
            var iid = IidInputPaneInterop;
            if (RoGetActivationFactory(hstr, ref iid, out factory) < 0 || factory == IntPtr.Zero) return false;

            // IInputPaneInterop: IInspectable (6 slots) then GetForWindow.
            var getForWindow = Marshal.GetDelegateForFunctionPointer<GetForWindowFn>(Slot(factory, 6));
            var iid2 = IidInputPane2;
            if (getForWindow(factory, hwnd, ref iid2, out pane) < 0 || pane == IntPtr.Zero) return false;

            // IInputPane2: IInspectable (6 slots) then TryShow, TryHide.
            var tryShow = Marshal.GetDelegateForFunctionPointer<TryShowFn>(Slot(pane, 6));
            return tryShow(pane, out var ok) >= 0 && ok != 0;
        }
        finally
        {
            if (pane != IntPtr.Zero) Marshal.Release(pane);
            if (factory != IntPtr.Zero) Marshal.Release(factory);
            if (hstr != IntPtr.Zero) WindowsDeleteString(hstr);
        }
    }

    private static IntPtr Slot(IntPtr comObject, int index) =>
        Marshal.ReadIntPtr(Marshal.ReadIntPtr(comObject), index * IntPtr.Size);

    private static void Log(string line)
    {
        // Rate-limited so taps never spam the log.
        if ((DateTime.UtcNow - _lastLog).TotalMilliseconds < 500) return;
        _lastLog = DateTime.UtcNow;
        LunaLog.Write(line);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetForWindowFn(IntPtr self, IntPtr hwnd, ref Guid riid, out IntPtr inputPane);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int TryShowFn(IntPtr self, out byte result);

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string source, int length, out IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
}
