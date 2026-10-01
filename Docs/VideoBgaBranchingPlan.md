# 视频 BGA 判定分支方案

> 状态：**提案，未实现，未改任何代码**。
> 术语以根目录 [CONTEXT.md](../CONTEXT.md) 为准；核心取舍记在 [ADR-0001](adr/0001-layer-first-bga-judgement-feedback.md)。
>
> ⚠️ **本方案正在按"玩法维度跟随 BGA 素材维度"重写**（[ADR-0003](adr/0003-gameplay-dimension-follows-bga-material.md)）。以下三处已被该决定取代，**今后只在三维玩法里成立**：
> - §0 非目标"不把 Note 改成 2D 屏幕空间"
> - 决定 12"Note 保留 3D 世界空间位姿"
> - 结尾重申的"不把 Note 2D 化"
>
> 全文其余表述（Note、判定平面、色板可读性基准等）也还带着三维假设，等重写统一。

## 0. 目标与非目标

**目标**：判定结果改变画面，并且看不出接缝。三件事同时要：

- **(a) 逐 Note 瞬时反应**——命中的那一下画面就有变化；
- **(b) 段落分叉**——上一段的表现决定接下来走哪条走向；
- **(c) 状态切换**——连续 Perfect 与连续 Miss 下，画面整体风格不同。

**非目标**（本方案明确不做）：

- 不做"每个判定切一条视频"。见 §9。
- 不做 Note 被 BGA 里的景物遮挡。当前 `GeometryRhythm/Backdrop` 是 `Queue=Background / ZWrite Off / ZTest Always`（`Assets/RhythmDemo/Resources/Shaders/Backdrop.shader:9-12,25`），注释原话是"every note still draws over it"——**物理上做不到**，要做得先换渲染路径。
- 不重制 BGA 素材。所有臂先用同一素材的派生变体。
- 不用带逐像素 alpha 的视频层（Android 内置 VP8 无透明，必须转码）。
- 不把 Note 改成 2D 屏幕空间。

## 1. 已定决策台账

| # | 决定 |
|---|---|
| 1 | (a) + (b) + (c) 全要 |
| 2 | "融入"= **风格一致**，不是空间一致 |
| 3 | 素材来源 = 不改视频只叠加 + 同源派生变体 |
| 4 | 平台 = Windows + Android |
| 5 | 存储形态 = **一条文件多区段 + 同 player seek**；已由 Spike A 判定成立（[验证记录](VideoBgaSeekSpikeVerification.md)） |
| 6 | 验收 = 必须能被自动检查；接受一次性 spike |
| 7 | 拓扑 = **发而后汇**，不累积历史 |
| 8 | 格式同时支持图层模型与换流模型，能力运行时探测 |
| 9 | 接受同一谱面在 Windows 与 Android 上观感可能不同 |
| 10 | 色板 = 离线烘焙进谱面 + 谱师手工兜底；运行时取色只驱动氛围量 |
| 11 | 反馈层由引擎侧程序化绘制；键控留作真分支备选 |
| 12 | Note 保留 3D 世界空间位姿 |
| 13 | `BlenderBgaRuntime` **冻结不删** |
| 14 | "把 `videoBga` 搬进游玩运行时"是独立前置工程；判定事件改多播，不引入全局总线 |
| 15 | 分叉点由**谱师在制谱器（Thart）显式标注 tick** |
| 16 | 写根 `CONTEXT.md` 与 ADR（已完成） |
| 17 | 授权未定：**暂时只本地验证** |
| 18 | 体积上限：主片 ≤ 120 MB、单曲 BGA 总量 ≤ 250 MB、单条臂 ≤ 8 MB |
| 19 | **BGA 必须是 60fps，且由真插帧得到**（不是复制帧）：30fps 源用 RIFE 2× 插帧，再按 `-bf 0 -g 60` 编码。理由与实测见 [验证记录 §8](VideoBgaSeekSpikeVerification.md) |
| 20 | **Note 可读性的对照物是运行时世界底色，不是视频**：色板主色压到亮度 ≤0.42（世界底色约 0.947）。且色板**整段保持、只在边界做 0.35 秒过渡**——RGB 插值在橙→青之间会穿过土黄 |
| 21 | `effectClips` 的判定过滤器用新增的可选字段 `result`（留空 = 任何判定）；此时 `startTick` 被忽略、`durationTicks` 变成包络长度 |

