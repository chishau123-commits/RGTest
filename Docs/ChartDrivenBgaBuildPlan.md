# 谱面驱动 BGA 生成 — 分块构建计划（修订版）

> **重要更正。** 本文件第一版建立在"编导编译器是核心新工作"这个假设上，是错的。
> 仓库里已经有 `BGA/fusion/` —— 一整套**确定性、谱面驱动、并与 Unity 运行时逐项对拍通过**的渲染器。
> 因此正确的问题不是"怎么用 AI 生成 BGA"，而是
> **"怎么让 AI 接手质感，而不夺走相机和时间轴"**。本文按这个前提重写。
> 配套调研见 [ChartDrivenBgaGeneration.md](ChartDrivenBgaGeneration.md)。

目标形态：**离线预渲染成品 BGA**，保留已验证的相机等价性，用 AI 换材质与光照。

---

## 0. 先确认已有资产（这些不要重建）

| 资产 | 位置 | 状态 |
|---|---|---|
| 纯 numpy 软渲染器，1920×1080@60，1200 帧约 10 分钟（24 线程） | `BGA/fusion/tools/render_bga.py`（48 KB） | 已交付 `FusionSpire_20s.grchart` |
| 相机/轨道数学的**唯一真源**，`VideoChartSpace.cs` 的逐行等价实现 | `BGA/fusion/tools/score.py`（32 KB） | 与 Unity 对拍 |
| 独立复核：不 import `score.py`，从 `chart.json` **重新实现**一遍再提问 | `BGA/fusion/tools/verify_package.py` | `16 passed, 0 failed` |
| 与真实 Unity 运行时逐项对拍 | `BGA/fusion/tools/crosscheck_unity.py` | `RESULT: MATCH` |

对拍实测值（`BGA/fusion/README.md`）：

```
[camera]  worst: position 0.000018 units, rotation 0.0000 deg, fov 0.00000
[notes]   median lane deviation 0.0000 % of the frame, worst 0.4364 %
[rail]    worst world-space difference 0.000004 units
RESULT: MATCH
```

它建立的不变量是：

```
视频里画出来的那条光轨  ==  游戏里 note 滑过的那条轨  ==  玩家看到 note 命中的那个像素
```

**这是整个项目相对所有"AI 生成 BGA"尝试的结构性优势。任何让模型自由发挥运镜的方案都会摧毁它。** 下文的每一块都以保住它为前提。

`verify_package.py` 的 16 项里还有两条是现成的验收工具，后面直接复用：

- `the largest movement in the picture is the drop itself`（drop 处运动量 16.6，build 段 4–8）
- `the picture calms through the build and opens up after the drop`

---

## 1. 总链路（修订）

```
chart.json ──┬─→ render_bga.py --control ──→ control_video（几何/深度/边缘，无 bloom 无调色）
             │                                          │
             │                                          ↓
             │                            Wan22FunControlToVideo / WanVaceToVideo（原生节点）
             │                                          │
             └─→ beat grid + sections ──→ 段落表 ────────┤
                                                        ↓
                                          context window 重叠融合（latent 空间）
                                                        │
                                                        ↓
                                          ffmpeg 封装 ──→ video-bga 包 ──→ .grchart
```

**相机与切点是输入，不是模型的输出。** 这是全部设计的关键。

---

## 2. 块 ①　控制视频导出

**改哪里**：`BGA/fusion/tools/render_bga.py` 加一个 `--control` 输出模式。

**做什么**：只渲染几何、深度、边缘，**跳过 bloom 与调色**。产物是给模型当条件用的，不是给人看的。

**为什么这样能拿到精确运镜**（这条是实测结论，不是推测）：

`comfy_extras/nodes_camera_trajectory.py` 里的 `WanCameraEmbedding`，`camera_pose` 是恰好 9 个预设的 COMBO：

```
Static / Pan Up / Pan Down / Pan Left / Pan Right / Zoom In / Zoom Out
/ Anti Clockwise (ACW) / ClockWise (CW)
```

且运动是**线性匀速斜坡**，源码就是这一行：

```python
_angle = (i/n)*speed*(CAMERA_DICT["base_angle"])*angle
```

