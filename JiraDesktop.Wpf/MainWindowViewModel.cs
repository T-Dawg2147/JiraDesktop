using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Media;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using JiraDesktop.Core.Interfaces;
using JiraDesktop.Core.Models;
using JiraDesktop.Core.Services;
using JiraDesktop.Wpf.Commands;
using JiraDesktop.Wpf.Services;

namespace JiraDesktop.Wpf;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly DashboardService _dashboardService;
    private readonly IJiraOAuthService _oauthService;
    private readonly IJiraService _jiraService;
    private readonly UserProfileService _profileService;
    private readonly ToastService _toastService;
    private readonly ThemeService _themeService;

    private readonly ObservableCollection<WorkItem> _items = [];
    private readonly ObservableCollection<string> _productManagers = ["All"];
    private readonly ObservableCollection<string> _assignees = ["All"];
    private readonly ObservableCollection<UserProfile> _profiles = [];
    private readonly ObservableCollection<UserRole> _roles = [UserRole.Admin, UserRole.ProductManager, UserRole.Viewer];
    private readonly ObservableCollection<string> _themes = ["JiraLight", "JiraDark"];

    private List<WorkItem> _allItems = [];
    private Window? _window;
    private DispatcherTimer? _syncTimer;
    private CancellationTokenSource? _autosaveCts;
    private bool _isApplyingProfile;
    private bool _isChangingProfile;
    private bool _isBusy;
    private bool _isConnected;
    private bool _isLoadingTransitions;
    private string _statusText = "Starting...";
    private string _lastSyncText = "Last sync: never";
    private UserProfile? _selectedProfile;
    private WorkItem? _selectedWorkItem;
    private string _selectedProductManager = "All";
    private string _selectedAssignee = "All";
    private string _searchText = string.Empty;
    private bool _hideDone = true;
    private bool _showChangedOnly;
    private bool _alwaysOnTop;
    private bool _enableChangeNotifications = true;
    private bool _enableNotificationSound;
    private string _syncIntervalSecondsText = "60";
    private string _selectedTheme = "JiraLight";
    private string _sortField = "Updated";
    private ListSortDirection _sortDirection = ListSortDirection.Descending;
    private string _newProfileName = string.Empty;
    private UserRole _newProfileRole = UserRole.Viewer;
    private string _newProfileManagedProductManager = string.Empty;

    public MainWindowViewModel(
        DashboardService dashboardService,
        IJiraOAuthService oauthService,
        IJiraService jiraService,
        UserProfileService profileService,
        ToastService toastService,
        ThemeService themeService)
    {
        _dashboardService = dashboardService;
        _oauthService = oauthService;
        _jiraService = jiraService;
        _profileService = profileService;
        _toastService = toastService;
        _themeService = themeService;

        SyncCommand = new RelayCommand(async _ => await SyncAsync(), _ => !IsBusy && SelectedProfile is not null);
        OpenWorkItemCommand = new RelayCommand(item => OpenWorkItemInBrowser(item as WorkItem), item => item is WorkItem);
        ChangeStatusCommand = new RelayCommand(async _ => await ChangeSelectedStatusAsync(), _ => !IsBusy && CanEditSelectedWorkItem);
        SaveProfileCommand = new RelayCommand(async _ => await SaveSelectedProfileAsync(), _ => SelectedProfile is not null);
        CreateProfileCommand = new RelayCommand(async _ => await CreateProfileAsync(), _ => CanManageProfiles);
        DeleteProfileCommand = new RelayCommand(async _ => await DeleteSelectedProfileAsync(), _ => CanManageProfiles && Profiles.Count > 1 && SelectedProfile is not null);
        SnapBottomRightCommand = new RelayCommand(_ => SnapBottomRight());
    }

    public ObservableCollection<WorkItem> Items => _items;
    public ObservableCollection<string> ProductManagers => _productManagers;
    public ObservableCollection<string> Assignees => _assignees;
    public ObservableCollection<UserProfile> Profiles => _profiles;
    public ObservableCollection<UserRole> Roles => _roles;
    public ObservableCollection<string> Themes => _themes;

    public ICommand SyncCommand { get; }
    public ICommand OpenWorkItemCommand { get; }
    public ICommand ChangeStatusCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand CreateProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand SnapBottomRightCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            RaiseCommandStates();
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (_isConnected == value) return;
            _isConnected = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoadingTransitions
    {
        get => _isLoadingTransitions;
        private set
        {
            if (_isLoadingTransitions == value) return;
            _isLoadingTransitions = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value) return;
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public string LastSyncText
    {
        get => _lastSyncText;
        private set
        {
            if (_lastSyncText == value) return;
            _lastSyncText = value;
            OnPropertyChanged();
        }
    }

    public UserProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (ReferenceEquals(_selectedProfile, value))
                return;

            _selectedProfile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanManageProfiles));
            OnPropertyChanged(nameof(CurrentRoleLabel));
            OnPropertyChanged(nameof(CanEditSelectedWorkItem));
            RaiseCommandStates();

            if (value is not null && !_isApplyingProfile)
                _ = ChangeProfileAsync(value, true);
        }
    }

    public WorkItem? SelectedWorkItem
    {
        get => _selectedWorkItem;
        set
        {
            if (ReferenceEquals(_selectedWorkItem, value))
                return;

            _selectedWorkItem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanEditSelectedWorkItem));
            RaiseCommandStates();
            _ = LoadTransitionsForSelectionAsync();
        }
    }

    public string SelectedProductManager
    {
        get => _selectedProductManager;
        set
        {
            if (_selectedProductManager == value) return;
            _selectedProductManager = value;
            OnPropertyChanged();
            RebuildVisibleItems();
            QueueAutosave();
        }
    }

    public string SelectedAssignee
    {
        get => _selectedAssignee;
        set
        {
            if (_selectedAssignee == value) return;
            _selectedAssignee = value;
            OnPropertyChanged();
            RebuildVisibleItems();
            QueueAutosave();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            RebuildVisibleItems();
            QueueAutosave();
        }
    }

    public bool HideDone
    {
        get => _hideDone;
        set
        {
            if (_hideDone == value) return;
            _hideDone = value;
            OnPropertyChanged();
            RebuildVisibleItems();
            QueueAutosave();
        }
    }

    public bool ShowChangedOnly
    {
        get => _showChangedOnly;
        set
        {
            if (_showChangedOnly == value) return;
            _showChangedOnly = value;
            OnPropertyChanged();
            RebuildVisibleItems();
            QueueAutosave();
        }
    }

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set
        {
            if (_alwaysOnTop == value) return;
            _alwaysOnTop = value;
            OnPropertyChanged();
            QueueAutosave();
        }
    }

    public bool EnableChangeNotifications
    {
        get => _enableChangeNotifications;
        set
        {
            if (_enableChangeNotifications == value) return;
            _enableChangeNotifications = value;
            OnPropertyChanged();
            QueueAutosave();
        }
    }

    public bool EnableNotificationSound
    {
        get => _enableNotificationSound;
        set
        {
            if (_enableNotificationSound == value) return;
            _enableNotificationSound = value;
            OnPropertyChanged();
            QueueAutosave();
        }
    }

    public string SyncIntervalSecondsText
    {
        get => _syncIntervalSecondsText;
        set
        {
            if (_syncIntervalSecondsText == value) return;
            _syncIntervalSecondsText = value;
            OnPropertyChanged();
            StartSyncTimer();
            QueueAutosave();
        }
    }

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme == value) return;
            _selectedTheme = value;
            OnPropertyChanged();
            _themeService.ApplyTheme(_selectedTheme);
            QueueAutosave();
        }
    }

    public string NewProfileName
    {
        get => _newProfileName;
        set
        {
            if (_newProfileName == value) return;
            _newProfileName = value;
            OnPropertyChanged();
        }
    }

    public UserRole NewProfileRole
    {
        get => _newProfileRole;
        set
        {
            if (_newProfileRole == value) return;
            _newProfileRole = value;
            OnPropertyChanged();
        }
    }

    public string NewProfileManagedProductManager
    {
        get => _newProfileManagedProductManager;
        set
        {
            if (_newProfileManagedProductManager == value) return;
            _newProfileManagedProductManager = value;
            OnPropertyChanged();
        }
    }

    public string CurrentRoleLabel => SelectedProfile is null ? "No profile selected" : $"Role: {SelectedProfile.Role}";
    public bool CanManageProfiles => SelectedProfile?.Role == UserRole.Admin;
    public bool CanEditSelectedWorkItem => SelectedWorkItem is not null && CanEditWorkItem(SelectedWorkItem);

    public async Task InitializeAsync(Window window)
    {
        _window = window;
        await LoadProfilesAsync();
        await AutoConnectAndSyncAsync();
        StartSyncTimer();
    }

    public async Task SaveWindowSettingsAsync(Window? window)
    {
        if (_isApplyingProfile)
            return;

        _window = window ?? _window;
        await PersistCurrentProfileAsync();
    }

    public void RefreshGrid() => OnPropertyChanged(nameof(Items));

    public void ApplyManualSort(string sortField, ListSortDirection direction)
    {
        _sortField = sortField;
        _sortDirection = direction;
        RebuildVisibleItems();
        QueueAutosave();
    }

    private async Task LoadProfilesAsync()
    {
        var profiles = await _profileService.LoadProfilesAsync();
        Profiles.Clear();
        foreach (var profile in profiles)
            Profiles.Add(profile);

        var active = await _profileService.GetActiveProfileAsync();
        var selected = Profiles.FirstOrDefault(x => string.Equals(x.Id, active.Id, StringComparison.OrdinalIgnoreCase)) ?? Profiles.FirstOrDefault();
        if (selected is not null)
            await ChangeProfileAsync(selected, false);
    }

    private async Task ChangeProfileAsync(UserProfile profile, bool syncAfterChange)
    {
        if (_isChangingProfile)
            return;

        _isChangingProfile = true;
        try
        {
            await _profileService.SetActiveProfileAsync(profile.Id);
            ApplyProfile(profile);
            if (syncAfterChange && IsConnected)
                await SyncAsync();
        }
        finally
        {
            _isChangingProfile = false;
        }
    }

    private void ApplyProfile(UserProfile profile)
    {
        _isApplyingProfile = true;
        try
        {
            if (!ReferenceEquals(_selectedProfile, profile))
            {
                _selectedProfile = profile;
                OnPropertyChanged(nameof(SelectedProfile));
            }

            var settings = profile.Settings ?? new UserWidgetSettings();
            AlwaysOnTop = settings.AlwaysOnTop;
            EnableChangeNotifications = settings.EnableChangeNotifications;
            EnableNotificationSound = settings.EnableNotificationSound;
            HideDone = settings.HideDone;
            ShowChangedOnly = settings.ShowChangedOnly;
            SelectedTheme = string.IsNullOrWhiteSpace(settings.ThemeName) ? "JiraLight" : settings.ThemeName;
            SyncIntervalSecondsText = settings.SyncIntervalSeconds.ToString();
            SearchText = settings.SearchText ?? string.Empty;
            SelectedProductManager = string.IsNullOrWhiteSpace(settings.SelectedProductManager) ? "All" : settings.SelectedProductManager;
            SelectedAssignee = string.IsNullOrWhiteSpace(settings.SelectedAssignee) ? "All" : settings.SelectedAssignee;
            _sortField = string.IsNullOrWhiteSpace(settings.SortField) ? "Updated" : settings.SortField;
            _sortDirection = settings.SortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending;

            _themeService.ApplyTheme(SelectedTheme);
            ApplyWindowSettings(settings);
            OnPropertyChanged(nameof(CanManageProfiles));
            OnPropertyChanged(nameof(CurrentRoleLabel));
            OnPropertyChanged(nameof(CanEditSelectedWorkItem));
        }
        finally
        {
            _isApplyingProfile = false;
        }

        RebuildVisibleItems();
        RaiseCommandStates();
    }

    private void ApplyWindowSettings(UserWidgetSettings settings)
    {
        if (_window is null)
            return;

        _window.Left = settings.WindowLeft;
        _window.Top = settings.WindowTop;
        _window.Width = Math.Max(980, settings.WindowWidth);
        _window.Height = Math.Max(640, settings.WindowHeight);
    }

    private async Task AutoConnectAndSyncAsync()
    {
        try
        {
            StatusText = "Connecting to Jira...";
            await _oauthService.EnsureLoggedInAsync();
            await _oauthService.GetCloudIdAsync();
            IsConnected = true;
            StatusText = "Connected";
            await SyncAsync();
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusText = $"Connection failed: {ex.Message}";
        }
    }

    private async Task SyncAsync()
    {
        if (SelectedProfile is null || IsBusy)
            return;

        IsBusy = true;
        try
        {
            StatusText = $"Syncing Jira for {SelectedProfile.DisplayName}...";
            var result = await _dashboardService.LoadDashboardAsync(SelectedProfile.Id);

            List<string> remotePmOptions;
            try
            {
                remotePmOptions = await _dashboardService.GetAllProductManagerOptionsAsync();
            }
            catch
            {
                remotePmOptions = result.ProductManagers;
            }

            _allItems = result.Items;
            RebuildFilterOptions(remotePmOptions, result.Assignees);
            RebuildVisibleItems();
            LastSyncText = $"Last sync: {DateTime.Now:dd MMM yyyy HH:mm:ss}";
            StatusText = $"Loaded {_allItems.Count} jobs";
            RaiseChangeNotifications(_allItems);
        }
        catch (Exception ex)
        {
            StatusText = $"Sync failed: {ex.Message}";
            _toastService.ShowError("Jira Desktop", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildFilterOptions(IEnumerable<string> productManagers, IEnumerable<string> assignees)
    {
        var pmSelection = SelectedProductManager;
        var assigneeSelection = SelectedAssignee;

        var pmValues = productManagers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        if (SelectedProfile?.Role == UserRole.ProductManager && !string.IsNullOrWhiteSpace(SelectedProfile.ManagedProductManager) &&
            pmValues.All(x => !string.Equals(x, SelectedProfile.ManagedProductManager, StringComparison.OrdinalIgnoreCase)))
        {
            pmValues.Add(SelectedProfile.ManagedProductManager);
            pmValues = pmValues.OrderBy(x => x).ToList();
        }

        ProductManagers.Clear();
        ProductManagers.Add("All");
        foreach (var value in pmValues)
            ProductManagers.Add(value);

        Assignees.Clear();
        Assignees.Add("All");
        foreach (var value in assignees.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            Assignees.Add(value);

        _isApplyingProfile = true;
        try
        {
            SelectedProductManager = ProductManagers.Contains(pmSelection) ? pmSelection : DefaultSelectedProductManager();
            SelectedAssignee = Assignees.Contains(assigneeSelection) ? assigneeSelection : "All";
        }
        finally
        {
            _isApplyingProfile = false;
        }
    }

    private string DefaultSelectedProductManager()
    {
        if (SelectedProfile?.Role == UserRole.ProductManager && !string.IsNullOrWhiteSpace(SelectedProfile.ManagedProductManager))
            return ProductManagers.FirstOrDefault(x => string.Equals(x, SelectedProfile.ManagedProductManager, StringComparison.OrdinalIgnoreCase)) ?? "All";

        return "All";
    }

    private IEnumerable<WorkItem> ApplyFilters(IEnumerable<WorkItem> source)
    {
        var query = source;

        if (SelectedProductManager != "All")
            query = query.Where(item => SplitCsv(item.ProductManager).Any(pm => string.Equals(pm, SelectedProductManager, StringComparison.OrdinalIgnoreCase)));

        if (SelectedAssignee != "All")
            query = query.Where(item => string.Equals(item.Assignee, SelectedAssignee, StringComparison.OrdinalIgnoreCase));

        if (HideDone)
            query = query.Where(item => !string.Equals(item.Status, "Done", StringComparison.OrdinalIgnoreCase));

        if (ShowChangedOnly)
            query = query.Where(item => item.HasChanges);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            query = query.Where(item =>
                item.Key.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Summary.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Assignee.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Status.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.ProductManager.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    private IEnumerable<WorkItem> ApplySort(IEnumerable<WorkItem> source)
    {
        return (_sortField, _sortDirection) switch
        {
            ("Key", ListSortDirection.Ascending) => source.OrderBy(ParseKeyNumber).ThenBy(x => x.Key),
            ("Key", _) => source.OrderByDescending(ParseKeyNumber).ThenByDescending(x => x.Key),
            ("Summary", ListSortDirection.Ascending) => source.OrderBy(x => x.Summary).ThenByDescending(x => x.Updated),
            ("Summary", _) => source.OrderByDescending(x => x.Summary).ThenByDescending(x => x.Updated),
            ("Assignee", ListSortDirection.Ascending) => source.OrderBy(x => x.Assignee).ThenByDescending(x => x.Updated),
            ("Assignee", _) => source.OrderByDescending(x => x.Assignee).ThenByDescending(x => x.Updated),
            ("Priority", ListSortDirection.Ascending) => source.OrderBy(x => x.Priority).ThenByDescending(x => x.Updated),
            ("Priority", _) => source.OrderByDescending(x => x.Priority).ThenByDescending(x => x.Updated),
            ("Status", ListSortDirection.Ascending) => source.OrderBy(x => x.Status).ThenByDescending(x => x.Updated),
            ("Status", _) => source.OrderByDescending(x => x.Status).ThenByDescending(x => x.Updated),
            ("DueDate", ListSortDirection.Ascending) => source.OrderBy(x => x.DueDate ?? DateTime.MaxValue).ThenByDescending(x => x.Updated),
            ("DueDate", _) => source.OrderByDescending(x => x.DueDate ?? DateTime.MinValue).ThenByDescending(x => x.Updated),
            ("ProductManager", ListSortDirection.Ascending) => source.OrderBy(x => x.ProductManager).ThenByDescending(x => x.Updated),
            ("ProductManager", _) => source.OrderByDescending(x => x.ProductManager).ThenByDescending(x => x.Updated),
            (_, ListSortDirection.Ascending) => source.OrderBy(x => x.Updated).ThenBy(x => x.Key),
            _ => source.OrderByDescending(x => x.Updated).ThenByDescending(x => x.Key)
        };
    }

    private void RebuildVisibleItems()
    {
        var visible = ApplySort(ApplyFilters(_allItems)).ToList();
        Items.Clear();
        foreach (var item in visible)
            Items.Add(item);
    }

    private async Task LoadTransitionsForSelectionAsync()
    {
        if (SelectedWorkItem is null)
            return;

        SelectedWorkItem.AvailableTransitions.Clear();
        SelectedWorkItem.SelectedTransitionId = string.Empty;

        if (!CanEditSelectedWorkItem || !IsConnected)
            return;

        IsLoadingTransitions = true;
        try
        {
            var transitions = await _jiraService.GetAvailableTransitionsAsync(SelectedWorkItem.Key);
            foreach (var transition in transitions)
                SelectedWorkItem.AvailableTransitions.Add(transition);

            if (SelectedWorkItem.AvailableTransitions.Count == 1)
                SelectedWorkItem.SelectedTransitionId = SelectedWorkItem.AvailableTransitions[0].Id;
        }
        catch (Exception ex)
        {
            StatusText = $"Transition load failed: {ex.Message}";
        }
        finally
        {
            IsLoadingTransitions = false;
            RaiseCommandStates();
        }
    }

    private async Task ChangeSelectedStatusAsync()
    {
        if (SelectedWorkItem is null)
            return;
        if (!CanEditSelectedWorkItem)
        {
            StatusText = "This profile cannot change the selected job status.";
            return;
        }
        if (string.IsNullOrWhiteSpace(SelectedWorkItem.SelectedTransitionId))
        {
            StatusText = "Choose a target status first.";
            return;
        }

        IsBusy = true;
        try
        {
            var selectedKey = SelectedWorkItem.Key;
            var transition = SelectedWorkItem.AvailableTransitions.FirstOrDefault(x => x.Id == SelectedWorkItem.SelectedTransitionId);
            var targetName = transition?.Name ?? "selected status";
            StatusText = $"Updating {SelectedWorkItem.Key}...";
            await _jiraService.UpdateWorkItemStatusAsync(SelectedWorkItem.Key, SelectedWorkItem.SelectedTransitionId);
            _toastService.ShowSuccess("Jira Desktop", $"{SelectedWorkItem.Key} moved to {targetName}.");
            IsBusy = false;
            await SyncAsync();
            var refreshedSelection = Items.FirstOrDefault(x => string.Equals(x.Key, selectedKey, StringComparison.OrdinalIgnoreCase));
            SelectedWorkItem = refreshedSelection;
            StatusText = $"Updated {selectedKey} to {targetName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Status update failed: {ex.Message}";
            _toastService.ShowError("Jira Desktop", ex.Message);
        }
        finally
        {
            if (IsBusy)
                IsBusy = false;
        }
    }

    private void RaiseChangeNotifications(IReadOnlyCollection<WorkItem> items)
    {
        if (!EnableChangeNotifications || items.Count == 0)
            return;

        var watchedPm = SelectedProductManager;
        var changedItems = items.Where(item =>
            item.HasDetectedChanges &&
            (watchedPm == "All" || SplitCsv(item.ProductManager).Any(pm => string.Equals(pm, watchedPm, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        if (changedItems.Count == 0)
            return;

        var keys = string.Join(", ", changedItems.Take(3).Select(x => x.Key));
        var moreSuffix = changedItems.Count > 3 ? "..." : string.Empty;
        var message = watchedPm == "All"
            ? $"{changedItems.Count} job(s) changed: {keys}{moreSuffix}"
            : $"{changedItems.Count} {watchedPm} job(s) changed: {keys}{moreSuffix}";

        StatusText = message;
        _toastService.ShowInfo("Jira Desktop", message);
        if (EnableNotificationSound)
            SystemSounds.Asterisk.Play();
    }

    private async Task SaveSelectedProfileAsync()
    {
        if (SelectedProfile is null)
            return;

        await PersistCurrentProfileAsync();
        await _profileService.UpsertProfileAsync(SelectedProfile);
        OnPropertyChanged(nameof(CanManageProfiles));
        OnPropertyChanged(nameof(CurrentRoleLabel));
        _toastService.ShowSuccess("Jira Desktop", $"Saved profile for {SelectedProfile.DisplayName}.");
        await LoadProfilesAsync();
    }

    private async Task CreateProfileAsync()
    {
        if (!CanManageProfiles)
            return;

        var displayName = string.IsNullOrWhiteSpace(NewProfileName) ? "New User" : NewProfileName.Trim();
        var settings = new UserWidgetSettings();
        if (NewProfileRole == UserRole.ProductManager && !string.IsNullOrWhiteSpace(NewProfileManagedProductManager))
            settings.SelectedProductManager = NewProfileManagedProductManager.Trim();

        var profile = new UserProfile
        {
            Id = Guid.NewGuid().ToString("n"),
            DisplayName = displayName,
            Role = NewProfileRole,
            ManagedProductManager = NewProfileManagedProductManager?.Trim() ?? string.Empty,
            Settings = settings
        };

        await _profileService.UpsertProfileAsync(profile);
        NewProfileName = string.Empty;
        NewProfileManagedProductManager = string.Empty;
        NewProfileRole = UserRole.Viewer;
        await LoadProfilesAsync();
        var created = Profiles.FirstOrDefault(x => string.Equals(x.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
        if (created is not null)
            SelectedProfile = created;
    }

    private async Task DeleteSelectedProfileAsync()
    {
        if (SelectedProfile is null || !CanManageProfiles)
            return;

        var deletingId = SelectedProfile.Id;
        await _profileService.DeleteProfileAsync(deletingId);
        await LoadProfilesAsync();
    }

    private async Task PersistCurrentProfileAsync()
    {
        if (SelectedProfile is null)
            return;

        SelectedProfile.Settings = new UserWidgetSettings
        {
            AlwaysOnTop = AlwaysOnTop,
            EnableChangeNotifications = EnableChangeNotifications,
            EnableNotificationSound = EnableNotificationSound,
            HideDone = HideDone,
            ShowChangedOnly = ShowChangedOnly,
            ThemeName = SelectedTheme,
            SyncIntervalSeconds = Math.Max(15, ParseInt(SyncIntervalSecondsText, 60)),
            SearchText = SearchText,
            SelectedProductManager = SelectedProductManager,
            SelectedAssignee = SelectedAssignee,
            SortField = _sortField,
            SortAscending = _sortDirection == ListSortDirection.Ascending,
            WindowLeft = _window?.Left ?? SelectedProfile.Settings.WindowLeft,
            WindowTop = _window?.Top ?? SelectedProfile.Settings.WindowTop,
            WindowWidth = _window?.Width ?? SelectedProfile.Settings.WindowWidth,
            WindowHeight = _window?.Height ?? SelectedProfile.Settings.WindowHeight
        };

        await _profileService.UpsertProfileAsync(SelectedProfile);
    }

    private void QueueAutosave()
    {
        if (_isApplyingProfile || SelectedProfile is null)
            return;

        _autosaveCts?.Cancel();
        _autosaveCts = new CancellationTokenSource();
        var token = _autosaveCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (!token.IsCancellationRequested)
                    await PersistCurrentProfileAsync();
            }
            catch
            {
            }
        }, token);
    }

    private void StartSyncTimer()
    {
        _syncTimer?.Stop();
        _syncTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(15, ParseInt(SyncIntervalSecondsText, 60)))
        };
        _syncTimer.Tick += async (_, _) =>
        {
            if (!IsBusy && IsConnected)
                await SyncAsync();
        };
        _syncTimer.Start();
    }

    private bool CanEditWorkItem(WorkItem item)
    {
        if (SelectedProfile is null)
            return false;

        return SelectedProfile.Role switch
        {
            UserRole.Admin => true,
            UserRole.ProductManager => !string.IsNullOrWhiteSpace(SelectedProfile.ManagedProductManager) &&
                                       SplitCsv(item.ProductManager).Any(pm => string.Equals(pm, SelectedProfile.ManagedProductManager, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private void SnapBottomRight()
    {
        if (_window is null)
            return;

        var area = SystemParameters.WorkArea;
        _window.Left = area.Right - _window.Width - 16;
        _window.Top = area.Bottom - _window.Height - 16;
        _ = SaveWindowSettingsAsync(_window);
    }

    private static IEnumerable<string> SplitCsv(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int ParseKeyNumber(WorkItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Key))
            return int.MinValue;

        var dash = item.Key.LastIndexOf('-');
        if (dash < 0 || dash >= item.Key.Length - 1)
            return int.MinValue;

        return int.TryParse(item.Key[(dash + 1)..], out var value) ? value : int.MinValue;
    }

    private static int ParseInt(string? value, int fallback)
        => int.TryParse(value, out var parsed) ? parsed : fallback;

    private void OpenWorkItemInBrowser(WorkItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Url))
            return;

        try
        {
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

    private void RaiseCommandStates()
    {
        (SyncCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ChangeStatusCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CreateProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
