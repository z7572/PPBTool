# PPBTool — Patrick's Parabox 层级浏览器与工具模组

基于 BepInEx 5 的《Patrick's Parabox》辅助模组：

1. **一键通关**：菜单栏右上角的“一键过关”按钮，按游戏正常结算流程判定当前关卡通关，并完整播放原版约 1.5 秒的逐渐变亮/发白动画。
2. **层级浏览器**：`F7` 打开。自动扫描当前场景的 大厅 → 区域 → 箱子 → 子箱子，以及无限进入/退出区。
   - 点击箱子 = **打开该箱子层级**；点击空格 = 传送。
   - 可折叠层级树、自绘滚动条、水平滚动、可调树宽、回到引用处、虚空过滤。
   - 菜单叫“无限进入 / 无限退出”；已有链接只切换 UI，未生成项二次确认后才生成并进入。
   - 地图用游戏的墙/箱子/玩家/地砖纹理与颜色绘制；箱子递归显示内部内容。
   - 无限进入/退出符号按游戏 `DrawInfEffect` 的尺寸、透明度和多重堆叠规则绘制。
   - “关卡缩略图”默认开启；可在工具栏关闭以退回紫色标记。

传送会登记为游戏原生的一步 `UndoManager.Turn`，游戏内 Undo/Redo（默认 `Z` / `X`，可在游戏按键设置里改）都能回到/再进入目标层级。

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
OpenMap = F7

[Teleport]
CloseAfterTeleport = true
Undoable = true
CellSize = 24
HideVoidBoxes = false      ; 隐藏游戏当前不可见的虚空层/虚空箱
LevelThumbnails = true     ; 关卡缩略图（默认开）
                           ; 旧 PortalThumbnails 会作为回退值读取

[Diagnostics]
LogSceneTree = false
LogInfSymbols = false       ; 记录无限符号判定（排查 hub 显示差异）
LogBlockTraits = false      ; 悬停方块属性日志（每个方块每次打开一次）

