# PPBTool v2 — 层级浏览器、∞ 导航与地图语义 设计规格

日期：2026-10-21
状态：**待审阅**（本轮只写规格，不改代码）
基线：v1 已提交（`16a331f`），`dotnet build` 0 warning / 0 error
前置文档：[v1 设计规格](2026-10-21-ppbtool-design.md)

## 1. 本轮目标

在 v1（一键通关 + 可撤销传送地图）基础上，把“地图浏览器”升级为真正的**层级浏览器**：

1. 点击可进入的箱子时，**打开该箱子的层级**，而不是把玩家传送到箱子内部中心。
2. 新增“返回上一级”：跳到包含当前箱子的那个箱子的层级。
3. 新增手动前往该层的 **∞ 进入 / ∞ 退出** 对应层级，并可在树/地图中标记。
4. 层级树可**折叠/展开**。
5. 可选择**隐藏虚空中的箱子/层级**。
6. 地图绘制更多元素：玩家位置、目标光标、箱子落点、快速移动、按钮/终点/传送门/信息/碎裂地砖等全部地砖（**复用游戏纹理/缩略图**，图案与游戏一致），以及箱子的翻转、克隆（浅色箱）、∞、可附身等属性。
7. 修复用户观测到的 5 个 UI 问题，并继续审查代码排除同类问题。
8. **一键通关播放原版“逐渐变白”动画**：不再使用调试键的瞬间结算写法。
9. **∞ 进入/退出只改变 UI 层级**；新建 ∞ 区必须二次确认；按多重无限（多索引）完整列出。

## 2. 关键语义（本轮澄清）

- **箱子格子 = 入口，不是落点。**
  - 点击/Enter 一个 `Block.SubLevel != null` 的格子 → 左栏选中该箱子的 `SubLevel`，右侧地图切换到箱子内部。
  - 不再直接传送到箱子内部。
  - 要传送到箱子内部：先进入该层级，再点击内部空格。
- **空格子 = 传送落点。**
  - 点击/Enter 一个“空且非传送门”的格子 → 执行 v1 的可撤销传送。
- **传送门格子**：只显示，不传送；点击提示“传送门不可作为落点”。
- **墙/玩家/不可进入块**：点击只提示，不改变状态。

## 3. 游戏内部模型（实现依据）

| 概念 | 依据 |
|---|---|
| 层级树 | `World.levels`；`Block.SubLevel` 是子层；父层由拥有该 SubLevel 的 `Block.OuterLevel` 给出 |
| 父块坐标陷阱 | `Level.GetExitBlock()` 返回 `BlocksWithThisAsTheirSubLevel[0]`，是**父层里的箱子块**；其 `x/y` 属于父层，不能当作子层内部坐标 |
| 虚空层 | `Level.InfZone == true`（由 `LoadLevel.CreateInfZone()` 创建，7×7，黑色虚空） |
| ∞ 退出 | `level.infExitBlocks[n]` 是一个块：`block.SubLevel == level`。按 `Movement.AttemptToExit`，玩家从 `level` 无限退出时落到 `block.OuterLevel` 中 `block.x/y + 退出方向` 的相邻格。`block.OuterLevel` 不一定是虚空，取决于该块被放在哪一层 |
| ∞ 进入 | `level.infEnterBlocks[n]` 是一个块：按 `Movement.AttemptToEnter`，`ComputeEnterTargetPos` 会把玩家放到 `block.SubLevel` 的边缘格。该子层不一定是 5×5，取决于该块实际拥有的 SubLevel |
| ∞ 区生成 | `LoadLevel.CreateInfExitZones()` 在读档时为每个正常层生成 `infExitBlocks[0]`；`CreateInfEnterZone` 在首次 ∞ 进入时按需生成。生成器默认创建“虚空容器 + 5×5 房间”，但 UI 必须按**实际 Block/Level** 导航，不能硬编码 5×5/虚空 |
| 翻转 | 进入翻转箱子会翻转玩家与摄像机；路径翻转 = 根→目标层所有父块 `FlipH` 的异或 |
| 克隆箱（浅色） | 游戏 `Draw.cloneIntensity()`：当 `block != block.SubLevel.GetExitBlock()` 时强度为 1，颜色变亮/降饱和。即“同一 SubLevel 的多个箱子中，不是主出口箱的那些”就是浅色克隆箱 |
| 一键通关 | 正常结算由 `UndoManager.CheckWinCondition()` 设置 `Winning=true; WinCount=0; WinSoundPlayed=false`；`World.UpdateInner` 用 1.5s 把 `WinAnimT` 从 0 推到 1，驱动墙面/箱子颜色逐渐变亮发白（TUI 下另有白色叠加）。v1 的 `WinCount=1.5f` 是调试键的瞬间写法，v2 改回 0 |
| 暂停 | `World.State == WS.Paused`；`World.UpdateInner` 被 Harmony Prefix 跳过即可冻结 |

