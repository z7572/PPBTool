# PPBTool — Patrick's Parabox 工具模组

基于 BepInEx 5 的《Patrick's Parabox》辅助模组，提供两个功能：

1. **一键通关**：按热键（默认 `F7`）把当前关卡按游戏**正常结算流程**判定为通关
   （胜利动画、存档、解锁、进入下一关/回大厅都与手动通关一致）。
2. **全屏传送地图**：按热键（默认 `F8`）打开地图浏览器，自动扫描当前场景的
   大厅 → 区域 → 箱子 → 子箱子层级，用鼠标/键盘选点传送。

传送会登记为游戏原生的一步 `UndoManager.Turn`，因此 `Ctrl+Z` 撤销、`Ctrl+Y` 重做都能正常回到/再传送到目标层级。

## 安装

1. 游戏目录安装 **BepInEx 5.4.x（x64）**。
2. 将 `PPBTool.dll` 放入 `BepInEx\plugins\`。
3. 启动游戏。日志 `BepInEx\LogOutput.log` 中应出现：

   ```
   [Info   :   BepInEx] Loading [PPBTool 0.1.0]
   [Info   :   PPBTool] [PPBTool] loaded v0.1.0
   ```

首次运行会生成 `BepInEx\config\ppbtool.cfg`。

## 配置

```ini
[Keys]
# 一键通关
WinLevel = F7
# 打开/关闭传送地图
Teleport = F8

[Teleport]
# 传送后自动关闭地图
CloseAfterTeleport = true
# 传送是否进入撤销栈（false 时清空撤销历史）
Undoable = true
# 地图初始格子大小（8–64 像素）
CellSize = 24

[Diagnostics]
# 打开地图时把扫描到的层级树写进日志（排查自定义大厅兼容问题用）
LogSceneTree = false
```

热键接受任意 `UnityEngine.InputSystem.Key` 名称，例如 `F7`、`F8`、`K`、`Numpad5`、`Digit7`；
填 `None` 可禁用。配置可在运行时修改（读的是实时值）。

## 使用

### 一键通关

- 在任意**关卡**内按 `F7`。
- 大厅里按 `F7` 不会生效，会提示「大厅里不能通关」。
- 过场动画/结算中按 `F7` 会被拒绝，稍后再试。

### 传送地图

按键后游戏逻辑暂停（移动、撤销、暂停菜单、过场全部冻结），显示：

- **左栏**：当前场景的层级树。展开形式为
  `hub #1 → Area Intro → Box (4, 7) → …`
  - 原版大厅：`hub → 区域 → 区域里的箱子`
  - CustomHubs 自定义大厅：顶层 `Block` 会形成多个根层，显示为 `hub #1`、`hub #2`；
    `Ref ... Area_X` 会自动成为区域名，无需额外配置。
  - 玩家所在层级用黄色高亮，并在行尾标注「当前」。
- **右栏**：所选层的俯视格子图。墙、空格、箱子、传送门、玩家位置、可落点一目了然。
- **操作**：
  - 方向键 / `WASD`：移动光标
  - `PageUp` / `PageDown`：切换层级
  - `Enter` / `Space` / 鼠标左键：传送到光标处
  - 鼠标滚轮：以鼠标为中心缩放
  - 鼠标右键拖动：平移地图
  - `Esc` / `F8`：关闭

点击**箱子格子**表示「进入这个箱子」，会落到箱子内部离中心最近的空格；
要精确落到箱子内部的某一格，请在左栏选中该箱子对应的层，再点具体格子。

## 兼容性

- **原版**：大厅、区域、箱子、嵌套箱子、传送门、Infinity 区域均可扫描。
- **CustomHubs**：不依赖任何硬编码区域名或关卡名。`area_data`、`puzzle_data`、
  `puzzle_Ref` 生成的层级/区域名会被自动读取；自定义大厅里的 `F7` 也会走
  CustomHubs 重定向后的存档与回大厅逻辑。
- 只扫描**当前已加载场景**中的层级；跨关卡跳转请使用大厅里的传送门。

## 已知限制

- 传送落点必须是「空格且不是传送门格」，因此不能直接落在传送门上（避免误触发关卡切换）。
- 箱子内部落点取「离中心最近的空格」，不模拟真实进入时的贴边位置。
- 从翻转箱子内部传送到其它层级时，玩家/摄像机翻转按「层级路径 FlipH 的异或差」修正；
  极端的多父级引用/翻转组合下可能与手动走进走出略有差异。
- 多人关卡只移动排序第一的玩家块。

## 构建

需要 .NET SDK 与 .NET Framework 4.6 参考程序集（NuGet 自动还原）。

```powershell
dotnet build -c Release
# 默认安装到 D:\Programs\Patricks Parabox\BepInEx\plugins
# 换路径：
dotnet build -c Release -p:GameDir="C:\...\Patrick's Parabox"
```

工程只引用游戏 `Managed` 下的 `Assembly-CSharp`、UnityEngine 模块和
`BepInEx\core` 的 `BepInEx.dll` / `0Harmony.dll`（均 `Private=false`），不会把游戏程序集打进模组。

## 代码结构

| 文件 | 职责 |
|---|---|
| `src/Plugin.cs` | BepInEx 入口、Harmony 补丁、每帧热键分派 |
| `src/ModConfig.cs` | 配置项与键位解析 |
| `src/GameState.cs` | 空安全的玩家块访问 |
| `src/WinService.cs` | 一键通关（复用游戏正常结算） |
| `src/LevelGraph.cs` | 场景层级扫描、箱子落点、层级翻转路径 |
| `src/TeleportService.cs` | 传送执行与 UndoManager Turn 登记 |
| `src/TeleportOverlay.cs` | 全屏 IMGUI 地图浏览器 |
| `src/UiText.cs` | 中/英文案 |
| `src/Log.cs` | 日志包装 |
