# Architecture: JiraDesktop.Core & JiraDesktop.Wpf

This document describes the two-project architecture of the **JiraDesktop** application — a WPF desktop widget that connects to the Atlassian Jira REST API and displays your team's issues directly on your desktop.

---

## 1. High-Level Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                      JiraDesktop.Wpf                        │
│                                                             │
│  App.xaml / App.xaml.cs  (DI bootstrap, startup/shutdown)  │
│  MainWindow.xaml / .cs   (code-behind, event routing)      │
│  MainWindowViewModel     (all presentation state + logic)  │
│  Commands / Converters   (WPF helpers)                      │
│  Services:               ThemeService, ToastService         │
│  Themes:                 JiraLight.xaml, JiraDark.xaml      │
│                                                             │
│  ↓  depends on                                              │
└─────────────────────────────────────────────────────────────┘
                           │
                           │  project reference
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                     JiraDesktop.Core                        │
│                                                             │
│  Configuration:  JiraOptions, JiraOAuthOptions              │
│  Interfaces:     IJiraService, IJiraOAuthService,           │
│                  IWorkItemCacheService                       │
│  Models:         WorkItem, WorkItemSnapshot, DashboardResult│
│                  WorkItemFieldChange, UserWidgetSettings,   │
│                  JiraOAuthTokenResponse, JiraAccessibleResource│
│  Services:       JiraService, JiraOAuthService,             │
│                  DashboardService, FileWorkItemCacheService, │
│                  UserSettingsService                        │
│                                                             │
│  ← No WPF references; plain .NET class library              │
└─────────────────────────────────────────────────────────────┘
                           │
                           │  HTTP (Atlassian REST API)
                           ▼
                  ┌────────────────┐
                  │  Jira Cloud    │
                  │  REST API v3   │
                  └────────────────┘
```

**Key principle:** `JiraDesktop.Core` has zero references to WPF or any UI framework. All Jira API calls and data transformations live there. `JiraDesktop.Wpf` depends on Core but Core never depends on Wpf.

---

## 2. Folder / Project Structure (Post-Refactor)

```
JiraDesktop/
├── JiraDesktop.sln
│
├── JiraDesktop.Core/                  ← Class library (.NET 10)
│   ├── JiraDesktop.Core.csproj
│   ├── Configuration/
│   │   ├── JiraOptions.cs
│   │   └── JiraOAuthOptions.cs
│   ├── Interfaces/
│   │   ├── IJiraService.cs
│   │   ├── IJiraOAuthService.cs
│   │   └── IWorkItemCacheService.cs
│   ├── Models/
│   │   ├── WorkItem.cs
│   │   ├── WorkItemSnapshot.cs
│   │   ├── WorkItemFieldChange.cs
│   │   ├── DashboardResult.cs
│   │   ├── UserWidgetSettings.cs
│   │   ├── JiraOAuthTokenResponse.cs
│   │   └── JiraAccessibleResource.cs
│   └── Services/
│       ├── JiraService.cs
│       ├── JiraOAuthService.cs
│       ├── DashboardService.cs
│       ├── FileWorkItemCacheService.cs
│       └── UserSettingsService.cs
│
└── JiraDesktop.Wpf/                   ← WPF application (.NET 10-windows)
    ├── JiraDesktop.Wpf.csproj
    ├── App.xaml
    ├── App.xaml.cs
    ├── AssemblyInfo.cs
    ├── appsettings.json
    ├── MainWindow.xaml
    ├── MainWindow.xaml.cs
    ├── MainWindowViewModel.cs
    ├── Commands/
    │   └── RelayCommand.cs
    ├── Converters/
    │   ├── BooleanToVisibilityConverter.cs
    │   ├── DueDateStateConverter.cs
    │   └── StatusToBrushConverter.cs
    ├── Services/
    │   ├── ThemeService.cs
    │   └── ToastService.cs
    └── Themes/
        ├── JiraLight.xaml
        └── JiraDark.xaml
