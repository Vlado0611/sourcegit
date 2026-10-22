# Bitbucket Remote Icon, Muted Branch-Column Colors, Toolbar Custom Actions — Design

Follow-up to `2026-10-22-branch-column-graph-style-design.md`, built on the
same branch (`worktree-branch-column-graph`). Three independent pieces.

## 1. Bitbucket icon for bitbucket.org remotes

### Intent
On branch labels, a remote that lives on bitbucket.org should be recognizable
at a glance: a small Bitbucket mark replaces the generic cloud icon.

### Decided by the user
- The Bitbucket mark goes exactly where the cloud icon is today.
- Only `bitbucket.org` counts. Self-hosted Bitbucket servers are out of scope.

### Builder's calls (Claude's defaults)
- **Per remote.** The icon is chosen for each remote separately: if `main`
  merges `origin` (bitbucket.org) and `upstream` (GitHub), `origin` gets the
  Bitbucket mark and `upstream` keeps the cloud.
- **Both styles.** The swap applies in the Branch column labels
  (`Views/CommitBranchLabel`) and in Classic's ref pills
  (`Views/CommitRefsPresenter`), anywhere a remote's cloud icon is drawn on a
  graph ref. It does not apply to the sidebar branch tree.
  - This is the one intentional visual change to Classic. It is visible only
    for bitbucket.org remotes, and every other Classic pixel stays identical.
- **Detection.** The remote URL's host equals `bitbucket.org`, for both
  `https://` and `git@bitbucket.org:` (SSH/scp-style) URLs. Reuse or extend
  the host parsing that `Models/Remote.cs` already does.
  - Cache the result per repository (e.g. a set of Bitbucket remote names on
    `ViewModels.Repository`, rebuilt when remotes reload) so nothing is parsed
    per frame. It must not be static, because two repositories can both have
    an `origin` on different hosts.
  - The views receive it the same way they receive `HasSingleRemote`.
- **What gets swapped.** The cloud is `Icons.Remote`. The swap covers
  remote-branch ref icons on graph labels: Classic pills, Branch column pills,
  and the merged-remote icons on a local pill. It does not cover context-menu
  icons or the sidebar.
- **Icon artwork.** A new single-color `Icons.Bitbucket` geometry in
  `src/Resources/Icons.axaml`, drawn in the same brush as the cloud it
  replaces, so the black/white contrast rule on Branch column pills still
  applies. Use a simple, recognizable Bitbucket-bucket silhouette path.

## 2. Muted lane colors in the Branch column style

### Intent
The default lane colors are loud. In the Branch column style they should be
softer: the same hues at roughly half the saturation (mockup option "A ·
Muted").

### Decided by the user
- Muted palette, Branch column style only. Classic keeps today's colors.

### Exact palette (index-for-index replacement of the defaults)
| # | Today | Muted |
|---|---|---|
| 0 | Orange `#FFA500` | `#D9A65B` |
| 1 | ForestGreen `#228B22` | `#4E9A5B` |
| 2 | Turquoise `#40E0D0` | `#5FB8B0` |
| 3 | Olive `#808000` | `#8E8A4A` |
| 4 | Magenta `#FF00FF` | `#B868B8` |
| 5 | Red `#FF0000` | `#C8605A` |
| 6 | Khaki `#F0E68C` | `#CFC28E` |
| 7 | Lime `#00FF00` | `#7FBF6A` |
| 8 | RoyalBlue `#4169E1` | `#6A84C8` |
| 9 | Teal `#008080` | `#3F8F8F` |

### Builder's calls
- **Theme overrides win.** If the user's theme overrides define
  `GraphColors`, those colors are used in both styles. The muted palette only
  replaces the built-in defaults.
- **Scope.** The muted palette applies to everything that takes a lane color
  in the Branch column style: lanes, links, circles, connectors and pills.
  Switching styles live switches palettes live.
- **Pen thickness.** The theme-override pen thickness still applies.
- **How it switches.** Graph style is a single global preference, so Classic
  and Branch column never show at the same time. The static pen set is simply
  re-applied when the style or theme changes:
  - Branch column → muted palette, or the override colors if any.
  - Classic → today's defaults, or the override colors.
  - Classic gets exactly today's pens back.

## 3. Repository custom actions on the toolbar, with icons

### Intent
Run a repository-wide custom action in one click from the repository toolbar,
instead of opening the custom-actions menu. Each custom action can carry an
icon.

### Decided by the user
- Only actions with `Scope == Repository` can be pinned.
- Each repository-scope action has a **"Show in toolbar"** option.
- Pinned buttons sit **right after Push**, spaced like Fetch / Pull / Push,
  before Stash and Apply patch (mockup option "B").
- **Icon picker (option "3").** A grid of SourceGit's built-in icons (the
  `Icons.*` geometries), plus a "Custom file…" choice that points at an
  image file on disk.
