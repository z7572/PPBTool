# PPBTool — Patrick's Parabox 全能工具模组 设计规格

日期：2026-10-21
状态：待用户审阅
目标游戏：`D:\Programs\Patricks Parabox`（BepInEx 5.4.21，Unity 2020.3.30，Mono/net46）
兼容目标：原版 + [CustomHubs](`D:\Codes\CustomHubs-main`) 自定义大厅

## 1. 目标与非目标

### 1.1 目标

1. **一键通关**：可配置热键，把当前关卡按游戏正常结算流程判定为通关。
2. **智能传送**：可配置热键打开全屏地图浏览器；自动扫描当前场景内的全部大厅/区域、箱子、子箱子，
   以「树 + 俯视格子图」的方式选点，而不是手输坐标。
3. **撤销兼容**：传送登记为游戏原生 `UndoManager.Turn`，`Undo` 可回到传送前、
   `Redo` 可再次传送；不会破坏原版撤销栈语义。
4. **自定义大厅兼容**：不依赖硬编码的区域名/关卡名；CustomHubs 的 `Ref ... Area_X`、
   `area_data`、`puzzle_data` 不需要改写即可被扫描和显示。

### 1.2 非目标

- 不做关卡编辑器、不做存档修改器、不做解锁全部关卡（游戏自带调试键已有）。
- 不加载不在当前场景中的其它关卡（跨关卡跳转交给大厅里的传送门）。
- 不修改游戏原有存档格式。
- 不接管/重绑定游戏自身的按键；热键走独立配置项。

## 2. 已验证的游戏内部事实（实现依据）

| 事实 | 位置 |
|---|---|
| `World.levels` 是当前场景的全部 `Level`，`Level.OuterLevel` 无引用的层为根层 | `LoadLevel.Load` |
| 箱子 = `Block.SubLevel`；父层由 `Block.OuterLevel` 给出；`Level.BlocksWithThisAsTheirSubLevel[0]` 是入口块 | `Level.GetExitBlock` |
| 进入箱子后玩家所在层变为 `block.SubLevel`，父箱子仍在父层原位 | `Movement.AttemptToEnter` / `MoveBlock` |
| 空格子判定：目标格 `blocks==null` 且 `floors` 不是 `LevelPortal`（未解锁传送门会挡住玩家） | `Movement.AttemptToMoveToSpot` |
| 正常通关 = `World.Winning=true` 后 `WinCount>=1.5` 走原版结算（存档、解锁、下一关/回大厅） | `World.UpdateInner` 1159–1238 |
| 游戏自带调试键就是 `Winning=true; WinCount=1.5f` | `World.KeyboardShortcuts` |
| 传送可登记为一次 Turn：`RegisterMove` + `TurnCompletedDontPerform` | `FastTravel.Update` |
| 暂停全部游戏逻辑 = 让 `World.UpdateInner` 不执行（Harmony Prefix） | `World.UpdateInner` |
| 自定义大厅的区域名来自 `Ref` 的最后一个参数 → `level.hubAreaName`；关卡名即 `World.currentLevelName` | `LoadLevel.ParseLine` / CustomHubs `LoadLevel.cs` |

## 3. 用户可见行为

### 3.1 配置（`BepInEx/config/ppbtool.cfg`）

```ini
[Keys]
# 一键通关
WinLevel = F7
# 打开/关闭传送地图
Teleport = F8

[Teleport]
# 传送后是否自动关闭面板
CloseAfterTeleport = true
# 是否把传送登记进撤销栈
Undoable = true
# 地图默认格子像素大小
CellSize = 24
```

热键使用 Unity Input System 的 `Key` 枚举解析（`F7`、`F8`、`F9`…），解析失败时回退默认值并写日志。

### 3.2 一键通关

前置条件全部满足才执行，否则在屏幕底部提示原因（1.5 秒淡出）：

- `World.State == WS.Playing`（非暂停/过场/结算）
- `!World.InHub()`（大厅本身不是关卡）
- `!World.Winning && !World.EndZoom && !World.Transitioning`
- `World.FindPlayerBlock() != null`

执行：`World.Winning = true; World.WinCount = 1.5f;`。
随后完全交给原版流程：胜利动画 → `SaveFile.WinLevel` → 成就 → `GoToNextLevel` 或回大厅。
CustomHubs 下同样成立：`currentLevelName` 是 puzzle 名，`SaveFile` 已被 CustomHubs 重定向到自定义目录，
`Hub.puzzleData` 也由 CustomHubs 填充。

