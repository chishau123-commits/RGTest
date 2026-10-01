# Note 与判定特效贴合视频（色板权威 + 判定挂点）：验证记录

> 对应 [VideoBgaBranchingPlan.md](VideoBgaBranchingPlan.md) 的**阶段 1**：达成"风格一致"（决定 2 的 A 目标）。
> 素材：[`BGA/goodtek60/`](../BGA/goodtek60)（用户指定的 BGA，RIFE 插帧 60fps，gitignore）。
> 证据截图：[4 秒（橙）](Screenshots/demo-bga-palette-early.png) · [12 秒（青）](Screenshots/demo-bga-palette-late.png)。

## 0. 结论速览

| 问题 | 结果 |
|---|---|
| Note 颜色能不能跟着视频走 | **能**。同一份谱面在 4 秒是橙色 Note、12 秒是青色 Note，与两段视频同色相 |
| 配色权威从哪来 | **离线烘焙**（`Tools/Bga/bake_bga_palette.py`），运行时不做任何自动取色 |
| Note 会不会被视频带得读不出来 | 不会。烘焙时把主色亮度压到 ≤0.42，与运行时世界底色（0.947）**拉开 0.527** |
| `effectClips` 的 `target=="judgement"` 是不是真的能用 | **能用**。Miss 触发（alpha 0.301）、Perfect 不触发（过滤器生效），走的是真实判定链路 |
| 自检结果 | **`RESULT=PASS CHECKS=22`**，退出码 0 |

## 1. 配色权威：为什么是离线烘焙

方案决定 10 定的规则是：**离线烘焙进谱面 + 谱师手工兜底**，运行时从画面取色只允许驱动氛围量。理由是可读性——自动取色会抖，而且会取到低对比度的脏色，而 Note 读不读得出来是玩法底线。

新增的数据：

```jsonc
"paletteKeys": [
  { "tick": 0,    "tap": {"r":0.882,"g":0.314,"b":0.106,"a":1}, "drag": {...}, "glow": {"r":0.68,"g":0.38,"b":0.27,"a":1} },
  ...
]
```

- `glow` = 视频自己那一秒的平均色，只驱动**氛围量**（路径线取它一半）。
- `tap` / `drag` = 派生出的 Note 主色，色相取自视频，亮度被压进可读区间。

烘焙工具：`Tools/Bga/bake_bga_palette.py`（**已跟踪**；输入输出在 gitignore 的 `BGA/` 下）。它用 ffmpeg 把每秒压成 1×1 像素拿到该秒平均色，按 `--segment-seconds`（默认 8 秒）分组，再由 tempo 图把秒换算成 tick。实测这条 BGA 烘出 16 个键，色相**确实在跟着画面走**：0–8 秒橙、8–16 秒青、16 秒回橙、24 秒琥珀。

### 一条被实测纠正的规则

第一版规则按"视频亮度"决定 Note 亮度（画面亮就给暗的 Note），跑出来 **亮度差只有 0.047**——饱和橙色在 V=0.98 时亮度也只有 0.48，G 分量压住了它。更要紧的是判据本身错了：**Note 画在运行时自己的浅色暖雾世界之上，不是直接贴在视频上**，所以可读性的对照物是**世界底色**（雾色 ≈0.965/0.945/0.91，亮度 0.947），不是视频平均色。

改成"在视频色相下二分出亮度 ≤ `--tap-luma`（默认 0.42）的 V"之后，16 个键的 gap 全部 ≥0.517。

### 另一条被截图纠正的规则

第一版按整段线性 RGB 插值，结果 4 秒处的 Note 变成**土黄**——因为这段视频从橙硬切到青，而 RGB 直线穿过灰调。视频自己的颜色是硬切的，本段就该是本段的颜色。现在改成**整段保持、只在边界做 0.35 秒过渡**（`RhythmDemoPalette.PaletteBlendSeconds`），土黄消失。

## 2. 判定驱动的特效

`EffectClipData` 早就有 `target`，注释里也写了 `judgement`，但运行期只把 `target` 当作 `scenes.Find(id)` 用——**判定语义完全没实现**。现在实现了，并新增一个字段：

```jsonc
{ "id": "missLayer", "kind": "flash", "target": "judgement",
  "result": "miss",              // 新增：留空 = 任何判定都触发
  "color": {"r":0.55,"g":0.05,"b":0.25,"a":1}, "intensity": 1,
  "durationTicks": 240,          // 此时含义变为「命中后持续多久」的包络长度（这里 0.25 秒）
  "easing": "impact" }
```

- `target=="judgement"` 的片段**忽略 `startTick`**，改看"这一档判定最后一次发生在多久以前"，`durationTicks` 变成包络长度。
- `result` 是过滤器：这一档 BMS 的 `poor_events` 在运行时的对应物——漏掉的音符可以有自己的画面。
- 触发链路是真实的：`JudgementEngine.Resolve` → `OnJudged` → `AuthoredVisualDirector.NotifyJudgement` → `Evaluate`。
  **注意 `NotifyJudgement` 必须放在 `OnJudged` 里 `if (Miss) return;` 之前**，否则漏掉的音符永远触发不了任何东西——而"漏掉时画面有反应"正是这个功能的重点。