## 4. 数据层设计

### 4.1 父层选择：`PreferredParent(Level)`

问题：一个层可能同时被“正常箱子”和“虚空里的 ∞ 块”引用。v1 取第一个父块，可能把正常层挂到虚空下，导致树错位/重复。

规则：

1. 遍历 `level.BlocksWithThisAsTheirSubLevel`；
2. 忽略 `OuterLevel == null` 的块（虚空根层的所有者块）；
3. 优先返回 `OuterLevel.InfZone == false` 的父块；
4. 否则返回第一个虚空父块；
5. 都没有则返回 null（该层是根）。

### 4.2 层级树构建

- 对每个 `World.levels` 求 `PreferredParent`，建立 `parentByLevel` 与 `childrenByLevel`。
- 根 = 没有有效父块的层。
- 根排序：普通根在前，虚空根在后。
- 每个层只建一个节点（多父层只挂到 preferred parent 下；其余父块仍可在地图上点击导航）。
- 节点字段：
  - `Level`、`ParentBlock`、`ParentNode`、`Title`、`Subtitle`、`Tags`、`Depth`、`Children`、`ContainsPlayer`。
- 标题：
  - 虚空层：`Void WxH`（多个虚空根加 `#n`）；
  - ∞ 进入目标层：按实际标题显示（`hubAreaName` / 尺寸 / `∞Room`），并带 `∞进入` 标签；生成器默认 5×5，但以实际 `Level` 为准；
  - `hubAreaName` 去下划线；
  - 根层用 `World.currentLevelName`；
  - 其它箱子：`Box (x, y)`。
- 标签（`Tags`）：
  - `void`；
  - `∞进入块` / `∞退出块`（当前层的父块是 ∞ 块时）；
  - `∞进入←n` / `∞退出←n`（有 n 个层通过 ∞ 进入/退出指向本层时）；
  - 父块属性：`{flip clone possess locked ...}`，其中 `clone` 使用游戏自身的浅色判定 `block != block.SubLevel.GetExitBlock()`。

### 4.3 ∞ 链接模型

```text
InfLink {
  bool Enter; int Index;
  Level Source;      // 当前层 L
  Block Block;       // L.infEnterBlocks[n] 或 L.infExitBlocks[n]
  Level Container;   // Block.OuterLevel：这个 ∞ 块实际所在的层
  Level Target;      // Enter ? Block.SubLevel : Block.OuterLevel
  int TargetX, TargetY; // 退出：Block 在 Container 中的格子；进入：-1（用落点）
}
```

- 对选中层 `L`：
  - `L.infEnterBlocks[n]` 非空：
    - `Container = block.OuterLevel`；
    - `Target = block.SubLevel`（玩家无限进入 L 后到达的层，实际尺寸/内容以它为准）；
    - 跳转时选中 `Target`，光标放到该层的最近可站立空格。
  - `L.infExitBlocks[n]` 非空：
    - `Container = block.OuterLevel`（玩家从 L 无限退出后出现的层，不一定是虚空）；
    - `Target = Container`；
    - 跳转时选中 `Container`，光标放到 `block` 的格子 `(block.xpos, block.ypos)`，即 ∞ 出口箱。
