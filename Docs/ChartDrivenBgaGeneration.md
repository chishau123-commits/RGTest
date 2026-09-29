# 谱面驱动 BGA 自动生成 — 一手资料调研

调研目标：**谱面（chart.json）已完成后，用谱面数据 + 歌曲音频作为条件，让 AI 自动生成背景视频（BGA）**。

本文只采信一手来源（官方仓库、官方文档、模型卡、带 arXiv 编号的论文）。每条非显然结论都带来源链接。抓不到原文的一律标 **未验证**，不做推测。文中明确区分「我抓到并读到了」与「我认为它存在」。

已核实的本地事实（不是推测）：

| 项 | 实测值 | 来源 |
|---|---|---|
| 制谱器谱面格式 | `version` / `ticksPerBeat: 480` / `endBeat` / `entrySeconds` / `tempos[{tick,bpm}]` / `stagePath{unitsPerSecond,points[x,y,z,roll]}` / `paths[{id,roll,offsetKeys,screenAnchors}]` / `sections[{startBeat,name,placements}]` / `cameraKeys[{beat,distance,height,fov,orbit,roll,easing,usePathPose,useWorldPose}]` / `notes[{id,tick,pathId,action}]` / `sceneObjects` / `effectClips[{kind,target,seed,easing}]` / `cameraMotionClips[{kind,seed,keys}]` / `videoBga` | `Assets/RhythmDemo/Runtime/ChartData.cs`、`BGA/Video20/Firefly20.chart.json` |
| 现有确定性渲染器 | `BGA/fusion/tools/render_bga.py`，**纯 numpy 软渲染器**，1920×1080 / 60fps，带 zbuffer / bloom / grade / vignette+grain / chromatic aberration；相机与 note 轨道全部由 `chart.json` 重建 | `BGA/fusion/README.md` |
| 渲染器与 Unity 运行时的等价性 | 已经在真实 Unity 运行时逐项对拍：`position 0.000018 units`、`rotation 0.0000 deg`、`fov 0.00000`、note 车道偏差中位数 `0.0000 %`、`RESULT: MATCH` | `BGA/fusion/README.md` |
| 现有节拍分析 | `BGA/fusion/tools/audio_analysis.py`，**numpy only**，只在**已知 BPM ±2 范围内精修**（`refine_tempo(bpm_lo=178.0, bpm_hi=182.2)`），无 downbeat、无段落切分 | `BGA/fusion/tools/audio_analysis.py` |
| 本地 Python 依赖 | 系统 Python 3.12，仅 vendored `numpy 2.5.3`（`BGA/tools/pylibs`），**没有 torch** | 实测 `python -c "import numpy"` / `Get-ChildItem BGA\tools\pylibs` |
| 本地 ffmpeg | 7.1-essentials（gyan.dev，GPL 构建，`--enable-gpl --enable-version3`） | 实测 `ffmpeg -version` |
| 可用 ComfyUI 节点面 | 3761 个节点类，来自本项目 Comfy Cloud v0.272.0 的 `object_info` 实拍转储 | `.buildtmp/object_info.json`（实测 `json.load` → 3761） |

**本报告最重要的一条结论先说**：这个项目已经拥有「谱面数据驱动视觉」的**确定性、逐帧精确、且已被 Unity 运行时验证过的**实现（`BGA/fusion`）。因此正确的问题不是「怎么用 AI 从零生成 BGA」，而是「**怎么让 AI 只接管画面质感，而不夺取相机与时间轴**」。第 5、6 节按这个前提给出方案。

### 修订记录（v2）

第 4 节在后续的一手规格核对后做了**实质性更正**。为避免误用，更正项集中列在这里；正文对应位置也保留了 `⚠ 本节为 v2 修订` 标注：

| 初版说法 | 更正后 | 依据 |
|---|---|---|
| BMS 通道 `01`=layer 1、`02`=layer 2、`03`=BGA | **错**。`01`=BGM、`02`=小节长度、`03`=整数 BPM；BGA 层是 `04`(Base)/`06`(Poor)/`07`(Layer)/`0A`(Layer2) | 1998 原始规格、`bms_rs` 源码 |
| bmson 在设计上**放弃了 BGA** | **错**。bmson 规范的 `bga` 字段完整存在（`bga_header`/`bga_events`/`layer_events`/`poor_events`），1.0.0 只是改名；规范原文 "Currently, BGA specification is just compatible with BMS." | bmson-spec |
| TouchDesigner 的 `Beat CHOP` 做**实际节拍检测** | **错**。Beat CHOP **无音频输入**，BPM 由 **Beat Dialog 手动 tap** 得到；Resolume 同样靠手耳 tap | Derivative 官方文档、Resolume 官方文档 |
| 主流商业节奏游戏的 BGA「绝大多数是预渲染」 | **过度概括**。Rock Band 的 `VENUE`/`BEAT`/`EVENTS` 轨是**真正的谱面数据驱动**，且 Magma 可自动生成；FNF 官方也已内置。确认预渲染的是 Clone Hero | RBN 官方文档、FNF 官方文档 |
| DJMAX「BGA 是预渲染、与 note 无绑定」 | **降级为未验证**（bmson 规范只证实呈现方式） | bmson-spec |
| StepMania 背景效果有 C++ `Effect`/`RageEffect` 类 | **未能在 master 确认**（`src/Effect.h` 404；`BackgroundEffects/` 实为 `.lua`） | StepMania 源码 |
| StepMania `#BGCHANGES` 字段顺序未验证 | **已结清**：11 字段顺序 + `rate` 语义（`fDeltaTime *= m_fUpdateRate`） | `NotesLoaderSM.cpp` / `ActorFrame.cpp` |

另外**新增**了一条本项目最该学的先例：StepMania 的 `BackgroundImpl::LoadFromRandom()` 用**拍号变化 + 小节边界的 BPM 变化**自动生成背景切换表，且用歌曲目录 hash 保证**确定性**（见 4.3）。

---

## 1. 音频/音乐条件化的视频生成 — 现在到底有什么

### 1.1 关键区分：音频是**输入**还是**输出**

这是最容易搞错的一点，也是选型的分水岭。

| 模型 | 音频方向 | 音频条件实际控制什么 | 能否用于纯器乐 BGA |
|---|---|---|---|
| **Wan2.2-S2V-14B** | **输入** | 口型/说话人表演（human-centric） | **不能** |
| **Ovi** | **输出**（生成音频） | —（自己造声音） | **不能**（但可用于反向场景） |
| **LTX-2** | **输入**（官方列为多模态输入之一） | 官方称"同步音视频"；具体控制粒度 **未验证** | **可能可以**，见 1.3 |
| **Wan VACE** | 无音频 | — | — |
| **Veo 3 / 3.1** | 见 1.4（**未验证**） | 没有证据表明音频是输入；"native audio"通指**输出** | **不能**（且闭源付费） |
| **WanSoundImageToVideo**（ComfyUI 原生节点） | **输入** | 有 `audio_encoder_output` 输入 | **待验证**，见 1.5 |

### 1.2 Wan2.2-S2V-14B：明确的口型工具，对器乐无用

一手证据来自官方 README：

```bash
python generate.py --task s2v-14B --size 1024*704 \
  --ckpt_dir ./Wan2.2-S2V-14B/ --image "examples/i2v_input.JPG" --audio "examples/talk.wav"
```

