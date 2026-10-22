# Block Special Attributes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Display a block's real traits and the current level's Priority / Inner Push / Shed mechanics without mislabeling level-wide settings as per-block properties.

**Architecture:** Add two small read-only model files (`BlockTraits`, `LevelTraits`) that read the live `Block` and `World` state. The overlay consumes them in the detail line, the level mechanics strip, and the game-faithful `SpecialEffect == 8` glyph renderer. No game state is modified.

**Tech Stack:** C# / BepInEx 5 / HarmonyX / Unity 2020.3 / IMGUI. Build with `dotnet build -c Release`.

**Spec:** `docs/superpowers/specs/2026-10-22-ppbtool-block-traits-design.md`

**Inspection:** `docs/superpowers/specs/2026-10-22-ppbtool-block-traits-inspection.md`

## Global Constraints

- This is a display-only feature; do not modify `World`, `Block`, levels, or save data.
- Priority / Inner Push / Shed are **level-wide**; never put them in a block's own attribute list.
- `SpecialEffect == 8` is the only trigger for Order / Shed / InnerPush glyphs.
- Do not draw a glyph for a global mechanic on blocks without `SpecialEffect == 8`.
- `exitblock = false` means non-primary reference; do not label it "exit-only" or alter movement.
- All user-facing text must go through `UiText`; keep Chinese/English complete.
- Do not add dependencies; do not change the existing teleport/undo/navigation behavior.
- `dotnet build -c Release` must finish with 0 warnings / 0 errors at every task boundary.
- Existing files are large; add focused files rather than expanding `TeleportOverlay` unnecessarily.

## Reference Fixtures

- User-provided hub: `custom_levels/自定义大厅/Reborn's Hub v3/(Reborn's Hub v3) HUB/hub.txt` (36 floatinspace refs, mostly SE0/SE11).
- `.../Levels/3rd collection/Extrude/Ext0.txt`: `shed 1` + SE8 block.
- `.../Levels/3rd collection/Inner Push/Push_2.txt`: `inner_push 1` + SE8 block.
- `.../Levels/1st collection/5 - Empty/Ep_5.txt`: `attempt_order push,enter,eat` (partial list).
- `hub.txt` `Forgotten_levels... (7,1)`: normal ref + infinite-exit ref at the same cell, both `floatinspace=0`; this is a hidden overlap, not floatinspace.

These are manual game fixtures; do not copy them into the repository.

## Review Focus

- **Global mechanics shown as block traits** — a level with `shed` or `inner_push` must not make every box say it has that property; only a SE8 block gets glyphs.
- **SE8 without global mechanics** — an SE8 block with no enabled mechanic must show no Order/Shed/InnerPush icon.
- **Float false positives** — floatinspace refs load into a hidden void; do not infer “float” from `OuterLevel == null` or from void membership.
- **Detail overflow / localization** — long English and Chinese trait lines must not wrap or hide the level path.
- **Diagnostics spam** — `LogBlockTraits` must log once per block per map open, not every frame.

---

### Task 1: Add `BlockTraits` and the SpecialEffect catalog

**Files:**
- Create: `src/BlockTraits.cs`
- Test: build only

**Interfaces:**
- Consumes: `Block`
- Produces:
  ```csharp
  internal sealed class BlockTraits
  {
      public Block Block;
      public bool IsRoot;
      public bool IsExitBlock;
      public bool IsClone;
      public bool IsInfEnter;
      public bool IsInfExit;
      public bool IsPlayer;
      public bool IsPossessable;
      public bool IsCounting;
      public int PlayerOrder;
      public int SpecialEffect;
      public int Solved;
      public int Required;
      public static BlockTraits From(Block block);
      public string Compact();
  }

  internal static class SpecialEffectCatalog
  {
      public static string Name(int id);
      public static bool IsKnown(int id);
  }
  ```

- [ ] **Step 1: Create `src/BlockTraits.cs` with the model and catalog**

