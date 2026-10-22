# Block Trait / Rendering Inspection Log

日期：2026-10-22
类型：**观察记录**，不是设计规格，也不是实施计划
用途：保存实机/关卡文件证据，供 spec 和 plan 引用

相关文档：
- [设计规格](2026-10-22-ppbtool-block-traits-design.md)
- [实施计划](../plans/2026-10-22-ppbtool-block-traits.md)

## 1. 参考关卡

用户提供的 CustomHubs 自定义大厅：

```text
custom_levels/自定义大厅/Reborn's Hub v3/(Reborn's Hub v3) HUB/hub.txt
```

静态扫描：

- 约 180 个 `Ref`；
- 36 个 `floatinspace = 1`；
- 41 个 `exitblock = 1`；
- hub 自身的 Ref 只出现 `specialeffect 0` / `11`；
- hub 头部没有 `attempt_order` / `shed` / `inner_push`；
- 子关卡中有 `Ext0`（shed + SE8）、`Push_2`（inner_push + SE8）、`Ep_5`（attempt_order push,enter,eat）。

## 2. `Forgotten_levels... (7,1)` hidden overlap

`hub.txt` 原文：

```text
Ref 7 1 3 0 1 0 0 0 0 0 0 0 1 0 0 Forgotten_levels...
Ref 7 1 3 0 0 0 0 0 0 0 0 0 1 0 0 Forgotten_levels...
```

字段：

| 字段 | 第一条 | 第二条 |
|---|---|---|
| id | 3 | 3 |
| exitblock | 0 | 0 |
| infexit | **1** | 0 |
| floatinspace | **0** | **0** |
| specialeffect | 0 | 0 |

结论：

- 两条都是同一个 `(7,1)` 上的引用；
- 一条是无限退出引用，一条是普通引用；
- 两条都不是 floatinspace；
- `hidden overlap` 是引用重叠，不是浮空标记。

## 3. `floatinspace`

加载代码：

```csharp
if (floatinspace)
{
    Block voidOwner = CreateInfZone();
    block.Init(
        voidOwner.SubLevel.width / 2,
        voidOwner.SubLevel.height / 2,
        voidOwner.SubLevel,
        targetLevel);
}
```

结论：

- floatinspace 引用运行时进入一个隐藏 7x7 void；
- `OuterLevel` 是 void，不是 null；
- 与普通 void 引用结构相同；
- 运行时没有可靠字段恢复 `floatinspace`；
- 不显示，不做启发式。

## 4. `Locked` / `Weight` / `Shield` / `Rooted`

| 字段 | 游戏内使用 | 官方格式 | CustomHubs |
|---|---|---|---|
| Locked | 只有 DrawBlockBorder 读取；无加载写入 | 无 | 无引用 |
| Weight | 只有未被调用的 ContainsWeight | 无 | 无引用 |
| Shield | 只有未被调用的 ContainsShield | 无 | 无引用 |
| Rooted* | 只有声明 | 无 | 无引用 |

结论：死字段，不显示。

## 5. 玩家墙颜色（Ext3 / S0）

### Ext3 (1,1)

关卡原文：

```text
Ext3.txt
Wall 1 1 1 1 0
```

日志：

```text
BlockTraits (1,1)
root=False exit=False clone=False
player=True possessable=True
counting=0/0 se=0(无)
outer= outerStart=
```

这是 `Wall` 行创建的“玩家墙”，不是普通 `Block` 玩家：

- `player=1`；
- `possessable=1`；
- Wall 格式没有 HSV，`hue/sat/val` 为零；
- 当前代码对 `IsPlayer` 走 `GamePlayerColor`，零色时回退到玩家粉色 → 颜色错误。

### S0 (2,4)

```text
S0.txt
Wall 2 4 1 0 1
```

同样是 `Wall` 玩家（player=1，playerorder=1），没有自身 HSV。

### 结论

玩家墙与普通玩家的区分不能只看 `IsPlayer`：

- 普通 `Block` 玩家有自身 HSV；
- `Wall` 玩家 HSV 为零，应使用关卡墙色/owner 色；
- `IsPlayerStart` 两者都可能为 true，不能作为唯一判据。

