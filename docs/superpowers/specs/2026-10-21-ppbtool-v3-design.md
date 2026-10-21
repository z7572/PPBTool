# PPBTool v3 — 真实方块渲染、∞/ε 符号与渲染管线替换

日期：2026-10-21
状态：**已实施（v3 first pass）**
基线：v2 已提交（`76f4e23`），v3 实施后 `dotnet build -c Release` 0 warning / 0 error
前置文档：[v2 设计规格](2026-10-21-ppbtool-v2-design.md)

## 1. 目标

v2 的地图仍是“示意图 + 自绘图案”。v3 改为**按游戏原始绘制逻辑渲染地图中的方块与地砖**：

1. **删除“关卡配色”开关/按钮/配置**，地图始终使用游戏自身的颜色与纹理。
2. **删除箱子下方的 `F/C/I/O/*/P/L` 徽标行**；属性信息只保留在标题栏悬停文本中。
3. 按游戏逻辑绘制：
   - 墙（不同连接状态、边框、计数墙、锁定/解锁、rooted 等）；
   - 箱子（箱子底色/渐变/网格/边框，并**递归显示箱子内部内容**）；
   - 玩家（眼睛、嘴巴、朝向、翻转、eye offset）；
   - 传送门、按钮、终点、快速移动、Info/Smile/Break/Gallery/Demo/Item/Label 等地砖。
4. 按游戏逻辑绘制 **∞ / ε 符号**：
   - ∞ = 无限退出（`IsSomeInfExitBlock`，`InfParticleTex`）；
   - ε = 无限进入（`IsSomeInfEnterBlock`，`InfParticleOutlineTex`）；
   - 多重无限渲染多个符号，尺寸缩小并纵向堆叠，布局/透明度与游戏一致。
5. 菜单“进入无限区”改为 **“进入∞/ε”**，条目使用 `ε #n` / `∞ #n`，不再使用“新建”字样。
6. **空格/关卡背景**使用该层出口块的游戏背景色，不再固定深灰。
7. **“关卡缩略图”**（原“传送门缩略图”）保留，默认开启。
8. 保留 v2 的交互：点击箱子进入层级、点击空格传送、树折叠/水平滚动/宽度调节、暂停 F8、Undo 兼容等。

## 2. 非目标

- 不修改游戏玩法、存档、关卡数据。
- 不渲染动态动画帧（地图是冻结快照）；移动中的方块按静止状态绘制。
- 不做关卡编辑器、不做全屏游戏内实时预览。
- 不保证与游戏 4K/HDR 截图逐像素一致；验收标准是同一纹理、同一公式、同一布局下的可辨认一致性。

## 3. 已核对的游戏绘制事实（实现依据）

### 3.1 ∞ / ε 符号：`Draw.DrawInfEffect`（`_decomp/Draw.cs:3243`）

```csharp
num   = (cellW + cellH) / 2f / screenHeight;
alpha = -log10(num + 0.5f) + 0.22f;          // 方块越小越不透明
if (cloneFade) alpha *= 1f - AnimT / AnimLength;

if (block.IsSomeInfEnterBlock) {             // ε 进入
    texture = InfParticleOutlineTex;
    count   = block.SomeInfEnterNum + 1;
} else {                                     // ∞ 退出
    texture = InfParticleTex;
    count   = block.SubLevel.infExitBlocks.IndexOf(block) + 1;
}

if (count == 1) {
    // 0.9 × cell，居中
} else {
    symbolW = cellW / (count + 1);
    symbolH = cellH / (count + 1);
    gapY    = (cellH - symbolH * count) / (count + 1);
    drawW   = symbolW * 1.8f;
    drawH   = symbolH * 1.8f;
    // 以 cell 中心为基准，从上方开始，每个符号向下移动 symbolH + gapY
}
```

要点：