## 2. 方案建立在这些事实之上

**F1｜游玩运行时目前不会播 BGA。** `chart.videoBga` 在整个 `Runtime` 程序集里只被声明、从不被消费（`Assets/RhythmDemo/Runtime/ChartData.cs:32`）；唯一会播它的是**旧制谱器**（`Assets/RhythmDemo/ChartEditor/ChartEditorVideoBga.cs`，内部用 `VideoBgaRuntime`）。所以第一阶段的第一件事不是分支，是**让游玩运行时（`GeometryRhythmDemo`）能播 BGA**。
> 归属已定：制谱器是 Thart，`.thr` 是格式权威，旧制谱器冻结（[ADR-0002](adr/0002-thart-is-the-chart-editor.md)）。Thart 目录内目前**没有任何视频代码**，`.thr` 容器也**没有 BGA 槽位**，所以"BGA 搬进 Thart"是排在后面的独立工程，不与本方案的阶段 0/1 捆绑。

**F2｜两条视频路径互不相通，容差都是 200–250 ms。** 制谱器 `VideoBgaRuntime.cs:131-159` 每帧自行 seek 对齐，`tolerance = run ? .20 : ...`；游玩运行时走 `chart.sceneObjects` 里 `kind=="video"` 的条目（`Assets/RhythmDemo/Runtime/VisualAuthoringRuntime.cs:199-205`，`MaterialOverride` + `isLooping=true`），漂移超过 `.25` 才纠偏（`:401,405`）。按 60fps 算是 **12–15 帧**——**BGA 源不可能承载逐 Note 反馈**。这是三层分离的根本理由。

**F3｜Note 与反馈层都是引擎侧渲染，可以同帧。** Note 是程序化 `Ring` 网格（`Assets/RhythmDemo/Runtime/SceneVisuals.cs:11-31`），判定时已经有 12 个对象池环形脉冲（`Assets/RhythmDemo/Runtime/RhythmDemoController.cs:156-160`，动画在 `:326-346`）与 UGUI 判定文字（`Assets/RhythmDemo/Runtime/DemoHud.cs:160-163`）。**唯一能跟上判定的就是这一层。**

**F4｜Note 颜色是硬编码的，没有配色权威。** `SceneVisuals.cs:64-75`：Tap 蓝 `(.025,.36,1)`、Drag 白；`CyberTheme.cs:15` 与 `Docs/PaletteApplied.md:10` 都明确"UI 主题不影响世界空间 Note 材质"；`NoteData` 没有颜色字段（`ChartData.cs:142-149`）。要做风格贴合，必须先建立色板权威。

**F5｜`effectClips` 已经有一个"判定"挂点，但是空的。** DTO 注释写 `target` 可以是 "screen, scene, judgement or a SceneObjectData id"（`ChartData.cs:182`），`kind` 已有 `flash/color/fog/light/scenePulse/shockwave/particles/speedLines/glitch`（`:173-189`）；但运行时 `target` 只用于 `scenes.Find(id)` 找场景对象，**`judgement` 没有实现**（`VisualAuthoringRuntime.cs:327,338`）。

**F6｜判定只有三档。** `NoteResult { Pending, Perfect, Good, Miss, Skipped }`（`Assets/RhythmDemo/Runtime/JudgementEngine.cs:6`），窗口 `.040` / `.120`（`:21-22`），全部判定经 `Resolve`（`:54-66`）并触发**单播** `Action<RuntimeNote,NoteResult> OnJudged`（`:34`）。`RhythmDemoController.cs:169` 用 `=` 赋值——第二个订阅者会覆盖第一个。

