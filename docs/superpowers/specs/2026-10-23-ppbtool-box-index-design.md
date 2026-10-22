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
  #2  Room 7x7
  ```
  ```text
  Infinite Enter #0 of #2 (Intro)
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
- 所以索引应标识**具体引用**，不是标识 Level。

## 3. 索引定义

推荐：

```csharp
boxIndex = block.SubLevel.BlocksWithThisAsTheirSubLevel.IndexOf(block)
```

即：这个箱子在“所有引用该子层的箱子”列表中的序号。

优点：

- 同一 Level 的 primary/clone/inf 引用可以区分；
- 与引用选择器、hidden overlap 的顺序一致；
- `Block.DebugID` 不用，因为它是运行时创建顺序，不是稳定箱子编号。

缺点：

- 它不是编辑器文件里的 `Block id`；
- 但运行时没有保存编辑器 id，这是可用的最稳定索引。

显示为 0-based：

```text
#0
#1
#2
```

根层没有 `ParentBlock`，索引留空；树里只显示标题。

## 4. 左侧层级树 UI

新增一个固定宽度的索引列，放在标题左边：

```text
[#0] Intro
[#1] Room 7x7
  [#0] Box (3,4)
```

- 使用 `smallStyle`，宽度固定（例如 `34px`）；
- 保留现有折叠箭头、缩进、选中/悬停、水平滚动和 tags；
- 只有 `ParentBlock != null` 的节点显示 `#n`；
- 索引不参与语言切换。

## 5. owner 文案

新增 helper：

```csharp
string LevelLabel(Level level)
```

规则：

1. 如果 `level.hubAreaName` 非空：
   ```text
   #2 (Intro)
   ```
2. 如果无 area：
   ```text
   #2
   ```
3. 找不到节点：
   ```text
   5x5
   ```
   或空，不显示 `???`。

推荐统一格式：

```text
#<index> (<area>)
#<index>
```

例：

```text
Infinite Enter #0 of #2 (Intro)
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
2. 用 `level.infEnterBlocks` 反查 owner/degree；
3. 按 owner+degree 去重；
4. 显示：
   ```text
   Infinite Enter #0 of #2 (Intro)
   ```

这适用于主箱子、clone、∞ 进入箱和 ∞ 退出箱。

只有方块自身是 ∞ 退出块时，才追加它自己的：

```text
Infinite Exit #1 of Intro
```

不能把父层的 ∞ 退出硬套到同层所有箱子上。

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
  - 有 area → `#2 (Intro)`；
  - 无 area → `#2`；
- 不出现伪造的 `???`；
- 水平滚动后索引和标题仍对齐；
- 中英文切换不影响索引文本。

## 9. 待确认

1. 索引是否采用 `BlocksWithThisAsTheirSubLevel.IndexOf(block)`？
2. 有 area 时显示 `#2 (Intro)` 还是只显示 `Intro`？
3. 索引列宽度固定 `34px` 是否足够？