- **ε 用 `InfParticleOutlineTex`，∞ 用 `InfParticleTex`**；两者都是 `Draw.instance` 私有字段。
- 透明度不是固定值，取决于**单元格像素尺寸 / 屏幕高度**；v3 必须使用同一公式（`Screen.height`）。
- 多重无限：
  - 进入索引 `SomeInfEnterNum = 0/1/2…` → `count = index + 1`；
  - 退出索引 = 该块在 `SubLevel.infExitBlocks` 中的位置 → `count = index + 1`；
  - `count > 1` 时**纵向堆叠**：符号尺寸按 `1/(count+1)` 缩小，间距按 `gapY`，不额外换行/缩放。
- 注意：公式给出的绘制矩形在 `count > 1` 时会比单元格略高约 4–8%（例如 N=2 约 1.044×、N=3 约 1.075×），但 `InfParticle*` 纹理自带透明边距，**实际可见符号落在格内**；因此按原公式绘制，不要人为把符号裁到格子内，也不要自己再加换行。
- `cloneFade` 在静态地图中为 false；屏幕截图模式倍增系数不适用。

### 3.2 墙：`Draw.DrawWall`（`Draw.cs:3009`）

- 纹理：
  - 小格：`WallTiles[block.WallTileIndex]` 或翻转版 `WallTilesFlip[block.WallTileIndexFlipH]`；
  - 大格（宽高 ≥ 256）：`WallBig[...]` 四象限，象限索引来自 `Autotile.quads[WallTileIndex]`。
- 颜色：`ComputeWallColor(parentBlock, fractionOfScreen, depth, cloneFade)`；
  - **墙块自身没有颜色**，必须用“所在层的出口块”的颜色（v2 的黑色墙就是取错了 `block.hue/sat/val`）；
  - `fractionOfScreen`、`val *= 2`、clone/flip/win 修正要按公式。
- 连接/边框：
  - `Level.leftWall/rightWall/upWall/downWall` 决定哪一侧画边框；
  - `Block.Rooted/RootedLeft/Right/Up/Down`、`Locked`、`Weight`、`Shield`、`SpecialEffect` 影响外观；
  - 计数墙 `numRequired/numSolved/Counting` 绘制数字与横条（`DrawWall` 尾段）。
- 阴影：`DrawBlockShadow`。

### 3.3 箱子：`Draw.DrawBlock`（`Draw.cs:3140`）

- 背景：`ComputeBlockColor` + `FullWhiteTex`；`CenterGradientTex` 叠加。
- 网格：`Prefs.Grid` 为真时，按 `SubLevel.width/height` 画内部网格。
- 边框：`ComputeBorderThicknessBlock` + `DrawBlockBorder`；`leftWall/rightWall/upWall/downWall` 决定边。
- 箱子内部内容：
  - 游戏通过 `DrawBlocks()`（`Draw.cs:1532`）与 `GoOutOneLevel()`（`Draw.cs:2517`）把子层按父层坐标/尺寸/翻转缩放绘制；
  - v3 需要递归渲染 `block.SubLevel` 的方块/地砖/玩家/∞ 符号，而不是只画一个空箱子。
- 特殊：
  - `SubLevel.InfZone` 的箱子背景透明；
  - `FilledWithWalls`、`SpecialEffect`、`IsSomeInfEnter/Exit`、`GeneratedInf` 影响颜色/符号/边框；
  - `borderColor`、`hubShortcutWon`、`CoveringButton` 等按游戏逻辑。

### 3.4 玩家：`Draw.DrawFace`（`Draw.cs:3968`）

- 眼睛纹理：`EyeOpen/EyeClosed/PossessEye`（小格用 `*Small`）。
- 眼睛位置：`x = ±0.25 * width`，`y = -0.05 * height`，叠加 `eyeXOffset/eyeYOffset`；
- 翻转：`flipH` 时左右眼交换；
- 透明度：`alpha = -log10(cellSize/screenHeight + 15) + 1.5`；有子层时用 `+1.5 → +0.5` 公式；
- 嘴巴/表情：`FrownyMouth/FlatMouth/OpenMouth/VMouth/MehMouth/SmileMouth/SquareMouth` 及翻转版；
- `FaceAnimT` 控制眨眼；`DrawIsPlayer`、`Possessable` 决定是否画脸。

### 3.5 地砖

v2 已按游戏纹理/几何绘制按钮、传送门、Info、Smile、FastTravel、Break、Gallery、Show 等；v3 保留并改为**始终使用游戏纹理/颜色**，不再受“关卡配色”开关影响。