**F7｜`BlenderBgaRuntime` 是唯一有真深度的路径。** 它加载 Blender 导出的包，含 `cameraCuts` / `visibilityTracks` / `lightTracks` / `worldColor` / `worldPanorama` / `cameraTrack`（`Assets/RhythmDemo/Runtime/BlenderBgaRuntime.cs:24-29`），由 `chart.blenderBga` 开关。**冻结不删**，因为它是"风格一致 → 空间一致"升级的唯一现成入口。

**F8｜体积的实测基准，以及关键帧密度不是它的原因。** 现成 1080p60 素材 20.0 秒 = 18.1 MB（`.buildtmp/bga20/package/chart.assets/BGA/generated20/manifest.json`），即 **0.905 MB/s**；交付规格见 `Docs/VideoBgaWorkflow.md:41`（H.264 High、固定 60fps、无 B 帧）。
> **Spike A 更正了一条假设**：那个"每 15 帧一个关键帧"对 seek 速度**没有贡献**——关键帧间距 0.25 s / 1 s / 4.17 s 的区段头 seek 成本完全相同（帧就绪 22.5–22.8 ms），而 `-g 60` 比 `-g 15` **小 12%**。另外交付包是**离线**接缝链装配的，实测 521/1200 帧是关键帧，并不遵守导入命令里的 `-g 15`——**不要假定交付包的关键帧网格等于转码命令里的数字**。详见 [VideoBgaSeekSpikeVerification.md](VideoBgaSeekSpikeVerification.md)。
> **真实素材复测（用户指定的 BGA，也是当前调试素材）**：30fps 原始 remux 带 B 帧，暂停 seek 的像素确定性会破（6/6 目标不确定，退出码 FAIL）；用 RIFE `rife-v4.6` 2× 插帧到 60fps 并按 `-bf 0 -g 60 -crf 23` 重编后为 **107.0 MB / 124.1 s / 125 个关键帧（1 秒网格）**，解码器 seek **15 ms**、运行时 **104 ms**、零黑帧、逐位确定、**PASS**。所以交付素材**不该带 B 帧**，且 1 秒网格下"区段头对齐关键帧"不再是性能前提。

**F9｜授权未定。** 验证素材是第三方 BGA（见 §10）。`/BGA/` 已被 gitignore——**素材放那里，不要放 `Assets/` 或 `Charts/`**。

## 3. 三层架构

关键的分离原则：**离判定越近的层，越不能经过视频解码。**

### 第 1 层 · BGA 源

职责：把某条视频的某个区段，以"定位到精确时间"的方式呈现在画面最底层。

- 两种实现，格式与素材相同，**能力运行时探测**：
  - `单流源`（默认，全平台）：一个 VideoPlayer + 一条多区段文件；切臂 = seek 到区段头。
  - `双流源`（Windows 增强）：两个 VideoPlayer 预 prepare，交叉淡化切流。
- 硬性约束：**BGA 源的音频永远静音**，音频权威是谱面音轨 + `SongClock`。这是现状（`VideoBgaRuntime.cs` 的 `audioOutputMode=None`、`VisualAuthoringRuntime.cs:201`），写进方案是为了防止有人"顺手"把视频音轨打开。
- 已知边界：定位精度 200–250 ms（F2）。**BGA 源只负责"这一段该长什么样"，不负责"这一下该有什么反应"。**

### 第 2 层 · 分支控制器

职责：持有一个**当前状态**，在分叉点上做一次切换决定。

