using Avalonia;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace neTiPx.UI.Avalonia;

class Program
{
    // The names are scoped to the current user session. This permits one instance
    // per desktop session while preventing duplicate starts from shortcuts or the tray.
    private const string InstanceMutexName = "neTiPx.UI.Avalonia.SingleInstance";
    private const string ActivationEventName = "neTiPx.UI.Avalonia.ActivateExistingInstance";
    private static int _activationRequested;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        using var instanceMutex = new Mutex(
            initiallyOwned: true,
            name: InstanceMutexName,
            createdNew: out var isFirstInstance);

        if (!isFirstInstance)
        {
            SignalExistingInstance();
            return;
        }

        using var activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            ActivationEventName);

        _ = Task.Run(() => WaitForActivationRequests(activationEvent));

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void SignalExistingInstance()
    {
        // A second launch can arrive in the few milliseconds between acquiring
        // the mutex and creating the event in the first process.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var activationEvent = EventWaitHandle.OpenExisting(ActivationEventName);
                activationEvent.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException) when (attempt < 19)
            {
                Thread.Sleep(25);
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return;
            }
        }
    }

    private static void WaitForActivationRequests(EventWaitHandle activationEvent)
    {
        try
        {
            while (true)
            {
                activationEvent.WaitOne();
                Interlocked.Exchange(ref _activationRequested, 1);
                App.RequestMainWindowActivation();
            }
        }
        catch (ObjectDisposedException)
        {
            // Application shutdown disposes the event handle.
        }
    }

    internal static bool ConsumeActivationRequest() =>
        Interlocked.Exchange(ref _activationRequested, 0) == 1;

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        // Plattformspezifische Konfiguration
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            builder.UseWin32();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            builder.UseX11();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            builder.UseAvaloniaNative();
        }

        return builder;
    }
}