[UI]
Language = auto             ; auto / zh / en
ShowBlockTraits = true      ; 右侧详情显示方块属性
ShowTraitIcons = true       ; SE8 方块上的 Order/Shed/InnerPush 图标
```

- 地图快捷键使用 BepInEx `KeyboardShortcut`：`F7`、`Ctrl+F7` 等；在配置界面清空快捷键即可禁用。
- 旧配置里的 `WinLevel` / `Teleport` 已废弃，当前只读取 `OpenMap`；旧的 `F7`/`F8` 快捷键值不会再使用。
- 界面语言初始为 `auto`，跟随游戏语言；工具栏最右侧按钮可在中文/English 间切换，并写回 `[UI] Language`。
- 显示开关也可在工具栏里切换，并即时写回配置。

## 使用

### 一键通关

- 打开层级浏览器，点击菜单栏右上角、语言切换左边的 **一键过关** 按钮。
- 成功时关闭浏览器并播放原版白化动画；大厅内只提示，不结算；过场/结算中会被拒绝。
- 不再提供通关快捷键。

### 层级浏览器

- 左栏：可折叠的层级树
  - 箭头 `+ / -` 折叠/展开；
  - 玩家所在层黄色高亮并标“当前”；
  - 标签显示 `void`、`refs ×N`、`Infinite Enter ×N`、`Infinite Exit ×N`、`hidden ×N`；
  - 工具栏有折叠全部/展开全部。
- 右栏：当前层俯视图
  - 点击箱子 → 打开该箱子层级；
  - 点击空格 → 传送；
  - 点击传送门/墙 → 只提示；
  - 使用游戏纹理绘制墙、箱子（含递归内部内容）、玩家脸、地砖；
  - 箱子外是黑色，只有箱子内部铺箱子背景色；没有引用的箱子使用 block/palette/中性色 fallback，不再纯黑；箱子框线/网格线宽度按游戏 `ComputeBorderThickness` 动态计算；
  - 无限进入/退出箱子在整个箱子上叠加半透明符号；
  - 玩家箱子的脸：在它自己的箱内层级里覆盖整个箱子（高透明度）；作为普通箱子里的玩家箱子时只在该箱子 cell 上（低透明度）；
  - 克隆箱内部墙壁按 `ComputeWallColor` 的 clone 分支变亮；
  - 镜像箱/镜像墙绘制游戏的白色 FlipShine 光带；
  - 计数墙绘制 `numSolved / numRequired` 和中间计数条；
  - 大厅里按游戏 `DrawPortalLines` 绘制传送门之间的解锁连接线；
  - 困难传送门 Hard 1 红、Hard 2 蓝，使用 `Particle.hardPortalColor`/`visualPortalColor` 的 HSV；
  - 箱子不再显示 `F/C/I/O...` 徽标；属性在右侧详情行显示：
    `(3,6) 打开层级: Area Transfer · 主出口 · 克隆 · SE8 (机制图标)`。
- 特殊属性
  - 详情行显示：玩家、可占据、主出口/克隆、无限进入/退出、Counting、SpecialEffect；
  - 关卡有非默认 Priority / Inner Push / Shed 时，地图顶部显示机制条；
  - `SpecialEffect == 8` 的箱子按游戏原逻辑显示 Order / Shed / InnerPush 图标；
  - `Priority / Inner Push / Shed` 是关卡级机制，不会重复写进每个方块的属性；
  - `floatinspace` 不显示：加载后会进入隐藏 void，运行时无法可靠恢复该标记；
  - `Locked / Weight / Shield / Rooted` 不是原版可玩关卡属性，不显示。
- 操作
  - 方向键 / `WASD`：移动光标
  - `Enter` / `Space` / 左键：打开箱子或传送
  - 左键拖动 / 右键拖动 / 中键拖动：平移
  - 滚轮向上：放大；滚轮向下：缩小（以鼠标为中心）
  - `Shift` + 滚轮：横向滚动层级树
  - 拖动层级树/地图之间的分隔条：调整树宽
  - `PageUp` / `PageDown`：切换层级
  - `Backspace`：回到引用处（不是树上的上一级）
  - `Alt+Left` / `Alt+Right`：视图后退 / 前进
  - `F`：适应窗口；`H`：隐藏/显示虚空
  - `Esc` / `F7`：关闭
- 工具栏直接提供 `←` / `→` 视图后退/前进（没有二级菜单）；`回到引用处` 仍是独立按钮。后退/前进只改变地图视图，不写游戏 Undo。
- 多引用层级：`回到引用处` 有多个候选时打开引用选择器；无外层引用时禁用并提示。引用选择器是模态的，鼠标不会穿透到地图传送。
- 同格重叠的箱子：只点击网格里的箱子；被覆盖的引用在引用选择器里标 `hidden overlap`。
- 悬停在方块上时，右侧地图详情行会追加该方块的真实属性（主出口/克隆、无限进入/退出、Counting、SE、玩家/可占据等）。
- 缩放/平移时边缘方块会按可见部分裁剪显示，不再整块消失。

### 无限进入 / 无限退出

- `无限进入` / `无限退出` 按钮打开对应菜单。
- 已有链接逐条列出：`无限进入 #0 → 目标层` / `无限退出 #0 → 目标层`。
  - 点击只改变 UI 选中层，不生成、不改场景、不移动玩家。
- 末尾有 `无限进入 #n（未生成）` / `无限退出 #n（未生成）`：
  - 第一次点击只弹确认；
  - 确认后才 `CreateInfEnterZone` / `CreateInfExitZone`，然后进入；
  - 取消不改场景。
