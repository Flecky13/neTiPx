using System;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using neTiPx.UI.Avalonia.Models;
using neTiPx.UI.Avalonia.Services;

namespace neTiPx.UI.Avalonia.ViewModels;

public partial class PingToolViewModel : ObservableObject
{
    [ObservableProperty]
    private string _targetInput = string.Empty;

    [ObservableProperty]
    private int _selectedIntervalSeconds = 5;

    [ObservableProperty]
    private string _intervalInput = "5";

    [ObservableProperty]
    private bool _backgroundEnabled = true;

    [ObservableProperty]
    private PingMonitorItem? _editingItem;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    private readonly PingMonitorService _monitorService;

    public ObservableCollection<int> AvailableIntervals { get; } = new() { 1, 2, 5, 10, 30, 60 };

    public ReadOnlyObservableCollection<PingMonitorItem> Items => _monitorService.Items;

    public string SubmitButtonText => EditingItem == null ? "Hinzufügen" : "Übernehmen";

    public PingToolViewModel(PingMonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    partial void OnSelectedIntervalSecondsChanged(int value)
    {
        if (value > 0)
        {
            IntervalInput = value.ToString(CultureInfo.InvariantCulture);
        }
    }

    partial void OnEditingItemChanged(PingMonitorItem? value)
    {
        OnPropertyChanged(nameof(SubmitButtonText));
    }

    public void OnPageLoaded()
    {
        _monitorService.SetUiActive(true);
    }

    public void OnPageUnloaded()
    {
        _monitorService.SetUiActive(false);
    }

    public void SetUiContextActive(bool isActive)
    {
        _monitorService.SetUiActive(isActive);
    }

    public void SaveInput()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(TargetInput))
            {
                StatusMessage = "Bitte eine Zieladresse eingeben.";
                return;
            }

            if (!TryParseIntervalSeconds(IntervalInput, out var intervalSeconds))
            {
                StatusMessage = "Bitte ein gültiges Ping-Intervall (> 0 Sekunden) eingeben.";
                return;
            }

            SelectedIntervalSeconds = intervalSeconds;

            if (EditingItem == null)
            {
                _monitorService.AddTarget(TargetInput, intervalSeconds, BackgroundEnabled);
                StatusMessage = "Ziel hinzugefügt";
            }
            else
            {
                _monitorService.UpdateTarget(EditingItem, TargetInput, intervalSeconds, BackgroundEnabled);
                StatusMessage = "Ziel aktualisiert";
            }

            ResetEditor();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message switch
            {
                "Target already exists. (Parameter 'target')" => "Ziel ist bereits vorhanden.",
                "Target must not be empty. (Parameter 'target')" => "Bitte eine Zieladresse eingeben.",
                _ => ex.Message
            };
        }
    }

    public void Edit(PingMonitorItem item)
    {
        EditingItem = item;
        TargetInput = item.Target;
        SelectedIntervalSeconds = item.IntervalSeconds;
        IntervalInput = item.IntervalSeconds.ToString(CultureInfo.InvariantCulture);
        BackgroundEnabled = item.RunInBackground;
        StatusMessage = "Bearbeitung aktiv";
    }

    public void CancelEdit()
    {
        ResetEditor();
        StatusMessage = "Bearbeitung verworfen";
    }

    public void Delete(PingMonitorItem item)
    {
        _monitorService.DeleteTarget(item);
        if (EditingItem == item)
        {
            ResetEditor();
        }

        StatusMessage = "Ziel gelöscht";
    }

    public void Start(PingMonitorItem item)
    {
        _monitorService.Start(item);
        StatusMessage = "Monitoring gestartet";
    }

    public void Pause(PingMonitorItem item)
    {
        _monitorService.Pause(item);
        StatusMessage = "Monitoring pausiert";
    }

    public void TriggerNow(PingMonitorItem item)
    {
        _monitorService.TriggerNow(item);
        StatusMessage = "Soforttest ausgeführt";
    }

    private void ResetEditor()
    {
        EditingItem = null;
        TargetInput = string.Empty;
        SelectedIntervalSeconds = 5;
        IntervalInput = "5";
        BackgroundEnabled = true;
    }

    private static bool TryParseIntervalSeconds(string? raw, out int seconds)
    {
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds) && seconds > 0)
        {
            return true;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out seconds) && seconds > 0)
        {
            return true;
        }

        seconds = 0;
        return false;
    }
}
