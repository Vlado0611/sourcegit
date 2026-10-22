# Branch Column Graph Style Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a GitKraken-like "Branch column" history graph style (dedicated ref-label column with +N dropdown, big lane-colored circles) next to the existing "Classic" style.

**Architecture:** A `Models.GraphStyle` preference selects the style. Graph geometry (`Models.CommitGraph.Generate`) takes per-style lane metrics; the overlay renderer (`Views.CommitGraph`) gains an X offset and a second anchor-drawing mode. The branch column lives *inside* the existing first DataGrid column (a left sub-column of the cell template), so every existing `Columns[N]` index in `Histories.axaml.cs` stays valid. A new pure function groups/orders a commit's decorators; a new custom-drawn control renders the visible label and the +N chip; existing branch/tag submenu builders are reused for right-click and the dropdown.

**Tech Stack:** C# / .NET 10, Avalonia 11 (custom `Control.Render`, DataGrid, ContextMenu), XAML resources for localization.

**Spec:** `docs/superpowers/specs/2026-10-22-branch-column-graph-style-design.md`

## Global Constraints

- **Classic must be pixel-identical to today.** Every changed code path keeps the old values when `GraphStyle == Classic` (row height 26, `unitWidth` 12, `halfWidth` 6, lane padding 4, dot radii 3/6, refs in the subject cell).
- Default preference value: `Classic`.
- Branch column style values: row height **30**, lane `unitWidth` **22** (`halfWidth` 11), lane padding **8**, circle radius **9**, HEAD ring radius **13** (2px stroke, `Brush.FG1`), merge = hollow circle (lane-color stroke, background fill) with a "+" (two 2px lines, ±4px).
- Branch column: default width **160**, min **60**, persisted per repository in `RepositoryUIStates`; graph+subject area keeps at least **100px**.
- Labels truncate with "…" and show the full ref name as a tooltip.
- Visible-slot priority: current branch / detached HEAD → other local branches → remote-only branches → tags.
- `ShowTagsInGraph = false` hides tags everywhere in the new style; `UseCompactBranchNamesInGraph` controls local+remote merging (reuse existing semantics: remote `X/name` merges into local `name`).
- No new NuGet packages, no new test project (upstream SourceGit has none). Verification is `dotnet build` with zero new warnings plus the manual checks listed in each task, run in the real app (`dotnet run --project src`).
- New user-visible strings go in `src/Resources/Locales/en_US.axaml` only; other locales fall back.
- Match surrounding code style (file-scoped `namespace … { }` blocks, `_camelCase` fields, `SetProperty`, `FuncValueConverter` statics).

## Review Focus

1. **Very long branch name in a narrow column** (`feature/JIRA-12345-some-very-long-description`, column at 60px) → label truncates with "…", tooltip shows full name, nothing spills into the graph. *(Task 4 manual check 4)*
2. **Commit with many refs** (e.g. 15 tags on one commit) → one label + "+14"; dropdown lists all 14 and is scrollable / fits on screen; row height unchanged. *(Task 5 manual check 3)*
3. **Tag-only commit with "Show tags in graph" off** → no label, no "+0" chip, no connector line. *(Task 3 case table row 6, Task 4 manual check 5)*
4. **Dimmed commits** (Graph highlighting = "Current branch only") → circles, connector and label of non-highlighted commits use the same gray as dimmed lanes. *(Task 2 manual check 4, Task 4 manual check 6)*
5. **Very wide graph + wide branch column** (repo with >15 parallel lanes, column dragged wide) → resize clamps so the graph+subject area keeps ≥100px; graph overlay stays aligned with rows while scrolling. *(Task 4 manual checks 3, 7)*

---

### Task 1: Toolchain + `GraphStyle` preference

**Files:**
- Create: `src/Models/GraphStyle.cs`
- Modify: `src/ViewModels/Preferences.cs` (property near `UseCompactBranchNamesInGraph`, ~line 208; field near line 826)
- Modify: `src/Views/Preferences.axaml` (General tab grid, near the graph checkboxes ~line 155–170)
- Modify: `src/Resources/Locales/en_US.axaml` (near line 705)

