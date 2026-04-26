# DevHub — Design Spec

**Date:** 2026-04-25  
**Status:** Approved  
**Author:** Alejandro Lazarte

---

## Overview

DevHub is a personal developer command center: a local Blazor Server web app that provides a unified view of all repos in a configurable root directory, with git state, bulk operations, secret profile management, and service bus visualization.

It is a rename and generalization of the existing `Woffu.Tools.RepoOrchestra` project. All Woffu-specific hardcoding is removed and replaced with configuration. The architecture, tech stack, and features remain intact.

Runs locally with `dotnet run`. No cloud, no database, no installation beyond .NET 10.

---

## Goals

- Remove all Woffu-specific hardcoding (paths, group names, namespace, solution name)
- Make repo grouping fully configurable via `appsettings.json`
- Show `RootPath` from config in the UI (not hardcoded)
- Add a `/settings` page (read-only) showing active configuration
- Add relative timestamps to last commit column (`2h ago`, `3d ago`)
- Preserve all existing features: git dashboard, secret profiles, service bus map

## Non-Goals (this iteration)

- Rules engine / pattern analysis from Documentation repo (next iteration)
- CLI entry point (`devhub scan` commands)
- Database persistence
- Azure DevOps integration

---

## Rename Scope

| Before | After |
|--------|-------|
| `Woffu.Tools.RepoOrchestra.sln` | `DevHub.sln` |
| `Woffu.Tools.RepoOrchestra` (project) | `DevHub` |
| `Woffu.Tools.RepoOrchestra.*` (namespace) | `DevHub.*` |
| `RepoOrchestraOptions` | `DevHubOptions` |
| `"RepoOrchestra"` (config section) | `"DevHub"` |
| `Woffu.Tools.RepoOrchestra.U.Tests` (project) | `DevHub.U.Tests` |

---

## Configuration

### appsettings.json

```json
{
  "DevHub": {
    "RootPath": "C:\\mis-repos",
    "ScanIntervalSeconds": 60,
    "ParallelScanDegree": 8,
    "ExcludedRepos": [],
    "Groups": [
      { "Name": "Services",  "Color": "primary",  "Prefixes": ["MyCompany.Services."] },
      { "Name": "Libraries", "Color": "secondary", "Prefixes": ["MyCompany.Library."] },
      { "Name": "Frontend",  "Color": "info",      "Prefixes": ["MyCompany.Frontend."] },
      { "Name": "Tools",     "Color": "success",   "Prefixes": ["MyCompany.Tools.", "MyCompany.Utils."] },
      { "Name": "DevOps",    "Color": "warning",   "Prefixes": ["Devops."] }
    ],
    "DefaultGroup": "Other"
  },
  "ServiceBusMap": {
    "ScriptPath": "..\\..\\scripts\\generate-servicebus-map.ps1",
    "TemplateFile": "wwwroot\\maps\\servicebus-map.template.html",
    "OutputFile": "wwwroot\\maps\\servicebus-map.html",
    "ReposRoot": "C:\\mis-repos"
  },
  "SecretProfiles": {
    "ProfilesRoot": "..\\..\\profiles",
    "Services": []
  }
}
```

### GroupRule model

```csharp
public class GroupRule
{
    public string Name    { get; set; } = "Other";
    public string Color   { get; set; } = "default";
    public List<string> Prefixes { get; set; } = [];
}
```

### DetermineGroup logic

Replace the hardcoded `switch` with a loop over configured rules:

```csharp
private string DetermineGroup(string repoName)
{
    foreach (var rule in _options.Groups)
        if (rule.Prefixes.Any(p => repoName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return rule.Name;
    return _options.DefaultGroup;
}
```

---

## UI Changes

### TopBar

- Replace hardcoded `C:\_O` with `@Options.Value.RootPath` read from `IOptions<DevHubOptions>`

### Group badges

- Each `RepoGroup` carries its configured `Color`
- `RepoRow` renders the group badge using that color instead of a fixed style

### Last commit column

- Show relative time: `2h ago`, `3d ago`, `just now`
- Full timestamp on hover (MudTooltip)
- Computed from `RepoInfo.LastCommitDate` (new field, parsed from `git log -1 --format=%ct`)

### /settings page

- New route `/settings`
- Read-only view of active config: `RootPath`, `ScanIntervalSeconds`, group rules list
- Link in nav sidebar
- Purpose: quickly verify how DevHub is configured without opening the JSON file

---

## Architecture

Single .NET 10 project: `DevHub`.

```
DevHub/
  Components/
    RepoList.razor
    RepoRow.razor
    FilterBar.razor
    BulkActions.razor
    UpdateBanner.razor
    Layout/
      MainLayout.razor
  Pages/
    Home.razor
    SecretProfiles.razor
    ServiceBusMap.razor
    Settings.razor          ← new
  Services/
    GitCliService.cs
    RepoScannerService.cs
    RepoStateStore.cs
    VersionService.cs
    SecretProfiles/
      SecretProfileService.cs
      ...
    ServiceBusMapService.cs
  Models/
    RepoInfo.cs             ← add LastCommitDate
    RepoGroup.cs            ← add Color
    GroupRule.cs            ← new
    FilterCriteria.cs
  Program.cs
  appsettings.json
  appsettings.Development.json
```

---

## Models

### RepoInfo (updated)

```csharp
public record RepoInfo
{
    public string Name            { get; init; }
    public string Path            { get; init; }
    public string Group           { get; init; }
    public string GroupColor      { get; init; }   // from GroupRule.Color
    public string Branch          { get; init; }
    public bool   IsDirty         { get; init; }
    public int    DirtyFileCount  { get; init; }
    public int    AheadCount      { get; init; }
    public int    BehindCount     { get; init; }
    public string LastCommitMessage { get; init; }
    public DateTime LastCommitDate  { get; init; } // new
    public DateTime LastScanned   { get; init; }
    public bool   IsGitRepo       { get; init; }
    public string? Error          { get; init; }
}
```

### RepoGroup (updated)

```csharp
public record RepoGroup(string Name, string Color, IReadOnlyList<RepoInfo> Repos);
```

---

## Testing

Existing test structure and conventions are preserved:
- Class `When_<condition>`, single method `Then_<result>` per file
- New tests needed:
  - `When_DetermineGroup_is_called/Then_first_matching_prefix_wins.cs`
  - `When_DetermineGroup_is_called/Then_unmatched_repo_falls_to_default_group.cs`
  - `When_GitCliService_gets_last_commit/Then_LastCommitDate_is_parsed.cs`

---

## Out of Scope / Next Iteration

- **Rules engine** — connect Documentation repo patterns; detect which repos violate naming/architecture conventions
- **CLI commands** — `devhub scan`, `devhub find` as a `dotnet tool`
- **RepoMind integration** — cross-repo search using the MCP index
- **Dark/light theme toggle**
- **SQLite** for persistent notes and favorites per repo
