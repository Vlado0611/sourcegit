# Bitbucket Icon, Muted Colors, Toolbar Custom Actions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a Bitbucket mark instead of the cloud for bitbucket.org remotes on graph labels, use a muted lane palette in the Branch column style, and let repository-scope custom actions be pinned to the toolbar with a chosen icon.

**Architecture:**
- **Bitbucket mark:** Bitbucket remote names are computed per repository and flow into both ref-label controls the same way `HasSingleRemote` already does.
- **Muted palette:** the static pen set is re-applied through one App-level function whenever the theme or graph style changes.
- **Custom actions:** `Models.CustomAction` gains two persisted fields. A single `CustomActionIcon` control renders either a built-in geometry or a bitmap file, and is reused by the editor, the menu and the toolbar. The toolbar binds an observable list on `ViewModels.Repository`.

**Tech Stack:** C# / .NET 10, Avalonia 11.3 (custom controls, `ItemsControl`, `Flyout`, `Bitmap`), System.Text.Json source-gen.

**Spec:** `docs/superpowers/specs/2026-10-22-bitbucket-muted-toolbar-actions-design.md`

## Global Constraints

- **Classic unchanged** except the bitbucket.org icon swap. Classic keeps today's palette (`s_defaultPenColors`) and today's pens.
- **Muted palette:**
  - Applies in Branch column style only, index for index: `#D9A65B, #4E9A5B, #5FB8B0, #8E8A4A, #B868B8, #C8605A, #CFC28E, #7FBF6A, #6A84C8, #3F8F8F`.
  - Theme-override `GraphColors` win in both styles.
  - Theme-override pen thickness always applies.
- **Bitbucket detection:** host equals `bitbucket.org`, case-insensitive, for https, `git@host:` scp-style and `ssh://` URLs. Detection is per repository, never static by remote name.
- **Bitbucket icon placement:** the swap covers graph ref icons only:
  - `CommitRefsPresenter` remote pills and merged-remote icons
  - `CommitBranchLabel` remote pills and merged-remote icons

  It does not cover context menus or the sidebar.
- **Custom action fields:**
  - `ShowInToolbar` (bool, default false) and `Icon` (string, default empty).
  - `Icon` values: `""` means the default `Icons.Action`; `"Icons.X"` is a built-in geometry; `"file:" + absolute path` is a raster image.
  - Existing saved JSON must load unchanged.
- **Custom icon files:**
  - No SVG.
  - Supported: whatever Avalonia `Bitmap` loads (PNG/JPG/BMP/ICO).
  - A missing or unreadable file silently falls back to `Icons.Action`.
- **Toolbar buttons:**
  - Only `Scope == Repository` actions with `ShowInToolbar` are pinned.
  - Order and duplicates follow `Repository.GetCustomActions(CustomActionScope.Repository)`: global first, then per-repo, no de-duplication.
  - Buttons sit right after Push with Push-style spacing (`Margin="16,0,0,0"`), before Stash. They also show in bare repos.
  - Each button is an `icon_button`, `Width="32"`, with a 14×14 icon. Its tooltip is the action name (`CustomAction` has no description field).
  - Clicking runs `repo.ExecCustomActionAsync(action, null)`, exactly like the menu.
- **Refreshing pinned buttons:** they refresh when the Preferences window closes (all open repositories), when Repository settings closes (that repository), and when a repository opens. They do not refresh live while a dialog is open.
- **Custom-actions menu:** keeps listing all actions and shows each action's icon.
- **Icon picker:** a button showing the current icon that opens a flyout. The flyout has a scrollable grid of every `Icons.*` geometry resource key (no exclusions), plus "Custom file…" (a file chooser filtered to png/jpg/jpeg/bmp/ico) and "Default". "Show in toolbar" is visible only when Scope is Repository.
- **New strings** go only in `src/Resources/Locales/en_US.axaml`, keys sorted alphabetically like the neighbors.
- **No new NuGet packages, no test project.** Verification is `~/.dotnet/dotnet build src/SourceGit.csproj -c Debug` with 0 warnings, plus throwaway console checks in the scratchpad for pure logic. GUI checks go to the user's checklist (the sandbox can't launch the app).
- **Code style:** match the surrounding code (namespace blocks, `_camelCase`, `SetProperty`, `FuncValueConverter` statics).

## Review Focus