## 4. 设计

### 4.0 为什么不能直接调用游戏的 Draw

“直接调用原函数”听起来最忠实，但游戏的 `Draw` 不是无状态的绘制库，而是一套**全局状态 + 队列 + 摄像机投影**的即时渲染器：

1. **全局可变状态**：
   `Draw.drawRect`、`clamp`/`clampRect`、`screenWidth/screenHeight`、`FlipH`、`FocusBlock`、`numLevelsUp`、`CameraMode`、`DrawStyle`、`hsv`、`minDrawSize`、`maxDepthMovingBlocks` 等都被所有绘制函数共享。直接调用前必须完整保存/恢复；任何遗漏都会污染游戏本帧/后续帧。
2. **不是“调用即画完”，而是“画一部分 + 入队”**：
   `DrawBlock` 只立即绘制底色/渐变/网格/边框的一部分，同时把脸、∞符号、flip shine、portal marker 等 `DrawTicket` 放进 `qFace`、`qInfEffect`、`qShine`、`qPortalMarker`、`qLev` 等静态队列；这些队列由 `Draw.DrawEverything()` 按固定顺序、配合 `StackClampTicket`/`EnableTicketClamp` 统一消费。单独调用 `DrawBlock` 会留下未消费的队列项，之后被游戏自己的绘制循环在**错误坐标**上画出来，或直接丢失。
3. **坐标/比例依赖游戏摄像机**：
   这些函数假定 `x/y/width/height` 是游戏屏幕空间中的投影结果，`fractionOfScreen`、边框厚度、墙大/小图分支、`ComputeWallColor/ComputeBlockColor` 都依赖 `screenHeight` 和当前 `numLevelsUp`/`FocusBlock`。我们的地图是任意单元格尺寸的独立面板，直接传入会命中不同分支（例如墙的大图/小图、边框厚度、透明度），外观与游戏不一致。
4. **绘制目标/GUI 矩阵**：
   部分路径用 `Graphics.DrawTexture` 立即绘制，部分经 `GUI`/`DrawTexture`，依赖当前 GUI 矩阵、裁剪和渲染目标。地图在 `OnGUI` 里绘制，若直接复用，需要自己搭建并恢复整套矩阵/裁剪/渲染目标；否则会画到错误位置或污染游戏画面。
5. **静态队列是共享的**：
   即使保存/恢复了字段，队列中残留的 ticket 仍然会影响游戏之后的绘制；必须在调用前后清空/隔离队列，并复刻 `DrawEverything` 的消费顺序——这实质上就是重写一遍渲染管线。
6. **私有 API/版本脆弱**：
   `Draw` 的方法/字段（`qFace`、`DrawTicket` 等）都是私有实现细节；跨游戏版本/模组环境直接调用容易因签名/顺序变化崩溃。

因此 v3 选择的方案是：**复用游戏纹理 + 复刻 `Draw` 的颜色/几何/透明度公式**，由我们自己的 `GameArt` 渲染器持有独立状态，不触碰游戏的绘制队列。这样外观来源仍是游戏素材和公式，同时不会污染游戏帧。若你坚持“必须调用原函数”，需要另立一个**离屏渲染子项目**：保存/恢复全部 `Draw` 状态、清空并隔离队列、搭建独立矩阵/RenderTexture、按 `DrawEverything` 顺序消费，风险和验证成本会显著更高。

### 4.1 实现结构

为降低首版改动风险，渲染代码**内联在 `TeleportOverlay.cs` 的 GameArt 区域**，逻辑上等价于以下模块：

```text
TextureBank         GetPrivateTexture / GetPrivateTextureArray，缓存 Draw.instance 纹理
ColorMath           GameWallColor / GameBlockColor / GamePlayerColor / GameFloorColor / LevelBackgroundColor
WallArt             DrawWallArt / DrawWallQuad（WallTiles / WallBig / 四象限）
BoxArt              DrawBoxArt / DrawBoxContents / DrawBoxGrid / DrawBoxBorder（递归内容）
FaceArt             DrawFaceArt（眼睛/嘴巴/eye offset/翻转）
InfSymbolArt        DrawInfSymbols（严格按 3.1 公式）
FloorArt            DrawFloor / GameFloorColor（沿用 v2 的地砖纹理，去掉配色开关）
RenderContext       DrawRealCell（可见单元格、递归深度、翻转、裁剪）
```