- 官方描述："an **audio-driven cinematic video generation** model"，输入是 `--audio` + `--image`（+ 可选 `--prompt`）。
- 官方明确它做**人**：示例提示词是 `"a person is singing"`，且提供 `--pose_video` 做 pose-driven 生成。
- 长度：**不设 `--num_clip` 时按输入音频长度自动调整**。
- 分辨率：480P & 720P；单卡需 **≥80GB VRAM**。
- 许可证：Apache-2.0（Wan2.2 全系），且官方写明 "We claim no rights over your generated contents"。
- 来源：[Wan-Video/Wan2.2 README](https://github.com/Wan-Video/Wan2.2)；仓库实测 **17,648 stars / Apache-2.0 / pushed 2026-09-21**（GitHub API）；论文 [arXiv 2503.20314](https://arxiv.org/abs/2503.20314)。

**结论**：S2V 是**口型/数字人**工具。它把音频映射到人脸与嘴部运动。对一首没有人声的器乐曲，它没有任何可用信号，**本项目不应采用**。即使有人声，它也不会把"节拍"变成运镜。

### 1.3 LTX-2：唯一一个把音频当**输入**的开放权重候选

官方新闻稿（Lightricks，2026-01-05）原文要点：

- "**Synchronized Audio and Video Generation** — Visuals and sound are created together in a single process"。
- "supports **multimodal inputs — text, image, audio, depth maps, and reference video**"。
- "Native 4K resolution at up to **50 frames per second**"，"clips up to **10 seconds**"。
- "**Multi-keyframe conditioning, 3D camera logic**, and LoRA fine-tuning provide **frame-level control**"。
- 成本："up to **50% lower compute cost** than competing models"。
- 来源：[LTX-2 Is Now Open Source（官方新闻稿）](https://ltx.io/newsroom/ltx-2-is-now-open-source-full-model-weights-released)、[官方许可页](https://ltx.io/model/license)。

**必须诚实标注的两点**：

1. **权重是否真的开放，官方口径自相矛盾。** 同一篇新闻稿标题写 "Full Model Weights Released"，正文却写 "Model weights **will be released later this fall**"，并称 "Core components of LTX-2 are available on GitHub"。**LTX-2 权重当前可下载状态：未验证。** 官方 GitHub 指向 `Lightricks/LTX-2`。
2. **"音频作为输入"到底控制什么，未验证。** 官方只给了 "synchronized" 这类营销表述，没有说明音频是驱动口型、驱动节拍、还是仅作风格/氛围参考。**不要假设它能把 kick drum 变成画面脉冲。**

已经在 ComfyUI 里可以看到真实节点（本项目 `object_info` 实拍，见 1.5 与第 3 节），其中 `LtxApi25AudioToVideo` 的官方节点描述是：

> "Generate a video **driven by an audio track**, with an optional first frame image."

输入为 `audio` / `model` / `prompt` / `seed` / 可选 `image`。注意它属于 `comfy_api_nodes.nodes_ltxv`，即**走 Comfy 云 API 的付费节点**，不是本地推理节点。长度与分辨率参数**未在该节点暴露**（服务端决定），因此**未验证**。

另一个容易误读的节点：`LTXVReferenceAudio` 的官方描述是 "Set reference audio for ID-LoRA **speaker identity transfer**"。**这是音色/说话人身份迁移，不是音乐条件化**，对 BGA 无用。

### 1.4 Veo 3 的原生音频是**输出**

Veo 3/3.1 的 "native audio" 指的是模型**生成**带声音的视频（对白、音效、环境声），不是接受你的歌曲作为条件。**未验证**：本轮抓取没有拿到 Google 官方模型卡原文（`web_fetch` 对该域失败）。**按现有证据，Veo 3 不能用作"用歌曲驱动画面"的工具，且它是闭源付费 API。** 如需引用此结论，请先补官方模型卡。

### 1.5 本项目 ComfyUI 里**已经存在**的音频条件节点（实测）

以下是从 `.buildtmp/object_info.json` 实测提取的，不是推测：

| 节点 | 所属模块 | 音频角色 | 备注 |
|---|---|---|---|
| `WanSoundImageToVideo` | `comfy_extras.nodes_wan`（**原生**） | **输入** `audio_encoder_output`（可选） | 另有 `ref_image` / `control_video` / `ref_motion`，默认 `length=77` |
| `WanSoundImageToVideoExtend` | `comfy_extras.nodes_wan`（原生） | 输入 | 续接长视频 |
| `WanDancerVideo` | `comfy_extras.nodes_wandancer`（原生） | **输入** `audio_encoder_output`（可选） | 默认 `length=149`、480×832（竖屏），音频驱动舞蹈 |
| `WanDancerEncodeAudio` / `WanDancerPadKeyframes` | `comfy_extras.nodes_wandancer` | 输入 | 音频编码与关键帧补齐 |
| `WanVideoAddS2VEmbeds` | `ComfyUI-WanVideoWrapper` | **输入** `audio_encoder_output` | 参数：`frame_window_size=80`、`audio_scale`、`pose_start_percent`、`pose_end_percent`、`ref_latent`、`pose_latent`、`enable_framepack` |
| `WanVideoImageToVideoSkyreelsv3_audio` | `ComfyUI-WanVideoWrapper` | 输入 | SkyReels v3 音频路径 |
| `WanVideoImageToVideoMultiTalk` / `WanInfiniteTalkToVideo` / `WanVideoLongCatAvatarExtendEmbeds` | `ComfyUI-WanVideoWrapper` | 输入 | 数字人/对白向 |
| `WanVideoEmptyMMAudioLatents` / `OviMMAudioVAELoader` | `ComfyUI-WanVideoWrapper` | 音频侧 | MMAudio（音频**生成**） |
| `LtxApi25AudioToVideo` | `comfy_api_nodes.nodes_ltxv`（云 API） | **输入** | 见 1.3 |
| `LTXVReferenceAudio` | `comfy_extras.nodes_lt`（原生） | 输入 | **说话人身份迁移，非音乐** |
| `LTXVAudioVideoMask` / `LTXVSetAudioVideoMaskByTime` | KJNodes / ComfyUI-LTXVideo | 时序掩码 | 按时间区间分别遮罩音/视频 latent |
| `AudioBPMDetector` / `FL_Audio_BPM_Analyzer` / `AudioGetTempo` | AudioTools / Fill-Nodes / audio-separation-nodes | 分析 | 见第 2 节 |
| `SoundReactive` / `AudioReactiveTransform` / `FL_Audio_Reactive_*` | KJNodes / nodesweet / Fill-Nodes | 分析→参数 | **这是最值得偷的 VJ 式管线**，见第 4 节 |

**判定**：真正"把歌曲当条件"的开放权重路径在 ComfyUI 里目前只有两条半 ——
1. `WanSoundImageToVideo` / `WanDancerVideo`（原生，音频编码器输入，但训练目标是**人**）；
2. `LtxApi25AudioToVideo`（云付费，口径未验证）；
3. 半条：`WanVideoAddS2VEmbeds`（同样是人体驱动）。

**没有任何一个开放权重模型是"音乐→抽象画面"的通用音乐视频模型。** 这一点必须成为方案设计的前提，而不是希望。

---

## 2. 节拍 / downbeat / 结构分析，以及分轨

### 2.1 本项目现状与真实缺口

`BGA/fusion/tools/audio_analysis.py` 已经做得不错，但它是**精修器，不是检测器**：

```python
def refine_tempo(env, rate, bpm_lo=178.0, bpm_hi=182.2, step=0.005):
```

它以 `grid_capture()`（相位直方图）在**已知 BPM 的 ±2 范围内**搜索拍周期与相位，并做抛物线峰值精修。它能给出 `bpm` / `beat_seconds` / `phase_seconds` / `capture`，也就是**一条刚性等间隔拍网格**。

它**做不到**的三件事：
1. **无法从零发现 BPM** —— 必须先知道大概 180 BPM。
2. **没有 downbeat（小节线）** —— 只有 beat，没有 4/4 的"第 1 拍"。而"炸点在第 36 拍"这类编排需要小节感知。
3. **没有段落切分** —— 无法自动产出 intro / build / drop / outro 的结构标签。
4. **不支持变速曲**（`tempos` 数组能表达变速，但分析器只输出单一周期）。

它的价值在于**零依赖、确定性、可复现**。任何引入的分析器都不应破坏这个性质（见第 5 节）。

### 2.2 节拍 / downbeat 跟踪：逐个核实

| 包 | 仓库 | 许可证 | 输出 | CPU-only Windows | 结论 |
|---|---|---|---|---|---|
| **beat_this** | [CPJKU/beat_this](https://github.com/CPJKU/beat_this) | **MIT（代码 + 权重重）** | **beats + downbeats** | **可以**，官方明确 "will fall back to CPU if PyTorch does not have CUDA access"，`--gpu=-1` 强制 CPU | **首选** |
| **madmom** | [CPJKU/madmom](https://github.com/CPJKU/madmom) | 见下 | beats / downbeats / DBN | 有坑 | 仅作 DBN 后备 |
| **essentia** | [MTG/essentia](https://github.com/MTG/essentia) | **AGPL-3.0（对商业发行有传染性）** | RhythmExtractor2013 / BeatTrackerDegara / TempoCNN | 需自建，Windows 麻烦 | **本项目不建议**（许可证） |
| **librosa** | [librosa/librosa](https://github.com/librosa/librosa) | ISC | `beat_track` | 可以 | 质量低于 beat_this |
| **BeatNet** | [marl/BeatNet](https://github.com/marl/BeatNet) | **未验证** | 实时 beats/downbeats | **未验证** | 实时场景备选 |
| **aubio** | [aubio/aubio](https://github.com/aubio/aubio) | **GPL** | onset/beat | 可以 | 许可证不适合发行 |

**beat_this 的一手细节**（我抓取并读了官方 README）：

- 论文：ISMIR 2024，[*Beat This! Accurate Beat Tracking Without DBN Postprocessing*](https://arxiv.org/abs/2407.21658)（Foscarin, Schlüter, Widmer）。
- 许可证原文："The code and the published model weights are released under the **MIT license**."
- 输出结构（Python API 原文）：
  ```python
  from beat_this.inference import File2Beats
  file2beats = File2Beats(checkpoint_path="final0", device="cuda", dbn=False)
  beats, downbeats = file2beats(audio_path)   # 两个时间数组（秒）
  ```
- CLI：`beat_this path/to/audio.file -o path/to/output.beats`，`--dbn` 开启 DBN 后处理，`--float16` 提速。
- 模型体积：主模型 `final0/1/2` 约 **78 MB/个**；小模型 `small0/1/2` 约 **8.1 MB/个**。三个 seed 可用于稳健性投票。
- 依赖：**PyTorch ≥ 2.0**、`einops`、`soxr`、`rotary-embedding-torch`、`tqdm`。
- 批处理：`--touch-first --skip-existing --gpu=$gpu` 可多进程分卡。

**madmom 的兼容性坑（由 beat_this 官方 README 一手确认）**：

> "This requires installing the `madmom` package (with `pip install git+https://github.com/CPJKU/madmom.git`, as **the current version on PyPI only supports Python<3.10 and numpy<1.20**)."

本项目是 **Python 3.12 + numpy 2.5.3**，所以 PyPI 上的 madmom **必然装不上**；要用只能从 GitHub 装，且这是唯一硬依赖它的理由（`--dbn`）。**建议直接不用 DBN**：beat_this 论文的卖点正是"without DBN postprocessing"。

**essentia 的许可证问题要重点标记**：AGPL-3.0 对**随游戏发行**的分发形态有源码开放要求。本项目已有 `BGA/tools/bin/FFMPEG-NOTICE.txt` 记录 ffmpeg GPL 合规问题（见 `Docs/VideoBgaWorkflow.md` 末尾），**不应再引入一个 AGPL 依赖**，除非只把它当离线工具而不随包分发。

### 2.3 音乐结构 / 段落切分

| 包 | 仓库 | 输出 | 许可证 | 状态 |
|---|---|---|---|---|
| **allin1** | [mir-aidj/all-in-one](https://github.com/mir-aidj/all-in-one) | 功能性段落标签（intro/verse/chorus/bridge…）+ beat/downbeat | **未验证**（代码与权重可能不同） | 仍在维护，见下 |
| **MSAF** | [urinieto/msaf](https://github.com/urinieto/msaf) | 结构边界 | **未验证** | **未验证**是否仍维护 |
| **SongFormer** | **未验证** | 结构 | **未验证** | 搜索中出现，未取得一手仓库 |
| **FL_Audio_Segment_Extractor**（ComfyUI） | `ComfyUI_Fill-Nodes` | 按 `start_beat` + `beat_count` 切音频段 | **未验证** | 已在本地节点面，见 2.5 |

**诚实说明**：`allin1` 与 `MSAF` 我**没有**在本轮抓到官方仓库原文（GitHub API 在抓取中途触发限流），因此星数、许可证、最后活动时间三项均为 **未验证**。它们存在于 MIR 领域是公认的，但本文不给未经核实的版本号或许可证断言。

对本项目的实际影响**不大**：谱面里已经有 `sections[{startBeat, name, placements}]`，制谱器本来就让人工标段落。自动结构分析的价值是**给还没有段落标注的旧谱面补标签**，属于锦上添花，不是主路径。

### 2.4 分轨（source separation）

| 包 | 仓库 | 许可证 | GPU | CPU-only Windows | 备注 |
|---|---|---|---|---|---|
| **Demucs v4 / htdemucs** | [facebookresearch/demucs](https://github.com/facebookresearch/demucs) | **代码 MIT；预训练权重许可证未验证** | 强烈建议 | 极慢但可行 | 4 分轨（drums/bass/vocals/other） |
| **Spleeter** | [deezer/spleeter](https://github.com/deezer/spleeter) | MIT | 建议 | 可行 | **是否已停止维护：未验证**，质量低于 Demucs v4 |
| **BS-RoFormer / Mel-Band RoFormer** | [lucidrains/BS-RoFormer](https://github.com/lucidrains/BS-RoFormer) | **未验证** | 需要 | 不现实 | 当前人声分离 SOTA 阵营 |
| **Apollo** | [JusperLee/Apollo](https://github.com/JusperLee/Apollo) | **未验证** | 需要 | 不现实 | 通用音乐分离 |
| **AudioSeparation**（ComfyUI 节点） | `audio-separation-nodes-comfyui` | **未验证** | 可 | 可 | 已在本地节点面 |

**注意 demucs 的许可证陷阱**：代码是 MIT，但**预训练权重**常另有条款（音乐分离模型多因训练数据受限而如此）。**权重许可证：未验证** —— 商业发行前必须单独确认。

**SOTA 断言保守处理**：BS-RoFormer / Mel-Band RoFormer 系列在 MDX23 类榜单上通常被列为最强，但**我本轮没有抓到官方 benchmark 表**，因此"当前 SOTA"标为 **未验证**。

**对本项目的实际价值**：分轨的用途是**让视觉跟着音乐里正确的元素动**。例如用 **drums 轨**算 onset 驱动"打击式"视觉脉冲，用 **bass 轨**驱动低频光晕，用 **vocals 轨**决定是否出现人像/剪影。这比"整曲 RMS"精确得多，而且**不需要 GPU 也能离线做一次**。

### 2.5 更轻的替代：ComfyUI 里已有的音频分析节点（实测）

如果不想在仓库里引入 torch，可以把分析全部留在 ComfyUI 侧：

| 节点 | 模块 | 作用 |
|---|---|---|
| `FL_Audio_BPM_Analyzer` | `ComfyUI_Fill-Nodes` | 参数 `bpm_method ∈ {beat_intervals, onset_strength}`、`half_time`、`beat_offset_ms` |
| `FL_Audio_Segment_Extractor` | `ComfyUI_Fill-Nodes` | 输入 `beat_positions` + `start_beat` + `beat_count` → 切出该段的音频 |
| `FL_Audio_Music_Video_Sequencer` | `ComfyUI_Fill-Nodes` | **节拍→镜头表编译器**，见 4.5 |
| `AudioBPMDetector` | `ComfyUI_AudioTools` | BPM 检测 |
| `AudioGetTempo` / `AudioTempoMatch` / `AudioSeparation` | `audio-separation-nodes-comfyui` | 速度与分轨 |
| `SoundReactive` | `ComfyUI-KJNodes` | 音频→参数 |
| `FL_Audio_Reactive_Scale/Speed/Brightness/Saturation/Edge_Glow/Envelope` | `ComfyUI_Fill-Nodes` | **音频包络直接驱动图像参数** |

`FL_Audio_BPM_Analyzer` 的实现细节**未验证**（我没读它的源码），但它的参数面显示它是 onset/拍间隔法，**精度预期不如 beat_this**。定位应为"够用的粗网格"，而不是"精确对齐"。

---

## 3. 相机 / 运动 / 轨迹控制

### 3.1 已在本项目 ComfyUI 中**实测存在**的节点（最强证据）

下表全部来自 `.buildtmp/object_info.json` 实拍。`comfy_extras.*` = **ComfyUI 原生**；`custom_nodes.ComfyUI-WanVideoWrapper` = **kijai 包装器**（[kijai/ComfyUI-WanVideoWrapper](https://github.com/kijai/ComfyUI-WanVideoWrapper)，实测 **6,712 stars / Apache-2.0 / pushed 2026-05-24**，141 个节点类）。

| 能力 | 节点 | 来源 | 关键参数 |
|---|---|---|---|
| **相机轨迹嵌入** | `WanCameraEmbedding` | **原生** `comfy_extras.nodes_camera_trajectory` | `camera_pose`：**仅 9 个预设**（见 3.2）、`width=832`、`height=480`、`length=81`、`speed=1.0`、**归一化内参 `fx/fy/cx/cy`（默认 0.5）** |
| **相机 + I2V** | `WanCameraImageToVideo` | **原生** `comfy_extras.nodes_wan` | — |
| **轨迹点控制（DragNUW 类）** | `WanTrackToVideo` | **原生** `comfy_extras.nodes_wan` | **`tracks`(STRING, 默认 `[]`)**、`temperature=220.0`、`topk=2`、`start_image` |
| 轨迹点 → TRACKS | `WanMoveTracksFromCoords` | **原生** `comfy_extras.nodes_wanmove` | `track_coords`(STRING, 默认 `[]`)、`track_mask` |
| 轨迹合并 / 可视化 | `WanMoveConcatTrack` / `WanMoveVisualizeTracks` | **原生** `nodes_wanmove` | `line_width`、`circle_size`、`opacity` |
| 轨迹 + 首帧 | `WanMoveTrackToVideo` | **原生** `nodes_wanmove` | `strength`、`tracks`(TRACKS)、`start_image` |
| **Uni3C 相机控制** | `WanUni3CControlnetApply` | **原生** `comfy_extras.nodes_model_patch` | — |
| **ATI 轨迹注入** | `WanVideoATI_comfy` / `WanVideoATITracks` / `WanVideoATITracksVisualize` | **包装器** | `tracks`(STRING)、`temperature=220.0`、`topk=2`、`start_percent`、`end_percent` |
| **ReCamMaster 相机** | `WanVideoReCamMasterCameraEmbed` / `...DefaultCamera` / **`...GenerateOrbitCamera`** | **包装器**（节点描述直接指向 `KwaiVGI/ReCamMaster`） | `num_frames=81`、`degrees=90` |
| **VACE 控制视频** | `WanVaceToVideo` | **原生** `nodes_wan` | `control_video`、`control_masks`、`reference_image`、`strength` |
| **Wan2.2 Fun-Control 控制视频** | **`Wan22FunControlToVideo`** | **原生** `nodes_wan` | `control_video`、`ref_image` |
| Wan2.1 Fun-Control / Inpaint | `WanFunControlToVideo` / `WanFunInpaintToVideo` | **原生** `nodes_wan` | `control_video`、`start_image`、`clip_vision_output` |
| 首尾帧 | `WanFirstLastFrameToVideo` | **原生** `nodes_wan` | `start_image`、`end_image` + 两个 clip_vision |
| 通用 controlnet | `WanVideoControlnet` / `WanVideoControlnetLoader` / `WanVideoAddControlEmbeds` / `...DualControlEmbeds` | **包装器** | `start_percent`/`end_percent` 可调度 |
| **长视频接缝** | **`WanVideoContextOptions`** | **包装器** | `context_schedule ∈ {uniform_standard, uniform_looped, static_standard}`、`context_frames=81`、`context_stride=4`、**`context_overlap=16`**、`fuse_method ∈ {linear, pyramid}`、`freenoise` |
| 时序一致性 | `WanVideoFreeInitArgs` | **包装器**（指向 [TianxingWu/FreeInit](https://github.com/TianxingWu/FreeInit)） | `freeinit_num_iters=3`、`freeinit_method ∈ {butterworth, ideal, gaussian, none}` |
| 循环 | `WanVideoLoopArgs` | **包装器**（Mobius） | `shift_skip=6` |
| 运动续接 | `WanAnimateToVideo` | **原生** `nodes_wan` | **`video_frame_offset`**、**`continue_motion`**、`continue_motion_max_frames=5` |
| 画质增强 | `WanVideoEnhanceAVideo` | **包装器**（指向 NUS-HPC-AI-Lab/Enhance-A-Video） | `weight=2.0` |

### 3.2 已读到源码的关键机制（以下均为读源码所得，不是推测）

#### (a) `WanCameraEmbedding` 只有 9 个预设相机，且是**线性匀速**——不能接受自定义轨迹

源码 `comfy_extras/nodes_camera_trajectory.py` 里的 `CAMERA_DICT` 是**完整枚举**：

```python
"Static", "Pan Up", "Pan Down", "Pan Left", "Pan Right",
"Zoom In", "Zoom Out", "Anti Clockwise (ACW)", "ClockWise (CW)"
```

运动生成是**线性斜坡**，不是任意曲线：

```python
def get_camera_motion(angle, T, speed, n=81):
    for i in range(n):
        _angle = (i/n) * speed * (CAMERA_DICT["base_angle"]) * angle   # base_angle = π/3
        _T     = (i/n) * speed * (CAMERA_DICT["base_T_norm"]) * T       # base_T_norm = 1.5
```

即：**相机在整个片段内匀速转/移，只能调 `speed`，没有缓动、没有关键帧、没有分段。**

而且它的来历是 **CameraCtrl**。源码注释原文：

> `"""Copied from https://github.com/hehao13/CameraCtrl/blob/main/inference.py"""`
> `"""Modified from https://github.com/hehao13/CameraCtrl/blob/main/inference.py"""`
> `"""Adapted from https://github.com/aigc-apps/VideoX-Fun/blob/main/comfyui/comfyui_nodes.py"""`

输出是 **Plücker embedding**（`ray_condition()`，Sitzmann et al. 2021），6 通道，`fx/fy/cx/cy` 是**归一化**内参。

**结论（这条现在有源码级证据，不再是"未验证"）**：`WanCameraEmbedding` 提供 **9 个预设运镜 + 一个速度旋钮**。它**无法**接受本项目 `cameraKeys` 那样的逐帧自定义位姿。**所以"用节点参数把谱面相机喂给模型"这条路是走不通的。**

顺带修正附录 #14 的一部分：**CameraCtrl 确实存在，仓库是 `hehao13/CameraCtrl`** —— 我不是从它的主页核实的，而是从 ComfyUI 官方源码对它的引用核实的。它的**星数、许可证、最后活动时间仍为 未验证**。

#### (b) 轨迹控制的两种**不同**实现，以及一个静默失败陷阱

ComfyUI 里有**两套**轨迹机制，容易混淆：

| | `WanTrackToVideo` | `WanMoveTrackToVideo` |
|---|---|---|
| 源文件 | `comfy_extras/nodes_wan.py` | `comfy_extras/nodes_wanmove.py` |
| 官方溯源 | `patch_motion()` —— DragNUW 系（点轨迹 patch） | **Wan-Move**，注释指向 `ali-vilab/Wan-Move`，用 `replace_feature()` 做特征替换 |
| 关键参数 | `temperature=220.0`、`topk=2`、`start_image`（**必填**） | `strength`（0–100，默认 1.0）、`start_image`（可选） |
| 轨迹输入 | `tracks`（multiline STRING） | `tracks`（TRACKS 类型，由 `WanMoveTracksFromCoords` 产生） |
| `search_aliases` | `motion tracking` / `trajectory video` / `point tracking` / `keypoint animation` | — |

**`tracks` 的 JSON 格式现在可以确定**（`parse_json_tracks()` 源码）：

```python
parsed = json.loads(tracks.replace("'", '"'))
# 单轨：[{"x":..,"y":..}, {"x":..,"y":..}, ...]        → 每帧一个 dict
# 多轨：[[{...},{...}], [{...},{...}]]
# 兼容单引号（源码做了 .replace("'", '"')）
```

坐标是**目标 `width`×`height` 下的像素坐标**（证据：`GenerateTracks` 里 `start_x_px = start_x * width`；`create_pos_embeddings()` 里以 `x >= width` / `y >= height` 判越界）。

**⚠ 静默失败陷阱（重要）**：`WanTrackToVideo` 在轨迹解析失败时**不报错**：

```python
tracks_data = parse_json_tracks(tracks)
if not tracks_data:
    return WanImageToVideo().execute(...)   # 静默退化成普通 I2V，无任何提示
```

`parse_json_tracks` 捕获 `json.JSONDecodeError` 后返回 `[]`。**所以一个拼错的轨迹 JSON 会悄悄变成"没有轨迹控制"的普通图生视频，不会给你任何报错。** 上线前必须用 `WanMoveVisualizeTracks` 先把轨迹画出来肉眼确认。

**另一个时序细节**（源码注释原文）：

> `# tracks: shape [t, h, w, 3] => samples align with 24 fps, model trained with 16 fps.`

轨迹采样按 **24fps** 对齐，而模型按 **16fps** 训练。`process_tracks()` 里还有一个 `if tracks.shape[1] == 121: permute` 的特判。**给轨迹打点时不能假设"一帧轨迹 = 一帧输出"。**

#### (c) `WanMoveTrackToVideo` 存在**无 seed 的随机性**

`nodes_wanmove.py` 的 `replace_feature()` 和 `create_pos_embeddings()` 都调用了 `torch.randperm`：

```python
track_pos = track_pos[:, torch.randperm(n), :, :]        # replace_feature：随机打乱轨迹顺序
tracks_idx = torch.randperm(n)[:track_num]               # create_pos_embeddings：随机抽轨迹
```

**节点本身没有暴露 seed 参数。** 这意味着 `WanMoveTrackToVideo` 的输出**即便固定采样 seed 也不完全可复现** —— 这是第 6 节可复现性讨论里的一条硬约束，必须记录。


#### (d) 控制视频到底怎么进模型（`Wan22FunControlToVideo` / `WanVaceToVideo` 源码）

这是推荐方案的技术核心，源码已确认：

**`WanVaceToVideo`**（`comfy_extras/nodes_wan.py`）：

```python
control_video = common_upscale(control_video[:length], width, height, "bilinear", "center")
control_video = control_video - 0.5
inactive  = (control_video * (1 - mask)) + 0.5     # mask 外的区域
reactive  = (control_video * mask) + 0.5           # mask 内的区域
control_video_latent = cat((vae.encode(inactive), vae.encode(reactive)), dim=1)
# 写入 conditioning：vace_frames / vace_mask / vace_strength
```

关键点：
- `control_video` 会被**强制 resize 到 `width`×`height`**，所以**控制视频分辨率不必等于生成分辨率**，但纵横比最好自己先对齐，否则会被 `bilinear` 拉伸。
- `control_masks` 决定**哪些区域听控制视频**（reactive）**哪些不听**（inactive）。这是"只让 AI 改某个区域"的正规手段。
- `length` 上限 `MAX_RESOLUTION`，步长 4；`strength` 范围 0–1000。
- 有 `reference_image` 时会往 latent 前面拼帧并输出 **`trim_latent`**，**解码后必须用 `TrimVideoLatent` 裁掉**，否则成片会多出参考帧。

**`Wan22FunControlToVideo`**（Wan 2.2 专用）：

- 用 `vae.spacial_compression_encode()` 与 `vae.latent_channels` 自适应；`latent_channels == 48` 时用 `Wan22()` latent 格式，否则 `Wan21()`。
- `control_video` 编码进 `concat_latent` 的**前半**（`[:, :latent_channels]`），`ref_image` 走 `reference_latents`（append），`concat_mask_index = latent_channels`。
- **注意**：`define_schema()` 只暴露了 `ref_image` 与 `control_video` 两个可选输入；源码里虽然有 `start_image` 分支，但**没有出现在节点 schema 里，界面接不上**。所以这条路径实际就是 **`ref_image` + `control_video`** 两个旋钮。

**对本项目的意义**：把 `render_bga.py` 的几何渲染输出接到 `control_video`，就是"**让 AI 在完全保留相机与构图的前提下换质感**"的机制级实现。这是方案 B 成立的技术依据。

#### (e) 一条能立刻省成本的证据：`1280×704` 是 Wan 2.2 的**原生默认**

`Wan22ImageToVideoLatent` 的源码默认值：

```python
width  = 1280   # min=32, step=32
height = 704    # min=32, step=32
length = 49
latent = torch.zeros([1, 48, ((length-1)//4)+1, height//16, width//16])
```

`height//16, width//16` 正是 Wan2.2 VAE 的 **16× 空间压缩**，`latent_channels=48` 也正是 Wan2.2 的通道数。**`1280×704` 被写成这个原生节点的默认值**，是对"Wan 2.2 原生支持 1280×704"最直接的源码级佐证。

因此本项目文档里的 **640×352 是 ComfyUI 默认工作流的尺寸，不是模型能力上限** —— 这个判断现在有官方 README（"720P 即 `1280*704`，需 ≥24GB VRAM"）与源码默认值两条独立证据。

#### (f) 长视频接缝与续接：三个真实的机制

1. **`WanVideoContextOptions`**（包装器）—— 官方描述："allows splitting the video into context windows and **attempts blending them** for longer generations than the model and memory otherwise would allow"。参数：`context_schedule ∈ {uniform_standard, uniform_looped, static_standard}`、`context_frames=81`、`context_stride=4`、**`context_overlap=16`**、`fuse_method ∈ {linear, pyramid}`、`freenoise`。
2. **`WanAnimate2ToVideo` 的续接 API**（原生）—— 输入 `continue_motion`（**上一段的尾部帧**）+ `video_frame_offset`（**接上一节点的 `video_frame_offset` 输出**），输出 `trim_latent` / `trim_image` / `video_frame_offset`。另有 `pose_strength`、`pose_start_percent` / `pose_end_percent`（官方 tooltip：运动主要在前段建立，所以 `0.7` 可以放松细节而保留编排）、`reference_image_strength`。这是**官方的"链式续接"接口**，比在 ffmpeg 里叠化更接近正解。
3. **`WanAnimate2Cache`** —— 缓存 pose 视频的每层激活，"Roughly **halves generation time**"，代价是 480×832/81 帧 bf16 下约 **12.5 GB 系统内存**。

**一条容易踩的交互坑**（`WanAnimate2Cache` 的官方 tooltip 原文）：

> "With context windows each window is cached separately, so RAM scales with the window count; use the **`static_standard`** schedule, as uniform schedules shift the windows every step and nothing ever recurs to hit the cache."

**若同时用 `WanAnimate2Cache` 与 `WanVideoContextOptions`，必须选 `static_standard` 调度**，否则缓存永远打不中。这类节点间交互不会写在教程里，只能从源码/tooltip 读到。

### 3.3 学术方法（一手核实程度说明）


| 方法 | 我核实到的 | 结论 |
|---|---|---|
| **VACE** | ✅ 抓到官方仓库与 README。`ali-vilab/VACE`，**3,954 stars / Apache-2.0 / pushed 2025-10-17**，ICCV 2025，[arXiv 2503.07598](https://arxiv.org/abs/2503.07598)。任务：**R2V / V2V / MV2V**；CLI 如 `python vace/vace_pipeline.py --base wan --task depth --video ...`；模型 `Wan2.1-VACE-1.3B`（81×480×832）与 `Wan2.1-VACE-14B`（81×720×1280），均 Apache-2.0 | **可用，且是控制视频的正统实现** |
| **ReCamMaster** | ✅ 节点描述指向 `KwaiVGI/ReCamMaster`，且 3 个节点存在。论文/星数/许可证 **未验证** | 在包装器里可用 |
| **ATI** | ✅ 节点 `WanVideoATI_comfy` 等 3 个存在。论文 arXiv 编号 **未验证** | 在包装器里可用 |
| **Uni3C** | ✅ 原生节点 `WanUni3CControlnetApply` 存在。论文 **未验证** | 原生可用 |
| **CameraCtrl** | ✅ **存在，仓库 `hehao13/CameraCtrl`** —— 但不是从它的主页核实的，而是 **ComfyUI 官方源码明确标注 `Copied from` / `Modified from` 该仓库**（见 3.2(a)）。星数/许可证/论文编号 **未验证**。**它已经以"预设相机"的形式被吸收进 `WanCameraEmbedding`** | 已吸收 |
| **Wan-Move** | ✅ **存在**，`ali-vilab/Wan-Move` —— 同样由 ComfyUI 官方源码注释指向（`nodes_wanmove.py` 顶部链接 `Wan-Move/blob/main/wan/modules/trajectory.py`）。星数/许可证/论文 **未验证** | 已吸收为 `WanMove*` 节点 |
| **MotionCtrl / TrajectoryCrafter** | ❌ **本轮未取得一手仓库证据** | **未验证**。不要据此选型 |
| **DragNUW 系点轨迹** | ⚠️ **机制已确认存在**（`WanTrackToVideo` 内部调用 `patch_motion(...)`，参数 `temperature`/`topk`），但"DragNUW"这个具体仓库名与论文 **未验证** | 机制可用，命名待核 |
| **Wan2.2-Animate** | ✅ 官方 README 确认存在：`--task animate-14B`，animation/replacement 两模式，输入 `pose_video`/`face_video`/`background_video`/`character_mask`，`segment_frame_length=77`，输出 30fps | 角色动画/替换，**不是相机控制** |
| **CameraBench** | ❌ 未核实 | **未验证** |

**必须诚实指出**：**MotionCtrl** 与 **TrajectoryCrafter** 我**没有**在权威来源上核实到仓库、论文编号与维护状态（GitHub API 在抓取过程中触发限流，且部分名称存在多个同名项目）。**本文不给出它们的星数、许可证或"是否支持 Wan"的断言。**

反过来，**CameraCtrl** 与 **Wan-Move** 的存在性现在有**强证据** —— 它们不是靠搜主页确认的，而是 **ComfyUI 官方源码在注释里直接标注了来源仓库**，而且**两者的机制都已经被吸收成 ComfyUI 原生节点**（`WanCameraEmbedding` / `WanMove*`）。这比"论文存在"有力得多：**代码在跑。**

### 3.4 工程判断：谁能真正把"谱面事件 → 运镜"落地

按**可落地性**排序：

1. **控制视频（`Wan22FunControlToVideo` / `WanVaceToVideo`）** — 本项目已有一个**逐帧渲染、被 Unity 验证过**的渲染器。把它当控制视频输入，相机就**由构造保证精确**，而不是靠模型"听话"。机制已由源码确认（3.2(d)）。**首选。**
2. **轨迹 JSON（`WanMoveTrackToVideo` / `WanTrackToVideo`）** — 坐标是**目标分辨率下的像素坐标**（已由源码确认），适合"让某个物体沿 note 轨迹飞行"这类**局部**控制；但注意 3.2(b) 的**静默失败陷阱**与 3.2(c) 的**无 seed 随机性**。用之前先用 `WanMoveVisualizeTracks` 肉眼确认轨迹。
3. **`GenerateTracks`（原生节点）** — 一个常被忽略的节点：输入是**归一化**的 `start_x/start_y/end_x/end_y`、可选 `bezier` + `mid_x/mid_y` 控制点、`num_tracks`、`track_spread`，以及 **`interpolation ∈ {linear, ease_in, ease_out, ease_in_out, constant}`**。**这个参数面与本项目 `chart.json` 的 `stagePath.points` + `cameraKeys.easing` 几乎逐项对应**，是实现"路径 + 缓动 → 轨迹"的现成转换目标。
4. **ReCamMaster 的 orbit 相机** — 只有 `degrees` 和 `num_frames` 两个参数，**是"绕轨道环拍"而非任意轨迹**，表达力有限。
5. **`WanCameraEmbedding`** — **只有 9 个预设 + 匀速斜坡**（已由源码确认，见 3.2(a)），**不能**接受自定义逐帧位姿，**不适合**做谱面精确运镜。

**反面提醒**：这些方法里能用的部分，是因为它们**已经被吸收进 ComfyUI 原生节点或 kijai 包装器**（ATI / ReCamMaster / Uni3C / VACE / CameraCtrl / Wan-Move）。**MotionCtrl 与 TrajectoryCrafter 在本项目 ComfyUI 节点面里没有出现**，其一手仓库也**未验证**。**"论文存在" ≠ "能在 ComfyUI 里跑"。**

---

## 4. 「谱面数据驱动视觉」的既有工程实践（确定性版）

这一节是**非 AI 版本**的同一件事，价值在于：这些系统已经把"什么数据值得绑定到视觉上"这个问题回答了三十年。

### 4.1 osu! storyboard（`.osb`）

- `.osu` 分节含 `[Events]`（"Beatmap and storyboard graphic events"）与 `[TimingPoints]`；storyboard 也可放**独立的 `.osb` 文件**，且"**External storyboards are shared between all difficulties**"，而 `.osu` 内联的那份只属于该难度。
- 优先级原文："**Commands from the .osb file take precedence over those from the .osu file within the layers**, as if the commands from the .osb were appended to the end of the .osu commands."
- 对象：`Sprite,(layer),(origin),"filepath",(x),(y)`、`Animation,(layer),(origin),"filepath",(x),(y),(frameCount),(frameDelay),(looptype)`、`Sample,<time>,<layer>,"<file>",<volume>`。
  - 关键原文：**"Take note that there is no indication of when the object should appear. That is entirely up to the commands themselves."** —— 对象声明只管"存在与叠放顺序"。
  - `Sample` "are **not commands**, so they aren't used in loops or triggers."
  - 画面 640×480，可玩区 510×385，原点左上。
- 层（数值 0–3）：`0 Background` / `1 Fail` / `2 Pass` / `3 Foreground`；规范正文另提到第 5 层 `Overlay`（"displayed above hit objects, use with caution"），但数值表只到 3 → **`Overlay` 的写法/编号：未验证**。
- 事件命令：`F`(Fade) `M`(Move) `MX` `MY` `S`(Scale) `V`(VectorScale) `R`(Rotate，**弧度**) `C`(Colour，**减色**混合) `P`(Parameter：`H` 水平翻转 / `V` 垂直翻转 / `A` 加色混合)。`P` 特殊："apply **ONLY while they are active**"。Easing 表 0–34。
- **`Trigger`（官方称之为 "trigger loops"，wiki 承认 "they aren't loops at all"）** 语法为 `HitSound[SampleSet] [AdditionsSampleSet] [Addition] [CustomSampleSet]`，实例：`HitSound`、`HitSoundClap`、`HitSoundFinish`、`HitSoundWhistle`、`HitSoundDrumWhistle`、`HitSoundAllSoft`、`HitSoundDrumClap0`、`HitSound6`。行为："If a trigger condition occurs while another trigger is running, the earlier trigger is stopped, and the new trigger starts."
- **Pass/Fail（storyboard 唯一能拿到的"玩家表现"数据）**：开局前永远 Pass；游玩中本 combo 首个物件或上一 combo 全 300 收尾 → Pass，否则 Fail；break 中血条 > 一半 → Pass；两个层**永远不会同时出现**，且"the game will transition to the results screen as soon as the last event occurs"（包含另一层的事件 → 两层收尾时长必须一致）。
- 来源（markdown 原文，因为 `osu.ppy.sh/wiki` 对匿名客户端只返回 JS 登录壳）：[Objects](https://raw.githubusercontent.com/ppy/osu-wiki/master/wiki/Storyboard/Scripting/Objects/en.md)、[Commands](https://raw.githubusercontent.com/ppy/osu-wiki/master/wiki/Storyboard/Scripting/Commands/en.md)、[Compound_Commands](https://raw.githubusercontent.com/ppy/osu-wiki/master/wiki/Storyboard/Scripting/Compound_Commands/en.md)、[General_Rules](https://raw.githubusercontent.com/ppy/osu-wiki/master/wiki/Storyboard/Scripting/General_Rules/en.md)、[Audio](https://raw.githubusercontent.com/ppy/osu-wiki/master/wiki/Storyboard/Scripting/Audio/en.md)。

**它把视觉绑定到什么** —— 只有三类，且这是**官方逐字**的：

1. **绝对毫秒**："Time is measured in milliseconds … from the start of the beatmap's main audio file"
2. **hitsound 类型**（`Trigger` 的 `HitSound*`）
3. **Pass / Fail 状态**（Pass/Fail 层 + `Passing`/`Failing` trigger）

**决定性引文（本报告里最有价值的一句一手证据）**：

> "**Time in the SB is not dependent upon timing of the beatmap itself (e.g., how many measures there are or beats per minute).** Therefore, it is recommended that the beatmap should be reasonably well-timed before storyboarding, as it will be harder to adjust these times later."

**进来不了的**：音符坐标/轨道/物件类型、**BPM 与 timing point**、小节/拍号、连击数、准确率、分数。storyboard 语法里**不存在**任何引用 `[TimingPoints]` 或 `[HitObjects]` 的结构。

**工程教训**：osu! 的 storyboard 是**纯绝对时间轴动画**，作者必须手写每一个关键帧时间；它**连 BPM 都拿不到**。**想做"跟着鼓点自动生成背景"，`.osb` 只给你"绝对时间轴 + 谁在什么时候被什么音效击中"，其余必须离线算好再写成绝对毫秒。** 它的 `Trigger` 设计值得偷的是**思路**：把"游戏运行时事件（含玩家表现）"作为视觉的合法输入。

> 附：官方 wiki 亲口把社区工具列为抽象层 —— [storybrew](https://github.com/Damnae/storybrew)（C#，脚本化生成 `.osb`）。其 API 细节未取 → **未验证**。

### 4.2 BMS 的 BGA 通道

BMS 是**唯一**一个把"分层视频"写进谱面格式本体的主流格式。

> **⚠ 本节为 v2 修订**：初版本文把通道 `01`/`02`/`03` 误写成 BGA 图层。经一手规格核对（1998 年原始规格 + `bms_rs` 解析器源码 + Angolmois 实现文档），**`01`=BGM、`02`=小节长度、`03`=整数 BPM 变化，三者都不是 BGA 通道**。下表为已更正的版本。

#### 通道表（据 1998 原始规格 + `bms_rs` 源码注释）

| 通道 | 语义 | 通道 | 语义 |
|---|---|---|---|
| `01` | **BGM**（自动播放的 WAVE） | `0A` | **BgaLayer2**（"layered over LAYER"） |
| `02` | 小节长度 SectionLen | `0B`–`0E` | BGA BASE / LAYER / LAYER2 / POOR 的**不透明度** |
| `03` | 整数 BPM 变化 | `97`/`98` | BGM 音量 / 按键音量 |
| **`04`** | **BgaBase**（最底层） | `99` | `#TEXTxx "string"` |
| `05` | `#SEEKxx` 视频定位 | `A0` | `#EXRANKxx`（100 = RANK:NORMAL） |
| **`06`** | **BgaPoor**（POOR BGA） | `A1`–`A4` | BASE/LAYER/LAYER2/POOR 的 `#ARGBxx` |
| **`07`** | **BgaLayer**（叠加在 Base 之上） | `A5` | **`#SWBGAxx`** 按键绑定动画 |
| `08`/`09` | BPM 变化对象 / STOP | `SC`/`SP` | `#SCROLL` 滚动速度 / `#SPEED` 音符间距 |

来源：1998 原始规格 [BM98 data format specification](http://bm98.yaneu.com/bm98/bmsformat.html)（原文 `04` = "BGA(background animation)"，`#BMP00` = "shows when a player do a poor play"）、[bms_rs channel.rs 源码](https://docs.rs/bms-rs/0.9.0/src/bms_rs/bms/command/channel.rs.html)、[Angolmois INTERNALS.md](https://raw.githubusercontent.com/lifthrasiir/angolmois/master/INTERNALS.md)。

#### 层优先级与 alpha（现在有一手依据）

Angolmois INTERNALS 原文把四层写得很清楚：

> "Channels `04` (**bottom layer**), `06` (**POOR BGA**), `07` (**middle layer**) and `0A` (**top layer**)"

> "**If the image has an alpha channel it will retain its transparency; otherwise the color black (`#000000`) will be used as a transparent color.**"

- **绘制顺序：`04` → `07` → `0A`**，POOR 依 `#POORBGA` 模式插入（**该模式细节未验证**）。
- **"黑 = 透明"在 LAYER 层是规范级规则**：bmson 规范原文 —— "Unlike [BMS Layer Channel #xxx07], **black pixels will not be made transparent**."（即 bmson 反过来选择不继承这个行为）
- **实现差异警告**：Angolmois 对**所有**非 alpha 的 `#BMPxx` 都用黑作透明键，而 bmson 规范只为 `07` 陈述该行为。**两个一手来源在"`04` 是否也黑透明"上口径不同 → 属实现差异，不要当规范。**
- bmson 给出的转换建议值得照抄：一个 BMP 可能同时被当作 BGA 和 LAYER 使用，因此"a single BMP file **may have to be converted into two different PNG files**"。**结论：直接产出带 alpha 的 PNG，绕开整个黑透明语义分歧。**

#### `#SWBGAxx` —— **按键**驱动的动画（不是 chart 驱动）

`bms_rs` 的 `SwBgaEvent` 结构体（源码级）：

```rust
pub struct SwBgaEvent {
    pub frame_rate: u32,  // 帧间隔(ms)，60FPS=17
    pub total_time: u32,  // 总时长(ms)；0 = 按住键期间一直播
    pub line: u8,         // 绑定的键位通道（11-19 / 21-29）
    pub loop_mode: bool,
    pub argb: Argb,       // 透明色
    pub pattern: String,  // 帧序列，如 "01020304"
}
```

这是"**performance-driven**"的确证：`total_time = 0` 表示按住键期间持续播放。注意 `SwBgaEvent.argb` 在此语境是**透明色键**，与图层 `#ARGBxx` 的"整体染色/不透明度"**语义不同，不要混用**。

#### bmson：**并没有砍掉 BGA**（更正）

初版本文写"bmson 放弃了 BGA"。**这个说法在一手来源下不成立**，属于流行误传。可验证的 bmson 规范（`bemusic/bmson-spec`，HEAD `668c4338`，版本 1.0.0-beta）里 BGA 是**一等结构**：

```
dictionary Bmson {
    ... BGA bga; ...
}
dictionary BGA {
    BGAHeader[] bga_header;   // 素材 id 与文件名
    BGAEvent[]  bga_events;
    BGAEvent[]  layer_events;
    BGAEvent[]  poor_events;
}
```

且 1.0.0 的 breaking change 是**改名**（`bgaHeader`→`bga_header` 等 snake_case），**不是删除**。规范正文原话：**"Currently, BGA specification is just compatible with BMS."** 来源：[bmson-spec](https://bmson-spec.readthedocs.io/en/master/doc/index.html)。

**对本项目有用的一点**：bmson **没有小节/拍号概念**，时间轴是 **pulse**（默认 240 pulses/quarter）+ `lines`(bar line) + `stop_events`（"暂停 N 个 pulse"）。**pulse 是不依赖 BPM 的整数时间轴** —— 这与本项目 `chart.json` 的 `tick`（`ticksPerBeat: 480`）是**同一种设计**，比"秒"更适合做确定性对齐。

**它把视觉绑定到什么**：**时间**（小节内 base-36 槽位 / pulse）、**玩家表现**（`06` + POOR）、**按键**（`A5 #SWBGA`）、**图层不透明度与 aRGB**。
**工程教训（最重要的一条）**：BMS 证明了一件本项目正需要的事 —— **"视频层"可以是与 note 数据共享同一时间轴的独立通道，允许分层叠加、按玩家状态与按键切换**。本项目 `chart.json` 已有 `effectClips[{kind,target,...}]`（`target` 可以是 `screen` / `scene` / `judgement` 或某个 `sceneObject` id），与 BMS 的层/通道模型**结构上同源**。

### 4.3 StepMania / Etterna 的 BGAnimation 与 Lua

#### `#BGCHANGES` 的确切字段顺序（已从 loader 源码核实，结清原 #13）

`SMLoader::LoadFromBGChangesString`（`src/NotesLoaderSM.cpp`）**按下标顺序**解析，最多 11 个字段：

| idx | 字段 | 说明 |
|---|---|---|
| 0 | `m_fStartBeat` | **触发 beat** |
| 1 | `m_def.m_sFile1` | 主文件 |
| 2 | `m_fRate` | **速率** |
| 3 | — | 向后兼容：非 0 → `transition = "CrossFade"` |
| 4 | — | 向后兼容：非 0 → `effect = "StretchRewind"` |
| 5 | — | 向后兼容：**0** → `effect = "StretchNoLoop"` |
| 6 | `m_def.m_sEffect` | 效果名（覆盖 4/5 推出的值） |
| 7 | `m_def.m_sFile2` | 次文件 |
| 8 | `m_sTransition` | 过渡名（覆盖 3 推出的值） |
| 9 | `m_def.m_sColor1` | `^` 替换成 `,`，再 NormalizeColorString |
| 10 | `m_def.m_sColor2` | 同上 |

- 返回值要求 **`size >= 2`**（至少 `beat=file`）；序列化端用 `"%.3f=%s=%.3f=%d=%d=%d=%s=%s=%s=%s=%s"` 反向输出同一顺序。
- 多个 change 用 `,` 分隔；`#BGCHANGES1` = LAYER 1、`#BGCHANGES2` = LAYER 2；别名 `#ANIMATIONS`，前景层是 `#FGCHANGES`。
- 过渡常量 `SBT_CrossFade`；效果常量 `SBE_UpperLeft` / `SBE_Centered` / `SBE_StretchNormal` / `SBE_StretchNoLoop` / `SBE_StretchRewind`。
- **`rate` 字段的真实含义**：`ActorFrame::UpdateInternal` 里 `fDeltaTime *= m_fUpdateRate` → **第 3 个字段就是背景动画的播放倍速**。
- 切换时机由 `FindBGSegmentForBeat(GetBeatFromElapsedTime(fCurrentTime))` 决定 → **按 beat 切换**。
- 来源：[NotesLoaderSM.cpp](https://raw.githubusercontent.com/stepmania/stepmania/master/src/NotesLoaderSM.cpp)、[BackgroundUtil.h](https://raw.githubusercontent.com/stepmania/stepmania/master/src/BackgroundUtil.h)、[Background.cpp](https://raw.githubusercontent.com/stepmania/stepmania/master/src/Background.cpp)、[ActorFrame.cpp](https://raw.githubusercontent.com/stepmania/stepmania/master/src/ActorFrame.cpp)。

#### ★ 最有价值的先例：**从谱面时序结构自动生成背景切换表**

`BackgroundImpl::LoadFromRandom()`（`src/Background.cpp`）是"**用谱面数据自动生成视觉时间表**"的真实工程先例，而且**零手工打点**：

- **按拍号变化切**：遍历 `SEGMENT_TIME_SIG`，步长 `RAND_BG_CHANGE_MEASURES * ts->GetNoteRowsPerMeasure()` —— 即"每次拍号变化、或每 4 小节"换背景。
- **按小节起点的 BPM 变化切**：`if (RAND_BG_CHANGES_WHEN_BPM_CHANGES)` 遍历 `SEGMENT_BPM`，判断 `(bpm->GetRow() - ts->GetRow()) % ts->GetNoteRowsPerMeasure() == 0`（**该 BPM 变化正好落在小节开头**）成立才插一条 BGA。
- **随机源是 `GetHashForString(pSong->GetSongDir())`** → **同一首歌每次播放的背景序列完全一致（确定性！）**，不是真随机。
- 主题可调：`RandomBGStartBeat`、`RandomBGChangeMeasures`、`RandomBGChangesWhenBPMChangesAtMeasureStart`、`RandomBGEndsAtLastBeat`、`RandomBackgroundMode`、`NumBackgrounds`、`SongBackgrounds`、`BGBrightness`。
- **触发条件**：只在"没有 `#BGCHANGES` 且未开 RandomBGOnly"时兜底 —— 即**没人为安排背景时**才自动生成。

**为什么这条对本项目重要**：它证明"**把谱面的 BPM 段 / 拍号段 / 小节边界当作视觉事件的生成源**"是可行的、且被商业级引擎采用过的做法。**本项目 `chart.json` 里的 `tempos` 与 `sections` 正好提供了同样的结构**，可以直接照这个思路生成镜头表 —— 而且本项目的 `sections` 比 StepMania 的 BPM 段**语义更强**（带 `name` 的人工段落标签）。

#### `[BGAnimation]` 与 Lua 可绑定面

- `BGAnimation` 是 `ActorFrame` 子类；`LoadFromAniDir()` 找 `BGAnimation.ini`：quirks 模式下读 `[BGAnimation]` + `[Layer1..N]`（`CompareLayerNames` 按 `sscanf("Layer%d")` **数值**排序）；无 `.ini` 则列目录下图片/视频文件，**每张图 = 一层**，跳过 `_` 开头的文件。层可 `Import="子目录"` 递归引入。
- **背景"效果"实际是 Lua**：`BackgroundUtil::GetBackgroundEffects` 就是 `GetDirListing(BACKGROUND_EFFECTS_DIR + name + ".lua")`。创建 actor 时注入 Lua 全局量 **`File1` / `File2` / `Color1`（缺省 `#FFFFFFFF`）/ `Color2`** → **Lua 效果脚本靠读这几个量拿到"这次该画什么"**。过渡文件是 `BackgroundTransitions/*.xml`（根节点 `BackgroundTransition`，属性 `LeavesCommand` / `RootCommand`）。
  - **更正**：初版本文提到的 C++ `Effect`/`RageEffect` 类**未能在 master 上确认**（`src/Effect.h` 返回 404，而 `BackgroundEffects/` 下实际是 `.lua`）→ **SM5 的"背景效果"是 Lua actor，不是 C++ Effect 类**。
- **暴露给主题的 Lua 绑定**：`Song:GetBGChanges()` → 每项 `{start_beat, rate, transition, effect, file1, file2, color1, color2}`；`Song:GetTimingData()`、`HasSignificantBPMChangesOrStops()`、`GetDisplayBpms()`；`ActorFrame:SetUpdateFunction(fn)` / `SetDrawFunction(fn)`（**逐帧跑 Lua**）、`playcommandonchildren`、`SetUpdateRate`、`GetChild/GetChildren`、`AddChildFromPath`；`ActorUtil::MakeActor(path)` 按路径造 actor。
- 另有谱面外的运行时状态驱动：`IsDangerAllVisible()` → `GAMESTATE->AllAreInDangerOrWorse()`，翻转时广播 `"ShowDangerAll"`/`"HideDangerAll"`；`BrightnessOverlay` 由两名玩家的 `PlayerOptions.Cover` 决定左右亮度。

**它把视觉绑定到什么**：**beat**（`#BGCHANGES` 的 start beat）、**速率**、**过渡/效果名**、**前后两层文件与颜色**；Lua 里还可读**完整 timing 数据**（BPM/STOP/DELAY/WARP/TIMESIG/SPEED/SCROLL/FAKE 段）与 Score/Cover/Danger 状态。
**工程教训**：`#BGCHANGES` 本质是一张 **`(beat, file, rate, transition, effect, color)` 镜头表**，与第 5 节的 `chart → shot list` 编译器是**同一个数据模型**。而且它的**过渡是内置一等公民**（`transition` 字段），不是事后在外部工具里补溶解 —— **这一点本项目现在做反了**（溶解在 ffmpeg 里补）。**更值得学的是 `LoadFromRandom`：先用谱面结构自动铺满时间表，再允许人工覆盖。**

**Etterna**（旁证，未独立复核）：官方 Lua 文档证明它继承了 Actor/Lua 体系，并额外提供 **`JudgmentMessageCommand`**（判定发生，params 含 `TapNoteScore`/`HoldNoteScore`/`NoteRow`/`TapNoteOffset`/`Early`/`WifePercent`）与 **`CrossedBeatMessageCommand`**（`{ Beat = number }`）—— 即**判定驱动 + 按拍驱动**都是一等回调。**Etterna 是否仍解析 `#BGCHANGES`：未验证。** 来源：[docs.etterna.dev Actor Commands](https://docs.etterna.dev/ldoc/topics/Actor-Commands.md.html)。

### 4.4 其他节奏游戏的坦白结论

| 游戏 | 视觉是否谱面数据驱动 | 一手证据 |
|---|---|---|
| **Rock Band / RBN** | **是（最强商业前例）** | `VENUE` + `BEAT` + `EVENTS` 三条 MIDI 轨，见下 |
| **BMS** | **是**（`04`/`07`/`0A` 按谱面时间；`06` 按 miss；`A5` 按按键） | 见 4.2 |
| **StepMania 5** | **是**（`#BGCHANGES` 按 beat；random 模式按拍号/BPM 结构自动生成） | 见 4.3 |
| **Friday Night Funkin'** | **是（官方已内置）** | stage JSON `danceEvery` + song events + 回调，见下 |
| **osu!** | 部分（只读时间/hitsound/Pass-Fail，**读不到 BPM 与坐标**） | 见 4.1 |
| **Clone Hero** | **否（预渲染）** | 每曲一图/一视频 + 单个 `video_start_time`(ms) 偏移；song.ini 官方字段表**无任何视觉事件字段**；`modchart` 官方定义仅为 "Used when sorting the song list" |
| **DJMAX** | 未验证 | bmson 规范只证实"整屏背景素材 + 音符叠加"这一**呈现方式**，**是否预渲染视频未验证**。**初版本文断言"DJMAX 是预渲染、与 note 无绑定"属过度断言，已降级** |
| **CHUNITHM / maimai / Arcaea / Groove Coaster / WACCA / Quaver / Malody** | **未验证** | 无官方引擎/格式文档可抓。**不要引用** |

**Rock Band（最强商业前例，细节）**——官方 modding 文档原文："Cameras and lighting in Rock Band are controlled via **MIDI notes and text events on the VENUE track** of your MIDI file."

- 镜头切换 = 音符 **60 (C3)**；聚焦音符 61–64（bass/drums/guitar/vocals）；镜头类型 70–73；灯光关键帧 48/49/50；post-process 96–110。
- 文本事件：`[lighting (strobe_fast)]`、`[verse]`、`[chorus]`、`[bonusfx]`、`[do_directed_cut directed_guitar]`。
- **`BEAT` 轨**："contains only MIDI notes, and **drives the animations for the characters, lighting, and the crowd**"（C-1(12)=强拍，C#-1(13)=其余拍）。
- **演奏状态条件筛选**（不是纯谱面驱动）：`[bonusfx_optional]` = "the effect will only be triggered **when the player is doing well**"；`[do_optional_cut]` 同理。
- **⚠ 编译器可自动生成**："Once you place one camera or lighting MIDI text event in the VENUE track, **autogeneration is disabled**." → **Magma 在 VENUE 轨为空时会自动生成整套演出镜头/灯光，人工放一个事件就交出控制权。** 这与 StepMania `LoadFromRandom` 是**同一种"自动铺底 + 人工覆盖"架构**。
- 来源：[RBN2 Camera And Lights](http://docs.c3universe.com/rbndocs/index.php?title=RBN2_Camera_And_Lights)、[Mix and MIDI Setup](http://docs.c3universe.com/rbndocs/index.php?title=Mix_and_MIDI_Setup)。

**Friday Night Funkin'（现代官方实现）**

- stage JSON prop 字段含 **`danceEvery`**，官方原文："If non-zero, this prop will play an animation **every X beats** of the song… This value supports precision up to **`0.25`**, where `0.25` plays the animation four times per beat."
- 官方内建 song event 类（仓库文件清单）：`PlayAnimationSongEvent`、`ZoomCameraSongEvent`、`FocusCameraSongEvent`、`SetCameraBopSongEvent`、`SetStageSongEvent`、`SetCharacterSongEvent`、`ScrollSpeedEvent` → **事件存在谱面里、由 chart editor 摆放、运行时触发**。
- Conductor API 暴露 `currentBeat` / `currentStep` / `currentMeasure` / `songPosition`(ms) / `beatLengthMs` / `stepLengthMs` / `bpm` / `timeSignatureNumerator`；信号 `onBeatHit`/`onStepHit`/`onMeasureHit`。
- 回调 `onNoteHit(event:HitNoteScriptEvent)`（字段 `judgement`/`score`/`isComboBreak`/`hitDiff`）、`onNoteMiss`、`onSongEvent`，且**对 Stages 也可用** → 舞台脚本可直接订阅 note hit/miss/beat。
- **诚实更正**：官方 Conductor **没有 `crochet`**；`crochet`/`stepCrochet`/`curBeat`/`curStep` 是 **Psych Engine（社区分支）Lua API** 的命名。
- 来源：[Creating a Stage](https://funkincrew.github.io/funkin-modding-docs/04-custom-stages/04-01-creating-a-stage.html)、[Scripted Song Events](https://thekade.net/funkin-cookbook/category/advanced/12.scriptedevents.html)、[class Conductor](https://thekade.net/funkin-api/funkin/Conductor.html)。

**总体教训（修订）**：初版本文写"主流商业节奏游戏的 BGA 绝大多数是预渲染"——**这个结论下得太快**。一手证据显示**至少 Rock Band、BMS、StepMania、FNF 四个体系都是真正的谱面数据驱动**，而且**商业 3A 的 Rock Band 就在其中**。真正预渲染的确认案例是 Clone Hero。

**四种架构范式**（可直接拿去对照本项目）：

| 范式 | 代表 | 数据形态 | 粒度 |
|---|---|---|---|
| **A. 谱面时间轴事件** | BMS `04/07/0A`、StepMania `#BGCHANGES`、RB VENUE | `(time, asset)` 列表 | 到点换整屏素材 |
| **B. 编译期烘焙 + 自动生成** | **RB Magma 自动 VENUE**、**SM `LoadFromRandom`** | 从 BPM/拍号/小节**算出**时间点 | 自动铺满全曲，零手工打点 |
| **C. 运行时信号绑定** | TouchDesigner CHOP、Resolume FFT、ISF | 连续标量流 | 逐帧改参数，无"事件"概念 |
| **D. 分析→具名特征→表达式** | projectM、Synesthesia、VDMX | bass/mid/treb + beat + BPM | 作者用方程组合具名特征 |

**本项目最该学的是 A + B**：先用谱面的结构化时序（`tempos`、`sections`、note 密度）**自动算出事件时间表**（B），再落成一份引擎可消费的事件清单（A）。**C/D 是音频侧范式；当谱面已经存在时，用谱面比用 FFT 既更精确也更确定。**

**对"该绑什么"的最终提醒**：`chart.json` 里有 `notes[{tick, pathId, action}]`，note 有 3D 位置。但**在所有已核实的先例里，"note 空间位置驱动背景"都不是常规做法**。真正稳妥的绑定量是：**时间、段落、密度、命中类型（tap/hold/flick）、按键状态、以及玩家表现（judge/miss/combo）**。这与本项目 `effectClips.target` 已支持 `judgement` 的设计方向一致。

### 4.5 VJ 软件：有没有值得偷的"音频分析 → 视觉参数"管线

**有，但必须先纠正一个流行误解：TouchDesigner 和 Resolume 都 *不* 做自动节拍检测。**

> **⚠ 本节为 v2 修订**：初版本文写"TouchDesigner 的 `Beat CHOP` 做**实际节拍检测**"。**这是错的** —— 官方文档明确 Beat CHOP 无音频输入，BPM 靠**人手 tap**。下表已更正。

#### 谁真的做检测（一手文档核实）

| 工具 | 真 FFT | 真 onset | 真自动 BPM/beat | 绑定形式 |
|---|---|---|---|---|
| **TouchDesigner** | ✅ `Audio Spectrum CHOP` | ❌ | ❌ **`Beat CHOP` 不检测音频** | CHOP 通道 → export 到任意参数 |
| **Resolume Arena/Wire** | ✅ Wire `Spectrum In` = 1024 float bins | ❌ | ❌ **手动 set/tap**，或 Ableton Link / StageLinQ / Pro DJ Link / SMPTE | FFT → 参数（L/M/H + Gain + Fall） |
| **VVVV** | ✅（VL.Audio） | 未验证 | ✅ 官方称可选包 **VL.Audio.GPL** 提供 "beat tracking"（**GPL-3.0**） | VL 数据流 → 任意 pin |
| **Notch** | ✅ `Sound FFT Modifier` 等 | 节点名存在，语义未验证 | `BPM Modifier` 存在，**是否自动检测未验证** | Modifier/Effector 挂参数 |
| **VDMX** | ✅ Audio Analysis（任意数量频率 filter） | 间接 | ✅ **真·BPM Detection**（"an extremely accurate beat tracker"） | filter → data-source → 任意参数 |
| **Modul8** | 未验证 | ❌ | ❌ 只有 beat **发生器**（tap 时钟） | 音频电平 → 参数 |
| **Synesthesia** | ✅ 4 频段 + `syn_Spectrum` | ✅ **`syn_*Hits`** | ✅ **`syn_BPM`/`syn_BPMConfidence`/`syn_OnBeat`** | SSF uniform → GLSL，逐帧 |
| **ISF 规范** | 有 `audio`/`audioFFT` 输入类型 | ❌ 规范不含 | ❌ 规范不含 | shader 内以 image 采样 FFT |
| **projectM** | ✅ MilkdropFFT | ✅ | ✅ "detecting tempo" | 预设方程消费 audio feature data |

**TouchDesigner 官方原文（证明它靠人耳）**：

> "The Beat CHOP generates a variety of ramps, pulses and counters that are timed to the beats per minute and the sync as produced by the **Beat Dialog**."
> "**The Beat Dialog is used to manually tap the beat to set the beats-per-minute (BPM).**"
> Beat Dialog："press the Tap button **EVERY 4 BEATS**" / "will **average out your taps** to the closest BPM"

**Resolume 官方原文**：

> "by far the best way to get the BPM is to **just use your ears and tap along to the music**"

**Synesthesia 是最值得偷的一个** —— 它是唯一把"分频段瞬态 + beat + 连续 BPM 估计 + 分频段包络"**同时作为一等 shader uniform** 暴露的工具：

- `syn_Spectrum` 纹理：r = Raw FFT、**g = Juiced FFT**、b = Smooth FFT、a = Waveform
- **Juiced FFT 的定义就是一条可直接照抄的预处理配方**："incorporates **bin smoothing, temporal smoothing, logarithmic frequency scaling, and high-end boosting**"
- `syn_Hits` / `syn_BassHits` / …："detect 'hits' within specific frequency bands, **spiking in value during the isolated transients**"
- `syn_BPM`(50–220) / `syn_BPMConfidence` / `syn_OnBeat` / `syn_ToggleOnBeat` / `syn_RandomOnBeat` / `syn_BeatTime`
- 来源：[Synesthesia SSF 文档](https://www.synesthesia.live/docs/ssf/ssf.html)、[audio_uniforms](https://www.synesthesia.live/docs/ssf/audio_uniforms.html)

**ISF 规范**（已核实，结清原 #15）：v2.0 的 `TYPE` 枚举含 **`audio`** 与 **`audioFFT`**（v1 没有；**没有** `audioLevels`）。数据**以图像形式**传入 shader：image 每一行 = 一个声道，`audio` 每一列 = 一个波形样本（值围绕 0.5 居中），`audioFFT` 每一列 = 一个 FFT 值，最后一列 = **Nyquist**。**规范不含 beat/onset/tempo 类型 —— beat 必须由 host 提供或 shader 自推。** 来源：[ISF ref_json](https://docs.isf.video/ref_json.html)、[primer_chapter_8](https://docs.isf.video/primer_chapter_8.html)。

**projectM**（"auto-VJ"正典）：官方 README —— "read an audio input and to produce mesmerizing visuals, **detecting tempo**"；"analyzing audio PCM data with **beat detection and FFT**, applying the preset to the **audio feature data**"。LGPL-2.1。

#### 三条可偷的工程教训

1. **把分析结果规范化成少量具名标量，比暴露 1024 个原始 bin 好用得多。** projectM 的 `bass/mid/treb` + attack 值、Synesthesia 的 `syn_BassHits` 都是这个思路。**TouchDesigner / Resolume 那一侧最缺的正是这一层** —— 它们把原始 FFT 交给用户手工接线。
2. **"节拍"应建模成连续信号（相位/脉冲），而不是离散事件。** TD 的 `Beat CHOP` 输出 `ramp/pulse/bar/beat/sixteenths/rampbeat/bpm` 一族信号，正因为如此它才能被任意参数消费。**这正好对本项目的接缝问题有用**：把 beat phase 当连续量驱动参数，就不必每次硬切。注意：**它的 BPM 是人手给的，但"把 BPM 变成连续相位信号"这个设计本身值得偷** —— 而本项目恰好天生就有精确的 `tempos` 与 `ticksPerBeat`，**比 tap 精确得多**。
3. **原始 FFT 直接驱动视觉会很难看，必须先做"Juiced"预处理**（bin 平滑 + 时域平滑 + 对数频率缩放 + 高频提升）。见上面 Synesthesia 的定义。

**ComfyUI 里已有的对应物（实测节点）**：`SoundReactive`（KJNodes）、`AudioReactiveTransform`（nodesweet）、`FL_Audio_Reactive_Scale/_Speed/_Brightness/_Saturation/_Edge_Glow/_Envelope`（Fill-Nodes）、`AudioWeightsRemap`（nodesweet）。这些节点的**具体实现与精度未验证**，但它们证明"音频包络 → 图像参数"在 ComfyUI 生态里已是现成积木。

### 4.6 直接命中的发现：ComfyUI 里已有一个"节拍 → 镜头表"编译器

这是第 4 节里对本项目最有用的一条。实测通过 `object_info` 提取到的节点签名：

```
FL_Audio_Music_Video_Sequencer   <-- custom_nodes.ComfyUI_Fill-Nodes
   req audio                 AUDIO
   req beat_positions        STRING   (默认空)
   opt pattern_A             STRING   def=4,4,8,4,4,8
   opt pattern_B             STRING   def=2,2,2,2
   opt pattern_C             STRING   def=8,8
   opt pattern_D             STRING   def=16
   opt pattern_sequence      STRING   def=A
   opt fps                   INT      def=30
   opt repeat_pattern        BOOLEAN  def=True
   opt max_shots             INT      def=1000

FL_Audio_Shot_Iterator           <-- ComfyUI_Fill-Nodes
   req sequence_json         STRING
   req shot_index            INT      def=0
```

**这说明**：`pattern_A = "4,4,8,4,4,8"` 是**以"拍"为单位的镜头长度序列**；`beat_positions` 是外部注入的拍时间串；输出 `sequence_json` 由 `FL_Audio_Shot_Iterator` 按 `shot_index` 逐镜消费。

**这就是第 5 节方案 (b) 需要的"chart → shot list 编译器"，而且已经存在。** 本项目的谱面有精确的 `tempos` 与 `ticksPerBeat`，可以直接算出 `beat_positions` 并**按段落设计 pattern**（例如主歌 `4,4,8`、副歌 `2,2,2,2`、drop 处 `16`）。这比从零写一个镜头表编译器省掉一大块工作。

**未验证**：`beat_positions` 的字符串格式（逗号分隔秒数？拍号？）、`sequence_json` 的 schema、以及该节点是否支持非均匀拍网格。**这些必须先做一次小实验确认。**

---

## 5. 可落地管线（按 投入 / 收益 排序）

前提重申：**本项目已有 `BGA/fusion` 确定性渲染器，并与 Unity 运行时对拍通过（position 误差 0.000018 units）。** 所以下面三条路线的核心问题都是"AI 放在哪一环，才不会破坏已证明的等价关系"。

### 方案 A：纯确定性渲染（零 AI）— **已经建成，只需扩产**

| 项 | 内容 |
|---|---|
| 组件 | `BGA/fusion/tools/render_bga.py`（numpy 软渲染，1920×1080@60）、`score.py`、`audio_analysis.py` |
| 谱面贡献 | **全部**：`cameraKeys`/`cameraZKeys`/`cameraPoseKeys` 决定相机；`sections` 决定段落；`notes` 决定光轨；`effectClips` 决定闪光/配色；`tempos` 决定拍网格 |
| 保留给 AI | **无** |
| 实测成本 | 1200 帧 / 1920×1080 / 60fps，24 线程约 **10 分钟**（`BGA/fusion/README.md`），**零 API 费用** |
| 确定性 | **完全确定、逐比特可复现**。无 seed 概念 |
| 失败模式 | 表现力上限受限于手写渲染代码；新风格要写新代码 |
| 已完成度 | **已交付** `BGA/ChartPackages/FusionSpire_20s.grchart`，134 notes / 6 轨 / 8 段，`verify_package.py` 16 passed 0 failed |

**这条路的真实价值被低估了**：它已经解决了 AI 路线最难的部分 —— **相机精确、节拍精确、与游戏渲染一致**。任何 AI 方案都应该把它当**底座**，而不是替代品。

### 方案 B（推荐）：确定性脊柱 + AI 只换质感 — **控制视频路线**

核心思想：**让 AI 做它擅长的（质感、光照、材质、氛围），把相机与时间轴留给已被证明的渲染器。**

```
chart.json ──┬─> render_bga.py --control  ──> control_video (depth/edge/白模)
             │                                        │
             │                                        v
             │                          Wan22FunControlToVideo   (原生节点)
             │                          / WanVaceToVideo          (原生节点)
             │                                        │
             └─> beat grid + sections ──> shot list ──┤
                                                      v
                                        WanVideoContextOptions
                                        (context_overlap=16, fuse=pyramid)
                                                      │
                                                      v
                                        ffmpeg 封装 ──> video-bga 包
```

| 项 | 内容 |
|---|---|
| 组件 | 现有 `render_bga.py`（**新增一个 `--control` 输出模式**，只渲染几何/深度/边缘，不做 bloom 与调色，速度远快于成片）；**原生节点** `Wan22FunControlToVideo` 或 `WanVaceToVideo`；**包装器节点** `WanVideoContextOptions`；现有 ffmpeg 封装链 |
| 谱面贡献 | **相机与剪辑点的唯一真源**。控制视频的每一帧都由 `cameraKeys`/`notes`/`sections` 生成 |
| 保留给 AI | 只负责**外观**：把发光线条/白模变成具体材质与光照；可换 LoRA 换风格 |
| 关键优势 | **运镜与切点由构造保证精确**，因为它们是输入而不是模型的输出。这就绕开了 3.2 节的结论（`WanCameraEmbedding` 不接受自定义逐帧位姿） |
| 成本 | Wan 2.2 TI2V-5B 官方口径："5-second 720P video in under 9 minutes on a single consumer-grade GPU"，原生 720P 为 `1280*704` @24fps，需 ≥24GB VRAM（如 4090） |
| 确定性 | **控制视频完全确定**；AI 输出在给定 seed + 相同控制视频 + 相同 prompt 时**原则上可复现**，但项目的既有经验是"正式视频需看结果选片"（`Docs/VideoBgaWorkflow.md`），**不应当假定一次成片** |
| 失败模式 | ① 模型可能**不尊重**控制视频的几何（控制强度 `strength` 需要调）；② 需要下载 **Fun-Control 专用权重** —— 本项目云端已确认的模型清单里**没有** Fun-Control 检查点（见 `Docs/CloudComfySetup.md`），这是一个**必须补的前置条件**；③ 控制视频若太"干净"，模型可能不产生足够细节 |

**为什么这是推荐路径**：它是唯一一个**在保留已证明的相机等价性的同时获得 AI 质感**的方案。而且它全部使用**已经在本项目 ComfyUI 里实测存在的原生节点**。

### 方案 C：按段落生成 + 逐帧关键帧引导 — **不需要控制视频的折中**

如果不想做控制视频（因为要下 Fun-Control 权重），可以用**帧号精确的关键帧引导**：

| 组件 | 作用 | 实测签名 |
|---|---|---|
| `LTXVAddGuide` | 把一张参考图钉在**指定帧号** | `image` + **`frame_idx`(INT)** + `strength` + `latent` |
| `LTXVAddGuideMulti`（KJNodes） | 一次钉多张图 | `num_guides`（DynamicCombo） |
| `LTXVGeneratedKeyframesToGuides`（原生 `nodes_lt_keyframes`） | 把关键帧集合转成 guide | — |
| `LTXVLinearOverlapLatentTransition` | **latent 空间**的带重叠过渡 | `samples1`/`samples2`/**`overlap`** |
| `LTXVLaplacianPyramidBlend` | 拉普拉斯金字塔无缝混合 | `image_a`/`image_b`/`mask` |
| `FL_Audio_Music_Video_Sequencer` | 拍 → 镜头表 | 见 4.6 |
| `FL_Audio_Segment_Extractor` | 按 `start_beat`+`beat_count` 切音频 | 见 2.5 |

**做法**：用谱面的 `sections` + `tempos` 算出**每个段落的起始帧号**，把该段落的参考图用 `LTXVAddGuide(frame_idx=…)` 钉进去。这样**接缝落在整拍上是由帧号保证的**，不依赖模型听话。

| 项 | 内容 |
|---|---|
| 谱面贡献 | 段落的**帧号**（由 `startBeat` + `tempos` + fps 精确换算），以及每段的镜头长度（拍数） |
| 保留给 AI | 段内画面与运动 |
| 优势 | **不依赖控制视频**，不需要 Fun-Control 权重；接缝用 `overlap` 参数在 latent 里处理 |
| 失败模式 | ① **段内相机的自由度仍在模型手里** —— 这会让"note 轨道贴合视频"的等价关系失效（本项目 `BGA/fusion` 花大力气建立的正是这个关系，见 `README.md` 里那个四元数 bug 的教训）；② `LTX` 与 `Wan` 混用会造成接缝处画风跳变（本项目文档已记录 Kubeez 的同款警告） |

**结论**：方案 C 适合**"背景是氛围层、不与 note 轨道对齐"**的曲目；不适合本项目 `fusion` 那种"视频里的轨道 == 游戏里 note 滑过的轨道"的强对齐需求。

### 三条路线的取舍总表

| | A 纯确定性 | **B 控制视频（推荐）** | C 关键帧引导 |
|---|---|---|---|
| 相机精确性 | **精确**（已验证） | **精确**（由构造保证） | 不精确 |
| 与 Unity 运行时的一致性 | **已对拍 MATCH** | 保持（控制视频来自同一渲染器） | 破坏 |
| 需要新权重 | 否 | **是**（Fun-Control / VACE） | 否（LTX 路径） |
| 质感上限 | 手写代码上限 | **高**（可换 LoRA） | 高 |
| 成本 | 免费（约 10 min/20s） | GPU 或 API | GPU 或 API |
| 确定性 | **逐比特** | 控制视频逐比特；AI 部分需选片 | 需选片 |
| 建议 | **保持为兜底与对齐基准** | **主线** | 氛围型曲目备选 |

---

## 6. 成本与可复现性现实核查

### 6.1 算力 / 费用

| 路线 | 每分钟成片（1080p）的算力 | 折算费用 | 来源 |
|---|---|---|---|
| **A 确定性渲染** | 约 **30 分钟 CPU**（20s/10min，24 线程线性外推）；**0 GPU** | **￥0** | 项目实测 `BGA/fusion/README.md` |
| **B/C 本地 Wan 2.2 TI2V-5B** | 官方："5-second 720P video **in under 9 minutes** on a single consumer-grade GPU"。即 12×9 ≈ **约 108 分钟 GPU / 分钟成片**（640×352 或 1280×704，视显存）。需 **≥24GB VRAM** | 按租用 4090 约 ￥2/h 量级估算 ≈ **￥3–4/分钟**（**价格为估算，未验证**） | [Wan2.2 README](https://github.com/Wan-Video/Wan2.2) |
| **B/C 云端 Comfy API** | — | 本项目**当前被判定为免费档**，`/api/prompt` 返回 `FREE_TIER_NOT_ALLOWED`，**API 路径目前不可用** | `Docs/CloudComfySetup.md` |
| **LTX-2 API** | — | 官方定价：**Fast 起 \$0.04/秒、Pro 起 \$0.08/秒、Ultra 起 \$0.16/秒** → Pro 约 **\$4.8/分钟**，Ultra 约 **\$9.6/分钟**；"随分辨率（720p–4K）与是否含音频而变" | [LTX-2 官方新闻稿](https://ltx.io/newsroom/ltx-2-is-now-open-source-full-model-weights-released) |
| **Ovi** | 121 帧 720×720 / 50 步：FA3+offload 约 **83–140 s**；8 卡约 40 s。**≥32GB VRAM**（fp8+offload 可降到 24GB） | 折算约 **7–12 分钟 GPU / 分钟成片** | [character-ai/Ovi README](https://github.com/character-ai/Ovi) |

**重要修正 —— 一个可能省下大量成本的发现**：本项目文档写 "Wan 2.2 TI2V 5B … 原生生成 640×352、24fps"，并计划"在更高显存机器用 1280×704 重跑"（`Docs/VideoBgaWorkflow.md`）。但**官方 README 明确 TI2V-5B 的 720P 就是 `1280*704`（或 `704*1280`），且只需 ≥24GB VRAM（RTX 4090）**。

**即 640×352 是 ComfyUI 默认工作流的尺寸，不是模型的能力上限。** 用同一份白模与参考帧、同一台 4090，**不需要"更高显存机器"**就能出 `1280×704`。这条如果成立，可以直接解决项目文档里"最短板是参考图"之外的第二个瓶颈。**建议优先做一次单点验证。**

### 6.2 可复现性

| 路线 | 给定谱面是否可复现 | 说明 |
|---|---|---|
| A | **是，逐比特** | 无随机性。`effectClips` 与 `cameraMotionClips` 带 `seed` 字段，说明运行时特效也已 seed 化 |
| B | **控制视频：是**；最终成片：**部分** | 控制视频由 numpy 渲染器生成，完全确定。AI 阶段给定 seed + prompt + 控制视频**原则上**可复现，但项目既有经验是必须选片（`selected-takes.json`、`Docs/VideoBgaWorkflow.md` 记录第 1 段用 CFG 4、第 5 段用 CFG 3.5、其余 CFG 5） |
| C | **部分** | 同 B，且引导图放置本身是确定的 |
| **轨迹控制（`WanMoveTrackToVideo`）** | **否，即便固定 seed** | 3.2(c)：`replace_feature()` 与 `create_pos_embeddings()` 内部调用 `torch.randperm`（随机打乱轨迹顺序 / 随机抽轨迹），**节点未暴露 seed**。这是源码级确认的不可复现点 |

**诚实的核心结论**：**不要指望"同一份谱面 + 同一个 seed → 同一段 BGA"。** 现有管线已经在做人工选片（`selected-takes.json` 是证据）。可复现的是**结构**（相机、切点、拍对齐），不是**像素**。这正好是方案 B 的立论基础：**把必须可复现的部分（相机与时间）从 AI 手里拿走，只让 AI 负责允许有随机性的部分（质感）。**

**并且注意**：即便在"AI 负责质感"的范围内，**不同节点的可复现性也不一样**。`Wan22FunControlToVideo` / `WanVaceToVideo` 走的是确定性的 VAE 编码 + conditioning（无 `randperm`），而 `WanMoveTrackToVideo` 自带随机性。**方案 B 选控制视频而不是轨迹，在可复现性上也是更优的选择。**

### 6.3 隐藏成本与合规

| 项 | 状态 |
|---|---|
| **ffmpeg 许可证** | 本地是 GPL 构建（`--enable-gpl --enable-version3`），项目已记录"公开分发前需补齐许可证及源码合规包"，见 `BGA/tools/bin/FFMPEG-NOTICE.txt` 与 `Docs/VideoBgaWorkflow.md` |
| **essentia** | AGPL-3.0，**不建议引入** |
| **madmom** | PyPI 版本仅支持 Python<3.10 / numpy<1.20，本项目 3.12 + numpy 2.5.3 **装不上**，须走 GitHub；建议直接用 beat_this 并**不开 DBN** |
| **demucs 权重许可证** | **未验证** —— 代码 MIT 不等于权重 MIT |
| **Ovi 许可证** | **未验证**（README 未声明，GitHub API 抓取时触发限流） |
| **LTX-2 权重是否已开放** | **未验证** —— 官方新闻稿标题与正文口径冲突，正文称"later this fall" |
| **Wan 系列** | **Apache-2.0**，且官方声明不对生成内容主张权利。**这是本方案里许可证最干净的一环** |
| **仓库依赖洁净度** | `BGA/tools/pylibs` **只有 numpy**，Python 3.12。引入 beat_this/allin1/demucs 都会拉入 **PyTorch（数 GB）**，破坏"可移植、零重依赖"的现有性质。**建议：分析工具离线跑，结果以 JSON 提交进仓库，不进运行时依赖** |

---

## 推荐路径

**做方案 B：把现有 `BGA/fusion` 确定性渲染器当作"相机与时序的唯一真源"，新增一个 `--control` 输出模式，用它的输出作为 `Wan22FunControlToVideo` 的控制视频，让 Wan 2.2 只负责把几何换成质感。**

理由，按重要性排序：

1. **本项目已经付清了 AI 路线最贵的那笔账。** `render_bga.py` 与 Unity 运行时的对拍结果是 `position 0.000018 units`、`RESULT: MATCH`。这个等价关系（视频里的轨道 == 游戏里 note 滑过的轨道）是**任何"让模型自由发挥运镜"的方案都会摧毁的资产**。方案 B 是唯一能同时保住它并获得 AI 质感的路线。
2. **它绕开了已核实的硬限制。** 3.2 节实测表明 `WanCameraEmbedding` 的 `camera_pose` 是预设 COMBO，**不接受自定义逐帧位姿**。所以"用节点参数控制相机"这条路是走不通的；**把相机做成输入视频**才走得通。
3. **全部使用已实测存在的原生节点**：`Wan22FunControlToVideo`（原生 `comfy_extras.nodes_wan`，接受 `control_video`）+ `WanVideoContextOptions`（`context_overlap=16`、`fuse_method=pyramid`）。
4. **`WanVideoContextOptions` 直接替代现有的 ffmpeg 交叉溶解**，从根上解决项目记录在案的接缝问题（6.0 s 处硬切 5.86×、溶解后仍有 2.33×，原因是素材只有 181 帧、比 6 秒窗口多 33ms）。让模型在 latent 空间重叠融合，不再受"素材不够"限制。
5. **Wan 是许可证最干净的选择**：Apache-2.0，且官方明确不主张生成内容权利。相比之下 essentia 是 AGPL、demucs 权重许可证未验证、LTX-2 权重开放状态口径矛盾。
6. **纠正一个可能的成本误判**：TI2V-5B 官方支持 `1280*704` @24fps 且**只需 24GB 显存**，不需要"更高显存机器"。建议立刻做一次单点验证。
7. **它符合两个被商业验证过的架构范式。** 第 4 节核实到的最强先例都采用同一种"**自动铺底 + 人工覆盖**"结构：StepMania `LoadFromRandom()` 在无人为安排时用拍号/BPM 结构**自动生成**整张背景切换表；Rock Band 的 Magma 在 `VENUE` 轨为空时**自动生成**全套镜头/灯光，一旦人工放入一个事件就交出控制权。**方案 B 正是这个结构**：`render_bga.py` 按谱面自动生成全部运镜与切点，人只需在不满意的段落覆盖 prompt/参考图。

**具体落地顺序（每步都可独立验证，失败不阻塞下一步）**：

| 步 | 动作 | 验收 |
|---|---|---|
| 1 | 给 `render_bga.py` 加 `--control` 模式（只出几何/深度/边缘，跳过 bloom 与调色） | 输出帧数、时长与成片**逐帧对齐**；渲染速度显著快于成片 |
| 2 | 单点验证 TI2V-5B 在 `1280*704` 的真实吞吐与显存 | 官方"5s 720P <9min @≥24GB"是否在本地复现 |
| 3 | 补 **Fun-Control 权重**（当前云端模型清单里没有） | `Wan22FunControlToVideo` 能加载并出片 |
| 4 | 20 秒对照实验：老管线（6s 片段 + ffmpeg 溶解）vs 新管线（control video + context windows） | 用**项目已有的** `BGA/tools/analyze_seams.py` 比较接缝倍数，目标是与"正常帧间差"同量级（<2×） |
| 5 | 加 `beat_this`（MIT、CPU 可跑、**离线**）补 downbeat 与段落，结果落成 JSON 提交进仓库；**不进运行时依赖** | 与现有 `audio_analysis.py` 的拍网格交叉验证 |
| 6 | 用 `FL_Audio_Music_Video_Sequencer` 的 `pattern_A` 形式表达"每段镜长（单位：拍）" | **先做格式小实验**：确认 `beat_positions` 与 `sequence_json` 的实际 schema |
| 7 | 把"镜头表"落成插件格式的**显式事件清单**（对齐 4.4 的范式 A），并允许逐段人工覆盖 prompt/参考图（对齐范式 B） | 生成结果与镜头表逐条可对账；覆盖项生效且不影响未覆盖段 |

**已核实的既有直驱源（第 2、4 节汇总，无需额外研究即可用）**：本项目 `chart.json` 里的 `tempos`（BPM 段）与 `sections`（带 `name` 的人工段落标签）正好提供 StepMania `LoadFromRandom` 所用的同类结构，**且语义更强**；`notes` 可按拍统计密度；`effectClips.target` 已支持 `judgement`，与 FNF/Etterna 的"判定驱动视觉"回调同构。**本项目的谱面在结构上已经具备做这些先例所做的事的全部信息。**

**明确不做的事**：不采用 Wan2.2-S2V（口型工具，对器乐无信号，见 1.2）；不采用 Ovi（它**生成**音频而非接收音频，其 TODO 里 "Reference voice condition" 仍未勾选，见 1.1）；不把 **MotionCtrl / TrajectoryCrafter** 纳入计划（两者**未能核实**一手仓库，且本项目 ComfyUI 节点面里也没有）；**不用 `WanCameraEmbedding` 做谱面运镜**（它只有 9 个预设 + 匀速斜坡，见 3.2(a)）；**不用 `WanMoveTrackToVideo` 做需要严格可复现的运镜**（其内部有无 seed 的 `torch.randperm`，见 3.2(c)）；不引入 essentia（AGPL，见 2.2）；**不照抄"黑=透明"作为普适规则**（那是 BMS `07` 层的规范行为，跨实现有分歧 —— 直接出带 alpha 的 PNG，见 4.2）。

**保底**：方案 A 继续作为**对齐基准与兜底**。任何时候 AI 阶段产不出可用结果，`BGA/fusion` 的确定性管线都能独立交付一条与谱面完全一致的 BGA —— 这是本项目相对所有"AI 生成 BGA"尝试的**结构性优势**，不应为了追求 AI 质感而丢掉。

---

## 附：待验证清单

### A. 本轮已**结清**的项（原为未验证，现已读到一手来源）

| # | 原问题 | 结论 | 来源 |
|---|---|---|---|
| 6 | `WanCameraEmbedding` 的 `camera_pose` 枚举 | **结清**：恰好 9 个预设 `Static` / `Pan Up` / `Pan Down` / `Pan Left` / `Pan Right` / `Zoom In` / `Zoom Out` / `Anti Clockwise (ACW)` / `ClockWise (CW)`；运动为**线性匀速**斜坡，仅有 `speed` 可调 | `comfy_extras/nodes_camera_trajectory.py` 源码 |
| 7 | `tracks` JSON schema 与坐标系 | **结清**：`[[{"x":..,"y":..}, ...], ...]`（单轨时省略外层数组）；坐标为**目标 `width`×`height` 像素坐标**；兼容单引号；**解析失败静默退化为普通 I2V** | `nodes_wan.py` `parse_json_tracks()`、`nodes_wanmove.py` |
| 14a | **CameraCtrl** 是否存在 | **结清（存在性）**：仓库 `hehao13/CameraCtrl`，由 ComfyUI 官方源码 `Copied from` 注释确认 | `nodes_camera_trajectory.py` |
| 14b | **Wan-Move** 是否存在 | **结清（存在性）**：仓库 `ali-vilab/Wan-Move`，由 `nodes_wanmove.py` 顶部注释确认 | `nodes_wanmove.py` |
| — | Wan 2.2 是否原生支持 1280×704 | **结清**：`Wan22ImageToVideoLatent` 默认值即 `width=1280, height=704`；官方 README 称 720P = `1280*704`，需 ≥24GB VRAM | `nodes_wan.py` 源码 + 官方 README |
| 12a | BMS 层优先级与"黑=透明"规则 | **结清**：绘制顺序 `04`→`07`→`0A`；Angolmois 原文给出四层定位与"非 alpha 图黑=透明"；`07` 黑透明是**规范级**（bmson 规范专门声明不继承） | [Angolmois INTERNALS](https://raw.githubusercontent.com/lifthrasiir/angolmois/master/INTERNALS.md)、[bmson-spec](https://bmson-spec.readthedocs.io/en/master/doc/index.html) |
| 12b | BMS **通道号**（初版曾写错） | **结清**：`04`=BgaBase、`06`=BgaPoor、`07`=BgaLayer、`0A`=BgaLayer2；**`01`=BGM、`02`=小节长度、`03`=整数 BPM，都不是 BGA 通道** | [BM98 1998 原始规格](http://bm98.yaneu.com/b98/bmsformat.html)、[bms_rs channel.rs](https://docs.rs/bms-rs/0.9.0/src/bms_rs/bms/command/channel.rs.html) |
| 12c | bmson 是否砍掉 BGA | **结清（否）**：`BGA{ bga_header, bga_events, layer_events, poor_events }` 完整存在；1.0.0 只是改名为 snake_case；规范原文 "Currently, BGA specification is just compatible with BMS." | [bmson-spec](https://bmson-spec.readthedocs.io/en/master/doc/index.html) |
| 12d | `#SWBGAxx` 语义 | **结清**：`bms_rs` 的 `SwBgaEvent` 结构体给出 `frame_rate`/`total_time`(0=按住期间播)/`line`(键位通道)/`loop_mode`/`argb`/`pattern` | [docs.rs SwBgaEvent](https://docs.rs/bms-rs/0.8.0/bms_rs/bms/command/minor_command/struct.SwBgaEvent.html) |
| 13 | StepMania `#BGCHANGES` 字段顺序 | **结清**：11 个字段按下标解析（0=beat, 1=file1, 2=rate, 3/4/5=兼容位, 6=effect, 7=file2, 8=transition, 9/10=color1/2）；`rate` 经 `ActorFrame::UpdateInternal` 的 `fDeltaTime *= m_fUpdateRate` 生效 | [NotesLoaderSM.cpp](https://raw.githubusercontent.com/stepmania/stepmania/master/src/NotesLoaderSM.cpp)、[ActorFrame.cpp](https://raw.githubusercontent.com/stepmania/stepmania/master/src/ActorFrame.cpp) |
| 15a | ISF 是否有标准化音频输入 | **结清（有）**：`TYPE` 含 `audio` 与 `audioFFT`（v2 新增，**无** `audioLevels`）；以**图像**形式传入（每行=声道，每列=样本/FFT bin）；**规范不含 beat/onset/tempo** | [ISF ref_json](https://docs.isf.video/ref_json.html)、[primer_chapter_8](https://docs.isf.video/primer_chapter_8.html) |
| 16a | FNF 视觉是否谱面驱动 | **结清（是，官方内置）**：stage JSON `danceEvery`（每 X 拍，精度 0.25）+ song events + note hit/miss 回调 + Conductor API | [Creating a Stage](https://funkincrew.github.io/funkin-modding-docs/04-custom-stages/04-01-creating-a-stage.html) |
| 16b | 商业游戏是否存在谱面驱动视觉 | **结清（存在）**：**Rock Band** 的 `VENUE`/`BEAT`/`EVENTS` MIDI 轨即谱面数据驱动，且 Magma 可自动生成 | [RBN2 Camera And Lights](http://docs.c3universe.com/rbndocs/index.php?title=RBN2_Camera_And_Lights) |

### B. 仍**未验证**的项

| # | 未验证内容 | 建议核实方式 |
|---|---|---|
| 1 | LTX-2 权重是否已实际开放下载 | 查 `Lightricks/LTX-2` 仓库与官方 release notes；官方新闻稿标题与正文口径自相矛盾 |
| 2 | LTX-2 的音频条件到底控制什么（口型/节拍/氛围） | 读 LTX-2 论文或官方文档的 conditioning 章节 |
| 3 | LTX-2 的许可证名称与商业条款 | 官方 licensing 页正文（本轮抓取被截断） |
| 4 | Veo 3 原生音频的确切性质 | 抓 Google 官方模型卡（本轮该域抓取失败） |
| 5 | Ovi 的许可证 | GitHub API 限流，需重试 |
| 8 | `FL_Audio_Music_Video_Sequencer` 的 `beat_positions` / `sequence_json` 格式 | 读 `ComfyUI_Fill-Nodes` 源码（**推荐优先，见 4.6**） |
| 9 | allin1 / MSAF 的仓库、许可证、维护状态 | GitHub API 重试 |
| 10 | Demucs 预训练**权重**的许可证 | 官方仓库 MODEL_LICENSE 或模型卡 |
| 11 | BS-RoFormer / Mel-Band RoFormer / Apollo 的官方 benchmark 排名 | 官方仓库或 MDX23 榜单原文 |
| 12e | BMS `#@BGAxx` 原始规格、`#POORBGA` 层级模式、`#ExtChr` 语义、`#SCROLL`/`#SPEED` 换算公式 | hitkey 单页 ~1MB 抓取被中段截断；需分段取或另找规范 |
| 12f | LR2 官方 skin 规范、beatoraja 的 `#SWBGA`/`#POORBGA` 支持与 Lua 判定回调、DTX/DTXMania 分层通道号 | 官方站点已下线/404，需 archive 或源码 |
| 13b | StepMania 是否仍有 C++ `Effect`/`RageEffect` 类（`src/Effect.h` 在 master 404，`BackgroundEffects/` 实为 `.lua`）；`.ssc` loader 的 `#BGCHANGES` 处理；Etterna 是否仍解析 `#BGCHANGES` | 读 `NotesLoaderSSC.cpp` 与 Etterna 源码 |
| 13c | osu! storyboard 第 5 层 `Overlay` 的写法/编号；storybrew 的 API | 数值表只到 3；storybrew README 过薄 |
| 14c | **MotionCtrl / TrajectoryCrafter** 是否存在且可用 | 逐个核实仓库、arXiv、许可证（未取得一手证据） |
| 14d | CameraCtrl / Wan-Move / ReCamMaster / ATI 的**星数、许可证、arXiv 编号** | 存在性已确认，但元数据需重试 GitHub API |
| 15b | Notch 的 `Sound*`/`BPM Modifier` 节点参数语义（尤其 `BPM Modifier` 是否自动检测）；VVVV `VL.Audio.GPL` 的节点名/算法 | 详情页 JS 渲染，`docs.notch.one` 不存在。**不可断言 `BPM Modifier` 是自动检测** |
| 16c | DJMAX BGA 是否预渲染视频；CHUNITHM / maimai / Arcaea / Groove Coaster / WACCA / Quaver / Malody / GH3 `.chart` events | 无官方引擎/格式文档可抓。**不要引用** |
| 16d | Psych Engine 的 `curStep`/`curBeat`/`crochet`/`stepCrochet` | 官方 FNF 确认**没有** `crochet`（改用 `beatLengthMs`/`stepLengthMs`）；Psych 是社区分支 |
| 17 | GPU 租用价格（用于费用折算 ￥/分钟） | 按实际采购/租用渠道确认；本文中的价格是估算 |
| 18 | Ovi 在 24GB 显存上的实际可用性 | 官方称 fp8+offload 可 24GB，但质量有损 |
| 19 | `WanVideoContextOptions` 与 `Wan22FunControlToVideo` 组合的实际接缝改善幅度 | **建议直接做第 5 节推荐路径的第 4 步实验**，用现有 `analyze_seams.py` 量化 |
| 20 | VACE / Fun-Control 对**几何保真度**的实际强度（`strength` 取值与贴合误差的关系） | 需要一次受控实验：固定控制视频，扫 `strength`，量化输出与控制的差异 |
| 21 | `WanVideoATI_comfy` 的 ATI 论文与实现细节 | 读 `ComfyUI-WanVideoWrapper` 源码与论文 |
| 22 | TouchDesigner / Resolume 之外的 auto-VJ 工具（`Musicvid` / `Spectrum` / BeatDrop / OBS 插件） | 未取得官方一手来源 |

> 说明：本文的 `object_info` 数据来自本项目 Comfy Cloud v0.272.0 的节点转储（3761 个节点类），而源码引用来自 `comfyanonymous/ComfyUI` 的 `master` 分支。**两者版本未必完全一致**：某个节点在源码里存在，不等于在当前云端实例里可用（反之亦然）。落地前请以实际环境的 `/api/object_info` 为准。

> 本文件只新增自身，未修改仓库中任何其他文件。