- 输入：谱面的分叉点列表（显式 tick，决定 15）+ 判定流（订阅多播化的 `OnJudged`，决定 14）。
- 判定度量：自上一个分叉点以来的段内统计（准确率 / 连击 / Miss 数）→ 三档。
- 输出：给第 1 层"切到哪个区段"，给第 3 层"当前是哪个状态"。
- **发而后汇**（决定 7）：每个分叉点最多三条臂，臂在 `convergeTick` 回到主线；**不累积历史**。这直接锁死素材量 = 主线 + 分叉点数 × 3 条短臂。
- 不引入全局事件总线（决定 14）。仓库现在没有（grep `EventBus` 只命中触控网络层的 `Thart/Network/ThartServer.cs`、`ThartClient.cs`），为这件事引入是过度设计。

### 第 3 层 · 反馈层

职责：逐 Note 的同帧反馈 + 状态型的风格贴合。

- 复用现有资产：对象池环形脉冲、UGUI 判定文字、`effectClips` 的九种 kind。
- **落地 `target == "judgement"`**（F5）：这是"判定驱动特效"的正式挂点，格式里已经预留、运行时空着。
- 新增**色板**：按 tick 从视频烘焙的颜色序列，作为 Note 材质 `_Color` 与反馈层颜色的权威（决定 10）。取代 `SceneVisuals.cs:64-75` 的硬编码常量。
- 运行时从画面取色（降采样 RenderTexture + `AsyncGPUReadback`）**只允许**驱动雾、泛光、后处理等氛围量，**不允许**决定 Note 主色——Note 的可读性是玩法底线，不能交给会抖的自动取色。

## 4. 数据契约提案（未实现）

权威容器是 **`.thr`**（[ADR-0002](adr/0002-thart-is-the-chart-editor.md)）。它是 store-only ZIP（`Assets/RhythmDemo/Thart/Core/ThartZip.cs`），字段随 `chart.json` 进包即可——**但 `.thr` 的内容清单目前没有 BGA 槽位**（`ThartPackage.cs:25-28` 只有 `chart.json / touch.json / meta.json / audio/`），所以阶段 2 之前要先把槽位加上。`.grchart` 侧"以后再说"，本方案不为它做设计。

`chart.json` 顶层新增（与现有 `videoBga` 并列；字段名实现时可改）：

```jsonc
"bgaBranching": {
  "schemaVersion": 1,
  "source": {
    // 一条文件里的全部区段；区段头必须落在关键帧上
    "segments": [
      { "id": "intro",      "startSeconds": 0.0,  "endSeconds": 12.0 },
      { "id": "verseA_main","startSeconds": 12.0, "endSeconds": 24.0 },
      { "id": "verseA_hi",  "startSeconds": 24.0, "endSeconds": 30.0 },
      { "id": "verseA_mid", "startSeconds": 30.0, "endSeconds": 36.0 },
      { "id": "verseA_lo",  "startSeconds": 36.0, "endSeconds": 42.0 }
    ],
    "trimSeconds": 0.5   // 交叉溶解长度，沿用接缝链的 5 帧做法
  },
  "branches": [
    {
      "id": "verseA",
      "tick": 1920,             // 分叉点：谱师显式标注
      "convergeTick": 3840,     // 汇流点
      "mainSegment": "verseA_main",
      "metric": { "kind": "accuracy", "since": "previousBranch" },
      "arms": [
        { "grade": "high", "content": { "kind": "derived", "variant": "bloom" } },
        { "grade": "mid",  "content": { "kind": "derived", "variant": "neutral" } },
        { "grade": "low",  "content": { "kind": "derived", "variant": "glitch" } }
      ]
    }
  ],
  "paletteKeys": [
    { "tick": 0,    "tap": [0.03, 0.36, 1.00], "drag": [0.99, 0.99, 1.00], "glow": [0.35, 0.68, 1.00] },
    { "tick": 1920, "tap": [0.85, 0.18, 0.55], "drag": [1.00, 0.95, 0.80], "glow": [1.00, 0.45, 0.70] }
  ]
}
```

两处设计要点：