Implement `From(Block)` as a null-safe snapshot. Use:
- `IsRoot = block.OuterLevel == null`
- `IsExitBlock = block.SubLevel != null && block == block.SubLevel.GetExitBlock()`
- `IsClone = block.SubLevel != null && !IsExitBlock`
- `IsInfEnter = block.IsSomeInfEnterBlock`
- `IsInfExit = block.IsSomeInfExitBlock`
- `IsPlayer = block.IsPlayer`
- `IsPossessable = block.Possessable`
- `IsCounting = block.numRequired > 0`
- `Solved = block.numSolved`, `Required = block.numRequired`
- `PlayerOrder = block.PlayerOrder`
- `SpecialEffect = block.SpecialEffect`

`Compact()` must return only truthy short labels, separated by ` · `, using `UiText` keys (defined in Task 3):
`player`, `possessable`, `primary_exit`, `clone`, `inf_enter`, `inf_exit`, `counting`, `se8`.

`SpecialEffectCatalog.Name(0..13)` must return:
`None / Focus / Right flip / Left flip / Unused / Hub Intro block / Unused / Unused / Mechanic glyphs / Open void sides / Hub Intro ref / Hide in ASCII-grid / Multiplayer camera / Disable glyphs`.

- [ ] **Step 2: Build**

Run: `dotnet build -c Release`
Expected: PASS, 0 warnings / 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/BlockTraits.cs
git commit -m "Add read-only block trait model"
```

---

### Task 2: Add `LevelTraits`

**Files:**
- Create: `src/LevelTraits.cs`
- Test: build only

**Interfaces:**
- Consumes: `World.attemptOrder`, `World.showAttemptOrder`, `World.shedEnabled`, `World.innerPushEnabled`
- Produces:
  ```csharp
  internal sealed class LevelTraits
  {
      public IReadOnlyList<World.Attempt> AttemptOrder;
      public bool ShowAttemptOrder;
      public bool Shed;
      public bool InnerPush;
      public bool HasNonDefaultMechanics { get; }
      public static LevelTraits Current();
      public string Compact();
  }
  ```

- [ ] **Step 1: Implement `Current()` as a snapshot**

Copy `World.attemptOrder` into a new list so the UI cannot mutate the game list. Set:
```csharp
ShowAttemptOrder = World.showAttemptOrder;
Shed = World.shedEnabled;
InnerPush = World.innerPushEnabled;
```

`HasNonDefaultMechanics` is true when `ShowAttemptOrder` or `Shed` or `InnerPush` is true.

- [ ] **Step 2: Implement `Compact()`**

Output only enabled mechanics, e.g.:
```text
Priority: enter → eat → push → possess    Inner Push    Shed
```
Use localized action names from `UiText` (`action_push`, `action_enter`, `action_eat`, `action_possess`) and the `inner_push` / `shed` keys.

- [ ] **Step 3: Build**

Run: `dotnet build -c Release`
Expected: PASS, 0 warnings / 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/LevelTraits.cs
git commit -m "Add read-only level mechanics model"
```

---

### Task 3: Show traits in the block detail line and add diagnostics

**Files:**
- Modify: `src/ModConfig.cs` (new `ShowBlockTraits`, `LogBlockTraits`)
- Modify: `src/UiText.cs` (trait labels and action names)
- Modify: `src/TeleportOverlay.cs` (`BuildDetail`, `BlockAttributes`, `Show`/`RebuildGraph` log reset)
- Test: `tests/custom_levels/block_traits_se8.txt` (new fixture)

**Interfaces:**
- Consumes: `BlockTraits.From`, `SpecialEffectCatalog.Name`
- Produces: detail line `... · Player · Primary exit · SE8`; optional `LogBlockTraits` lines.

- [ ] **Step 1: Add the failing fixture**

Create `tests/custom_levels/block_traits_se8.txt` from the official format with:
```text
version 4
attempt_order enter,eat,push,possess
shed 1
inner_push 1
```
Include one playable player, walls/floor, and one box block with `specialeffect 8`. Expected before this task: no trait text in the map detail and no optional log.

- [ ] **Step 2: Add config and `UiText` keys**

In `ModConfig` add:
```csharp
public bool ShowBlockTraits { get; } // default true
public bool ShowTraitIcons { get; }  // default true; SE8 map glyphs
public bool LogBlockTraits { get; }  // default false
```

