# Block Special Attributes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Display a block's real traits and the current level's Priority / Inner Push / Shed mechanics without mislabeling level-wide settings as per-block properties.

**Architecture:** Add two small read-only model files (`BlockTraits`, `LevelTraits`) that read the live `Block` and `World` state. The overlay consumes them only in the detail line, the level mechanics strip, and the optional `SpecialEffect == 8` glyph renderer. No game state is modified.

**Tech Stack:** C# / BepInEx 5 / HarmonyX / Unity 2020.3 / IMGUI. Build with `dotnet build -c Release`.

**Spec:** `docs/superpowers/specs/2026-10-22-ppbtool-block-traits-design.md`

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

## Review Focus

- **Global mechanics shown as block traits** — a level with `shed` or `inner_push` must not make every box say it has that property; only a SE8 block gets glyphs.
- **SE8 without global mechanics** — an SE8 block with no enabled mechanic must show no Order/Shed/InnerPush icon.
- **Float false positives** — root hub blocks and float-in-space blocks can both have `OuterLevel == null`; do not silently label all roots as floating.
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

- [ ] **Step 4: Build and commit**

```bash
dotnet build -c Release
git add src/TeleportOverlay.cs src/UiText.cs
git commit -m "Show level-wide priority and push mechanics"
```

---

### Task 5: Draw game-faithful SpecialEffect 8 glyphs

**Files:**
- Modify: `src/TeleportOverlay.cs` (texture fields, `EnsureGameArtTextures`, `DrawBoxArt`, new `DrawBoxGlyphs`)
- Test: `tests/custom_levels/block_traits_se8.txt`

**Interfaces:**
- Consumes: `Block.SpecialEffect`, `World.showAttemptOrder`, `World.shedEnabled`, `World.innerPushEnabled`
- Produces:
  ```csharp
  private void DrawBoxGlyphs(Rect r, Block block, int cell);
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
if (block == null || block.SpecialEffect != 8 || block.SubLevel == null)
    return;

int index = 0;
if (World.showAttemptOrder) DrawGlyph(texOrder, index++);
if (World.shedEnabled) DrawGlyph(texShed, index++);
if (World.innerPushEnabled) DrawGlyph(texInnerPush, index++);
```

Glyph layout:
- size = `cell * 0.75`;
- x = box left + `cell * 0.05`;
- y = box top + `cell * 0.05 + index * cell`;
- color = `new Color(0f, 0f, 0f, 0.35f)`.

Call `DrawBoxGlyphs(r, block, cell)` immediately before `DrawBoxBorder`/`DrawInfSymbols` so icons sit on the box contents. Only SE8 boxes.

- [ ] **Step 3: Verify**

Load the fixture:
- SE8 + all three headers: three icons in Order/Shed/InnerPush order;
- SE8 + only shed: one Shed icon;
- non-SE8 box: no glyph;
- default level: no bar and no glyph.

- [ ] **Step 4: Build and commit**

```bash
dotnet build -c Release
git add src/TeleportOverlay.cs
git commit -m "Draw SpecialEffect 8 mechanic glyphs"
```

---

### Task 6: Float-in-space diagnostic and conservative label

**Files:**
- Modify: `src/BlockTraits.cs` (`NoOuterLevel` / label)
- Modify: `src/TeleportOverlay.cs` (detail label only)
- Test: `tests/custom_levels/float_in_space.txt`

**Interfaces:**
- Consumes: `Block.OuterLevel`, `Block.OuterLevelStart`, `Block.SubLevel`
- Produces: `BlockTraits.IsRoot`, optional `NoOuterLevel` label; no hard float claim yet.

- [ ] **Step 1: Create a float test fixture**

Create a level with a normal root block and a float-in-space reference block, following the official `Block ... floatinspace ...` format. Expected log:
```text
BlockTraits ... outer=null|... outerStart=null|... sub=...
```
for both; record whether a stable distinction exists.

- [ ] **Step 2: Add the conservative label**

In `BlockTraits.Compact()` add:
- `IsRoot` → `no_outer_level` (“无外层 / no outer level”);
- do **not** add “Float in space” unless the fixture proves a distinction beyond `OuterLevel == null`.

If a distinction is proven, add a separate `FloatInSpace` field and test; otherwise document the limitation and keep the conservative label.

- [ ] **Step 3: Verify root hub and float block**

- Root hub exit block: shows `no outer level` if applicable, **not** `float`;
- Float block: shows the same conservative label;
- No behavior changes.

- [ ] **Step 4: Build and commit**

```bash
dotnet build -c Release
git add src/BlockTraits.cs src/TeleportOverlay.cs src/UiText.cs tests/custom_levels/float_in_space.txt
git commit -m "Add conservative root/floating trait label"
```

---

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
- float-in-space is displayed conservatively because runtime has no boolean;
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
- [ ] Root hub and float block labels do not falsely claim float.
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
- Use `LogBlockTraits` for the float investigation; do not ship a guess.
- If the user chooses “detail only” and rejects SE8 map glyphs, Task 5 is skipped; Tasks 1–4 and 6 still stand.
- If the user rejects conservative `no outer level`, stop before Task 6 and confirm the desired float predicate.