1. **`arm.content` 是一个引用，不是内联参数。** `kind: "derived"` 指向派生变体，`kind: "segment"` 指向一条真素材区段。阶段 3 把 `derived` 换成 `segment` 时**格式不变、代码不变**（决定 8 与 ADR-0001 的后果）。
2. **汇流约束**：每条臂的结束帧必须与主线在 `convergeTick` 的帧逐像素一致，或落在 `trimSeconds` 的交叉溶解内。这正是 `Docs/VideoBgaWorkflow.md:116-138` 接缝链已经在做的事（实测把接缝从 7.18× 压到 1.04×），直接复用。

## 5. 分阶段计划

### 阶段 0a · 前置工程：制谱器（Thart）能播 BGA —— **已完成**

- **视频 BGA 的编辑侧归宿是 Thart**（[ADR-0002](adr/0002-thart-is-the-chart-editor.md)）。已接进去：加载 `videoBga` 指向的包、建 `VideoBgaRuntime`、由 Thart 自己的歌曲时钟驱动、作为编辑器背景呈现。
- Thart 的屏幕绘制权归 `OnGUI`，它先整屏铺不透明 backdrop，所以远平面视频会被盖住——改走**贴图模式**（新增 `VideoBgaRuntime.ActivateAsTexture()`，追加式），由 `DrawMainView` 里的 `GUI.DrawTexture` 画在主视图矩形内。
- **`.thr` 已有 BGA 槽位**：`bga/manifest.json` + `bga/video.mp4` + `bga/audio.wav`；保存时打包、打开时展开到缓存再绑定。
- 旧制谱器**不动**，且其上的 harness 已删除。
- 验收：`-thartBgaPackSmoke` 往返自检 **`RESULT=PASS CHECKS=14`**（含两张主视图截图），见 [ThartBgaBindingVerification.md](ThartBgaBindingVerification.md)。
- **seek 量测 harness 已落在 Thart**：旧制谱器 partial 类里的 `ChartEditorSeekSpike.cs` 已删除，改为 Thart 的两个分片 —— `ThartEditorSeekSpike.cs`（量测本体）与 `ThartEditorVideoBga.cs`（视频通路 + CLI + 探针宿主）。Thart 原本没有运行期冒烟约定，这一步建立了它：`-thartCapture <dir>` → `<套件>-checks.txt` + `Application.Quit(code)`。
- 独立验收，和分支无关。

### 阶段 0b · 前置工程：游玩运行时能播 BGA —— **已完成**

- 把 `videoBga` 接进**游玩运行时 `GeometryRhythmDemo`**（`RhythmDemoController` 的新分片 `RhythmDemoBga.cs`）。运行时此前完全不消费它（F1）。
- 运行时用 `Activate()`（相机远平面）——与 Thart 必须用贴图模式相反，因为这里有真实的世界相机。
- 复用运行时既有的 `-demoSmoke` / `-demoCapture` 约定，新增 `-demoBgaSmoke`。
- 验收：**`RESULT=PASS CHECKS=13`**，零黑帧、五个采样画面各不相同、相机渲出的画面中心灰度 0.907，截图见 [RhythmDemoBgaRuntimeVerification.md](RhythmDemoBgaRuntimeVerification.md)。
- **发现**：远平面视频会被运行时自己的世界几何体挡住，所以它现在是"天空背景"而非全屏 BGA。要做"视频就是整个环境"，得隐藏舞台几何体或改走全屏 quad + Background 队列（记录在该文档 §3）。

### 阶段 1 · 核心体验：(a) + (c) 与风格贴合 —— **已完成**