后续若代码继续膨胀，可原样拆到 `src/GameArt/`，不改变行为。

### 4.2 递归箱子内容

`BoxArt.Draw`：

1. 计算箱子单元格 `R`。
2. 画箱子底色/渐变/网格。
3. 计算内容区 `inner`：按 `DrawBlock` 的边框/网格比例内缩。
4. 若 `SubLevel != null` 且未超过深度上限：
   - `RenderLevel(SubLevel, inner, depth+1, flipH ^ block.FlipH)`；
   - 内容区先 `GUI.BeginClip(inner)`，绘制后 `EndClip`；
   - 子层每格尺寸 = `inner.width / SubLevel.width`、`inner.height / SubLevel.height`；
   - 递归到 `depth >= 4` 或子格 < 2px 时停止，画纯色占位，避免无限递归/性能爆炸。
5. 画箱子边框（`leftWall/rightWall/upWall/downWall`）。
6. 画 ∞/ε 符号（严格按 3.1 公式；符号层在内容之后，只受地图面板裁剪，不被箱子内容区裁剪）。

翻转/坐标：
- 子层内容按 `GoOutOneLevel` 的规则处理 `FlipH`、`DrawXOffset/DrawYOffset`（静态时 offset=0）；
- 箱子自身 `FlipH` 与子层内容翻转异或；
- 内层内容裁剪在箱子矩形内。

> 说明：游戏原版通过 `DrawBlocks` 队列一次性绘制整个嵌套结构；v3 用同一组变换公式做递归迷你渲染，视觉上应一致。若后续发现某些特殊关卡（`FilledWithWalls`、InfZone、`iii`）需要特殊处理，在 `BoxArt` 中按 `SpecialEffect`/`InfZone` 分支。

### 4.3 墙的连接状态

- 直接使用 `Block.WallTileIndex` / `WallTileIndexFlipH`（`Autotile` 已在读档时算好）。
- 小格用 `WallTiles`/`WallTilesFlip`；大格用 `WallBig`/`WallBigFlip` 四象限。
- 边框由 `Level.leftWall/rightWall/upWall/downWall` 控制；`Rooted*`/`Locked`/`Counting` 按 `DrawWall` 逻辑。
- 颜色使用 `ComputeWallColor(level.GetExitBlock(), ...)`；墙自己的 `hue/sat/val` 不参与（通常为 0）。
- 需要绘制 `DrawBlockShadow`/`DrawBlockBorder` 的阴影/边框，避免“只有色块”的观感。

### 4.3.1 空格与关卡背景

v2 的空格用固定 `EmptyCol` 灰色填充，这是示意图做法，v3 必须改为游戏逻辑：

- 游戏没有“独立的灰色空格地砖”。一个关卡/箱子内部的空间，其底色来自**该层出口块的箱子底色**（`ComputeBlockColor(exitBlock)` + `CenterGradientTex`/背景处理）；空格就是这层底色。
- v3 的 `LevelBackgroundColor` / `MapBackground`：
  - 画布整体铺 `LevelBackgroundColor(level)`；
  - `LevelBackgroundColor` 使用 `level.GetExitBlock()` 的 `ComputeBlockColor`（必要时按游戏背景做暗化/去饱和），而不是固定 `EmptyCol`；
  - 空格不再覆盖固定灰色；只在没有 floor/block 时保留这层背景；
  - 箱子内部同理：`BoxArt` 的底色就是子层的背景，递归内容画在其上；
  - 网格线（`Prefs.Grid`）使用游戏 `DrawBlock` 的网格颜色公式（`val *= 0.65; val -= 0.075; clone 修正`），不是 UI 自己的分隔线。