1. **Bitbucket URL look-alikes** must not match: `https://bitbucket.org.evil.com/x/y`, `https://notbitbucket.org/x/y`, `git@github.com:bitbucket.org/x.git`. *(Task 1 Step 2, cases 6–8)*
2. **An old preferences or repo settings JSON** without the new fields loads with `ShowInToolbar=false` and `Icon=""`, and saving it again writes the fields. *(Task 3 Step 2 check)*
3. **Custom icon file deleted or replaced** after it was chosen:
   - A deleted file falls back to `Icons.Action` with no exception.
   - A replaced file shows the new image after the next refresh.

   *(Task 3 Step 3 (cache keyed by path + last-write time), Task 5 manual check 4)*
4. **Switching style while the theme has override colors** keeps the override colors in both styles. Switching back to Classic restores exactly the old pens. *(Task 2 Step 2 case table)*
5. **Many pinned actions (e.g. 6)** fit on the toolbar without hiding Fetch/Pull/Push. The center group grows and the side groups shrink, as today. *(Task 5 manual check 3)*

---

### Task 1: Bitbucket remote icon

**Files:**
- Modify: `src/Models/Remote.cs` (add static detector)
- Modify: `src/Resources/Icons.axaml` (add `Icons.Bitbucket`)
- Modify: `src/ViewModels/Repository.cs` (`Remotes` setter, ~line 132), `src/ViewModels/Histories.cs` (next to `HasSingleRemote`, ~line 168)
- Modify: `src/Views/CommitRefsPresenter.cs`, `src/Views/CommitBranchLabel.cs`, `src/Views/Histories.axaml` (bind the new property on both controls)

**Interfaces:**
- Produces: `public static bool Models.Remote.IsBitbucketOrg(string url)`
- Produces: `ViewModels.Histories.BitbucketRemotes`, a `HashSet<string>` of remote names; never null, empty by default. Set from `Repository.Remotes` like `HasSingleRemote`.
- Produces: a styled property `BitbucketRemotes` (`HashSet<string>`) on both `CommitRefsPresenter` and `CommitBranchLabel`. A change invalidates measure.
- Produces: `CommitRefsIconCache.GetIcon` keeps working; add `CommitRefsIconCache.Instance.Bitbucket` (Geometry, loaded like the others).

- [ ] **Step 1: Implement `IsBitbucketOrg`**: parse the host from https/`ssh://`/scp-style `user@host:path`, compare `bitbucket.org` with `OrdinalIgnoreCase`, and return false for null or empty input.

- [ ] **Step 2: Throwaway check.** Create a console project at `$SCRATCH/bitbucket-check/` that compiles `src/Models/Remote.cs` (via `<Compile Include>`), or copy just the method if Remote.cs drags in too much. Run it with `~/.dotnet/dotnet run --project`. Each case must print PASS.

| # | url | expected |
|---|---|---|
| 1 | `https://bitbucket.org/team/repo.git` | true |
| 2 | `https://user@bitbucket.org/team/repo.git` | true |
| 3 | `git@bitbucket.org:team/repo.git` | true |
| 4 | `ssh://git@bitbucket.org/team/repo.git` | true |
| 5 | `https://BitBucket.ORG/team/repo` | true |
| 6 | `https://bitbucket.org.evil.com/x/y` | false |
| 7 | `https://notbitbucket.org/x/y` | false |
| 8 | `git@github.com:bitbucket.org/x.git` | false |
| 9 | `""` / `null` | false |

- [ ] **Step 3: Add the `Icons.Bitbucket` StreamGeometry** to `Icons.axaml`. It is a single-path Bitbucket bucket silhouette sized like the neighbouring icons, filled (not stroked), and recognisable at 10px.

- [ ] **Step 4: Wire the data.** In the `Repository.Remotes` setter, build the set of remote `Name`s whose `URL` passes `IsBitbucketOrg` and assign it to `_histories.BitbucketRemotes`. Bind both controls in `Histories.axaml` with `BitbucketRemotes="{Binding $parent[v:Histories].((vm:Histories)DataContext).BitbucketRemotes}"`, or the same binding form already used there for `HasSingleRemote`.