- **导航严格按游戏逻辑**：∞ 进入对应“进入后到达的子层”（`AttemptToEnter` → `SubLevel`）；∞ 退出对应“退出后出现的容器层 + 出口箱”（`AttemptToExit` → `OuterLevel`）。不使用固定的 5×5/虚空假设。
- `GetInfLinks(L)` 返回两者，UI 按方向分组。
- 菜单文案使用**实际标题**，例如：
  - `∞进入 #0 → ∞Room 5x5`（若 Target 是 5×5）；
  - `∞退出 #0 → Box (3,3) @ Void 7x7`（若 Container 是虚空）。
  - 不硬编码“5×5”“虚空”字样，标题来自 `Level.hubAreaName / 尺寸 / Void`。
- 反向计数：供树标签 `∞进入←n` / `∞退出←n`。
- 若 ∞ 进入/退出链接不存在，或用户想新增一个：菜单提供“＋ 新建 ∞ 进入区 / ＋ 新建 ∞ 退出区”，但**第一次点击只弹确认**，确认后才调用 `LoadLevel.CreateInfEnterZone(L, n)` / `CreateInfExitZone(L, n)`（见 5.5）。
  - `n` = 第一个空位（`list[i] == null`），没有空位则 `list.Count`；不覆盖已有索引。
  - 生成器默认形式：∞进入 = 虚空容器 + 5×5 房间；∞退出 = 虚空容器 + 出口块。
  - 生成是**显式操作**，会改动当前场景（新增层/块）；不可撤销；生成后 `World.ComputeBlockLists()` + 重建树 + 跳到实际 Target。
  - 生成失败（如根层没有父块、`GetExitBlock()` 抛异常）→ 提示，不改变状态。

### 4.4 落点与翻转

- 箱子落点 = 箱子子层内部、离中心最近的可站立空格（BFS，失败则全层扫描）。
- 传送翻转 = `PathFlip(currentLevel) ^ PathFlip(targetLevel)`；同时应用到 `player.FlipH` 与 `Draw.FlipH`，并写入 `UndoManager.Turn` 的 `From/ToCameraFlipH`。
- `PathFlip` 使用 `PreferredParent`，避免虚空父块干扰。

### 4.5 多重无限（multi-infinity）

游戏不是“每个层只有一个 ∞ 入口/出口”，UI 必须按集合处理：

- `Level.infEnterBlocks` / `Level.infExitBlocks` 是 **List**，索引 `n` 可以：
  - 有多个非空项（∞进入 #0、#1、#2…）；
  - 中间存在 `null` 空洞（例如只生成了 #0 和 #2）；
  - 同一个目标层被多个索引/多个源层指向。
- ∞ 区还会**链式/嵌套**：
  - 生成的 ∞ 房间/虚空本身也可能带 ∞ 入口/出口；
  - `Movement.AttemptToEnter` / `AttemptToExit` 在 `InfEnterUnwinding` / `InfExitUnwinding` 时会把 `n` 递增，按需继续 `CreateInfEnterZone(level, n)` / `CreateInfExitZone(level, n)`；
  - 因此树里可能出现“同一层被多条 ∞ 路径引用”，也可能出现环状引用。
- 处理规则：
  - 每个非空索引都生成一条独立 `InfLink`，菜单逐条列出，不合并、不只取 #0；
  - 树按 `Level` 去重（每个层一个节点），标签显示 `∞进入←n` / `∞退出←n` 的计数；
  - 同一目标层被多条链接指向时，菜单仍保留每条链接及其 `Index/Source`；
  - 生成新 ∞ 区时选择**第一个空位**（`list[i] == null` 的 i），没有空位则用 `list.Count`；不覆盖已有索引；
  - 所有父层/翻转/树递归都有 64 层保护，避免环状 ∞ 引用导致死循环；
  - `PreferredParent` 仍优先非虚空父块，避免正常层被挂到 ∞ 容器下；仅通过 ∞ 可达的层保留在 ∞ 容器下。
- ∞ 导航只改变 UI 选中层；只有“新建 ∞ 区”会改场景，且必须二次确认（见 5.5）。