- 落地 `effectClips` 的 `target == "judgement"`（F5：格式里预留、运行时空着）。新增一个可选字段 `result` 作为判定档过滤器——这是 BMS `poor_events` 在运行时的对应物，漏掉的音符可以有自己的画面。触发走真实链路 `Resolve → OnJudged → NotifyJudgement → Evaluate`。
- 建立色板权威：离线烘焙（`Tools/Bga/bake_bga_palette.py`）→ `chart.paletteKeys` → 每帧插值 → 共享材质，驱动全部 Note、保护壳与路径线。运行时**不做**自动取色（决定 10）。
- **这一阶段不需要任何分支素材、不需要任何 seek**，已达成"风格一致"。
- 验收：**`RESULT=PASS CHECKS=22`**；两张截图（4 秒橙、12 秒青）显示 Note 颜色确实跟着视频走，见 [JudgementFeedbackPaletteVerification.md](JudgementFeedbackPaletteVerification.md)。
- 两条被实测纠正的规则：可读性的对照物是**运行时世界底色**而不是视频平均色；色板要**整段保持 + 短过渡**，否则 RGB 插值会在橙→青之间穿过土黄。

### 阶段 2 · (b) 分叉：同文件多区段 seek + 发而后汇

- **先在 `.thr` 里加 BGA 槽位**：容器当前只有 `chart.json / touch.json / meta.json / audio/`（`ThartPackage.cs:25-28`）。这是阶段 2 的硬前置。
- 制谱器（**Thart**）：标注分叉点；按 Note 密度与乐句**自动建议候选点**，谱师点选落定（决定 15）。
- 素材规范扩写：主线 + 每个分叉点 3 条短臂，拼进**同一条文件**。关键帧规格从"每 15 帧"放宽到 **`-g 60`（1 秒）**：Spike A 实测 seek 成本不变、文件小 12%，而 1 秒内的非关键帧 seek 也是免费的；区段头仍对齐关键帧，但那已是保险而非前提。
- 运行时：`单流源` 实现 seek 到区段头。**成本已量过**（解码器 23 ms、运行时含策略 107 ms、零黑帧），见 [VideoBgaSeekSpikeVerification.md](VideoBgaSeekSpikeVerification.md)。

### 阶段 3 · 增强与预留

- `双流源`（Windows）：双 player 预 prepare + 交叉淡化。
- **真分支素材槽位**：`arm.content` 从 `derived` 换成 `segment`。
- 平台能力探测：Android 上报可用解码实例数，决定降级阈值。

## 6. 两个 Spike

### Spike A · seek 到区段头的观感（判决定 5）—— **已完成，结论成立**

结果已落盘：[VideoBgaSeekSpikeVerification.md](VideoBgaSeekSpikeVerification.md)。要点：

- 解码器级：区段头 seek **帧就绪 22.5–22.8 ms**，且与关键帧间距无关（0.25 s / 1 s / 4.17 s 相同）。
- 运行时级：玩家实际感觉到 **107–111 ms**；多出的约 84 ms 全部是 `VideoBgaRuntime.cs:55` 那个 80 ms 合并宽限加轮询粒度，**不是解码**。
- **零黑帧**（49 个样本）、**精确落帧**（`playerFrame == round(t×60)`）、**确定性**（同目标两次像素指纹一致）。
- 非关键帧 seek：1 秒 GOP 内免费；4.17 秒 GOP 内慢 2.1 倍（47.4 ms）。
- **失败退路未触发**，但保留：若将来 Android 硬解把 23 ms 抬到不可接受，把 (b) 降级为"派生变体的状态表达"，范围收缩回阶段 1。
- **已在真实素材上复测，且宿主已迁到 Thart**：`-thartSeekSpike` / `-thartSeekSpikeDecoder` 跑在 `ThartEditor.exe` 里，对用户指定的那条 BGA（RIFE 插帧 60fps 版）得到解码器 15 ms / 运行时 104 ms / 零黑帧 / 逐位确定 / PASS。

### Spike B · Android 的解码与降级阈值（判阶段 3）

- **测什么**：同时开两个 VideoPlayer 是否都出画、是否掉帧、内存是否单调上涨；以及连续 20 次切换后是否有解码器泄漏。
- **判据**：能稳定双流 → `双流源` 在 Android 上也可用；否则 Android 固定 `单流源`。
- **注意**：Unity 侧能否直接读到 `getMaxSupportedInstances()` **未验证**；读不到就只能黑盒探测。