不直接修改 `SaveFile`，避免与 CustomHubs 的 `SaveFile_GetSaveFilePath` 冲突。

### 3.3 传送地图

热键打开 → 生成快照 → 全屏覆盖层 → 选中目标 → Enter/左键确认 → 传送并关闭。

**冻结**：覆盖层打开期间 Harmony Prefix 跳过 `World.UpdateInner`，因此游戏逻辑、输入、暂停菜单全部静止；
再次按热键或 Esc 关闭并恢复原 `State`。

**扫描**（每次打开重新扫描，随场景变化自动更新）：

- 遍历 `World.levels`，为每层找出「父块」：`foreach level in World.levels: foreach block in level.blockList: if block.SubLevel != null: parents[block.SubLevel] = block`。
- 树的根 = 没有父块的层；子的父块可能不唯一（同一箱子被多处引用），取第一个作为展示路径，但**目标仍然有效**，因为玩家进入后父块本身没动。
- 节点标题：`level.hubAreaName` 去掉下划线（原版区域/自定义区域都适用）；
  根层用 `World.currentLevelName`；无名字的子层显示 `箱子 @ (x, y)` + 深度。
- 节点附加信息：宽×高、是否包含玩家、是否可传送（是否有空格）。

**可传送目标**（智能点选的核心）：

- 空格且非（未解锁）传送门 → 直接落点。
- 格子是箱子（`SubLevel != null`）→ 视为「进入这个箱子」，目标 = 箱子子层里离入口块最近的空格。
- 箱子套箱子：递归下钻到最内层，落点同样取最内层入口附近的空格。
- 墙/玩家/无法下钻的箱子 → 不出现在目标列表里（地图上仍会绘制，但点击无效并提示）。
- 目标格若被占用（极端情况：入口块本身不是空格），用 BFS 在子层内找最近的空格，保证落点一定合法。

**界面布局**（全屏，IMGUI，缩放自适应 1280×720 以上）：

```
┌───────────────────────────────────────────────────────────────┐
│ 传送地图  [大厅: Area_Intro] [层级: hub / Area_Intro / 箱子]  F8/Esc 关闭 │
├──────────────────────┬────────────────────────────────────────┤
│ ▼ hub                │   ┌──────────────────────────────┐    │
│   ▼ Area_Intro       │   │  俯视格子图                   │    │
│     ▶ 箱子 @ (5,9)   │   │  █ 墙  □ 箱子  ◎ 玩家         │    │
│     ▶ 箱子 @ (3,2)   │   │  ◇ 传送门  · 可落点           │    │
│   ▼ Area_Center      │   └──────────────────────────────┘    │
│     ...              │   光标: (12, 7)  目标: 箱子「...」     │
├──────────────────────┴────────────────────────────────────────┤
│ 方向键/WASD 移动光标  PgUp/PgDn 切层  Enter 传送  滚轮缩放  右键拖动 │
└───────────────────────────────────────────────────────────────┘
```

- 左栏：树，鼠标点击选中，滚轮滚动；玩家所在节点用高亮色。
- 右栏：格子图。玩家当前位置为实心圆，箱子为带边线的方块，墙为深色，传送门为菱形，
  可落点为半透明圆点，光标为闪烁方框，悬停格子显示坐标与内容。
- 缩放：滚轮以鼠标为中心缩放（8–64 px/格），右键拖动平移；层大于视口时显示滚动条。
- 键盘：方向键/WASD 移动光标（WASD 与游戏移动键相同，但覆盖层期间游戏输入已被冻结，无冲突），
  `PageUp/PageDown` 在层级树中前后切换，`Enter` 传送，`Esc`/热键关闭。

**传送执行**（顺序固定，任何一步失败都回滚并提示）：

1. 若 `World.AnyBlockMoving || World.Transitioning`，先 `UndoManager.StopAnimations()`、清空各层 `phantoms`；
2. 冲掉可能存在的半截 Turn：`UndoManager.TurnCompletedDontPerform()`（仅当 `CurrentTurn.moves.Count>0`）；
3. 记录 `fromLevel/fromX/fromY/fromFlipH`，构造 `UndoManager.Move`，`Nudge=true`，
   `RegisterMove`，然后 `Movement.MoveBlock(block, toLevel, toX, toY)`；
4. `World.ComputeBlockLists()`、`Draw.ResetCameraOffset()`、眼睛偏移清零、
   `World.UpdateButtonsPressed()`、`World.UpdateCoveringButtonSounds(false)`、
   `Movement.DetectInfExitPreviews(block)`；