## 5. 用户可见行为

### 5.1 打开/关闭

- `F8` 打开/关闭；`Esc` 关闭。
- **允许在 `Playing` 与 `Paused` 打开**：
  - 暂停界面按 F8 → 打开地图，不再提示“当前状态不能通关”；
  - 关闭后恢复到暂停界面（`State = Paused`）。
- 其它状态（过场/结算/Show/FastTravel 等）→ 提示“当前界面不能打开传送地图”。
- 打开时冻结 `World.UpdateInner`；关闭后额外吞 2 帧输入，防止 Esc/左键漏给游戏。
- 打开时备份并强制显示鼠标；关闭时恢复。

### 5.2 层级树

- 可折叠：每个有子节点的行左侧有 `- / +` 按钮；点击行选中，点击箭头折叠。
- 默认展开策略：
  - 所有根层展开；
  - 玩家所在路径上的祖先展开；
  - 其它有子节点的层默认折叠；
  - 虚空根默认折叠。
- 工具栏提供“折叠全部 / 展开全部”。
- 自绘滚动条（替代 Unity 原生 `BeginScrollView`），滚轮滚动。
- 行内容：箭头、缩进、标题、`[标签]`、玩家行黄色高亮 + “当前”。
- Hover 高亮必须与行完全对齐（本轮 bug #1）。

### 5.3 地图

- 点击/Enter 分派：
  - 箱子格（包括虚空层里的箱子）→ 打开该箱子层级；
  - 空格且非传送门 → 传送（虚空层里的空格同样可传送）；
  - 其它 → 提示。
- 光标：
  - 跟随鼠标（仅在鼠标实际移动时）与方向键/WASD；
  - 颜色：可传送=绿、箱子=青、传送门=紫、墙/玩家=红；
  - 中心显示 `+ / > / P / x`。
- 玩家标记：黄色圆环 + 点。
- 箱子落点标记：绿色圆环 + 十字，表示“进入该层会落在哪里”。
- **地砖按游戏实际图案绘制**，不是纯色块 + 字母占位。实现方式：复用游戏自身的纹理/缩略图 + 复刻游戏的比例/几何。
- 纹理来源（`Draw.instance` 私有字段，反射缓存；只读，不修改游戏对象）：
  - `PlayerButtonOnlyEyesTex` / `PlayerButtonOnlyEyesSmallTex`
  - `InfoFloorTex`
  - `SmileFloorTex`
  - `UpArrow` / `DownArrow`
  - 传送门：`Hub.puzzleData[floor.SceneName].thumbnail`；为空时 `Resources.Load("level_thumbnails/" + scene)`，再为空用白块。
- 各类地砖：

| 地砖 | 游戏表现 | 地图复刻 |
|---|---|---|
| Button | 四条边框，厚约 6.64% 单元格，中间透明；颜色由出口块 HSV + alpha 0.6 | 4 条 Fill，同比例；中间透明 |
| PlayerButton | 同上 + `PlayerButtonOnlyEyesTex`（大格）/`...SmallTex`（小格） | 边框 + 眼睛纹理 |
| LevelPortal | 关卡缩略图，按 won/locked/hard 着色；Hard≠0 有硬背景色；未通关加白色标记 | `GUI.DrawTexture(thumbnail)` + 同色 tint + 白色标记 |
| Info | 边框 + `InfoFloorTex` | 边框 + 纹理 |
| Smile | `SmileFloorTex`，0.9 缩放，颜色 ×1.75 | 纹理 |
| FastTravel | `UpArrow` + `DownArrow` 两张纹理，0.45 缩放，上下排列 | 两张纹理 |
| BreakFloor | 0.65 缩放的 3×3 九宫格，块间 13% 间隙 | 9 个 Fill |
| GalleryFloor | 0.7 缩放：上方 1.0×0.7 横条 + 下方 1.2×0.15 横条 | 2 个 Fill |
| Show | 与 Break 相同的九宫格，颜色更亮 | 九宫格 |
| DemoEnd | 游戏里是全屏黑幕/文字，不是格子图案 | 地图特殊标记（黑底 + DEMO） |
| Item / Label | 游戏正常关卡绘制路径没有独立图案 | 地图用简单标记（金色菱形 / 标签） |

