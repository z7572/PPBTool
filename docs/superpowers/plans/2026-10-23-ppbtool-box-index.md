# Box Index and Infinity Access Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every tree row a stable box index and show, for every box, which Infinite Enter/Exit links can reach its level.

**Architecture:** Add `BoxIndex` to `LevelNode` from the reference list order. Add a read-only `InfLink` list to `BlockTraits`, resolved through the game's `Level.infEnterBlocks` / `infExitBlocks` lists. Add one `LevelGraph` label helper that combines `#index` with optional `hubAreaName`. The tree draws a fixed index column; tree tags and detail text use the shared helper. No game state changes.

**Tech Stack:** C# / BepInEx 5 / HarmonyX / Unity 2020.3 / IMGUI / .NET Framework 4.6.

**Spec:** `docs/superpowers/specs/2026-10-23-ppbtool-box-index-design.md`

## Global Constraints

- Display-only; never modify `World`, `Block`, `Level`, or save data.
- Index is 0-based and never localized.
- Do **not** use `Block.DebugID` as the box index.
- Do not fabricate `???`; only show it when it is the actual `hubAreaName`.
- Infinite labels stay English in both UI languages:
  `Infinite Enter #0 of #2 (Intro)`.
- Preserve existing tree tags: `refs ×N`, `hidden ×N`, `void`.
- Root nodes have no index and must not show `#-1`.
- Build with `dotnet build -c Release`; 0 warnings / 0 errors.

## Review Focus

- A level with multiple references: primary, clone, infinite-enter, and infinite-exit boxes must get distinct indices.
- A root level and a level whose `ParentBlock` is null must not render an index or `#-1`.
- A `???` area that is a real `hubAreaName` must remain visible; an unresolved owner must not become `???`.
- Tree index, indentation, expand arrow, tags, selection, and horizontal scroll must not overlap.
- A normal/clone box with no infinite-enter references must stay clean; only an actual ∞ exit box gets an `Infinite Exit` line.

## File Structure

- `src/LevelGraph.cs`
  - `LevelNode.BoxIndex`
  - `DescribeLevelWithIndex(...)`
  - tree tag generation
- `src/BlockTraits.cs`
  - `InfLink` list for all boxes
  - formatting without assuming enter/exit ownership
- `src/TeleportOverlay.cs`
  - tree index column
  - detail formatting
- `src/UiText.cs`
  - only new labels that are actually localized; infinity labels stay literal English

---

### Task 1: Add `BoxIndex` to the level graph

**Files:**
- Modify: `src/LevelGraph.cs` (`LevelNode`, `BuildNode`, `nodesByLevel`)

**Interfaces:**
- Produces:
  ```csharp
  public int LevelNode.BoxIndex; // -1 when no parent
  public string LevelNode.IndexLabel; // "" or "#n"
  ```

- [ ] **Step 1: Add the field and label**

Add to `LevelNode`:
```csharp
public int BoxIndex;
public string IndexLabel => BoxIndex >= 0 ? "#" + BoxIndex : string.Empty;
```

- [ ] **Step 2: Compute the index**

In `BuildNode`, after `ParentBlock` is known:
```csharp
BoxIndex = parent != null && parent.SubLevel != null
    ? parent.SubLevel.BlocksWithThisAsTheirSubLevel.IndexOf(parent)
    : -1;
```

`IndexOf` must use the same reference list used by the reference chooser; do not use `DebugID`.

- [ ] **Step 3: Build**

Run: `dotnet build -c Release`
Expected: 0 warnings / 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/LevelGraph.cs
git commit -m "Add box index to level nodes"
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

- [ ] **Step 1: Add the link model**

`BlockTraits.From(block)` must populate `InfLinks` for any block with `SubLevel != null`:

1. scan `World.blocks` for:
   ```csharp
   other.IsSomeInfEnterBlock && other.SubLevel == block.SubLevel
   ```
2. resolve each through `TryGetInfOwner`;
3. deduplicate by `(Owner, Degree)`.

This covers primary, clone, infinite-enter, and infinite-exit boxes.

- [ ] **Step 2: Add the block's own exit link**

If `block.IsSomeInfExitBlock`, add its own link with:
```csharp
Enter = false;
Owner = resolved owner;
Degree = resolved degree;
```

Do not scan the parent level and attach its exit links to every box.

- [ ] **Step 3: Keep the no-owner fallback**

If `TryGetInfOwner` fails:
- keep the link with `Owner = null`;
- render `Infinite Enter #n` or `Infinite Exit #n`;
- never render `of ???`.

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
- Modify: `src/LevelGraph.cs` (`nodesByLevel`, new helper)
- Modify: `src/BlockTraits.cs` (`Compact` uses a label provider)

**Interfaces:**
- Produces:
  ```csharp
  public string DescribeLevelWithIndex(Level level);
  ```
