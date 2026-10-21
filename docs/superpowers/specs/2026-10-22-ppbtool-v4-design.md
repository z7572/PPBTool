# PPBTool v4 设计规格 —— 箱子引用、返回引用、无引用背景、无限标签与符号可见性

日期：2026-10-22
状态：**规格草案**（本轮只写规格，不实施）
基线：v3 完成，交接见 [HANDOFF-v3.md](../../HANDOFF-v3.md)
构建基线：`dotnet build -c Release` 0 warning / 0 error

## 1. 目标

v4 集中解决四类问题：

1. **把“引用（reference）”作为一等概念**：
   - 一个 `Level` 可以被多个 `Block` 引用；
   - “返回上一级”实际应该是“回到引用这个层级的那个箱子所在的位置”；
   - 自定义大厅等场景中，外层引用可能不在当前树父链上，v3 的 Back 会走不到。
2. **修复没有引用的箱子**：
   - 没有 `BlocksWithThisAsTheirSubLevel` 的层级不再错误地画成纯黑背景；
   - Back 对无引用层级应明确禁用/提示，而不是跳到错误层级。
3. **重做无限标签**：
   - 不再出现 `[Infinite Enter Infinite Enter←1]` 这种重复；
   - 明确 `←1/←2` 到底是什么；
   - 把“引用数”“进入链接数”“退出链接数”分开。
4. **让无限符号与游戏显示一致**：
   - 有的游戏里显示、mod 不画；
   - 有的游戏里不显示、mod 反而画；
   - 需要按 `Draw.DrawBlocks` 的入队条件、`DrawInfEffect` 的数量规则、父层是否可见来统一。

## 2. 非目标

- 不修改游戏的引用结构/存档。
- 不改变无限区的生成逻辑（`CreateInfEnterZone` / `CreateInfExitZone`）。
- 不重写传送/Undo 机制。
- 不在本轮实施 v4；只写规格和诊断方案。

## 3. 游戏源码里的“引用”模型

### 3.1 数据结构

`Block.Init`（`_decomp/Block.cs:235` 附近）：

```csharp
SubLevel = subLevel;
...
if (SubLevel != null && !SubLevel.BlocksWithThisAsTheirSubLevel.Contains(this))
    SubLevel.BlocksWithThisAsTheirSubLevel.Add(this);
```

因此：

- `level.BlocksWithThisAsTheirSubLevel` 就是**所有引用该层级的 Block**。
- `level.GetExitBlock()` 返回 `BlocksWithThisAsTheirSubLevel[0]`，只是“primary reference”，不是唯一引用。
- `level.SetExitBlock(block)` 把某个引用移到第 0 位。
- clone 箱（浅色）就是“不是 primary 的引用”：
  `block != block.SubLevel.GetExitBlock()`。

### 3.2 引用的种类

一个引用可能是：

| 种类 | 特征 | 是否可 Back |
|---|---|---|
| Root owner | `block.OuterLevel == null` | 否（没有“外层箱子位置”） |
| Real box | `OuterLevel != null`，普通箱子/Ref | 是 |
| Clone box | 非 primary 引用 | 是 |
| Infinite Enter | `block.IsSomeInfEnterBlock` | 视父层是否可见 |
| Infinite Exit | `block.IsSomeInfExitBlock` | 视父层是否可见（常在虚空） |
| Generated | `block.GeneratedInf` | 视情况 |
| CustomHubs Ref | `OuterLevel != null`，可能不在当前树父链 | 是 |

**关键**：同一个 Level 可以同时有 root owner、真实箱子、clone、∞进入/退出引用；v3 的树只保留一个 `PreferredParent`，所以 Back 必然丢失一部分引用。

### 3.3 游戏外层渲染怎么选引用

- 游戏在每层都会渲染该层里的 Block；一个 Level 被多个箱子引用时，这些箱子在各自父层里都会画出来。
- `GetExitBlock()` 只用于“默认出口/相机/颜色”等 primary 语义；不是“唯一外层”。
- 因此 mod 的 Back 不能用“树的唯一父节点”代替“引用”。

### 3.4 两个箱子重叠：为什么“只生成一个”但出口仍生效

`Block.Init`（`_decomp/Block.cs:240`）：

```csharp
if (OuterLevel != null)
{
    if (OuterLevel.blocks[xpos, ypos] != null)
        Debug.LogWarning("Trying to init block where there already is one");
    OuterLevel.blocks[xpos, ypos] = this;   // 后 Init 的覆盖前一个
}
if (SubLevel != null && !SubLevel.BlocksWithThisAsTheirSubLevel.Contains(this))
    SubLevel.BlocksWithThisAsTheirSubLevel.Add(this); // 两个都保留在引用列表
```

所以：

