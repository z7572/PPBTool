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
- 所以索引应标识**具体引用**，不是标识 Level。

## 3. 索引定义

不再使用“引用列表序号”直接作为最终编号，也不再新增固定索引列。

目标：模仿游戏 ASCII mode 中每个箱子左侧的标号，但把 A~Z 改成连续数字：

```text
[1] Intro
[2] Room 7x7
[10] Next Box
```

规则：

- 1-based；
- 每个箱子在其**父层**内编号；
- 顺序采用父层 `blockList` 中箱子的创建/文件顺序；
- 只对真正可见的箱子编号；
- 不显示 A~Z，超过 9 后直接 `10`、`11`；
- gray small bracket，和右侧 `[refs ×1]` 同一风格。

### SpecialEffect 11

`SpecialEffect == 11` 的箱子在 ASCII mode 中不显示。

建议：

- 不给它分配数字编号；
- 树里可显示 `[hidden]` 或直接无编号；
- 不伪造 `[?]`。

### 虚空

虚空不是普通关卡内的箱子，没有 ASCII 标号。

建议单独编号：

```text
[v1] Void 7x7
[v2] Void 7x7
```

- 虚空使用独立的 `v` 序列，不占用普通箱子数字；
- 如果不想显示虚空编号，则显示 `[void]`，但不要显示 `[0]` 或 `[?]`。

该点需要确认。

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
2. 用 `level.infEnterBlocks` 反查 owner/degree；
3. 按 owner+degree 去重；
4. 显示：
   ```text
   Infinite Enter #0 of [2] (Intro)
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
  - 有 area → `[2] (Intro)`；
  - 无 area → `[2]`；
- 不出现伪造的 `???`；
- 水平滚动后索引和标题仍对齐；
- 中英文切换不影响索引文本。

## 9. 待确认

1. 父层 `blockList` 顺序是否与游戏 ASCII mode 的箱子标号顺序一致？如果 ASCII mode 按网格阅读顺序编号，需要改为 `y/x` 扫描。
2. 虚空使用独立 `[v1]` / `[v2]`，还是不显示编号只显示 `[void]`？
3. `SpecialEffect == 11` 的隐藏箱子，显示 `[hidden]` 还是完全不显示索引？
4. 索引 1-based `[1]`、`[2]` 是否符合预期？
