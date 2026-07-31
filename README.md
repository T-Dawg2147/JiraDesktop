# JiraDesktop

A lightweight WPF desktop widget that connects to your company's Jira Cloud instance via the Atlassian REST API and displays your team's issues directly on your Windows desktop.

## Features

- Connects to Jira Cloud using OAuth 2.0 (browser-based login, silent refresh)
- Displays issues with key, summary, assignee, priority, status, and due date
- Filters by Product Manager, assignee, search text, and "Hide Done"
- Highlights changed fields between syncs with pulse animations and toast notifications
- Dark / light themes, configurable sync interval, and paginated grid
- Persists window position, filters, and theme between sessions

## Architecture

JiraDesktop follows a clean **two-project** separation:

| Project | Role |
|---|---|
| `JiraDesktop.Core` | All Jira API access, OAuth, domain models, business logic, caching. Zero WPF references. |
| `JiraDesktop.Wpf` | WPF UI, MVVM view models, converters, themes, and DI bootstrap. Depends on Core. |

For a full description of classes, the Jira integration map, data-flow walkthrough, and extension points, see:

📄 **[docs/ARCHITECTURE_CORE_WPF.md](docs/ARCHITECTURE_CORE_WPF.md)**

## Quick Start

1. **Register an Atlassian OAuth 2.0 app** at [developer.atlassian.com](https://developer.atlassian.com/console/myapps/) with the callback URL `http://127.0.0.1:51234/callback` and scopes `read:jira read:jira-user offline_access`.

2. **Edit `JiraDesktop.Wpf/appsettings.json`** with your values:
   ```json
   {
     "Jira": {
       "BaseUrl": "https://your-org.atlassian.net",
       "ProjectKey": "PROJ",
       "ProductManagerFieldId": "customfield_10055",
       "ProductManagerFieldContextId": 123456
     },
     "JiraOAuth": {
       "ClientId": "your-client-id",
       "ClientSecret": "your-client-secret",
       "SiteUrl": "https://your-org.atlassian.net"
     }
   }
   ```

3. **Build and run**:
   ```sh
   dotnet build JiraDesktop.sln
   dotnet run --project JiraDesktop.Wpf
   ```
   Your browser will open for the first-time OAuth login.

## Requirements

- Windows 10 / 11 (WPF)
- .NET 10 SDK

## Project Structure

```
JiraDesktop/
├── JiraDesktop.sln
├── JiraDesktop.Core/        ← class library: Jira API, models, services
├── JiraDesktop.Wpf/         ← WPF application: UI, view models, themes
└── docs/
    └── ARCHITECTURE_CORE_WPF.md
```
