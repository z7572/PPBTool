# PPBTool 方块特殊属性显示设计

日期：2026-10-22
状态：**待确认**（本文只定义设计，不实施）
基线：v4 核心已实现（`bcc4483` 之后）
参考：
- [Patrick's Parabox 官方自定义关卡文档](https://www.patricksparabox.com/custom-levels/)
- [Parabox-Editor](https://github.com/sudo-jade/Parabox-Editor/)

## 1. 目标

在层级浏览器里显示方块的特殊属性，重点是：

- 玩家 / 可占据 / Player Order / Flip / Counting / 主出口块 / clone / 无限进入 / 无限退出 / SpecialEffect；
- Player Order；
- 方块是否为主出口块（exit block）/ clone / 无限进入 / 无限退出；
- Counting 墙的 `numSolved / numRequired`；
- `specialeffect` 的数值和语义；
- 浮空（float in space）的显示；
- 关卡级的 Priority / Inner Push / Shed；
- SpecialEffect 8 在方块上按游戏原逻辑显示 Order / Shed / InnerPush 图标。

非目标：

- 不修改任何方块或关卡状态；
- 不把关卡级机制伪装成方块自身属性；
- 不恢复以前那种所有箱子满屏 `F/C/I/O` 徽标的做法；
- 不改变现有传送、撤销、导航和渲染行为。

## 2. 关键事实核对

### 2.1 Priority / Inner Push / Shed 是关卡级机制

官方自定义关卡文档里，它们是关卡头部字段：

```text
attempt_order push,enter,eat,possess
shed
inner_push
```

游戏加载后对应：

```csharp
World.showAttemptOrder
World.attemptOrder
World.shedEnabled
World.innerPushEnabled
```

`specialeffect = 8` 的方块只是**标记图标**：

```csharp
if (block.SpecialEffect == 8)
    DrawGlyphs(block);
```

`DrawGlyphs` 再根据 `World.showAttemptOrder / World.shedEnabled / World.innerPushEnabled` 决定画哪些图标。

因此：

- 不是“这个箱子有优先顺序/内推/挤出属性”；
- 而是“这个关卡启用了这些机制，这个箱子被作者标记为显示对应图标”。

### 2.2 `floatinspace` 是编辑器/加载期属性

官方文档说明：

> Float in space 让 block 的 `OuterLevel` 为 null，使它漂浮在虚空里；这是为了编辑器里保持一棵 GameObject 树的 hack。

运行时 `Block` **没有** `FloatInSpace` 布尔字段；只能根据加载后的 `OuterLevel` / `OuterLevelStart` / `SubLevel` 推断。根大厅的出口块也可能 `OuterLevel == null`，直接把它标成 floatinspace 会产生误报。

设计上必须先加诊断日志验证，再决定显示标签；第一版只显示“无外层 / root”，不武断断言 floatinspace。

### 2.3 `exitblock` 是 Ref 的标记

官方文档：

> `exitblock` - if 1, this is the exit block for this level.

`exitblock = false` 只表示该引用不是主出口块，不等于“箱子只能出不能进”。显示上应叫“Primary / Exit block”或“Clone / non-primary ref”，不要用 `exitblock` 推断移动规则。

### 2.4 `Locked` / `Weight` / `Shield` / `Rooted` 的审计

这些字段只存在于游戏程序集，不是官方自定义关卡能力：

| 字段 | 游戏内实际使用 | 官方关卡格式 | CustomHubs |
|---|---|---|---|
| `Locked` | 只有 `DrawBlockBorder` 读取，成立时边框变红；没有任何加载/移动代码写入 | 无 | 无引用 |
| `Weight` | 只有 `Block.ContainsWeight` 读取；该方法没有调用方；没有写入 | 无 | 无引用 |
| `Shield` | 只有 `Block.ContainsShield` 读取；该方法没有调用方；没有写入 | 无 | 无引用 |
| `Rooted*` | 只有字段声明；没有读取或写入 | 无 | 无引用 |

结论：

- 它们不是原版可玩关卡会出现的属性；
- 不是官方自定义关卡格式字段；
- CustomHubs 程序集也没有引用；
- 常驻值只能是默认 false，除非其它代码通过反射写入；
- **不要显示、不要用于推断行为、不要写进测试 fixture**。

`PlayerOrder`、`Possessable`、`FlipH`、`SpecialEffect` 则确实由官方格式加载，保留。

### 2.5 SpecialEffect 表

来自 Parabox-Editor `place/utils.py`：

| ID | 名称 | 说明 |
|---|---|---|
| 0 | None | 无 |
| 1 | Focus on this block | Challenge 38 聚焦 |
| 2 | Right flip effect | 右翻转光带 |
| 3 | Left flip effect | 左翻转光带 |
| 4 | Unused | 未使用 |
| 5 | Unused | 未使用 |
| 6 | Mark Hub Intro block | 大厅引导方块 |
| 7 | Unused | 未使用 |
| 8 | Draw glyphs for Inner Push / Shed / Priority | 绘制机制图标 |
| 9 | Leave sides open to void when floating | 浮空时开放边 |
| 10 | Mark Hub Intro ref | 大厅引导引用 |
| 11 | Don't show box in ASCII/grid display | TUI/网格模式隐藏 |
| 12 | Focus camera with multiple players | 多人镜头聚焦 |
| 13 | Disable glyph drawing | 性能 hack |

4/5/7 只做诊断/详情显示，不做地图徽标。

## 3. 属性分类

### 3.1 方块级

```text
玩家 / Player
可占据 / Possessable
Player Order
Flip
Counting: solved/required
Primary exit block / Clone / non-primary
Infinite Enter / Infinite Exit
SubLevel / OuterLevel null
SpecialEffect # + name
```

不显示以下字段：`Locked`、`Weight`、`Shield`、`Rooted*`。它们不符合原版关卡的能力模型，见 2.4。

### 3.2 关卡级

```text
Priority / Attempt Order
Inner Push
Shed
DrawStyle (可选展示)
```

关卡级属性只在当前层级顶部显示，不进入方块的属性行。

### 3.3 推断级

```text
Root / no outer level
Float in space (需诊断确认)
```

第一版把 `OuterLevel == null` 显示为 **“无外层 / root”**；只有在 floatinspace 测试关卡里确认可区分后，才加 **“漂浮 / float”**。

## 4. UI 设计

### 4.1 方块详情

右侧详情行只显示光标所指方块，追加紧凑属性：

```text
(3,6) 打开层级: Area Transfer
· Player · Possessable · Primary exit · Clone · Infinite Exit
· Counting 3/5 · SE8
```

- 空属性不显示；
- 只显示短标签，详细解释放 tooltip/README；
- 不显示 Priority/InnerPush/Shed（这些是关卡级）。

### 4.2 关卡机制条

只有关卡有非默认机制时才显示一条 22px 的机制栏：

```text
Priority: enter → eat → push → possess    Inner Push    Shed
```

- 默认 `push,enter,eat,possess` 且没有 shed/innerpush 时不显示，避免占用空间；
- `attempt_order` 只在 `showAttemptOrder` 为真时显示；
- 使用本地化动作名。

### 4.3 SpecialEffect 8 图标

按游戏 `DrawGlyphs` 复刻，仅在 `SpecialEffect == 8` 时：

- 图标来源：`Draw.Order` / `Draw.Shed` / `Draw.InnerPush`；
- 顺序：Order → Shed → InnerPush；
- 位置：方块左上角，纵向堆叠；
- 大小：0.75 格；
- 颜色：黑色 alpha 0.35；
- 只画关卡实际启用的项；
- 不是 SE8 的方块不画。

### 4.4 地图图标（SE8，必需）

游戏里会显示 SE8 的 Order / Shed / InnerPush 图标，所以地图也要显示；但**只画 SE8 的机制图标**，不把 Player/Clone/Count 等所有属性都做成地图徽标。

配置：

```ini
[UI]
ShowBlockTraits = true      ; 详情行
ShowTraitIcons = true       ; 地图 SE8 图标
LogBlockTraits = false      ; 诊断日志
```

## 5. 数据接口

```csharp
internal sealed class BlockTraits
{
    public Block Block;
    public bool IsRoot;
    public bool IsExitBlock;
    public bool IsClone;
    public bool IsInfEnter;
    public bool IsInfExit;
    public bool IsPlayer;
    public bool IsPossessable;
    public bool IsCounting;
    public int PlayerOrder;
    public int SpecialEffect;
    public int Solved;
    public int Required;

    public static BlockTraits From(Block block);
    public string Compact();
}

internal sealed class LevelTraits
{
    public IReadOnlyList<World.Attempt> AttemptOrder;
    public bool ShowAttemptOrder;
    public bool Shed;
    public bool InnerPush;

    public static LevelTraits Current();
    public bool HasNonDefaultMechanics { get; }
    public string Compact();
}

internal static class SpecialEffectCatalog
{
    public static string Name(int id);
    public static bool IsKnown(int id);
}
```

## 6. 验收

- SE8 + `attempt_order`：方块显示 Order 图标；
- SE8 + `shed`：显示 Shed 图标；
- SE8 + `inner_push`：显示 InnerPush 图标；
- 只有 `shed` 没有 SE8：不显示 Shed 图标，但机制栏显示 Shed；
- 普通方块不会因为关卡有 priority 而显示“优先”属性；
- `exitblock=false` 只显示为非主引用，不显示“只能出不能进”；
- 详情行、日志和测试 fixture 都不出现 `Locked` / `Weight` / `Shield` / `Rooted*`；
- floatinspace 样例：日志可区分，UI 不误标根大厅；
- 中英文、缩放、详情行截断正常；
- `dotnet build -c Release` 0/0。

## 7. 待确认

1. 只做详情 + 机制栏，还是也要 SE8 地图图标？
2. floatinspace 第一版接受“无外层/root”占位吗？
3. `exitblock` 是否需要单独显示，还是归入 Clone/Primary 即可？
