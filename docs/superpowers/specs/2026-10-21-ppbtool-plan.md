# PPBTool 实施计划

配套规格：[2026-10-21-ppbtool-design.md](2026-10-21-ppbtool-design.md)

## 约束

- 目标框架 `net46`；只用游戏 `Managed` 程序集 + `BepInEx\core`，不引入新运行时依赖。
- 唯一 Harmony 补丁：`World.UpdateInner` Prefix（冻结覆盖层 + 热键分派）。
- 所有游戏 API 调用基于 `_decomp`（ILSpy 反编译验证），字段/方法名不得猜测。
- 每个任务结束都要 `dotnet build -c Release` 通过；不通过不进入下一任务。

## 任务 1：工程与骨架

文件：`PPBTool.csproj`、`src/Plugin.cs`、`src/ModConfig.cs`、`src/Log.cs`

- `PPBTool.csproj` 已可构建；补 `<Compile Include="src\**\*.cs" />` 的默认行为（SDK 默认已包含 `**/*.cs`，确认 `_decomp` 被排除）。
- `Plugin`：`BaseUnityPlugin`，`Awake` 里 `new Harmony("ppbtool").PatchAll()`，创建 `ModConfig`、`WinService`、`TeleportOverlay`，DontDestroyOnLoad 挂到 `World.instance.gameObject` 或自建 `GameObject`。
- `ModConfig`：`ConfigEntry<string>` WinLevel/Teleport，`ConfigEntry<bool>` CloseAfterTeleport/Undoable，`ConfigEntry<int>` CellSize；字符串 → `UnityEngine.InputSystem.Key` 解析，失败回退 F7/F8。
- `Log`：`ManualLogSource` 包装，统一 `[PPBTool]` 前缀。

验证：`dotnet build` 通过；启动游戏日志出现加载行。

## 任务 2：一键通关

文件：`src/WinService.cs`、`src/Plugin.cs`（补丁内分派）

- `CanWin(out reason)`：按规格 3.2 检查 `World.State/InHub/Winning/EndZoom/Transitioning/FindPlayerBlock`。
- `Win()`：`World.Winning = true; World.WinCount = 1.5f; World.WinSoundPlayed = false;`。
- `Plugin` 补丁 Prefix：先读热键，`WinService.TryWin()` 返回 false 时设置 toast。

验证：普通关卡 F7 出现胜利动画并进入下一关；大厅按 F7 只提示不结算；
自定义大厅 puzzle 中 F7 正常回大厅且存档记录通关。

## 任务 3：场景扫描（LevelGraph）

文件：`src/LevelGraph.cs`

- `Build()`：清空并重建 `List<LevelNode>`；`Dictionary<Level, Block>` 父块表（只取第一个）；
  根 = `World.levels.Where(l => !parents.ContainsKey(l))`；递归建子节点。
- `LevelNode`：`Level Level`、`Block ParentBlock`、`string Title`、`string Subtitle`、
  `int Depth`、`List<LevelNode> Children`、`bool ContainsPlayer`、`bool HasTargets`。
- 标题规则：`hubAreaName` 去下划线；根层用 `World.currentLevelName`；无名子层 `箱子 @ (x, y)`。
- `GetTargets(LevelNode)`：遍历 `level.blocks`；空格且非未解锁传送门 → 直接目标；
  箱子 → 递归 `ResolveEntry(box)` 下钻到最内层入口块，再 BFS 找最近空格。
- `ResolveEntry`：`box.SubLevel.GetExitBlock()` 为落点候选；若被占用则 BFS（4 邻域，跳过墙/玩家/传送门）。
- 只读，不写任何游戏状态；异常捕获后返回空列表并记日志。

验证：在原版大厅 F8 后日志打印「扫描到 N 层 / M 个目标」，与原版大厅区域数量目测一致。

## 任务 4：传送执行（TeleportService）

文件：`src/TeleportService.cs`

- `TryTeleport(LevelNode node, int x, int y, out string error)`：按规格 3.3 第 1–5 步执行。
- 先 `StopAnimations` + 清 `phantoms`；`CurrentTurn.moves.Count>0` 时 `TurnCompletedDontPerform()`。
- 构造 `UndoManager.Move`（`Nudge=true`、Anim* 为 0/1、`MovedSinceRestart` 继承），
  `RegisterMove` → `Movement.MoveBlock` → 状态修复 → `World.MoveCount++` → `TurnCompletedDontPerform()`。
- `Undoable==false` 时改为 `UndoManager.Clear()` + 直接 `MoveBlock`（不登记 Turn）。

验证：传送后游戏内 Undo（默认 `Z`）回原位，Redo（默认 `X`）再传送；箱子内外互传后撤销层级正确。

## 任务 5：覆盖层 UI（TeleportOverlay）

文件：`src/TeleportOverlay.cs`、`src/UiText.cs`

- `Update()`：读热键开/关；打开时 `LevelGraph.Build()`、记录原 `World.State`、`open=true`。
- `OnGUI()`：全屏 `GUI.Box` 背景；左栏树（自绘 `GUI.Button`/标签 + 滚动）；
  右栏格子图（`GUI.DrawTexture`/`GUI.Box` 绘制格子、玩家、箱子、传送门、目标点、光标）；
  底栏操作提示 + 状态文本；`GUI.depth = -100` 保证盖住游戏。
- 输入：`Event.current` 处理鼠标点击/滚轮；`Keyboard.current` 处理方向键/WASD/Enter/Esc/PgUp/PgDn。
- 缩放/平移：`cellSize` 8–64，滚轮以鼠标为中心；右键拖动平移。
- `UiText`：按 `Prefs.Language == "zh"` 返回中文，否则英文；未知语言回退英文。
- 关卡切换检测：每帧比较 `World.currentLevelName` 与根层，变化则重建。

验证：窗口缩放不影响布局；点树节点切换地图；点空目标点/Enter 传送成功；Esc 关闭后游戏恢复。

## 任务 6：集成、日志与收尾

- `Plugin` 统一异常边界：补丁内 try/catch，异常写 `LogError` 并关闭覆盖层，不让异常冒泡到游戏。
- 打开/传送/胜利各记一行 info 日志，便于用户反馈问题。
- README：安装方式、配置说明、热键、已知限制（只扫描当前场景）。
- 全量 `dotnet build` 0 warning；启动游戏确认 `LogOutput.log` 无异常。

## 验收

按规格第 6 节 6 条逐条手动验证，记录证据（日志片段 + 现象描述）。
