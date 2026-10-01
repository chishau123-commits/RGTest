# osu!(standard) 的 2D 玩法 / 画面合成机制，以及 BMS BGA 的对照

调研日期：本次会话。全部结论尽量落到 **一手来源**：osu! 官方 wiki（`ppy/osu-wiki` 仓库原文）、osu! 官方仓库 `ppy/osu` / `ppy/osu-framework` 源码、BMS command memo、bmson 规范。

标注约定：
- 【有据可查】= 有直接原文/源码可引。
- 【推断】= 由一手事实推导，未找到直接陈述。
- 【查不到】= 明确说明没有找到可靠来源。

## 取证说明（重要，影响可信度但不影响内容）

- 本机直连 `osu.ppy.sh` 与 `raw.githubusercontent.com` 会超时。本次 wiki 原文通过 `ppy/osu-wiki` 仓库的 `wiki/<页面>/en.md` 原始 Markdown 取得（该仓库就是 osu! wiki 的权威源，线上页面由它渲染），GitHub raw 走了镜像。因此下面的引用是 **wiki 的源文本**，不是线上渲染页；链接给出线上等价页面。
- 源码为 `master` 分支在本次取证的快照（非固定 commit），链接写 `master`。稳定版（osu!stable）与 osu!(lazer) 在个别细节上不同，下文会分别指明。
- 所有原始抓取已落盘：`research/_raw/wiki-raw/`（wiki 原文 Markdown）、`research/_raw/osu-src/`、`research/_raw/osu-framework/`、`research/_raw/bms/`。

---

## 1. 判定面与坐标系

### 1.1 playfield 是固定尺寸的 2D 坐标域：512×384，原点左上

【有据可查】osu! 的 playfield（判定面）是一个固定尺寸的 2D 坐标域，与窗口分辨率无关。单位叫 game pixel / osu!pixel，定义是「在 640×480 下 1 game pixel = 1 像素」。

> The playfield's coordinate system uses resolution-independent units called **game pixels**, or osu! pixels, such that a game pixel is equivalent to a pixel when osu! is running at a 640x480 resolution. On higher resolutions, the visual size of game pixels stays the same. The playfield is slightly shifted vertically, placed 8 game pixels lower than the window's centre.
>
> The [beatmap editor](/wiki/Client/Beatmap_editor)'s grid is 512x384 game pixels.
>
> | Playfield top left | Playfield bottom right | Playfield centre |
> | :-- | :-- | :-- |
> | (0, 0) | (512, 384) | (256, 192) |

