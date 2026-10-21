# PPBTool v3 交接说明

日期：2026-10-22
仓库：`D:\Codes\PPBTool`
游戏：`D:\Programs\Patricks Parabox`
当前分支：`master`
构建：`dotnet build -c Release`，0 warning / 0 error
DLL：`D:\Programs\Patricks Parabox\BepInEx\plugins\PPBTool.dll`

> 重要：v3 的渲染改动**没有启动游戏做实机验收**，全部是源码对照 + 静态构建结果。接手后应先做一轮实机截图对照。

## 1. 版本状态

v3 功能已全部落地，最近提交：

- `1179f28` 计数墙数字
- `cc73daa` 隐藏虚空只隐藏游戏不可见虚空
- `6c1e587` 克隆箱墙壁上下文 + 大厅传送门连接线
- `f68b06c` 镜像闪带动画
- `e6a5529` Hard 传送门颜色 + 克隆墙变亮 + FlipShine
- `269d8b8` 墙变亮/背景变暗/嵌套网格非等比对齐
- `328dd30` IMGUI 全亮度颜色、关卡/箱子网格、full-size 嵌套内容
- `4f4f347` HSV/渐变/网格颜色、去掉重复箱子内背景

当前工作树在交接提交前应保持 clean。

## 2. 已实现功能

### 基础

- `F7` 一键通关：设置 `World.Winning/WinCount=0`，播放约 1.5 秒原版白化/发白动画，再正常存档/解锁/下一关。
- `F8` 全屏层级浏览器。
- 传送登记为游戏原生 `UndoManager.Turn`，游戏内 Undo/Redo（默认 `Z` / `X`，可重绑）与游戏一致。
- 兼容 CustomHubs（不引用其程序集，仅 Harmony patch `World.UpdateInner`）。

### 层级树

- 可折叠、默认展开根层/玩家路径。
- 自绘滚动条、水平滚动、树/地图分隔条可拖动调宽。
- 标题栏路径/副标题省略号，不换行。
- 虚空过滤：只隐藏游戏当前不可见的虚空（`WentToInfZone && blockList.Count > 1`）；玩家/箱子进入过的虚空保留。
- 标签：`void`、`Infinite Enter`、`Infinite Exit`、`Infinite Enter←n`、`Infinite Exit←n`。

### 地图

- 点击箱子 = 打开箱子层级；点击空格 = 传送；传送门/墙只提示。
- 方向键/WASD 移动光标，Enter/Space 打开/传送，Backspace 返回上级。
- 滚轮向上放大，向下缩小；左/右/中键拖动平移。
- 缩放/平移时边缘格子按可见部分裁剪。
- 右侧详情行显示当前格信息 + 箱子属性：
  `(3,6) 打开层级: Area Transfer · 箱子落点 (5,5) · 克隆 · 翻转`。
- 多玩家只处理排序第一的玩家块。

### 真实绘制

- 墙：`WallTiles/WallTilesFlip`、`Autotile.quads` + `WallBig/WallBigFlip` 四象限、`ComputeWallColor`、连接边/锁定色、计数墙。
- 箱子：`ComputeBlockColor`、`CenterGradientTex`（黑色 alpha 0.08）、网格、`DrawBlockBorder` 分段边框、递归内容。
- 箱子内容：4 层 / 单箱 1600 格上限；内容画满整个箱子，边框覆盖在内容上，不占内容空间。
- 玩家：`DrawFace` 的眼睛/嘴巴/eye offset/翻转/透明度公式；玩家箱子在整个箱面覆盖脸。
- 地砖：按钮/PlayerButton/Info/Smile/FastTravel/Break/Gallery/Show/Demo/Item/Label/LevelPortal。
- 传送门：关卡缩略图默认开启（`LevelThumbnails=true`，旧 `PortalThumbnails` 回退）；Hard 1 红、Hard 2 蓝。
- 空格/背景：箱子外黑；箱子内部用箱子底色；当前层位于箱子内时铺关卡网格背景。
- 克隆箱：箱子/网格/内部墙按 clone 变亮；从地图点击 clone 箱进入时保留该 clone 作为颜色 owner。
- 镜像箱/墙：`DrawFlipShine` 白色光带，按 `MiscAnimT + Time.unscaledTime` 运动。
- 计数墙：`numSolved/numRequired` + 中间计数条，颜色按 `LineColor(active:false)`。
- 大厅连接线：按 `DrawPortalLines` 的 `UnlockLine`/shape/dx/dy/thick/lastPortal/finishToReception 绘制。
- 无限进入/退出：
  - ε 进入 / ∞ 退出（内部按游戏纹理）；
  - 整个箱子覆盖半透明符号；
  - 多重符号按 `1.8×` 缩小 + 纵向堆叠；
  - 虚空父层不绘制这些父层覆盖元素。