## 7. 验收清单（全部必须能自动检查）

1. BGA 与音轨在同一次播放内偏移 ≤ 1 帧。
2. 每次切换：切换前后各 3 帧无黑帧、无上一区段残留（读像素）。
3. Note 主色落在当前 tick 的色板容差内（ΔE ≤ 阈值）。
4. 同一条判定序列重放两次，分支序列一致（确定性）。
5. 每条臂结束后第一帧与主线在 `convergeTick` 的帧逐像素一致，或落在交叉溶解窗口内。
6. 连续 20 次切换后，视频相关内存不单调上涨。
7. 回拖、暂停、重开后分支状态可重现，且不影响判定统计。
8. 降级路径可验证：强制 `单流源` 后，全部条目 1–7 仍通过。

落点：**编辑器侧**验收在 Thart（本方案新建的运行期套件约定：`-thartCapture <dir>` → `<套件>-checks.txt`）；**运行期**验收在 `GeometryRhythmDemo`（既有 `-demoSmoke` + `-demoCapture <dir>` → `player-smoke.txt`）。旧制谱器的 `-chartEditorSmoke` 那套不再使用。

## 8. 预算

| 项 | 上限 | 依据 |
|---|---|---|
| 主片（125 s / 1080p60） | ≤ 120 MB | 实测 0.905 MB/s（F8），125 s ≈ 113 MB；关键帧放宽到 `-g 60` 可再省约 12% |
| **真实素材实测**（124.1 s / 1080p60，RIFE 插帧后） | **107.0 MB** | `-crf 23 -preset slow` → 7.24 Mbps。同素材 CRF 20 会到 151 MB / 10.2 Mbps，超预算且源片只有 2.6 Mbps |
| 单曲 BGA 总量 | ≤ 250 MB | 主片 + 5 个分叉点 × 3 条臂 × 5 s ≈ 68 MB |
| 单条臂 | ≤ 8 MB | 5 s 1080p60 ≈ 4.5 MB，留余量 |
| 超限处理 | 走 fast-follow / 首启下载，并回来复核本决定 | Unity 手册：额外资源超 1 GB 才自动拆包 |
| 同时存活解码器 | Windows ≤ 2，Android 默认 1 | 决定 8 与 Spike B |
| 视频相关内存 | ≤ 64 MB（估算） | 1080p RGBA 上界约 7.9 MiB/路，另算缓冲 |

**体积是画面规格的价格，不是关键帧的价格**（原判断已被 Spike A 更正）：1080p60 + 无 B 帧是为了画面与 16:9 锚点对齐的一致性；关键帧密度**不买** seek 速度，所以"每 15 帧一个关键帧"这条可以从规范里放宽到 1 秒 GOP。BMS 圈的常规是 480p / 单文件 <40 MB，我们走 1080p60 仍是 4–5 倍——那部分钱买的是画质与视口一致性，不是切换手感。

## 9. 不做的事

- **不做逐 Note 的视频切换**：判定最密每秒十几次，视频容差 200–250 ms，物理不可行（F2）。这条同时是 ADR-0001 的核心。
- **不做默认的文件级换流**：Unity 没有 playlist，换 clip 必然重新 `Prepare()`，官方 Issue Tracker 有黑帧条目。换流仅作 Windows 增强（ADR-0001）。
- **不用带 alpha 的视频层**：Android 内置 VP8 无透明，必须转码。键控（chroma/luma key）留作真分支落地时的备选。
- **不引入全局事件总线**（决定 14）。
- **不把 Note 2D 化**（决定 12）。
- **不投人力到 `BlenderBgaRuntime`**：冻结不删（决定 13）。
- **不做视频景物的深度遮挡**：需要相机/深度 sidecar，超出本方案范围。

## 10. 未验证项与风险