- **网格 `level.blocks[x,y]` 只能放一个**：后加载的箱子覆盖前一个；
- **`level.blockList` 只从网格重建**（`World.ComputeBlockLists`）：
  只有网格获胜者会进入 `blockList`，也只有它会被正常渲染/参与碰撞；
- **被覆盖的箱子仍存在于 `World.blocks` 和 `SubLevel.BlocksWithThisAsTheirSubLevel`**；
- `Level.GetExitBlock()` 返回 `BlocksWithThisAsTheirSubLevel[0]`，与网格里是谁无关；
- 出口逻辑使用 `GetExitBlock()` 的 `OuterLevel/xpos/ypos`。两个箱子坐标相同：
  - 即使出口箱子是被覆盖、**不在网格/不渲染**的那个，`GetExitBlock()` 仍能返回它；
  - 玩家从该层退出时，仍会出现在同一个 `(x,y)`，所以“出口正常起作用”；
  - 如果出口箱子是网格获胜者，则它也就是可见箱子，行为相同。
- 视觉上只看到网格获胜者；如果获胜者不是 primary，`IsCloneBox` 仍按 `GetExitBlock` 判断，可能显示为 clone，这和游戏一致。

**v3 的问题**：

- `LevelGraph` 用 `BlocksWithThisAsTheirSubLevel` 建父链，但地图点击只命中网格获胜者；
- `PreferredParent` 可能选到**被覆盖的隐藏引用**，导致树父节点/Back 指向的箱子与地图上看到的箱子不是同一个 SubLevel；
- `refs` 计数包含隐藏引用，但没有标记 hidden/overlap；
- Back 到隐藏引用仍可定位到同一 `(x,y)`，但用户无法通过点击该格重新进入隐藏层（点击会打开网格获胜者的 SubLevel）。

## 4. v3 的问题定位

### 4.1 Back 不完整

v3：

- `LevelGraph.PreferredParent(level)` 只选一个父引用（prefer 非虚空）；
- `LevelNode.ParentNode` 只有一条；
- `Backspace` 调 `GoParent()` → `selectedNode.ParentNode`。

失效场景：

- 自定义大厅里，一个层级被多个区域/Ref 引用；
- 当前层级是从 clone 箱进入的，但树父节点是 primary；
- 外层引用在另一个树分支/虚空分支；
- 层级有多个引用且 primary 是 root owner（`OuterLevel == null`），Back 跳到虚空或失效。

### 4.2 无引用层级背景黑

`LevelGraph.ExitBlockOf`/`SafeExitBlock` 对 `BlocksWithThisAsTheirSubLevel.Count == 0` 返回 `null`。
v3 的背景逻辑：

```csharp
if (levelOwner != null && levelOwner.OuterLevel != null)
    Fill(levelRect, GameBlockColor(levelOwner, ...));
```

没有引用 → 不填 → 画布黑色 → 箱子内部空格全黑。

### 4.3 无限标签重复/含义不清

v3 `BuildInfinityCounts`：

```csharp
foreach (block in level.infEnterBlocks)
    Bump(infInCounts, block.SubLevel);

foreach (block in level.infExitBlocks)
    Bump(infOutCounts, block.OuterLevel);
```

v3 `BuildTags` 同时加：

- `parent.IsSomeInfEnterBlock` → `"Infinite Enter"`
- `infInCounts[level] > 0` → `"Infinite Enter←N"`

所以一个节点可能出现：

```text
[Infinite Enter Infinite Enter←1]
```

含义：

- 第一个 `Infinite Enter`：该节点的父引用是 ∞ 进入块；
- `Infinite Enter←1`：`infInCounts[level] == 1`，即**有一个 ∞ 进入块以该层为 SubLevel**。
- `←2`：有两个 ∞ 进入块指向同一层（多/二重无限进入）。

但 `←` 不是“引用数”，也不是“层级深度”，v3 没有解释，且和父标签重复。

`infOutCounts[level]` 则是**位于该层的 ∞ 退出块数量**（∞退出块通常位于虚空容器），不是“从该层出去的链接数”。v3 标签 `Infinite Exit←N` 同样容易误解。

### 4.4 无限符号显示不一致

v3 `DrawInfSymbols` 与游戏 `Draw` 的差异：