5. `World.MoveCount++`、`TurnCompletedDontPerform()` → 传送成为独立可撤销的一步；
6. 关闭覆盖层，恢复 `State`。

这条路径与 `FastTravel.Update` 的做法一致，因此不会与撤销功能冲突：
`Undo` 调 `DoUndo` → `MoveBlock` 回 `fromLevel`；`Redo` 走 `DoRedo` 回到目标层。

## 4. 架构

```
PPBTool.csproj
src/
  Plugin.cs            BepInEx 入口、Harmony 初始化、配置绑定、Update 补丁分派
  ModConfig.cs         键位/选项读取（ConfigEntry → Key 枚举）
  WinService.cs        一键通关的前置校验与执行
  LevelGraph.cs        场景扫描：层树、父块、节点标题、可传送目标计算
  TeleportService.cs   传送执行（Turn 登记、状态修复）
  TeleportOverlay.cs   IMGUI 全屏地图：树、格子图、光标、缩放、输入
  UiText.cs            中/英文字符串（按 Prefs.Language 选择）
  Log.cs               统一日志前缀
```

依赖方向：`Plugin → WinService/TeleportOverlay`；`TeleportOverlay → LevelGraph/TeleportService`；
`LevelGraph` 只读游戏状态，不产生副作用（纯扫描，便于审查）。

补丁只有一个：`World.UpdateInner` Prefix。
- 覆盖层打开 → 返回 false（冻结）。
- 否则检查热键 → 触发通关或打开覆盖层 → 返回 true 让游戏正常跑。

## 5. 错误处理

| 情况 | 行为 |
|---|---|
| 场景中没有玩家块 | 提示「找不到玩家」，不执行 |
| 目标层/目标格在扫描后失效（关卡切换） | 重新扫描；仍无效则提示并保持覆盖层 |
| 玩家正在过场/胜利动画 | 传送前先停动画；胜利中禁止传送并提示 |
| 覆盖层打开时关卡被外部切换（例如 CustomHubs F5） | 每帧校验 `World.currentLevelName` 与根层，变化则重建树 |
| 配置键位解析失败 | 回退默认 F7/F8，日志 warning |
| 异常 | 捕获并 `Logger.LogError`，保证不把异常抛回游戏 Update |

## 6. 验收标准

1. 任意普通关卡按 F7：出现原版胜利动画，随后按原版规则进入下一关或回到大厅；
   存档中该关标记为已通关（用游戏内关卡选择/大厅传送门变色验证）。
2. 原版大厅按 F8：树中能看到大厅根 → 各区域（如 `Area_Intro`、`Area_Center`）→ 区域里的箱子；
   选中任意区域的空格并 Enter，玩家出现在该格，画面/相机跟随正确。
3. 任意含箱子的关卡：在树中选中箱子节点，地图显示箱子内部；选中内部空格传送后，
   玩家位于箱子内部，箱子仍留在父层原位；`Ctrl+Z` 一次回到传送前，`Ctrl+Y` 再次进入。
4. 在箱子内传送到父层空格后按 `Ctrl+Z`：回到箱子内原位置（层级也恢复）。
5. 安装 CustomHubs + `test_hub` 后加载自定义大厅按 F8：
   树显示 `Area_1`、`Area_2` 及其中的箱子；传送落点与显示一致；F7 在自定义 puzzle 中能正常结算并回自定义大厅。
6. 覆盖层打开期间：游戏不响应移动/撤销/暂停；关闭后恢复；按 Esc 不会弹出游戏暂停菜单。

## 7. 构建与部署

```powershell
dotnet build -c Release                     # 自动复制到 BepInEx\plugins\PPBTool.dll
dotnet build -c Release -p:GameDir="其他路径" # 换游戏目录
```

目标框架 `net46`，引用游戏 `Managed` 下的 `Assembly-CSharp` 与 UnityEngine 模块、`BepInEx\core` 的
`BepInEx.dll`/`0Harmony.dll`，全部 `Private=false`，不把游戏程序集打进模组。

## 8. 测试策略

- **构建验证**：`dotnet build` 0 warning / 0 error。
- **加载验证**：启动游戏，`BepInEx\LogOutput.log` 出现 PPBTool 加载与补丁成功日志，无 Harmony 异常。
- **手动验收**：按第 6 节逐条执行；每步记录日志（扫描到的层数、目标、传送前后坐标）。
- 由于是 Unity 运行时行为，不做自动化单测；用日志 + 实机验证作为证据。
