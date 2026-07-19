namespace DownKyi.Core.Settings.Models;

public class HistorySettings
{
    public bool IsAutoRefreshEnabled { get; set; }
    public decimal AutoRefreshIntervalSeconds { get; set; } = 30m;
}