```

---

## 3. Core Class Catalog

### Configuration

#### `JiraOptions`
| | |
|---|---|
| **Namespace** | `JiraDesktop.Core.Configuration` |
| **Purpose** | Strongly-typed options for the Jira project and API behaviour. Bound from the `"Jira"` section of `appsettings.json`. |
| **Key Properties** | `BaseUrl`, `ProjectKey`, `ProductManagerFieldId`, `MaxResults`, `ProjectId`, `ProductManagerFieldContextId` |
| **Dependencies** | None |

#### `JiraOAuthOptions`
| | |
|---|---|
| **Namespace** | `JiraDesktop.Core.Configuration` |
| **Purpose** | OAuth 2.0 (Authorization Code) configuration for authenticating with the Atlassian identity platform. Bound from `"JiraOAuth"` in `appsettings.json`. |
| **Key Properties** | `ClientId`, `ClientSecret`, `RedirectUri`, `Scopes`, `AuthUrl`, `TokenUrl`, `Audience`, `SiteUrl` |
| **Dependencies** | None |

---

### Interfaces

#### `IJiraService`
| | |
|---|---|
| **Namespace** | `JiraDesktop.Core.Interfaces` |
| **Purpose** | Abstraction over the Jira REST API for fetching work items and custom-field option lists. |
| **Key Methods** | `GetWorkItemsAsync(productManager, assignee, ct)`, `GetAllProductManagerOptionsAsync(ct)` |
| **Implemented by** | `JiraService` |

#### `IJiraOAuthService`
| | |
|---|---|
| **Namespace** | `JiraDesktop.Core.Interfaces` |
| **Purpose** | Manages the full OAuth 2.0 lifecycle: interactive browser login, silent token refresh, and Atlassian Cloud ID resolution. |
| **Key Methods** | `EnsureLoggedInAsync(ct)`, `GetValidAccessTokenAsync(ct)`, `GetCloudIdAsync(ct)` |
| **Implemented by** | `JiraOAuthService` |

#### `IWorkItemCacheService`
| | |
|---|---|
| **Namespace** | `JiraDesktop.Core.Interfaces` |
| **Purpose** | Persists lightweight `WorkItemSnapshot` records between syncs, enabling field-change detection. |
| **Key Methods** | `LoadAsync(ct)`, `SaveAsync(snapshots, ct)` |
| **Implemented by** | `FileWorkItemCacheService` |

---

### Models

#### `WorkItem`
Domain model representing a single Jira issue on the dashboard. Contains display fields (Key, Summary, Assignee, Priority, Status, DueDate, Updated, ProductManager, Url) plus runtime state (Changes list, HasDetectedChanges, IsPulseActive).

#### `WorkItemSnapshot`
Lightweight serializable copy of a `WorkItem`'s key fields written to disk after each sync. Used as the baseline for next-sync change detection.

#### `WorkItemFieldChange`
Records one changed field on a `WorkItem`: field name, old value, new value, and detection timestamp.

#### `DashboardResult`
Aggregate returned by `DashboardService.LoadDashboardAsync`: the ordered `Items` list plus distinct `ProductManagers` and `Assignees` for populating filter dropdowns.

#### `UserWidgetSettings`
All user-configurable preferences: window geometry, sync interval, page size, theme, notification flags, filter presets. Serialised to `%LOCALAPPDATA%\JiraDesktop\settings.user.json`.

#### `JiraOAuthTokenResponse`
Stores the OAuth access token, refresh token, expiry duration, and a computed `ExpiresAt` UTC timestamp.

#### `JiraAccessibleResource`
DTO representing one Atlassian Cloud site from the `accessible-resources` endpoint: `Id` (Cloud ID), `Url`, `Name`.

---

### Services

#### `JiraService`
| | |
|---|---|
| **Namespace** | `JiraDesktop.Core.Services` |
| **Purpose** | Implements `IJiraService`. Executes paginated Jira REST API searches and maps JSON responses to `WorkItem` domain objects. |
| **Key Methods** | `GetWorkItemsAsync` — paginated JQL search; `GetAllProductManagerOptionsAsync` — custom-field option enumeration; `MapIssue` (private) — JSON → `WorkItem`; `BuildJql` (private) — JQL construction |
| **Dependencies** | `HttpClient`, `IOptions<JiraOptions>`, `IJiraOAuthService` |

#### `JiraOAuthService`
| | |
|---|---|
| **Namespace** | `JiraDesktop.Core.Services` |
| **Purpose** | Implements `IJiraOAuthService`. Handles the Authorization Code flow: launches a local `HttpListener` to catch the callback, exchanges the code for tokens, refreshes silently on subsequent starts, and caches the Atlassian Cloud ID to disk. |
| **Key Methods** | `EnsureLoggedInAsync`, `GetValidAccessTokenAsync`, `GetCloudIdAsync`, `RunInteractiveLoginAsync` (private), `ExchangeCodeForTokenAsync` (private), `RefreshTokenAsync` (private) |
| **Dependencies** | `HttpClient`, `IOptions<JiraOAuthOptions>` |
| **Persists to** | `%LOCALAPPDATA%\JiraDesktop\oauth-token.json`, `%LOCALAPPDATA%\JiraDesktop\oauth-cloudid.txt` |

#### `DashboardService`
| | |
|---|---|
| **Namespace** | `JiraDesktop.Core.Services` |
| **Purpose** | Orchestrates a full dashboard refresh: fetches items, compares against the snapshot cache to populate `WorkItem.Changes`, saves a new snapshot, and returns a `DashboardResult`. |
| **Key Methods** | `LoadDashboardAsync(productManager, assignee, ct)`, `GetAllProductManagerOptionsAsync(ct)` |
| **Dependencies** | `IJiraService`, `IWorkItemCacheService` |

#### `FileWorkItemCacheService`
| | |
|---|---|
| **Namespace** | `JiraDesktop.Core.Services` |
| **Purpose** | Implements `IWorkItemCacheService`. Reads and writes a JSON dictionary of `WorkItemSnapshot` objects keyed by Jira issue key. |
| **Key Methods** | `LoadAsync(ct)`, `SaveAsync(snapshots, ct)` |
| **Persists to** | `%LOCALAPPDATA%\JiraDesktop\workitem-cache.json` |

#### `UserSettingsService`
| | |
|---|---|
| **Namespace** | `JiraDesktop.Core.Services` |
| **Purpose** | Loads and saves `UserWidgetSettings` as indented JSON in the user's local app data folder. Returns a default instance if no file exists yet. |
| **Key Methods** | `LoadAsync(ct)`, `SaveAsync(settings, ct)` |
| **Persists to** | `%LOCALAPPDATA%\JiraDesktop\settings.user.json` |

---

## 4. Jira Integration Map

### Authentication (OAuth 2.0 Authorization Code)

| Step | Detail |
|---|---|
| **Auth endpoint** | `https://auth.atlassian.com/authorize` |
| **Token endpoint** | `https://auth.atlassian.com/oauth/token` |
| **Scopes** | `read:jira read:jira-user offline_access` |
| **Redirect URI** | `http://127.0.0.1:51234/callback` (local `HttpListener`) |
| **Client credentials** | Stored in `appsettings.json` → `JiraOAuth.ClientId` / `ClientSecret` |
| **Token storage** | `%LOCALAPPDATA%\JiraDesktop\oauth-token.json` |
| **Cloud ID resolution** | `GET https://api.atlassian.com/oauth/token/accessible-resources` — returns the list of Atlassian sites the user has access to; the first match for `JiraOAuth.SiteUrl` is cached to `oauth-cloudid.txt` |

