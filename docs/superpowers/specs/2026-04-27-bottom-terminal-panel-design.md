# Bottom Terminal Panel — Design Spec

**Date:** 2026-04-27  
**Branch:** claude/interactive-console-commands-y1awS

## Problem

The current `RepoTerminalPanel` (right drawer) has three UX issues:
1. Killing the shell shows a "restart" option — confusing, users just want to close and reopen later.
2. The shell doesn't follow the active repo when switching between repos.
3. Commands (auto-detected + custom) always force-open a heavy drawer panel, making the terminal hard to use for other repos.

## Design

### Layout

A collapsible bottom panel lives in `MainLayout`, hidden by default. Nothing is visible when closed. When opened it slides up and takes a fixed height (approx. 300px). No persistent tab bar or chrome when closed.

### Opening / Closing

- Each `RepoRow` gets a terminal icon button. Clicking it opens the panel with that repo as the active session.
- Pressing X in the panel header closes the panel. The shell session stays alive in the background (output is preserved).
- Kill button (stop icon) kills the process only. The panel stays open so the user can read the output. The input is disabled. No restart option is shown anywhere.
- To start a new shell, the user clicks the terminal button on any repo row — this opens the panel (or switches repo if already open) and creates a fresh session.

### Shell follows active repo

- If the panel is open and the user clicks the terminal button on a **different** repo:
  - **No process running** → `cd` to the new repo's path automatically; commands reload for new repo.
  - **Process running** → panel stays on the current repo; no automatic switch (prevents accidental interruption).
- "Process running" = the shell session exists, `HasExited == false`, and at least one Input line exists (i.e., a command was sent by the user).

### Commands toolbar

Commands are displayed inside the terminal panel itself — no separate drawer section. Layout (top to bottom within the panel):

1. **Toolbar row** — repo name, kill button, filter input (searches output lines), clear button, X (close panel).
2. **Commands strip** — horizontally scrollable row of chips for auto-detected and custom commands. Includes a search/filter field to narrow commands by name, a refresh (rescan) button, and a `+` button to add custom commands. Collapsed by default; toggled with a chevron button in the toolbar.
3. **Output area** — fills remaining space, same as current `InteractiveConsole`.
4. **Input row** — `$` prompt + text input + send button.

### State coordination

A new singleton `TerminalPanelService` owns:
- `bool IsOpen`
- `RepoInfo? ActiveRepo`
- `event Action? StateChanged`

`MainLayout` subscribes to `StateChanged` to show/hide the panel.  
`RepoRow` calls `TerminalPanelService.OpenForRepo(repo)` on terminal button click.  
`BottomTerminalPanel` reads `ActiveRepo` and handles the cd-or-stay logic.

## Components

| Component | Action |
|-----------|--------|
| `TerminalPanelService` | New singleton — open/close/active-repo state |
| `BottomTerminalPanel.razor` | New — replaces `RepoTerminalPanel` drawer |
| `MainLayout.razor` | Add bottom slot wired to `TerminalPanelService` |
| `RepoRow.razor` | Add terminal icon button |
| `InteractiveConsole.razor(.cs)` | Remove toggle/restart logic; kill = kill only |
| `RepoTerminalPanel.razor(.cs)` | Delete (replaced) |
| `ModalConsoleDialog.razor(.cs)` | Delete (no longer needed) |

## Out of scope

- Resizable panel height (can be added later).
- Multiple simultaneous terminal tabs.
- Modal/floating console window.