**它不接受自定义逐帧位姿。** 所以"用节点参数把谱面相机喂给模型"这条路是死的。
**把相机做成输入视频才走得通** —— 而你的 `render_bga.py` 已经能逐帧算出这台相机。

**验收**：`--control` 输出的帧数、时长与成片**逐帧对齐**；渲染速度显著快于成片。

**注意**：控制视频不能太"干净"。太干净时模型没有足够细节可接管，会退化成近似原样输出或糊掉。深度的层次保留比边缘干净更重要。

---

## 3. 块 ②　段落与 downbeat 分析

现状：`BGA/fusion/tools/audio_analysis.py` 的 `refine_tempo()` **只在已知 BPM 附近 ±2 做精修**（`bpm_lo=178.0, bpm_hi=182.2`），不出 downbeat、不出段落。

**推荐**：`beat_this`（CPJKU）—— **代码与权重都是 MIT**，返回 beats + downbeats（`beats, downbeats = file2beats(path)`），有官方 CPU 回退路径，模型 78 MB / 8.1 MB。

**不推荐**：
- **essentia 是 AGPL-3.0** —— 仓库已经有一份 ffmpeg GPL 合规待办（`BGA/tools/bin/FFMPEG-NOTICE.txt`），不要再引入第二个许可证负担
- **madmom 在 PyPI 上是死的** —— 官方 README 自己写着仅支持 Python<3.10 / numpy<1.20，本项目是 3.12 + numpy 2.5.3，装不上

**关键约束（务必遵守）**：`BGA/tools/pylibs` 现在**只有 numpy**。引入 `beat_this` 会拉进 PyTorch（数 GB），破坏"可移植、零重依赖"这个现有性质。

> **分析工具离线跑，结果以 JSON 提交进仓库，不进运行时依赖。**
> 输出写到 `BGA/fusion/analysis/`（那里已经有 `grid.json` / `song.json` 的先例）。

**验收**：与现有 `audio_analysis.py` 的拍网格交叉验证，两者应当一致；段落边界应当落在 `chart.json` 的 `sections[].startBeat` 附近。

---

## 4. 块 ③　生成

**节点**（已在本机 ComfyUI 源码中逐条确认存在）：

| 节点 | 用途 |
|---|---|
| `Wan22FunControlToVideo` | 吃 `control_video` |
| `WanVaceToVideo` | 备选，控制能力更强、参数更多 |
| `WanFirstLastFrameToVideo` | 需要钉首尾帧时用 |

**接缝**：改用 **context window 在 latent 空间重叠融合**，替代现在的 ffmpeg 交叉溶解。

理由是你自己记录在案的：每个 take 只有 181 帧 @30fps = 6.033 秒，**比 6 秒窗口只多 33 毫秒**，所以溶解加不长；硬切 7.18×，5 帧溶解后 6.0s 仍有 2.33×。让模型在 latent 空间重叠融合，不再受"素材不够"限制。

对应节点 `WanVideoContextOptions`（`context_overlap=16`、`fuse_method=pyramid`）——注意这是 **kijai `ComfyUI-WanVideoWrapper` 的包装器节点，不是原生的**，本机未安装，需要一并部署。

**⚠️ 前置条件（当前未满足）**：`Docs/CloudComfySetup.md` 的云端模型清单里**没有 Fun-Control / VACE 检查点**。本机 `models/` 下的 wan / fun / vace / umt5 目录**全是空的**——所有生成都在 AutoDL 上做，这份权重必须补下，是第 3 步的硬前提。

**分辨率更正（可能省一大笔钱）**：本项目文档写 "Wan 2.2 TI2V 5B 原生生成 640×352"，并计划"在更高显存机器用 1280×704 重跑"。这是错的。已核实 `comfy_extras/nodes_wan.py` 里：

```python
io.Int.Input("width",  default=1280, ...)
io.Int.Input("height", default=704,  ...)
```

**640×352 是 ComfyUI 默认工作流的尺寸，不是模型的能力上限。** 官方口径 720P 就是 `1280*704` @24fps，需 ≥24GB VRAM（4090 即够）。同一台 4090、同一份白模就能出 1280×704，**不需要"更高显存机器"**。