Add `UiText` keys:
`player`, `possessable`, `primary_exit`, `clone`, `inf_enter`, `inf_exit`, `counting`, `se8`, `action_push`, `action_enter`, `action_eat`, `action_possess`, `inner_push`, `shed`, `no_outer_level`, `special_effect`.

- [ ] **Step 3: Replace duplicate block attribute formatting**

In `TeleportOverlay.BuildDetail`, keep the existing “open level” prefix, then append:
```csharp
string traits = BlockTraits.From(block).Compact();
if (!string.IsNullOrEmpty(traits))
    info += " · " + traits;
```
Remove the old `BlockAttributes` call only if `BlockTraits.Compact()` covers the same flags. Keep `LevelGraph.BlockFlags` for the tree subtitle; do not change the tree.

- [ ] **Step 4: Add optional diagnostics**

When `config.LogBlockTraits` is true and the hovered block changes, call:
```csharp
Log.Info("BlockTraits " + block.xpos + "," + block.ypos
    + " root=" + traits.IsRoot
    + " exit=" + traits.IsExitBlock
    + " clone=" + traits.IsClone
    + " player=" + traits.IsPlayer
    + " possessable=" + traits.IsPossessable
    + " counting=" + traits.Solved + "/" + traits.Required
    + " se=" + traits.SpecialEffect + "(" + SpecialEffectCatalog.Name(traits.SpecialEffect) + ")"
    + " outer=" + (block.OuterLevel == null ? "null" : block.OuterLevel.hubAreaName)
    + " outerStart=" + (block.OuterLevelStart == null ? "null" : block.OuterLevelStart.hubAreaName));
```
Use a `HashSet<Block>` cleared in `Show`/scene rebuild to log once per block per map open.

- [ ] **Step 5: Build and manual verify**

Run: `dotnet build -c Release`
Expected: PASS.

Load `tests/custom_levels/block_traits_se8.txt`, enable `LogBlockTraits`, hover the SE8 box.
Expected:
- Detail line includes `SE8`;
- Log shows `se=8(Mechanic glyphs)`;
- No global `Shed`/`Inner Push` text is appended to the block unless it is a level mechanics strip (Task 4).

- [ ] **Step 6: Commit**

```bash
git add src/ModConfig.cs src/UiText.cs src/TeleportOverlay.cs tests/custom_levels/block_traits_se8.txt
git commit -m "Show block traits in map details"
```

---

### Task 4: Add the level mechanics strip

**Files:**
- Modify: `src/TeleportOverlay.cs` (`DrawMap`, new `DrawMechanicsBar`)
- Modify: `src/UiText.cs` if any short labels are missing
- Test: `tests/custom_levels/block_traits_se8.txt`

**Interfaces:**
- Consumes: `LevelTraits.Current()`
- Produces: optional 22px bar above the map canvas.

- [ ] **Step 1: Reserve the bar only when needed**

In `DrawMap`:
```csharp
LevelTraits mechanics = LevelTraits.Current();
bool showMechanics = mechanics.HasNonDefaultMechanics;
float mechanicsH = showMechanics ? 22f : 0f;
```
Place it directly under the existing detail bar; move the canvas down by `mechanicsH` and reduce its height accordingly.

- [ ] **Step 2: Implement `DrawMechanicsBar`**

Draw one line with `mechanics.Compact()`, `smallStyle`, clipping/ellipsis. Do not show the bar for a level with default `push,enter,eat,possess` and no shed/innerpush.

- [ ] **Step 3: Verify semantics**

Load the fixture:
- Header has `attempt_order enter,eat,push,possess`, `shed`, `inner_push`.
- Expected bar: `Priority: enter → eat → push → possess    Inner Push    Shed`.
- Hover a normal box: its detail must not repeat Priority/Inner Push/Shed.
- Hover the SE8 box: detail still only says SE8; icons are Task 5.
- Also load `Ep_5.txt` with `attempt_order push,enter,eat`; the bar must show exactly those three actions, not a fabricated `possess`.

- [ ] **Step 4: Build and commit**

