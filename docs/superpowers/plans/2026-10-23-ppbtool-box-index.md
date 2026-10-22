# Box Index and Infinity Access Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a gray `[n]` box index before every tree title and use the same index/area label in Infinite Enter/Exit text.

**Architecture:** Reimplement the game's `ComputeTUILevels` order and assign 1-based numbers to `Level`s; add a separate `[vN]` sequence for void levels. Add a read-only `InfLink` list to `BlockTraits`, resolved through `Level.infEnterBlocks` / `infExitBlocks`. One `LevelGraph` helper combines index and optional `hubAreaName`. The tree draws `[n]` inline with the existing gray tag style. No game state changes.

**Tech Stack:** C# / BepInEx 5 / HarmonyX / Unity 2020.3 / IMGUI / .NET Framework 4.6.

**Spec:** `docs/superpowers/specs/2026-10-23-ppbtool-box-index-design.md`

## Global Constraints

- Display-only; never modify `World`, `Block`, `Level`, or save data.
- Box indexes are 1-based; void indexes are a separate `v` sequence.
- Do not use `Block.DebugID` as the index.
- Do not fabricate `???`; only show it when it is the actual `hubAreaName`.
- Infinite labels stay English in both UI languages.
- Preserve existing tree tags: `refs ×N`, `hidden ×N`, `void`.
- Root nodes without an index draw no bracket.
- Build with `dotnet build -c Release`; 0 warnings / 0 errors.

## Review Focus

- `tuiLevels` is per `Level`, not per reference; primary and clone share one index, matching `Draw.levelChar`.
- A level omitted by `ComputeTUILevels` (SpecialEffect 11) must still receive `[n]` via the full index list.
- Void levels must not consume normal numbers; they use `[vN]`.
- Non-hub `ComputeTUILevels` sorts by width descending; hub keeps world order.
- Inline `[n]` must not break indentation, arrow hit area, tags, selection, or horizontal scroll.

## File Structure

- `src/LevelGraph.cs` — box/void indexes, index label, tree tags, level label helper.
- `src/BlockTraits.cs` — `InfLink` list for all boxes.
- `src/TeleportOverlay.cs` — inline tree prefix and detail formatting.
- `src/UiText.cs` — only localized fallbacks; infinity labels remain literal English.

---

### Task 1: Add box and void indexes

**Files:**
- Modify: `src/LevelGraph.cs` (`LevelNode`, graph build, index computation)

**Interfaces:**
- Produces:
  ```csharp
  public int LevelNode.BoxIndex;   // 1-based, -1 if none
  public int LevelNode.VoidIndex;  // 1-based, -1 if not a void
  public string LevelNode.IndexLabel; // "[1]" / "[v1]" / ""
  ```

- [ ] **Step 1: Reimplement the TUI level order**

Port `Draw.ComputeTUILevels`:

```csharp
foreach (Level level in World.levels)
{
    if (!level.FilledWithWalls
        && (!World.LevelIsInfZone(level)
            || (World.WentToInfZone && level.blockList.Count > 1))
        && (level.GetExitBlock() == null
            || level.GetExitBlock().SpecialEffect != 11)
        && hubVisibility(level))
    {
        list.Add(level);
    }
}

if (!World.InHub())
    list.Sort((a, b) => b.width.CompareTo(a.width));

BoxIndex = list.IndexOf(level) + 1;
```

`hubVisibility` must copy the game condition exactly.

- [ ] **Step 2: Add separate hidden and void sequences**

SpecialEffect 11 is excluded from the visible TUI list. Give it an independent `[b1]`, `[b2]` sequence and do not consume a visible number.

For `level.InfZone`, use `[v1]`, `[v2]`; set `BoxIndex = -1`.

Do not use `blockList`, `BlocksWithThisAsTheirSubLevel`, or `DebugID`.

- [ ] **Step 3: Add `IndexLabel`**

```text
[1]
[10]
[b1]
[v1]
""
```

Never `[0]`, `[A]`, `[b]`, `[z]`, or `[?]`.

- [ ] **Step 4: Build**

Run: `dotnet build -c Release`
Expected: 0 warnings / 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/LevelGraph.cs
git commit -m "Add box and void indexes"
```

### Task 2: Resolve infinite access for every box

**Files:**
- Modify: `src/BlockTraits.cs`
- Modify: `src/LevelGraph.cs` (`TryGetInfOwner`, already exists)

**Interfaces:**
- Consumes: `LevelGraph.TryGetInfOwner(Block, out Level, out int)`
- Produces:
  ```csharp
  public struct InfLink
  {
      public bool Enter;
      public Level Owner;
      public int Degree;
  }

  public List<InfLink> BlockTraits.InfLinks;
  ```

- [ ] **Step 1: Add enter links for any box with SubLevel**

Scan `World.blocks` for:

```csharp
other.IsSomeInfEnterBlock && other.SubLevel == block.SubLevel
```

Resolve each with `TryGetInfOwner`, then deduplicate by `(Owner, Degree)`.

- [ ] **Step 2: Add the block's own exit link**

If `block.IsSomeInfExitBlock`, add its own link (`Enter = false`). Do not copy the parent level's exits onto every box.

- [ ] **Step 3: Keep an unresolved-owner fallback**

If `TryGetInfOwner` fails, keep the link with `Owner = null`; render `Infinite Enter #n` without `of ...`. Never render `of ???`.