- 多重无限：`#0/#1/#2…` 全部列出；新索引取第一个空位，不覆盖已有项。
- 无限进入 → 实际 `block.SubLevel`；无限退出 → 实际 `block.OuterLevel`，光标对准出口箱。
- 方块上的符号严格按游戏 `DrawInfEffect`：进入用 `InfParticleOutlineTex`，退出用 `InfParticleTex`；多重符号尺寸缩小并纵向堆叠。

### 显示开关

- **关卡缩略图**（默认开）：传送门格绘制 `Hub.puzzleData[scene].thumbnail`（缺省 `Resources.Load("level_thumbnails/…")`）；关闭时退回紫色标记，作为性能/兼容选项。
- “关卡配色”按钮已移除；地图始终使用游戏纹理与颜色。空格底色来自该层出口块的游戏背景色，不再是固定深灰。

## 兼容性

- **原版**：大厅/区域/箱子/嵌套箱子/无限区/传送门/虚空。
- **CustomHubs**：不硬编码区域名；多顶层 `Block` 根层显示为 `hub #1/#2`；`Ref ... Area_X` 作为区域标题；自定义 puzzle 仍通过菜单栏的“一键过关”走 CustomHubs 的存档/回大厅逻辑。
- 不引用 CustomHubs 程序集，不与其补丁冲突（仅补 `World.UpdateInner`）。
- 虚空层可正常进入/传送；`隐藏虚空` 只隐藏游戏当前不可见的虚空，玩家/箱子进入过、游戏正在显示的虚空会保留。

## 已知限制

- v4 的“回到引用处”按 `BlocksWithThisAsTheirSubLevel` 的真实引用工作，不再依赖树父节点；无外层引用时禁用。
- 同格重叠箱子只渲染/点击网格 occupant；被覆盖引用仍可 Back/通过引用选择器进入，但地图格点击会打开 occupant。
- 克隆箱颜色上下文：从地图点击 clone 箱进入其层级时会保留该 clone 作为颜色 owner；从层级树直接打开同一层时没有“从哪个 clone 进入”的信息，会退回 primary exit block 颜色。
- “后退/前进”是地图视图历史，不改变玩家位置、不写 `UndoManager`；游戏内 Undo/Redo 默认 `Z`/`X`。
- 无限符号按 `DrawBlocks` 入队条件、`DrawInfEffect` 数量、`DrawStyle`/`iii` 和父层可见性判定；计数 <=0 或纹理缺失时不画，不再用文本 fallback。地图查看 void 时不依赖玩家是否已进入无限空间，离开后也保留符号。
- 没有引用的层级使用 block/palette/中性色 fallback；不再纯黑。
- 箱子内部递归到 4 层 / 单箱 1600 格上限；更大或更深时只显示底色/边框，避免卡顿。
- 玩家表情只复刻眼睛/常见嘴巴；部分 `PlayerOrder` 特殊表情与眨眼相位可能不同。
- 小嵌套箱子的边框最小 1px，按游戏 `ComputeBorderThickness` 可能视觉上偏粗。
- v3/v4 方块渲染是“复用游戏纹理 + 复刻 `Draw` 公式”，不是直接调用游戏 `Draw` 队列；极端特殊效果/深层嵌套可能与游戏有细微差异。
- 只显示当前已加载场景；跨关卡跳转仍用大厅传送门。
- 箱子落点取“内部离中心最近空格”，无限进入落点也取最近空格，不模拟按方向贴边的精确落点。
- 传送门只显示，不主动加载目标关卡。
- 虚空箱/生成无限区的结构依赖游戏自身的 `CreateInf*`；异常关卡生成失败只提示。
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
| `src/LevelGraph.cs` | 场景层级、多重无限链接、虚空/克隆/属性、路径翻转 |
| `src/TeleportService.cs` | 传送执行与 UndoManager Turn 登记 |
| `src/TeleportOverlay.cs` | 折叠树、自绘控件、地砖图案、无限菜单、地图交互 |
| `src/UiText.cs` | 中/英文案 |
| `src/Log.cs` | 日志包装 |