```bash
dotnet build -c Release
git add src/TeleportOverlay.cs src/UiText.cs
git commit -m "Show level-wide priority and push mechanics"
```

---

### Task 5: Draw game-faithful SpecialEffect 8 glyphs

**Files:**
- Modify: `src/TeleportOverlay.cs` (texture fields, `EnsureGameArtTextures`, `DrawBoxArt`, `DrawMap`, new `DrawBoxGlyphs`)
- Test: `tests/custom_levels/block_traits_se8.txt`

**Interfaces:**
- Consumes: `Block.SpecialEffect`, `World.showAttemptOrder`, `World.shedEnabled`, `World.innerPushEnabled`, `selectedNode.ParentBlock`
- Produces:
  ```csharp
  private void DrawBoxGlyphs(Rect r, Block block, int cell, Level sub);
  private void DrawLevelBoxGlyphs(Level level, float originX, float originY, int cell);
  ```

- [ ] **Step 1: Load the three game textures**

In `EnsureGameArtTextures` add private texture loads:
```csharp
texOrder = GetPrivateTexture("Order");
texShed = GetPrivateTexture("Shed");
texInnerPush = GetPrivateTexture("InnerPush");
```
Add null-safe fields. If a texture is missing, skip that glyph; do not draw text fallback.

- [ ] **Step 2: Implement the game rule**

```csharp
if (!config.ShowTraitIcons || block == null || block.SpecialEffect != 8)
    return;

int index = 0;
if (World.showAttemptOrder) DrawGlyph(texOrder, index++);
if (World.shedEnabled) DrawGlyph(texShed, index++);
if (World.innerPushEnabled) DrawGlyph(texInnerPush, index++);
```

Glyph layout inside a box:
- `cw = r.width / sub.width`, `ch = r.height / sub.height`;
- first inner cell center: `(r.x + cw/2, r.y + ch/2)`;
- size = `Mathf.Min(cw, ch) * 0.75f`;
- subsequent icons shift `index * ch` down;
- color = `new Color(0f, 0f, 0f, 0.35f)`.

Call `DrawBoxGlyphs(r, block, cell, sub)` immediately before `DrawBoxBorder`/`DrawInfSymbols`.

- [ ] **Step 3: Keep the marker visible inside the opened box**

In `DrawMap`, after drawing the level cells, if `selectedNode.ParentBlock` has `SpecialEffect == 8`, call:
```csharp
DrawLevelBoxGlyphs(level, localX, localY, cell);
```
Draw at the **current level's top-left first cell**, using the same order/size rule. The marker belongs to the parent reference, not to the interior blocks.

- [ ] **Step 4: Verify**

Load the fixture:
- parent level: SE8 box icons centered in its first interior cell;
- click into the box: the same icons remain at the interior top-left cell;
- three mechanics: icons stack downward, each one inner cell tall;
- SE8 + only shed: one Shed icon;
- non-SE8 box: no glyph;
- default level: no bar and no glyph;
- Ext0 (`shed` + SE8): one Shed glyph;
- Push_2 (`inner_push` + SE8): one InnerPush glyph.

- [ ] **Step 5: Build and commit**

```bash
dotnet build -c Release
git add src/TeleportOverlay.cs
git commit -m "Draw SpecialEffect 8 mechanic glyphs inside boxes"
```

---

### Task 6: Exclude floatinspace from trait UI

**Files:**
- Modify: `docs/superpowers/specs/2026-10-22-ppbtool-block-traits-design.md` (already records the limitation)
- Test: provided `Reborn's Hub v3` hub

**Interfaces:**
- Produces: no `FloatInSpace` field, no float badge, no heuristic.

- [ ] **Step 1: Verify the runtime shape**

Open the provided `Reborn's Hub v3` hub and hover several `floatinspace` refs. Confirm that `OuterLevel` is a generated void, not null, and that no reliable distinction from other void refs exists in `Block` / `OuterLevelStart` / `SubLevel`.

Expected: no code path can recover the original `floatinspace` flag.

- [ ] **Step 2: Keep `floatinspace` out of the model**

Do **not** add `FloatInSpace` to `BlockTraits`; do **not** show a “float” label. Only `OuterLevel == null` may use the neutral `no_outer_level` label.

