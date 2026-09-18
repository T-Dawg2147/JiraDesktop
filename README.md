# JiraDesktop

JiraDesktop is a WPF desktop tracker for a single Jira project with local user profiles and role-aware actions.

## What it does

- Displays all jobs for the configured Jira project in one desktop grid
- Supports local user accounts with `Admin`, `ProductManager`, and `Viewer` roles
- Persists filters, theme, sorting, window position, and notification preferences per user profile
- Lets admins update any job status and lets product managers update jobs that match their assigned Product Manager value
- Opens Jira issues directly from the issue key in the grid
- Sends desktop notifications when changed jobs match the currently selected Product Manager filter

## Roles

- **Admin**: manage local user accounts and update any job status
- **ProductManager**: update job statuses only for jobs whose Product Manager matches the profile's managed Product Manager value
- **Viewer**: read-only access

## Configuration

Update `JiraDesktop.Wpf/appsettings.json` with your Jira values.

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
    "SiteUrl": "https://your-org.atlassian.net",
    "Scopes": "read:jira read:jira-user write:jira-work offline_access"
  }
}
```

## Run

```sh
dotnet build JiraDesktop.sln
dotnet run --project JiraDesktop.Wpf
```

## Local storage

The app stores local profile data under `%LOCALAPPDATA%/JiraDesktop`:

- `profiles.json` for local accounts and per-user settings
- `active-profile.txt` for the current profile
- `profiles/<profile-id>/workitem-cache.json` for per-user change tracking
- `oauth-token.json` and `oauth-cloudid.txt` for Jira authentication