- 纹理按单元格缩放绘制，保留 alpha；小格（<12px）时退化为纯色/边框，避免糊成一团。
- 传送门缩略图是“和游戏里显示一样”的关键，必须优先使用；锁定/已通关只做 tint，不替换图案。
- 箱子属性徽标（小字）：`F` 翻转、`C` 克隆（浅色箱）、`I` ∞进入块、`O` ∞退出块、`*` 生成∞、`P` 可附身、`L` 锁定。
- **克隆箱按游戏自身浅色逻辑渲染**：`block != block.SubLevel.GetExitBlock()` 时用更亮、更低饱和的箱色；主出口箱用正常箱色。
- 详情栏显示光标格坐标、内容、地砖名、属性、落点。
- 图例分两行，覆盖主要颜色/图案。
- 虚空层使用深色背景 + 淡 `VOID` 水印；虚空层里的箱子与普通箱子一样可进入，空格可传送。

### 5.4 返回上一级

- 工具栏按钮 `← 上一级`、快捷键 `Backspace`。
- 目标 = `selectedNode.ParentNode`。
- 已在根层 → 提示“已经在最顶层”。
- 父层被虚空过滤隐藏时，自动关闭过滤并跳转。

### 5.5 ∞ 进入 / ∞ 退出

- 工具栏两个按钮：`∞ 进入`、`∞ 退出`。
- 弹出菜单列出当前层的**所有**对应链接（多重无限，不只 #0），使用实际层级标题：
  - `∞进入 #0 → ∞Room 5x5`（Target = `block.SubLevel`）；
  - `∞退出 #0 → Box (3,3) @ Void 7x7`（Target = `block.OuterLevel`，光标对准出口箱）；
  - 有多个索引时逐条列出 `#0 / #1 / #2 …`。
- **点击已有链接只改变 UI 选中层**：不生成、不改场景、不移动玩家。
- 菜单底部始终提供“＋ 新建 ∞ 进入区 / ＋ 新建 ∞ 退出区”（即使已有链接），用于手动补一个 ∞ 区。
- 新建必须**二次确认**：
  1. 第一次点击“新建”只打开确认面板：
     “这会修改当前场景：新增 ∞ 容器/房间/块，且不可撤销。确认生成到索引 #n？”
  2. 面板按钮 `确认生成` / `取消`。
  3. 只有点 `确认生成` 才调用 `LoadLevel.CreateInfEnterZone(L, n)` / `CreateInfExitZone(L, n)`；
     `n` 取第一个空位（`list[i] == null`），没有空位则取 `list.Count`。
  4. 生成后 `World.ComputeBlockLists()` → 重建树 → 跳到实际 Target。
  5. `取消` 不改变任何状态。
- 树标签、地图徽标、详情栏共同标记 ∞ 层级/块；菜单保留每条链接的 `Index/Source`。
- **不把“5×5/虚空”写死**：自定义/原版关卡可以把 ∞ 块放在任意层、指向任意 SubLevel，UI 按 Block 的实际 `OuterLevel/SubLevel` 展示与跳转。

### 5.6 虚空过滤

- 工具栏开关 `隐藏虚空 / 显示虚空`，快捷键 `H`，持久化到 `[Teleport] HideVoidBoxes`。
- 过滤规则：
  - 隐藏 `Level.InfZone == true` 的层（含其所有箱子）；
  - 隐藏父块位于虚空层的节点（虚空箱子的内部层，如生成的 ∞Room）。
- 被隐藏的节点仍保留在 `graph` 中；当前选中层不受影响。
- 跳转到被隐藏的 ∞ 层级时自动关闭过滤。
- **过滤只影响显示，不影响交互**：
  - 关闭过滤、查看虚空层时，虚空层里的箱子可以正常点击进入，空格可以正常传送；
  - 虚空层不是只读层，玩家可以被传送到虚空层（与游戏 ∞ 出口机制一致）。