| 情况 | 游戏 | v3 |
|---|---|---|
| 进入块 `SomeInfEnterNum + 1 <= 0` | `numSymbols <= 0` → 警告并 return | `Mathf.Max(1, ...)` → 仍画 1 个 |
| 退出块不在 `infExitBlocks` 列表 | `IndexOf + 1 == 0` → 警告并 return | `index < 0 ? 1` → 仍画 1 个 |
| 父层是虚空 | 只有虚空本身被绘制时才画 | v3 直接 `parentVoid` 跳过 → 游戏显示、mod 不画 |
| `DrawStyle == GRID && depth == 0` | `DrawBlock` 不 enqueue InfEffect | v3 无此条件 → mod 画、游戏不画 |
| `iii` 特殊关卡 `depth >= 2 && SpecialEffect == 13` | 不 enqueue | v3 画 |
| 当前层是箱子内部、父箱符号 | 父层渲染时画；父层不可见不画 | v3 只按 `OuterLevel.InfZone` 跳过，过粗 |
| 无纹理 fallback | 游戏没有（纹理存在） | v3 画文本 `ε/∞` → 与游戏相反 |

游戏入队条件（`_decomp/Draw.cs:6172` 附近）：

```csharp
if (DrawStyle != DS.GRID || ticket.depth != 0 || World.State == World.FastTravel)
{
    if ((block.IsSomeInfExitBlock || block.IsSomeInfEnterBlock)
        && (!iii || depth < 2 || block.SpecialEffect != 13))
    {
        qInfEffect.Enqueue(...);
    }
}
```

`DrawInfEffect` 数量：

- 进入：`block.SomeInfEnterNum + 1`；
- 退出：`block.SubLevel.infExitBlocks.IndexOf(block) + 1`；
- `<= 0` 直接 return，不画。

## 5. v4 设计

### 5.1 引用模型

新增 `LevelReference`：

```csharp
enum RefKind { RootOwner, RealBox, CloneBox, InfEnter, InfExit, CustomRef, Other }

sealed class LevelReference
{
    Block Block;          // 引用这个层的箱子
    Level Parent;         // Block.OuterLevel（RootOwner 为 null）
    int X, Y;             // Block.xpos/ypos；Parent 为 null 时无意义
    RefKind Kind;
    bool IsGridResident;  // Parent.blocks[X,Y] == this（地图上实际可见/可点击）
    bool IsHiddenOverlap; // 与同一格的 grid occupant 重叠、但不是 occupant
    Level Target;         // Block.SubLevel
    bool GameVisible;     // 父层当前是否被游戏绘制（仅用于标签，不阻止 Back）
    bool CanBack;         // Parent != null 且 Parent 仍在 World.levels 中
    string Label;         // "Area Transfer (5,5) · clone" 等
}
```

`IsGridResident`：

```csharp
Parent != null && Parent.blocks[X, Y] == Block
```

- 隐藏重叠引用仍然 `CanBack == true`（出口/引用仍有效）；
- 只有网格 occupant 能在 map 上被点击/重新进入；隐藏引用通过 tree / reference chooser 进入。

`CanBack` 的定义：

```csharp
Block != null
&& Block.SubLevel == level
&& Block.OuterLevel != null
&& World.levels.Contains(Block.OuterLevel)
```

- **RootOwner 永远不 CanBack**（`OuterLevel == null`，没有“外层位置”）。
- **Void 引用可以 CanBack**：即使该层在树里是根节点，只要游戏里能看到它的外层是虚空，就应该能回到那个虚空。
- `GameVisible` 只影响标签/过滤显示，不阻止 Back；虚空当前是否被 `隐藏虚空` 过滤掉，导航时再临时处理。

`LevelGraph` 为每个 `Level` 构建：

```csharp
List<LevelReference> References;
LevelReference Primary;      // GetExitBlock, 可能不可 Back
LevelReference PreferredBack; // 最近一次有效引用（可为 null；不用于盲猜）
int VisibleReferenceCount;
```

分类：

- `Block.OuterLevel == null` → `RootOwner`；
- `IsSomeInfEnterBlock` → `InfEnter`；
- `IsSomeInfExitBlock` → `InfExit`；
- `GeneratedInf` → 标记 Generated（仍可 Back，若父层可见）；
- `Block != GetExitBlock()` → `CloneBox`；
- 其它 `OuterLevel != null` → `RealBox` / `CustomRef`。

**不修改游戏数据**，只读 `BlocksWithThisAsTheirSubLevel`。

### 5.2 引用导航（Go to Reference）

**按钮改名**：v3 的 `返回上一级` 改为 **`回到引用处` / `Go to Reference`**。
它不是“树上的父级”，而是“回到引用当前层级的那个箱子所在位置”。
`Backspace` 保留。

新增 **reference stack**（不是树父链）：

```csharp
Stack<LevelReference> navRefs;
```

- 点击地图上的箱子：
  - 记录该 `Block` 的 `LevelReference`；
  - `navRefs.Push(reference)`；
  - 打开 `block.SubLevel`。
- 通过树/无限菜单/传送门导航：
  - 不 push 任意猜测的父引用；
  - 保持 stack 为空。