**Interfaces:**
- Produces: `namespace SourceGit.Models { public enum GraphStyle { Classic = 0, BranchColumn } }`
- Produces: `ViewModels.Preferences.GraphStyle` (`Models.GraphStyle`, get/set via `SetProperty`, default `Classic`) — raises `PropertyChanged` with name `"GraphStyle"`.
- Produces locale keys: `Text.Preferences.General.GraphStyle` = "Graph style", `Text.Preferences.General.GraphStyle.Classic` = "Classic", `Text.Preferences.General.GraphStyle.BranchColumn` = "Branch column".

- [ ] **Step 1: Install the .NET SDK** (none is on this machine; `global.json` asks for 10.0 with `latestMajor` roll-forward)

Run: `curl -sSL https://dot.net/v1/dotnet-install.sh -o "$SCRATCH/dotnet-install.sh" && bash "$SCRATCH/dotnet-install.sh" --channel 10.0 --install-dir ~/.dotnet && ~/.dotnet/dotnet --version`
Expected: prints `10.0.x`. Use `~/.dotnet/dotnet` (or add it to `PATH`) for all later commands.

- [ ] **Step 2: Baseline build and screenshot**

Run: `dotnet build src/SourceGit.csproj -c Debug 2>&1 | tail -5`
Expected: `Build succeeded.` Record the warning count. Launch the app on this repo, open History, and save a screenshot as `$SCRATCH/classic-before.png` (used by Task 2/4 to prove Classic is unchanged).

- [ ] **Step 3: Add `Models.GraphStyle` and `Preferences.GraphStyle`** with the interfaces above.

- [ ] **Step 4: Add the "Graph style" row to Preferences → General**: label `TextBlock` in column 0 + `ComboBox` in column 1 placed directly above the "Show tags in commit graph" checkbox; add a `RowDefinition` and renumber following `Grid.Row`s. Items are the two enum values shown via the locale strings (follow the existing ComboBox pattern in the same file; a small `FuncValueConverter<Models.GraphStyle, string>` in a new `src/Converters/GraphStyleConverters.cs` is fine for display text).

- [ ] **Step 5: Verify**

Run: `dotnet build src/SourceGit.csproj -c Debug 2>&1 | tail -3` → `Build succeeded.`, no new warnings.
Manual: open Preferences → General, "Graph style" shows "Classic"; switch to "Branch column", restart app, value persisted.

- [ ] **Step 6: Commit**

```bash
git add src/Models/GraphStyle.cs src/ViewModels/Preferences.cs src/Views/Preferences.axaml src/Resources/Locales/en_US.axaml src/Converters/GraphStyleConverters.cs
git commit -m "feature: add graph style preference"
```

---

### Task 2: Per-style graph geometry, circles and row height

**Files:**
- Modify: `src/Models/CommitGraph.cs` (`CommitGraphLayout` record line 9; `Dot` class ~line 62; `Generate` ~line 74–268)
- Modify: `src/Views/CommitGraph.cs` (`Render`, `DrawAnchors`)
- Modify: `src/ViewModels/Histories.cs` (`GenerateGraph` ~line 566)
- Modify: `src/Views/Histories.axaml` (DataGrid `RowHeight` line 35, `CommitSubjectPresenter Height` ~line 140, `v:CommitGraph` ~line 278)
- Modify: `src/Views/Histories.axaml.cs` (`OnCommitListLayoutUpdated` ~line 780; attach/detach)
- Modify: `src/Converters/GraphStyleConverters.cs`