- [ ] **Step 5: Swap the icon.** Wherever either control draws the remote/cloud icon for a remote name, draw the Bitbucket geometry instead when `BitbucketRemotes.Contains(remoteName)`:
  - For a `RemoteBranchHead` decorator, the remote name is the text before its first `/`.
  - For merged remotes, it is the remote name already stored on the item or group.
  - Use the same brush, size and position as the cloud.

- [ ] **Step 6: Verify.** `~/.dotnet/dotnet build src/SourceGit.csproj -c Debug 2>&1 | tail -3` → `Build succeeded.`, 0 warnings. Record the GUI checks for the user:
  1. A bitbucket.org remote shows the Bitbucket mark in both styles.
  2. A GitHub remote keeps the cloud.
  3. A branch on both kinds of remote shows one of each.

- [ ] **Step 7: Commit**

```bash
git add src/Models/Remote.cs src/Resources/Icons.axaml src/ViewModels/Repository.cs src/ViewModels/Histories.cs src/Views/CommitRefsPresenter.cs src/Views/CommitBranchLabel.cs src/Views/Histories.axaml
git commit -m "feature: show bitbucket icon for bitbucket.org remotes on graph labels"
```

---

### Task 2: Muted palette in Branch column style

**Files:**
- Modify: `src/Models/CommitGraph.cs` (`SetDefaultPens` ~line 28, palette list ~line 448)
- Modify: `src/App.axaml.cs` (`SetTheme` ~lines 120–170)
- Modify: `src/ViewModels/Preferences.cs` (`GraphStyle` setter)

**Interfaces:**
- Produces: `Models.CommitGraph.SetDefaultPens(double thickness = 2, bool muted = false)`. With `muted == false` it behaves exactly as today.
- Produces: `private static readonly List<Color> s_mutedPenColors` in `Models.CommitGraph`, holding the 10 colors from Global Constraints in order.
- Produces: `public static void App.ApplyGraphPens()`. It uses the stored override colors if any (`SetPens(colors, thickness)`), otherwise `SetDefaultPens(thickness, Preferences.Instance.GraphStyle == Models.GraphStyle.BranchColumn)`.
- Produces: App stores `_graphColorOverrides` (`List<Color>`, empty when none) and `_graphPenThickness` (`double`, default 2), set by `SetTheme`.

- [ ] **Step 1: Implement.**
  - `SetTheme` stops calling `SetPens`/`SetDefaultPens` directly. It records the override colors and thickness, or resets them to empty and 2, then calls `ApplyGraphPens()`.
  - The `Preferences.GraphStyle` setter assigns the field, calls `App.ApplyGraphPens()`, and only then raises `PropertyChanged`. That way views regenerating on that event already see the new pens. Don't use `SetProperty` here; use the explicit field-compare / assign / `OnPropertyChanged()` pattern.
  - Guard against `App.Current` being null or not yet initialised, as other App statics do.

- [ ] **Step 2: Verify by reasoning plus build.** `~/.dotnet/dotnet build … | tail -3` gives 0 warnings. In the report, walk through this table against the code:

| theme overrides GraphColors | style | pens |
|---|---|---|
| none | Classic | `s_defaultPenColors`, thickness 2 (identical to before) |
| none | BranchColumn | `s_mutedPenColors`, thickness 2 |
| none, thickness 3 | BranchColumn | muted, 3 |
| `[#111111,#222222]` | Classic | overrides |
| `[#111111,#222222]` | BranchColumn | overrides |
| switch BranchColumn → Classic | — | back to exactly the Classic row |

GUI check for the user: lanes and labels look muted in Branch column and unchanged in Classic.

- [ ] **Step 3: Commit**

```bash
git add src/Models/CommitGraph.cs src/App.axaml.cs src/ViewModels/Preferences.cs
git commit -m "feature: muted lane palette for branch column style"
```

---

### Task 3: Custom action icon/toolbar fields and the icon renderer