- **Go to Reference 按钮 / Backspace**：
  1. 如果 `navRefs` 非空：pop 顶部，导航到 `reference.Parent`，光标定在 `(reference.X, reference.Y)`；
  2. 否则收集当前 Level 的所有 `CanBack` 引用：
     - **0 个**：按钮禁用，提示 `No outer reference` / `无外层引用`；
     - **1 个**：直接去这个引用；
     - **多个**：打开 **Reference chooser**，列出每个引用的父层/坐标/kind（Box2、void、clone…），由用户选择；不自动猜一个。
  3. stack 顶部的引用如果已经失效（`Block.SubLevel != level` / 父层不在 `World.levels`），跳过并继续下一个。
- **多引用示例**：
  - Box1 包含 Box2 引用，Box2 包含 Box1 引用；当前在 Box1 层级：
    - 如果是从 Box2 里的 Box1 引用点进来的：`navRefs` 顶部就是该引用，Back 直接回到 **Box2 层级**，光标在 Box2 里的 Box1 箱子；
    - 如果是从树直接打开 Box1 层级：没有 stack；Box1 的引用可能同时有 Box2、void 等，Back 打开 Reference chooser，选择 **Box2** 就能进入 Box2；
    - 循环 A→B→A→B 用 stack + 64 深度上限处理，不依赖树父链。
  - Box1 在游戏里的外层是虚空，但树里是根节点：
    - `BlocksWithThisAsTheirSubLevel` 里有 `OuterLevel.InfZone` 的 ∞退出引用；
    - 该引用 `CanBack == true`；Back/chooser 直接去那个虚空楼层，光标在虚空里的箱子引用。
  - Box1 在游戏里外层什么都没有：
    - 只有 `OuterLevel == null` 的 root owner，没有 `CanBack` 引用；
    - Back 禁用并提示。
- **场景切换/传送**：
  - 场景变化清空 `navRefs`；
  - 同一场景内 Undo/Redo 不清空（UI 导航不是 Undo turn）；
  - Teleport 到不同 Level 时，可选保留 stack；spec 默认保留，Back 仍回到进入点。
- **循环保护**：stack 深度限制（例如 64），每次 push 校验引用仍有效；不要因为 A↔B 引用而无限。
- **自定义大厅**：因为枚举全部 `BlocksWithThisAsTheirSubLevel`，即使父引用不在树父链上，也能 Back；树只影响显示，不影响引用导航。
- **根节点不等于无外层**：
  - `root node` 只表示树里没有“真实父箱”；
  - 不代表没有虚空/∞退出引用；
  - Back 以引用列表为准，不以 `ParentNode` 为准。

### 5.3 树显示

- 仍然**一个 Level 一个节点**，避免同一层在树里重复爆炸。
- 节点副标题/标签增加：
  - `refs ×N`：`BlocksWithThisAsTheirSubLevel.Count`；
  - `enter ×N`：`infInCounts`；
  - `exit ×N`：`infOutCounts`。
- 节点右键/详情面板提供 **“Go to reference…”**：
  - 列出所有 `CanBack` 引用：父层标题 + `(x,y)` + kind + clone/inf/void；
  - 点击直接导航到该引用所在父层并定位光标；
  - 这解决“树只有一个父节点”的显示限制。
- Go to Reference 优先 stack；没有 stack 时：0 个引用禁用、1 个直接去、多个打开 Reference chooser；不自动使用 `PreferredBack` 盲猜。

### 5.4 无引用层级的背景

新增 `ResolveLevelColor(Level level)`：

1. `level.BlocksWithThisAsTheirSubLevel.Count > 0` → `GetExitBlock()` 的颜色（现有逻辑）。
2. 否则扫描 `level.blockList`：
   - 优先有非零 `hue/sat/val` 的箱子/玩家块；
   - 取第一个可用的 `GameBlockColor`。
3. 否则使用 `Hub.puzzleData[World.currentLevelName].palette.root` 的 HSV（若可用）。
4. 否则使用中性深色 `new Color(0.16, 0.18, 0.22)`，**不再纯黑**。

- 背景只用于箱子内部/当前层网格；箱子外仍黑。
- 无引用层级的 Back：禁用，提示“无外层引用”。
- 在 `LogSceneTree` 或新诊断里打印：
  `level=..., refs=0, fallback=block/palette/neutral`。

### 5.5 无限标签重做

不再使用 `←` 和重复标签。节点标签拆成三个独立字段：

```text
refs ×3
Infinite Enter ×2
Infinite Exit ×1
```

语义：

- `refs ×N`：
  `BlocksWithThisAsTheirSubLevel.Count`，包含 root owner、真实箱、clone、∞引用。