> 建议在花任何大钱之前，先做这一项单点验证。

---

## 5. 块 ④　汇编与验收

`assemble_bga20.py` 里的 `count_frames()` / `probe()` / sha256 / manifest 生成继续留用（用 stream-copy 数帧、用正则解析 ffmpeg banner 因为没有 ffprobe，都是对的）。

两条不变量必须保住：

- **帧锁**：输出帧数 == 歌长 × fps，逐帧断言
- **歌锁**：每个镜头第一帧落在它的 `startSeconds` 上

`BGA/fusion/README.md` 记录了交付时的编码取舍，直接沿用：H.264 High / yuv420p / 60fps / BT.709 / 无 B 帧 / 每 15 帧关键帧 / faststart，外加 `-refs 1 -tune fastdecode -crf 26` 与 0.45 px 高斯预滤。

> 那条经验值得记住：上一版 31 Mbps / 102 MB 时，Unity 的 WMF 解码器在编辑器里追不上 `VideoBgaRuntime.Evaluate` 的 0.20 s 容差，表现为不断 seek → 再落后 → 再 seek，看起来就是"卡"。**BGA 的体积上限不是文件大小，是解码器能不能跟上。**

**验收工具**（全部现成）：`analyze_seams.py` 量接缝倍数，目标与"正常帧间差"同量级（< 2×）；`verify_package.py` 的 16 项检查。

---

## 6. 块 ⑤　回灌

现成，不用新写。产出 `video-bga` 包（`manifest.json` + `video.mp4` + `audio.wav`），制谱器 **Files → Import / replace video**，交付用 **Export .grchart package**。

注意 `videoBga.timeOffsetSeconds` 与 `audioOffsetSeconds` 是两个独立参数，别混。

---

## 7. 关于确定性 —— 更正第一版的过度承诺

第一版计划写了"种子从谱面哈希派生 → 同一张谱永远同一个视频"。**这句话对 AI 部分是不成立的**，要收回。

| 部分 | 可复现性 |
|---|---|
| 控制视频 | **逐比特可复现**（numpy，无随机性） |
| 相机、切点、拍对齐 | **精确可复现** |
| AI 输出的**像素** | **不可复现** |

证据就在你自己的仓库里：`selected-takes.json` 记录了第 1 段用 CFG 4、第 5 段用 CFG 3.5、其余 CFG 5，`Docs/VideoBgaWorkflow.md` 也写着"正式视频需看结果选片"。

还有一个**源码级确认的不可复现点**：`WanMoveTrackToVideo` 内部调用 `torch.randperm` 且**节点未暴露 seed**，所以它**即便固定 seed 也不可复现**。这是不选轨迹控制、而选控制视频的又一个理由（`Wan22FunControlToVideo` / `WanVaceToVideo` 走确定性 VAE 编码 + conditioning，无 `randperm`）。

> **结论：可复现的是结构，不是像素。** 这恰恰是本方案的全部立论基础——
> 把必须可复现的部分（相机与时间）从 AI 手里拿走，只让 AI 负责允许有随机性的部分（质感）。

---

## 8. 明确不做的事

- **Wan2.2-S2V** —— 口型/数字人工具（官方示例 prompt 是 `"a person is singing"`，80 GB 显存），对器乐无信号
- **Ovi** —— 它是**生成**音频而非接收音频，自己 TODO 里 `[ ] Reference voice condition` 还没勾
- **`WanCameraEmbedding` 做谱面运镜** —— 只有 9 个预设 + 匀速斜坡，见块 ①
- **`WanMoveTrackToVideo` 做需要严格复现的运镜** —— 无 seed 的 `torch.randperm`
- **essentia** —— AGPL-3.0
- **MotionCtrl / TrajectoryCrafter** —— 调研阶段**未能核实一手仓库**，且本项目 ComfyUI 节点面里也没有，不纳入计划

---

## 9. 落地顺序

每步可独立验证，失败不阻塞下一步。