来源：[Playfield — osu! wiki](https://osu.ppy.sh/wiki/en/Client/Playfield)（源文本 `wiki/Client/Playfield/en.md`）

【有据可查】osu!standard 的判定面尺寸在代码里就是这个常量：

```csharp
public static readonly Vector2 BASE_SIZE = new Vector2(512, 384);
```

来源：[`osu.Game.Rulesets.Osu/UI/OsuPlayfield.cs`（ppy/osu）](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/UI/OsuPlayfield.cs)

注意：wiki 在别处（storyboard 一页）把 playfield 写成「510 x 385」，那是过时的近似值；以 512×384 为准（见 §3.1）。

### 1.2 映射到任意窗口宽高比：等比缩放 + 居中，不拉伸

【有据可查】判定面自身保持 4:3，用 `FillMode.Fit`（等比）套进可用空间，绝不拉伸；外层按 512/640 = 0.8 的比例确定大小：

```csharp
private const float playfield_size_adjust = 0.8f;
...
// Calculated from osu!stable as 512 (default gamefield size) / 640 (default window size)
Size = new Vector2(playfield_size_adjust);

InternalChild = new Container
{
    Anchor = Anchor.Centre,
    Origin = Anchor.Centre,
    RelativeSizeAxes = Axes.Both,
    FillMode = FillMode.Fit,
    FillAspectRatio = 4f / 3,
    Child = content = new ScalingContainer { RelativeSizeAxes = Axes.Both }
};
```

并且它把自己对齐到「512 / 640」的经典比例，缩放系数恒为 1.6：

```csharp
// The following calculation results in a constant of 1.6 when OsuPlayfieldAdjustmentContainer
// is consuming the full game_size. This matches the osu-stable "magic ratio".
...
// Parent is a 4:3 aspect enforced, using height as the constricting dimension
// Parent!.ChildSize.X = min(game_size.X, game_size.Y * (4 / 3)) * playfield_size_adjust
// Parent!.ChildSize.X = 819.2
// Scale = 819.2 / 512
// Scale = 1.6
Scale = new Vector2(Parent!.ChildSize.X / OsuPlayfield.BASE_SIZE.X);
Position = new Vector2(0, (PlayfieldShift ? 8f : 0f) * Scale.X);
```

来源：[`OsuPlayfieldAdjustmentContainer.cs`（ppy/osu）](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/UI/OsuPlayfieldAdjustmentContainer.cs)

【有据可查】整个游戏区的外层容器用的是 `DrawSizePreservationStrategy.Minimum` + 目标尺寸 1024×768（4:3），含义是「等比缩放，保证一条轴等于目标、另一条轴总是更大」——也就是内容铺满窗口而不是留黑边：

> /// The strategy to be used for enforcing `TargetDrawSize`. The default strategy
> /// is Minimum, which preserves the aspect ratio of all children while ensuring one of the
> /// two axes matches `TargetDrawSize` while the other is always larger.
>
> public Vector2 TargetDrawSize = new Vector2(1024, 768);
>
> case DrawSizePreservationStrategy.Minimum:
>     content.Scale = new Vector2(Math.Min(drawSizeRatio.X, drawSizeRatio.Y));
>     break;

来源：[`osu.Framework/Graphics/Containers/DrawSizePreservingFillContainer.cs`](https://github.com/ppy/osu-framework/blob/master/osu.Framework/Graphics/Containers/DrawSizePreservingFillContainer.cs)

【有据可查】16:9 时**多出来的是横向的画面/坐标空间**（而不是把 playfield 拉宽，也不是加黑边）。storyboard 坐标系在同一比例尺下，16:9 的可视范围从 640×480 变成 854×480：

> | Aspect ratio | Screen top left | Screen bottom right | Screen centre | In-bounds dimensions |
> | :-- | :-- | :-- | :-- | :-- |
> | **4:3** | (0, 0) | (640, 480) | (320, 240) | 640x480 |
> | **16:9** | (-107, 0) | (747, 480) | (320, 240) | 854x480 |
>
> To convert a position in playfield coordinates to storyboard coordinates, add the offset vector (64, 56), which is the position of the playfield's top-left corner in storyboard coordinates.

来源：[Playfield — osu! wiki](https://osu.ppy.sh/wiki/en/Client/Playfield)

一个可以自查的算术：16:9 屏上 playfield 中心 (64+512/2)=320 与屏幕中心 ((-107)+747)/2=320 重合；上下方向 y 范围仍是 0–480。所以 16:9 时 playfield 左右各让出一块区域给画面/UI。

【有据可查】正因为 4:3 时没有余量，UI 会压到判定面上：

> On screen resolutions with a 4:3 aspect ratio, the playfield is partially covered with [interface](/wiki/Client/Interface) elements such as the leaderboard, key counter, or [replay](/wiki/Gameplay/Replay) controls.

来源：同上

【推断】「16:9 时是加黑边还是拉伸」的完整答案是：**两者都不是**。playfield 本身等比缩放、保持 4:3 形状，超出 4:3 的窗口区域由背景/storyboard 层覆盖（其坐标空间在同一比例尺下变宽到 854×480，见上），因此正常游戏时看不到黑边。这里「背景铺满非 playfield 区域」是推断，wiki 的 Playfield 页只明确写了 storyboard 坐标范围变宽。

### 1.3 音符坐标的范围与原点

【有据可查】原点在左上角，`.osu` 里 `x`/`y` 是 osu!pixels 位置：

> *Hit object syntax:* `x,y,time,type,hitSound,objectParams,hitSample`
>
> - **`x` (Integer)** and **`y` (Integer):** Position in [osu! pixels](/wiki/Client/Beatmap_editor/osu!_pixel) of the object.

来源：[osu! file formats — osu! wiki](https://osu.ppy.sh/wiki/en/Client/File_formats/osu_(file_format))

【有据可查】判定面坐标系原点在左上、y 向下（storyboard 页的表述，同一比例尺）：

> Coordinates are specified with positive values for `X` going to the **right**, positive values for `Y` going **down**, and the origin (0,0) being placed at the upper-left corner of the screen.

来源：[General rules for storyboarding — osu! wiki](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/General_Rules)

### 1.4 允许超出 playfield 吗

【有据可查】wiki 明确说可以（靠手改 `.osu`，或 stacking leniency 的自动偏移）；并且「在 4:3 下至少部分在屏幕外的游戏元素」会违反 ranking criteria：

> It is possible to place objects outside the playfield by editing the [`.osu` file](/wiki/Client/File_formats/osu_(file_format)) in a text editor, or by using automatic stacks triggered by [stacking leniency](/wiki/Beatmap/Stack_leniency). However, gameplay elements that are at least partially off-screen on screens 4:3 aspect ratio break the [ranking criteria](/wiki/Ranking_criteria).

来源：[Playfield — osu! wiki](https://osu.ppy.sh/wiki/en/Client/Playfield)

【有据可查】但 osu!(lazer) 的解码器会把 x 和 y **夹到 0–512**（注意 y 也被夹到 512，而不是 384——这是照抄 stable 行为的既成事实）：

```csharp
float x = Math.Clamp(Parsing.ParseFloat(split[0], Parsing.MAX_COORDINATE_VALUE), 0, 512);
float y = Math.Clamp(Parsing.ParseFloat(split[1], Parsing.MAX_COORDINATE_VALUE), 0, 512);

Vector2 pos = formatVersion >= LegacyBeatmapEncoder.FIRST_LAZER_VERSION
    ? new Vector2(x, y)
    : new Vector2((int)x, (int)y);
```

来源：[`ConvertHitObjectParser.cs`（ppy/osu）](https://github.com/ppy/osu/blob/master/osu.Game/Rulesets/Objects/Legacy/ConvertHitObjectParser.cs)

【有据可查】spinner 的位置无意义，默认取判定面中心 256,192：

> - `x` and `y` do not affect spinners. They default to the centre of the playfield, `256,192`.

来源：[osu! file formats — osu! wiki](https://osu.ppy.sh/wiki/en/Client/File_formats/osu_(file_format))

---

## 2. 打击物件与判定

### 2.1 判定的是「光标是否落在圆圈内」

【有据可查】hit circle 的命中区是一个**真正的圆**（不是方形包围盒）：接收器尺寸 = 128×128（即直径），圆角半径设为物件半径、指数设为 2（圆形超椭圆）：

```csharp
public HitReceptor()
{
    Size = OsuHitObject.OBJECT_DIMENSIONS;   // OBJECT_RADIUS * 2 = 128

    Anchor = Anchor.Centre;
    Origin = Anchor.Centre;

    CornerRadius = OsuHitObject.OBJECT_RADIUS;   // 64
    CornerExponent = 2;
}
```

按下时必须 `IsHovered` 才算命中：

```csharp
public bool OnPressed(KeyBindingPressEvent<OsuAction> e)
{
    if (!CanBeHit())
        return false;

    switch (e.Action)
    {
        case OsuAction.LeftButton:
        case OsuAction.RightButton:
            ...
            if (IsHovered)
            {
                Hit();
                HitAction ??= e.Action;
                return true;
            }
```

而 `IsHovered` 背后的包含测试确实是圆：

```csharp
public override bool Contains(Vector2 screenSpacePos)
{
    float cRadius = effectiveCornerRadius;
    float cExponent = CornerExponent;

    // Select a cheaper contains method when we don't need rounded edges.
    if (cRadius == 0.0f)
        return base.Contains(screenSpacePos);

    return DrawRectangle.Shrink(cRadius).DistanceExponentiated(ToLocalSpace(screenSpacePos), cExponent) <= Math.Pow(cRadius, cExponent);
}
```

来源：[`DrawableHitCircle.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Objects/Drawables/DrawableHitCircle.cs)、[`CompositeDrawable.cs`（osu.Framework）](https://github.com/ppy/osu-framework/blob/master/osu.Framework/Graphics/Containers/CompositeDrawable.cs)

【推断】因为 `Size` 是 128×128、`CornerRadius` 为 64 且指数为 2，`DrawRectangle.Shrink(64)` 退化成中心点，判别式化简为「光标到圆心距离 ≤ 64（物件局部单位）」，该局部空间还会乘上物件的 `Scale`。所以判定就是「光标与物件中心的距离 ≤ 该物件的显示半径」，没有额外容差。

### 2.2 圆半径与 CS 的换算

【有据可查】wiki 给的公式（单位 osu!pixels）：

> `r = (54.4 - 4.48 * CS) * 1.00041`
>
> Where `r` is the radius measured in [osu!pixels](/wiki/Client/Beatmap_editor/osu!_pixel), and `CS` is the circle size value.
>
> The `1.00041` multiplier is used to account for a bug in old replays causing incorrect radius calculations on widescreen displays.

来源：[Circle size — osu! wiki](https://osu.ppy.sh/wiki/en/Beatmap/Circle_size)

CS 在编辑器里只能选 2–7，但文件里可以写 0–10（同上页面）。

【有据可查】源码里等价的两段：

```csharp
public const float OBJECT_RADIUS = 64;
...
public double Radius => OBJECT_RADIUS * Scale;
```

```csharp
public static float CalculateScaleFromCircleSize(float circleSize, bool applyFudge = false)
{
    // The following comment is copied verbatim from osu-stable:
    //
    //   Builds of osu! up to 2013-05-04 had the gamefield being rounded down, which caused incorrect radius calculations
    //   in widescreen cases. This ratio adjusts to allow for old replays to work post-fix, which in turn increases the lenience
    //   for all plays, but by an amount so small it should only be effective in replays.
    ...
    const float broken_gamefield_rounding_allowance = 1.00041f;
    ...
    return (float)(1.0f - 0.7f * IBeatmapDifficultyInfo.DifficultyRange(circleSize)) / 2 * (applyFudge ? broken_gamefield_rounding_allowance : 1);
}
```

其中 `DifficultyRange` 把难度 [0,10] 映射到 [-1,1]：

```csharp
/// Maps a difficulty value [0, 10] to a linear range of [-1, 1].
static double DifficultyRange(double difficulty) => (difficulty - 5) / 5;
```

来源：[`OsuHitObject.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Objects/OsuHitObject.cs)、[`LegacyRulesetExtensions.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Rulesets/Objects/Legacy/LegacyRulesetExtensions.cs)、[`IBeatmapDifficultyInfo.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/IBeatmapDifficultyInfo.cs)

【有据可查】把两者对齐即可验证：`Radius = 64 · (1 − 0.7·(CS−5)/5)/2 · 1.00041 = (54.4 − 4.48·CS) · 1.00041`。取 CS=0/5/10 分别得 54.4 / 32 / 9.6，与 wiki 公式一致。

【有据可查】CS 的 mod 影响：Easy 减半，Hard Rock ×1.3 且上限 10；spinner 不受 CS 影响（[Circle size — osu! wiki](https://osu.ppy.sh/wiki/en/Beatmap/Circle_size)）。

### 2.3 判定档位与时间窗公式（与 OD 的关系）

【有据可查】osu! 的档位、命中值与**最大命中误差**（= 窗口半宽）：

> | Image | Name | [Hit value](/wiki/Gameplay/Score/ScoreV1/osu!) | [Accuracy](/wiki/Gameplay/Accuracy#osu!) | Max hit error (ms) |
> | :-: | :-: | --: | --: | :-- |
> | hit300 | GREAT | 300 | 100% | `80 - 6 × OD` |
> | hit100 | OK | 100 | 33.33% | `140 - 8 × OD` |
> | hit50 | MEH | 50 | 16.67% | `200 - 10 × OD` |
> | hit0 | MISS | 0 | 0% | `400` |
>
> A hit is then considered inside a hit window if `hit error < max hit error`, meaning the value listed is half of the hit window width.

来源：[osu! judgement system — osu! wiki](https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu!)

同一组公式在 OD 页以「Hit window (ms)」列出，并给了 round 的细节：

> | Score | Hit window (ms) |
> | --: | :-- |
> | 300 | `80 - 6 × OD` |
> | 100 | `140 - 8 × OD` |
> | 50 | `200 - 10 × OD` |
>
> Note that in the stable version of osu!, hit windows in osu! and osu!taiko can effectively be up to 0.5 ms shorter on both sides than what the formulas suggest ... This is because in osu! and osu!taiko, a hit is considered inside a hit window if `hit error < round(hit window)`, while in osu!mania it is considered inside if `hit error <= round(hit window)`.

来源：[Overall difficulty — osu! wiki](https://osu.ppy.sh/wiki/en/Beatmap/Overall_difficulty)

【有据可查】lazer 用 `DifficultyRange(80,50,20)` 这样的三段式（min/mid/max 对应 OD 0/5/10）实现，并且做了 `floor(x) − 0.5`：

```csharp
public static readonly DifficultyRange GREAT_WINDOW_RANGE = new DifficultyRange(80, 50, 20);
public static readonly DifficultyRange OK_WINDOW_RANGE = new DifficultyRange(140, 100, 60);
public static readonly DifficultyRange MEH_WINDOW_RANGE = new DifficultyRange(200, 150, 100);

/// osu! ruleset has a fixed miss window regardless of difficulty settings.
public const double MISS_WINDOW = 400;

public override void SetDifficulty(double difficulty)
{
    great = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(difficulty, GREAT_WINDOW_RANGE)) - 0.5;
    ok = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(difficulty, OK_WINDOW_RANGE)) - 0.5;
    meh = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(difficulty, MEH_WINDOW_RANGE)) - 0.5;
}
```

来源：[`OsuHitWindows.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Scoring/OsuHitWindows.cs)

【有据可查】OD 还决定 spinner 的转速要求：

> |  |  |
> | :-- | :-- |
> | Minimum spins per second | `1.5 + 0.2 × OD` if OD < 5, `1.25 + 0.25 × OD` if OD ≥ 5 |
> | Minimum spins required | Spinner length in seconds × minimum spins per second + 0.5 |
>
> If a spinner is very short, the number of spins required may be calculated to be 0, and thus the spinner will always complete itself with a GREAT.

来源：[osu! judgement system — osu! wiki](https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu!)

【有据可查】OD 的 mod 影响：Easy 减半；Hard Rock ×1.4（上限 10）；DT/HT 不改 OD 值但缩/放窗口 33%（[Overall difficulty — osu! wiki](https://osu.ppy.sh/wiki/en/Beatmap/Overall_difficulty)）。

### 2.4 点击判定：按下瞬间 vs 按住

【有据可查】hit circle 是**按下瞬间**判定，判定挂在 `OnPressed` 上（见 §2.1 的代码）；松开不做任何事：

```csharp
public void OnReleased(KeyBindingReleaseEvent<OsuAction> e)
{
}
```

来源：[`DrawableHitCircle.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Objects/Drawables/DrawableHitCircle.cs)

【有据可查】slider 是**按住跟随**：

> Once the [approach circle](/wiki/Gameplay/Hit_object/Approach_circle) reaches the sliderhead's border, like with [hit circles](/wiki/Gameplay/Hit_object/Hit_circle), the player must click on/tap the beginning of the slider and then, keeping the button pressed, follow a moving ball (called a slider ball) along the track until the slider tail is reached.

来源：[Slider — osu! wiki](https://osu.ppy.sh/wiki/en/Gameplay/Hit_object/Slider)

【有据可查】lazer 里「是否在按住」是一个显式的 `Tracking` 状态，且要求有合法的按住动作：

```csharp
Tracking =
    ... // we don't want to potentially update from Tracking=true to Tracking=false at this point.
    && isValidTrackingPosition
    ...
    && validTrackingAction;
```

前一声明的注释是：

```csharp
/// <summary>
/// Whether the mouse is currently in the follow area.
/// </summary>
```

来源：[`SliderInputManager.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Objects/Drawables/SliderInputManager.cs)

### 2.5 notelock（note lock）

【有据可查】定义是两个条件同时成立：

> **Notelock**, or **note lock**, is an informal term for a gameplay mechanic of [osu!](/wiki/Game_mode/osu!) which may prevent a player from clearing a hit object. It happens if **two** conditions are met at the same time:
>
> 1. The [timing windows](/wiki/Beatmap/Overall_difficulty#timing) of two hit objects overlap.
> 2. The first object of those two has not been judged yet (hit or missed).
>
> In this case, the second object is said to be *locked* behind the first one, which makes osu! ignore the player's input on it until the first object's hit window has passed.
>
> When notelock occurs, the clicked hit circle will shake. This does not happen for sliders and spinners.
>
> Notelock is a part of osu!'s timing system and happens when the timing windows of two objects overlap. It occurs more often on beatmaps with low [OD](/wiki/Beatmap/Overall_difficulty) or high [BPM](/wiki/Music_theory/Tempo) values, because timing windows may overlap more frequently.

来源：[Notelock — osu! wiki](https://osu.ppy.sh/wiki/en/Gameplay/Judgement/Notelock)

【有据可查】lazer 放宽了规则：

> When compared to osu!(stable), notelock was made more lenient in osu!(lazer). This was mitigated by making the timing window significantly more forgiving: once the first hit object reaches an offset of 0 ms, the next object is no longer locked and can be clicked immediately. In short, late hits from a previously missed object will no longer lock the note right afterwards.

来源：同上

【有据可查】lazer 的实现入口是一个返回三态决策的回调（`Ignore` / `Shake` / `Hit`）：

```csharp
/// An action that an <see cref="IHitPolicy"/> recommends be taken in response to a click
/// on a <see cref="DrawableOsuHitObject"/>.
public enum ClickAction
{
    Ignore,
    Shake,
    Hit
}
```

```csharp
/// <summary>
/// Shake the hit object in case it was clicked far too early or late (aka "note lock").
/// </summary>
public virtual void Shake() { }
```

```csharp
var result = ResultFor(timeOffset);
var clickAction = CheckHittable?.Invoke(this, Time.Current, result);

if (clickAction == ClickAction.Shake)
    Shake();

if (result == HitResult.None || clickAction != ClickAction.Hit)
    return;
```

来源：[`ClickAction.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/UI/ClickAction.cs)、[`DrawableOsuHitObject.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Objects/Drawables/DrawableOsuHitObject.cs)、[`DrawableHitCircle.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Objects/Drawables/DrawableHitCircle.cs)

还有一点与「按下瞬间」相关的明文规则：

> Hitting a circle before the MISS window has no effect (other than causing [notelock](/wiki/Gameplay/Judgement/Notelock)), and not hitting a circle will cause a MISS after the MEH window passes.

来源：[osu! judgement system — osu! wiki](https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu!)

### 2.6 slider 的跟随判定与 tick 计算

**跟随区半径**——【有据可查】跟随圈（扩展态）是物件半径的 2.4 倍；非扩展态就是物件半径本身：

```csharp
public const float FOLLOW_AREA = 2.4f;
```

```csharp
private float getFollowRadius(bool expanded)
{
    float radius = (float)slider.HitObject.Radius;

    if (expanded)
        radius *= DrawableSliderBall.FOLLOW_AREA;

    return radius;
}

public bool IsMouseInFollowArea(bool expanded)
{
    ...
    float radius = getFollowRadius(expanded);

    double followProgress = Math.Clamp((Time.Current - slider.HitObject.StartTime) / slider.HitObject.Duration, 0, 1);
    Vector2 followCirclePosition = slider.HitObject.CurvePositionAt(followProgress);
    Vector2 mousePositionInSlider = slider.ToLocalSpace(pos) - slider.OriginPosition;

    return (mousePositionInSlider - followCirclePosition).LengthSquared <= radius * radius;
}
```

来源：[`DrawableSliderBall.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Objects/Drawables/DrawableSliderBall.cs)、[`SliderInputManager.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Objects/Drawables/SliderInputManager.cs)

【有据可查】「扩展态」与「非扩展态」的用法差别被源码注释说明：头部按晚了时，如果光标始终在扩展跟随区内，就把已经滑过的嵌套物件全部补判为命中；一旦中途离开过扩展区，就反过来全部判 miss：

```csharp
// When the head is hit late:
// - If the cursor has at all times been within range of the expanded follow area, hit all nested objects that have been passed through.
// - If the cursor has at some point left the expanded follow area, miss those nested objects instead.
...
// If all ticks were hit so far, enable tracking the full extent.
// If any ticks were missed, assume tracking would've broken at some point, and should only activate if the cursor is within the slider ball.
updateTracking(allTicksInRange || IsMouseInFollowArea(false));
```

来源：[`SliderInputManager.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Objects/Drawables/SliderInputManager.cs)

**tick 间距**——【有据可查】：

```csharp
internal const float BASE_SCORING_DISTANCE = 100;
```

```csharp
Velocity = BASE_SCORING_DISTANCE * difficulty.SliderMultiplier / LegacyRulesetExtensions.GetPrecisionAdjustedBeatLength(this, timingPoint, OsuRuleset.SHORT_NAME);
// WARNING: this is intentionally not computed as `BASE_SCORING_DISTANCE * difficulty.SliderMultiplier`
double scoringDistance = Velocity * timingPoint.BeatLength;
TickDistance = GenerateTicks ? (scoringDistance / difficulty.SliderTickRate * TickDistanceMultiplier) : double.PositiveInfinity;
...
var sliderEvents = SliderEventGenerator.Generate(StartTime, SpanDuration, Velocity, TickDistance, Path.Distance, this.SpanCount(), cancellationToken);
```

来源：[`Slider.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Objects/Slider.cs)、[`SliderEventGenerator.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Rulesets/Objects/SliderEventGenerator.cs)

（`Velocity` 的定义：`BASE_SCORING_DISTANCE` 是「速度调整后 1 秒的节拍长度下的评分距离」，即 slider 球每秒走过的路径长度。）

【有据可查】wiki 对 slider velocity 的单位说明：

> Slider velocity is measured as *hundreds of [osupixels](/wiki/Client/Beatmap_editor/osu!_pixel) per beat*, so a slider with a velocity of 1.00 will travel 100 osupixels (or, 100 pixels at 640x480 resolution) in one full beat.

来源：[Slider velocity — osu! wiki](https://osu.ppy.sh/wiki/en/Gameplay/Hit_object/Slider/Slider_velocity)

【有据可查】tick rate 的语义与不生成 tick 的情形：

> For example, slider tick rate 1 means that slider ticks appears once per beat. However, as slider ticks do not appear in [sliderheads](/wiki/Gameplay/Hit_object/Slider/Sliderhead) and [sliderends](/wiki/Gameplay/Hit_object/Slider/Slidertail), some sliders do not contain any slider ticks. For example, a 1-beat slider does not contain any slider ticks with the slider tick rate set to 1.

来源：[Slider tick rate — osu! wiki](https://osu.ppy.sh/wiki/en/Beatmapping/Slider_tick_rate)

**slider 整体判定**——【有据可查】：

> | Judgement | Slider completion |
> | :-: | :-- |
> | GREAT | 100% |
> | OK | 50% |
> | MEH | At least one slider part |
> | MISS | 0% |

来源：[osu! judgement system — osu! wiki](https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu!)

lazer 的经典模式下实现为命中嵌套物件的比例：

```csharp
int totalTicks = hitObject.NestedHitObjects.Count;
int hitTicks = hitObject.NestedHitObjects.Count(h => h.IsHit);

if (hitTicks == totalTicks)
    r.Type = HitResult.Great;
else if (hitTicks == 0)
    r.Type = HitResult.Miss;
else
{
    double hitFraction = (double)hitTicks / totalTicks;
    r.Type = hitFraction >= 0.5 ? HitResult.Ok : HitResult.Meh;
}
```

来源：[`DrawableSlider.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Objects/Drawables/DrawableSlider.cs)

【有据可查】slider 的「宽松」与不判 miss 的细节：

> In [osu!](/wiki/Game_mode/osu!), [sliders](/wiki/Gameplay/Hit_object/Slider) will reward a 300 as long as they are hit within the 50's hit window. This is sometimes called slider leniency and is removed in [ScoreV2](/wiki/Gameplay/Game_modifier/ScoreV2).

来源：[Overall difficulty — osu! wiki](https://osu.ppy.sh/wiki/en/Beatmap/Overall_difficulty)

> - Tapping the slider head too early (before the MEH hit window), missing a slider tick, or missing a repeat does not incur a MISS, but will cause a [combo break](/wiki/Gameplay/Judgement/Combobreak). The other slider parts can still be hit if a key is held down. This is colloquially referred to as a [slider break](/wiki/Gameplay/Judgement/Slider_break).
> - Missing the slider end does not incur a MISS, but will not increment combo.

来源：[osu! judgement system — osu! wiki](https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu!)

【有据可查】tick 的采集条件：

> During a play, slider ticks are collected by keeping the cursor within said slider's follow circle, that, once collected, will increase the combo by one unit per slider tick collected. If a player fails to collect all the slider ticks in a slider, they will receive a `100` and break their combo.

来源：[Slider tick — osu! wiki](https://osu.ppy.sh/wiki/en/Gameplay/Hit_object/Slider/Slider_tick)

---

## 3. 视觉与合成

### 3.1 approach circle 的机制

【有据可查】approach circle 的初始与终了缩放、透明度，以及它随时间线性收缩到与 hit circle 重合：

```csharp
public SkinnableDrawable ApproachCircle { get; private set; } = null!;
...
ApproachCircle = new ProxyableSkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.ApproachCircle), _ => new DefaultApproachCircle())
{
    Anchor = Anchor.Centre,
    Origin = Anchor.Centre,
    RelativeSizeAxes = Axes.Both,
    Alpha = 0,
    Scale = new Vector2(4),
}
```

```csharp
protected override void UpdateInitialTransforms()
{
    base.UpdateInitialTransforms();

    CirclePiece.FadeInFromZero(HitObject.TimeFadeIn);

    ApproachCircle.FadeTo(0.9f, Math.Min(HitObject.TimeFadeIn * 2, HitObject.TimePreempt));
    ApproachCircle.ScaleTo(1f, HitObject.TimePreempt);
    ApproachCircle.Expire(true);
}

protected override void UpdateStartTimeStateTransforms()
{
    base.UpdateStartTimeStateTransforms();

    // always fade out at the circle's start time (to match user expectations).
    ApproachCircle.FadeOut(50);
}
```

来源：[`DrawableHitCircle.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Objects/Drawables/DrawableHitCircle.cs)

【有据可查】缩放的定标方式是「approach circle 的贴图被限制到 2× 物件尺寸」，且默认皮肤额外乘了一个补偿系数，使它在碰到 hit circle 时才消失：

```csharp
Texture = textures.Get(@"Gameplay/osu/approachcircle").WithMaximumSize(OsuHitObject.OBJECT_DIMENSIONS * 2);

// In triangles and argon skins, we expanded hitcircles to take up the full 128 px which are clickable,
// but still use the old approach circle sprite. To make it feel correct (ie. disappear as it collides
// with the hitcircle, *not when it overlaps the border*) we need to expand it slightly.
Scale = new Vector2(128 / 118f);
```

来源：[`DefaultApproachCircle.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Skinning/Default/DefaultApproachCircle.cs)

**时间参数与 AR 的关系**——【有据可查】源码常量与公式：

```csharp
/// Minimum preempt time at AR=10.
public const double PREEMPT_MIN = 450;

/// Median preempt time at AR=5.
public const double PREEMPT_MID = 1200;

/// Maximum preempt time at AR=0.
public const double PREEMPT_MAX = 1800;

public static readonly DifficultyRange PREEMPT_RANGE = new DifficultyRange(PREEMPT_MAX, PREEMPT_MID, PREEMPT_MIN);
...
TimePreempt = IBeatmapDifficultyInfo.DifficultyRangeInt(difficulty.ApproachRate, PREEMPT_RANGE);
...
TimeFadeIn = 400 * Math.Min(1, TimePreempt / PREEMPT_MIN);
```

来源：[`OsuHitObject.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Objects/OsuHitObject.cs)

【有据可查】wiki 给出同样的 preempt 分段公式与「出现时机 / 完全显形时机」：

> The duration of a hit object that stays visible on the screen (without mods) ranges from 1800ms at AR0 to 450ms at AR10. AR levels scale by 120ms for below AR5 and 150ms for above AR5.
>
> The hit object starts fading in at `X - preempt` with:
>
> - AR < 5: `preempt = 1200ms + 120ms * (5 - AR)`
> - AR = 5: `preempt = 1200ms`
> - AR > 5: `preempt = 1200ms - 150ms * (AR - 5)`
>
> Hit object is at final (full) opacity at 2/3 of preempt time.

来源：[Approach rate — osu! wiki](https://osu.ppy.sh/wiki/en/Beatmap/Approach_rate)

【有据可查】approach circle 的颜色来自 combo colour：

> *Approach circles* are coloured circles which shrink around [hit circles](/wiki/Gameplay/Hit_object/Hit_circle) in [osu!](/wiki/Game_mode/osu!) [beatmaps]. They help players determine when to click to earn the biggest amount of [score](/wiki/Gameplay/Score) by overlapping with the hit circle at the correct moment in a song. The time it takes for an approach circle to reach a hit circle is determined by a beatmap's [approach rate](/wiki/Beatmap/Approach_rate) which is set by the mapper. The colour of approach circles is also determined by the [combo colours](/wiki/Beatmapping/Combo_colour) used by the mapper of a beatmap.

来源：[Approach circle — osu! wiki](https://osu.ppy.sh/wiki/en/Gameplay/Hit_object/Approach_circle)

【推断】wiki 说「完全显形在 preempt 的 2/3 处」，而 lazer 代码用的是 `TimeFadeIn = 400 × min(1, TimePreempt/450)`（AR≥5 时恒为 400ms）。以 AR5（preempt 1200）为例，2/3·preempt = 800ms ≠ 400ms。两处口径不一致，本次未找到能判定哪一方是当前 stable 权威行为的来源，故并列陈述，不做调和。

### 3.2 背景（图片/视频）的层序

【有据可查】beatmap 的背景图被放在**所有其它层之下**：

> By default, the preview background (the background visible in [song select](/wiki/Client/Interface#song-select)) specified for the beatmap is placed below all other layers. However, if that same file is referenced as an object in the storyboard, it will disappear immediately after the beatmap loads.

来源：[General rules for storyboarding — osu! wiki](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/General_Rules)

【有据可查】storyboard 的五个层，按优先级递增：

> These are the five storyboard layers, in increasing order of priority:
>
> - Background
> - Fail (only displayed if the player is in the "Fail state", see [Game State](#game-state) below)
> - Pass (only displayed if the player is in the "Pass state", see [Game State](#game-state) below)
> - Foreground
> - Overlay (displayed above hit objects, use with caution)
>
> Note that the "Fail" and "Pass" layers are never on-screen simultaneously, unlike in the design tab.

来源：同上

【有据可查】层的叠放规则：

> - Objects that overlap in **different** layers will be drawn in the order described above (e.g., any object in the Foreground layer will always be visible in front of any object in the Background, Fail, or Pass layers).
> - Objects that overlap in the **same** layer will be drawn in the order in which they are specified (e.g., if object 1 is specified first in the .osb or .osu file, and then object 2 is as well, but they are both in the same layer, object 2 will appear in front of object 1).

来源：同上

【有据可查】源码里有**第六个**内部层 `Video`，且 depths 明确（数值越大画得越靠后）：

```csharp
public Storyboard()
{
    layers.Add("Video", new StoryboardVideoLayer("Video", 4, false));
    layers.Add("Background", new StoryboardLayer("Background", 3));
    layers.Add("Fail", new StoryboardLayer("Fail", 2) { VisibleWhenPassing = false, });
    layers.Add("Pass", new StoryboardLayer("Pass", 1) { VisibleWhenFailing = false, });
    layers.Add("Foreground", new StoryboardLayer("Foreground", minimumLayerDepth = 0));

    layers.Add("Overlay", new StoryboardLayer("Overlay", int.MinValue));
}
```

```csharp
public StoryboardVideo? PrimaryVideo => GetLayer(@"Video").Elements.OfType<StoryboardVideo>().FirstOrDefault();
```

来源：[`Storyboard.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Storyboard.cs)

【有据可查】`Depth` 的语义（数值大 = 更靠后）：

> /// Controls which Drawables are behind or in front of other Drawables.
> /// This amounts to sorting Drawables by their `Depth`.
> /// A Drawable with higher `Depth` than another Drawable is
> /// drawn behind the other Drawable.

来源：[`Drawable.cs`（osu.Framework）](https://github.com/ppy/osu-framework/blob/master/osu.Framework/Graphics/Drawable.cs)

【有据可查】`Video` 层是解析 `.osu`/`.osb` 的 `Video` 事件时创建的：

```csharp
case LegacyEventType.Video:
{
    int offset = Parsing.ParseInt(split[1]);
    string path = CleanFilename(split[2]);
    ...
    storyboard.GetLayer("Video").Add(storyboardSprite = new StoryboardVideo(source, path, offset));
    break;
}
```

来源：[`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs)

【有据可查】旧的层级枚举里 `Video = 5` 排在最后：

```csharp
internal enum LegacyStoryLayer
{
    Background = 0,
    Fail = 1,
    Pass = 2,
    Foreground = 3,
    Overlay = 4,
    Video = 5
}
```

来源：[`LegacyStoryLayer.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Legacy/LegacyStoryLayer.cs)

【推断】把上面几条合起来，自下而上的绘制顺序是：
**beatmap 背景图 → Video → Background → (Fail | Pass) → Foreground → 打击物件 → Overlay**。
依据：(a) 背景图「placed below all other layers」；(b) `Depth` 数值大者在后，而 Video=4 > Background=3 > Fail=2 > Pass=1 > Foreground=0 > Overlay=int.MinValue；(c) 「All storyboard sprites are placed below the hit objects except Overlay」。
即：**视频是紧贴在静态背景图之上、storyboard 的 Background 层之下的一层**。这一点 wiki 没有用文字直接列出（wiki 的「五个层」不含 Video），所以标为推断——但每一步前提都有出处。

### 3.3 Storyboard 能否画在背景之上、playfield 之下

【有据可查】可以。除了 Overlay 之外，所有 storyboard sprite 都在打击物件之下：

> All storyboard sprites are placed below the [hit objects](/wiki/Gameplay/Hit_object) except Overlay, which is still placed below the skin. So, even the "highest" (Overlay) layer in the storyboard will always be above hit objects, but behind the HP bar, the cursor, etc.

来源：[General rules for storyboarding — osu! wiki](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/General_Rules)

【有据可查】lazer 里 Overlay 层被单独做代理、插到 playfield 之前（说明它确实在判定面之上，而其余层不在）：

```csharp
protected override bool ShowDimContent => IgnoreUserSettings.Value || (ShowStoryboard.Value && (DimLevel < 1 || storyboardMustAlwaysBePresent.Value));

...
private void onStoryboardCreated(DrawableStoryboard storyboard)
{
    Add(storyboard);
    OverlayLayerContainer.Add(storyboard.OverlayLayer.CreateProxy());
}
```

并且注解里点名了理由：

> /// cases where the storyboard has an overlay layer sprite, as it should continue to display fully dimmed
> /// <i>in front of</i> the playfield (https://github.com/ppy/osu/issues/29867),

来源：[`DimmableStoryboard.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Screens/Play/DimmableStoryboard.cs)

### 3.4 Background Dim 默认值

【有据可查】默认 0.7（范围 0–1，步长 0.01）；另有「break 期间变亮」默认开启：

```csharp
SetDefault(OsuSetting.DimLevel, 0.7, 0, 1, 0.01);
...
SetDefault(OsuSetting.LightenDuringBreaks, true);
...
SetDefault(OsuSetting.ScalingBackgroundDim, 0.9f, 0.5f, 1f, 0.01f);
```

来源：[`OsuConfigManager.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Configuration/OsuConfigManager.cs)

【有据可查】dim 的作用对象与 break 行为：

> | `Background dim` | Darken the playfield (including storyboards and/or background videos). | During breaks, the dim is decreased by 30% (max 0%) (this behaviour can be disabled in the options). *Note: Background dim changes are saved per beatmap but will be lost after closing osu!.* |

来源：[Visual settings — osu! wiki](https://osu.ppy.sh/wiki/en/Client/Interface/Visual_settings)

### 3.5 playfield 有没有边框 / 遮罩

【有据可查】有一个可选的判定面边框，样式三选一，**默认是 None**：

```csharp
public enum PlayfieldBorderStyle
{
    None,
    Corners,
    Full
}
```

```csharp
SetDefault(OsuRulesetSetting.PlayfieldBorderStyle, PlayfieldBorderStyle.None);
```

来源：[`PlayfieldBorderStyle.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Rulesets/UI/PlayfieldBorderStyle.cs)、[`OsuRulesetConfigManager.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/Configuration/OsuRulesetConfigManager.cs)

【有据可查】osu!standard 的判定面**不做裁剪/遮罩**，理由写在注释里：

```csharp
// For osu! gameplay, everything is always on screen.
// Skipping masking calculations improves performance in intense beatmaps (ie. https://osu.ppy.sh/beatmapsets/150945#osu/372245)
public override bool UpdateSubTreeMasking() => false;
```

来源：[`OsuPlayfield.cs`](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Osu/UI/OsuPlayfield.cs)

### 3.6 视频是不是「可选的背景层」

【有据可查】是。`.osu` 里 `Video` 事件与 `Background` 事件并列、语法同构，且可省：

> ### Videos
>
> *Video syntax:* `Video,startTime,filename,xOffset,yOffset`
>
> `Video` may be replaced by `1`.
>
> - **`filename` (String)**, **`xOffset` (Integer)**, and **`yOffset` (Integer)** behave exactly as in backgrounds.

（Background 语法为 `0,0,filename,xOffset,yOffset`。）

来源：[osu! file formats — osu! wiki](https://osu.ppy.sh/wiki/en/Client/File_formats/osu_(file_format))

【有据可查】客户端把「storyboard」和「video」当作两个独立的开关：

> | `Disable storyboard` | Remove all storyboard elements. This does not affect [Kiai Time](/wiki/Gameplay/Kiai_time) and the background video, if any. | ... |
> | `Disable video` | Do not play the background video. This does not remove the storyboard. | This requires a retry if activated after the gameplay begins. This option is disabled if there is no background video to play. |

来源：[Visual settings — osu! wiki](https://osu.ppy.sh/wiki/en/Client/Interface/Visual_settings)

【有据可查】曾经还有一个专门关视频的 mod，后被设置项取代：

> The **No Video** mod was a [game modifier](/wiki/Gameplay/Game_modifier) that enabled players to disable the background video of a [beatmap](/wiki/Beatmap). This was used to help improve framerate and game performance.
>
> The mod's functionality was replaced with an option in [Visual Settings](/wiki/Client/Interface/Visual_settings), and thus the icon and its mod are no longer being used.

来源：[No Video (mod) — osu! wiki](https://osu.ppy.sh/wiki/en/Gameplay/Game_modifier/No_Video)

【有据可查】而**背景图是强制的，视频不是**：

> - **You must have a background image on every difficulty of your beatmap.** Different background files for different difficulties is acceptable.

来源：[Ranking criteria § Video and background — osu! wiki](https://osu.ppy.sh/wiki/en/Ranking_criteria)

【有据可查】`.osu` 里还有 `WidescreenStoryboard`（默认 0）与 `EpilepsyWarning`（默认 0）两个相关开关：

> | `EpilepsyWarning` | 0 or 1 | Whether or not a warning about flashing colours should be shown at the beginning of the map | 0 |
> | `WidescreenStoryboard` | 0 or 1 | Whether or not the storyboard allows widescreen viewing | 0 |

来源：[osu! file formats — osu! wiki](https://osu.ppy.sh/wiki/en/Client/File_formats/osu_(file_format))

【有据可查】lazer 里「storyboard 只由 video 元素构成」时会被当成 16:9 处理：

```csharp
Size = new Vector2(640, 480);

bool onlyHasVideoElements = Storyboard.Layers.SelectMany(l => l.Elements).All(e => e is StoryboardVideo);

Width = Height * (storyboard.Beatmap.WidescreenStoryboard || onlyHasVideoElements ? 16 / 9f : 4 / 3f);
```

来源：[`DrawableStoryboard.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/DrawableStoryboard.cs)

---

## 4. 视频背景的现实约束

### 4.1 官方（ranking criteria）对视频的硬性要求

【有据可查】以下四条是成文规则（视频尺寸上限、编码、offset 一致性、必须去掉音轨）：

> - **A video's dimensions must not exceed a width of 1280 and a height of 720 pixels.** Additionally, upscaling lower resolution video to a higher resolution should be avoided. This ensures video files do not become excessively large or resource intensive.
> - **A video must be encoded in H.264.**
> - **A video's offset must be correct if it synchronizes with the song.** An incorrect offset can result in a misleading visual representation of the song. If the same video appears in multiple difficulties, it must always have the same offset(s).
> - **A video's audio track must be removed from the video file.** The audio track in a video is not used in osu!, and removing it reduces the file size of the beatmap. This includes videos with muted audio tracks.

【有据可查】背景图另有尺寸与体积上限：

> - **The following are requirements for background images:**
>   ...
>   - **Maximum file size:** 2.5MB

【有据可查】ranking criteria **没有**给视频规定码率；码率要求只针对音频：

> - **...have an average bit rate no greater than 192 kbps for MP3 files, or 208 kbps for Ogg Vorbis files.**
> - **...have an average bit rate no lower than 128 kbps**, if such a source exists. Otherwise, use the highest quality available.

以上全部来源：[Ranking criteria § Video and background / § Audio — osu! wiki](https://osu.ppy.sh/wiki/en/Ranking_criteria)

【有据可查】wiki 的辅助指南补上了「支持哪些容器/编码」：

> **osu! supports video encoded in the H.264 format with the `.mp4` file extension.** Other formats, such as H.265, VP9, and AV1, and file extensions such as `.mkv` and `.mov`, are currently not supported.
>
> **The [ranking criteria](/wiki/Ranking_criteria#video-and-background) specify a maximum video resolution of 1280x720 pixels.**

以及推荐的 ffmpeg 参数（含 `-an` 去音轨、CRF 20、veryslow、缩到 720p）：

> ffmpeg -i input -c:v libx264 -crf 20 -preset veryslow -vf scale=-1:720 -an -sn -map_metadata -1 -map_chapters -1 output.mp4

来源：[Compressing files — osu! wiki](https://osu.ppy.sh/wiki/en/Guides/Compressing_files)

### 4.2 整包的体积上限

【有据可查】提交上限按谱面长度线性增长，并有硬顶：

> The file size limit is 5 MB plus an additional 10 MB for every minute of beatmap length, and it caps at 100 MB. The difficulty limit is currently 128.

来源：[Beatmap submission § Limitations — osu! wiki](https://osu.ppy.sh/wiki/en/Beatmapping/Beatmap_submission)

### 4.3 视频与音频是两个文件；osu! 不读视频音轨

【有据可查】`.osu` 的 `[General]` 里有独立的 `AudioFilename`：

> | `AudioFilename` | String | Location of the audio file relative to the current folder |  |

来源：[osu! file formats — osu! wiki](https://osu.ppy.sh/wiki/en/Client/File_formats/osu_(file_format))

【有据可查】视频是另一个文件，由 `Video` 事件引用（见 §3.6）。

【有据可查】osu! **不使用**视频里的音轨，而且规则要求把它删掉：

> **A video's audio track must be removed from the video file.** The audio track in a video is not used in osu!, and removing it reduces the file size of the beatmap.

来源：[Ranking criteria — osu! wiki](https://osu.ppy.sh/wiki/en/Ranking_criteria)

### 4.4 是否必须 16:9

【有据可查】没有任何规则要求视频必须是 16:9。能查到的相关约束只有三条：宽度 ≤ 1280、高度 ≤ 720、编码必须 H.264（见 §4.1）。wiki 的 `Compressing files` 示例用 `-vf scale=-1:720`，即**保持原宽高比**只把高度定到 720（`-1` 让 ffmpeg 自动算宽度）——这本身就说明宽高比不被约束。

来源：[Compressing files — osu! wiki](https://osu.ppy.sh/wiki/en/Guides/Compressing_files)

【有据可查】storyboard 侧另有一个「宽屏支持」开关 `WidescreenStoryboard`（默认 0 = 4:3 布局）；而 lazer 对「纯视频 storyboard」自动按 16:9 处理（见 §3.6 引文）。

---

## 5. 与 BMS / BGA 的关系

主要一手来源是 hitkey 的 **BMS command memo**（BMS 规格的社区权威参考）与 **bmson** 规范。**重要限定**：hitkey 本人声明这只是一份个人备忘，不保证准确：

> - This is not what translated specifications. This is only my memo.
> - I used on-line translation service 100%.
> - Since I cannot understand English, I cannot judge whether the result of automatic translation is right.
> - The contents may lead to misunderstandings because the translation could be a bit off.
> - Moreover, my research may not be exact.

来源：[BMS command memo (draft) — hitkey](https://hitkey.nekokan.dyndns.info/cmds.htm)

### 5.1 BGA 与音符 lane 是两个不同的通道组

【有据可查】通道表把 BGA 与音符明确分开：

> | | | | |
> | 04 | BGA BASE | #BMPxx (BASE is displayed under LAYER) | BM98 |
> | 06 | BGA POOR | #BMPxx (POOR is displayed when a mistake is made) | BM98 |
> | 07 | BGA LAYER | #BMPxx (LAYER is layered over BASE) | BM98k |
> | 0A | BGA LAYER2 | #BMPxx (LAYER2 is layered over LAYER) | nanasi |
> | 0B | Opacity of BGA BASE | transparent « [01-FF] » opaque | nanasi |
> | 0C | Opacity of BGA LAYER | ... | nanasi |
> | 0D | Opacity of BGA LAYER2 | ... | nanasi |
> | 0E | Opacity of BGA POOR | ... | nanasi |
> | 11-1Z | 1P Visible | { BM98: 11-17, ... } | BM98/FT/MGQ/pomu |
> | 21-2Z | 2P Visible | ... | BM98/FT/MGQ/pomu |
> | 31-3Z | 1P Invisible | ... | BM98/FT/MGQ/pomu |
> | 41-4Z | 2P Invisible | ... | BM98/FT/MGQ/pomu |
> | 51-5Z | 1P Longnote | ... | MGQ/pomu |
> | 61-6Z | 2P Longnote | ... | MGQ/pomu |
> | A1 | BGA BASE aRGB | #ARGBxx a,r,g,b (each [0-255]) | nanasi |
> | A2 | BGA LAYER aRGB | #ARGBxx | nanasi |
> | A3 | BGA LAYER2 aRGB | #ARGBxx | nanasi |
> | A4 | BGA POOR aRGB | #ARGBxx | nanasi |
> | A5 | BGA KEYBOUND | #SWBGAxx | nanasi |

关键的 1P 键位映射（KEYMAP Table）：

> | BMS | KEY1 KEY2 KEY3 KEY4 KEY5 SCRATCH (FREE ZONE) |
> | 11 12 13 14 15 16 17 |

来源：[BMS command memo — Channel Mapping Table / KEYMAP Table](https://hitkey.nekokan.dyndns.info/cmds.htm)

### 5.2 BGA 是铺满整屏还是在某个区域？

【有据可查】**规格上没有「满屏 BGA」这回事**：默认的图像画布就是 256×256，超过这个尺寸的处理方式规格未定义、由实现自行决定。

> An image file is defined. The usual size is less than 256x256 pixel. It is because the size of the usual implementation's image canvas is 256x256 pixel. Specification has not defined the method of processing oversized images. Therefore, the method is implementation-dependent. For example, nazo does not display oversized images.

来源：[BMS command memo — #BMPxx](https://hitkey.nekokan.dyndns.info/cmds.htm)

【有据可查】部分实现支持「spread canvas」，此时大图会**铺到音符显示区**上：

> spread canvas: BM98 (origin ?) (5KEYS | 5K-Couple only ?), DDR (origin), nanasi, fgt++, uBMplay (1.5.0+), IIDXv, HDX, Sonorous(?), BGAEncAdv, ... (under investigation) A larger image than 256x256 is spread and displayed even on the space for a musical score display. BMS which used this behavior as stage effects exists slightly.
>
> Although LR2, and ruvit and IIDXv and HDX can use "a larger image than 256x256", an image does not overflow from the space for an image display. In Sonorous, canvas size may be able to be changed by #CANVASSIZE extension.

来源：[BMS command memo — #BMPxx / Image file formats](https://hitkey.nekokan.dyndns.info/cmds.htm)

【推断】从「the space for a musical score display」与「the space for an image display」这两个措辞可以推出：默认情况下 **BGA 区域与音符（乐谱）显示区是两个不同的区域**，BGA 不会盖住音符；只有走 spread canvas 的实现才会溢出到音符区。这是措辞推断，memo 里没有画出示意图，也没给出两个区域的具体坐标。

【有据可查】BGA 实际被画在多大、什么形状的窗口里，是**播放器/皮肤**决定的，规格不管；社区指南直说没有共识：

> Unfortunately there doesn't seem to be a consensus on this.
>
> Some people pad their BGAs with black bars to 1:1 resolutions as many skins have square BGA windows and many people use the setting to stretch BGAs. On ther hand some skins have 4:3 BGA windows
>
> - **256x256**: Note that LR2SD can only render up to 256x256 for BGAs.
> - **512x512**: A larger resolution for square BGAs. May be used for LR2HD, though 480p may be preferred.
> - **640x480**: Possibly the most common resolution for rectangular BGAs.
> - **(?)x720**: Usually only for mp4 BGAs targeting people on beatoraja.

来源：[BMS BGA Encoding Guide — wcko87](https://wcko87.github.io/bms-checklist/video-encoding)

### 5.3 #BMP / #BGA 的尺寸与位置规则

【有据可查】`#BMPxx` 只给文件，没有位置参数；`#BGAxx` 才是带裁剪与定位的版本：

> #BGA [00-ZZ] [#BMP-index] x1 y1 x2 y2 dx dy
> channel: remarks
> #xxx04 BGA BASE
> #xxx06 BGA POOR
> #xxx07 BGA LAYER
> #xxx0A BGA LAYER2 (nanasi, Angolmoi...
>
> dx dy start point of drawing
> Upper left corner coordinates of drawing region
>
> For example:
> original image file: #BMP 01 320x64.bmp
> portion to clip: portion to display:
> From (64, 64) to (128, 128) is clipped.
> #BGA02 01 64 64 128 128 0 0
> It is displayed from (96, 96) of canvas.

来源：[BMS command memo — #BGAxx](https://hitkey.nekokan.dyndns.info/cmds.htm)

（即 `x1 y1 x2 y2` 是在源图上裁剪的矩形，`dx dy` 是这块被画到画布上的左上角坐标。）

【有据可查】`#BGA` 里同一索引的命名优先级高于 `#BMP`：

> When the same index is defined in #BMP and #BGA , priority is given to #BGA .

来源：[BMS command memo — #BGAxx](https://hitkey.nekokan.dyndns.info/cmds.htm)

【有据可查】另有按键触发的 BGA 动画 `#SWBGAxx`（帧率:时长:触发通道:是否循环:颜色 + BMP 索引序列）：

> #SWBGA01 100:400:16:0:255,255,255,255 01020304
> #070A5:01
> In #070 or later, this is displayed when the channel #xxx16 (1P-side SCRATCH) is inputted. Frame rate is 10 FPS. (100 = 1000/10) Duration is 400ms. Loop does not carry out.
> ...
> Among the images of #BMP01-04 , "a completely opaque and completely white portion" becomes transparent, and displays the image sequence of the usual BMS.

来源：[BMS command memo — #SWBGAxx](https://hitkey.nekokan.dyndns.info/cmds.htm)

### 5.4 BGA 视频：层次、音轨、宽高比

【有据可查】BGA 视频是一个**底**层：`#xxx07` 在最上、`#xxx04` 居中、`#VIDEOFILE` 在最下；并且视频的音轨被忽略。

> summary
> channel/header remarks
> BGA LAYER #xxx07 top level
> BGA BASE #xxx04 middle level
> VIDEOFILE #VIDEOFILE bottom level
>
> The black (RGB:00:00:00) portion of both of channels #xxx04 and #xxx07 becomes transparent.
> ...
> The channels #xxx04 , 07 , and 06 can be used simultaneously with a video. The channel #xxx04 is piled up on the VIDEOFILE. The channel #xxx07 is piled up on the channel #xxx04.

```
#VIDEOFILE videofilename
origin: bemaniaDX
support: bemaniaDX, TypeAce, Be-Pachi Music, RDM, ruvit, Sonorous(parsing-only)
The filename of the video to playback is specified as BGA.
filetype: supporting MPEG: TypeAce, RDM, ruvit
          supporting AVI: bemaniaDX, TypeAce, Be-Pachi Music, RDM, ruvit
...
loopable: If time remains, it will rewind and repeat. A show is begun from #000 .
sounding: The sound which a video contains is not sounded. Video's sound is ignored. AVI is dependent on Codec installed in PC.
```

【有据可查】另一条 `#MOVIE` 命令同样忽略视频音轨：

> When a video contains a sound, the sound in a video does not play back.

来源：[BMS command memo — #VIDEOFILE / #MOVIE](https://hitkey.nekokan.dyndns.info/cmds.htm)

【有据可查】视频宽高比在 BMS 里没有统一规定：

> Usually, the aspect ratio of a video is 1:1. However, some implementations are supporting 4:3, 16:9, etc ...

来源：[BMS command memo — Video file defining to #BMP](https://hitkey.nekokan.dyndns.info/cmds.htm)

【有据可查】bmson（BMS 的后续 JSON 规范）里，BGA 事件**只有脉冲位置和图片 id，没有位置/尺寸字段**：

> // bga
> dictionary BGA {
>     BGAHeader[] bga_header;   // picture id and filename
>     BGAEvent[]  bga_events;   // picture sequence
>     BGAEvent[]  layer_events; // picture sequence overlays bga_notes
>     BGAEvent[]  poor_events;  // picture sequence when missed
> }
>
> // bga note
> dictionary BGAEvent {
>     unsigned long y;  // pulse number
>     unsigned long id; // corresponds to BGAHeader.id
> }

来源：[bmson format specification](https://bmson-spec.readthedocs.io/en/master/doc/index.html)

【推断】bmson 既然把 BGA 定义成「只有时间与图片 id 的序列」，那么 BGA 在屏幕上的摆放只能是播放器/皮肤的自由；这与 §5.2 的社区观察一致。

### 5.5 「BGA 画面与 note 位置如何协调」

【查不到】没有找到任何**规格级或官方**资料来规定或建议「BGA 画面内容如何与 note 位置协调以实现可读性」。可查到的只有以下三类间接材料：

1. 上文 §5.2 的 spread canvas 事实——部分实现会让大图溢出到音符显示区（这一条本身是「会不会挡到 note」的唯一规格相关陈述）。
2. 皮肤侧事实——BGA 窗口有方形也有 4:3，且有「拉伸 BGA」的设置项（[BMS BGA Encoding Guide](https://wcko87.github.io/bms-checklist/video-encoding)）。
3. BMS 的 BGA 层叠（BASE / LAYER / LAYER2 / POOR，以及 #ARGB 调透明度、0B–0E 调不透明度、#SWBGA 按键动画）确实是谱师**可以**用来控制画面压住 note 程度的机制（[BMS command memo](https://hitkey.nekokan.dyndns.info/cmds.htm)）。但这是机制清单，不是「可读性指引」。

关于 osu! 一侧，可查到的**唯一**成文可读性要求都只涉及「颜色不要和背景/视频混在一起」这类，而不是位置协调：

> - **Each beatmap must use at least two different custom combo colours unless the default skin is forced.** The combo colours must not blend with the beatmap's background/storyboard/video in any case.
> - **Slider body colour and slider border colour together must not blend in with a beatmap's background or video.**

来源：[Ranking criteria (osu!) — osu! wiki](https://osu.ppy.sh/wiki/en/Ranking_criteria/osu!)

【推断】之所以找不到，很可能是因为在 BMS 里 BGA 与 note 分属不同通道、且（默认）不同区域，协调问题被留给皮肤布局而不进入谱面格式；但这只是我的推测，没有来源支持。