- `Infinite Enter ×N`：
  `infInCounts[level]`，即**有多少个 ∞进入块以该层为 SubLevel**。
  - `×1` = 一个进入块指向它；
  - `×2` = 两个进入块指向同一层（二重/多重无限进入）。
- `Infinite Exit ×N`：
  `infOutCounts[level]`，即**该层里有多少个 ∞退出块**（通常虚空容器）。
- 父引用类型（`parent.IsSomeInfEnterBlock/Exit`）只在“引用/父级”一栏显示一次，不再和计数标签重复。
- 详情面板显示解释 tooltip：
  - “Enter links: ∞进入块指向该层的数量”
  - “Exit links: 该层内 ∞退出块的数量”
  - “References: 引用该层的箱子总数（含 clone/∞/root）”

### 5.6 无限符号可见性

严格实现游戏规则：

1. **入队条件**：
   ```csharp
   bool drawStyleAllows = Draw.DrawStyle != Draw.DS.GRID
       || depth != 0
       || World.State == World.WS.FastTravel;
   bool specialAllows = !iii || depth < 2 || block.SpecialEffect != 13;
   if (drawStyleAllows && specialAllows
       && (block.IsSomeInfEnterBlock || block.IsSomeInfExitBlock))
       renderInfSymbol = true;
   ```
   - `iii`/`DrawStyle` 通过反射读取；读不到时按“可见”处理，并记录诊断。
2. **数量**：
   - 进入：`SomeInfEnterNum + 1`；
   - 退出：`SubLevel.infExitBlocks.IndexOf(block) + 1`；
   - `<= 0` → **不画**，不要 fallback 1。
3. **父层可见性**：
   - 非虚空父层：正常画；
   - 虚空父层：只有该虚空被游戏绘制时才画：
     ```csharp
     !void.FilledWithWalls
     && World.WentToInfZone
     && void.blockList.Count > 1
     ```
   - 不因为 `OuterLevel.InfZone` 一律跳过。
4. **当前层是箱子内部**：
   - 父箱符号只在父层可见时覆盖到整个 boxRect；
   - 父层是隐藏虚空/未渲染时不画。
5. **纹理缺失**：
   - 不再用文本 `ε/∞` fallback 冒充游戏符号；
   - 只记录诊断，避免“mod 画、游戏不画”。
6. **诊断开关**：
   新增 `[Diagnostics] LogInfSymbols=false`：
   ```text
   block=(x,y) level=... parent=... enter=true num=0 count=1
   exitIndex=... drawStyle=... wentToInfZone=... depth=... rendered=...
   ```
   用于实际对比“游戏显示但 mod 不画”的 hub 场景。

### 5.7 重叠引用（两个箱子同格）

- **枚举**：`BlocksWithThisAsTheirSubLevel` 全部保留，包括被网格覆盖的隐藏引用。
- **map 渲染/点击**：
  - 只渲染/点击 `Parent.blocks[x,y]` 的网格 occupant（与游戏一致）；
  - 隐藏重叠引用不在 map 上单独画，也不响应点击。
- **树父级（DisplayParent）**：
  - 优先选 `IsGridResident` 的非虚空引用；
  - 其次非虚空引用；
  - 其次 void 引用；
  - 这样树节点父级/光标对应“地图上实际看到的箱子”，避免 v3 的“树父节点是隐藏引用，但地图格是另一个 SubLevel”。
- **引用列表**：
  - 保留所有隐藏引用；`LevelReference.Label` 标注：
    - `grid`（可见）
    - `hidden overlap`（同格被覆盖）
    - `primary`（`GetExitBlock`）
  - `refs ×N` 仍统计全部；详情可显示 `refs ×N (M hidden overlap)`。
- **Go to Reference**：
  - 隐藏引用同样可导航到父层同一 `(x,y)`；
  - 到达后光标在该格；点击只能打开 grid occupant，因此要再次进入隐藏层，使用 reference chooser / 树节点，而不是点击地图格。
- **出口语义**：
  - clone/颜色/出口仍以 `GetExitBlock` 为准；
  - 如果 primary 是隐藏重叠引用，地图上的 grid occupant 仍按 `IsCloneBox` 显示为 clone（与游戏一致）；
  - 隐藏出口能正常退出，因为出口逻辑用 `GetExitBlock` 的 `OuterLevel/xpos/ypos`，不要求它在网格里。
- **CanBack**：
  - 隐藏重叠引用只要有 `OuterLevel != null` 且在 `World.levels` 中，就 `CanBack == true`；
  - 校验时不要加 `IsGridResident` 条件。
- **树/引用 chooser 的标签**：
  - 显示 `Target` 层名、父层 `(x,y)`、`grid/hidden overlap`、`primary/clone/inf`，避免两个同格引用不可区分。

### 5.8 外层箱子视图与“无限缩放”（扩展设计，非 v4 必需）