### 5.7 缩放与平移

- 缩放范围 `0.1x – 10x`；格子像素 `2 – 400`。
- 滚轮以鼠标为中心缩放；工具栏 `+ / - / 适应`。
- 平移：
  - 左键拖动（与点击区分：移动超过 4px 视为拖动，不触发点击动作）；
  - 右键/中键拖动；
  - 水平、垂直都可拖动；
  - 至少保留 24px 地图可见。
- 只绘制**完整落在画布内**的格子；格子尺寸与原点取整，边缘不出现半格。

### 5.8 视觉与控件

- 关闭按钮、工具栏按钮、树行、滚动条全部自绘，使用与面板一致的深色扁平风格，不使用 Unity 原生滚动条/按钮皮肤。
- 中/英文案跟随 `Prefs.Language`。
- Toast 在关闭状态下也能显示（例如暂停时按 F8 的状态提示）。

### 5.9 一键通关的白化动画

- v1 使用游戏调试键写法 `Winning=true; WinCount=1.5f`，会跳过 1.5 秒白化过程。
- v2 改为与正常结算一致：
  - `World.Winning = true;`
  - `World.WinCount = 0f;`
  - `World.WinAnimT = 0f;`
  - `World.WinSoundPlayed = false;`
- 随后完全交给 `World.UpdateInner`：`WinAnimT` 在 1.5 秒内从 0 平滑到 1，墙面/箱子颜色逐渐变亮发白（TUI 下另有白色叠加），再执行存档/解锁/下一关。
- F7 的前置校验、CustomHubs 兼容、存档路径均不变。

## 6. 布局

```text
┌──────────────────────────────────────────────────────────────────────┐
│ 传送地图  场景: hub  层级: 83/41         层级: hub / Area / Box   [关闭] │
├──────────────────────────────────────────────────────────────────────┤
│ [← 上一级] [∞进入] [∞退出] [适应] [缩小] [放大] [隐藏虚空] [折叠全部] … │
├───────────────────────┬──────────────────────────────────────────────┤
│ 层级 (41)             │ (12, 7) 箱子 · 打开层级: Area Intro           │
│ - hub                 │ ┌──────────────────────────────────────────┐ │
│   - Area Intro        │ │  ██  ▣  ·  ◇  …  俯视格子图              │ │
│     + Box (4,7)       │ │  ◎ 玩家   ▣ 箱子   ◇ 传送门   + 落点      │ │
│   + Area Center       │ └──────────────────────────────────────────┘ │
│   + Void 7x7          │ [墙][箱][玩家][传送门][落点][虚空][按钮]…   │
├───────────────────────┴──────────────────────────────────────────────┤
│ 操作提示 …                                                           │
│ 状态/Toast …                                                         │
└──────────────────────────────────────────────────────────────────────┘
```

## 7. 交互细节与边界

| 情况 | 行为 |
|---|---|
| 点击箱子 | 打开层级；不传送；不要求箱子有空格 |
| 点击虚空层里的箱子 | 与普通箱子相同：打开层级；过滤只影响是否显示 |
| 点击空格 | 传送；登记 Undo Turn；可选自动关闭 |
| 点击传送门 | 提示，不传送 |
| 点击墙/玩家 | 提示，不改变状态 |
| 左键拖动 | 平移；超过 4px 不触发点击 |
| 右键拖动 | 平移 |
| 树箭头点击 | 折叠/展开；不选中 |
| 树行点击 | 选中并切换地图 |
| 被过滤的当前层 | 地图照常显示；树无高亮；跳转时自动取消过滤 |
| 暂停中 F8 | 打开；关闭恢复暂停菜单，Esc 不会把暂停菜单也关掉 |
| ∞ 导航 | 只改 UI 选中层；不改场景、不移动玩家 |
| 新建 ∞ 区 | 第一次点击只弹确认；确认后才生成；取消不改状态 |
| 生成 ∞ 区失败 | Toast；不改状态；日志记录异常 |
| 场景在打开期间被外部切换 | 每帧校验 `currentLevelName / levels.Count`，变化则重建 |
| 关卡很大 | 只遍历可见行列；箱子落点按 Block 缓存 |
| 多父层 | 只建一个节点；共享层属性标记；其余父块点击仍可导航 |
| F7 一键通关 | 播放约 1.5s 的逐渐变白动画，再存档/解锁/下一关 |

