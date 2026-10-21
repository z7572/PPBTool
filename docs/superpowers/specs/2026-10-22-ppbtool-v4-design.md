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
    bool GameVisible;     // 父层当前是否被游戏绘制
    bool CanBack;         // Parent != null && 父层可导航
    string Label;         // "Area Transfer (5,5) · clone" 等
}
```

`LevelGraph` 为每个 `Level` 构建：

```csharp
List<LevelReference> References;
LevelReference Primary;      // GetExitBlock, 可能不可 Back
LevelReference PreferredBack; // 最后一个可用引用/默认引用
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

### 5.2 引用导航（Back）

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
  - 选择该 Level 的 `PreferredBack` 作为“默认外层引用”，或保持 stack 为空。
- **Back 按钮 / Backspace**：
  1. 如果 `navRefs` 非空：pop 顶部，导航到 `reference.Parent`，光标定在 `(reference.X, reference.Y)`；
  2. 否则使用 `PreferredBack`：
     - 优先 `CanBack && Kind != RootOwner` 的引用；
     - 优先非虚空、非生成、非 clone 的真实父箱；
     - 其次 clone；
     - 其次可见虚空里的引用；
  3. 如果没有任何 `CanBack` 引用：**不跳转**，提示 `No outer reference` / `无外层引用`；按钮禁用。
- **场景切换/传送**：
  - 场景变化清空 `navRefs`；
  - 同一场景内 Undo/Redo 不清空（UI 导航不是 Undo turn）；
  - Teleport 到不同 Level 时，可选保留 stack；spec 默认保留，Back 仍回到进入点。
- **循环保护**：stack 深度限制（例如 64），重复引用去重；避免 A→B→A 无限。
- **自定义大厅**：因为枚举全部 `BlocksWithThisAsTheirSubLevel`，即使父引用不在树父链上，也能 Back；树只影响显示，不影响引用导航。

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
- Back 仍优先 stack；没有 stack 时用 `PreferredBack`。

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

## 6. 验收标准

1. **引用/Back**：
   - 点击任意箱子进入后，Back 回到**该箱子所在位置**，而不是 primary/树父节点；
   - clone 箱、CustomHubs Ref、多引用层级都能回到实际入口；
   - 无引用层 Back 禁用并提示。
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
5. **无引用背景 fallback** 的颜色优先级需要实机确认；先用 block → palette.root → neutral。

## 8. 预计改动（批准后）

- `src/LevelGraph.cs`：
  - `LevelReference`/`RefKind`；
  - `References`、`Primary`、`PreferredBack`；
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