### Work Item Search

| Aspect | Detail |
|---|---|
| **Endpoint** | `GET https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/search/jql` |
| **Authentication** | `Authorization: ****** |
| **JQL template** | `project = "{ProjectKey}" [AND "Product Managers" = "…"] [AND assignee = "…"] ORDER BY created DESC` |
| **Fields fetched** | `summary`, `assignee` (→ `displayName`), `priority` (→ `name`), `status` (→ `name`), `duedate`, `updated`, `{ProductManagerFieldId}` (custom field) |
| **Pagination** | `startAt` / `maxResults` loop until `startAt >= total` |
| **Page size** | 100 (Jira maximum per request) |

### Product Manager Options

| Aspect | Detail |
|---|---|
| **Endpoint** | `GET https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/field/{fieldId}/context/{contextId}/option` |
| **Authentication** | `Authorization: ****** |
| **Returns** | All valid option values for the configured custom field (used to populate the PM filter dropdown) |

### Request / Response Flow

```
JiraOAuthService.GetValidAccessTokenAsync()
    └── Loads token from disk
    └── If expired → RefreshTokenAsync() or RunInteractiveLoginAsync()
    └── Returns ******

JiraService.GetWorkItemsAsync()
    ├── GetValidAccessTokenAsync()      → ******
    ├── GetCloudIdAsync()               → cloudId (from disk cache or API)
    ├── BuildJql()                      → JQL string
    └── Loop: GET /search/jql?jql=...
            └── Parse JSON array "issues"
                └── MapIssue() → WorkItem (key, summary, assignee, priority,
                                           status, duedate, updated, PM field, url)
```

---

## 5. Data Flow Walkthrough

```
App startup
  │
  ├─ App.xaml.cs builds IHost, registers all DI services
  │
  ├─ MainWindow is created → MainWindowViewModel injected
  │
  └─ Window.Loaded event fires
        │
        ├─ UserSettingsService.LoadAsync()       restore window geometry, theme, prefs
        ├─ ThemeService.ApplyTheme()             swap ResourceDictionary
        │
        └─ AutoConnectAndSyncAsync()
              │
              ├─ JiraOAuthService.EnsureLoggedInAsync()
              │     ├─ Check disk token → still valid? → done
              │     ├─ Expired + refresh token? → RefreshTokenAsync()
              │     └─ No token → launch browser → catch /callback → ExchangeCode
              │
              ├─ JiraOAuthService.GetCloudIdAsync()
              │     └─ accessible-resources → cache cloudId to disk
              │
              └─ DashboardService.LoadDashboardAsync(null, null)
                    │
                    ├─ FileWorkItemCacheService.LoadAsync()   previous snapshots
                    ├─ JiraService.GetWorkItemsAsync()        current from API
                    │     └─ paginated /search/jql → MapIssue() × N
                    │
                    ├─ Compare each WorkItem against snapshot → populate Changes
                    │
                    ├─ FileWorkItemCacheService.SaveAsync()   save new snapshots
                    │
                    └─ Return DashboardResult
                          │
                          └─ MainWindowViewModel
                                ├─ Sort items by key number descending
                                ├─ RebuildFilterListsAsync()  PM + Assignee dropdowns
                                ├─ RebuildVisibleItems()      apply filters + paginate
                                └─ RaiseWatchNotifications()  toast + sound if changed
                                      └─ ToastService.ShowInfo()
```