**结论：能做，但需要把当前“单层视图”升级为“引用链视图”。**

当前 v3：

- 只渲染 `selectedNode.Level` 一层；
- 箱子内容向内递归（`DrawBoxContents`），但不向**外**画当前层的父箱；
- `zoom` 限制 0.1–10，`cell` 限制 2–400px；
- 没有 outer reference path，也没有跨层 LOD。

#### 5.8.1 引用链视图

新增 `ViewPath`：

```csharp
List<LevelReference> outerPath; // 从当前层向外：current -> parent ref -> ...
```

- 从当前 Level 选一个 `CanBack` 引用（优先 stack / DisplayParent / chooser）；
- 沿 `Parent` 继续向上，直到：
  - `OuterLevel == null`（root owner，停止）；
  - 没有任何 `CanBack` 引用（停止）；
  - 遇到已访问 Level（引用环，停止并标记 cycle）；
  - 达到深度上限（例如 32）。
- 最外层 Level 作为渲染根；它里面的路径箱子递归进入下一层，直到当前 Level。
- 当前 Level 的网格加高亮边框；外层只显示、不响应传送点击。
- 面包屑：
  `Outermost > ... > Current`，点击可切换 focus。
- 多引用/重叠引用：用 Reference chooser 选路径；隐藏 overlap 只显示不响应 map 点击。
- 无引用/只有 root owner：outerPath 为空，不画外框，保持当前单层视图。

#### 5.8.2 渲染方式

复用现有 `DrawRealCell`/`DrawBoxArt`/`DrawBoxContents`：

```text
RenderLevel(outermost, viewportRect, pathIndex)
  for each visible cell:
      DrawRealCell(...)
      if cell is the path box and pathIndex < outerPath.Count:
          childRect = cellRect
          RenderLevel(child, childRect, pathIndex+1)
```

- 从最外层向内递归；当前层就是路径上的某个 child。
- 非路径箱子仍按普通箱子递归显示内容，但受 LOD/深度限制。
- 外层 Level 的 `blockList`/`floorList` 直接用现有渲染器。
- 当前层边界、路径箱边界用高亮描边区分。
- 外层 cell 点击不传送；只有当前层 cell 保留现有交互。

#### 5.8.3 “无限缩放”的现实定义

- 去掉 v3 的硬编码 `zoom 0.1–10` 与 `cell 2–400`。
- 使用连续 world scale，范围设为实际可用：
  - 放大：`cell` 可到 4096px（受 float/纹理/性能限制）；
  - 缩小：`cell` 可到 0.25px（低于 2px 时进入 LOD 简化）。
- 缩放中心仍是鼠标；`Fit` 提供两种：
  - `Fit current`：适应当前层；
  - `Fit outer`：适应最外层链。
- **“无限”指没有固定档位/固定上限的连续缩放体验**，不是数学意义上的无限细节：
  - 放大到一定程度后，箱子内部内容由像素阈值停止递归；
  - 缩小到一定程度后，外层继续出现，但 cells 进入 LOD；
  - 引用环/深度上限会截断。
- LOD：
  - `cell < 2px`：只画色块/边框，不画内容、符号、文字；
  - `cell < 6px`：不画箱子内部细节；
  - `cell > 400px`：正常画大墙四象限；更高时继续放大纹理；
  - 只遍历 viewport 内 cells；外层/内层都做 culling。
- 缩放范围/深度作为配置（可选）：
  ```ini
  [View]
  MaxOuterDepth = 32
  MinCellPixels = 0.25
  MaxCellPixels = 4096
  ```

#### 5.8.4 风险

1. **引用环**：Box1↔Box2 会形成无限外层/内层；必须用 `ViewPath` 深度/已访问集合截断，不能纯递归。
2. **多引用**：同一层有多个外层箱子；必须由 stack/chooser 选一条路径，否则无法定义“外层”。
3. **重叠引用**：隐藏 overlap 不能画成可见箱子；只能作为引用列表/路径选项。
4. **性能**：外层链 + 内层递归；只画 viewport，LOD 必须默认开启。
5. **内存/精度**：超大 cell、深层路径、float 精度；设置实际可用上下限。
6. **交互歧义**：外层 cell 不应触发传送；当前层高亮，避免用户误点。

#### 5.8.5 验收

- 当前层有真实父箱：zoom out 能看到父层和当前层所在的箱子；
- 当前层只有 void 引用：能 zoom out 到 void；
- 无引用/root owner：不画外框；
- Box1↔Box2：不无限递归，路径在环处停止并提示；
- 多引用：chooser 切换路径后外框随之变化；
- zoom 连续、无固定 10×/400px 卡死；LOD 下不卡顿；
- 当前层交互仍是原来的点击/传送。