- No SVG support for custom files. The user will add it later if needed.

### Builder's calls
- **Data model.** `Models.CustomAction` gains `bool ShowInToolbar` (default
  `false`) and `string Icon` (default empty).
  - `Icon` holds either a built-in key such as `Icons.Fetch`, or a file path
    marked with a prefix, e.g. `file:` + absolute path. Empty means the
    default `Icons.Action`.
  - Existing saved JSON loads unchanged, because the new fields are optional.
- **Which actions are pinned.** Global actions (Preferences) and per-repo
  actions (repository settings) can both be pinned. A pinned global action
  appears in every repository's toolbar.
  - Order: the same order `Repository.GetCustomActions(Scope.Repository)`
    already returns (global first, then per-repo). Toolbar and menu share that
    one source.
  - That method does no de-duplication, and the menu already shows
    same-named actions twice. So two same-named pinned actions simply produce
    two buttons.
- **Refresh.** The pinned set is rebuilt when Preferences or Repository
  settings is closed, and whenever the repository's settings reload. It does
  not need to update on each checkbox click while the dialog is still open.
  - Implementation: an observable list on `ViewModels.Repository`, rendered by
    an `ItemsControl` in the toolbar.
- **The button.**
  - It is an `icon_button`, 32px wide, with a 14×14 icon.
  - Its tooltip shows the action's name, plus its description if it has one.
  - Clicking it runs exactly what choosing the action from the custom-actions
    menu does, including the input dialog when the action has controls.
  - Shown in bare repositories too, since the custom-actions menu isn't hidden
    there today.
- **The menu.** The custom-actions menu keeps listing every repository action
  (pinned or not), and shows each action's chosen icon instead of the generic
  one.
- **Built-in icons** render as `Path` geometry in the theme foreground, so
  they recolor with light and dark themes.
- **Custom files** render as raster images (PNG, JPG, BMP, ICO; whatever
  Avalonia's `Bitmap` loads) scaled to 14×14 (or 16×16) and shown as they are.
  A missing or unreadable file falls back to `Icons.Action` without an error
  dialog.
- **The editor.** Both custom-action editors get the new fields:
  - Preferences → Custom Actions (global).
  - Repository settings → Custom Actions (per repo).
  - Show "Show in toolbar" only when Scope is Repository. If the user changes
    Scope away from Repository, the flag is ignored; it doesn't need clearing.
  - The icon picker is a button showing the current icon. It opens a flyout
    with a scrollable grid of built-in icons plus a "Custom file…" entry that
    opens a file chooser filtered to image types.
  - The grid lists every `Icons.*` geometry resource key, discovered at
    runtime, including brand icons. No exclusion list.
- **Custom-file paths** are stored as absolute paths. That is fine because
  both global preferences and per-repo SourceGit settings are local to the
  machine.
- **Localization.** New strings go in `en_US.axaml` only.

## Non-goals
- SVG custom icons.
- Self-hosted Bitbucket detection.
- Icons for other hosting providers (GitHub, GitLab…).
- Toolbar buttons for non-repository scopes.
- Drag-to-reorder pinned buttons.

## Anti-goals
- Any Classic change beyond the bitbucket.org icon swap.
- Breaking existing saved custom actions or preferences.

## Acceptance
- **Bitbucket icon:** a repo with an `origin` on bitbucket.org shows the
  Bitbucket mark instead of the cloud on its remote labels in both styles.
  GitHub remotes keep the cloud.
- **Muted colors:** the Branch column style uses the muted palette; Classic is
  unchanged; theme-override colors still win in both styles.
- **Toolbar:**
  - Ticking "Show in toolbar" on a repository action adds its button right
    after Push. Clicking it runs the action. Unticking removes the button.
  - Changes apply without restarting.
  - A chosen built-in icon shows on the button and in the menu, and recolors
    with the theme.
  - A custom PNG shows as chosen. Deleting the file falls back to the default
    icon.
- `dotnet build` shows 0 warnings.