| 步 | 动作 | 验收 |
|---|---|---|
| 1 | `render_bga.py` 加 `--control` 模式 | 帧数/时长与成片逐帧对齐；速度显著更快 |
| 2 | 单点验证 TI2V-5B 在 `1280*704` 的真实吞吐与显存 | 官方"5s 720P < 9min @≥24GB"是否复现。**花钱前先做这个** |
| 3 | 补 Fun-Control / VACE 权重（本机与云端都没有） | `Wan22FunControlToVideo` 能加载并出片 |
| 4 | 20 秒对照实验：老管线（6s 片段 + ffmpeg 溶解）vs 新管线（control video + context windows） | 用现有 `analyze_seams.py` 比较，目标 < 2× |
| 5 | 离线跑 `beat_this` 补 downbeat 与段落，结果落 JSON 进仓库 | 与 `audio_analysis.py` 拍网格交叉验证 |
| 6 | 扫 `strength` 找几何保真度与质感的最佳点 | 固定控制视频，量化输出与控制的差异 |

**第 1、2 步是当前性价比最高的两件事**：第 1 步不花钱、不依赖任何新权重，且它是后续所有步骤的输入格式；第 2 步可能直接省掉一次"换更高显存机器"的开支。

**保底不丢**：任何时候 AI 阶段产不出可用结果，`BGA/fusion` 的确定性管线都能独立交付一条与谱面完全一致的 BGA。这是结构性优势，不要为了 AI 质感把它丢掉。

---

## 10. 相邻结论与环境约束

以下几条来自 [ChartDrivenBgaRealtime.md](ChartDrivenBgaRealtime.md) 的调研，已逐条复核（含一手来源）。它们不改变上面的方案，但改变**边界条件**。

### 10.1 ⚠️ 产品级约束：osu! 明文禁止 AI 生成的视频

