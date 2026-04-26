# Secret Profiles MVP — Design

**Date:** 2026-04-17
**Status:** Approved
**Scope:** New feature in `Woffu.Tools.RepoOrchestra`

## Problem

Developers keep multiple environments (e.g. dev01 and prod) mixed in the same
UserSecrets `secrets.json`, switching by commenting/uncommenting blocks. The
risk: running `dotnet test` — including "tool tests" that mutate databases —
against **prod** by accident. The current trigger case is the Documents
service, whose API, Function, and Integration.Tests projects all share the
UserSecretsId `Woffu.Services.Documents.API.Secrets` and therefore the same
physical `secrets.json`.

The commenting workflow is error-prone by construction: a missed `//`, a
mis-closed brace, or forgetting which block is active, and you are suddenly
pointed at production.

## Goal

Replace the comment/uncomment workflow with atomic profile swaps, a prominent
visual indicator of the active environment, and a confirmation gate when
activating a production profile.

The MVP covers a **single service (Documents)** and a **single UserSecretsId**.
The design must allow adding more services by configuration — no code changes.

## Non-Goals (MVP)

- Syncing profiles with Key Vault (`setup-local.ps1` already handles that).
- Managing multiple UserSecretsIds per service.
- Editing secret values key-by-key — the unit is the whole JSON file.
- History, versioning, or audit trail of profile changes.

## Architecture

New Blazor page `/secret-profiles` backed by a singleton
`SecretProfileService`. The service owns a profiles catalog on disk and
operates on the real UserSecrets folder by full-file copy. Active profile
detection is hash-based (SHA-256): the profile whose hash matches the current
`secrets.json` is the active one; no match means the file was edited manually
("dirty").

```
Orchestra UI (Razor)
  ↓
SecretProfileService (Singleton)
  ↓
IFileSystem (abstraction, mockable)
  ↓
Real disk
```

### Storage layout

```
C:\woffu-orchestra\
  profiles\                            # gitignored
    Documents\
      dev.json
      prod.json
```

The profile filename **is** the profile name. No metadata file; the
per-service configuration (UserSecretsId, which profile names count as prod)
lives in `appsettings.json`.

The real `secrets.json` continues to live at
`%APPDATA%\Microsoft\UserSecrets\{UserSecretsId}\secrets.json`. Orchestra
never reads that folder for any purpose other than this feature.

### Configuration

```json
"SecretProfiles": {
  "ProfilesRoot": "..\\..\\profiles",
  "Services": [
    {
      "Name": "Documents",
      "UserSecretsId": "Woffu.Services.Documents.API.Secrets",
      "ProdProfileNames": [ "prod" ]
    }
  ]
}
```

- `ProfilesRoot` is resolved relative to `IHostEnvironment.ContentRootPath`
  (which is the API project dir, e.g. `C:\woffu-orchestra\src\Woffu.Tools.RepoOrchestra`),
  matching how `ServiceBusMapService` resolves its paths. The default
  `..\..\profiles` lands at the repo root.
- `ProdProfileNames` is a set of exact profile names (no regex). Comparison is
  case-insensitive.
- Adding another service = one new entry in `Services`. No code change.

## Components

### `SecretProfileService`

Public surface (all methods `async`, take `CancellationToken`):

```csharp
Task<IReadOnlyList<ServiceProfileView>> GetServicesAsync(CancellationToken ct);
Task<IReadOnlyList<ProfileInfo>> GetProfilesAsync(string serviceName, CancellationToken ct);
Task<ActiveProfileInfo> GetActiveProfileAsync(string serviceName, CancellationToken ct);
Task<string> ReadProfileContentAsync(string serviceName, string profileName, CancellationToken ct);
Task<string> ReadActiveContentAsync(string serviceName, CancellationToken ct);
Task CaptureAsync(string serviceName, string profileName, CancellationToken ct);
Task ApplyAsync(string serviceName, string profileName, bool prodConfirmed, CancellationToken ct);
Task DeleteAsync(string serviceName, string profileName, CancellationToken ct);
```

Types:

```csharp
public record ServiceProfileView(string Name, string UserSecretsId);

public record ProfileInfo(string Name, bool IsProd, long SizeBytes, DateTime ModifiedUtc);

public record ActiveProfileInfo(string? MatchedProfileName, bool IsDirty, bool IsProd);
// MatchedProfileName null + IsDirty true → user edited secrets.json manually
// MatchedProfileName null + IsDirty false → secrets.json does not exist yet
```

Behavior:

- `CaptureAsync(serviceName, profileName)` — copies the live `secrets.json`
  to `profiles/{Service}/{profileName}.json`. Overwrites if it exists.
  Validates `profileName` against `^[A-Za-z0-9._-]+$` to prevent path
  traversal.
- `ApplyAsync(serviceName, profileName, prodConfirmed)` — if the profile is
  in `ProdProfileNames` and `prodConfirmed` is false, throws
  `ProdConfirmationRequiredException`. Otherwise copies the profile over
  `secrets.json`, creating the UserSecrets folder if needed. The copy is
  atomic: write to `secrets.json.tmp` in the same folder, then `File.Move`
  with overwrite.
- `GetActiveProfileAsync` — SHA-256 of current `secrets.json` compared
  against SHA-256 of each profile file. First match wins.