- 验收：开启/关闭关卡缩略图、切换不同关卡时，空格底色应随该层出口块颜色变化，而不是永远深灰。
- 根层没有出口块（`GetExitBlock() == null`）时，退回游戏背景/深色中性底，不要把“没有出口块”误判成固定 `EmptyCol`。

### 4.4 玩家脸

- `FaceArt.Draw` 复制 `DrawFace`：
  - 眼睛纹理/大小格选择；
  - 眼睛位置/`eyeXOffset/eyeYOffset`/翻转；
  - 透明度公式；
  - 嘴巴/表情纹理；
  - `FaceAnimT` 眨眼（静态快照取当前值）。
- 只为 `DrawIsPlayer` 或 `Possessable` 的块画脸；箱子/墙不画。

### 4.5 ∞/ε 符号

- `InfSymbolArt.Draw(block, cellRect, cloneFade=false)` 严格按 3.1：
  - 选择 `InfParticleOutlineTex`（ε）或 `InfParticleTex`（∞）；
  - `count` 由 `SomeInfEnterNum+1` 或 `infExitBlocks.IndexOf+1` 计算；
  - `alpha = -log10(((w+h)/2)/Screen.height + 0.5) + 0.22`；
  - `count==1`：0.9 cell 居中；`count>1`：1.8× 缩放、纵向堆叠、间距公式；
  - 严格按原公式绘制，**不要自行换行/缩放**；公式矩形虽略大于单元格，但纹理透明边距让可见符号保持在格内。不要把符号裁到单元格内部；只由地图面板统一裁剪。
- `DrawBlock` 的符号层在内容/边框之后；多重无限全部绘制，不合并。

### 4.6 菜单与文案

- 工具栏：把 `∞ 进入` / `∞ 退出` 改为 **`进入∞/ε`** 或两个按钮 `ε` / `∞`。
  - 推荐：单个 `进入∞/ε` 按钮，弹窗同时列出两组：
    - `ε #0 → <target>`（进入，`IsSomeInfEnterBlock`）；
    - `∞ #0 → <target>`（退出，`IsSomeInfExitBlock`）。
- 菜单标题：`进入∞/ε`。
- 未生成项：`ε #next（未生成）` / `∞ #next（未生成）`；确认框使用 `ε`/`∞`。
- 删除“进入无限区”文字（README/UI 文案同步）。
- 箱子上的 ∞/ε 符号是唯一“无限”视觉标识；不再使用 `I/O/*` 文字徽标。

### 4.7 删除项

- 删除 `[Teleport] UseLevelPalette` 配置与工具栏“关卡配色”按钮。
- 删除箱子下方的 `F/C/I/O/*/P/L` 徽标绘制（`BadgeText`/`DrawBlock` 底部条）。
- 标题栏悬停属性文本保留（克隆/翻转/∞/可附身等），作为唯一文字属性说明。
- 传送门缩略图保留，**默认开启**，UI/README 改名为 **“关卡缩略图”**。
  - 新增配置 `[Teleport] LevelThumbnails = true`；
  - 读取旧 `PortalThumbnails` 作为回退值，避免升级后丢失用户设置；
  - 关闭时只作为性能/兼容选项，退回紫色标记；默认使用游戏缩略图。

## 5. 性能

- 只遍历可见单元格；递归只在箱子内容区进行，深度 ≤4，子格 <2px 停止。
- `TextureBank` 反射/加载只做一次；地砖/墙/脸纹理缓存。
- 颜色计算、`CalcSize`、字符串拼接避免每帧重复；树内容宽度/路径省略号可缓存。
- 放大时箱子递归成本上升；用深度/像素阈值限制，必要时对深层箱子只画底色+边框。
- 平移/缩放时立即重绘；不做全量 RenderTexture 缓存（实现复杂，且游戏绘制是即时模式）。若性能不足，后续可对“整层缩略图”做缓存。

## 6. 验收标准