建议规则：

```csharp
bool playerHasOwnColor = block.val > 0.0001f || block.sat > 0.0001f;
Color color = block.IsPlayer
    && !possessedWall
    && playerHasOwnColor
        ? GamePlayerColor(block, cell)
        : GameWallColor(colorSource, cell);
```

## 6. 黄色边框（Ext5 / C1）

### Ext5 (5,5)

```text
Ref 5 5 1 0 1 0 0 0 0 0 0 0 1 0 0
```

- id=1；
- exitblock=0；
- infexit=1；
- floatinspace=0；
- specialeffect=0。

### C1 (1,3)

```text
Ref 1 3 1 1 1 0 0 0 0 0 0 0 0 0 0
```

- id=1；
- exitblock=1；
- infexit=1；
- floatinspace=0。

按游戏 `DrawBlockBorder`：

```csharp
bool flag = World.LevelIsInfZone(block.OuterLevel)
    && !block.SubLevel.FilledWithWalls
    && block.SpecialEffect != 12;
if (block.IsSomeInfExitBlock)
    flag = true;
```

两者都应该有黄色闪烁边框。

当前 mod 未绘制，最可能：

- 这两个引用 `SubLevel == null`，走 `DrawWallArt`；
- 黄色逻辑只在 `DrawBoxBorder`，墙路径没有。

待日志确认：

- `SubLevel`；
- `IsSomeInfExitBlock`；
- `OuterLevel.InfZone`；
- `SpecialEffect`。

## 7. Ext5 (5,1) 错误玩家眼睛

```text
Ext5.txt
Ref 5 1 0 1 0 0 0 0 -1 0 0 0 0 0 0
```

当前 `DrawBoxArt` 的 phantom face 条件只检查：

```csharp
GameState.Player.OuterLevel == sub
&& GameState.Player.SubLevel == null
```

没有检查“这个箱子就是玩家”。如果 `sub` 正好是玩家所在层，任何指向该层的箱子都会画玩家脸。

方案：加 `block == GameState.Player`（或 `block.IsPlayer`）条件。

## 8. PlayerOrder 2 的嘴

游戏 `DrawFace` 对 order 2 使用白色横条：

```text
texture = FullWhiteTex
width  = width * 0.2666
height = height * 0.05
x      = 1.5 * eyeXOffset
y      = 0.17 + 1.5 * eyeYOffset
```

当前 mod：

```csharp
else if (block.PlayerOrder == 2)
{
    mouth = null;
}
```

所以序号 2 玩家没有嘴。需要按白色横条绘制。

## 9. SE8 图标位置与作用域

当前实现：

- `DrawBoxGlyphs` 只在 `DrawBoxArt`（父层箱子格）调用；
- 位置 `r.x + 0.05*cell`，大小 `0.75*cell`；
- 点进箱子内部后父层箱子不再绘制，图标消失。

确认目标：

- 图标位于箱子**内部第一格**的中心；
- 大小等于内部格子大小；
- 多个图标按游戏逻辑向下各占一格；
- 点进箱子后，用 `selectedNode.ParentBlock` 判断外层引用是否 SE8，在**当前内部层左上角第一格**继续画。

## 10. 无限进入/退出所属层与重数

需求：

```text
无限进入 #0 · Area Transfer
无限退出 #2 · Void 7x7
```

- 所属层：`block.SubLevel` 的标题；
- 进入重数：`block.SomeInfEnterNum`；
- 退出重数：`block.SubLevel.infExitBlocks.IndexOf(block)`；
- 用户确认使用**游戏索引**（0-based），不转换成第 1 重。

## 11. 已确认决策

- SE8 图标按游戏逻辑向下各占一格；
- 点进箱子后图标显示在内部层左上角第一格；
- 玩家墙零 HSV 时使用关卡墙色；
- 无限进入/退出显示游戏索引 `#0`；
- floatinspace 不显示；
- Locked / Weight / Shield / Rooted 不显示。

## 12. SE8 尺寸修正

用户反馈当前图标比游戏实际显示略大。

游戏 `DrawGlyphs`：