#### 5.8.6 循环嵌套、克隆箱、镜像箱

这三类问题必须分开建模，不能只用一个 `Level` 去重：

- **循环**：引用/包含关系形成环（A 含 B，B 含 A；A 含自己的 clone；A 含镜像 A）。
- **克隆**：同一个 Level 被多个 Block 引用；clone 是“非 primary 引用”，内容相同但颜色变亮。
- **镜像**：`Block.FlipH`（以及 `SpecialEffect 2/3`）让该箱子的子层内容水平翻转，并带 FlipShine。

##### 渲染上下文

```csharp
sealed class RenderContext
{
    Stack<Level> ancestors; // 仅当前递归祖先，用于环检测
    int depth;
    bool cumulativeFlipH;
    Level focus;
    List<LevelReference> path;
}
```

- **环检测只看 `Level` 是否在 `ancestors` 中**。
  - 如果 `box.SubLevel` 已在祖先栈：不递归内容；画该 box 的底色/边框/符号/脸，并叠加 `↻ cycle` 标记。
  - 环检测**不能**用“全局已渲染 Level 集合”，否则同一 Level 的两个兄弟 clone 只能画一个。
  - 环检测也**不能**只看 `Block`，否则 A 含 clone B、B 含 clone A 时不会被认为是环。
- **clone 不去重**：
  - 同一 Level 的两个兄弟 clone 都渲染；
  - 每个 clone 用 `parentBox` 计算 clone 墙/箱子颜色；
  - 缓存 key 不能只用 Level，至少 `(Level, parentBlock, cumulativeFlipH)`。
- **镜像用累积翻转**：
  ```csharp
  childFlip = context.cumulativeFlipH ^ box.FlipH;
  ```
  - 每层 `DrawBoxContents` 按 `childFlip` 镜像 x；
  - 外层链、内层递归、点击坐标反算都用同一个累积翻转；
  - 嵌套镜像自然 XOR：镜中镜恢复正向；
  - 环在镜像处也必须截断，不能靠“翻回去”继续。

##### 具体案例

1. **A 含 clone B，B 含 clone A，当前 focus A**
   - `ancestors=[A]` → 渲染 A；
   - A 的 clone B：B 不在祖先 → 进入，`ancestors=[A,B]`；
   - B 的 clone A：A 在祖先 → **cycle placeholder**，不再进入；
   - 视觉：B 箱内有一个“A(cycle)”框，而不是无限画 A/B。
   - Back：引用栈仍可回到 B/A 的实际 entry；与渲染截断无关。

2. **A 含 clone B，B 含 mirror A**
   - 同样在 A 处成环；mirror A 的 box 自身按其 `FlipH` 画边框/符号，内容不递归；
   - `cumulativeFlipH` 用于该 box 的显示，但不会再进入 A。

3. **A 内有两个 clone B（兄弟）**
   - `ancestors=[A]`；两个 B 都不在祖先，**都渲染**；
   - 各自 clone 颜色、位置不同；不能因为 Level 相同就跳过第二个。

4. **mirror A 内含 mirror B，B 内含 A**
   - 先按 `flip ^= true` 渲染 A 的镜像内容；
   - B 再 `flip ^= true`，累计回正向；
   - 到 A 时命中祖先栈，画 cycle 标记；不会无限翻转。

5. **A 的 clone 与 primary 同时存在**
   - primary 用 `GetExitBlock` 颜色；clone 用 clone 变亮；
   - 渲染/引用列表都保留两者；重叠同格时按 5.7 标记 hidden overlap。

##### 环的显示与交互

- **cycle placeholder**：
  - 保留箱子底色/边框/∞·ε/玩家脸等父层元素；
  - 内部不绘制子层内容；
  - 中心/角落显示 `↻` 或 `cycle`，可带深度 `↻3`；
  - 不画 Infinity 的多重符号？父层符号照常；内容不展开。
- **点击**：
  - 普通箱子 → 打开 SubLevel；
  - cycle 箱子 → 不自动无限展开；提示 `Cycle reference`，或打开 Reference chooser 选择“聚焦到该层/查看引用”；
  - 如果允许显式展开，最多增加 1–3 层“手动 cycle depth”，不能默认无限。
- **缩放**：
  - 放大 cycle 箱子只是放大 placeholder，不产生新细节；
  - 缩小到 LOD 时 cycle 标记可简化为边框/点；
  - 不会因“无限缩放”触发无限递归。

##### 外框视图（5.8）中的环

- `ViewPath` 从当前层向外走时，遇到已访问 Level 立即停止，并标 `cycle`；
- 外框最多到环的前一个节点，不会无限外套；
- 多引用时由 stack/chooser 选一条路径；不同路径可能在不同位置成环。