---

## 6. Extension Points / Future Additions

| Feature | Where to implement | Notes |
|---|---|---|
| **Full-text / JQL search** | `JiraService.BuildJql()` | Add a `searchText` parameter and append `text ~ "…"` to the JQL clause. |
| **Background sync with retry / back-off** | New `SyncBackgroundService` in `.Core` | Implement `IHostedService`; use `Polly` for transient HTTP retries with exponential back-off. |
| **Offline cache / read-only mode** | `FileWorkItemCacheService` | Expose a `GetLastKnownAsync()` method so the VM can display cached data while the API is unavailable. |
| **Desktop notifications per issue** | `ToastService` in `.Wpf` | Add an overload that accepts a `WorkItem` and renders rich toast with action buttons. |
| **Unit / integration tests** | New `JiraDesktop.Tests` project | Test `DashboardService` with mock `IJiraService` and `IWorkItemCacheService`. Test `JiraService.MapIssue()` with static JSON fixtures. |
| **Typed JQL query builder** | New `JiraDesktop.Core.Query` namespace | Fluent API: `JqlBuilder.ForProject("PROJ").AssignedTo("alice").OrderBy(JqlField.Created).Build()`. |
| **Pagination improvements** | `DashboardService` / ViewModel | Expose `total` count from the API; allow the VM to navigate large datasets without re-fetching all pages. |
| **Telemetry / diagnostics** | `.Core` services | Inject `ILogger<T>` via `Microsoft.Extensions.Logging` for structured log output (already compatible with the Generic Host). |
| **Role-based / multi-project views** | `JiraOptions` + ViewModel | Support a list of project keys; fan-out queries and merge results. |
| **Plugin / module architecture** | `JiraDesktop.Core.Interfaces` | Define an `IDashboardPlugin` interface; scan for implementations at startup using `AssemblyLoadContext`. |
| **Settings UI revamp** | `.Wpf` only | Move the drawer into a dedicated `SettingsWindow` or `UserControl` and expose more configuration knobs without touching Core. |
| **Auto-update** | `.Wpf` startup | Check a GitHub Releases feed for newer versions and prompt the user. |

---

## 7. Developer Guidance

### Where to add a new Jira API integration

1. Add or extend an **interface** in `JiraDesktop.Core/Interfaces/` (e.g. `ISprintService`).
2. Implement the interface in `JiraDesktop.Core/Services/`.
3. Add any new request/response **models or DTOs** to `JiraDesktop.Core/Models/`.
4. Register the implementation in `App.xaml.cs` (`services.AddHttpClient<ISprintService, SprintService>()`).
5. Inject the interface into `DashboardService` or `MainWindowViewModel` as needed.

### Where to add a new UI feature

1. All **state and logic** goes into `MainWindowViewModel` (or a new child ViewModel).
2. All **visual markup** goes into `MainWindow.xaml` or a new `UserControl`.
3. WPF-specific helpers (converters, value converters, styles) belong in `JiraDesktop.Wpf/Converters/` or `Themes/`.
4. WPF-specific platform services (notifications, tray icon, window chrome) belong in `JiraDesktop.Wpf/Services/`.

### Do / Don't boundaries

| ✅ DO in `.Core` | ❌ DON'T in `.Core` |
|---|---|
| HTTP requests to Jira | Reference `System.Windows.*` |
| JSON parsing and DTO mapping | Reference WPF types (`Dispatcher`, `Application`) |
| Business logic and domain rules | Include UI-only concerns (visibility, brushes) |
| File I/O for settings and cache | Use `System.Media` or toast APIs |
| Logging via `ILogger<T>` | Hard-code UI strings that belong in XAML |

| ✅ DO in `.Wpf` | ❌ DON'T in `.Wpf` |
|---|---|
| Data binding and MVVM | Make direct HTTP calls |
| WPF converters and styles | Contain Jira API business logic |
| DI host bootstrap and wiring | Parse raw JSON from Jira |
| Platform notifications / sound | Know about OAuth internals |
| Theme switching | Duplicate models already in `.Core` |