| # | 项 | 影响 |
|---|---|---|
| R1 | **授权未定**：验证素材是第三方 BGA（EBIMAYO - GOODTEK (Rework) [BGA]，B 站 `BV12K4y1P7ps`，125 s / 1920×1080 / 单 P） | 只能本地验证，素材必须放 gitignore 的 `/BGA/`，不得进 `Assets/` 或 `Charts/` |
| R2 | Google Play 的 AAB/资源包下载上限具体数字 | 抓取 `support.google.com` 两次超时，**未验证**。只在决定出版形态时才需要 |
| R3 | Android 硬解实例数实测值；Unity 侧是否有 API 可读 | Spike B |
| R4 | 非 Windows 平台的 Unity VideoPlayer 解码行为 | `VideoBgaRuntime.cs:43-45` 只谈了 WMF 解码器，仓库无平台分支 |
| R5 | 1080p60 双流在目标 Android 机型上的可行性 | AVPro 自述移动端高分辨率双 player 可能不可用 |
| R6 | 离线调色板烘焙的工具链尚不存在 | 阶段 1 的输入依赖它，需要单独排期 |
| R7 | `.buildtmp/bga20/video.mp4` 被 gitignore、无代码引用 | Spike A 把它当 fixture 用过，但它**不是**交付基线，随时可能被清掉；阶段 0 起应改用受控素材 |
| R8 | 同谱面跨平台观感不一致（决定 9） | 产品层面的已知代价，需要在玩家可见处说明或接受 |
| R9 | ~~Thart 零视频代码、`.thr` 无 BGA 槽位~~ | **已解决**（阶段 0a）：Thart 能播 BGA 且能把它打进 `.thr`，往返自检 14 项 PASS。剩余待办见 [ThartBgaBindingVerification.md](ThartBgaBindingVerification.md) §4（BGA 音频可能被装两遍、打包整块读内存） |
| R10 | seek 量测 harness 有间歇性不落盘 | 8 次调用里 2 次 player 约 7 秒退出且未写报告（`exit=0`、日志无异常），单独重跑即成功。**每次只跑一个素材并确认报告存在**；连续启动间留 ≥8 秒 |
| R11 | 解码器级数字只来自 Windows/WMF/单卡 | Android 的 seek 与硬解一个都没测（Spike B 范围），不要把 23 ms 外推 |

## 11. 相关文档

- [CONTEXT.md](../CONTEXT.md) — 术语表
- [ADR-0001：判定反馈走图层模型，不做文件级换流](adr/0001-layer-first-bga-judgement-feedback.md) — 本方案的证据与取舍
- [ADR-0002：制谱器是 Thart，谱面格式权威是 .thr，旧制谱器冻结](adr/0002-thart-is-the-chart-editor.md) — 决定本方案落在哪个程序、哪个容器
- [VideoBgaSeekSpikeVerification.md](VideoBgaSeekSpikeVerification.md) — Spike A 实测：决定 5 成立，关键帧规范可放宽
- [ThartBgaBindingVerification.md](ThartBgaBindingVerification.md) — 阶段 0a：Thart 播 BGA 与 `.thr` 打包，14 项 PASS
- [RhythmDemoBgaRuntimeVerification.md](RhythmDemoBgaRuntimeVerification.md) — 阶段 0b：游玩运行时播 BGA，13 项 PASS；含"远平面只是天空背景"的发现
- [JudgementFeedbackPaletteVerification.md](JudgementFeedbackPaletteVerification.md) — 阶段 1：色板权威与判定挂点，22 项 PASS
- [VideoBgaWorkflow.md](VideoBgaWorkflow.md) — 现有 BGA 交付与转码规范
- [VideoBgaSeamChainVerification.md](VideoBgaSeamChainVerification.md) — 接缝链的实测做法，阶段 2 直接复用
- [ChartDrivenBgaRealtime.md](ChartDrivenBgaRealtime.md) — Unity 视频/音频能力的既有调研
- [ThartChartEditor.md](ThartChartEditor.md) — 制谱器（Thart）的坐标域与录入规范