- [ ] **Step 3: Verify no false label**

- A floatinspace ref in a void: no “float” label;
- A normal void reference: no “float” label;
- A root-level block with `OuterLevel == null`: may show `no outer level`, not `float`.

- [ ] **Step 4: Build and commit docs**

```bash
dotnet build -c Release
git add docs/superpowers/specs/2026-10-22-ppbtool-block-traits-design.md
git commit -m "Document that floatinspace is not recoverable at runtime"
```

---

## Follow-up Fixes (from runtime inspection)

These fixes come from the [inspection log](../../specs/2026-10-22-ppbtool-block-traits-inspection.md). Do them after reproducing with `LogBlockTraits`.

### 8.1 Player-wall color

**Files:** Modify `src/BlockTraits.cs` (diagnostic fields), `src/TeleportOverlay.cs` (`DrawWallArt`)

- [ ] Add `hue/sat/val`, `startHue/startSat/startVal`, and `WallTileIndex` to the `BlockTraits` log.
- [ ] In `DrawWallArt`, use player color only when `block.IsPlayer && !possessedWall && (block.val > 0.0001f || block.sat > 0.0001f)`.
- [ ] Otherwise use `GameWallColor(colorSource, cell)` so Ext3 `(1,1)` and S0 `(2,4)` use the level wall color.
- [ ] Verify: Ext3 `(1,1)`, S0 `(2,4)` match surrounding walls; a normal colored player still uses its own color.
- [ ] Build and commit.

### 8.2 Flashing exit border on wall path

**Files:** Modify `src/TeleportOverlay.cs` (shared `IsFlashingExitBlock`, `DrawWallArt`)

- [ ] Extract the `DrawBlockBorder` condition:
  ```csharp
  bool flashing = block.IsSomeInfExitBlock
      || (block.OuterLevel != null
          && block.OuterLevel.InfZone
          && block.SubLevel != null
          && !block.SubLevel.FilledWithWalls
          && block.SpecialEffect != 12);
  ```
- [ ] Draw the same oscillating hue `0.15` border in `DrawWallArt` when the block has no `SubLevel`.
- [ ] Verify Ext5 `(5,5)` and C1 `(1,3)` both flash; an ordinary wall does not.
- [ ] Build and commit.

### 8.3 Phantom player face

**Files:** Modify `src/TeleportOverlay.cs` (`DrawBoxArt`)

- [ ] Change the phantom face condition from “player is inside `sub`” to also require `block == GameState.Player` (or `block.IsPlayer`).
- [ ] Verify Ext5 `(5,1)` no longer draws eyes; a real player box still draws its face.
- [ ] Build and commit.

### 8.4 PlayerOrder 2 mouth

**Files:** Modify `src/TeleportOverlay.cs` (`DrawFaceArt`)

- [ ] For `PlayerOrder == 2`, draw a white bar:
  ```text
  width  = r.width * 0.2666
  height = r.height * 0.05
  x      = 1.5 * eyeXOffset
  y      = 0.17 + 1.5 * eyeYOffset
  ```
- [ ] Flip `x` when `block.FlipH`.
- [ ] Verify S0 `(2,4)` (player order 1) and a multi-player level's order 2 show the bar.
- [ ] Build and commit.

### 8.5 Infinite Enter/Exit owner and degree

**Files:** Modify `src/BlockTraits.cs`, `src/UiText.cs`

- [ ] Add `InfOwnerTitle` and `InfDegree`.
  - 无限退出：owner = `block.SubLevel`；degree = `block.SubLevel.infExitBlocks.IndexOf(block)`；
  - 无限进入：owner = `block.OuterLevel`；degree = `block.SomeInfEnterNum`。
- [ ] Format:
  ```text
  Infinite Exit #1 of Intro
  Infinite Enter #0 of Multi Infinite
  ```
  Use game 0-based indexes and English text in both UI languages.
- [ ] Fall back to plain `Infinite Exit` / `Infinite Enter` when the title or index is unavailable.
- [ ] Verify with a level that has multiple `infenter`/`infexit` indexes.
- [ ] Build and commit.

