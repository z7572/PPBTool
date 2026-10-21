# PPBTool v3 — 真实方块渲染、∞/ε 符号与渲染管线替换

日期：2026-10-21
状态：**待审阅**（本轮只写规格，不实施 v3）
基线：v2 已提交（`76f4e23`），`dotnet build -c Release` 0 warning / 0 error
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
   - 多重无限渲染多个符号，位置/尺寸/透明度/换行与游戏一致。
5. 菜单“进入无限区”改为 **“进入∞/ε”**，条目使用 `ε #n` / `∞ #n`，不再使用“新建”字样。
6. 保留 v2 的交互：点击箱子进入层级、点击空格传送、树折叠/水平滚动/宽度调节、暂停 F8、Undo 兼容等。

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
  - `count > 1` 时**纵向堆叠**，尺寸缩小、间距按公式，最多可超出单个格子（绘制区域允许溢出）。
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

### 4.1 模块拆分

```text
src/GameArt/
  TextureBank.cs      反射缓存 Draw.instance 私有纹理（墙/大墙/渐变/眼睛/嘴/∞/ε/地砖/UI）
  ColorMath.cs        ComputeWallColor / ComputeBlockColor / CloneIntensity / FlipIntensity / HSV
  WallArt.cs          墙纹理、四象限、连接边框、计数墙、锁定/rooted
  BoxArt.cs           箱子底色/渐变/网格/边框 + 递归内容
  FaceArt.cs          玩家脸/眼睛/嘴巴/eye offset/翻转
  InfSymbolArt.cs     ∞/ε 符号（严格按 3.1 公式）
  FloorArt.cs         地砖（沿用 v2，去掉配色开关）
  RenderContext.cs    屏幕/单元格尺寸、透明度基准、裁剪、翻转、深度
```

- `MapRenderer` 只负责遍历可见单元格、调用 `GameArt`、处理目标/光标/玩家高亮等 UI 叠加。
- `TextureBank` 在首次打开地图时反射读取并缓存；字段缺失时退化为纯色/占位，不能崩溃。
- 所有颜色/透明度公式集中在 `ColorMath`/`InfSymbolArt`，便于对照 `_decomp` 审查。

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
6. 画 ∞/ε 符号（可溢出单元格，不被内容裁剪；符号层在内容之后）。

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
  - 允许符号超出单元格（与原版一致），但要在面板层级裁剪；不要被箱子内容裁剪。
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
- 传送门缩略图开关保留还是删除，待确认：
  - 若“直接使用游戏绘制”包括传送门缩略图，则默认开启并保留开关作为性能选项；
  - 或直接删除开关，始终绘制缩略图。

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
   - 单个符号 0.9 cell；多个符号按 `1.8×/堆叠/间距` 公式；
   - 透明度随 cell/screen 高度变化；多重无限符号数量正确；
   - 与游戏内同位置截图并排对照，布局/透明度可辨认一致。
5. **菜单**：标题为“进入∞/ε”；条目 `ε #n`/`∞ #n`；未生成项二次确认；不再出现“进入无限区”/“新建”。
6. **删除项**：无“关卡配色”按钮/配置；箱子下方无 `F/C/I/O...` 徽标；标题栏悬停仍显示属性文字。
7. v2 交互不回归：点击箱子进入、点击空格传送、树折叠/水平滚动/宽度调节、暂停 F8、Undo/Redo、F7 白化。
8. `dotnet build -c Release` 0 warning / 0 error；不启动游戏做实机验证（按用户要求）时，至少完成代码对照审查。

## 7. 风险与待确认

1. **“直接使用游戏绘制”的实现方式**：本规格选择“复用游戏纹理 + 复刻 `Draw` 公式”的 `GameArt` 模块，而不是直接调用游戏 `DrawBlocks` 队列（后者依赖全局绘制状态/队列，难以在 IMGUI 面板中安全复用）。如果你要求必须调用原函数，需要进一步设计离屏渲染管线。
2. **递归箱子内容**：原版 `DrawBlocks` 的嵌套变换较复杂；规格采用 `GoOutOneLevel` 等价变换 + 深度上限。极端 `iii`/InfZone/FilledWithWalls 关卡可能需要专门分支。
3. **传送门缩略图开关**：保留（默认开）还是删除，待确认。
4. **符号字体/纹理**：使用游戏纹理（非字体），因此不受 UI 字体缺字影响；但纹理字段是私有的，需反射，缺失时退化。
5. **性能**：深层箱子/超大关卡可能掉帧；深度/像素阈值是保护措施。

## 8. 预计改动文件（批准后才动手）

- 新增 `src/GameArt/`：`TextureBank`、`ColorMath`、`WallArt`、`BoxArt`、`FaceArt`、`InfSymbolArt`、`FloorArt`、`RenderContext`。
- `src/TeleportOverlay.cs`：地图渲染改为 `GameArt`；删除徽标/配色按钮；菜单改 `进入∞/ε`。
- `src/ModConfig.cs`：删除 `UseLevelPalette`；决定是否保留 `PortalThumbnails`。
- `src/UiText.cs`：`进入∞/ε`、`ε`/`∞` 文案。
- `README.md` / 本规格：说明 v3 视觉与限制。

## 9. 测试策略

- 构建：`dotnet build -c Release`，0 warning / 0 error。
- 代码对照：逐项对照 `_decomp/Draw.cs` 的 `DrawInfEffect`、`DrawWall`、`DrawBlock`、`DrawFace`、`DrawOrEnqueueFloor`、`GoOutOneLevel`。
- 视觉验收：同位置游戏截图 vs 地图截图并排；重点墙、嵌套箱子、玩家脸、多重 ∞/ε。
- 交互回归：v2 验收清单。
- 本轮不实施 v3；只写规格，等待批准后再排实施计划。
