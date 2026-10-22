# Box Index and Infinity Owner Label Design

日期：2026-10-23
状态：**待确认**（本阶段只写设计，不实施）
基线：block traits / infinity owner 修正已完成
关联规格：
- [Block Traits 设计](2026-10-22-ppbtool-block-traits-design.md)
- [Inspection Log](2026-10-22-ppbtool-block-traits-inspection.md)

## 1. 目标

在左侧层级树中给每个箱子的标题左边增加一个**箱子索引**，并在无限进入/退出的 owner 文案中使用：

- 索引（始终有）；
- area name（如果该层在大厅里有名字）；
- 例如：
  ```text
  [2]  Room 7x7
  ```
  ```text
  Infinite Enter #0 of [2] (Intro)
  ```
  ```text
  Infinite Exit #1 of Intro
  ```

重点解决：

- 关卡内部的箱子没有 `hubAreaName`，现有标题只能显示 `Room 7x7` / `Box (x,y)`，无法区分同层多个箱子；
- 无限进入/退出 owner 只能显示 area；如果 owner 在关卡内部，需要索引兜底。

## 2. 关键概念

“原始箱子”不是独立对象：

- 每个有 `SubLevel` 的方块都是该层的一个引用；
- `SubLevel.GetExitBlock()` 是主引用/主箱子；
- clone、无限进入、无限退出都是同一 `SubLevel` 的其他引用；
- 游戏 `levelChar` 的索引单位是 **Level**，不是每个引用；
- 因此 primary / clone 会共享同一个 `[n]`，与游戏 ASCII mode 一致。

## 3. 索引定义

已查阅 `Draw.cs` 的游戏实现。游戏并不是按“父层箱子顺序”编号，而是：

```csharp
private static string levelChar(Level level)
{
    if (level.InfZone)
        return "i";

    int num = tuiLevels.IndexOf(level);
    if (num == -1)
        return "b";

    if (num < indexCharacters.Length)
        return indexCharacters[num].ToString();

    return indexCharacterOverflow;
}
```

其中：

```csharp
private static string indexCharacters =
    "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
private static string indexCharacterOverflow = "z";
```

`tuiLevels = ComputeTUILevels()`，过滤条件：

- `!level.FilledWithWalls`；
- `!World.LevelIsInfZone(level)`，或满足已进入 void 且 `blockList.Count > 1`；
- `level.GetExitBlock() == null || SpecialEffect != 11`；
- hub 可见性条件。

非 hub 时：

```csharp
tuiLevels.Sort((a, b) => b.width.CompareTo(a.width));
```

所以：

- 游戏编号是 **Level 在 tuiLevels 中的序号**，不是每个 box 引用各自编号；
- primary 和 clone 共享同一个 Level，因此共享同一个字母/数字；
- 虚空固定返回 `"i"`；
- SpecialEffect 11 被排除，`levelChar` 返回 `"b"`；
- 超过 35 个可见层统一返回 `"z"`。

### 本 mod 的转换

按你的要求，保留游戏序号，但把字母换成连续数字：

```text
游戏 A/B/C → 1/2/3
游戏 9/A → 9/10
```

即：

```csharp
index = tuiLevels.IndexOf(level);
label = "[" + (index + 1) + "]";
```

- 1-based；
- `[1]`、`[2]`、`[10]`；
- 不使用 A~Z；
- 不新增索引列，直接放在标题左边。

### SpecialEffect 11

游戏将它从 `tuiLevels` 排除，因此它没有字母位置；`levelChar` 会返回 `"b"`。

按确认结果：

- 可见层严格使用游戏的 `tuiLevels` 编号；
- SpecialEffect 11 使用**独立的** `[b1]`、`[b2]` 序列；
- 不占用可见数字；
- 不改变可见层的游戏编号。

### 虚空

游戏 `levelChar` 对 `InfZone` 返回 `"i"`，所有虚空都是 `i`。