##### 验收

- A↔B clone/mirror 循环：不无限递归/不卡死，显示 cycle placeholder；
- 两个兄弟 clone：都显示，颜色正确；
- mirror 嵌套：XOR 正确，镜中镜正向；
- clone+mirror+cycle 组合：不崩、不重复、不丢层；
- Back/引用 chooser 仍能回到实际 entry；
- 无限缩放仅放大 cycle 标记，不递归展开。

## 6. 验收标准

1. **引用/Back**：
   - 按钮改名为“回到引用处 / Go to Reference”，Backspace 保留；
   - 点击任意箱子进入后，Back 回到**该箱子所在位置**，而不是 primary/树父节点；
   - clone 箱、CustomHubs Ref、多引用层级都能回到实际入口；
   - **Box1 ↔ Box2 循环**：从 Box2 的 Box1 引用进入 Box1 后，Back 回到 Box2；从树打开 Box1 且存在多个引用时，Reference chooser 能选择 Box2；
   - **根节点但有 void 引用**：Back 能去对应虚空；
   - **只有 root owner、无任何 OuterLevel 引用**：Back 禁用并提示。
   - **两个箱子同格**：出口在隐藏覆盖引用时出口仍正常；树父级指向 grid occupant；reference chooser 能区分/进入 hidden overlap。
2. **无引用背景**：
   - 不再纯黑；有 block/palette fallback；
   - 背景只影响箱子内部，外部仍黑。
3. **无限标签**：
   - 不再出现 `Infinite Enter Infinite Enter←1`；
   - `refs ×N`、`Infinite Enter ×N`、`Infinite Exit ×N` 分开；
   - `×2` 能正确表达二重无限进入；
   - 详情解释三个计数。
4. **无限符号**：
   - 与游戏同场景对照：游戏显示→mod显示；游戏不显示→mod不显示；
   - 多重符号数量/alpha/堆叠一致；
   - 虚空父层只在虚空可见时画；
   - 不因纹理缺失画 fallback 文本。
5. v3 功能不回归；构建 0/0。

## 7. 待实机确认 / 风险

1. **CustomHubs 的 Ref** 是否都走 `Block.SubLevel`/`BlocksWithThisAsTheirSubLevel`；需要用实际 CustomHubs 场景确认引用枚举完整。
2. **`DrawStyle`/`iii` 的反射**：`Draw.DrawStyle`、`Draw.iii` 是私有/静态字段，需反射；缺失时按可见处理并记录日志。
3. **无限符号“游戏显示但 mod 不画”** 仍可能是 hub 特殊 TUI/portal 渲染；需要 `LogInfSymbols` 对照。
4. **Back stack** 与传送/Undo 的交互：默认保留 stack；如果用户不希望传送后 Back 回旧位置，可配置清空。
5. **Void 引用的 Back**：即使虚空当前被 `隐藏虚空` 过滤，Back 也要能导航过去并自动恢复显示；需实机确认虚空加载/层级节点存在。
6. **重叠引用**：CustomHubs/自定义关卡可能在同一格 Init 多个箱子；需要实机确认 `blockList` 只含 grid occupant、隐藏出口仍可退出，以及 `GetExitBlock` 是哪个。
7. **无引用背景 fallback** 的颜色优先级需要实机确认；先用 block → palette.root → neutral。

## 8. 预计改动（批准后）

- `src/LevelGraph.cs`：
  - `LevelReference`/`RefKind`、`IsGridResident`/`IsHiddenOverlap`；
  - `References`、`DisplayParent`、`Primary`、`PreferredBack`；
  - `BuildTags` 拆分 refs/enter/exit；
  - `BuildInfinityCounts` 保留但改名语义。
- `src/TeleportOverlay.cs`：
  - `navRefs` stack、`GoBackReference`；
  - `Go to reference…` 列表；
  - `ResolveLevelColor`；
  - `DrawInfSymbols` 规则/诊断；
  - 无引用 Back 禁用。
- `src/ModConfig.cs`：`LogInfSymbols`。
- `src/UiText.cs`：`refs`、`enter links`、`exit links`、`no outer reference`、`go to reference` 等。
- `README.md` / 本规格。

## 9. 测试策略

- 构建：`dotnet build -c Release` 0/0。
- 场景：
  1. 普通箱子/嵌套箱子/克隆箱 Back；
  2. CustomHubs 多区域/Ref 的 Back；
  3. 无引用层背景；
  4. 二重无限进入箱子（enter ×2）；
  5. hub 无限符号：游戏 GRID/TUI 各样式对照；
  6. 虚空可见/隐藏前后的符号。
- 日志：`LogInfSymbols=true`，对比游戏截图和 dump。
- 本轮只写规格，未实施。