## 8. 配置

```ini
[Teleport]
CloseAfterTeleport = true
Undoable = true
CellSize = 24
HideVoidBoxes = false   ; 新增

[Diagnostics]
LogSceneTree = false
```

键位不变：`WinLevel = F7`、`Teleport = F8`。

## 9. 兼容性

- **原版**：大厅/区域/箱子/嵌套箱子/∞ 区/传送门。
- **CustomHubs**：
  - 仍不硬编码区域名；
  - 多顶层 `Block` 根层显示为 `hub #1/#2`；
  - `Ref ... Area_X` 作为区域标题；
  - 自定义 puzzle 的 `F7` 仍走 CustomHubs 的存档/回大厅逻辑。
- 不引用 CustomHubs 程序集，不与其补丁冲突（仍只补 `World.UpdateInner`）。
- 生成 ∞ 区只使用游戏自身的 `LoadLevel.CreateInf*`，不写自定义存档。

## 10. 验收标准

1. 任意含箱子的关卡：点击箱子格 → 左栏选中该箱子层级、地图显示内部；玩家位置不变。
2. 在箱子内部点击空格 → 传送成功；`Ctrl+Z` 回父层原位；`Ctrl+Y` 再进入。
3. 点击空格 → 传送；点击传送门/墙 → 只提示。
4. 进入箱子后按“上一级”/Backspace → 回到包含该箱子的父层。
5. 选中任意层按 `∞ 进入`：列出/生成链接，并跳转到 ∞ 进入块的**实际 SubLevel**（生成器默认 5×5，但不写死）；
   `∞ 退出`：跳转到 ∞ 退出块的**实际 OuterLevel**，光标对准出口箱（生成器默认虚空，但不写死）；树上有 ∞ 标签。
6. 打开“隐藏虚空”：虚空层、虚空层内的箱子、虚空箱子的内部层都从树中消失；关闭后恢复；刷新/重开后配置保持；
   关闭过滤后，虚空层里的箱子可正常进入，虚空层里的空格可正常传送。
7. 树可折叠/展开；滚动条为自绘；鼠标悬停高亮与行完全对齐。
8. 地图边缘不出现半格；可水平/垂直拖动；缩放范围明显大于 v1；`适应` 可复位。
9. 暂停界面按 F8：打开地图；关闭后仍在暂停界面；不出现“当前状态不能通关”。
10. 按钮/关闭按钮视觉与面板一致，不出现 Unity 原生皮肤。
11. 地砖、快速移动、箱子属性、玩家/落点标记均可辨认；图例与标记一致。
12. **浅色克隆箱与游戏表现一致**：`block != block.SubLevel.GetExitBlock()` 的箱子在地图上用更亮的箱色，并标 `C`；主出口箱正常色。
13. **F7 一键通关完整播放原版逐渐变白动画**（约 1.5 秒），随后正常存档/解锁/下一关；不是瞬间跳结算。
14. **地砖图案与游戏一致**：按钮边框/眼睛、传送门缩略图、Info/Smile 纹理、FastTravel 上下箭头、Break 九宫格、Gallery 双横条等；不是“纯色块 + 字母”。
15. **多重无限**：一个层存在 `∞进入 #0/#1/#2` 或 `∞退出 #0/#1/#2` 时，菜单逐条列出；导航只改 UI；新建时选第一个空索引并二次确认。
16. **新建 ∞ 区二次确认**：第一次点击只弹确认；取消后场景完全不变；确认后才新增层/块并跳转。
17. `dotnet build -c Release` 0 warning / 0 error；v1 的撤销、Restart、CustomHubs 行为不回归。

## 11. 非目标

