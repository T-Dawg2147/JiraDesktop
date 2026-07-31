using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Media;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using JiraDashboardApp.Core.Interfaces;
using JiraDashboardApp.Core.Models;
using JiraDashboardApp.Infrastructure.Services;
using JiraDashboardApp.Wpf.Services;

namespace JiraDashboardApp.Wpf;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly DashboardService _dashboardService;
    private readonly IJiraOAuthService _oauthService;
    private readonly UserSettingsService _settingsService;
    private readonly ToastService _toastService;
    private readonly ThemeService _themeService;

    private bool _isBusy;
    private bool _isConnected;
    private string _statusText = "Starting...";
    private string _lastSyncText = "Last sync: never";
    private WorkItem? _selectedWorkItem;

    private bool _showChangedOnly;
    private bool _hideDone = true;
    private string _selectedProductManager = "All";
    private string _selectedAssignee = "All";
    private string _searchText = "";

    private bool _isDrawerOpen;
    private double _drawerWidth;

    private bool _alwaysOnTop;
    private bool _autoHideDrawerOnFocusLoss = true;
    private string _watchedProductManager = "All";
    private bool _enableChangeNotifications = true;
    private bool _enableNotificationSound;
    private string _pageSizeText = "50";
    private string _syncIntervalSecondsText = "60";
    
    private string _selectedTheme = "JiraLight";

    private int _currentPage = 1;
    private int _totalPages = 1;

    private DispatcherTimer? _syncTimer;
    private CancellationTokenSource? _autosaveCts;

    private List<WorkItem> _allItems = new();
    private Dictionary<string, DateTime> _lastSeenUpdatedByKey = new();

    public ObservableCollection<WorkItem> Items { get; } = new();
    public ObservableCollection<string> ProductManagers { get; } = new() { "All" };
    public ObservableCollection<string> Assignees { get; } = new() { "All" };
    
    public ObservableCollection<string> Themes { get; } = new() { "JiraLight", "JiraDark" };

    public ICommand ToggleDrawerCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand SyncCommand { get; }
    public ICommand SnapBottomRightCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PrevPageCommand { get; }

    public MainWindowViewModel(
        DashboardService dashboardService,
        IJiraOAuthService oauthService,
        UserSettingsService settingsService,
        ToastService toastService,
        ThemeService themeService)
    {
        _dashboardService = dashboardService;
        _oauthService = oauthService;
        _settingsService = settingsService;
        _toastService = toastService;
        _themeService = themeService;

        ToggleDrawerCommand = new RelayCommand(_ => ToggleDrawer());
        SaveSettingsCommand = new RelayCommand(async w => await SaveSettingsAsync(w as Window));
        SyncCommand = new RelayCommand(async _ => await SyncAsync(), _ => !IsBusy && IsConnected);
        SnapBottomRightCommand = new RelayCommand(w => SnapBottomRight(w as Window));
        NextPageCommand = new RelayCommand(_ => NextPage(), _ => _currentPage < _totalPages);
        PrevPageCommand = new RelayCommand(_ => PrevPage(), _ => _currentPage > 1);
    }

    public async Task InitializeAsync(Window window)
    {
        var s = await _settingsService.LoadAsync();

        AlwaysOnTop = s.AlwaysOnTop;
        AutoHideDrawerOnFocusLoss = s.AutoHideDrawerOnFocusLoss;
        WatchedProductManager = s.WatchedProductManager;
        EnableChangeNotifications = s.EnableChangeNotifications;
        EnableNotificationSound = s.EnableNotificationSound;
        PageSizeText = s.PageSize.ToString();
        SyncIntervalSecondsText = s.SyncIntervalSeconds.ToString();
        SearchText = s.SearchText;
        HideDone = s.HideDone;
        
        SelectedTheme = string.IsNullOrWhiteSpace(s.ThemeName) ? "JiraLight" : s.ThemeName;
        _themeService.ApplyTheme(SelectedTheme);

        window.Left = s.WindowLeft;
        window.Top = s.WindowTop;
        window.Width = s.WindowWidth;
        window.Height = s.WindowHeight;

        await AutoConnectAndSyncAsync();
        StartSyncTimer();
    }

    public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); RaiseCommandStates(); } }
    public bool IsConnected { get => _isConnected; private set { _isConnected = value; OnPropertyChanged(); RaiseCommandStates(); } }

    public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChanged(); } }
    public string LastSyncText { get => _lastSyncText; private set { _lastSyncText = value; OnPropertyChanged(); } }

    public WorkItem? SelectedWorkItem { get => _selectedWorkItem; set { _selectedWorkItem = value; OnPropertyChanged(); } }

    public bool ShowChangedOnly { get => _showChangedOnly; set { _showChangedOnly = value; OnPropertyChanged(); ResetToFirstPageAndRebuild(); QueueAutosave(); } }
    public bool HideDone { get => _hideDone; set { _hideDone = value; OnPropertyChanged(); ResetToFirstPageAndRebuild(); QueueAutosave(); } }

    public string SelectedProductManager { get => _selectedProductManager; set { _selectedProductManager = value; OnPropertyChanged(); ResetToFirstPageAndRebuild(); } }
    public string SelectedAssignee { get => _selectedAssignee; set { _selectedAssignee = value; OnPropertyChanged(); ResetToFirstPageAndRebuild(); } }
    public string SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(); ResetToFirstPageAndRebuild(); QueueAutosave(); } }

    public bool IsDrawerOpen { get => _isDrawerOpen; set { _isDrawerOpen = value; OnPropertyChanged(); DrawerWidth = _isDrawerOpen ? 270 : 0; } }
    public double DrawerWidth { get => _drawerWidth; set { _drawerWidth = value; OnPropertyChanged(); } }

    public bool AlwaysOnTop { get => _alwaysOnTop; set { _alwaysOnTop = value; OnPropertyChanged(); QueueAutosave(); } }
    public bool AutoHideDrawerOnFocusLoss { get => _autoHideDrawerOnFocusLoss; set { _autoHideDrawerOnFocusLoss = value; OnPropertyChanged(); QueueAutosave(); } }

    public string WatchedProductManager { get => _watchedProductManager; set { _watchedProductManager = value; OnPropertyChanged(); QueueAutosave(); } }
    public bool EnableChangeNotifications { get => _enableChangeNotifications; set { _enableChangeNotifications = value; OnPropertyChanged(); QueueAutosave(); } }
    public bool EnableNotificationSound { get => _enableNotificationSound; set { _enableNotificationSound = value; OnPropertyChanged(); QueueAutosave(); } }

    public string PageSizeText { get => _pageSizeText; set { _pageSizeText = value; OnPropertyChanged(); ResetToFirstPageAndRebuild(); QueueAutosave(); } }
    public string SyncIntervalSecondsText
    {
        get => _syncIntervalSecondsText;
        set { _syncIntervalSecondsText = value; OnPropertyChanged(); StartSyncTimer(); QueueAutosave(); }
    }

    public string SelectedTheme { get => _selectedTheme; set { if (_selectedTheme == value) return; _selectedTheme = value; OnPropertyChanged(); _themeService.ApplyTheme(_selectedTheme); QueueAutosave(); } }
    
    public string PageText => $"Page {_currentPage} / {_totalPages}";

    private async Task AutoConnectAndSyncAsync()
    {
        try
        {
            StatusText = "Auto-connecting...";
            await _oauthService.EnsureLoggedInAsync();
            await _oauthService.GetCloudIdAsync();
            IsConnected = true;
            StatusText = "Connected";
            await SyncAsync();
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusText = $"Startup connect failed: {ex.Message}";
        }
    }
    
    public void RefreshGrid()
    {
        OnPropertyChanged(nameof(Items));
    }

    private async Task SyncAsync()
    {
        if (!IsConnected || IsBusy) return;

        IsBusy = true;
        try
        {
            StatusText = "Syncing Jira...";

            var result = await _dashboardService.LoadDashboardAsync(null, null); // fetch all tasks

            var previousPm = SelectedProductManager;
            var previousAssignee = SelectedAssignee;

            var previousUpdated = _lastSeenUpdatedByKey;

            // Default sort: Key numeric DESC
            _allItems = result.Items
                .OrderByDescending(ParseKeyNumber)
                .ThenByDescending(x => x.Updated)
                .ToList();
            
            DetectChangesAgainstPreviousSnapshot(result.Items.ToList());

            _lastSeenUpdatedByKey = _allItems.ToDictionary(x => x.Key, x => x.Updated);

            await RebuildFilterListsAsync(result.ProductManagers, result.Assignees);

            if (!ProductManagers.Contains(previousPm)) previousPm = "All";
            if (!Assignees.Contains(previousAssignee)) previousAssignee = "All";

            SelectedProductManager = previousPm;
            SelectedAssignee = previousAssignee;
            
            

            ResetToFirstPageAndRebuild();

            LastSyncText = $"Last sync: {DateTime.Now:dd MMM yyyy HH:mm:ss}";
            StatusText = $"Loaded {_allItems.Count} total";

            RaiseWatchNotifications(previousUpdated, _allItems);
        }
        catch (Exception ex)
        {
            StatusText = $"Sync failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    public void OpenWorkItemInBrowser(WorkItem item)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(item.Url)) return;
            Process.Start(new ProcessStartInfo
            {
                FileName = item.Url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Open failed: {ex.Message}";
        }
    }

    private void DetectChangesAgainstPreviousSnapshot(List<WorkItem> newItems)
    {
        var previous = _allItems.ToDictionary(x => x.Key, x => x);

        foreach (var item in newItems)
        {
            item.HasDetectedChanges = false;
            item.Changes.Clear();

            if (!previous.TryGetValue(item.Key, out var old))
                continue;

            AddChangeIfDifferent(item, "Summary", old.Summary, item.Summary);
            AddChangeIfDifferent(item, "Assignee", old.Assignee, item.Assignee);
            AddChangeIfDifferent(item, "Priority", old.Priority, item.Priority);
            AddChangeIfDifferent(item, "Status", old.Status, item.Status);
            AddChangeIfDifferent(item, "Product Manager", old.ProductManager, item.ProductManager);
            AddChangeIfDifferent(item, "Due Date",
                old.DueDate?.ToString("yyyy-MM-dd") ?? "",
                item.DueDate?.ToString("yyyy-MM-dd") ?? "");

            if (item.Changes.Count > 0)
            {
                item.HasDetectedChanges = true;
                item.IsPulseActive = true; // start pulsing until clicked
            }
            else
            {
                item.IsPulseActive = false;
            }

        }
    }

    private static void AddChangeIfDifferent(WorkItem item, string field, string oldVal, string newVal)
    {
        if (string.Equals(oldVal ?? "", newVal ?? "", StringComparison.Ordinal)) return;

        item.Changes.Add(new WorkItemFieldChange
        {
            Field = field,
            FromValue = oldVal ?? "",
            ToValue = newVal ?? "",
            ChangedAt = DateTime.UtcNow
        });
    }

    private async Task RebuildFilterListsAsync(IEnumerable<string> pmsFromIssues, IEnumerable<string> assigneesFromIssues)
    {
        var previousPm = SelectedProductManager;
        var previousAssignee = SelectedAssignee;

        List<string> allPmOptions;
        try
        {
            allPmOptions = await _dashboardService.GetAllProductManagerOptionsAsync(); // add pass-through in DashboardService
        }
        catch
        {
            allPmOptions = pmsFromIssues.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList();
        }

        ProductManagers.Clear();
        ProductManagers.Add("All");
        foreach (var p in allPmOptions)
            ProductManagers.Add(p);

        Assignees.Clear();
        Assignees.Add("All");
        foreach (var a in assigneesFromIssues.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x))
            Assignees.Add(a);

        SelectedProductManager = ProductManagers.Contains(previousPm) ? previousPm : "All";
        SelectedAssignee = Assignees.Contains(previousAssignee) ? previousAssignee : "All";
    }

    private IEnumerable<WorkItem> ApplyFilters(IEnumerable<WorkItem> source)
    {
        var query = source;

        if (SelectedProductManager != "All")
            query = query.Where(i => string.Equals(i.ProductManager, SelectedProductManager, StringComparison.OrdinalIgnoreCase));

        if (SelectedAssignee != "All")
            query = query.Where(i => string.Equals(i.Assignee, SelectedAssignee, StringComparison.OrdinalIgnoreCase));

        if (ShowChangedOnly)
            query = query.Where(i => i.HasChanges);

        if (HideDone)
            query = query.Where(i => !string.Equals(i.Status, "Done", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText.Trim();
            query = query.Where(i =>
                (i.Key?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.Summary?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.Assignee?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.Status?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.ProductManager?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return query;
    }

    private void RebuildVisibleItems()
    {
        var filtered = ApplyFilters(_allItems).ToList();

        var pageSize = Math.Max(10, ParseInt(PageSizeText, 50));
        _totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
        _currentPage = Math.Min(_currentPage, _totalPages);

        var skip = (_currentPage - 1) * pageSize;
        var page = filtered.Skip(skip).Take(pageSize).ToList();

        Items.Clear();
        foreach (var item in page)
            Items.Add(item);

        OnPropertyChanged(nameof(PageText));
        RaiseCommandStates();
    }

    private void ResetToFirstPageAndRebuild()
    {
        _currentPage = 1;
        RebuildVisibleItems();
    }

    private void NextPage()
    {
        if (_currentPage >= _totalPages) return;
        _currentPage++;
        RebuildVisibleItems();
    }

    private void PrevPage()
    {
        if (_currentPage <= 1) return;
        _currentPage--;
        RebuildVisibleItems();
    }

    private void RaiseWatchNotifications(Dictionary<string, DateTime> previous, IReadOnlyCollection<WorkItem> currentItems)
    {
        if (!EnableChangeNotifications || currentItems.Count == 0) return;

        var watched = string.IsNullOrWhiteSpace(WatchedProductManager) ? "All" : WatchedProductManager;
        var changedWatched = currentItems.Where(i =>
            i.HasDetectedChanges &&
            (WatchedProductManager == "All" ||
             string.Equals(i.ProductManager, WatchedProductManager, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        if (changedWatched.Count == 0) return;

        var msg = $"{changedWatched.Count} watched issue(s) changed";
        StatusText = $"🔔 {msg}";
        _toastService.ShowInfo("Jira Monitor", msg);

        if (EnableNotificationSound) SystemSounds.Asterisk.Play();
    }
    
    public void ApplyManualSort(string sortField, ListSortDirection direction)
    {
        Func<WorkItem, object?> selector = sortField switch
        {
            "KeyNumeric" => w => ParseKeyNumber(w),
            "Summary" => w => w.Summary,
            "Assignee" => w => w.Assignee,
            "Priority" => w => w.Priority,
            "Status" => w => w.Status,
            "Due" => w => w.DueDate,
            _ => w => ParseKeyNumber(w) // fallback to numeric key
        };

        var filtered = ApplyFilters(_allItems);

        var sorted = direction == ListSortDirection.Ascending
            ? filtered.OrderBy(selector).ThenBy(w => w.Key)
            : filtered.OrderByDescending(selector).ThenByDescending(w => w.Key);

        _allItems = sorted.ToList();
        _currentPage = 1;
        RebuildVisibleItems();
    }
    
    public async Task SaveSettingsAsync(Window? window = null)
    {
        await _settingsService.SaveAsync(new UserWidgetSettings
        {
            AlwaysOnTop = AlwaysOnTop,
            AutoHideDrawerOnFocusLoss = AutoHideDrawerOnFocusLoss,
            WatchedProductManager = WatchedProductManager,
            EnableChangeNotifications = EnableChangeNotifications,
            EnableNotificationSound = EnableNotificationSound,
            PageSize = ParseInt(PageSizeText, 50),
            SyncIntervalSeconds = ParseInt(SyncIntervalSecondsText, 60),
            SearchText = SearchText,
            HideDone = HideDone,
            WindowLeft = window?.Left ?? 80,
            WindowTop = window?.Top ?? 80,
            WindowWidth = window?.Width ?? 760,
            WindowHeight = window?.Height ?? 470,
            ThemeName = SelectedTheme
        });
    }

    private void QueueAutosave()
    {
        _autosaveCts?.Cancel();
        _autosaveCts = new CancellationTokenSource();
        var token = _autosaveCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (!token.IsCancellationRequested)
                    await SaveSettingsAsync();
            }
            catch { }
        }, token);
    }

    private void StartSyncTimer()
    {
        _syncTimer?.Stop();
        _syncTimer = new DispatcherTimer();
        var seconds = Math.Max(20, ParseInt(SyncIntervalSecondsText, 60));
        _syncTimer.Interval = TimeSpan.FromSeconds(seconds);
        _syncTimer.Tick += async (_, _) =>
        {
            if (!IsBusy && IsConnected) await SyncAsync();
        };
        _syncTimer.Start();
    }

    private void SnapBottomRight(Window? window)
    {
        if (window is null) return;
        var area = SystemParameters.WorkArea;
        window.Left = area.Right - window.Width - 16;
        window.Top = area.Bottom - window.Height - 16;
    }

    private static int ParseKeyNumber(WorkItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Key)) return int.MinValue;
        var dash = item.Key.LastIndexOf('-');
        if (dash < 0 || dash >= item.Key.Length - 1) return int.MinValue;
        return int.TryParse(item.Key[(dash + 1)..], out var n) ? n : int.MinValue;
    }

    private static int ParseInt(string? value, int fallback) => int.TryParse(value, out var n) ? n : fallback;

    private void ToggleDrawer() => IsDrawerOpen = !IsDrawerOpen;

    private void RaiseCommandStates()
    {
        (SyncCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PrevPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}