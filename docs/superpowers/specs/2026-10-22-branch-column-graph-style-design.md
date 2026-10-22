# Branch Column Graph Style — Design

## Intent

In SourceGit's history view it is hard to see which branches point at which
commit, especially when several branches and tags sit on the same commit. The
ref labels are small pills squeezed into the subject cell, their horizontal
position shifts with the graph width, and the commit dots (3px radius) are hard
to read. GitKraken does this well: a dedicated branch column on the left, one
label per commit with a dropdown for the rest, and big, lane-colored circles.

This change adds a second, GitKraken-like graph style **alongside** the
existing one. The existing look stays the default and is untouched.

Success: with the new style on, a user can glance at any row and see which
branch(es) point there, and spot commits on the graph without squinting.

## Goals

- A new "Graph style" preference with two values: **Classic** (current look,
  default) and **Branch column** (new).
- In Branch column style:
  - Ref labels move out of the subject cell into a dedicated, resizable column
    to the left of the graph.
  - One label is visible per commit; any additional refs on that commit are
    reachable through a **+N** chip that opens a dropdown.
  - Bigger, filled, lane-colored commit circles.
- Suitable for an upstream pull request to SourceGit.

## Non-goals

- Author avatars or initials inside commit circles (explicitly not wanted).
- Changing the Classic style in any way.
- Changing the author / date / SHA columns, commit selection, or the commit
  detail panel.
- Changing graph layout algorithm (lane assignment, colors, curve shapes) beyond
  the spacing constants needed for bigger circles.

## Anti-goals

- Anything that alters behavior for users who never switch the setting.
- Losing any ref information: every ref visible in Classic must be reachable in
  Branch column style (directly or via the dropdown).

## Current state (for orientation)

- `src/Views/CommitGraph.cs` — renders the graph as an overlay
  (`Histories.axaml:278`, `IsHitTestVisible="False"`) on top of the commit
  list. `DrawAnchors` draws dots: Default r=3 hollow, Head r=6 ring + r=3 fill,
  Merge r=6 filled with a "+".
- `src/Models/CommitGraph.cs` — lays out lanes; `unitWidth = 12`,
  `halfWidth = 6`. Sets `commit.LeftMargin` so the subject cell starts right of
  the graph.
- `src/Views/CommitRefsPresenter.cs` — measures and draws ref pills (16px high)
  inside the subject cell (`Histories.axaml:129`). Pills use the lane color only
  when `UseGraphColor` (commit is highlighted), otherwise gray; tags always
  gray. When `UseCompactBranchNames` is on it already merges a local branch with
  same-named remote branches into one pill with remote icons/names.
  `DecoratorAt(point)` is used for double-click checkout
  (`Histories.axaml.cs:841`).
- Row context menu (`Histories.axaml.cs:~1064`) already builds per-decorator
  submenus (branch / remote branch / tag actions).
- Row height is `26` (`Histories.axaml:35`, also `CommitSubjectPresenter`
  `Height="26"`).
- Preferences: `ShowTagsInGraph`, `UseCompactBranchNamesInGraph` in
  `src/ViewModels/Preferences.cs`, checkboxes in `Preferences.axaml` General tab.

## Decided by the user

1. **Branch column layout (mockup "C").** A dedicated column left of the graph
   holds ref labels. When a local branch and its remote counterpart point at the
   same commit they collapse into one label with a laptop (local) and/or cloud
   (remote) icon. Remaining refs go into a **+N** dropdown.
2. Labels are colored with their lane's color (not gray), with a short dashed
   connector from the label to the commit circle.
3. **Circles (option "C").** All commit circles the same, larger size (≈9px
   radius), filled with the lane color. The current HEAD commit gets an extra
   white (contrasting) ring. Merge commits are hollow (lane-colored stroke,
   background fill) with a "+" inside.
4. No avatars or initials in circles.
5. Delivered as a setting next to the existing style, not a replacement.

## Builder's calls (Claude's defaults — the user may override)

- **Setting shape:** a `GraphStyle` enum preference (`Classic`, `BranchColumn`)
  shown in Preferences → General near the existing graph options; default
  `Classic`. Needs a localized string in `src/Resources/Locales/en_US.axaml`
  (other locales can fall back).
- **Which ref takes the visible slot:** current branch → other local branches →
  remote-only branches → tags. Detached HEAD (`CurrentCommitHead`) counts as
  "current".
- **Tags** live in the same dropdown as branches; no separate tag spot.
  `ShowTagsInGraph = false` hides them from both the label and the dropdown.
- **Local+remote merging** reuses the existing `UseCompactBranchNamesInGraph`
  logic. With that setting off, local and remote show as separate refs (one in
  the slot, the other in the dropdown).
- **Interaction:** double-clicking the visible label checks out that ref (same as
  today's `DecoratorAt` behavior). Right-clicking a label or a dropdown entry
  opens that ref's existing submenu (the one the row context menu already builds).
  Clicking a dropdown entry checks it out only on double-click, matching labels;
  a single click just closes the dropdown. *(Builder may simplify if the
  dropdown makes double-click awkward — e.g. dropdown entries open the
  context menu on click.)*
- **Column width:** resizable by dragging its header separator; starts at ~160px;
  min ~60px; long names truncate with "…" and show the full name in a tooltip.
  Width persists like `LeftArea` / other column widths do.
- **Spacing:** row height grows from 26 to ~30px and lane `unitWidth` from 12 to
  ~22px in Branch column style only, so ≈9px circles and 3px lanes don't
  collide. Exact values are the builder's call as long as Classic stays at the
  current values.
- **HEAD that is also a merge:** hollow circle with "+" (merge style) plus the
  white HEAD ring around it.
- **Width persistence:** per repository, stored alongside `LeftArea` and the
  author column width in `RepositoryUIStates`.
- **Header buttons** (search / advanced options) keep their current position;
  the branch column must not cover them.
- **Dimmed state:** when a commit is not highlighted in the graph (filter /
  "highlighted in graph" off), circle and label use the same gray treatment the
  graph already uses for non-highlighted lines.

## Left to the builder

- Whether the branch column is a real `DataGridTemplateColumn` placed before the
  graph/subject column, or space reserved inside the existing first column; the
  graph overlay must be offset by the column's width either way.
- How the +N dropdown is implemented (Flyout/Popup) and styled to match
  SourceGit's existing popup brushes for light and dark themes.
- How the dashed connector is drawn (in the refs control, or in `CommitGraph`
  given the column width and the commit's dot X).
- How layout constants are parameterized (e.g. passing style into
  `Models.CommitGraph.Parse` or into `CommitGraphLayout`).

## Acceptance

- With **Classic** selected, the history view is pixel-identical to today.
- With **Branch column** selected:
  - Every ref visible in Classic is visible in the label or the dropdown.
  - A commit with 4 refs shows one label + "+3"; opening it lists the other 3.
  - Circles are large and lane-colored; HEAD has a ring; merges are hollow
    with "+".
  - Double-click on a label checks out; right-click gives that ref's menu.
  - Switching styles applies live without restarting.
- Works in light and dark themes; `dotnet build` succeeds with no new warnings.