### 8.6 SE8 glyph size

**Files:** Modify `src/TeleportOverlay.cs` (`DrawTraitGlyph` / `DrawLevelBoxGlyphs`)

- [ ] The game draws glyphs at `0.75 * inner cell`, not at full cell size.
- [ ] Change both parent-box and opened-interior glyphs to `size = cell * 0.75f`.
- [ ] Keep the first glyph centered in the first inner cell; subsequent glyphs step by one full inner cell.
- [ ] Verify Ext0/Push_2 glyph size matches the game, including after opening the box.
- [ ] Build and commit.

### 8.7 Tree ∞ owner/degree labels

**Files:** Modify `src/LevelGraph.cs` (`BuildTags`), `src/BlockTraits.cs` (shared owner/degree helper), `src/UiText.cs`

- [ ] For every `LevelNode` entry whose block is `IsSomeInfEnterBlock` or `IsSomeInfExitBlock`, add a compact tag:
  ```text
  Multi Infinite  [refs ×2 · Infinite Exit #1 of Intro]
  Target          [refs ×N · Infinite Enter #0 of Container]
  ```
- [ ] 无限退出：
  - node = `block.OuterLevel`（出口块所在的目的地层，即 Multi Infinite）；
  - owner = `block.SubLevel`（来源层，即 Intro）；
  - degree = `block.SubLevel.infExitBlocks.IndexOf(block)`；
  - scan exit blocks with `OuterLevel == level` because they are not in `LevelNode.References`.
- [ ] 无限进入：
  - node = `block.SubLevel`（目标层）；
  - owner = `block.OuterLevel`（来源层）；
  - degree = `block.SomeInfEnterNum`；
  - **only use the current node's `ParentBlock`**, do not enumerate every incoming enter ref.
- [ ] Use game 0-based indexes and English text in both UI languages.
- [ ] Remove the aggregate `Infinite Enter ×N` / `Infinite Exit ×N` tags; keep `refs ×N` / hidden tags / `void`.
- [ ] Verify `Intro`'s second infinite exit is visible on the `Multi Infinite` node as `Infinite Exit #1 of Intro`.
- [ ] Build and commit.

### Task 7: Documentation and final acceptance

**Files:**
- Modify: `README.md` (attribute display, config, limitations)
- Modify: `docs/superpowers/specs/2026-10-22-ppbtool-block-traits-design.md` (status: implemented)
- Test: full manual checklist

- [ ] **Step 1: Document**

README must state:
- Priority / Inner Push / Shed are level-wide;
- SE8 is only a glyph marker;
- `exitblock=false` is non-primary, not “exit-only”;
- floatinspace is not displayed; the runtime cannot recover the flag (see Task 6);
- config keys.

- [ ] **Step 2: Build**

```bash
dotnet build -c Release
```

Expected: 0 warnings / 0 errors.

- [ ] **Step 3: Manual acceptance checklist**

- [ ] SE8 + all mechanics shows three glyphs in Order/Shed/InnerPush order.
- [ ] SE8 + only shed shows one Shed glyph.
- [ ] Non-SE8 box shows no mechanics glyph.
- [ ] Normal box does not show Priority/Inner Push/Shed as its own trait.
- [ ] Level mechanics bar appears only for non-default mechanics.
- [ ] No block is labeled “float” just because it is in a void.
- [ ] Chinese/English switch updates all new text.
- [ ] Detail line ellipsizes cleanly at narrow width.
- [ ] No regression in teleport, back/forward, reference chooser, undo, or win button.

- [ ] **Step 4: Commit**

```bash
git add README.md docs/superpowers/specs/2026-10-22-ppbtool-block-traits-design.md
git commit -m "Document block trait display"
```

---

## Execution Notes

- This repository has no unit-test project for Unity types; each task uses `dotnet build` plus the named custom-level fixture. Do not invent a fake Unity test project for this change.
- Do not attempt to recover `floatinspace` from runtime `Block` fields; use Task 6's documented limitation.
- If a future request needs actual float detection, create a separate source-file parsing design.
- Map SE8 glyphs are required in this plan; do not treat Task 5 as optional.