- 不做跨场景传送（传送门只显示，不主动加载目标关卡）。
- 不修改存档格式、不做解锁全部关卡。
- 不模拟真实“走进箱子”的贴边落点；箱子落点仍是“内部离中心最近空格”，∞进入的落点也取最近空格，不模拟方向贴边。
- 不自动折叠/展开到用户未选择的层级（除默认策略）。
- 不保证 ∞ 区生成在所有异常/损坏关卡上都成功；失败只提示。

## 12. 已确认（无待确认项）

1. **克隆箱** = 游戏里显示为浅色的箱子：`block != block.SubLevel.GetExitBlock()`（游戏 `cloneIntensity` 条件），不是“所有多父层箱子”。
2. **∞ 进入 / ∞ 退出**：按游戏实际逻辑。
   - ∞进入 → `block.SubLevel`（`Movement.AttemptToEnter` / `ComputeEnterTargetPos` 的到达层）；
   - ∞退出 → `block.OuterLevel`（`Movement.AttemptToExit` 的出现层），光标对准出口箱 `(block.x, block.y)`；
   - 不硬编码 5×5/虚空，按实际 `Block/Level` 展示与跳转。
3. **虚空交互**：虚空层和虚空层里的箱子/空格**都可以进入、传送**；`隐藏虚空` 只影响树/地图是否显示，不影响交互。
4. **生成 ∞ 区**：接受；仅在显式点击时执行，会改动场景、不可撤销。
5. **暂停中关闭**：恢复到暂停菜单，不直接继续游戏。
6. **F7 白化**：完整播放原版约 1.5 秒的逐渐变亮/发白，不再用瞬间结算。
7. **地砖图案**：使用游戏自身纹理/缩略图（传送门缩略图、眼睛、Info/Smile、FastTravel 箭头）并复刻按钮边框、九宫格等比例；不是字母占位。
8. **∞ 导航只改 UI**：点击已有 ∞ 链接不生成、不改场景；新建 ∞ 区必须二次确认，取消不改状态。
9. **多重无限**：所有非空索引逐条列出，保留 `Index/Source`；新建取第一个空索引，不覆盖已有项。

规格已无待确认项；等待你批准后，我再写实施计划（计划通过后才改代码）。

## 13. 预计改动文件（批准后才动手）

- `src/LevelGraph.cs`：`PreferredParent`、多重 ∞ 链接（全部索引/空位）、虚空/克隆（浅色判定）/属性、标题/标签、`PathFlip` 修正。
- `src/TeleportOverlay.cs`：折叠树 + 自绘滚动条、点击分派、上一级/∞ 菜单/二次确认生成、虚空过滤、地砖图案（反射游戏纹理 + 复刻几何）、地图缩放/拖动、暂停状态、自绘控件。
- `src/WinService.cs`：F7 改为 `WinCount=0`，播放原版白化动画。
- `src/UiText.cs`：新增文案。
- `src/ModConfig.cs`：`HideVoidBoxes`。
- `README.md` / 本规格：使用说明与已知限制。

## 14. 测试策略

- 构建：`dotnet build -c Release`，0 warning / 0 error。
- 静态审查：对照 `_decomp` 确认 `Level.infEnterBlocks/infExitBlocks`、`CreateInf*`、`InfZone`、`Block` 属性名，以及 `Draw.cloneIntensity` 的浅色判定。
- 手动验收：按第 10 节逐条；重点：
  - 箱子点击不再传送；
  - 浅色克隆箱显示与游戏一致；
  - 地砖图案（按钮/传送门缩略图/Info/Smile/箭头/九宫格）与游戏一致；
  - ∞进入/退出按实际 Block/Level 跳转；多重无限逐条列出；
  - 新建 ∞ 区二次确认，取消不改场景；
  - 暂停 F8；
  - 虚空过滤 + ∞ 生成；
  - 虚空层/虚空箱可进入、可传送；
  - F7 白化动画；
  - 箱子内传送 + 撤销/重做 + Restart；
  - CustomHubs 大厅。
- 本轮不启动游戏做实机验证（按你的要求）；只做代码审查与构建验证。