改成独立编号：

```text
[v1] Void 7x7
[v2] Void 7x7
```

- 独立 `v` 序列；
- 不占用普通数字；
- 1-based。

## 4. 左侧层级树 UI

不新增索引列。索引直接作为灰色小字放在标题左边：

```text
[1] Intro  [refs ×1 Infinite Exit #0 of Intro]
[2] Room 7x7
  [3] Box (3,4)
```

- 使用 `smallStyle`；
- `[n]` 灰色；
- 右侧 `[refs ×N ...]` 继续使用现有灰色风格；
- 保留折叠箭头、缩进、选中/悬停、水平滚动；
- 根节点/无索引节点不显示方括号；
- 不预留固定宽度列，标题紧跟 `]` 后一个空格；
- `contentW` 计算要包含 `[n]` 宽度。

## 5. owner 文案

新增 helper：

```csharp
string LevelLabel(Level level)
```

规则：

1. 如果 `level.hubAreaName` 非空且有索引：
   ```text
   [2] (Intro)
   ```
2. 如果无 area 但有索引：
   ```text
   [2]
   ```
3. 如果根层有 area 但无索引：
   ```text
   Intro
   ```
4. 找不到节点：
   ```text
   5x5
   ```
   或空，不显示 `???`。

推荐统一格式：

```text
[<index>] (<area>)
[<index>]
Intro
```

例：

```text
Infinite Enter #0 of [2] (Intro)
Infinite Exit #1 of Intro
```

详情和树使用同一个 helper，避免两处不一致。

## 6. 所有箱子的无限入口

不是只有 `IsSomeInfEnterBlock` / `IsSomeInfExitBlock` 才显示。

“原始箱子”不是独立对象：

- 每个有 `SubLevel` 的方块都是该层的引用；
- 主箱子 = `SubLevel.GetExitBlock()`；
- clone / ∞ 进入 / ∞ 退出都是同一 `SubLevel` 的不同引用。

所以对任意有 `SubLevel` 的箱子：

1. 扫描 `block.IsSomeInfEnterBlock && block.SubLevel == 当前.SubLevel`；
2. 再扫描 `当前.SubLevel.infExitBlocks`；
3. 用 `level.infEnterBlocks` / `infExitBlocks` 反查 owner/degree；
4. 按 owner+degree 去重；
5. 显示：
   ```text
   Infinite Enter #0 of [2] (Intro)
   Infinite Exit #1 of [2] (Transfer)
   ```

这适用于主箱子、clone、∞ 进入箱和 ∞ 退出箱。

不能只看方块自身是不是 ∞ 退出块：普通箱子所属 Level 也可能有 ∞ 退出引用，例如 Reception 2 `(9,2)` 的 Transfer、`(9,3)` 的 Flip+、`(9,6)` 的 Redo。

## 7. 与现有 infinity owner 的关系

- owner 仍通过 `Level.infEnterBlocks` / `Level.infExitBlocks` 扫描；
- 索引只负责**展示** owner 的具体箱子；
- `infinite enter` 的 from id / owner list 不变；
- `infinite exit` 的 owner list 不变；
- 不改变游戏加载、绘制或传送。

## 8. 验收

- 大厅区域：树标题左边有索引；
- 关卡内部：`Room`/`Box` 节点左边有索引；
- 同层多个箱子可以通过索引区分；
- detail owner：
  - 有 area → `[2] (Intro)`；
  - 无 area → `[2]`；
- 不出现伪造的 `???`；
- 水平滚动后索引和标题仍对齐；
- 中英文切换不影响索引文本。

## 9. 待确认

1. 索引 1-based `[1]`、`[2]` 是否符合预期？
2. 可见层严格按 `tuiLevels`；SE11 `[bN]`；虚空 `[vN]` 已确认。
3. 索引 1-based `[1]`、`[2]` 是否符合预期？