**Interfaces:**
- Consumes: `Models.GraphStyle`, `Preferences.Instance.GraphStyle` (Task 1).
- Produces: `public record CommitGraphLayout(double StartY, double ClipWidth, double RowHeight, double OffsetX = 0);`
- Produces: `Models.CommitGraph.Generate(List<Commit> commits, bool firstParentOnlyEnabled, CommitGraphHighlighting highlighting, HashSet<string> highlightExtraCommits, GraphStyle style)` — `style` picks `unitWidth`/`halfWidth`/lane padding (Classic 12/6/4, BranchColumn 22/11/8); the literal `4` padding at the start offset and in `path.End((i + 0.5) * unitWidth + 4, …)` becomes the per-style padding.
- Produces on `Models.CommitGraph.Dot`: `public bool IsMerge;` (true when `commit.Parents.Count > 1`, set regardless of `Type`), `public bool HasBranches;` (any non-tag decorator), `public bool HasTags;` (any tag decorator).
- Produces: `Views.CommitGraph.GraphStyle` styled property (`Models.GraphStyle`, default `Classic`, invalidates visual) and `Views.CommitGraph.ShowTags` styled property (`bool`, default `true`) — `ShowTags` is consumed by the connector in Task 4.
- Produces: `ViewModels.Histories.RegenerateGraph()` (public, calls `GenerateGraph(_commits)`).
- Produces: `Converters.GraphStyleConverters.ToRowHeight` (`FuncValueConverter<Models.GraphStyle, double>`: Classic 26, BranchColumn 30).

- [ ] **Step 1: Thread `GraphStyle` through `Generate`** and set the three new `Dot` fields. `Histories.GenerateGraph` passes `Preferences.Instance.GraphStyle`.

- [ ] **Step 2: Offset + new anchors in `Views.CommitGraph.Render`**: clip rect becomes `new Rect(OffsetX, 0, ClipWidth, clipHeight)` and the transform `Matrix.CreateTranslation(OffsetX, -startY)`. `DrawAnchors` keeps today's body for Classic; for BranchColumn, per dot (`pen` chosen exactly as today, including the gray pen for non-highlighted):
  - `IsMerge`: `DrawEllipse(DotBrush, new Pen(pen.Brush, 2), center, 9, 9)` + "+" lines in `pen.Brush` (2px, ±4px).
  - otherwise: `DrawEllipse(pen.Brush, null, center, 9, 9)`.
  - then if `Type == Head` (independent of merge): `DrawEllipse(null, new Pen(HeadRingBrush, 2), center, 13, 13)` where `HeadRingBrush` is a new styled `IBrush` property bound to `{DynamicResource Brush.FG1}` in XAML.

- [ ] **Step 3: Row height follows style**: bind DataGrid `RowHeight` and `CommitSubjectPresenter.Height` to `Preferences.Instance.GraphStyle` via `GraphStyleConverters.ToRowHeight`; bind `v:CommitGraph.GraphStyle` likewise.

- [ ] **Step 4: Live switching**: in `Views.Histories`, subscribe to `ViewModels.Preferences.Instance.PropertyChanged` on `OnAttachedToVisualTree` and unsubscribe on `OnDetachedFromVisualTree`; on `"GraphStyle"` call `vm.RegenerateGraph()`.

- [ ] **Step 5: Verify**

Run: `dotnet build src/SourceGit.csproj -c Debug 2>&1 | tail -3` → succeeded, no new warnings.
Manual:
1. Classic: screenshot History at the same scroll position as `$SCRATCH/classic-before.png`; compare (e.g. `compare -metric AE` from ImageMagick, or side-by-side) → identical.
2. Switch to Branch column without restarting: rows 30px, lanes wider, circles filled r=9, merges hollow with "+", HEAD has a ring.
3. Check out a merge commit (detached) → hollow "+" circle *with* ring.
4. Set Graph highlighting → "Current branch only": circles on other branches are gray.

- [ ] **Step 6: Commit**

```bash
git add src/Models/CommitGraph.cs src/Views/CommitGraph.cs src/ViewModels/Histories.cs src/Views/Histories.axaml src/Views/Histories.axaml.cs src/Converters/GraphStyleConverters.cs
git commit -m "feature: branch-column graph style geometry and commit circles"
```

---

### Task 3: Decorator grouping and ordering

**Files:**
- Create: `src/Models/DecoratorGroup.cs`