osu! 通用 ranking criteria 的 AI policy 原文（[一手来源](https://raw.githubusercontent.com/ppy/osu-wiki/master/wiki/Ranking_criteria/en.md)）：

> **Videos which are substantially AI-generated must not be used.**
> - Unacceptable examples include:
>   - Direct outputs from generative AI programs.
>   - **Clips from generative AI programs which are edited together.**
>   - Videos in which sections, characters, or background images were created using generative AI programs.

**"多个 AI 片段剪辑拼接"正是本方案的产品形态**，属于明列的不接受项。同一条 policy 也规定谱面的 hit objects / hitsounds / timing 必须完全由人直接输入。

含义：

- **不影响自研游戏的运行**，本方案照做
- **但只要打算发布到 osu!，AI BGA 不能随谱提交** —— 那时必须用 `BGA/fusion` 的纯确定性渲染器出图（碰撞检测：确定性渲染是代码产物，不属于"generative AI program"）
- 顺带：osu! 视频规格是 H.264 `.mp4`、**≤1280×720**、**必须去掉音轨**。fusion 成片是 1920×1080，若将来要移植需要另出一版

> 这条值得在决定"AI 质感是否值得投入"之前先想清楚。如果目标包含 osu! 发布，那么 AI 路线只能用于自研游戏内，**fusion 确定性渲染器才是可发布的那个**——这反过来抬高了块 ① 的价值。

### 10.2 本项目是 Built-in Render Pipeline，VFX Graph / Shader Graph 不可用

已复核：`ProjectSettings/GraphicsSettings.asset` → `m_CustomRenderPipeline: {fileID: 0}`，Unity `2022.3.62f3c1`，`Packages/manifest.json` 里没有 URP / VFX Graph / Shader Graph / Sentis。

官方口径：VFX Graph "uses a **Scriptable Render Pipeline**… uses on **compute Shaders**"；Shader Graph 是 SRP 专属包。

**更正**：本项目若要做实时反应层，只能用**手写 Shader + 粒子系统**，不能用 VFX Graph / Shader Graph 的图形化音频绑定。VFX Graph 确实有官方的 Audio Spectrum to AttributeMap binder，但需要 URP/HDRP。

### 10.3 不要用 FFT 做节奏同步

Unity 官方对 `GetSpectrumData` / `GetOutputData` 的原话：**"isn't suited for critical or chronological, real-time data analysis or processing, or scenarios where you require low latency"**。样本精确路径只有 `OnAudioFilterRead`（音频线程，约 20ms 一块，禁止调用多数 Unity API）。Unity **没有**节拍检测 API，**没有**可配置窗/对数刻度 FFT（只有 6 个固定 `FFTWindow` 枚举，bin 线性）。

→ **同步走 `SongClock` + `sections[]` / `notes[]`，FFT 只做装饰。** 仓库现状已经是对的，不要为了"音频反应"把它改坏。

### 10.4 视频层不能逐音符 seek

官方口径：seek "may be noticeably long"，且重复设 `time` 会**排队串行**执行，`time` 只在帧真正显示后才稳定。

正解：`timeUpdateMode = Audio DSP Time` + `skipOnDrop = ON`（代价是靠丢帧追时钟）。Unity **没有**硬解开关。H.264 无 alpha。

→ 这印证了 `GeometryChartStudio.md` 里"暂停时直接定位、连续拖动只保留最新 Seek"的做法是对的，也解释了 `BGA/fusion/README.md` 记录的"卡"其实是解码器追不上 0.20 s 容差。

### 10.5 三条低成本的谱面表现力改进（与本方案无关，但值得做）

- **Cytoid / Cytus II 的 storyboard 规范是完整公开的 JSON，且支持按具体音符触发**：`"triggers":[{"type":"noteClear","notes":[4],"spawn":[...]}]`，时间可直接锚定音符（`"start:<Note ID>"`）。**osu! 做不到这个。** 建议把 `effectClips[]` 扩展成可锚定音符。
- **Beat Saber 的 lightshow 事件是 `(beat, targetGroup, action, floatParam)` 四元组**，一个结构覆盖灯光/旋转/位移/换色。建议 `effectClips[]` 照抄这个四元组，不必为每种效果新增数据结构。
- **加一个"miss 时显示的 POOR 层"** —— 见下。

#### POOR 层：BGA 体系里唯一的玩法反馈通道

BMS/IIDX 的 BGA 分四层：`#xxx04` BGA-BASE、**`#xxx06` BGA-POOR（miss 时显示）**、`#xxx07` 中间层、`#xxx0A` 顶层。整个调研里**唯一确证**的"玩法反应式背景"就是这个 POOR 层——其他商业作品（DJMAX 等）的"反应"只是解锁门控。

**好消息是你的代码已经准备好了。** 已核实 `Assets/RhythmDemo/Runtime/ChartData.cs`：

```csharp
// screen, scene, judgement or a SceneObjectData id
public string target = "screen";
```

`EffectClipData.target` **已经支持 `"judgement"`**，结构上就差把这些片段渲染成一层。

#### 顺带：判定等级可以再细分，而 error 已经在算了

现状 `NoteResult { Pending, Perfect, Good, Miss, Skipped }` —— 5 个值，其中只有 3 个真实等级。

而 ADOFAI 的 `Set Conditional Events` 支持 **8 种**：`Loss / Too Early / Too Late / Early / Late / Perfect / EPerfect / LPerfect`，**比 osu! 和 Cytoid 都细**。

关键在于：**你的 `JudgementEngine` 内部已经算出了带符号的 error**（`Resolve(n, error <= PerfectWindow ? Perfect : Good)`，drag 分支还显式判 `error < 0`）。所以 Early/Late 与 EPerfect/LPerfect 的区分**不需要新的判定逻辑，只需要把已有的中间量暴露成一个等级**。

> 注意 ADOFAI 的 `OnHit` / `OnLand` / `OnFalling` **不存在**，不要照着这些名字设计。

### 10.6 不要指望的几件事

- **Sentis 端上生成背景 = 三重否决**：现名 `com.unity.ai.inference` 且要求 Unity 6+（本项目 2022.3 装不上）；不支持 `GroupNormalization`（扩散 UNet 标配）与 `If/Loop/Scan` 控制流；不能导入已量化的 ONNX
- **LASP 不支持 Android**：官方 README 原文 "LASP only supports desktop platforms (Windows, macOS, and Linux)"（原生 libsoundio）。Keijiro 整个 I/O 栈里能上 Android 的只有 `KlakNDI` 与 `Minis`
- **`KlakVJ` / `AlembicUnity` / `AlembicExporter` 不存在**（404）。`Reaktion` 最后 commit 是 2015-07-07 且无 URP 分支
- Keijiro 的包**安装方式只有 scoped registry**（`jp.keijiro` → registry.npmjs.com），作者从不提供 git URL

---

## 11. 可直接抄的架构先例：自动填充 + 首次人工覆盖即交还控制权

已逐行复核 StepMania `src/Background.cpp` 的 `BackgroundImpl::LoadFromRandom()`。**两个独立的商业级系统用了同一套架构**：

| 系统 | 自动填充的触发 | 交还控制权的条件 |
|---|---|---|
| StepMania | 谱面**没有**人工写的 `#BGCHANGES` 时（`pSong->HasBGChanges()` 为假，`LoadFromSong()` 走 `else` 分支） | 作者写下第一条 BGChange |
| Rock Band（Magma 编译器） | `VENUE` MIDI 轨为空时自动生成整条相机/灯光轨 | 放进第一个相机或灯光事件后，自动生成关闭 |

**这就是本方案该有的产品形态**：确定性渲染器先按谱面结构把整条 BGA 铺满，谱师只在不满意的镜头手工覆盖。

### 11.1 切点规则可以直接抄

StepMania 的自动切点只用了两个判据，而这两个判据的输入**你的 `chart.json` 里都有**：

```cpp
// change BG every time signature change or 4 measures
j += RAND_BG_CHANGE_MEASURES * ts->GetNoteRowsPerMeasure();

// change BG every BPM change that is at the beginning of a measure
if ((bpm->GetRow() - ts->GetRow()) % ts->GetNoteRowsPerMeasure() == 0)
```

| StepMania | 你的对应字段 |
|---|---|
| `SEGMENT_TIME_SIG` | `sections[{startBeat, name}]` —— **且你的更丰富**，带人工写的段落名 |
| `SEGMENT_BPM` | `tempos[{tick, bpm}]` |
| `GetNoteRowsPerMeasure()` | `ticksPerBeat × 拍号` |

注意第二个判据的精确含义：**不是"每个 BPM 变化都切"，而是"BPM 变化且正好落在小节起始才切"**。这个 `%` 取模条件就是"落在小节线上"的判定，值得照抄——它避免了变速歌里到处乱切。

`RAND_BG_CHANGE_MEASURES` 是 theme metric（注释写 4 小节），**可配而不是硬编码**。建议同样处理：把"每 N 小节兜底切一次"做成配置项。

### 11.2 ⚠️ 修正我自己的一处计划：种子应该绑"歌曲"而不是"谱面"

StepMania 的种子是：

```cpp
// Pick the same random items every time the song is played.
RandomGen rnd( GetHashForString(pSong->GetSongDir()) );
```

注意它用的是 **`GetSongDir()`——歌曲目录，不是谱面文件**。

这是刻意的：**同一首歌的所有难度共享同一个背景序列**，玩家换难度看到的是同一套画面。

我上一版计划里写的是从 `chart.json` 派生种子，那会让同一首歌的不同难度得到**不同的**背景——是个错误的设计。应当改成：

```
seed = hash(audioFile + songTitle)      // 歌曲级，所有难度共享
```

这也顺带让 `.grchart` 的重新导出不会改变画面。

### 11.3 一条顺带的性能取舍

同一段源码里有一行注释值得注意：

```cpp
// Don't fade. It causes frame rate dip, especially on slower machines.
```

**StepMania 在自动切点处明确不做淡入淡出，理由是掉帧。**

这跟你 `BGA/fusion/README.md` 记录的"卡"是同一类问题——不是画面不好看，是解码/渲染跟不上。所以接缝处理的第一优先级永远是**帧锁 + 歌锁**，溶解只在帧锁已经成立的前提下才谈。

### 11.4 一处需要保留的差异

StepMania 的随机背景来自一个**全局素材池**（`GetGlobalRandomMovies()`），所以"同一首歌同一个序列"是在**同一台机器的素材池**前提下成立。

本方案不同：素材由 `render_bga.py` 按谱面**渲染**出来，不依赖本机素材池。所以你的确定性比 StepMania 更强——**逐比特**，而不只是"同一序列"。这是可以对外承诺的性质，值得在文档里写清楚区别。