**Files:**
- Modify: `src/Models/CustomAction.cs` (`CustomAction` class ~line 74)
- Create: `src/Views/CustomActionIcon.cs`
- Modify: `src/Views/RepositoryToolbar.axaml.cs` (`OpenCustomActionMenu` ~line 471: use the action's icon)
- Modify: any other place that builds a custom-action menu item with `CreateMenuIcon("Icons.Action")` (grep for it) so it uses the action's icon.

**Interfaces:**
- Produces on `Models.CustomAction`:
  - `public bool ShowInToolbar` (`SetProperty`, default `false`)
  - `public string Icon` (`SetProperty`, default `string.Empty`)
  - `public const string IconFilePrefix = "file:";`
- Produces `Views.CustomActionIcon : Control`:
  - Styled properties: `Icon` (string) and `Foreground` (IBrush). Changing either invalidates the visual.
  - It renders a built-in geometry, stretched uniformly into its bounds and filled with `Foreground`, or a bitmap scaled uniformly into its bounds. It falls back to `Icons.Action`.
  - `public static List<string> GetBuiltinIconKeys()` returns, sorted, every resource key starting with `"Icons."` whose value is a `Geometry`, searched across `Application.Current.Resources` and its merged dictionaries.
  - Bitmaps are cached in a static dictionary keyed by `(path, File.GetLastWriteTimeUtc(path))`. Load failures are cached as null and fall back silently.

- [ ] **Step 1: Add the two properties** to `CustomAction`. JSON source-gen needs no extra registration; check that `CustomAction` is serialized through existing contexts.

- [ ] **Step 2: Throwaway JSON check.** Create a console project at `$SCRATCH/customaction-json/` that compiles `CustomAction.cs` and round-trips:
  - (a) `{"Name":"Deploy","Scope":0,"Executable":"x","Arguments":""}` deserializes with `ShowInToolbar=false` and `Icon=""`.
  - (b) An object with `ShowInToolbar=true` and `Icon="file:/tmp/a.png"` serializes and deserializes equal.

  Use plain `JsonSerializer` with default options plus whatever the project's options add, if that's reachable. Otherwise note the difference.

- [ ] **Step 3: Implement `CustomActionIcon`.** Use `Icon.StartsWith(CustomAction.IconFilePrefix, Ordinal)` to pick the file path branch.

- [ ] **Step 4: Menu icons.** Every custom-action `MenuItem.Icon` becomes `new CustomActionIcon { Icon = action.Icon, Width = 12, Height = 12, Foreground = <same brush CreateMenuIcon uses> }`. Read `CreateMenuIcon` for its size and brush and match them.

- [ ] **Step 5: Verify.** Build gives 0 warnings, and the JSON check passes (both cases). GUI check for the user: the menu shows the default icon for existing actions.

- [ ] **Step 6: Commit**

```bash
git add src/Models/CustomAction.cs src/Views/CustomActionIcon.cs src/Views/RepositoryToolbar.axaml.cs <other menu files>
git commit -m "feature: custom action icon and show-in-toolbar fields"
```

---

### Task 4: Custom action editor — "Show in toolbar" and icon picker

**Files:**
- Create: `src/Views/CustomActionIconPicker.axaml`, `src/Views/CustomActionIconPicker.axaml.cs`
- Modify: the custom-action edit form in `src/Views/Preferences.axaml` (~line 716 onward) and in `src/Views/RepositoryConfigure.axaml`
- Modify: `src/Resources/Locales/en_US.axaml`

**Interfaces:**
- Consumes: `CustomAction.ShowInToolbar`, `CustomAction.Icon`, `CustomAction.IconFilePrefix`, `CustomActionIcon`, `CustomActionIcon.GetBuiltinIconKeys()` (Task 3).
- Produces `Views.CustomActionIconPicker : UserControl` with a two-way bindable `Icon` (string) property. It shows a button with a 14×14 `CustomActionIcon` preview. Clicking opens a `Flyout` containing:
  - a ~300px-high scrollable `WrapPanel`/`ItemsControl` of 28×28 buttons, one per built-in key, with the key as tooltip; clicking one sets `Icon` to that key and closes the flyout;
  - a "Default" button that sets `""`;
  - a "Custom file…" button that opens `StorageProvider.OpenFilePickerAsync` filtered to `*.png;*.jpg;*.jpeg;*.bmp;*.ico` and sets `Icon = IconFilePrefix + absolutePath`.
- Produces these locale keys:
  - `Text.ConfigureCustomAction.Icon` = "Icon"
  - `Text.ConfigureCustomAction.Icon.Default` = "Default"
  - `Text.ConfigureCustomAction.Icon.CustomFile` = "Custom file…"
  - `Text.ConfigureCustomAction.ShowInToolbar` = "Show in toolbar"

  If the existing custom-action field keys use a different prefix (check `en_US.axaml`), use that prefix instead and keep the suffixes.

- [ ] **Step 1: Build the picker.**
- [ ] **Step 2: Add two rows to both editors**, next to the existing fields and in the same row style:
  - "Icon" with the picker bound to `Icon`.
  - "Show in toolbar" as a CheckBox bound to `ShowInToolbar`, with `IsVisible` bound to `Scope == Repository`. Use a converter or `Scope` comparison in the existing style; add a small `FuncValueConverter` if none exists.

  Insert the new grid rows and renumber the following `Grid.Row`s and `RowDefinitions`.
- [ ] **Step 3: Verify.** Build gives 0 warnings. GUI checks for the user:
  1. Both editors show the Icon picker.
  2. "Show in toolbar" appears only for repository scope.
  3. The grid lists the built-in icons and picking one updates the preview.
  4. "Custom file…" picks a PNG and the preview shows it.
- [ ] **Step 4: Commit**

```bash
git add src/Views/CustomActionIconPicker.axaml src/Views/CustomActionIconPicker.axaml.cs src/Views/Preferences.axaml src/Views/RepositoryConfigure.axaml src/Resources/Locales/en_US.axaml
git commit -m "feature: icon picker and show-in-toolbar option for custom actions"
```

---

### Task 5: Pinned custom-action buttons in the toolbar

**Files:**
- Modify: `src/ViewModels/Repository.cs`
- Modify: `src/Views/RepositoryToolbar.axaml` (after the Push button ~line 70), `src/Views/RepositoryToolbar.axaml.cs`
- Modify: `src/Views/Preferences.axaml.cs` (`OnClosing` ~line 188), `src/Views/RepositoryConfigure.axaml.cs` (`OnClosing` ~line 16)

**Interfaces:**
- Consumes: `CustomAction.ShowInToolbar/Icon`, `CustomActionIcon` (Task 3); `Repository.GetCustomActions(CustomActionScope.Repository)` and `Repository.ExecCustomActionAsync(Models.CustomAction, object)` (existing).
- Produces:
  - `public AvaloniaList<Models.CustomAction> ViewModels.Repository.ToolbarCustomActions { get; }`, never null.
  - `public void ViewModels.Repository.RefreshToolbarCustomActions()`. It replaces the list contents with the `ShowInToolbar` actions from `GetCustomActions(CustomActionScope.Repository)`, in that order.

    `GetCustomActions` returns `(CustomAction, label)` tuples. Take the action from each tuple. If the tuple's action is a copy ("dup"), use exactly what the menu passes to `ExecCustomActionAsync`.

- [ ] **Step 1: VM.** Call `RefreshToolbarCustomActions()` once the repository's settings are loaded on open; find where `_settings` is assigned or the repo is opened. In Preferences `OnClosing`, call it for every `ViewModels.Repository` in `App.GetLauncher().Pages` (`page.Data is ViewModels.Repository r`). In RepositoryConfigure `OnClosing`, call it for that window's repository.
- [ ] **Step 2: Toolbar.** Directly after the Push button, add an `ItemsControl` bound to `ToolbarCustomActions` with a horizontal `StackPanel` items panel. Its item template is an `icon_button` `Button` with `Width="32"`, `Margin="16,0,0,0"` and `ToolTip.Tip="{Binding Name}"`, containing a 14×14 `CustomActionIcon` bound to `Icon` with the same foreground as the other toolbar icons. Clicking calls a code-behind handler that runs `await repo.ExecCustomActionAsync(action, null)` on the toolbar's `DataContext` repo. Visible in bare repos.
- [ ] **Step 3: Verify.** Build gives 0 warnings. GUI checks for the user:
  1. Ticking "Show in toolbar" on a repository action and closing settings adds the button right after Push.
  2. Clicking it runs the action; an action with input controls opens its dialog.
  3. Six pinned actions still leave Fetch/Pull/Push visible.
  4. A custom PNG icon shows; after deleting the file and reopening settings, it falls back to the default icon.
  5. Unticking removes the button.
  6. A global pinned action appears in every open repository.
- [ ] **Step 4: Commit**

```bash
git add src/ViewModels/Repository.cs src/Views/RepositoryToolbar.axaml src/Views/RepositoryToolbar.axaml.cs src/Views/Preferences.axaml.cs src/Views/RepositoryConfigure.axaml.cs
git commit -m "feature: pin repository custom actions to the toolbar"
```