- `DeleteAsync` — refuses to delete the currently active profile (returns an
  error the UI surfaces).

### UserSecrets path resolution

Windows-only (project runs on Windows): `%APPDATA%\Microsoft\UserSecrets\{id}\secrets.json`.
Resolved via `Environment.GetFolderPath(SpecialFolder.ApplicationData)` — no
cross-platform branching needed for MVP.

### `IFileSystem` abstraction

Minimal surface for what the service actually does:

```csharp
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct);
    Task WriteAllBytesAsync(string path, byte[] contents, CancellationToken ct);
    void Move(string source, string dest, bool overwrite);
    void Delete(string path);
    IEnumerable<string> EnumerateFiles(string path, string pattern);
    DateTime GetLastWriteTimeUtc(string path);
    long GetFileSize(string path);
}
```

Production implementation `FileSystem` wraps `System.IO`. Tests use Moq.
This mirrors the `IProcessRunner` pattern already in the project.

### UI — `/secret-profiles` page

One `MudCard` per configured service. For Documents:

- **Header:** service name + big active-status chip:
  - Green `DEV` when active profile name is not in `ProdProfileNames`
  - Red `PROD` (larger font, red background) when active profile is prod
  - Grey `DIRTY` when `secrets.json` exists but no profile matches
  - Grey `NO SECRETS` when `secrets.json` doesn't exist yet
- **Profiles table:** columns `Name | Size | Modified | Actions`
  - `[Apply]` — disabled on the active one
  - `[View]` — opens modal with Monaco single editor, JSON, read-only
  - `[Diff]` — opens modal with Monaco diff editor comparing profile vs live
  - `[Delete]` — disabled on active; confirm dialog
- **Capture button:** "Capture current as…" opens a dialog with a name field.
  Disabled when live `secrets.json` doesn't exist. If the name matches an
  existing profile, the dialog shows a warning and requires explicit confirm.
- **Prod confirmation dialog:** red banner, body text explaining the risk,
  user must type the service name exactly. Submit enabled only on exact match.

### Monaco integration

Package: `BlazorMonaco` (current major). One wrapper component
`MonacoJsonViewer` accepting `Content` and `ReadOnly` parameters, and one
`MonacoJsonDiffViewer` accepting `Original` and `Modified`. Theme: `vs-dark`
to match the rest of the app.

## Error handling

- `ProdConfirmationRequiredException` — thrown by service, caught by UI,
  shown as Snackbar with red severity. Should be impossible in normal UI flow
  because the button path goes through the confirmation dialog first.
- `InvalidProfileNameException` — invalid characters in capture name.
- `ProfileNotFoundException` — profile file missing when applying/reading.
- `ServiceNotConfiguredException` — service name not in config.
- Filesystem errors bubble up as-is; UI shows snackbar with message.

## Testing

xUnit + Moq + FluentAssertions, `When_<Condition>/Then_<Result>.cs` layout
used elsewhere in the project.

Required test cases for `SecretProfileService`:

- `When_Capturing`:
  - `Then_copies_live_secrets_to_profile_file`
  - `Then_overwrites_existing_profile`
  - `Then_throws_on_invalid_name`
  - `Then_throws_when_live_secrets_missing`
- `When_Applying`:
  - `Then_copies_profile_to_secrets_location`
  - `Then_creates_user_secrets_folder_if_missing`
  - `Then_throws_when_prod_not_confirmed`
  - `Then_applies_when_prod_confirmed`
  - `Then_writes_atomically_via_temp_file`
- `When_Getting_active_profile`:
  - `Then_returns_matching_profile_by_hash`
  - `Then_returns_dirty_when_no_match`
  - `Then_returns_no_secrets_when_file_missing`
  - `Then_flags_prod_when_active_profile_is_prod`
- `When_Deleting`:
  - `Then_deletes_inactive_profile`
  - `Then_refuses_to_delete_active_profile`
- `When_Getting_services`:
  - `Then_returns_configured_services`

No integration tests in MVP — `IFileSystem` mocks are sufficient.

## Security considerations

- Profile name validated against `^[A-Za-z0-9._-]+$` to prevent path
  traversal (`../../etc/passwd`-style).
- UserSecretsId from config is trusted (it's your own app config).
- Prod confirmation is UI-only — anyone with access to the Orchestra process
  can call the service directly. This is acceptable because Orchestra is a
  local dev tool running on the developer's machine.

## Out of scope, explicitly deferred

Items raised during brainstorming that are **not** in this MVP:

- Multi-UserSecretsId per service.
- Key Vault sync (use `setup-local.ps1`).
- Key-level diff or edit.
- Audit log of applies/captures.
- Pre-`dotnet test` guard hook.
- Multi-service UI (the code supports it, the config only lists Documents).

## Success criteria

- Starting from current commented/uncommented `secrets.json`, developer can:
  1. Capture current state as `dev`.
  2. Edit `secrets.json` once to point at prod, capture as `prod`.
  3. From then on, switch with one click, with impossible-to-miss visual
     indication of which environment is active.
- Applying `prod` without explicit confirmation is structurally impossible
  through the UI.
- All `SecretProfileService` methods are covered by unit tests.