**Interfaces:**
- Produces:
  ```csharp
  public class DecoratorGroup
  {
      public Decorator Decorator { get; set; }          // the ref that represents the group (local branch when merged)
      public List<string> Remotes { get; } = [];        // remote names merged into this label, e.g. "origin"
      public bool IsLocal => Decorator.Type is DecoratorType.CurrentBranchHead or DecoratorType.LocalBranchHead or DecoratorType.CurrentCommitHead;
      public bool IsRemote => Decorator.Type == DecoratorType.RemoteBranchHead || Remotes.Count > 0;
      public bool IsHead => Decorator.Type is DecoratorType.CurrentBranchHead or DecoratorType.CurrentCommitHead;

      public static List<DecoratorGroup> Build(List<Decorator> decorators, bool showTags, bool compact);
  }
  ```
  Returns groups ordered by: head (CurrentBranchHead/CurrentCommitHead) → LocalBranchHead → RemoteBranchHead → Tag; stable within a bucket (keep git's order). `Build(null or empty, …)` → empty list.

- [ ] **Step 1: Implement `Build`.** The local/remote merging must match `CommitRefsPresenter.MeasureOverride` (lines ~341–373): only when `compact`, only for local/current heads, a remote `R/name` (first `/`, not at start or end) merges into local `name`; merged remotes are not emitted as their own groups.

- [ ] **Step 2: Verify with a throwaway check** (no test project upstream): temporarily add a `#if DEBUG` static `DecoratorGroup.SelfCheck()` that builds each case below, compares labels/remotes, and writes PASS/FAIL per case with `Console.WriteLine`; call it once from `App` startup, run the app once, then delete both before committing. Cases (input in git's order → expected labels in order, `+remotes`):

| # | decorators | showTags | compact | expected |
|---|---|---|---|---|
| 1 | `CurrentBranchHead main`, `RemoteBranchHead origin/main`, `Tag v1` | true | true | `main(+origin)`, `v1` |
| 2 | same | true | false | `main`, `origin/main`, `v1` |
| 3 | `Tag v1`, `RemoteBranchHead origin/dev`, `LocalBranchHead dev`, `LocalBranchHead x` | true | true | `dev(+origin)`, `x`, `v1` |
| 4 | `RemoteBranchHead origin/a`, `RemoteBranchHead upstream/a`, `LocalBranchHead a` | true | true | `a(+origin,+upstream)` |
| 5 | `RemoteBranchHead origin/feature/x` (no local) | true | true | `origin/feature/x` |
| 6 | `Tag v1`, `Tag v2` | false | true | *(empty)* |
| 7 | `CurrentCommitHead HEAD`, `LocalBranchHead b` | true | true | `HEAD`, `b` |

Run: `dotnet build src/SourceGit.csproj -c Debug 2>&1 | tail -3` → succeeded, no new warnings; run once and confirm all 7 cases pass; remove the check code.

- [ ] **Step 3: Commit**

```bash
git add src/Models/DecoratorGroup.cs
git commit -m "feature: group and order commit decorators for branch column"
```

---

### Task 4: Branch column control, layout, resizing, connector

**Files:**
- Create: `src/Views/CommitBranchLabel.cs`
- Modify: `src/Views/Histories.axaml` (first column cell template ~line 115–155; `v:CommitGraph`)
- Modify: `src/Views/Histories.axaml.cs` (`OnCommitListLayoutUpdated`, header pointer handlers ~line 470–530, `OnDataContextChanged`)
- Modify: `src/ViewModels/Histories.cs`, `src/Models/RepositoryUIStates.cs`
- Modify: `src/Views/CommitGraph.cs` (connector)

**Interfaces:**
- Consumes: `DecoratorGroup.Build` (Task 3); `CommitGraphLayout.OffsetX`, `Dot.HasBranches/HasTags`, `Views.CommitGraph.GraphStyle/ShowTags` (Task 2); `Preferences.Instance.GraphStyle` (Task 1).
- Produces: `RepositoryUIStates.BranchColumnWidth` (`double`, default `160`).
- Produces: `ViewModels.Histories.BranchColumnWidth` (get/set to `_repo.UIStates.BranchColumnWidth`, raises `PropertyChanged`).
- Produces `Views.CommitBranchLabel : Control` (DataContext = `Models.Commit`), styled properties `FontFamily`, `FontSize`, `Foreground`, `Background`, `ShowTags`, `UseCompactBranchNames`, `HasSingleRemote`, `UseGraphColor` (same meaning as on `CommitRefsPresenter`), and:
  - `public Models.Decorator VisibleDecorator { get; }` — `Decorator` of the first group, or `null`.
  - `public List<Models.Decorator> HiddenDecorators { get; }` — `Decorator` of every other group (merged remotes are reachable via the label's own menu, not listed here).
  - `public enum HitPart { None, Label, More }` and `public HitPart HitTest(Point p)`.

- [ ] **Step 1: `CommitBranchLabel`** — custom-drawn like `CommitRefsPresenter` (reuse `CommitRefsIconCache` icons). Layout, right-aligned within the control with 6px right padding: `[label pill][4px][+N chip]`, then a 2px dashed line (`DashStyle.Dash`) from the chip's (or pill's) right edge to the control's right edge. Pill: height 18, corner radius 9, fill = lane brush `Models.CommitGraph.Pens[commit.Color].Brush` when `UseGraphColor`, else gray (same gray as `CommitRefsPresenter`); tags use gray always; text bold for head; leading icon by type; trailing laptop/cloud icons: laptop (`Icons.Local`) when `IsLocal`, cloud (`Icons.Remote`) when `IsRemote`. Chip: "+N", fill = a new styled `ChipBrush` property bound to `{DynamicResource Brush.Border2}` in XAML, text in `Foreground`. If the pill doesn't fit, shrink it and trim the text with "…" (`FormattedText` + `TextTrimming` or manual measure-and-cut). `ToolTip.Tip` = full name of the visible ref. Nothing drawn when there are no groups.

- [ ] **Step 2: Cell layout** — wrap the first column's cell content in `Grid ColumnDefinitions="Auto,*"`: column 0 is `v:CommitBranchLabel` with `Width="{Binding $parent[v:Histories].((vm:Histories)DataContext).BranchColumnWidth}"` (or equivalent), visible only in BranchColumn style; column 1 is the existing `Border`. Hide `CommitRefsPresenter` in BranchColumn style (`IsVisible` via `GraphStyle` converter `IsClassic`). The header search/settings buttons are not moved.

- [ ] **Step 3: Graph alignment** — in `OnCommitListLayoutUpdated`, when style is BranchColumn: `offsetX = vm.BranchColumnWidth`, `clipWidth = Columns[0].ActualWidth - 4 - offsetX`; pass `OffsetX` in the layout; include it in the "layout changed" comparison.

- [ ] **Step 4: Connector in `Views.CommitGraph`** — BranchColumn only: for each visible dot with `HasBranches || (ShowTags && HasTags)`, draw a 2px dashed line in the dot's pen brush (gray if not highlighted) from `x = 0` to `center.X - 9` at `center.Y`, *before* anchors so circles draw over lanes and connector. Bind `ShowTags` to `ShowTagsInGraph`.

- [ ] **Step 5: Resizing** — extend the existing header handlers: a second drag zone at `x = BranchColumnWidth` (±4px, BranchColumn style only) resizes the branch column; clamp to `[60, Columns[0].ActualWidth - 100]`; store into `vm.BranchColumnWidth`. Keep the existing author-column drag zone working; use a separate `_resizingBranchColumn` flag.

- [ ] **Step 6: Verify**

Run: `dotnet build src/SourceGit.csproj -c Debug 2>&1 | tail -3` → succeeded, no new warnings.
Manual (BranchColumn style unless stated):
1. `main` + `origin/main` + tag on one commit → one `main` pill with laptop+cloud, "+1" chip; dashed line reaches the circle; pill color = lane color.
2. Classic → identical to `$SCRATCH/classic-before.png`.
3. Drag the column edge both ways → clamps at 60 and at (column width − 100); reopen the repo → width restored; other repo keeps its own width.
4. Create branch `feature/JIRA-12345-some-very-long-description`, narrow column to 60 → "…" trimmed, tooltip shows full name.
5. Turn off "Show tags in graph" → tag-only commits show no pill, chip or connector.
6. Graph highlighting "Current branch only" → off-branch pills, connectors, circles gray.
7. Repo with many parallel lanes (e.g. a clone of `git/git`, or create ≥15 branches) → scroll fast; graph stays aligned to rows and clipped left of the subject.

- [ ] **Step 7: Commit**

```bash
git add src/Views/CommitBranchLabel.cs src/Views/Histories.axaml src/Views/Histories.axaml.cs src/ViewModels/Histories.cs src/Models/RepositoryUIStates.cs src/Views/CommitGraph.cs
git commit -m "feature: dedicated branch column in history graph"
```

---

### Task 5: Label interactions — checkout, ref menu, +N dropdown

**Files:**
- Modify: `src/Views/Histories.axaml.cs` (`CreateContextMenuForSingleCommit` ~line 1056; `OnCommitListContextRequested` ~line 796; `OnCommitListDoubleTapped` ~line 827)
- Modify: `src/Views/Histories.axaml` (`PointerPressed` on `v:CommitBranchLabel` if needed)

**Interfaces:**
- Consumes: `CommitBranchLabel.HitTest/VisibleDecorator/HiddenDecorators` (Task 4); `ViewModels.Histories.CheckoutBranchByDecoratorAsync(Models.Decorator)` (existing).
- Produces: `private void FillDecoratorMenu(ContextMenu menu, ViewModels.Repository repo, Models.Commit commit, Models.Decorator decorator)` — the body of the existing `switch (d.Type)` in `CreateContextMenuForSingleCommit`, with the `Tag` case calling `FillTagMenu` directly. `CreateContextMenuForSingleCommit` keeps today's output exactly (tags still collected and appended after branches + separator) — refactor only.

- [ ] **Step 1: Extract `FillDecoratorMenu`** and use it from `CreateContextMenuForSingleCommit`; row context menu output unchanged.

- [ ] **Step 2: Double-click** — in `OnCommitListDoubleTapped`, add a branch beside the `CommitRefsPresenter` case: `e.Source is CommitBranchLabel cbl && cbl.HitTest(pos) == HitPart.Label` → `CheckoutBranchByDecoratorAsync(cbl.VisibleDecorator)`.

- [ ] **Step 3: Right-click on the label** — in `OnCommitListContextRequested`, when `e.Source is CommitBranchLabel` and `HitTest == Label`: build a `ContextMenu`, `FillDecoratorMenu(menu, repo, commit, cbl.VisibleDecorator)`, open it at the label, `e.Handled = true`. Other hit parts fall through to the normal row menu.

- [ ] **Step 4: "+N" dropdown** — on left `PointerPressed` over `HitPart.More`: build a `ContextMenu`, call `FillDecoratorMenu` for each of `HiddenDecorators` (each entry is a ref with its action submenu, which includes Checkout), open at the label, mark handled so the row selection doesn't change. *(Spec allowed this simplification over double-click-in-dropdown.)*

- [ ] **Step 5: Verify**

Run: `dotnet build src/SourceGit.csproj -c Debug 2>&1 | tail -3` → succeeded, no new warnings.
Manual:
1. Right-click a commit row (not on a label), both styles → menu identical to before.
2. Double-click a local branch pill → checks it out; on a remote-only pill → existing "checkout remote" flow.
3. Tag 15 tags on one commit (`for i in $(seq 1 15); do git tag t$i HEAD~3; done`) → "+14" (or +15 if a branch is there); click it → dropdown lists them, fits on screen/scrolls; each tag submenu works. Clean up with `git tag -d`.
4. Right-click the pill → only that ref's submenu.
5. Classic: double-click on `CommitRefsPresenter` still checks out.

- [ ] **Step 6: Commit**

```bash
git add src/Views/Histories.axaml.cs src/Views/Histories.axaml
git commit -m "feature: branch column label checkout, menu and +N dropdown"
```