```csharp
double num3 = num * 0.75;   // 宽
double num4 = num2 * 0.75;  // 高
```

所以正确规则是：

- 位于内部第一格中心；
- 大小 = `内部格子 * 0.75`，不是整个格子；
- 多个图标之间间隔一个完整内部格子高度；
- 点进箱子后显示在内部层第一格时也用 `cell * 0.75`。

## 13. 无限进入/退出的所属层

用户例子：

```text
Intro 的二重无限退出 → Multi Infinite @ (1,1)
```

`hub.txt` 中相关行：

```text
Block 0 0 29 ...                    // Multi Infinite 层
Ref 1 1 1 0 1 1 ... Intro           // Intro 的无限退出，位于 Multi Infinite (1,1)
Ref 1 1 1 0 0 0 ... Intro           // 普通引用，同格重叠
```

这条 `Ref`：

- `id=1` → Intro 层；
- `infexit=1`；
- `infexitnum=1`；
- `SubLevel = Intro`；
- `OuterLevel = Multi Infinite`。

因此“谁的无限退出”不能只靠 `block.SubLevel` / `block.OuterLevel` 猜。正确做法是使用游戏加载后的 owner 列表：

- 无限退出：扫描 `World.levels`，找 `level.infExitBlocks.IndexOf(block) >= 0`；
  - owner = 该 `level`；
  - degree = 该索引。
- 无限进入：扫描 `World.levels`，找 `level.infEnterBlocks.IndexOf(block) >= 0`；
  - owner = 该 `level`（即 editor 的 `from id` 对应层）；
  - degree = 该索引。

这直接复用游戏 `LoadLevel` 的 `infEnterBlocks` / `infExitBlocks` 结构，不再把 `OuterLevel` / `SubLevel` 当 owner。`???` 只是某个真实层的名字，不能用作 fallback。

### 树标签

左侧层级节点要区分“入口引用”和“出口引用”：

```text
Multi Infinite  [refs ×2 · Infinite Exit #1 of Intro]
```

树标签**不要求固定文案或固定顺序**；只要求：

- 使用英文 `Infinite Enter/Exit`，不随中文 UI 切换；
- 保留现有的 `refs ×N`、`hidden ×N`、`void` 等标签；
- 删除 `Infinite Enter ×N` / `Infinite Exit ×N` 聚合计数，避免与具体 owner/degree 重复；
- 追加无限引用的 owner/degree；
- 多个标签用 ` · ` 分隔；
- 树已有水平滚动，放不下时可滚动查看；
- 详情行的格式仍固定为 `Infinite Exit #1 of Intro`。

规则：

- **Infinite Exit**：
  - 节点 = `block.OuterLevel`（出口块所在的目的地层）；
  - owner/degree = 扫描 `level.infExitBlocks` 找到的 owner 与索引；
  - 标签 `Infinite Exit #n of owner`；
  - 出口块可能不在 `LevelNode.References` 里（`References` 是“谁引用本层”），必须从 `World.blocks` 中扫描 `OuterLevel == level`。
- **Infinite Enter**：
  - 节点 = `block.SubLevel`（进入的目标层）；
  - owner/degree = 扫描 `level.infEnterBlocks` 找到的 owner 与索引（对应 `from id`）；
  - 标签 `Infinite Enter #n of owner`；
  - 可以扫描所有入向引用，但必须按 `owner + degree` 去重；Epsilon 的多个引用会折叠成一个 `Infinite Enter #0 of Intro`，不会再显示多个 `???`。
- 同层多个引用用 ` · ` 分隔；
- 使用游戏 0-based 索引；
- 使用英文 `Infinite Exit #1 of Intro` / `Infinite Enter #0 of X`，不随 UI 语言切换；
- 不显示 `∞` 字符。

### 详情

`BlockTraits` 使用同样的 owner/degree 规则：

- 光标在**出口块**上 → `Infinite Exit #1 of Intro`；
- 光标在**进入块**上 → `Infinite Enter #0 of X`；
- 标签固定英文。

若当前光标落在**同格重叠的普通引用**上，应提示该格还有隐藏引用，而不是伪造无限标签。引用选择器里也应显示 owner/degree。