- 菜单/树文字：`无限进入`/`无限退出`、`Infinite Enter`/`Infinite Exit`，不在 UI 文字中使用 ∞/ε。

## 3. 关键文件

```text
src/Plugin.cs            BepInEx 入口、Harmony World.UpdateInner、热键、OverlayDriver
src/ModConfig.cs         键位/配置；LevelThumbnails，旧 PortalThumbnails 回退
src/GameState.cs         空安全玩家块
src/WinService.cs        F7 白化通关
src/LevelGraph.cs        层级树、父子、多重无限、虚空、克隆、属性、路径翻转
src/TeleportService.cs   可撤销传送、翻转、WentToInfZone
src/TeleportOverlay.cs   IMGUI、树、地图、真实渲染、无限菜单
src/UiText.cs            中英文案
src/Log.cs               日志
README.md                使用/配置/限制
docs/superpowers/specs/2026-10-21-ppbtool-v3-design.md  v3 设计规格
_decomp/                 ILSpy 反编译参考（gitignored，不发布）
```

## 4. 配置

```ini
[Keys]
WinLevel = F7
Teleport = F8

[Teleport]
CloseAfterTeleport = true
Undoable = true
CellSize = 24
HideVoidBoxes = false
LevelThumbnails = true
# 旧 PortalThumbnails 会作为 LevelThumbnails 的回退值

[Diagnostics]
LogSceneTree = false
```

## 5. 已知限制 / 待实机确认

1. **v3 不是直接调用游戏 `Draw` 队列**，而是复用纹理 + 复刻公式（原因见 v3 规格 §4.0）。视觉可能仍有细节差异。
2. 克隆箱颜色上下文：
   - 从地图点击 clone 箱进入其内部时，保留该 clone 作为颜色 owner；
   - 从层级树直接打开同一层时无法区分 clone 入口，会退回 primary exit block 颜色。
3. 嵌套小箱子边框最小 1px；按游戏 `ComputeBorderThickness` 可能视觉上偏粗。
4. 玩家表情只复刻眼睛/常见嘴巴；部分 `PlayerOrder` 表情、眨眼相位可能不同。
5. 箱子递归有深度/格数上限；超大箱子只显示底色/边框。
6. 大厅连接线、Hard 传送门颜色、计数墙数字、镜像光带都未实机截图对照。
7. 虚空可见性按 `Draw.DrawBlocks` 的 `WentToInfZone && blockList.Count > 1`；如果“箱子进入虚空但玩家没进去”游戏仍显示而 `WentToInfZone` 为 false，需要再补状态。
8. 仍未启动游戏实机验证；不要在没有截图对照的情况下继续大改颜色/线宽。

## 6. 接手后建议顺序

1. 先跑 `dotnet build -c Release`，确认 0/0。
2. 启动游戏，按 `F8` 打开地图，逐项截图：
   - 普通箱子 vs 克隆箱内部墙亮度；
   - Hard 1 红框、Hard 2 蓝框；
   - 计数墙数字上下顺序/字号/颜色；
   - 大厅传送门连接线；
   - 镜像箱白色光带；
   - 网格是否在墙后、边缘是否干净；
   - 虚空过滤在进入虚空前后。
3. 发现问题时优先对照 `_decomp/Draw.cs`：
   - `DrawBlock`/`DrawBlockBorder`/`DrawWall`
   - `DrawFace`
   - `DrawInfEffect`
   - `DrawPortalLines`/`DrawPortalLine`/`LineColor`
   - `DrawFlipShine`
4. 任何颜色改动先确认是 `HSVToRGBGUI` 的材质补偿问题，还是 `ComputeBlockColor/ComputeWallColor` 的公式/尺寸参数问题，不要直接乘系数。

## 7. 新对话启动提示

可直接把下面这段发给接手的新对话：

```text
接手 D:\Codes\PPBTool 的 PPBTool v3。先读 docs/HANDOFF-v3.md、README.md 和
docs/superpowers/specs/2026-10-21-ppbtool-v3-design.md。
当前 master 最新提交是 1179f28（交接文档提交紧随其后），dotnet build -c Release 0/0。
v3 渲染未实机验收；不要先改架构，先根据我提供的游戏截图逐项对照
DrawBlock/DrawWall/DrawBlockBorder/DrawFace/DrawInfEffect/DrawPortalLines/DrawFlipShine。
重点检查：克隆墙亮度、Hard 传送门红/蓝、计数墙数字、大厅连接线、镜像光带、
网格层级、虚空过滤。每次修改后构建并提交小 commit。
```