1. **墙**：任意关卡地图中的墙与游戏内截图对照，颜色/纹理/连接边/阴影一致；不再出现黑色墙。
2. **箱子**：能看到内部内容；嵌套箱子递归显示（至少 3 层）；翻转箱子的内容/边框方向正确；不出现无限递归/卡顿。
3. **玩家**：带眼睛/嘴巴/朝向；翻转与 eye offset 正确；箱子/墙不画脸。
4. **∞/ε**：
   - ε 使用 `InfParticleOutlineTex`，∞ 使用 `InfParticleTex`；
   - 单个符号 0.9 cell；多个符号按 `1.8×/堆叠/间距` 公式缩小并纵向排列；
   - 透明度随 cell/screen 高度变化；多重无限符号数量正确；
   - 不自行换行/缩放/裁到格子；可见符号保持在格内（纹理透明边距）；
   - 与游戏内同位置截图并排对照，布局/透明度可辨认一致。
5. **空格/背景**：空格不再固定深灰；底色随该层出口块的游戏背景色变化；箱子内部空格与子层背景一致。
6. **菜单**：标题为“进入∞/ε”；条目 `ε #n`/`∞ #n`；未生成项二次确认；不再出现“进入无限区”/“新建”。
7. **删除/保留项**：无“关卡配色”按钮/配置；箱子下方无 `F/C/I/O...` 徽标；“关卡缩略图”按钮保留、默认开启。
8. v2 交互不回归：点击箱子进入、点击空格传送、树折叠/水平滚动/宽度调节、暂停 F8、Undo/Redo、F7 白化。
9. `dotnet build -c Release` 0 warning / 0 error；不启动游戏做实机验证（按用户要求）时，至少完成代码对照审查。

## 7. 风险

1. **实现方式**：本规格选择“复用游戏纹理 + 复刻 `Draw` 公式”的 `GameArt` 模块，而不是直接调用游戏 `DrawBlocks` 队列（原因见 §4.0）。若坚持必须调用原函数，需要另立离屏渲染子项目并承担更高验证成本。
2. **递归箱子内容**：原版 `DrawBlocks` 的嵌套变换较复杂；规格采用 `GoOutOneLevel` 等价变换 + 深度上限 4。极端 `iii`/InfZone/FilledWithWalls 关卡可能需要专门分支。
3. **符号纹理/透明度**：使用游戏纹理（非字体），不受 UI 字体缺字影响；但纹理字段私有，需反射，缺失时退化。绘制矩形略大于单元格属于原公式，靠纹理透明边距保持可见符号在格内，不要手动裁剪。
4. **性能**：深层箱子/超大关卡可能掉帧；深度/像素阈值是保护措施。

无其它待确认项；递归深度 4、关卡缩略图默认开启且改名，均已确认。

## 8. 实际改动文件

- `src/TeleportOverlay.cs`：
  - 真实绘制：`DrawRealCell`、`DrawWallArt`、`DrawBoxArt`/`DrawBoxContents`、`DrawFaceArt`、`DrawInfSymbols`、`Game*Color`、`LevelBackgroundColor`；
  - 反射纹理：`GetPrivateTextureArray`/`EnsureGameArtTextures`；
  - 删除“关卡配色”按钮/`usePalette`、箱子徽标、底部颜色图例；
  - 右侧详情行追加箱子属性；
  - 菜单 `进入∞/ε`、`ε 进入`/`∞ 退出`。
- `src/ModConfig.cs`：删除 `UseLevelPalette`；新增 `LevelThumbnails = true`，旧 `PortalThumbnails` 作为回退。
- `src/UiText.cs`：`进入∞/ε`、`ε`/`∞`、`关卡缩略图` 文案。
- `src/LevelGraph.cs`：∞/ε 菜单项文案。
- `README.md` / 本规格：说明 v3 视觉与限制。

## 9. 测试策略

- 构建：`dotnet build -c Release`，0 warning / 0 error（已通过）。
- 代码对照：逐项对照 `_decomp/Draw.cs` 的 `DrawInfEffect`、`DrawWall`、`DrawBlock`、`DrawFace`、`DrawOrEnqueueFloor`、`GoOutOneLevel`。
- 视觉验收：同位置游戏截图 vs 地图截图并排；重点墙、嵌套箱子、玩家脸、空格背景、多重 ∞/ε。
- 交互回归：v2 验收清单；“关卡缩略图”开启/关闭。
- v3 first pass 已实施；实机视觉验收未执行（按用户要求不启动游戏）。