自检（`-demoBgaSmoke`）覆盖：

```
PASS 谱面里有 target=="judgement" 的特效
PASS 漏掉音符会触发判定特效（alpha=0.301）
PASS Perfect 不会触发 result=="miss" 的特效（过滤器生效）
```

## 3. 自检覆盖了什么

`-demoBgaSmoke` 现在一共 24 项，分四段：视频通路（阶段 0b）、音画对齐、色板权威、判定挂点。

```
PASS chart.videoBga 指向的包能加载并解码
PASS 五个采样点都没有黑帧（black=0）
PASS 五个采样点画面各不相同（distinct=5/5）
PASS 两张色板截图画面中心都不是黑的（0.916 / 0.907）
SHOT earlyTap=(0.882,0.314,0.106) lateTap=(0.069,0.509,0.572)
PASS BGA 与歌曲时钟对齐在 1 帧内（frame 1200 vs 1200）
SYNC t=20.000 offset=0.000 expectedFrame=1200 actualFrame=1200
PASS 谱面带 paletteKeys，色板已激活
PALETTE keys=16 tap@0.000s=(0.882,0.314,0.106) tap@8.000s=(0.069,0.509,0.572)
PASS 色板已推到 Note 材质上（t=0 对齐 keys[0].tap）
PASS 按 tempo 算出的第二个键时刻对齐 keys[1].tap
PASS 两个时间点的 Note 主色不同（跟着视频走）
PASS Note 主色与世界底色有足够亮度差（worldLuma=0.947 gap=0.527）
PASS 谱面里有 target=="judgement" 的特效
PASS 漏掉音符会触发判定特效（alpha=0.301）
PASS Perfect 不会触发 result=="miss" 的特效（过滤器生效）
RESULT=PASS CHECKS=24
```

**方案 §7 验收清单的覆盖情况**（清单本身是围绕"分叉"写的，所以只有一部分现在可评）：

| # | 条目 | 现在 |
|---|---|---|
| 1 | BGA 与音轨同一次播放内偏移 ≤ 1 帧 | **已验**：帧级精确，0 帧漂移 |
| 3 | Note 主色落在当前 tick 的色板容差内 | **已验**：对齐 `keys[0]/keys[1]`，容差 0.02 |
| 2 / 4 / 5 / 6 / 7 / 8 | 切换前后无黑帧、分支序列确定性、臂与主线汇流一致、20 次切换后内存、回拖暂停后状态可重现、强制单流源降级 | **属于阶段 2**（分叉本身），现在没有可测的对象 |

两个时间点的采样秒数不是写死的 8 秒，而是由 `keys[1].tick` 经 tempo 图算出来的——这样验的是"tick → 秒 → 取键 → 推到材质"整条路，而不是一个常数。

## 4. 落地清单

| 位置 | 内容 |
|---|---|
| `Runtime/ChartData.cs` | `PaletteKeyData`、`ChartData.paletteKeys`、`EffectClipData.result` |
| `Runtime/RhythmDemoPalette.cs`（新） | 色板解析 + 每帧插值 + 推给共享材质；`PaletteActive/PaletteTap/...` 供自检 |
| `Runtime/SceneVisuals.cs` | `VisualLibrary.ApplyPalette(tap,drag,glow)`——驱动 Tap/Drag/两个保护壳/路径线；Border/Marker 刻意不参与 |
| `Runtime/VisualAuthoringRuntime.cs` | 每档判定最后发生时刻、`NotifyJudgement`、`target=="judgement"` 分支、`LastJudgementEffectAlpha` |
| `Runtime/RhythmDemoController.cs` | `InitializePalette()`；`EvaluateVisuals` 里 `ApplyPalette(time)`；`OnJudged` 里 `NotifyJudgement` |
| `Runtime/RhythmDemoBga.cs` | 自检扩到色板与判定；两张相机截图 |
| `Tools/Bga/bake_bga_palette.py`（新，已跟踪） | 离线烘焙色板 |

## 5. 已知限制

- **`result` 是新字段**：旧谱面没有它就是"任何判定都触发"，行为不变，所以不构成破坏性改动；但 `.thr`/`.grchart` 的格式文档要补上。
- **包络长度换算假定 tempo 从 0 起算**（`tempo.SecondsAtBeat(durationTicks/ticksPerBeat)` 的差值）。多 tempo 谱面上这个换算会有偏差，需要时改成"从当前时刻往后算"。
- **色板插值仍在 RGB 里做**：靠"整段保持 + 短过渡"回避了脏中间色；若要更讲究，可以在 HSV 里插值色相，但那要处理色相环绕。
- **色板键的密度由烘焙参数决定**（默认 8 秒）。段越短越贴合视频，但边界过渡也越频繁。
- **只有一条 BGA 被烘焙验证过**。别的素材要重新跑工具，可读性规则（`--tap-luma`）可能也要按素材调。