- [ ] **Step 4: Build**

Run: `dotnet build -c Release`
Expected: 0 warnings / 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/BlockTraits.cs
git commit -m "Collect infinite access for every box"
```

### Task 3: Add the shared level label

**Files:**
- Modify: `src/LevelGraph.cs`
- Modify: `src/BlockTraits.cs` (label provider)

**Interfaces:**
- Produces:
  ```csharp
  public string DescribeLevelWithIndex(Level level);
  ```
- Consumes: `LevelNode.IndexLabel`, `Level.hubAreaName`.

- [ ] **Step 1: Implement the label helper**

Rules:

```text
[2] (Intro)   // index + area
[2]           // index only
Intro         // root area, no index
5x5           // no area/index
```

Do not use `???` as a fallback.

- [ ] **Step 2: Use it for infinity labels**

Change `BlockTraits.Compact` to accept a `Func<Level, string>` label provider, or assign each link's owner label before formatting.

Final text:

```text
Infinite Enter #0 of [2] (Intro)
Infinite Exit #1 of Intro
```

- [ ] **Step 3: Build**

Run: `dotnet build -c Release`
Expected: 0 warnings / 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/LevelGraph.cs src/BlockTraits.cs
git commit -m "Label levels with box index and area"
```

### Task 4: Draw inline gray index brackets

**Files:**
- Modify: `src/TeleportOverlay.cs` (`DrawTree`)
- Consumes: `LevelNode.IndexLabel`

- [ ] **Step 1: Compute and draw the prefix**

At the current `textX`:
- if `IndexLabel` is empty, draw the title at `textX`;
- otherwise draw `smallStyle` `[n]` in gray, measure its width, and draw the title with one space after it.

- [ ] **Step 2: Keep existing layout**

- no fixed index column;
- `contentW` includes index width;
- tags still start after the title;
- arrow and row hit rect unchanged;
- root nodes start directly with the title.

- [ ] **Step 3: Build and verify**

Run: `dotnet build -c Release`
Manual:
- `[1] Intro`;
- `[10] Next Box`;
- `[v1] Void 7x7`;
- horizontal scroll keeps all text aligned.

- [ ] **Step 4: Commit**

```bash
git add src/TeleportOverlay.cs
git commit -m "Draw inline box index brackets"
```

### Task 5: Use index/area labels in tree and details

**Files:**
- Modify: `src/LevelGraph.cs` (`BuildTags`)
- Modify: `src/TeleportOverlay.cs` (`BuildDetail`)
- Modify: `src/UiText.cs` only for a localized fallback

- [ ] **Step 1: Tree tags**

Use the shared label for each enter/exit link:

```text
Infinite Enter #0 of [2] (Intro)
Infinite Exit #1 of Intro
```

Keep `refs ×N`, `hidden ×N`, `void`; do not re-add aggregate `Infinite Enter ×N` / `Infinite Exit ×N`.

- [ ] **Step 2: Detail**

Show every deduplicated `InfLink` for the hovered box, including normal/clone boxes. Only an actual ∞ exit block gets its exit line.

- [ ] **Step 3: Build and verify**

Run: `dotnet build -c Release`
Manual fixtures:
- Epsilon: `Infinite Enter #0 of ... Intro`;
- Multi Infinite: `Infinite Exit #1 of ... Intro`;
- `(No) Reference`: one `Infinite Enter #1 of ... Epsilon`, not four;
- a normal box with no ∞ links: no fake infinity text.

- [ ] **Step 4: Commit**

```bash
git add src/LevelGraph.cs src/TeleportOverlay.cs src/UiText.cs
git commit -m "Use indexed infinity labels in tree and details"
```

### Task 6: Documentation and final acceptance

**Files:**
- Modify: `docs/superpowers/specs/2026-10-23-ppbtool-box-index-design.md`
- Modify: `README.md`

- [ ] Document that the index order is `blockList` order unless changed to ASCII grid order.
- [ ] Document `[n]`, `[vN]`, and the SpecialEffect 11 rule.
- [ ] Run the full manual checklist.
- [ ] Build `dotnet build -c Release`; expect 0/0.
- [ ] Commit.

## Manual Checklist

- [ ] Every visible non-root box has a `[n]` or `[vN]`.
- [ ] `[9]` is followed by `[10]`, not `[A]`.
- [ ] Primary and clone boxes of the same Level share one index, matching `Draw.levelChar`.
- [ ] Void nodes use `[v1]`, `[v2]`, not `[i]` or `[0]`.
- [ ] SpecialEffect 11 uses `[b1]`, `[b2]`, and does not change visible numbers.
- [ ] A real `???` area remains visible.
- [ ] No unresolved owner renders `of ???`.
- [ ] Normal/clone boxes show enter links when their level has them.
- [ ] Only an actual ∞ exit box shows its exit link.
- [ ] Tree layout survives narrow panels, horizontal scroll, and language switching.
- [ ] No regression in map, reference chooser, teleport, undo, or win button.

## Execution Notes

- This repository has no Unity unit-test project; use `dotnet build` plus the named Reborn hub cases.
- Port `ComputeTUILevels`; do not replace it with parent/block order.
- If SpecialEffect 11 numbering is rejected, keep the visible `tuiLevels` index and omit `[n]` for hidden levels.
