namespace neTiPx.Core.Interfaces;

/// <summary>
/// System Tray Service für Plattform-spezifische Tray-Icon-Verwaltung
/// </summary>
public interface ITrayService
{
    /// <summary>
    /// Initialisiert das Tray-Icon
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Zeigt das Hauptfenster
    /// </summary>
    void ShowMainWindow();

    /// <summary>
    /// Versteckt das Hauptfenster
    /// </summary>
    void HideMainWindow();

    /// <summary>
    /// Minimiert zum Tray
    /// </summary>
    void MinimizeToTray();

    /// <summary>
    /// Setzt Tray-Icon-Tooltip
    /// </summary>
    void SetTooltip(string text);

    /// <summary>
    /// Zeigt eine Notification
    /// </summary>
    void ShowNotification(string title, string message, NotificationType type = NotificationType.Info);

    /// <summary>
    /// Event wenn auf Tray-Icon geklickt wird
    /// </summary>
    event EventHandler? TrayIconClicked;

    /// <summary>
    /// Event wenn auf Tray-Menü-Item geklickt wird
    /// </summary>
    event EventHandler<string>? MenuItemClicked;
}

public enum NotificationType
{
    Info,
    Warning,
    Error,
    Success
}