- Format:
  ```text
  #2 (Intro)
  #2
  Intro
  ```
- Consumes: `LevelNode.BoxIndex`, `Level.hubAreaName`.

- [ ] **Step 1: Implement the helper**

Use the graph's `nodesByLevel` lookup:
- area + index → `#2 (Intro)`;
- index only → `#2`;
- area only → `Intro`;
- neither → `WxH`, never `???` except a real area name.

- [ ] **Step 2: Use it for infinity labels**

Change `BlockTraits.Compact` to accept the graph label provider, or assign the label before `Compact`:
```csharp
traits.InfOwnerTitle = graph.DescribeLevelWithIndex(owner);
```

The final text:
```text
Infinite Enter #0 of #2 (Intro)
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

### Task 4: Draw the index column in the tree

**Files:**
- Modify: `src/TeleportOverlay.cs` (`DrawTree`)
- Consumes: `LevelNode.IndexLabel`, `BoxIndex`

- [ ] **Step 1: Reserve the index width**

Add:
```csharp
const float IndexW = 34f;
```

Include it in `contentW`:
```csharp
float need = 24f + node.Depth * 13f + IndexW + titleW + tagsW + 90f;
```

- [ ] **Step 2: Draw the label and shift the title**

- draw `node.IndexLabel` at the old title position with `smallStyle`;
- draw the title at `textX + IndexW`;
- draw tags after the shifted title;
- leave the row hit rect and arrow unchanged.

- [ ] **Step 3: Handle root nodes**

If `BoxIndex < 0`, draw nothing in the index column; do not draw `#-1`.

- [ ] **Step 4: Build and verify**

Run: `dotnet build -c Release`
Manual:
- hub area row: `#n Intro`;
- interior row: `#n Room 7x7`;
- horizontal scroll keeps all columns aligned.

- [ ] **Step 5: Commit**

```bash
git add src/TeleportOverlay.cs
git commit -m "Draw box index in level tree"
```

### Task 5: Use indexed labels in tree tags and details

**Files:**
- Modify: `src/LevelGraph.cs` (`BuildTags`)
- Modify: `src/TeleportOverlay.cs` (`BuildDetail`)
- Modify: `src/UiText.cs` only if a new fallback is needed

- [ ] **Step 1: Tree tags**

For each enter/exit link, use:
```text
Infinite Enter #0 of #2 (Intro)
Infinite Exit #1 of Intro
```
Keep `refs ×N`, `hidden ×N`, `void`; do not add back
`Infinite Enter ×N` / `Infinite Exit ×N`.

- [ ] **Step 2: Detail**

Show every `InfLink` from Task 2, deduplicated by owner+degree:
```text
Infinite Enter #0 of #2 (Intro)
Infinite Exit #1 of Intro
```
A normal/clone box with no enter links shows nothing for infinity.

- [ ] **Step 3: Build and verify**

Run: `dotnet build -c Release`
Manual fixtures:
- Epsilon: `Infinite Enter #0 of #1 (Intro)` (or area-only label if no index);
- Multi Infinite: `Infinite Exit #1 of Intro`;
- `(No) Reference`: one `Infinite Enter #1 of Epsilon`, not four;
- a normal box with no ∞ link: no fake label.

- [ ] **Step 4: Commit**

```bash
git add src/LevelGraph.cs src/TeleportOverlay.cs src/UiText.cs
git commit -m "Use indexed infinity labels in tree and details"
```

### Task 6: Documentation and final acceptance

**Files:**
- Modify: `README.md`
- Modify: `docs/superpowers/specs/2026-10-23-ppbtool-box-index-design.md`

- [ ] Document that the index is a reference-list index, not the editor's `id`.
- [ ] Document the `#n (Area)` label and English-only infinity text.
- [ ] Run the full manual checklist.
- [ ] Build `dotnet build -c Release`; expect 0/0.
- [ ] Commit.

## Manual Checklist

- [ ] Every non-root tree row has a stable 0-based index.
- [ ] Primary and clone boxes of the same level have different indices.
- [ ] A box inside a level shows `#n` with no area.
- [ ] A hub row shows `#n (Area)` or the agreed area-only form.
- [ ] A real `???` area remains visible.
- [ ] No unresolved owner renders `of ???`.
- [ ] Normal/clone boxes show enter links when their level has them.
- [ ] Only an actual ∞ exit box shows its exit link.
- [ ] Tree layout survives narrow panels, horizontal scroll, and language switching.
- [ ] No regression in map, reference chooser, teleport, undo, or win button.

## Execution Notes

- This repository has no Unity unit-test project; use `dotnet build` plus the named Reborn hub cases.
- If area-only owner text is preferred, keep the tree index but omit `#n` from owner text. Confirm before implementing.
