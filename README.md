# PPBTool — Patrick's Parabox 层级浏览器与工具模组

基于 BepInEx 5 的《Patrick's Parabox》辅助模组：

1. **一键通关**：`F7` 按游戏正常结算流程判定当前关卡通关，并完整播放原版约 1.5 秒的逐渐变亮/发白动画。
2. **层级浏览器**：`F8` 打开。自动扫描当前场景的 大厅 → 区域 → 箱子 → 子箱子，以及 ∞ 进入/退出区。
   - 点击箱子 = **打开该箱子层级**；点击空格 = 传送。
   - 可折叠层级树、自绘滚动条、水平滚动、可调树宽、返回上一级、虚空过滤。
   - ∞ 菜单叫“进入∞/ε”；已有链接只切换 UI，未生成项二次确认后才生成并进入。
   - 地图用游戏的墙/箱子/玩家/地砖纹理与颜色绘制；箱子递归显示内部内容。
   - ∞/ε 符号按游戏 `DrawInfEffect` 的尺寸、透明度和多重堆叠规则绘制。
   - “关卡缩略图”默认开启；可在工具栏关闭以退回紫色标记。

传送会登记为游戏原生的一步 `UndoManager.Turn`，`Ctrl+Z` 撤销、`Ctrl+Y` 重做都能回到/再进入目标层级。

## 安装

1. 游戏目录安装 **BepInEx 5.4.x（x64）**。
2. 将 `PPBTool.dll` 放入 `BepInEx\plugins\`。
3. 启动游戏，确认 `BepInEx\LogOutput.log` 出现：

   ```
   [Info   :   BepInEx] Loading [PPBTool 0.1.0]
   [Info   :   PPBTool] [PPBTool] loaded v0.1.0
   ```

首次运行生成 `BepInEx\config\ppbtool.cfg`。

## 配置

```ini
[Keys]
WinLevel = F7
Teleport = F8

[Teleport]
CloseAfterTeleport = true
Undoable = true
CellSize = 24
HideVoidBoxes = false      ; 隐藏虚空层/虚空箱
LevelThumbnails = true     ; 关卡缩略图（默认开）
                           ; 旧 PortalThumbnails 会作为回退值读取

[Diagnostics]
LogSceneTree = false
```

- 热键接受 `UnityEngine.InputSystem.Key` 名称（`F7`、`K`、`Numpad5`、`Digit7`…），`None` 禁用。
- 显示开关也可在工具栏里切换，并即时写回配置。

## 使用

### 一键通关

- 关卡内按 `F7`：播放原版白化动画后正常存档/解锁/进入下一关或回大厅。
- 大厅内按 `F7` 只提示，不结算；过场/结算中会被拒绝。

### 层级浏览器

- 左栏：可折叠的层级树
  - 箭头 `+ / -` 折叠/展开；
  - 玩家所在层黄色高亮并标“当前”；
  - 标签显示 `void`、`∞进入块`、`∞退出块`、`∞进入←n`、`∞退出←n`；
  - 工具栏有折叠全部/展开全部。
- 右栏：当前层俯视图
  - 点击箱子 → 打开该箱子层级；
  - 点击空格 → 传送；
  - 点击传送门/墙 → 只提示；
  - 使用游戏纹理绘制墙、箱子（含递归内部内容）、玩家脸、地砖；
  - 箱子不再显示 `F/C/I/O...` 徽标；属性在右侧详情行显示：
    `(3,6) 打开层级: Area Transfer · 箱子落点 (5,5) · 克隆 · 翻转`。
- 操作
  - 方向键 / `WASD`：移动光标
  - `Enter` / `Space` / 左键：打开箱子或传送
  - 左键拖动 / 右键拖动 / 中键拖动：平移
  - 滚轮向上：放大；滚轮向下：缩小（以鼠标为中心）
  - `Shift` + 滚轮：横向滚动层级树
  - 拖动层级树/地图之间的分隔条：调整树宽
  - `PageUp` / `PageDown`：切换层级
  - `Backspace`：返回上一级
  - `F`：适应窗口；`H`：隐藏/显示虚空
  - `Esc` / `F8`：关闭
- 悬停在箱子上时，右侧地图详情行会追加该箱子的属性（克隆、翻转、∞、可附身等）。
- 缩放/平移时边缘方块会按可见部分裁剪显示，不再整块消失。

### 进入∞/ε

- `ε 进入` / `∞ 退出` 打开“进入∞/ε”菜单。
- 已有链接逐条列出：`ε #0 → 目标层` / `∞ #0 → 目标层`。
  - 点击只改变 UI 选中层，不生成、不改场景、不移动玩家。
- 末尾有 `ε #n（未生成）` / `∞ #n（未生成）`：
  - 第一次点击只弹确认；
  - 确认后才 `CreateInfEnterZone` / `CreateInfExitZone`，然后进入；
  - 取消不改场景。
- 多重无限：`#0/#1/#2…` 全部列出；新索引取第一个空位，不覆盖已有项。
- ε 进入 → 实际 `block.SubLevel`；∞ 退出 → 实际 `block.OuterLevel`，光标对准出口箱。
- 方块上的 ε/∞ 符号严格按游戏 `DrawInfEffect`：ε 用 `InfParticleOutlineTex`，∞ 用 `InfParticleTex`；多重符号尺寸缩小并纵向堆叠。

### 显示开关

- **关卡缩略图**（默认开）：传送门格绘制 `Hub.puzzleData[scene].thumbnail`（缺省 `Resources.Load("level_thumbnails/…")`）；关闭时退回紫色标记，作为性能/兼容选项。
- “关卡配色”按钮已移除；地图始终使用游戏纹理与颜色。空格底色来自该层出口块的游戏背景色，不再是固定深灰。

## 兼容性

- **原版**：大厅/区域/箱子/嵌套箱子/∞ 区/传送门/虚空。
- **CustomHubs**：不硬编码区域名；多顶层 `Block` 根层显示为 `hub #1/#2`；`Ref ... Area_X` 作为区域标题；自定义 puzzle 的 `F7` 仍走 CustomHubs 的存档/回大厅逻辑。
- 不引用 CustomHubs 程序集，不与其补丁冲突（仅补 `World.UpdateInner`）。
- 虚空层可正常进入/传送；`隐藏虚空` 只影响显示。

## 已知限制

- v3 方块渲染是“复用游戏纹理 + 复刻 `Draw` 公式”，不是直接调用游戏 `Draw` 队列；极端的计数墙、特殊效果、深层嵌套可能与游戏有细微差异。
- 箱子内部递归到 4 层 / 单箱 1600 格上限；更大或更深时只显示底色/边框，避免卡顿。
- 玩家表情只复刻眼睛/常见嘴巴；部分 `PlayerOrder` 特殊表情与眨眼相位可能不同。
- 只显示当前已加载场景；跨关卡跳转仍用大厅传送门。
- 箱子落点取“内部离中心最近空格”，∞进入落点也取最近空格，不模拟按方向贴边的精确落点。
- 传送门只显示，不主动加载目标关卡。
- 虚空箱/生成 ∞ 区的结构依赖游戏自身的 `CreateInf*`；异常关卡生成失败只提示。
- 多人关卡只移动排序第一的玩家块。

## 构建

```powershell
dotnet build -c Release
# 默认安装到 D:\Programs\Patricks Parabox\BepInEx\plugins
dotnet build -c Release -p:GameDir="C:\...\Patrick's Parabox"
```

只引用游戏 `Managed` 下的 `Assembly-CSharp`、UnityEngine 模块与 `BepInEx\core` 的 `BepInEx.dll`/`0Harmony.dll`（`Private=false`）。

## 代码结构

| 文件 | 职责 |
|---|---|
| `src/Plugin.cs` | BepInEx 入口、Harmony 补丁、每帧热键分派 |
| `src/ModConfig.cs` | 配置项与键位解析 |
| `src/GameState.cs` | 空安全玩家块访问 |
| `src/WinService.cs` | 一键通关（正常白化结算） |
| `src/LevelGraph.cs` | 场景层级、多重 ∞ 链接、虚空/克隆/属性、路径翻转 |
| `src/TeleportService.cs` | 传送执行与 UndoManager Turn 登记 |
| `src/TeleportOverlay.cs` | 折叠树、自绘控件、地砖图案、∞ 菜单、地图交互 |
| `src/UiText.cs` | 中/英文案 |
| `src/Log.cs` | 日志包装 |
