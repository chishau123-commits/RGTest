# 谱面驱动的实时 BGA：非生成式 / 近实时方案调研

> 范围：不依赖视频扩散模型的前提下，**谱面数据 + 音频分析**能驱动出什么背景；以及 Unity 原生与 VJ 工业界为此提供的现成工具。
> 本仓库现状：Unity **2022.3.62f3c1**，**Built-in Render Pipeline**（`GraphicsSettings.asset` 中 `m_CustomRenderPipeline: {fileID: 0}`），已有一套 DSP 时钟对齐的预渲染 MP4 BGA 管线（见 `Docs/VideoBgaWorkflow.md`）。

**证据等级约定**：本文只采用一手来源（Unity 官方手册/脚本 API、官方 GitHub 仓库、包文档）。无法核实的一律标注 **未验证**，不做推测。

---

## 0. 结论速览（先看这张表）

| 问题 | 结论 | 关键证据 |
|---|---|---|
| Unity 有没有可用于节奏同步的音频分析？ | **基本没有**。`GetSpectrumData` / `GetOutputData` 被官方明确标注**不适合低延迟、按时间顺序的分析** | [GetSpectrumData 文档](https://docs.unity3d.com/ScriptReference/AudioSource.GetSpectrumData.html) |
| 那节奏同步靠什么？ | 靠 **BPM 网格 + `AudioSettings.dspTime`**。本仓库 `SongClock.cs` 已经是这个架构 | [AudioSettings.dspTime](https://docs.unity3d.com/ScriptReference/AudioSettings-dspTime.html) |
| 有没有样本精确的音频访问？ | 有，**只有 `OnAudioFilterRead`**（音频线程，~20ms 一块） | [OnAudioFilterRead 文档](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnAudioFilterRead.html) |
| Unity 有没有可配置窗/对数刻度的 FFT？ | **没有**。只有固定 6 个 `FFTWindow` 枚举，bin 线性均分，无对数刻度 | [FFTWindow](https://docs.unity3d.com/ScriptReference/FFTWindow.html) |
| VFX Graph 有音频采样吗？ | 有官方内置 **Audio Spectrum to AttributeMap** binder；但 **VFX Graph 必须跑在 SRP（URP/HDRP）上**，本仓库 BiRP 用不了 | [Property Binders](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/PropertyBinders.html)、[GettingStarted](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@14.0/manual/GettingStarted.html) |
| Shader Graph 能用吗？ | **不能**。Shader Graph 是 SRP 专属包 | [About Shader Graph](https://docs.unity3d.com/Packages/com.unity.shadergraph@14.0/manual/index.html) |
| 视频能不能锁歌曲时钟？ | 能：`timeUpdateMode = Audio DSP Time` + `skipOnDrop = ON`。代价是**靠丢帧追时钟** | [Clock management](https://docs.unity3d.com/2022.3/Documentation/Manual/video-clock.html)、[class-VideoPlayer](https://docs.unity3d.com/Manual/class-VideoPlayer.html) |
| 能不能逐音符 seek 视频？ | **不能。** 官方明说 seek "may be noticeably long"，且重复设 `time` 会**排队**串行执行 | [seekCompleted](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-seekCompleted.html)、[VideoPlayer.time](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-time.html) |
| Android 上视频编解码怎么办？ | H.264 是硬件加速最佳选择；软件解码路径只有 VP8+Vorbis；**没有硬解开关**，由平台决定 | [Video file compatibility](https://docs.unity3d.com/2022.3/Documentation/Manual/VideoSources-FileCompatibility.html) |
| 端上神经网络推理能用吗？ | **当前不能。** Sentis 2.5+ 要求 **Unity 6+**，本仓库是 2022.3 | [Install Sentis](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/install.html) |

---

## 1. Unity 音频分析 API 与其确切限制

### 1.1 API 对照表

| API | 运行线程 | 数据 | 硬性限制（官方原文依据） | 节奏同步可用性 |
|---|---|---|---|---|
| `AudioSource.GetSpectrumData(float[] samples, int channel, FFTWindow window)` | 主线程调用 | 频域幅度，线性 bin | ① 数组长度**必须是 2 的幂**，且 **≥64、≤8192**；② 使用 `AudioSettings.outputSampleRate`，**不是** AudioClip 自己的采样率；③ 频段在 **0 ~ 采样率一半之间均匀分布**（线性，非对数）；④ 缓冲区**首次调用时才分配**，早期返回空数据 | ❌ |
| `AudioSource.GetOutputData(float[] samples, int channel)` | 主线程调用 | 时域样本，[-1,1] | 长度须为 2 的幂；无 window 参数；同样的"短历史窗口"警告 | ❌ |
| `AudioListener.GetSpectrumData` / `GetOutputData` | 主线程调用 | 同上，取全局混音 | 同上 | ❌ |
| `MonoBehaviour.OnAudioFilterRead(float[] data, int channels)` | **音频线程**（非主线程） | 时域，多声道**交错**存放 | ① 每送一块音频调用一次，**约每 ~20ms**（随采样率与平台变化）；② **禁止调用多数 Unity API**（会运行时警告）；③ Web 平台不支持 | ✅ 唯一接近样本精确的路径 |
| `AudioSettings.dspTime` | 任意 | double 秒 | "基于音频系统实际处理的样本数"，**比 `Time.time` 精确得多** | ✅ 时钟基准 |
| `AudioSettings.outputSampleRate` | 任意 | int | 自 Unity 5.0 起**不能由脚本设置**，只能在 Project Settings 设；官方注明 Android/iOS 等平台**允许系统改变采样率** | ⚠️ 必须运行时读取 |
| `AudioSettings.GetDSPBufferSize(out int bufferLength, out int numBuffers)` | 任意 | 环形缓冲分块参数 | 见 1.3 | ⚠️ |
| `AudioSettings.Reset(AudioConfiguration)` | 任意 | 重配音频设备 | 官方警告：**在异步加载对象时调用会造成主线程卡顿** | ⚠️ |

来源：[GetSpectrumData](https://docs.unity3d.com/ScriptReference/AudioSource.GetSpectrumData.html)、[GetOutputData](https://docs.unity3d.com/ScriptReference/AudioSource.GetOutputData.html)、[OnAudioFilterRead](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnAudioFilterRead.html)、[dspTime](https://docs.unity3d.com/ScriptReference/AudioSettings-dspTime.html)、[outputSampleRate](https://docs.unity3d.com/ScriptReference/AudioSettings-outputSampleRate.html)、[AudioSettings](https://docs.unity3d.com/ScriptReference/AudioSettings.html)、[GetDSPBufferSize](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSettings.GetDSPBufferSize.html)

### 1.2 最重要的一段官方原文（决定了整个架构）

Unity 在 `GetSpectrumData` 和 `GetOutputData` 两页上都写了同一段免责声明：

> "GetSpectrumData provides access to audio data from a short history window (for example, the last few milliseconds) for analysis purposes. Unity doesn't automatically allocate the buffers required to store this history because doing so would be expensive and memory-intensive. Instead, Unity only allocates buffers and starts to record when you first call this function, on a per-object basis. As a result, the output data will initially be empty until the engine processes sufficient audio to populate the buffer. **Please note this function isn't suited for critical or chronological, real-time data analysis or processing, or scenarios where you require low latency.**"

**读法**：官方是让你拿它做"氛围类"可视化（频谱条、整体能量），**不是**拿它做判定/打点。用它做 beat 触发，你会拿到一个延迟不确定且与 `dspTime` 无固定相位关系的信号。

三条可直接落地的推论：

1. **不要用 FFT 找 beat**。谱面里已经有权威的 BPM 网格，`dspTime` 已经是样本精确时钟。用 FFT 反推只会引入一个无法标定的固定延迟。
2. **FFT 只用于"连续量"**：整体能量、低频/高频占比、频谱质心。这些允许 20–50ms 延迟，且天然平滑。
3. **`OnAudioFilterRead` 是唯一能拿到"当前正在播出的样本"的地方**，但它跑在音频线程，只能写无锁的共享变量（例如 `volatile` 结构或环形缓冲），主线程再读。

### 1.3 延迟与窗口的真实约束

| 约束 | 数值/说明 | 来源 |
|---|---|---|
| `OnAudioFilterRead` 回调周期 | ~20ms 一块（"every ~20ms depending on the sample rate and platform"） | [OnAudioFilterRead](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnAudioFilterRead.html) |
| DSP 环形缓冲 | `bufferLength` 个样本 × `numBuffers` 块 | [GetDSPBufferSize](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSettings.GetDSPBufferSize.html) |
| 参数平滑阈值 | 官方原文："**Anything above 20 ms will be noticeable** and sound parameter changes will be obvious instead of smooth." | 同上 |
| 改缓冲的建议 | 官方原文："**It is not recommended changing this value unless you really need to.** You may get worse performance than the default settings chosen by Unity." | 同上 |
| DSP Buffer Size 档位 | Default / Best Latency / Good Latency / Best Performance 四档；"Latency is a measure of how long it takes for the audio output to react to input / API commands" | [Audio project settings](https://docs.unity3d.com/Manual/class-AudioManager.html) |
| 系统采样率 | "System Sample Rate：设为 0 则 Unity 用系统采样率。**Note: This only serves as a reference only, since certain platforms allow you to change the sample rate, such as iOS or Android.**" | 同上 |
| FFT 窗 | 固定 6 种：Rectangular / Triangle / Hamming / Hanning / Blackman / BlackmanHarris。官方对取舍的说明："Stronger windows taper the signal more at the edges... typically lowers sidelobe leakage but **widens the effective main lobe and can reduce frequency resolution**。" | [FFTWindow](https://docs.unity3d.com/ScriptReference/FFTWindow.html) |

**结论：Unity 没有暴露"可配置窗 + 对数刻度"的 FFT。** 你只能从 6 个窗里挑一个，拿到线性 bin，然后自己在 C# 侧做 bin→频带 的重映射（对数分带、A 加权等）。想要自定义窗（如 Kaiser）或更好的频率分辨率，只能自己写 `OnAudioFilterRead` + 自己的 FFT（Burst/Jobs），或者走 §3 的 LASP。

### 1.4 Android 上的样本精确性

| 项目 | 结论 | 依据 |
|---|---|---|
| `OnAudioFilterRead` 是否支持 Android | 官方文档只声明**不支持 Web 平台**，未把 Android 列为不支持 → **推断支持，但未验证** | [OnAudioFilterRead](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnAudioFilterRead.html) |
| Android 采样率是否稳定 | **不稳定**。官方明说 Android/iOS 允许系统改变采样率，因此 `outputSampleRate` 是"仅供参考"，必须运行时读取 | [Audio project settings](https://docs.unity3d.com/Manual/class-AudioManager.html) |
| Android 上 "Best Latency" 是否安全 | **未验证**。Unity Issue Tracker 存在 "Best latency 导致播放变慢并爆音（OnePlus 7 Pro）" 的问题记录 | [Issue Tracker #316](https://issuetracker.unity.com/issues/316/when-dsp-buffer-size-is-set-to-best-latency-the-audio-plays-significantly-slower-and-is-crackling-on-oneplus-7-pro) |
| Android 低延迟音频的绝对值 | **未验证**（官方无公开承诺数值） | — |

**可落地的做法**：Android 上**不要**依赖"采样率恒定"。所有时间换算都写 `songTime = (dspTime - anchorDsp)`，用秒而不是样本数做权威单位；`outputSampleRate` 只在需要做 FFT bin→Hz 换算时读取，并且每帧现读。本仓库 `SongClock.cs` 正是这么做的（见 §8）。

### 1.5 Unity 6 的新东西（本仓库暂不可用，但值得记账）

Unity 6 引入了 **Scriptable Audio Pipeline**：用 Burst 兼容的 HPC# 在音频引擎的特定集成点上扩展音频系统（Generators / Root outputs）。官方注明 **Web 平台不支持**。
来源：[Scriptable audio pipeline](https://docs.unity3d.com/Manual/audio-scriptable-processors.html)

> 对本仓库的含义：2022.3 没有这个 API。若未来升到 Unity 6，它是"在音频线程上做自定义分析并喂给渲染"的官方正路。

---

## 2. Keijiro Takahashi 生态

> **重要前提：这一节里几个"常识"是错的。** 下面用一手核查（仓库页 404 检查、README、npm registry）更正了三条流传很广的说法。

### 2.1 整体分发方式：不是 `Klak` 单体，而是 npm 上的 `jp.keijiro.*`

| 事实 | 说明 | 来源 |
|---|---|---|
| `Klak` 单体仓库已是**旧形态** | 默认分支 `master`，README 仍写 "Download one of the unitypackage files from the Releases page"；**根目录没有 `package.json`**，因此**无法**从该仓库以 UPM git URL 安装 | [github.com/keijiro/Klak](https://github.com/keijiro/Klak) |
| 最后一次 Release | **v1.1.0 = 2018-11-08**（"Unity 2018.3 update"）；最后 push 2024-01-05 | Releases 页 / commits |
| 现状 | Klak 被**拆成多个独立模块仓库**，各自发布到 **npm**，包名前缀 `jp.keijiro.klak.*`（如 KlakMath、ProceduralMotion、KlakNDI、KlakSpout、KlakHap、KlakSyphon、KlakVJUI、KlakTimelineMidi、KlakTestTools 等） | npm registry |
| 安装方式 | 在 `manifest.json` 里加 **scoped registry** 指向 `https://registry.npmjs.com`，scope 为 `jp.keijiro`（该 scope 下约 **105** 个包） | npm registry |

> **对本项目的含义**：想用 Keijiro 的包，正确做法是配置 scoped registry，而不是抄老教程里的 `Klak` unitypackage。

### 2.2 "不存在"与"名不副实"的仓库（常见误传）

| 常被提到的名字 | 实际情况 |
|---|---|
| **`keijiro/KlakVJ`** | ❌ **不存在（HTTP 404）**。真正的 VJ UI 仓库是 **`KlakVJUI`**（`jp.keijiro.klak.vjui`，"Custom UI controls for VJing"）与 **`VJUITK`**（`jp.keijiro.vjuitk`，UI Toolkit 控件） |
| **`keijiro/AlembicUnity`** | ❌ **不存在（HTTP 404）** |
| **`keijiro/AlembicExporter`** | ❌ **不存在（HTTP 404）**。Alembic 现在是 **Unity 官方包** `com.unity.formats.alembic`（[repo](https://github.com/Unity-Technologies/com.unity.formats.alembic)），吸收了 Keijiro 的工作。⚠️ **仅 64 位桌面（Win/macOS/Linux），无 Android** |
| **`keijiro/KlakLasp`** | ✅ **存在**（分支 `master`），但**已死**：最后 push **2018-08-02**，Unity **2017.1+**，走 2017 年代的 `Klak.Wiring` 节点系统，**无 SRP 支持**，只发 unitypackage（需预装旧 Klak + LASP）。⚠️ 注意与 **`LaspVfx`**（`jp.keijiro.laspvfx`，现代 LASP→VFX Graph）区分 |
| **`keijiro/MidiJack`** | ⚠️ 被 **`Minis`** 取代的旧方案。Unity **5** 时代，**仅 Windows/OSX，无 Android**（README："No iOS support yet"） |
| **`keijiro/Klak`** 的"全家桶" | ⚠️ **名不副实**。见 §2.2b —— 它**不含** Klak.Audio / Klak.Vfx / Klak.Ndi |

#### 2.2b `Klak` 里到底有什么（已从 v1.1.0 文件树核实）

流传的"Klak 包含 Klak.Motion / Klak.Audio / Klak.Vfx / Klak.Ndi"**是过时的**。这个单体仓库在 `Assets/Klak/` 下**只有 4 个模块**：

| 模块 | 内容 | 管线 |
|---|---|---|
| `Klak.Extensions` | MaterialExtension、VectorMathExtension | 管线无关（纯 C#） |
| `Klak.Math` | BasicMath、Interpolator、Tween、Perlin、NoiseGenerator、XXHash | 管线无关（纯 C#） |
| `Klak.Motion` | BrownianMotion、ConstantMotion、SmoothFollow | 管线无关（纯 C#） |
| `Klak.Wiring` | 节点式连线（Input/Filter/Output/Basic nodes） | 管线无关（纯 C#），但整个仓库是 **Unity 2018.3 工程**，不是 package |

> **没有 Klak.Audio、没有 Klak.Vfx、没有 Klak.Ndi。** 音频在 `KlakLasp`；NDI/Spout/Hap/Syphon 各自独立仓库。现代拆分版以 npm 包发布：`jp.keijiro.klak.math` 2.1.1、`jp.keijiro.klak.motion` 1.1.1（仓库名却是 `ProceduralMotion`）、`jp.keijiro.klak.vjui` 1.0.3、`jp.keijiro.klak.timeline.midi` 等。
> **`Klak` 本身是冻结的 Unity 2018.3 工程，不是 UPM 包**（无根 `package.json`）→ 不要试图用 git URL 装它，请改用拆分后的 `jp.keijiro.klak.*` 包。License = **MIT**（不是 Unlicense）。

### 2.3 与 Android 直接相关的平台支持（本项目的关键筛选条件）

三个"像素传输"包的官方 System Requirements 原文：

| 包 | npm 包名 | 功能 | 平台（官方原文） | **Android？** | Unity 要求 |
|---|---|---|---|---|---|
| **KlakSpout** | `jp.keijiro.klak.spout` | 经 **Spout** 收发视频流 | "**Windows system with Direct3D 11/12 support**"；"KlakSpout currently supports **only Direct3D 11 and 12**; other graphics APIs such as OpenGL or **Vulkan aren't available**." | ❌ **仅 Windows** | **Unity 2022.3+** ✅ |
| **KlakNDI** | `jp.keijiro.klak.ndi` | 经 **NDI** 收发视频流 | 桌面：Windows x64 D3D11/12、macOS x64/arm64 Metal、Linux x64 Vulkan；移动：iOS arm64 Metal、**Android arm64, Vulkan/OpenGL ES 3.x** | ✅ **支持** | **Unity 2022.3+** ✅ |
| **KlakHap** | `jp.keijiro.klak.hap` | Hap 视频编解码播放 | **64 位桌面**（Windows / macOS / Linux） | ❌ **无 Android** | 未验证 |

来源：[KlakSpout README](https://cdn.jsdelivr.net/npm/jp.keijiro.klak.spout/README.md)、[KlakNDI README](https://cdn.jsdelivr.net/npm/jp.keijiro.klak.ndi/README.md)（npm 包自带 README，作者一手发布）

**KlakNDI 的硬限制（官方原文）**：

| 限制 | 原文 |
|---|---|
| 帧尺寸 | "Dimensions of frame images **should be multiples of 16x8**. This limitation causes **glitches on several mobile devices** when using the Game View capture method." |
| 音频 | "**KlakNDI doesn't support audio streaming.** There are several technical difficulties to implement without perceptible noise or delay, so there are **no plans to implement it**." |
| License | ⚠️ **不是普通开源协议**："The **NDI library files are provided under the terms of the NDI SDK license**. Please review it before using the package in your project." |
| Android 权限 | 需 `INTERNET`、`ACCESS_NETWORK_STATE`、`CHANGE_WIFI_MULTICAST_STATE` |

**两个包共同的一个 BiRP 陷阱（官方原文）**：

> "The **Camera capture method is available only on URP and HDRP—you can't use it on the built-in render pipeline**."

→ 在本项目的 Built-in RP 下，Spout/NDI 的 **Camera 捕获方式不可用**，只能用 **Game View** 或 **Texture** 捕获。
来源：[KlakSpout README](https://cdn.jsdelivr.net/npm/jp.keijiro.klak.spout/README.md)、[KlakNDI README](https://cdn.jsdelivr.net/npm/jp.keijiro.klak.ndi/README.md)

**Spout vs NDI —— 作者本人的官方对比**（KlakSpout README FAQ 原文）：

> - **NDI**: Video-over-IP codec/protocol
> - **Spout**: Interprocess GPU memory sharing on DirectX
>
> "**NDI consumes CPU, memory, and network bandwidth but is highly versatile. Spout adds virtually no CPU load, though its applications are more limited.** If you need to share video between applications on a **single Windows PC**, Spout is usually the better option."

| 维度 | Spout（`KlakSpout`） | NDI（`KlakNDI`） |
|---|---|---|
| 机制 | DirectX 进程间 GPU 内存共享 | IP 视频流编解码 |
| CPU 开销 | **几乎为零** | 有（编解码 + 网络） |
| 跨机 | ❌ 只能同机 | ✅ 局域网 |
| Android | ❌ | ✅ |
| 像素格式（接收） | R8G8B8A8 UNorm、B8G8R8A8 UNorm、R16G16B16A16 Half Float、R32G32B32A32 Float | 未验证 |
| 像素格式（发送） | **仅 R8G8B8A8 UNorm** | 未验证 |
| 与 TouchDesigner | 在 Spout Out TOP 里选对应像素格式 | ✅ |
| Unity + 捕获方式 | 2022.3+；BiRP 下不可用 Camera 方式 | 2022.3+；BiRP 下不可用 Camera 方式 |

> **这是第 2 节最重要的结论**：如果外部 VJ 进程（TouchDesigner / Python / StreamDiffusion）要把画面送进 Unity，**Windows 同机用 Spout（零 CPU 开销），需要跨机或上 Android 只能用 NDI**。
> NDI 在 Android 上的实际延迟与稳定性 **未验证**（README 只声明支持，未给数字）；且注意**帧尺寸必须是 16×8 的倍数**，否则移动端会出画面故障。

### 2.4 逐仓库对照表（License / 最后活动 / Unity 要求 / 管线 全部核实）

| 仓库 | npm 包名 | 功能 | License | 最后活动 | Unity 要求 | 管线 | 平台 / **Android** |
|---|---|---|---|---|---|---|---|
| **`Klak`** | ❌ 无 UPM | Klak.Extensions / Math / Motion / Wiring | **MIT** | commit **2018-11-08**（v1.1.0） | 2018.3 时代 | **管线无关**（纯 C#） | 全平台（纯 C#） |
| **`KlakSpout`** | `jp.keijiro.klak.spout` 2.0.6 | Spout 收发 | **Unlicense** | push **2025-11-16** | **2022.3+** | BiRP/URP/HDRP，但 **Camera 捕获仅 URP/HDRP** | ⚠️ **仅 Windows D3D11/12**；❌ 无 Android/macOS/Linux |
| **`KlakNDI`** | `jp.keijiro.klak.ndi` 2.1.6 | NDI 收发 | ⚠️ **NDI SDK license**（见下） | push **2025-12-07** | **2022.3+** | 同上 | ✅ **含 Android arm64（Vulkan/GLES 3.x）** |
| **`KlakHap`** | `jp.keijiro.klak.hap` 1.0.0 | HAP/HAP Alpha/HAP Q `.mov` 播放 | **MIT © 2019 Unity Technologies**（+ HAP codec FreeBSD / Snappy BSD-3 / MP4 demuxer CC0） | push **2026-02-05** | **2022.3+** | **管线无关**（输出纹理） | ⚠️ **仅 64 位桌面**；❌ **无 Android/iOS** |
| **`KlakSyphon`** | `jp.keijiro.klak.syphon` 1.0.4 | Syphon（macOS 版 Spout） | 未验证（NOASSERTION） | push **2025-11-16** | **2022.3+** | Camera 捕获仅 URP/HDRP | ⚠️ **仅 macOS + Metal** |
| **`KlakLasp`** | ❌ 无 UPM | LASP → Klak.Wiring 音频反应节点 | **MIT** | push **2018-08-02**（已死） | **2017.1+** | **仅 built-in/legacy**，无 SRP | Windows/macOS 64 位 |
| **`Lasp`** | `jp.keijiro.lasp` **2.1.8** | 低延迟音频**输入**分析 | **Unlicense**（≤2.1.2 是 MIT，2.1.3 起 Unlicense） | push **2025-10-10** | **2019.4+**；依赖 Burst 1.4.4 + `jp.keijiro.libsoundio` 1.0.6 | **管线无关** | ⚠️ **仅桌面**；❌ **无 Android**（原生 libsoundio） |
| **`LaspVfx`** | `jp.keijiro.laspvfx` 1.0.3 | LASP → VFX Graph 扩展 | **Unlicense** | 2025-03-31 | 未验证 | **需 VFX Graph → 需 SRP** | ❌ BiRP 不可用 |
| **`Smrvfx`** | `jp.keijiro.smrvfx` 1.1.6 | 蒙皮网格 → VFX Graph 粒子源 | **Unlicense** | push **2022-10-12**（npm 版 2020-12-08） | **2020.1**；依赖 VFX Graph 8.2.0 + Burst 1.3.4 | **需 VFX Graph → 需 SRP** | 未验证 |
| **`Minis`** | `jp.keijiro.minis` 1.3.2 | MIDI → Input System | **Unlicense** | push **2025-11-23** | **2022.3 LTS+** | **管线无关** | ✅ **含 Android arm64** |
| **`MidiJack`** | `jp.keijiro.midi-jack` 0.0.1 | 旧 MIDI 输入 | ⚠️ **无 LICENSE 文件**（README 内嵌 MIT） | push **2020-09-30** | **Unity 5** | 管线无关 | ⚠️ 仅 Windows/OSX；❌ **无 Android** |
| **`Reaktion`** | ❌ **npm 404** | 音频反应动画工具包 | ⚠️ **README 有完整 MIT 正文，但仓库无 LICENSE 文件**（API `license: null`） | commit **2015-07-07** | Unity 5 时代 | ⚠️ **仅 built-in/legacy；仓库无 URP 分支** | 桌面时代；Android 未验证 |
| **`TestCards`** | `jp.keijiro.testcards` 1.0.0 | 电视测试图卡生成器 | **Unlicense** | push **2020-05-18** | **2019.3** | ⚠️ **built-in 管线**（`TestOverlay` 是**相机 image effect**，不是 Renderer Feature） | 任意 |
| **`Alembic`（官方）** | `com.unity.formats.alembic` | Alembic 顶点缓存导入/导出 | Unity Companion License | Unity 维护中 | **2019.4+** | 管线无关（网格动画） | ⚠️ **仅 64 位桌面**；❌ **无 Android** |

**License 与分发的三个反常项**（容易踩坑）：

| 项 | 说明 |
|---|---|
| **`KlakNDI` 的 License 不是开源协议** | 仓库 LICENSE 只写 "Please review `libndi_licenses.txt`" → **由 NDI SDK license 管辖**；npm license 字段是 `SEE LICENSE IN LICENSE`。**商用前必须单独审阅** |
| **`KlakHap` 是 MIT © Unity Technologies** | 不是他惯用的 Unlicense —— 因为是在 Unity 合约期内写的。同时捆绑了 HAP codec（FreeBSD）、Snappy（BSD-3）、MP4 demuxer（CC0） |
| **`Reaktion` / `MidiJack` 没有 LICENSE 文件** | 两者的 MIT 文本只出现在 README 里，GitHub API 的 `license` 字段为 `null`。**法律上比有 LICENSE 文件弱** |

> **安装方式（作者本人的唯一文档化机制）**：所有现代包都用 **scoped registry**，**不是** git URL。他的 README 里**从未出现** `https://github.com/keijiro/X.git?path=...` 这种写法，且这些仓库多数**不是 package 根目录**（`KlakSpout/main/package.json` → 404）。因此**不要自行拼造 git URL**。
> ```json
> { "scopedRegistries": [ { "name": "Keijiro", "url": "https://registry.npmjs.com", "scopes": [ "jp.keijiro" ] } ],
>   "dependencies": { "jp.keijiro.klak.ndi": "2.1.6" } }
> ```
> （Unity 2021.1 及更早还需额外加 "Unity NuGet" registry 以解析部分依赖。）

### 2.5 与本项目相关的四条结论

| # | 结论 | 依据 |
|---|---|---|
| 1 | **能上 Android 的只有两个**：`KlakNDI`（Android arm64）与 `Minis`（Android arm64，必须用 "Activity" 入口）。**Spout / Syphon / Hap / LASP / MidiJack / Alembic / Reaktion 都到不了 Android。** | §2.4 平台列 |
| 2 | **Android 上完全用不了 Keijiro 的音频栈** —— LASP 是原生 libsoundio 桌面插件。Android 要做音频反应，只能回到 Unity 自带的 `Microphone` / `GetSpectrumData`（并接受 §1.2 的限制） | [Lasp README](https://cdn.jsdelivr.net/npm/jp.keijiro.lasp@2.1.8/README.md) |
| 3 | **`Reaktion` 已实质废弃且没有 URP 路径**：最后 commit **2015-07-07**，无 LICENSE 文件，分支里没有 `main`/URP（分支为 master、unity5、dev-v2、dev-lasp、dev-midi-ext、documentation、generic-input-improvement、gh-pages）。**未 GitHub-archived（`archived: false`），但已死。** | §2.4 |
| 4 | **Keijiro 生态大量包要求 Unity 2022.3+**（KlakSpout / KlakNDI / KlakHap / KlakSyphon / Minis 均已确认）→ **本仓库的 2022.3.62f3c1 正好达标**，这一点上不需要升级 | §2.4 |

**一个对本项目特别有用的发现**：`Lasp` 有一个 **`loopback` 分支**，用来分析 **Unity 自己的音频输出**（而不是外部麦克风输入）。桌面端若想对"正在播放的歌曲"做音频反应，这是比 `GetSpectrumData` 更可控的路径。**该分支的具体能力与维护状态 = 未验证。**

**关于 `TestCards`**：它是 **built-in 管线**的相机 image effect（`Runtime/TestOverlay.cs` + `Resources/TestOverlay.shader`），这与本项目的 BiRP 恰好相容 —— 如果需要一个"画面校准/测试图卡"工具，它可用。**但它与实时 BGA 无关，只是一个测试工具。**

---

## 3. LASP / 音频输入与 MIDI / OSC

### 3.1 一个必须先说的坏消息：LASP 不支持 Android

LASP 是关于音频反应视觉的事实标准库，但官方 README 明确写着：

> "**LASP** is a Unity plugin providing **low-latency audio input** features that are useful to create audio-reactive visuals."
> System Requirements: "Unity **2019.4** or later"
> "**At the moment, LASP only supports desktop platforms (Windows, macOS, and Linux).**"
> — [jp.keijiro.lasp README](https://cdn.jsdelivr.net/npm/jp.keijiro.lasp@2.1.8/README.md)（npm 包自带 README，作者一手发布）

| 项 | 值 |
|---|---|
| 包名 / 安装 | `jp.keijiro.lasp`，经 **scoped registry**（`https://registry.npmjs.com`，scope `jp.keijiro`） |
| Unity 要求 | **2019.4+** → 本仓库 2022.3 ✅ 满足 |
| 平台 | **仅桌面（Windows / macOS / Linux）** → **Android ❌** |
| License | **未验证**（README 未声明；npm/jsd 未取到 LICENSE） |

> **对本项目的含义**：**不能给 Android 用 LASP。** 若 Android 需要音频分析，只有两条路：自己写 `OnAudioFilterRead` + FFT，或把分析放到外部进程/服务。这是第 3 题结论里最硬的一条。

### 3.2 LASP 提供了什么（桌面端）

LASP 关注的不是"分析正在播放的 AudioSource"，而是**音频输入流**（麦克风/线入）。它用 `AudioSystem.InputDevices` 枚举设备。这一点常被误解。

| 组件 / 类 | 作用 | 关键细节 |
|---|---|---|
| **Audio Level Tracker** | 计算当前音量电平，支持 **Property Binders** 驱动外部对象属性 | 基于"当前电平与峰值电平之差"做归一化 |
| **Spectrum Analyzer** | 对输入流做 **FFT** 取频谱 | 用与 Audio Level Tracker 相同的算法归一化 |
| **Spectrum To Texture** | **把频谱数据烘成纹理**，便于用 shader 做视觉 | 见下方要求 |
| `InputStream` | 原始波形访问（交错存放） | 用于 Lissajous 这类逐样本绘制 |

`Spectrum To Texture` 的 RenderTexture 要求（官方原文）：

> - Width: Must be the same as the **spectrum resolution**.
> - Height: Must be **1**.
> - Format: **R32_SFloat**

也可以走 **Material Override**，直接覆盖某个材质的纹理属性（配合 MeshRenderer 很方便）。
来源：[jp.keijiro.lasp README](https://cdn.jsdelivr.net/npm/jp.keijiro.lasp@2.1.8/README.md)

**这正好印证 §4.3 的"频谱纹理"模式**：一张 宽=N、高=1、R32_SFloat 的纹理，shader 里按 UV.x 取频带幅度。这是 Unity 生态里事实上的标准做法，LASP 把它做成了官方组件。

**与"节奏点缀"直接相关的三个参数**（官方描述）：

| 参数 | 作用 | 官方说明 |
|---|---|---|
| **Filter Type** | Bypass / **Low Pass** / **Band Pass** / **High Pass** | "These filters are useful to **detect a specific type of rhythmic accents**. For instance, you can use the low pass filter to create a behavior **reacting to kick drums or basslines**." |
| **Dynamic Range (dB)** | 归一化区间 | 输入电平 ≤ (Peak − Dynamic Range) 时输出为 0 |
| **Smooth Fall** | 输出值缓降 | "It's useful to make **choppy animation smoother**." |

> 注意：**这些都不是"节拍检测"**。它们是分带能量 + 包络平滑。LASP 没有任何 onset/beat 检测 API —— 它把"什么算一个重音"交给作者用滤波器近似。这与 §1.2 的结论一致：**Unity 生态里没有权威的 beat 检测，谱面才是权威。**

### 3.3 MIDI：Minis 是当前正解，且**支持 Android**

`MidiJack` 是旧方案，现已被 **`Minis`** 取代（Minis 基于 `jp.keijiro.rtmidi`，是 Unity **Input System** 的 MIDI 扩展）。

官方 README（[jp.keijiro.minis](https://cdn.jsdelivr.net/npm/jp.keijiro.minis@1.3.2/README.md)）：

| 项 | 值 |
|---|---|
| 全名 | "**Minis**: MIDI Input Extension for Unity **Input System**" |
| Unity 要求 | "**Unity 2022.3 LTS or later**" → 本仓库 2022.3 ✅ **正好满足** |
| 平台矩阵 | Windows x86_64 / macOS (Intel + Apple Silicon) / iOS arm64 / Linux x86_64 / **Android arm64** / Web (需 Web MIDI) |
| **Android 注意事项（重要）** | "Minis currently **does not support the GameActivity entry point**. You must select **'Activity'** as the Application Entry Point in the Player Settings." |
| Android 已知问题 | 多端口 MIDI 设备有问题（`keijiro/jp.keijiro.rtmidi#16`） |
| MIDI Out | **不支持**（但底层 RtMidi 支持，可直接调用） |
| 映射 | MIDI 音符 → button 控件（velocity 与 poly aftertouch 归一化到 0–1）；CC → axis；Pitch Bend / Channel Pressure → axis |
| 事件 | `onWillNoteOn` / `onWillNoteOff` / `onWillAftertouch` / `onWillControlChange` / `onWillChannelPressure` / `onWillPitchBend`（在控件状态更新**之前**触发） |
| 通道语义 | 每个 MIDI 通道当作独立设备；**通道号从 0 开始编号**（与 MIDI 规范从 1 开始不同） |

> **Android 落地要点**：如果要做 MIDI 控制器支持，**必须在 Player Settings 里把 Application Entry Point 设为 "Activity"**，否则 Android 上不工作。

### 3.4 OSC

| 方案 | 说明 | 包名 / 版本 | License | 状态 |
|---|---|---|---|---|
| **`osc-jack`（Keijiro）** | "**OSC server/client (control surface)**" —— Keijiro 自己的 OSC 方案，最贴合"外部 VJ 进程 → Unity"的用法 | npm **`jp.keijiro.osc-jack` 2.0.0** | **Unlicense** | 发布 **2022-04-25**；可经 `jp.keijiro` scoped registry 安装 |
| **`Unity-Technologies/UnityOSCProtocolSupport`** | **Unity 官方**仓库（OSC 协议支持） | — | 未验证 | 仓库存在已确认；**内容/平台/许可 = 未验证** |
| **`Iam1337/extOSC`** | "extOSC is a tool dedicated to simplify creation of applications in Unity with **OSC** protocol usage." Unity Asset Store 亦有条目 | ❌ 不在 npm | 未验证 | 仓库存在已确认 |
| **`thomasfredericks/UnityOSC`** | "Open Sound Control (OSC) C# classes interface for the Unity3d game engine" | ❌ 不在 npm | 未验证 | 仓库存在已确认 |

> **推荐**：优先考虑 **`jp.keijiro.osc-jack`** —— 它是唯一一个**许可明确（Unlicense）、版本明确（2.0.0）、安装方式明确（scoped registry）**的选项，且与 §2 的整个生态同一套安装机制。
> OSC 本身是**基于 UDP 的轻量消息协议**，因此凡是 .NET Socket 可用的平台都能跑（含 Android）。**但"能跑"不等于"延迟可接受" —— 没有任何官方延迟数字 → 未验证。**

### 3.5 从外部 VJ 进程驱动 Unity 的标准做法

| 通道 | 传什么 | 带宽 | 平台 | 适用 |
|---|---|---|---|---|
| **OSC**（UDP） | **控制参数**：能量值、频带、BPM、段落号、触发事件 | 极低（几十字节/消息） | 全平台（含 Android） | **推荐作为主控通道** |
| **Spout** | **像素**（GPU 纹理共享，同机跨进程） | 极高（零拷贝，同 GPU） | ❌ **仅 Windows + D3D11/12**（`KlakSpout`） | 桌面端把 TouchDesigner / StreamDiffusion 画面送进 Unity |
| **NDI** | **像素**（网络视频流，跨机） | 高（走网络，有压缩与延迟） | ✅ **含 Android**（`KlakNDI`，arm64 / Vulkan / GLES 3.x） | 需要跨机、或需要上 Android 时的像素通道 |

**标准架构（业界常见形态）**：

```
外部分析/VJ 进程（TouchDesigner / Python+librosa / StreamDiffusion / 专用音频机）
   │
   ├── OSC ──→ Unity：每帧几个 float（能量、低频、频带、BPM、段落、触发）  ← 主控，低带宽
   │
   └── Spout(Windows) / NDI(跨平台) ──→ Unity：整幅画面纹理              ← 仅在需要"外部生成画面"时
```

**推荐的职责划分**：
- **能用参数表达的，一律走 OSC**，不要传像素。频谱/能量/段落号都是几个 float，OSC 足够且延迟最低。
- **只有当画面本身必须由外部生成**（例如 StreamDiffusion 的实时 img2img）时，才引入 Spout/NDI 的像素通道。此时延迟由"外部生成耗时 + 传输 + Unity 上屏"三段叠加，**且必然 ≥ 外部生成一帧的耗时**。

**延迟数字：全部 未验证。** 本环境没有取到任何一手延迟测量（OSC 端到端、Spout、NDI 都没有官方数字）。任何"OSC 大约 X ms"的说法都不要当事实用。

---

## 4. Shader / VFX 路径

### 4.1 决定性的平台约束

Unity 官方原文：

> "Visual Effect Graph is a Unity package that **uses a Scriptable Render Pipeline** to render visual effects. Visual Effect graph uses on **compute Shaders** to simulate effects."
> — [Getting started with Visual Effect Graph](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@14.0/manual/GettingStarted.html)

> "If you install a **Scriptable Render Pipeline (SRP)** such as the Universal Render Pipeline (URP) or the High Definition Render Pipeline (HDRP), Unity **automatically installs Shader Graph** in your project."
> — [About Shader Graph](https://docs.unity3d.com/Packages/com.unity.shadergraph@14.0/manual/index.html)

| 技术 | BiRP（本仓库当前） | URP | HDRP |
|---|---|---|---|
| Shader Graph | ❌ 不可用（SRP 专属） | ✅ | ✅ |
| VFX Graph | ❌ 不可用（需 SRP + compute shader） | ✅ | ✅ |
| 手写 CG/HLSL + `Material.SetFloat/SetTexture` | ✅ **唯一可用路径** | ✅ | ✅ |

**这是本报告最重要的一条硬约束**：在当前 Built-in RP 下，第 4 题里的 VFX Graph / Shader Graph 方案**全部不可用**，除非迁移到 URP。迁移成本需单独评估（材质、Shader、后处理、光照全要重做）。

### 4.2 VFX Graph 的音频采样（给未来 URP 迁移后的参考）

VFX Graph **有**音频相关的官方内置能力，形式是 **Property Binder** 而不是采样节点：

> Built-in Property Binders → **Audio**
> **Audio Spectrum to AttributeMap**: Bakes the Audio Spectrum to an Attribute map and binds it to a Texture2D and uint Count properties.
> — [Property Binders](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/PropertyBinders.html)

| 问题 | 答案 |
|---|---|
| VFX Graph 有 first-class 音频采样吗？ | 不是"Sample Audio 节点"，但**有官方内置 binder** 把频谱烘焙成 AttributeMap（Texture2D + uint Count）。属于官方一等支持 |
| 还是必须手动绑 `GraphicsBuffer`/`Texture2D`？ | 手动路径完全可行且常用：`VisualEffect.SetTexture` / `SetGraphicsBuffer` / `SetFloat` / `SetUInt` |
| 自定义 binder 怎么写 | 继承 `UnityEngine.VFX.Utility.VFXBinderBase`，实现 `IsValid(VisualEffect)` 与 `UpdateBinding(VisualEffect)`；用 `[VFXBinder("...")]` 注册到 Add 菜单 |

来源：[Property Binders](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/PropertyBinders.html)

`VisualEffect` 脚本 API 中与"逐音符"和"确定性回放"直接相关的方法（[VFX.VisualEffect](https://docs.unity3d.com/ScriptReference/VFX.VisualEffect.html)）：

| API | 用途 |
|---|---|
| `SendEvent(string/ExposedProperty)` | **发自定义命名事件** → Spawn context。**这就是"每个音符一次爆发"的标准做法** |
| `SetFloat` / `SetVector4` / `SetTexture` / `SetGraphicsBuffer` / `SetUInt` | 逐帧连续量（频谱纹理、能量、颜色） |
| `pause` + `AdvanceOneFrame()` | 暂停下推进**恰好一帧**，用当前 delta time |
| `Simulate(stepCount, deltaTime)` | 快进模拟若干步 → **可用于把 VFX 对齐到歌曲时间（比如 seek 后重算状态）** |
| `playRate` | 施加到 delta time 的倍率 |
| `Reinit()` / `StartSeed` / `resetSeedOnPlay` | 重置与可复现随机 |

### 4.3 Shader Graph 的 Custom Function 节点

| 项 | 说明 | 来源 |
|---|---|---|
| 两种模式 | **String**（直接写 HLSL 片段，Unity 自动补参数/花括号）或 **File**（引用 `.hlsl` 文件，注入 `#include`） | [Custom Function Node](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Custom-Function-Node.html) |
| 精度 | `Name` 字段**不带** `_float`/`_half` 后缀；String 模式可用 `$precision` token | 同上 |
| 文件要求 | 必须有 `#ifndef` / `#define` 防重包含；每个函数名必须带 `_float` 或 `_half` 精度后缀；多个文件需用不同 id 字符串 | 同上 |
| 纹理类型 | 自 10.3 起提供 `UnityTexture2D` / `UnityTexture2DArray` / `UnityTexture3D` / `UnityTextureCube` / `UnitySamplerState`；可访问 `myInputTex.samplerstate` 与 `myInputTex.texelSize` | 同上 |
| 全局 uniform | 函数外部声明的 uniform 变量是**全局**的，只能用 `Shader.SetGlobalX()` 设置，**不能 per-material**（不在 `UnityPerMaterial` cbuffer 里） | 同上 |
| 预览保护 | 节点/主预览访问不到渲染管线库，必须用 `#ifdef SHADERGRAPH_PREVIEW` 隔离并给默认值，否则编辑器报编译错误 | 同上 |
| 管线宏 | BiRP `BUILTIN_PIPELINE_CORE_INCLUDED` / URP `UNIVERSAL_PIPELINE_CORE_INCLUDED` / HDRP `UNITY_HEADER_HD_INCLUDED` | 同上 |

**用频谱纹理驱动 Shader 的标准套路**（与管线无关，BiRP 也适用）：

1. 每帧把 `GetSpectrumData` 结果（或 LASP 的频谱）写进一张小纹理，例如 `Texture2D` 128×1、`TextureFormat.RFloat`，用 `SetPixelData`/`SetPixels` + `Apply(false)`，或更好用 `ComputeBuffer`→`GraphicsBuffer`→`Shader.SetGlobalBuffer`。
2. Shader 里按 UV.x 采样该纹理拿到对应频带幅度。
3. **逐音符爆发** vs **逐帧连续**的分工：

| 需求 | 数据通道 | 时机 |
|---|---|---|
| 逐音符爆发（每次命中一发） | **不要用频谱**。用谱面 note 事件 → 结构化的 `_BurstTime` / `_BurstPosition` 数组（`GraphicsBuffer`），或直接对象池化粒子上播放一次 | 由 `songTime` 与 note 时间戳比对触发 |
| 逐帧连续（频谱呼吸、整体能量） | 频谱纹理 / 能量 float | `Update()` 每帧 |
| 段落切换（副歌变色调） | 谱面 `sections[].startBeat` → 直接给材质/后处理参数 | 段落边界，样本精确 |

### 4.4 若坚持留在 BiRP：实际可行的手写 Shader 路径

| 层 | 做法 |
|---|---|
| 频谱数据 | `OnAudioFilterRead`（音频线程）→ 无锁环形缓冲 → 主线程每帧取最新块做 FFT/分带 |
| 传参 | `Material.SetFloat` / `SetVector` / `SetTexture` / `Shader.SetGlobalTexture` |
| 背景着色器 | 手写 CG/HLSL 的 Unlit/Unlit-transparent shader，在 `frag` 里采频谱纹理做位移/发光/扭曲 |
| 逐音符 | C# 侧 `songTime` 比对 note 时间戳 → 触发对象池粒子 / 改材质参数 / 发事件 |
| 后处理 | BiRP 需要自己写 `OnRenderImage` 或自己搭后处理栈（官方 Post Processing Stack v2 亦为 BiRP 兼容） |

---

## 5. 端上神经网络推理

### 5.1 Unity Sentis —— 先纠正一个常见误解

**包名与显示名不是一回事，而且改过两次。** Unity 官方原文：

> "In Sentis `2.4`, the package **display name changed from Inference Engine to Sentis**. You can find the Sentis package by searching `Sentis` or `Inference Engine` in the Package Manager. You don't need to change any code in your project as this is a change to the package display name only."
> — [Sentis overview](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/index.html)

| 项 | 现状 | 来源 |
|---|---|---|
| 包 ID（manifest 里写的） | **`com.unity.ai.inference`** | 上述文档页元数据 `"name": "com.unity.ai.inference"` |
| 显示名 | **Sentis**（2.4 起从 "Inference Engine" 改回 Sentis） | 同上 |
| 当前版本 | **2.6.1**（文档站） | 同上 |
| 发布状态 | "The package is **officially released** and available to all Unity users through the **Package Manager**." | 同上 |
| 是否还是 Barracuda | Barracuda 是前身，已被 Sentis 取代 | 见 §5.3 |

> **纠正**：调研问题里把 Sentis 叫 "formerly Barracuda" 是对的，但**当前它既不叫 Barracuda、包名也不叫 `com.unity.sentis`**。旧的 `com.unity.sentis` 文档仍然在线（例如 [Sentis 2.1](https://docs.unity3d.com/Packages/com.unity.sentis@2.1/manual/whats-new.html)），容易误导。

### 5.2 支持的模型格式与算子

| 项 | 值 | 来源 |
|---|---|---|
| 平台 | "Sentis supports **all Unity runtime platforms**." | [Sentis overview](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/index.html) |
| ONNX opset 范围 | **7 到 25** | 同上 |
| 其他格式 | 同时支持 **LiteRT（原 TensorFlow Lite）** 与 **PyTorch**（需 decompose 到 **Core ATen IR** 算子） | 同上 |
| 逐算子支持表 | 官方分三份：`supported-operators.html`（ONNX）、`supported-litert-operators.html`、`supported-torch-export-operators.html` | 同上 |

> **重要**：问题假设"只有 ONNX"，实际 Sentis 现在有**三条导入路径**（ONNX / LiteRT / PyTorch），选型空间比预期大。

### 5.3 致命约束：Sentis 2.5+ 需要 Unity 6

> "**Sentis 2.5 is compatible with Unity 6 (or later).**"
> — [Install Sentis](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/install.html)
>
> "Package version **2.6.1 is released for Unity Editor version 6000.0**." — [Unity 6.0 Manual — Sentis](https://docs.unity3d.com/6/Documentation/Manual/com.unity.ai.inference.html)

| 项目 | 本仓库 | 当前 Sentis 要求 | 结论 |
|---|---|---|---|
| Unity 版本 | **2022.3.62f3c1** | **6000.0（Unity 6）+** | ❌ **装不上** |

**这是第 5 题对本项目的决定性结论：端上神经网络推理在当前 Unity 版本下不可用。** 想用 Sentis，必须先升到 Unity 6。旧版 `com.unity.sentis` 1.x 曾支持更早的 Unity，但那是已被弃用的旧包（见 §5.1），且其算子/后端能力弱于 2.6。

### 5.4 后端与移动端现实预算（供升级 Unity 6 后参考）

官方后端**恰好三个**（[Create an engine](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/create-an-engine.html)、[How Sentis runs a model](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/how-sentis-runs-a-model.html)）：

| `BackendType` | 实现 | 官方描述 |
|---|---|---|
| `CPU` | CPU + **Burst** | "Faster than GPU for small models or when inputs/outputs are on the CPU." |
| `GPUCompute` | **compute shader，经 `CommandBuffer`** | "Generally the fastest backend for most models."；**"Uses DirectML for inference acceleration when running on DirectX12-supported platforms."** |
| `GPUPixel` | **pixel shader（blit）** | "Use only on platforms that lack compute shader support." 需 `SystemInfo.supportsComputeShaders` 判断 |

> **纠正**：没有名为 `GPUCommandBuffer` 或 `Metal`/`Vulkan` 的独立后端。GPU 路径统一走 Unity 图形抽象，底层 API 是 Unity 当前运行的 API（Windows 上 DirectX 12 + DirectML，Android 上 Vulkan）。

| 平台 | CPU | GPUCompute | GPUPixel |
|---|---|---|---|
| Windows | ✅ | ✅ DirectX12 下 DirectML 加速 | ✅ |
| Android | ✅ | ⚠️ **需 Vulkan**（OpenGL ES 不可用） | ✅ |
| 所有 Unity 运行平台 | ✅ | ✅ | ✅ |

Android 的 OpenGL ES 限制出自前身 Barracuda 的官方平台文档（当前 Sentis 文档未复述）：

> "GPU inference: all Unity platforms are supported except: `OpenGL ES` on `Android/iOS`: **use Vulkan/Metal**."
> — [Barracuda 3.0.0 SupportedPlatforms.md](https://raw.githubusercontent.com/Unity-Technologies/barracuda-release/release/3.0.0/Documentation~/SupportedPlatforms.md)

**该限制在 Sentis 2.6 是否仍然成立 = 未验证**（2.6 文档对此沉默）。

CPU 回退的代价（官方原文）：

> "If Sentis supports an operator on the CPU but not the GPU, Sentis might automatically fall back to running on the CPU. This requires Sentis to **sync with the GPU and read back the input tensors to the CPU**. … If a model has many layers that use CPU fallback, Sentis might spend significant time to upload and read back from the CPU. This can impact the performance of your model."

**没有任何官方 benchmark 数字（无 FPS、无 ms、无模型体积表）→ 具体数值一律 未验证。** 官方只给了一个算例和一条移动端策略：

| 官方指引 | 原文/数值 | 来源 |
|---|---|---|
| 帧切片算例 | "**if a model takes 50 milliseconds to run** … spread the run over **10 frames to allocate 5 milliseconds per frame**" | [Split inference over frames](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/split-inference-over-multiple-frames.html) |
| 机制 | `Worker.ScheduleIterable(...)` 返回 `IEnumerator`，可每帧跑 N 层；官方样例硬编码 `const int k_LayersPerFrame = 20;` | 同上 |
| **官方移动端策略** | `DepthEstimationSample`："we **split the model inference over 2 frame[s]**. By running a **lower frequency** as the refresh rate, we **avoid the phone to overheat**." | [DepthEstimationSample README](https://raw.githubusercontent.com/Unity-Technologies/sentis-samples/main/DepthEstimationSample/README.md) |
| 量化 | Float16 / Uint8 可选，但官方明确："A lower bit count per value **decreases your model's disk and memory usage without significantly affecting inference speed**." → **量化省内存，不省时间** | [Quantize a model](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/quantize-a-model.html) |
| 主要性能陷阱 | `ReadbackAndClone()` "is a **blocking** call … can be slow, especially when reading back from the GPU." 应改用 `ReadbackRequest` / `ReadbackAndCloneAsync` | [How Sentis runs a model](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/how-sentis-runs-a-model.html) |

**读法**：Unity 自己的移动端逐帧图像模型**都跑不满帧率**——拆到 2 帧、并且低于刷新率运行，理由是**过热**而不只是帧时间。这直接回答了"手机上每帧能跑什么"：**不能指望全帧率的图像到图像模型**。

### 5.5 已发布的逐帧推理样例

任务描述里的样例清单**已经过时**。`Unity-Technologies/sentis-samples` 当前顶层样例**恰好是这 8 个**：

| 目录 | 作用 | 是否产出完整图像 |
|---|---|---|
| `BlazeDetectionSample` | BlazeFace/Hand/Pose 检测（用 `NonMaxSuppression`） | ❌（框 + 关键点） |
| `BoardGameAISample` | 棋类 AI | ❌ |
| `ChatSample` | 多模态 LLM（LLaVA-OneVision 0.5B） | ❌（文本） |
| **`DepthEstimationSample`** | **实时逐像素 相机→深度** | ✅ **唯一一个**（深度图，非 RGB） |
| `DigitRecognitionSample` | 数字识别（MNIST 的后继） | ❌ |
| `ProteinFoldingSample` | 蛋白质折叠，**仅兼容 Sentis 1.X** | ❌ |
| `StarSimulationSample` | N 体物理，GPU 常驻，钉到 vertex shader | ❌ |
| `TextToSpeechSample` | Kokoro-82M TTS | ❌（音频） |

来源：[sentis-samples](https://github.com/Unity-Technologies/sentis-samples)、[package samples 文档](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/package-samples.html)

> **关键结论：不存在官方的 style-transfer / img2img / 超分辨率逐帧样例。** 唯一产出整幅图像的样例是深度估计，它生成的是深度图。
> StyleTransfer / MNIST / BlendShape / SemanticSegmentation / YOLO / Whisper / BLIP **现在都不在该仓库中**。
> "曾经存在过 StyleTransfer 样例" 这一点**无法从一手来源证实 = 未验证**（archive.org 与 api.github.com 在本环境不可达）。

唯一值得抄的模式是 `StarSimulationSample` 的 **GPU 常驻**：结果张量直接钉进 vertex shader buffer，`ComputeTensorData.Pin(x, clearOnInit: false).buffer`，**完全不回读 CPU**。这才是逐帧图像模型该有的形态。

### 5.6 StreamDiffusion

**主仓库没有改名，但已经停更。**

| 项 | 值 |
|---|---|
| 规范仓库 | **[`cumulo-autumn/StreamDiffusion`](https://github.com/cumulo-autumn/StreamDiffusion)**（HTTP 200，仍是正主） |
| ⚠️ 不是改名的 | `daydreamlive/StreamDiffusion` 是**另一个 fork**（create 2025-10-23），README 自述 "This repository **builds on** the original StreamDiffusion project." **不是**新官方仓库 |
| Stars / forks | **10,816** / 834 |
| 创建 | 2023-11-28 |
| **最后 push** | **2024-12-04 → 已休眠约一年** |
| 最新 release | **v0.1.1**（2023-12-31） |
| License | **Apache-2.0** |
| 运行环境 | Windows ✅ / Linux ✅；**NVIDIA CUDA 专用**（CUDA 11.8 或 12.1；`torch.device("cuda")` 硬编码；**AMD/ROCm 未验证**） |
| TensorRT | **非必需**。默认用 `xformers`；官方："It requires TensorRT extension and time to build the engine, **but it will be faster**" |
| Python | **3.10**；`torch==2.1.0`、diffusers 锁 `==0.24.0` |

#### 官方 benchmark 表（README 里唯一一张表，原样转录）

> 原文前提："When images are produced using our proposed StreamDiffusion pipeline in an environment with **GPU: RTX 4090**, **CPU: Core i9-13900K**, and **OS: Ubuntu 22.04.3 LTS**."

| model | Denoising Step | fps on Txt2Img | fps on Img2Img |
|:---:|:---:|:---:|:---:|
| SD-turbo | 1 | **106.16** | **93.897** |
| LCM-LoRA + KohakuV2 | 4 | **38.023** | **37.133** |

**❗ 只有四列。没有 batch size、没有分辨率、没有精度、没有多卡列，也没有第二张表 —— 这些一律 未验证。** README 的代码样例用 `torch.float16` / `resize((512,512))` / `max_batch_size=2`，但**从未与上面的 fps 数字关联**，不要自行拼装。

#### 延迟数字来自论文，不是 README

**README 里没有任何毫秒数字（只有 fps）。** 毫秒数据出自论文 **arXiv:2312.12491**（ICCV 2025）：

| Step | StreamDiffusion（ms，含 TensorRT） | w/o TensorRT（ms） | AutoPipeline Img2Img（ms） |
|:---:|---:|---:|---:|
| 1 | **10.65**（59.6×） | 21.34（29.7×） | 634.40（1×） |
| 2 | **16.74**（39.3×） | 30.61（21.3×） | 652.66（1×） |
| 4 | **26.93**（25.8×） | 48.15（14.4×） | 695.20（1×） |
| 10 | **62.00**（13.0×） | 96.94（8.3×） | 803.23（1×） |

**读法（对节奏游戏至关重要）**：
- 1 步 img2img 在 **RTX 4090** 上约 **10.65 ms**（≈94 fps）—— 纸面上够 60fps。
- 但 4 步就要 **26.93 ms**（≈37 fps），**已经跟不上 60fps**。
- **最低显卡门槛官方未给 → 未验证。** README 只列了一张 4090。论文提到测过 **RTX 3060**，但那是**能耗**结果（2.39×），不是吞吐；**笔记本 3060 可行性 = 未验证**，且 6 GB 显存有实际风险（论文称 Stream Batch 的权衡是"balancing **VRAM capacity** and generation quality"，而 README 未给任何显存要求）。
- 两处论文自相矛盾已记录：能耗那段 §1 写 RTX **3090**，摘要/§4/Fig.6 写 RTX **3060**；fps 论文写 **91.07**，README 表写 **93.897**。

#### 传输：README 完全没提 Spout / NDI

> **Spout、NDI、Syphon、OBS、TouchDesigner、WebSocket 在完整 README 里出现次数为 0。** README 唯一关于输出的表述是："There is a real time img2img demo with a live webcam feed or screen capture **on a web browser**."

| 项 | 事实 |
|---|---|
| `demo/realtime-img2img` 的技术栈 | **FastAPI + uvicorn + 编译好的 JS 前端**，**不是** Streamlit 也**不是** Gradio |
| 传输协议 | `/api/ws/{user_id}` → **WebSocket**；`/api/stream/{user_id}` → **MJPEG over HTTP**（`multipart/x-mixed-replace`） |
| 端口 | `http://0.0.0.0:7860`（需 Node.js 18+ 与 Python 3.10） |
| **对 Unity 的含义** | **没有一手 Spout/NDI 通道。** 要么自己消费 MJPEG/WebSocket，要么自己在 Python 侧写 Spout/NDI 发送端 |

**StreamDiffusionTD（TouchDesigner 版）的 GitHub 仓库已消失**：`cumulo-autumn/StreamDiffusionTD` → **404**。它曾以 `.tox` 形式由 @dotsimulate（Lyell Hintz）发布，其教程要求 Windows + CUDA 显卡 + **NDI SDK** + TouchDesigner 2023 → **说明它当年走的是 NDI**（教程为二手来源）。现状：**已商业化闭源**，并入 **LOPs**（"LOPs is **proprietary software** for TouchDesigner"，由 Dotsimulate 发行）。
⚠️ `github.com/sina-cb/StreamDiffusionTD` 存在但**只是主仓库的 fork 挂了个误导性名字**，不是 TD 工具，不要引用。

#### 结论：StreamDiffusion 能不能当实时背景源？

| 判断 | 依据 |
|---|---|
| 作为**桌面 PC 上**的实时 img2img | ✅ 可行，但**必须 RTX 级别显卡**，且 1 步才有 ~94fps |
| 作为**游戏内嵌**背景 | ❌ **不可行**。它是独立的 Python/CUDA 进程，Unity 侧只能通过 MJPEG/WebSocket 或自建 Spout/NDI 消费 |
| 作为**Android 上的背景源** | ❌ **完全不可行**（CUDA 专用） |
| 项目**维护风险** | ⚠️ **高**。停更约一年，v0.1.1，依赖锁死旧版 diffusers |
| 把它当作"必定可用的方案" | ❌ 这是**研究风险**，不是 turnkey 路径 |

**如果确实要试**：正确形态是"**外部 PC 跑 StreamDiffusion → Spout（Windows 同机）或 NDI（跨机/上 Android）→ Unity 当作一张纹理**"，并且必须接受**端到端延迟 ≥ 一次生成的耗时（≥10ms，实际更长）+ 传输 + 上屏**。**该端到端延迟的具体数值 = 未验证。**

### 5.7 端上推理的实际结论（三重否决）

对本项目来说，第 5 题有**三条独立的否决理由**，任何一条都足以否掉"用端上神经网络生成背景"：

| # | 否决理由 | 依据 |
|---|---|---|
| 1 | **Sentis 2.5+ 需要 Unity 6**，本仓库是 2022.3 | §5.3 |
| 2 | **Sentis 跑不了扩散 UNet**：不支持 `GroupNormalization`（扩散 UNet 的标配）、不支持 `If`/`Loop`/`Scan` 控制流、不支持 `Attention`/`RotaryEmbedding`、**不能导入已量化的 ONNX 模型** | §5.4 / [supported-operators](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/supported-operators.html) |
| 3 | **即便跑得动，移动端也撑不住全帧率**：Unity 自己的移动端逐像素图像模型都要"拆到 2 帧 + 低于刷新率运行以防过热" | §5.4 / [DepthEstimationSample README](https://raw.githubusercontent.com/Unity-Technologies/sentis-samples/main/DepthEstimationSample/README.md) |

**能跑的和不能跑的（若将来升到 Unity 6）**：

| 类型 | 可行性 | 理由 |
|---|---|---|
| 前馈 CNN（风格迁移、ESRGAN 类超分） | ⚠️ 理论可行 | 无控制流、无 GroupNorm 依赖的模型有机会；但分辨率与帧率仍需帧切片 |
| 完整 Stable Diffusion / SDXL UNet | ❌ **不可行** | `GroupNormalization` 缺失 + 无控制流 |
| 已量化的 ONNX（QDQ / INT8） | ❌ **不可行** | 量化算子不支持；只能在 Sentis 内部用 `ModelQuantizer` 量化，而它**只省内存不省时间** |
| 逐帧 img2img 背景 | ❌ | 三条否决叠加 |

**结论：把"生成式背景"放在端上是错误的方向。** 生成放在离线（现有 ComfyUI 管线）或外部 PC，端上只做"数据驱动的实时视觉"。

---

## 6. 视频播放作为中间路线

### 6.1 编解码与容器

Editor 平台可导入的扩展名（[Video file compatibility](https://docs.unity3d.com/2022.3/Documentation/Manual/VideoSources-FileCompatibility.html)）：

| Extension | Windows | macOS | Linux |
|---|---|---|---|
| `.mp4` / `.mov` / `.m4v` | X | X | — |
| `.avi` / `.asf` / `.wmv` | X | — | — |
| `.mpg` / `.mpeg` | X | X | — |
| `.ogv` / `.vp8` / `.webm` | X | X | X |
| `.dv` | X | X | — |

> "The optimal supported video codec for most platforms is **H.264**. However, the optimal encoding for Linux is a .webm container with VP8 for video and Vorbis for audio."

**WebM 导入的坑（官方原文）**："Unity usually only supports the import of WebM videos with **VP8 (video) and Vorbis (audio)** codecs. However, you can use the `StreamingAssets` folder to add WebM videos with the other codecs to the project… You can then use code to assign the raw file to the VideoPlayer component (`VideoPlayer.url`). This allows the target platform to read the file directly and bypass the Editor's support limitations."

**Android 的编解码不是 Unity 决定的**：官方表格直接把 Android 的 video/audio codec 指向 [Android Supported media formats](https://developer.android.com/media/platform/supported-formats)，Unity 侧无自己的白名单。

### 6.2 硬件解码 —— 注意：Unity **没有**硬解开关

| 项 | 官方说法 | 来源 |
|---|---|---|
| 最佳硬件加速编解码 | "The best natively supported video codec for hardware acceleration is **H.264**" | [Video file compatibility](https://docs.unity3d.com/2022.3/Documentation/Manual/VideoSources-FileCompatibility.html) |
| Unity 的软件解码路径 | "Unity is also capable of **software-based video decoding. This uses the VP8 video codec and Vorbis audio codec**" | [Understanding video files](https://docs.unity3d.com/2022.3/Documentation/Manual/VideoSources-VideoFiles.html) |
| **有没有"硬件解码"开关？** | **没有。** Video Player 组件参考与 Video Clip Importer 参考都**不提供**该选项 | [class-VideoPlayer](https://docs.unity3d.com/Manual/class-VideoPlayer.html)、[class-VideoClip](https://docs.unity3d.com/Manual/class-VideoClip.html) |
| Importer 实际暴露的选项 | sRGB、**Transcode**、Dimensions、**Codec (Auto/H264/H265/VP8)**、Bitrate Mode、Spatial Quality、Keep Alpha、Deinterlace、Flip H/V、Import Audio | [class-VideoClip](https://docs.unity3d.com/Manual/class-VideoClip.html) |
| ⚠️ 一个容易误认的开关 | `VideoPlayerEditor.cs` 里的 `enableDecodingTooltip = "Enable decoding for this track…"` 是**音轨启用**复选框，**不是**硬件解码开关 | 官方编辑器 C# 源码 |
| 谁决定硬解/软解 | **平台原生解码器**，不是你。Unity 编解码表：VP8 在多数平台为软件、在 Android 与 Web 可为硬件；H.264 在多数平台为硬件 | [Video file compatibility](https://docs.unity3d.com/2022.3/Documentation/Manual/VideoSources-FileCompatibility.html) |
| 跨平台优先 | VP8 "widely supported… but it **consumes more resources** than hardware-accelerated codecs such as H.264" | 同上 |
| Android | "Android supports VP8 using native libraries, so **VP8 might also be hardware-assisted on some Android devices**" | 同上 |
| H.265 | Android 5.0+；Windows 需 Win10 + HEVC extensions | 同上 |

**Alpha / 透明视频**（[Video transparency overview](https://docs.unity3d.com/Manual/VideoTransparency-overview.html)）：

| 方案 | 说明 |
|---|---|
| H.264 | **不支持**内建 alpha |
| Unity 的透明路径 | **VP8/WebM 带 alpha**，或 **ProRes 4444** |
| H.264 强行带 alpha | Unity 会把**帧宽加倍**，alpha 以单色右半幅打包，运行时由 shader 重新组装 → **可用分辨率减半**（4096 上限变成 2048 有效） |

补充已核实细节：

| 项 | 事实 | 来源 |
|---|---|---|
| **Android + VP8 透明** | ⚠️ "**Android's built-in VP8 support doesn't include transparency support, so you must enable transcoding** to ensure Unity uses its internal alpha representation." | [Video transparency overview](https://docs.unity3d.com/Manual/VideoTransparency-overview.html) |
| **WebM 的解码方式** | "**Most of Unity's supported platforms use a software implementation to decode WebM files.**" → 再次确认 VP8 路线在多数平台是软解 | 同上 |
| **VP9** | ❌ "**Unity doesn't support the import of VP9 videos.**" 仅能通过 `StreamingAssets` 原始文件交给目标平台直接读 | [Video file compatibility](https://docs.unity3d.com/2022.3/Documentation/Manual/VideoSources-FileCompatibility.html) |
| **AV1** | **Unity 视频文档中完全没有提及 → 视为不支持（负面结论）** | 全文检索无结果 |
| **HAP** | **Unity 官方文档完全没有提及 HAP** → `KlakHap` 是第三方编解码插件，**不是** Unity 支持的能力 | 同上；`VideoCodec` 枚举仅 Auto/H264/H265/VP8 |
| **GOP / all-intra 调优** | **Unity 无任何官方建议**。官方"Key encoding values"表只列 Codec / Resolution / Profile / Profile Level / Audio Codec / Audio Channels | [Video file compatibility](https://docs.unity3d.com/2022.3/Documentation/Manual/VideoSources-FileCompatibility.html) |
| **其他可转码编解码** | 只有 H.264 / H.265 / VP8（音频自动：H.264/H.265 → AAC，VP8 → Vorbis） | [Introduction to transcoding](https://docs.unity3d.com/Manual/video-transcode-intro.html) |
| **转码耗时** | `VideoClipImporter.transcodeSkipped` 文档警告：转码"may be **quite long, up to many hours** depending on source resolution and content duration" | [VideoClipImporter](https://docs.unity3d.com/ScriptReference/Video.VideoClipImporter.html) |

#### ⚠️ Android 视频的官方历史约束（**已从当前文档中删除**）

以下内容见于 **2019.4** 的 `Video.VideoPlayer` 脚本文档，**在 2022.3 及更新版本中已被移除**。它们仍是理解 Android 视频能力边界的唯一官方表述：

> "Support for **resolutions above 640 x 360 is not available on all devices**. Runtime checks are done to verify this and failures will cause the movie to **not be played**."
> "For Jelly Bean/MR1, movies **above 1280 x 720 or with more than 2 audio tracks will not be played** due to bugs in the OS libraries."
> "For Lollipop and above, any resolution or number of audio channels may be attempted, but **will be constrained by device capabilities**."
> "**Playback from asset bundles is only supported for uncompressed bundles**, read directly from disk."
> "When targetting **Android 9 or newer**, and playing over HTTP, the `usesCleartextTraffic` attribute must be added to your Android manifest."
> — [Video.VideoPlayer (2019.4)](https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Video.VideoPlayer.html)

**同一份 2019.4 文档还给出了最清晰的一句硬解总结**：

> "**The best natively supported video codec for hardware acceleration is H.264**, with VP8 being a **software** decoding solution that can be used when required. **On Android, VP8 is also supported using native libraries and as such may also be hardware-assisted depending on models. H.265 is also available for hardware acceleration where the device supports it.**"

> **对本项目的含义**：官方历史文档明确写了"**分辨率高于 640×360 不是所有设备都支持，检查失败会导致视频根本不播**"。本项目交付 2560×1440 的成片 —— 在低端 Android 设备上这是**真实的兼容性风险**，且失败模式是"不播放"而非"降级播放"。**建议 Android 构建附带一个降分辨率版本（例如 1280×720），并在运行时按设备能力选择。**

**Android MediaCodec / ExoPlayer：未验证。** 未找到任何 Unity 官方文档、博客或发布说明提及。唯一官方线索是 2019.4 文档中的一句：格式兼容性问题会报告在 `adb logcat`，且"**always prefixed with `AndroidVideoMedia`**"。**不要断言 Unity 用了 ExoPlayer。**

**对本项目的含义**：现有约定（H.264 High、yuv420p、无 B 帧、短 GOP、MP4 faststart）**正好命中硬件解码最优路径**，Windows 与 Android 都安全。**不要为了 alpha 改 VP8** —— 那会掉到软件解码，Android 上必卡；真需要 alpha 请用独立的叠加层方案而不是让主 BGA 带 alpha。

### 6.3 时钟：这是"锁歌曲时钟"的官方答案

Unity 2022.3 手册有专门一页讲 VideoPlayer 的时钟管理：

> "You can use the Video Player component to control how to time video playback relative to other interactive behaviors. For example, you can **synchronize video playback with animation or with audio**. You can do this through the following time update modes: **Audio Digital Signal Processing (DSP) clock / Game time / Unscaled game time**."
> "The audio DSP clock comes from the Audio module. You can access it through `AudioSettings.dspTime`."
> — [Clock management with the Video Player component](https://docs.unity3d.com/2022.3/Documentation/Manual/video-clock.html)

**枚举名与官方描述**（`VideoTimeUpdateMode`）：

| 枚举值 | 官方描述 | 适用 |
|---|---|---|
| **`DSPTime`** | "Update time based on the DSP clock. **Use this value to synchronize playback with Audio.**" | ✅ **节奏游戏唯一正确选择**（本仓库已在用） |
| `GameTime` | "based on `Time.time`" | 需要随 `timeScale` 暂停/慢放时 |
| `UnscaledGameTime` | "based on `Time.unscaledTime`" | 需要独立于游戏时间 |

`timeUpdateMode` 的总定义："The **clock source** used by the VideoPlayer to derive its current time."

**`VideoTimeReference`（`timeReference`）—— 决定"观察哪个时钟来检测并纠正漂移"**：

| 枚举值 | 官方描述 |
|---|---|
| `Freerun` | "The video plays **without influence from external time sources**." |
| `InternalTime` | "The **internal** reference clock the VideoPlayer observes to detect and correct drift." |
| `ExternalTime` | "The **external** reference clock…" |

另有 `externalReferenceTime`："Reference time of the external clock the VideoPlayer uses to correct its drift. **Only relevant when `timeReference` is `ExternalTime`.**"
以及只读的 `clockTime`："The clock time that the VideoPlayer follows to schedule its samples."

来源：[VideoPlayer.timeUpdateMode](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-timeUpdateMode.html)、[VideoTimeUpdateMode](https://docs.unity3d.com/ScriptReference/Video.VideoTimeUpdateMode.html)、[VideoPlayer.timeReference](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-timeReference.html)、[class-VideoPlayer](https://docs.unity3d.com/Manual/class-VideoPlayer.html)

> **组合建议**：`timeUpdateMode = DSPTime` + `timeReference = ExternalTime`（外部喂 `externalReferenceTime`）可以**由你自己的 `SongClock` 直接驱动纠偏**，而不是依赖 Unity 内部时钟。这比只设 `DSPTime` 更可控。**该组合的实际效果 = 未验证（未实测）。**

另外：`Time.captureFramerate > 0` 时 VideoPlayer 进入**同步**模式——"The Video Player displays all frames at the expected time stamp even if it needs to **delay the entire game's execution**"，prepare 与 seek 也变同步。该行为**只在 Game Time 模式下生效**；其他模式下 VideoPlayer 会"skip or repeat frames to stay in sync"，且 prepare/seek 异步。
来源：[Clock management](https://docs.unity3d.com/2022.3/Documentation/Manual/video-clock.html)

### 6.4 丢帧与 Seek —— 两条必须知道的硬事实

#### (a) `skipOnDrop`：官方原文

> "**Skip On Drop**: When you enable this option, and the Video Player component detects drift between the playback position and the game clock, the Video Player **skips ahead**. When you disable this option, the Video Player doesn't correct for drift and **systematically plays all frames**."
> — [Video Player component reference](https://docs.unity3d.com/Manual/class-VideoPlayer.html)

脚本侧定义：`skipOnDrop` = "Whether the VideoPlayer is **allowed to skip frames to catch up with current time**."，仅当 `canSetSkipOnDrop` 为 `true` 时可设。
来源：[skipOnDrop](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Video.VideoPlayer-skipOnDrop.html)

> **结论**：`timeUpdateMode = Audio DSP Time` + `skipOnDrop = ON` **就是"把视频锁在音频时钟上"的官方配置**。代价是它会**丢帧**来追上时钟。

#### (a2) ⭐ `Wait For First Frame` —— 起播同步的取舍（与本仓库直接相关）

组件参考原文：

> "**Wait For First Frame**: Wait for the first frame of the source video to be ready for display before playback starts. **Clear it to keep the video time in sync with the rest of the game, which might cause the first few frames to be discarded.**"
> — [Video Player component reference](https://docs.unity3d.com/Manual/class-VideoPlayer.html)

| 设置 | 行为 | 适用 |
|---|---|---|
| **勾选**（默认） | 等首帧就绪才开始播 | 过场动画（宁可晚一点也不丢帧） |
| **不勾选** | **视频时间与游戏其余部分保持同步，但可能丢弃开头几帧** | ✅ **节奏游戏**：首帧晚到比整体错位好 |

**另外**：`Play On Awake` 是组件级开关；`VideoPlayer.Pause()` 的绑定层备注也印证了本仓库的做法 —— "If you seek through to a different point in the video and then call `Pause()` **before the VideoPlayer finishes preparation**, it triggers preparation and **shows the frame that was the seek target**."
来源：[class-VideoPlayer](https://docs.unity3d.com/Manual/class-VideoPlayer.html)、`VideoPlayer.bindings.cs`

#### (b) Seek 是**异步且串行排队**的 —— 不要用它做逐音符随机访问

> "Seek operations are done by changing the `time` or `timeFrames` property. **Seek duration may be noticeably long** depending on the codec performance and the parameters chosen at encoding time."
> — [VideoPlayer.seekCompleted](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-seekCompleted.html)
>
> "If you set `time` to another value during this operation, the VideoPlayer creates a **new seek operation and adds it to a queue**. The new operation will start when the previous one completes."
> "The `time` value only properly settles when the VideoPlayer **displays the frame**."
> — [VideoPlayer.time](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-time.html)

| 事实 | 设计含义 |
|---|---|
| seek 时长"可能明显很长"，取决于编解码性能与**编码时的参数** | 本仓库"每 15 帧一个关键帧"正是控制这个"encoding parameter" |
| 重复设 `time` 会**排队**，新操作要等前一个完成 | 快速连续 seek 会**积压**，产生长尾延迟 |
| `time` 只在**帧真正显示后**才稳定 | 不能把 `time` 当作即时的权威位置 |
| 没有官方 seek 耗时数字 | 具体数值 **未验证** |
| `canSetTime` 文档："**Seeking is not supported in all contexts.** For example, seeking in a HTTP live stream." | 不能假设任何来源都可 seek |
| `frame`："A frame index of **-1** indicates that **no valid frame is available**."（WebGL 上因帧率未知，按 **24FPS** 假定） | 必须判 -1，否则会拿到无效帧 |
| `frameReady`：需先把 `sendFrameReadyEvents` 设为 `true`；"**This event is likely to tax the CPU**, so set it back to `false` when you don't need it." | 逐帧回调有真实 CPU 成本，只在需要时开 |
| `frameDropped`：在解码器未能按时源产出帧时回调 | 可用来检测/统计丢帧 |
| `StepForward()`：暂停并前进**恰好一帧**；若尚未 prepared 则只准备并显示首帧、不前进。"**the WebGL implementation is unable to provide frame-accurate control**" | 逐帧步进只适合编辑器/调试 |
| `canStep` / `canSetTime` / `canSetSkipOnDrop` 均为只读，且"**only valid after the movie has been prepared**" | 必须在 prepare 完成后才能查询 |

来源：[VideoPlayer.time](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-time.html)、[VideoPlayer.frame](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-frame.html)、[VideoPlayer.frameReady](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-frameReady.html)、[VideoPlayer.frameDropped](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-frameDropped.html)、[VideoPlayer.StepForward](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer.StepForward.html)

> **一个官方技巧**（`frameReady` 文档）："When you call `Pause()` on a VideoPlayer that's not prepared or playing, it behaves as if you called `Play()` and then immediately called `Pause()`. **This allows you to seek to a certain point and pause to give it time to prepare the frame.**" —— 这正是本仓库"视频在解码器定位完成且目标帧进入显示队列后恢复播放"做法的官方依据。

> **关键设计结论：绝不要用"逐音符 seek 视频"来做 BGA。** 官方文档明确把 seek 描述为昂贵且串行的。视频背景必须**连续播放**，由 `timeUpdateMode = Audio DSP Time` 自然保持同步；seek 只用于**大跨度跳转**（重开、编辑器里拖时间轴），且必须做合并/去抖。

### 6.5 结构选型：长单视频 + 连续播放 vs 多短片 + 交叉淡化

| 维度 | 长单视频（连续播放，DSP 时钟） | 多短片 + 交叉淡化 |
|---|---|---|
| 与歌曲时钟对齐 | `timeUpdateMode = Audio DSP Time` 直接锁定，**天然对齐**；漂移由 `skipOnDrop` 兜底 | 需自己管理多条 VideoPlayer 的起播与淡入淡出，容易出现相位漂移 |
| seek 需求 | **正常播放不需要 seek**；只有大跳转才 seek | 切换点只需 prepare 下一条，**无需 seek** |
| 解码器占用 | 1 个 | N 个（Android 硬解实例数量有限，**风险高**；上限 **未验证**） |
| 段落硬切 | 边界处交叉溶解（在视频内预烘焙，或运行时两视频叠加） | 天然分段，接缝需交叉溶解 |
| 本仓库经验 | 已在用 | `VideoBgaWorkflow.md` 记录 6 段硬切"实测每次跳变约为该视频正常帧间差的 7 倍"，故改为 **5 帧交叉溶解** 的接缝链 |

**结论：对本项目，"长单视频连续播放 + DSP 时钟 + `skipOnDrop` 兜底" 优于 "多短片 + 多解码器"。** 段落切换优先**在视频内预烘焙**（本仓库已这么做），而不是在运行时叠多个 VideoPlayer。

一个重要的**否定结论**：`timeUpdateMode = Game Time` + `captureFramerate > 0` 虽然能得到"显示每一帧"的严格同步，但代价是**拖慢整个游戏的执行**，且只在 Game Time 下生效——**不适合需要实时判定的节奏游戏**。

### 6.6 视频的音频如何处理（与本项目的 DSP 时钟直接相关）

`VideoPlayer.audioOutputMode` 四档（[class-VideoPlayer](https://docs.unity3d.com/Manual/class-VideoPlayer.html)）：

| 模式 | 官方描述 |
|---|---|
| `None` | "Audio isn't played." |
| `Audio Source` | "**Audio samples are sent to selected audio sources, enabling Unity's audio processing to be applied.**" |
| `Direct` | "**Audio samples are sent directly to the audio output hardware, bypassing Unity's audio processing.**"（C# 备注：绕过 AudioSource 直接送硬件） |
| `API Only`（实验性） | "Audio samples are sent to the associated `AudioSampleProvider`." |

两个坑：

| 坑 | 官方原文 |
|---|---|
| Audio Source 模式的控制不生效 | "**The audio source's playback controls (Play On Awake and `Play()`) don't apply to the video source's audio track.**"；Mute/Volume 属性"**available only when Audio Output Mode is `Direct`**" |
| Web 限制 | "**WebGL only fully supports `None` and `Direct`.** If you set the output mode to `AudioSource`, Unity **ignores all AudioSource fields except mute**." |

**本项目相关的关键未知项**：手册说 DSP 时钟"comes from the Audio module… `AudioSettings.dspTime`"，且 `timeUpdateMode = DSPTime` 是"Use the same clock source that processes audio"。
**但 VideoPlayer 自身的音频是否推进/驱动 `AudioSettings.dspTime`（尤其在 `Direct` 模式下）= 未验证**，未找到任何官方说明。

> **因此本仓库现有的做法（视频静音 + 独立 AudioSource 播歌曲 + `SongClock` 以 `dspTime` 为权威）是最稳的**：它让**歌曲**成为唯一的时钟权威，视频只是跟随者。
> 注：Unity **没有**官方推荐"静音视频改用独立 AudioSource" —— 官方只文档化了能力，没有给出处方。**该结论是基于机制推理的工程建议，不是官方声明。**

---

## 7. 已发行节奏游戏的先例

> **本节证据分级**：osu! 是**开源 + 官方 wiki**，证据最强，已逐条核实。其余游戏多为社区逆向或缺乏一手文档，**未核实的一律标注**。

### 7.1 osu!（证据最完整，也是最重要的先例）

osu! 的背景 = **手写的分镜脚本（storyboard script）**，存在 `.osb` 文件（或 `.osu` 的 `[Events]` 段）里。它是**纯文本命令序列**，不是可视化编辑器产物（虽然编辑器能编辑）。

**命令全集**（官方 cheat sheet 原表）：

| 事件 | 含义 |
|---|---|
| `F` | fade（不透明度 0–1） |
| `M` | move（起点 x,y → 终点 x,y） |
| `MX` / `MY` | 只改 X / 只改 Y |
| `S` | scale |
| `V` | vector scale（宽高分别缩放） |
| `R` | rotate（**单位是弧度**，正值为顺时针） |
| `C` | colour（**减色**：255,255,255 = 原色，0,0,0 = 全黑） |
| `L` | loop（标准循环，`loopcount` 次） |
| **`T`** | **trigger（事件触发）** ← **关键** |
| `P` | parameter（`H` 水平翻转 / `V` 垂直翻转 / `A` 加色混合） |

层级：`0` Background / `1` Fail / `2` Pass / `3` Foreground（lazer 客户端内部另有 `Overlay = 4`、`Video = 5`）。
画布 **640×480**，实际游玩区 **510×385**（x 60–570，y 55–440）。
来源：[Storyboard scripting commands](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Commands)、[Cheat sheet](https://raw.githubusercontent.com/ppy/osu-wiki/refs/heads/master/wiki/Storyboard/Scripting/Cheat_Sheet/en.md)

> ⚠️ **更正**：Cheat Sheet 只列了 3 种缓动（`0` none / `1` 先快后慢 / `2` 先慢后快），但**完整的 Commands 页列出了 0–34**：
> `0` Linear、`1` Easing Out、`2` Easing In、`3–5` Quad In/Out/In-Out、`6–8` Cubic、`9–11` Quart、`12–14` Quint、`15–17` Sine、`18–20` Expo、`21–23` Circ、`24–28` Elastic、`29–31` Back、`32–34` Bounce。
> 代码里是直接转型到 `osu.Framework.Graphics.Easing`。**做视觉复刻时按 0–34 实现，不要按 Cheat Sheet 的 3 种。**
> 来源：[Storyboard scripting commands](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Commands)、[`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L230

**一个值得借鉴的性能指标 —— SB Load**：

> "**SB Load** … is a measure of **how many times the full 640x480 area needs to be redrawn in a frame**."
> "Without any storyboarding, this value is 1x… Including a single image that takes up exactly half of the screen would result in 1.5x; two images that overlap entirely and take up half of the screen would result in 2x."
> "**It's best if a map never exceeds 5x SB Load.**"
> — [SB load](https://osu.ppy.sh/wiki/en/Client/Beatmap_editor/SB_load)

→ 这是一个**与设备无关的过绘制预算**。本项目的实时 BGA 层可以直接照抄这个思路：给每首曲子设一个 overdraw 上限并让编辑器实时显示。

#### ⭐ 核心问题的答案：osu! 能按 hitsound / 按"任意命中"触发，但**不能按某一个具体音符**触发

Wiki 的 Cheat Sheet 列了 5 项（**这是过时的**）：

> "Current triggers supported are: **HitSoundClap / HitSoundFinish / HitSoundWhistle / Passing / Failing**"
> — [Cheat sheet](https://raw.githubusercontent.com/ppy/osu-wiki/refs/heads/master/wiki/Storyboard/Scripting/Cheat_Sheet/en.md)

**lazer 客户端的真实触发器清单**（比 wiki 宽，来自源码 `StoryboardTriggerController.cs` 的分发逻辑）：

```csharp
switch (triggerGroup.TriggerName)
{
    case @"Passing":            bindPassing(drawable, triggerGroup, true);  break;
    case @"Failing":            bindPassing(drawable, triggerGroup, false); break;
    case @"HitObjectHit":       bindHitObjectHit(drawable, triggerGroup);   break;   // ← lazer 新增
    case string s when s.StartsWith(HitSampleTriggerDefinition.PREFIX, StringComparison.OrdinalIgnoreCase):
                                bindHitSample(drawable, triggerGroup);      break;
}
```

| 触发器 | 精确拼写 | 行为 |
|---|---|---|
| 通过 / 失败状态 | `Passing` / `Failing` | 绑定到 `Bindable<bool> Passing`，状态翻转且极性匹配时触发 |
| ⭐ **任意命中** | **`HitObjectHit`** | **当 `LastJudgementResult` 变为"命中且可计分"时触发**（lazer 新增，**wiki 尚未收录**） |
| hitsound 家族 | `HitSound` **前缀，大小写不敏感** | 当 `LastPlayedSamples` 变化且播放的 `HitSampleInfo[]` 匹配时触发 |

**hitsound 触发器的真实语法是一条正则家族**（比 wiki 的 5 项宽得多）：

```csharp
PREFIX = @"HitSound";
parse_regex = new Regex(
  @$"(?i)^{PREFIX}(?<bank1>(All|Normal|Soft|Drum))?(?<bank2>(All|Normal|Soft|Drum))?(?<name>(Whistle|Clap|Finish))?(?<suffix>\d+)?$",
  RegexOptions.Compiled);
```
→ 全部片段可选，因此 `HitSound`、`HitSoundClap`、`HitSoundDrumWhistle`、`HitSoundAllSoft`、`HitSoundDrumClap0`、`HitSound6` **都能解析**。
来源：[`StoryboardTriggerController.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/StoryboardTriggerController.cs) L43–64、L109–121

#### ⚠️ 但**没有**"某一个具体音符"的触发器

> "There is **no note/object ID, index or timestamp reference anywhere in the trigger grammar**. … The **only** targeting mechanism is the validity window `[starttime, endtime]`."
> — 源码核实：`HitSampleTriggerDefinition` 只有 `RawName` / `NormalBank` / `AdditionBank` / `AdditionName` / `Suffix`；`StoryboardTriggerGroup` 只有 `TriggerName` / `TriggerStartTime` / `TriggerEndTime` / `GroupNumber`。

激活判定就是时间窗：

```csharp
ActiveAt(double time) => TriggerStartTime <= time && time <= TriggerEndTime
```

**所以精确到某个音符的做法只能是"把时间窗收窄到那个音符的时间戳"** —— 这是官方实现的机制，不是 hack。

| 子问题 | 已核实答案 |
|---|---|
| 能按 hitsound 触发？ | ✅ 是（正则家族） |
| 能按"任意命中"触发？ | ✅ 是（`HitObjectHit`，仅 lazer） |
| 能按**某一个指定音符**触发？ | ❌ **不能**（无音符 ID/索引，只有时间窗） |
| 近似做法？ | 把 `starttime`/`endtime` 收窄到该音符时间戳 |

**其他已核实约束**（[Compound commands](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Compound_Commands)）：

> "If a trigger condition occurs while another trigger is running, **the earlier trigger is stopped, and the new trigger starts**. Triggers will **not occur until other commands are finished**, so it's usually best to either use only triggers on an object declaration or not at all."

Ranking criteria 的实务提醒："**Fade out sprites activated from triggers after usage.** Triggers will activate from their first possible command and stay active until the end of the difficulty."

#### 官方编辑器能做什么、不能做什么

> "The **Storyboard Editor** is a section of the in-game Beatmap Editor, under the **Design** tab"
> **限制（原文）**："**No loop or trigger support.**" / "**No Move-X/Move-Y commands.**" / 坐标永远是 320,240
> "To use the Loop and Parameters, you will need to do some **Storyboard Scripting** to utilise them."
> — [Design tab](https://osu.ppy.sh/wiki/en/Client/Beatmap_editor/Design)

> "Storyboarding is often very difficult… **most avid storyboarders opt to program via storyboard scripting directly.** Many creators choose to write programs in full-featured programming languages to generate storyboard scripts"
> — [Storyboard](https://osu.ppy.sh/wiki/en/Storyboard)

> **关于任务里提到的 "Sabik"**：**官方 osu! wiki 中查无此物**（Storyboard / Storyboard scripting / Design tab 及其 Community tools 段落均无提及），网络检索也无 osu! 相关一手来源。**结论：Sabik 不是官方文档化的 osu! 分镜工具 = 未验证/不存在。**
> **SGL** 则确有其事，但**只在社区论坛帖子里**（"[Guide] SGL & Optimization"），不在官方 wiki：由 **MoonShade** 开发、**Damnae** fork 维护，是编译到分镜代码的脚本语言；缩写展开为 "Storyboard Graphing Language" 这一点 **未验证**。
> 官方 wiki 唯一提到的社区工具是 **storybrew**（Damnae）："Various tools have been made by the community to abstract and build upon storyboard scripting, such as Damnae's storybrew."
> — [Storyboard scripting § Community tools](https://osu.ppy.sh/wiki/en/Storyboard/Scripting)

#### 其他已核实事实

| 项 | 事实 |
|---|---|
| 动画 | **不是视频，是图片序列**：`Animation,"layer","origin","filepath",x,y,frameCount,frameDelay,looptype`；文件名形如 `sliderball0.png`…`sliderball9.png`；`looptype` ∈ `LoopForever` / `LoopOnce` |
| Z 序 | 由文件在 `.osu` 中出现的顺序决定（**先出现的在后面**） |
| 变量 | `.osb` 支持 `[Variables]` 段（如 `$white=255,255,255`），**`.osu` 内的分镜不支持** |
| 音效 | `Sample,time,layer,"filepath",volume`（volume 1–100） |
| 简写 | 三种 shorthand，可批量生成同类事件；`endtime` 留空是**必须的**写法 |
| **视频背景** | ✅ **支持**。见下方专节 |

#### osu! 的视频背景规范（⚠️ 含一条对本项目直接相关的禁令）

官方规定（[Guides/Compressing files](https://osu.ppy.sh/wiki/en/Guides/Compressing_files)、[Ranking criteria](https://osu.ppy.sh/wiki/en/Ranking_criteria)）：

> "**osu! supports video encoded in the H.264 format with the `.mp4` file extension.** Other formats, such as H.265, VP9, and AV1, and file extensions such as `.mkv` and `.mov`, are currently **not supported**."

| 规则（Ranking criteria 原文） |
|---|
| "A video's dimensions **must not exceed a width of 1280 and a height of 720 pixels**." |
| "**A video must be encoded in H.264.**" |
| "A video's offset **must be correct if it synchronizes with the song**." |
| "**A video's audio track must be removed from the video file.**" |
| ⚠️ **"Videos which are substantially AI-generated must not be used."** |

> 🚨 **对本项目的重要提示**：osu! 的 ranking criteria **明确禁止"实质性 AI 生成"的视频背景**用于可 rank 的谱面。本仓库当前用 ComfyUI/Wan 2.2 生成的 BGA **属于该禁令范围**。这不影响本项目自研游戏的运行，但**如果将来要把谱面发布到 osu!，AI 生成的 BGA 不能随谱提交**。这是一条产品层面的约束，不只是技术约束。

**视频在图层中的位置（源码核实）**：视频位于**最底层**，在 `Background` 之后：
`Video (4) → Background (3) → Fail (2) → Pass (1) → Foreground (0) → Overlay (最前)`
（`Storyboard.cs` L66–72；depth 越大越靠后）

**`.osb` 与 `.osu` 的分工**：`.osb` 里的命令作用于**所有难度**；`.osu` 里的只作用于**该难度**。且 "Commands from the `.osb` file take precedence over those from the `.osu` file within the layers, as if the commands from the `.osb` were appended to the end of the `.osu` commands."

> **osu! 的启示**：一个成熟节奏游戏的分镜系统 = **命令式时间轴 + 分类触发器**。作者控制的是"某个图层在某个时刻做某个变换"，以及"某类音效播放时播一段一次性动画"。**基础美术永远是手做的；数据驱动的是变换与触发。**

### 7.2 ADOFAI（A Dance of Fire and Ice）

**官方内置关卡编辑器**（Events 分页：Gameplay / Track / Decorations / Visual Effects / Event Modifiers / Conveniences / DLC）+ Steam Workshop（app 977950）。**文档为社区撰写**（adofaieditor.gitbook.io），非官方。

#### 谱面结构：`.adofai` 是 JSON，**四个顶层键**

| 键 | 内容 |
|---|---|
| `angleData` | 地砖角度（度） |
| `settings` | 关卡级设置 |
| **`actions`** | **事件列表** |
| `decorations` | 装饰物 |

#### ⭐ 事件绑定到「地砖索引」，并且**支持逐判定触发**

| 机制 | 说明 |
|---|---|
| 绑定单位 | 事件通过 **`floor`（地砖索引）** 绑定；子砖级精度用 **`angleOffset`（度）**，官方文档："**0º = on hit**" |
| 事件串联 | 可选 `eventTag` 把事件互相链接 |
| ⭐ **逐判定条件触发** | **`Set Conditional Events`** 支持条件标签：**Loss · Too Early · Too Late · Early · Late · Perfect · EPerfect · LPerfect**。事件只有**与条件事件建在同一地砖上**才会触发；每个条件只接受一个标签 |

> **这比 osu! 更细**：ADOFAI 能按**具体某一块砖（音符）+ 具体判定等级**触发视觉事件。osu! 只能按音效类别或"任意命中"，Cytoid 能按音符 ID 但不带判定等级。**ADOFAI 是三者中条件表达力最强的。**

⚠️ **不要引用 `OnHit` / `OnLand` / `OnFalling`** —— 这些**不在 ADOFAI 的事件词汇表中**。同样，`AddObject`/`AddParticle` 也不是事件名，真实名称是 `Set Object` / `Set Text` / `Set Particle` / `Emit Particle` / `Move Decorations`。

#### 事件分类（已核实）

| 分类 | 事件 |
|---|---|
| Gameplay | Set Speed、Twirl、Checkpoint、Set Hitsound、Play Sound、Set Planet Orbit、Pause Tiles、AutoPlay Tiles、Scale Planets |
| Track | Set Track Color、Set Track Animation、Recolor Track、Move Track、Position Track |
| Decorations | Move Decorations、Set Text、Emit Particle、Set Particle、Set Object、Set Default Text |
| **Visual Effects** | **Set Background、Flash、Move Camera、Set Filter、Set Filter Advanced、Hall Of Mirrors、Shake Screen、Bloom、Tile Screen、Scroll Screen、Set Frame Rate** |
| Event Modifiers | Repeat Events、**Set Conditional Events**、Set Input Event |
| Conveniences | Editor Comment、Bookmark |
| DLC | Hold、Multi Planet、Freeroam… |

#### ⭐ 视频背景是**官方支持**的

`settings` 里有 **`bgVideo`、`loopVideo`、`vidOffset`**；实际发行的关卡文件使用了 `bga_1.mp4`。

> ⚠️ **但 `SetBackground` 事件没有视频选项** —— **视频是关卡级设置，不是逐地砖触发器**。即"视频当底图，事件在其上叠加"。
> 另一个已核实限制：**ADOFAI 不支持 GIF**（"Since adofai doesn't support gif's"），社区靠事件切换帧来模拟动画。

**官方且数据驱动的镜头/画面能力**：`Shake Screen`（时长/强度/速度/缓动/淡出）、`Flash`、`Set Filter`、`Bloom`、`Hall Of Mirrors`，以及装饰物的 **Depth**（"virtual layers of depth to create the illusion of distance"）。

> **ADOFAI 的启示（对本项目最强）**：
> 1. 谱面本身就是**事件列表**（每个地砖可挂事件），与本仓库 `effectClips[]` 思想一致。
> 2. **视频作为关卡级底图 + 事件在其上叠加** —— 这正是本报告 §9.1 推荐的三层架构，而且是一个已发行商业游戏的做法。
> 3. **按"地砖 + 判定等级"触发** 是比按时间戳触发更强的表达力，值得作为 `effectClips[]` 的长期目标。

### 7.3 Cytoid / Cytus II（**对本项目最有参考价值的一手规范**）

Cytoid 是一个开源 Cytus 风格音游，其官方 wiki **完整文档化了 Cytus II 的谱面格式（C2 format）与 storyboard 规范**。这是本报告找到的**最接近本项目需求的一手资料**：JSON 谱面 + JSON storyboard + 逐音符触发。

#### (a) C2 谱面格式（= Cytus II 格式）

> "**Cytus II (C2) chart format** is a **JSON** file, that means you can edit easily in any text editor."

根参数：`format_version`、`time_base`、`music_offset`、`size`、`ring_color`、`fill_colors`、`opacity`、`tempo_list`、`page_list`、`note_list`、**`event_order_list`**。
音符类型 0–7：Click / Hold / Long Hold / Drag head / Drag child / Flick / C-Drag head / C-Drag child。
来源：[C2 chart format](https://cytoid.wiki/en/reference/chart/c2-format)

**`event_order_list` 是关键** —— 谱面自带一个**按 tick 排列的事件列表**：

| 对象 | 字段 |
|---|---|
| `EventOrder` | `tick`（何时触发）、`event_list`（`ChartEvent` 数组） |
| `ChartEvent` | `type`、`args` —— 其中 `W`（扫描线恢复原速）、`R`（比原速慢）、`G`（比原速快） |

> 即：**Cytus II 的数据驱动视觉集中在"扫描线（判定线）"上，由谱面的 tick 事件驱动**，而不是驱动背景贴图。

#### (b) Storyboard 规范 v2.0.2 —— ⭐ **逐音符触发**

根对象：`texts`、`sprites`、`lines`、**`videos`（实验性）**、`controllers`、**`note_controllers`**、`templates`。

**这是对本项目最重要的一条 —— 触发器可以精确到"某一个音符"**：

```json
"texts": [
  {
    "id": "hello_world", "text": "Hello world!", "size": 80, "opacity": 1,
    "states": [ { "opacity": 0, "relative_time": 0.5, "destroy": true } ]
  }
],
"triggers": [
  { "type": "noteClear", "notes": [4], "spawn": ["hello_world"] }
]
```

> 官方注释："the `Hello world!` text is **spawned and displayed when note 4 is cleared**"

**对比 osu!**：osu! 只能按 **hitsound 类别 × 时间窗** 触发；Cytoid/Cytus II 可以按 **具体音符 ID** 触发。**后者才是本报告 §9.2 想要的粒度。**

**时间可以直接锚定到音符**（官方支持的 `time` 取值格式）：

| 格式 | 含义 |
|---|---|
| `"start:<Note ID>"` | 指定音符的**开始**时间 |
| `"end:<Note ID>"` | 指定音符的**结束**时间 |
| `"intro:<Note ID>"` | 指定音符的**淡入**时间 |
| `"start:<Note ID>:<Offset>"` | 上者 + 偏移（秒，可负） |
| ⭐ `"at:<Note ID>:<Percentage>"` | **仅长按音符**：start + (end − start) × Percentage；0 等价于 start，1 等价于 end |

也可以传数组一次生成多个相同状态：`"time": ["start:57", "start:123", "start:153"]` 会自动展开。

**`note_controllers` —— 官方称"迄今为止最强大的分镜技术"**：

> "A note controller **overrides and animates the properties a single note defined in the chart file**. This is the most powerful storyboard technique so far. **You can implement almost any desired gameplay in Cytoid using note controllers!**"

字段：`note`（谱面里的音符 ID）、`override_x`/`x`/`x_multiplier`/`dx`、`override_y`/`y`/`y_multiplier`/`dy`。

**这套坐标系统设计值得直接借鉴**：

| 坐标系统 | 范围 | 用途 |
|---|---|---|
| `stageX` / `stageY` | **800 × 600**，(0,0) 在中心 | sprite / text 画布 |
| `noteX` / `noteY` | **[0,1]**，(0,0) 左下 | 音符画布 |
| `cameraX` / `cameraY` | 基于正交尺寸 | 相机 |
| depth（Z） | — | 仅当场景控制器开启 perspective |

并支持**坐标系统转换语法 `[coordinate system]:[value]`**，例如 `"x": "noteX:0"`、`"width": "stageY:600"`。
官方解释其价值：2.0.0 之前要让 sprite 对齐音符区边界，必须**按屏幕宽高比手工估算并维护多套 storyboard**；有了坐标转换就能直接表达真实坐标，"Cytoid automatically calculates the actual `x` and `y` values"。

**其他已核实要点**：

| 项 | 事实 |
|---|---|
| 图层 | `layer` 0（在所有游戏元素之后、背景之前）/ 1（在音符之上、UI 之下）/ 2（在所有游戏元素之上）；层内用 `order` 排序 |
| 状态与缓动 | 关键帧 `states` + `easing`（参见 easings.net，默认 `linear`）；`relative_time`（相对父状态）、`add_time`（相对上一个状态） |
| 属性复用 | `target_id`（让一个"无实体"对象去控制另一个对象的属性，用于**叠加不同缓动**，例如 X 用 linear、Y 用 easeOutQuad 画弧线）；`parent_id`（坐标系跟随父对象，**可以把 sprite 的 parent 设为 note controller，让 sprite 跟着音符动**） |
| 背景辅助 | `fill_width`：忽略 width/height，自动撑满舞台宽度、高度 10000 |
| 性能建议 | sprite "keep resolution below **1920px × 1080px**, and convert PNGs to JPGs when transparency is not needed" |
| ⭐ **视频支持** | `videos` 数组（**实验性**）。官方原文："Since supported video codecs are different across platforms and devices, it is **strongly recommended to use a standard H.264 `.mp4` file at maximum 720p resolution**." 已知问题："**Video will not pause when the game is paused.**" |

来源：[Cytoid Storyboard Specification v2.0.2](https://cytoid.wiki/en/reference/storyboard/specification)

> ⭐ **这条视频建议独立印证了本报告 §6 的结论**：一个已发行、跨平台（含移动端）的音游，其官方 storyboard 规范推荐的正是 **H.264 / MP4 / ≤720p** —— 与本仓库现有的 H.264 交付约定和 §6.2 的硬件解码分析完全一致。

### 7.4 BGA 这个词的来源：beatmania IIDX / BMS

> **证据分级**：Konami 从未公开 IIDX 的背景格式文档。但 **BMS 侧的通道定义是公开且社区权威的**（BMS command memo、angolmois 的内部文档、bms-rs 的通道枚举），足以确定 BGA 的技术含义。

**BGA = BackGround Animation**。BMS 通道表直接给出了展开：

| 通道 | 含义 |
|---|---|
| **`#xxx04`** | **BGA-BASE**（`<abbr title="BackGround Animation">BGA</abbr>-BASE`） |
| **`#xxx06`** | **BGA-POOR** —— 官方注解："**displayed when a note is missed**" |
| `#xxx07` | 中间层（LAYER） |
| `#xxx0A` | 顶层（LAYER2） |

现代解析器枚举同一家族：`BgaBase, BgaLayer, BgaLayer2, BgaPoor, BgaBaseArgb…, BgaKeybound(#SWBGAxx), BgaBaseOpacity…, Seek`。
来源：[BMS command memo](https://hitkey.nekokan.dyndns.info/cmdsJP.htm)、[angolmois INTERNALS.md](https://raw.githubusercontent.com/lifthrasiir/angolmois/master/INTERNALS.md)、[bms-rs channel.rs](https://docs.rs/bms-rs/0.9.0/)

#### 已核实的技术约束

| 项 | 事实 |
|---|---|
| **画布** | **固定 256×256**。"If the image or video is larger than 256 by 256 pixels, **only the upper left region will be used**." |
| 图片格式 | BMP（含 RLE）/ PNG / JPEG / GIF |
| 视频格式 | **MPEG-1（`.mpg`）** |
| 透明 | 图片：有 alpha 用 alpha，否则**黑色为透明色**；**"A video does not support a transparent color."** |
| 合成指令 | `#BGAxx yy x y w h dx dy`（图片合成/间接引用） |

#### ⭐ 同步模型（对本项目最关键的一条）

> **"Cue points 是谱面数据（小节/拍），播放是时间驱动的，不是逐音符的。"**
> "It plays while it is 'in display', that is, not replaced by other image or video via data command."

即：**谱面只决定"何时切到哪段 BGA"，播放本身是连续时间轴。** 这与本报告 §6.5 与 §9.1 的结论完全一致 —— **视频层连续播放，谱面只做切换决策**。

**分层播放的代价（已核实）**：多个层共用同一段视频时，它们**共享播放状态**，且"**may restart unexpectedly**"。

#### ⭐ 整个 BGA 家族里唯一真正"玩法反应式"的元素

**POOR BGA（`#xxx06`）—— 玩家 miss 时显示的图。** 除此之外，BGA 全部是时间轴驱动。这是一个很强的信号：**连 IIDX 这样成熟的作品，背景对玩法的反应也只做到"miss 时贴一张图"。**

#### 制作流程完全在游戏外

主视频 → 编码（Expression Encoder 4 / AviUtl / 携帯動画変換君+ffmpeg / TMPGEnc；社区手册称"**mpg 格式是主流但我不推荐…我认为 WMV 最安全**"，并提醒"用 TMPGEnc 时注意不要附带音轨"）→ 放进 BMS 目录 → 写"BGA定義"。专用工具有 **BGAEncAdvance (BGAEncAdv)**。

> **对本项目的启示**：本仓库的 `videoBga` 字段（连续播放 + `timeOffsetSeconds`）**在结构上与 BGA 通道模型同构**。若要向 IIDX 学习，只有一件事值得加：**一个"miss 时显示的 POOR 层"** —— 成本极低，且是 BGA 体系里唯一的玩法反馈通道。
> ⚠️ `remywiki.com/BGA` 是 **404**，RemyWiki 没有 BGA 词条；`#VIDEOFILE`/`#VIDEODLY`/`#SEEKxx`/`#POORBGA`/`#SWBGAxx`/`#CHARFILE` 的正文因抓取截断而未取到 → **未验证**。

### 7.5 Beat Saber（**Unity 引擎，与本项目技术栈最接近**）

Beat Saber 用一份**独立的 `lightshow` 文件**驱动全部环境与灯光。BSMG wiki 的定义：

> "Similar to the beatmap file, the **lightshow** file defines collections and associated metadata for all **non-interactable** beatmap items, such as **environment objects and lighting effects**."
> "Introduced in **1.34.5**. All non-interactable beatmap objects were relocated to this file."
> — [BSMG Wiki — Lightshow](https://bsmg.wiki/mapping/map-format/lightshow.html)

#### 基本事件结构

| 字段 | 含义 |
|---|---|
| **Beat**（`b`） | "A specific point in time, as determined by the **BPM** of the song" → **按拍，不是按秒** |
| **Type**（`et`/`t`） | "**what group of environment objects are affected**" —— 整数，0–19 / 40–43 / 100 |
| **Value**（`i`） | 按 type 决定的效果编号 |
| **Float Value**（`f`） | 精细参数。对 Light Events 而言是 **"control the brightness of the light. A value of 0 will turn the light off."** |

**灯光 Value 语义（官方表，部分）**：

| Value | 结果 | 行为 |
|---|---|---|
| `0` | Off | 关灯 |
| `1`/`2`/`3`/`4` | Static / **Flash** / Fade / Transition（Secondary 色） | — |
| `5`–`8` | 同上四种（Primary 色） | — |
| `9`–`12` | 同上四种（白色） | — |

**即：一个事件 = 拍号 + 目标对象组 + 动作枚举 + 亮度浮点。** 这是一个非常干净的数据结构。

#### v4 新增的表达力（值得注意）

v4 的 lightshow 增加了 `lightRotationEventBoxes` / `lightRotationEvents`（含 `r`（角度，样例 340）、`d`（方向）、`l`）、`lightTranslationEventBoxes` / `lightTranslationEvents`（`t`：位移）、`lightColorEventBoxes` / `lightColorEvents`、`fxEventBoxes` / `floatFxEvents`、`indexFilters`。
→ **环境物体的旋转与位移也是数据驱动的**，且用 `indexFilters` 做筛选。

#### 两个特别有启发的点

| 点 | 说明 |
|---|---|
| **Color Boost** | 独立事件类型，把环境配色**整体切到第二套色板** —— 这就是"段落级换色"的工程化实现 |
| **Waypoints** | 官方描述："Used to control the **TinyTAN figures** that are exclusive to the BTS environment." → **连环境里的角色动画都是按拍事件驱动的** |

#### ⭐ 官方明说的两条引擎边界（非常有教育意义）

> "Each lane has its own unique event blocks… which is determined by what 'lane type' has been set for that lane **at the programming level (Not adjustable by beatmappers)**."
> "**The lighting engine does not allow us to choose a specific colour.** Instead, it pulls from whatever colour scheme the player has set to use."
> — [Beat Saber 官方术语表](https://beatsaber.com/documentation/terminology/index.html)

**读法**：
- **谱师能驱动的，只有程序员预先暴露出来的那些状态。** 环境里有哪些物体、哪个物体属于哪个 Light ID / Group ID，都是开发者写死的。
- **配色是"槽位"而非任意 RGB。** 原版只有 primary / secondary / white 三个槽（任意 RGB 要靠 Chroma mod）。
- 这两条划定了"架构 A"（实时场景暴露参数）的**上限**：**数据驱动的天花板 = 引擎暴露的接口面**。

#### 360°/90° 也是背景事件

官方轨数："90 Degree… **28 lanes total**" / "360 Degree… **96 lanes total**"，编码为**事件类型 14（Early Rotation）/ 15（Late Rotation）**，值 0–7 = 60/45/30/15° 逆时针再到顺时针，相对玩家当前朝向。
→ **连"关卡朝向"都是用背景/灯光事件表达的。**

#### ⚠️ 版本管理教训（可直接用于本项目的 `effectClips[]`）

BSMG 明确警告：事件类型 **14/15 原本是 Custom Platforms mod 的自定义灯光轨**，后来被官方改成 360/90 旋转；**类型 10 在 1.18.0 之前是 BPM 事件**。
> **给第三方预留 ID 空间，永远不要复用已被占用的 ID** —— 否则旧谱面会静默错乱。

**另一个教训**：**Chroma 2.0 "is currently only supported by ChroMapper"**，且 Chroma 1.0 在超过约 2 万个事件后不可用。**数据模型是简单的那一半，编写 UI 才是难的那一半。**

#### Beat Saber 的"没有"

| 项 | 事实 |
|---|---|
| 原版视频背景 | ❌ **没有**，靠 Cinema mod 加（`cinema-video.json`，含 `videoID`/`videoUrl`/`videoFile`/`offset`(ms)） |
| 原版相机震动 | ❌ **没有**。没有相机事件轨；最接近的是 **mod** Noodle Extensions 的 `AssignPlayerToTrack` |
| Chroma / Noodle / Heck | ❌ **都是 mod，不是官方**。Heck 自定义事件：`AnimateTrack`/`AssignPathAnimation`/`InvokeEvent`；Noodle：`AssignTrackParent`/`AssignPlayerToTrack` |

> "Chromafy" 是真实存在的 —— Heck 仓库里有 `LightPairRotationChromafier.cs`、`RingRotationChromafier.cs`、`LightRotationChromafier.cs`、`RingStepChromafier.cs`。
> ⚠️ `AssignObjectPrefab` **未验证**（在 Heck/Noodle/Chroma 常量中未找到）。

**且这一切都是谱师在编辑器里手工编排的** —— BSMG 有完整的 Basic / Intermediate / Advanced / Extended Lighting 教学分级。

> **Beat Saber 的启示（对本项目最直接）**：一个 Unity 做的、商业发行的 VR 节奏游戏，其"背景"完全由**按拍索引的事件表**驱动；事件是 `(beat, targetGroup, action, floatParam)` 的四元组；配色切换（Color Boost）和物体旋转/位移都是同一套机制。
> **本仓库的 `effectClips[]` 只要补上"目标对象组 + 动作枚举 + 浮点参数"，就与这套成熟设计等价。**

### 7.6 其余游戏逐一核实

> **证据分级约定**：`[官方]` = 开发者/官方文档；`[社区]` = 社区文档但权威；`[逆向]` = 社区逆向工程；**未验证** = 无一手来源。

#### Phigros —— `[逆向]`（请大声说明这一点）

| 项 | 事实 |
|---|---|
| **官方谱面格式** | ❌ **不存在公开文档**；**官方编辑器也不存在** |
| 公开格式 | 只有 **RPE / PEC / PBC** —— 全部是社区格式，被社区模拟器 **Phira** 消费。编辑器 **PhiEditor(PE)** 与 **Re:PhiEdit(RPE)** 由社区开发者 @cmdysj 制作 |
| 官方谱面获取方式 | 只能用 **AssetStudio 解包 Unity asset bundle**，无公开 schema |
| RPE 谱面结构 | 根 `BPMList`/`META`/`judgeLineList`；每条判定线有 `Group/Name/father/zOrder`、**`Texture`**（自定义判定线贴图）、`isGif`(v150+)、最多 **5 个 `eventLayers`**、`extended` 分镜层、`notes`、`attachUI`、以及控制曲线 `posControl`/`sizeControl`/`skewControl`/`yControl`/`alphaControl` |
| ⭐ 事件轨（5 条常规） | `moveXEvents`、`moveYEvents`、`rotateEvents`、`alphaEvents`、`speedEvents`（起止拍 + 缓动 1–25 + 贝塞尔） |
| ⭐ 特殊/分镜层（`extended`） | `colorEvents`、`scaleXEvents`、`scaleYEvents`、`textEvents`（含 `%P%` 动态数值）、`gifEvents`(v150+)；`inclineEvents` 已废弃；**`paintEvents` 在 RPE v143 被移除，改为 shader 编辑功能** |
| ⭐ **逐音符视觉控制的形式** | **是"逐音符属性"，不是"逐音符事件"**：`alpha`、`size`、`yOffset`、`visibleTime`、`hitsound`(v142+)、`judgeArea`(v170+)、`tint`/`color`、`isFake`、`above`、`speed`。**没有"事件挂在第 N 个音符上"这个概念** —— 编舞活在判定线级的事件轨上，按时间采样 |
| **背景内部实现** | **未验证** —— 无法可靠判断发行版是视频 / shader / 实时。视频与 shader 只在**社区** Phira/prpr 渲染器里确认，且 phi-chart-render 功能矩阵把 "Shaders / Videos" 标为 **prpr-only** |

来源：[Phira Documents](https://teamflos.github.io/phira-docs/print.html)

#### Arcaea —— `[逆向]` + 一条 `[官方]` 反证

| 项 | 事实 |
|---|---|
| **背景** | **每首歌一张静态 JPG**。`songlist` 字段 `"bg"`，文件在 `\assets\img\bg`；另有逐难度 `bg` 覆盖与 `bg_daynight{day,night}`。**未发现视频背景** |
| 动画元素来自哪里 | **不在背景资源里**，而来自 (a) 谱面相机移动、(b) `scenecontrol(...,storyboard,...)` 叠加资源、(c) 轨道/判定线 UI 效果 |
| 演出素材名 | 逆向规范里出现 **`redline`、`arcahvdistort`、`arcahvdebris`**；文档称原则上可指向自定义资源 |
| `.aff` 格式 | **格式是 lowiro 专有的**（官方谱面在 APK 中未加密分发）；**所有公开文档都是社区逆向**。lowiro 未发布任何规范 |
| ⭐ **无官方编辑器的官方证据** | lowiro 自己的招聘页（"Game Designer (Charter)"）写明谱师 "will work with **in-house chart design tools**"（[lowiro 招聘页](https://lowiro.com/en-us/job-game-designer-charter/)）。lowiro 还曾于 **2019-02-27 对社区编辑器 Arcade 发出 DMCA** |
| 谱面能驱动什么 | `camera(t,x,y,z,xozAng,yozAng,xoyAng,ease,duration)` —— **6 自由度**（lowiro 在 v1.6.1 实装）；`scenecontrol` 变体（`trackstate`、`trackdisplay,mt,alpha`、`storyboard,mt,alpha`、`hidegroup,x,type`、`enwidentype`）；`arc(...,color,hitsound,...)`；`timinggroup` 标志（`noinput` v3.5.3 = 仅显示音符、`fadingholds`、`anglex/angley`） |
| **永远手做的** | 逐曲背景 JPG、曲绘、演出素材本身 |
| 社区编辑器 | **ArcCreate**（Unity，GPL-3.0，支持相机编辑 + Lua scenecontrol）与 **Arcade-plus** |

#### Cytus II（Rayark）

| 项 | 事实 | 分级 |
|---|---|---|
| 引擎 | **Unity 2017.4.5f1，Mono 运行时（非 IL2CPP）** | `[逆向]` |
| 谱面格式 | 见 §7.3a（C2 JSON）；根含 `event_order_list`，扫描线速度 `W`/`R`/`G` | `[社区]`（Cytoid 兼容文档） |
| UI/演出事件 | ✅ 数据驱动（显示/隐藏/淡入/淡出/动画进出，目标为 combo、score、曲名组、难度、扫描线、边界线、频谱、进度条）+ 带颜色的屏上消息 | `[社区]`（编辑器文档） |
| **背景技术** | **未验证**。社区编辑器里的"背景视频"字段**只用于预览/导出视频，不在游戏内播放**。游戏内动画背景是视频 / 序列帧 / 实时 shader —— 无来源可判 | **未验证** |
| 官方编辑器 | **未找到（未验证）**。社区工具 **Cylheim**（`.cyl` 工程，导出 Cytus II JSON，可导入导出 Cytoid） | — |
| Rayark 开发者的背景/工具相关表述 | **未找到任何（未验证）** | — |

#### Muse Dash（PeroPeroGames）—— 难得有 `[官方]` 一手口径

| 项 | 事实 |
|---|---|
| 引擎 | **Unity + Mono**（MelonLoader 的兼容层直接叫 `Muse_Dash_Mono`） |
| **背景** | **一小撮固定的具名场景（10 个）**，按歌曲指派 —— 不是逐曲定制美术，也不是视频。资源名形如 `Scene01Bg.png`…`Scene10Bg.png` |
| ⭐ **视频背景不是原生能力** | 需要 mod 才能加："Cinema is a mod for Muse Dash that **adds background video support** to custom charts"（[MDMods/Cinema](https://github.com/MDMods/Cinema)）。"feber" 层是独立的叠加系统 |
| ⭐ **官方对"什么手做、什么数据驱动"的口径**（2017 GameRes 制作人访谈） | "关卡的难度的因素由 **Map（谱面）**和**音符动画**组成… Map 的制作我们找来了比较专业的**制谱师**来担当，**音符动画则由我们自己来设计和分配**。这些都是苦力活，**必需手动一个一个来**"（[来源](https://www.gameres.com/774573.html)） |
| 官方编辑器 | **未找到（未验证）**。社区管线：**MDMC/CustomAlbums**（`.mdm` 自定义专辑；谱面用 **BMS** 格式在 MDBMSC 里写）与 **MDCP**（闭源，自带 CustomPlay Editor，含关键帧化"故事板"效果轴）。**MDCP 明确非官方**，其中多少是原生能力 **未验证** |

#### DJMAX Respect V（Neowiz / Rocky Studio）—— `[官方补丁说明]`

| 项 | 事实 |
|---|---|
| 引擎 | **Unity** —— 开发者补丁说明原文："(CLIENT) **Unity version has been updated to 6.3**"；"The default graphics API for the **Unity engine** has been changed to prioritize DirectX 11." |
| ⭐ **背景就是预渲染视频，且开发者自己就叫它 BGA** | "(**BGA**) The **BGA sync** for RE;DIEIN has been improved."；"Fixed an issue where **BGAs were not displaying**…"；"The **aspect ratio of BGAs** … **has been changed to 16:9**"；"the BGA for 'Astro Fight' and 'Para-Q' **played the pre-improved flicker effect versions**" |
| 玩家视角 | 在 COLLECTION 里当作 **MUSIC VIDEO**（原由 MAX COMBO 解锁，后放宽为 CLEAR）。"**All tracks within the album share a common album image and BGA.**" → 是**逐曲资源**，不是逐谱面数据 |
| ⭐ **BGA 同步偏移** | **是引擎/资源层数值，由补丁固定 —— 不是谱面数据。** 没有任何证据显示存在谱面驱动、音符反应的背景 |
| 谱面工具 | 有公开的 **DPC Sequencer**，但那是**节奏型（pattern）**音序器，**不是背景工具** |

#### CHUNITHM（SEGA）—— `[官方]`

| 项 | 事实 |
|---|---|
| 背景 = **"舞台"（ステージ）** | X-VERSE 把它变成了**玩家可设置的道具**："楽曲プレイ中に表示される**ステージ**がアイテムとなり、マップ報酬として登場します！"；"**自分の好きなステージ背景を設定して**プレイを楽しむことができます。…**USER BOX もしくは CHUNITHM-NET から変更**"；"ステージ背景を「**設定なし**」にすることで、これまで通り**楽曲に対応したステージ**でプレイすることも可能です"（[官方公告](https://info-chunithm.sega.jp/10024/)） |
| 覆盖被部分曲目禁止 | 联动舞台、UNLOCK CHALLENGE、WORLD'S END 不可覆盖 → 舞台指派是**逐曲/逐分类编写**的 |
| **实时 3D 还是预渲染视频** | **未验证**。推理倾向实时 3D（一个舞台资源能在**任意**符合条件的曲目下使用，且按版本/标签组织而非逐曲 —— 这不是逐曲 BGA 的形态），但无官方声明 |
| **舞台对玩法的反应** | **未验证** |

> ⚠️ **`remywiki.com/BGA` 是 404** —— RemyWiki **没有** BGA 词条。本报告中 BGA 的技术定义依据的是 BMS 侧公开资料（§7.4）。

### 7.7 本节要的教训：哪些该数据驱动，哪些永远手做

#### (a) 先看结构：已发行节奏游戏只有两种背景架构

这个分类比任何单点事实都重要，因为它**决定了"什么能被数据驱动"的上限**。

| 架构 | 代表作品 | **谱面能控制什么** |
|---|---|---|
| **A. 实时场景 + 暴露对象参数** | **Beat Saber、Phigros(RPE)、ADOFAI、osu! storyboard** | 引擎暴露的一切：逐拍灯光状态、亮度、色槽、激光速度、圆环旋转、逐段颜色/旋转/位移扇出、预定义物体动画、相机变换、生成朝向、逐音符视觉属性、粒子、滤镜、文字 |
| **B. 预渲染视频背景（BGA）** | **beatmania IIDX、BMS、DJMAX Respect V** | **只有**：哪一段视频/静帧在哪个拍上屏、放在哪一层、以及（BMS）一个同步/seek 偏移。**视频内部的时间是制作期固定的** |

**Beat Saber 官方文档从内部证明了这个边界**：

> "Each lane has its own unique event blocks… which is determined by what 'lane type' has been set for that lane **at the programming level (Not adjustable by beatmappers)**."
> "**The lighting engine does not allow us to choose a specific colour.** Instead, it pulls from whatever colour scheme the player has set to use."
> — [Beat Saber 官方术语表](https://beatsaber.com/documentation/terminology/index.html)

> **对本项目的直接含义**：本仓库当前是**架构 B**（预渲染 `videoBga`）。如果想让谱师真正"用谱面驱动背景"，必须往**架构 A** 走一部分 —— 即让实时层暴露**具名对象组 + 可数据驱动的状态参数**。想同时保留 AI 生成的画面质感，就是 §9.1 的三层混合：**视频当底（B）+ 实时层暴露参数（A）**。

#### (b) 背景元素 × 是否数据驱动 × 证据

| # | 背景元素 | 数据驱动？ | 证据 |
|---|---|---|---|
| 1 | **基础美术 / 场馆 3D 模型 / 贴图 / 灯架布局** | ❌ **永远手做**。环境是一份**固定的具名清单**；存在哪些物体、它们的 Light ID / Group ID 映射由开发者编写 | Beat Saber `_environmentName` 从约 25 个具名环境中选一个 |
| 2 | 某首歌用哪个环境 / 舞台 | ✅ **但只是一个数据字段，不是事件** | Beat Saber `_environmentName`；CHUNITHM 舞台按曲目/分类指派且玩家可覆盖 |
| 3 | **BGA / MV 视频内容本身** | ❌ **永远手做**，在游戏外制作 | DJMAX 把 BGA 当成品视频流播放、补丁里统一重导出 16:9；BMS 作者在 AviUtl/TMPGEnc 里编码母版再"BGA定義" |
| 4 | **背景内部的动画方向 / 运镜** | ❌ **永远手做**。BS 里谱师只选**触发哪个预定义 FX**，从不决定其内容；BGA 里它被烘进渲染结果 | BS GLS 文档称 FloatFX 为 "predefined object animations"；BGA 制作教程自我界定为"不教怎么做视频本身" |
| 5 | 场馆配色 / boost | ✅ `_envColorLeft/_envColorRight` + boost 对 + `colorBoostBeatmapEvents` | BSMG 灯光文档 |
| 6 | **逐拍灯光 开/关/闪/淡/过渡 + 亮度** | ✅ **灯光秀的核心**。值 0–12，浮点亮度 | BSMG lightshow |
| 7 | 激光旋转速度 | ✅ 值通道 0–9（v2）/ 旋转量+缓动+循环数（v3） | BS 官方灯光文档 |
| 8 | 圆环旋转 / 缩放 | ✅ Trigger 事件 | BSMG |
| 9 | **逐段扇出 / 渐变** | ✅ **v3+**：event box × index filter × wave/step 分配 × 缓动 | BS 官方 GLS 文档 |
| 10 | 环境物体**成组旋转** | ✅ v3+（轴 X/Y/Z、顺逆时针、循环数） | 同上 |
| 11 | 环境物体**成组位移** | ✅ v3.2+ | BSMG |
| 12 | 预定义物体动画（碎裂、缩放、光罩） | ✅ v3.3+/v4：FX event box + float FX | BSMG |
| 13 | **音频频谱可视化元素** | ⚠️ ✅ **但是引擎侧，不是谱师侧**。环境标记 `Spectrum: ✅`，**谱师只是点亮 `Spectrograms` 对象，音频→视觉的映射是引擎代码** | BSMG basic/intermediate-lighting |
| 14 | 关卡朝向旋转（360°/90°） | ✅ **以背景/灯光事件形式编写**（类型 14/15）；90° = 28 轨，360° = 96 轨 | BSMG；BS 官方术语表 |
| 15 | **重拍相机震动** | ❌ **原版 Beat Saber 没有**。无相机事件轨；旋转事件转的是**生成点**、玩家自己转身。最接近的是 **mod** `AssignPlayerToTrack` | BSMG；Aeroluna/Heck Noodle 源码 |
| 16 | 任意 RGB / 逐灯颜色 / 渐变 | ❌ 原版只有 3 个色槽 → ✅ 靠 **Chroma mod** | Aeroluna/Heck Chroma 源码 |
| 17 | 隐藏/移动/缩放/克隆任意环境物体 | ❌ 原版 → ✅ Chroma `_environment` | 同上 |
| 18 | 用命名 track 动画化任意物体 | ❌ 原版 → ✅ Heck（`AnimateTrack`/`AssignPathAnimation`/`InvokeEvent`） | Heck 源码 |
| 19 | 物体挂到 track 上 | ❌ 原版 → ✅ Noodle `AssignTrackParent` | Noodle 源码 |
| 20 | **逐曲视频背景** | ❌ 原版 BS → ✅ Cinema mod；**DJMAX 原生（BGA 就是背景）**；**ADOFAI 原生（`bgVideo`）**；osu! 原生（仅 H.264/.mp4） | 各自官方来源 |
| 21 | 哪个 BGA/图在哪个拍上屏（BMS） | ✅ 通道 04/06/07/0A；`#BGAxx yy x y w h dx dy` 组合 `#BMPxx` | BMS command memo |
| 22 | **BGA 片段内部的播放时序** | ❌ 只要"in display"就跑自己的时钟 | angolmois INTERNALS.md |
| 23 | ⭐ **miss 反应式背景（POOR BGA）** | ✅ **真正玩法驱动**：通道 `06` / `#POORBGA` 在 miss 时显示指定图。**BGA 家族里最强的"玩法→背景"耦合** | BMS command memo |
| 24 | BGA 同步/seek/淡入/颜色头 | **未验证**（`#VIDEOFILE`、`#VIDEODLY`、`#SEEKxx` 等仅有目录，正文未取到） | — |
| 25 | **DJMAX BGA 同步偏移** | ❌ **不是谱面数据** —— 由开发者在补丁里固定 | DJMAX 补丁说明 |
| 26 | 解锁隐藏 BGA | ✅ **但是进度数据，不是谱面事件** | 同上 |
| 27 | osu! 按 hitsound 触发 | ✅ `HitSound[bank][bank][addition][suffix]` | osu! + 客户端源码 |
| 28 | osu! 按"任意命中"触发 | ✅ `HitObjectHit`（lazer） | 同上 |
| 29 | osu! 按**某一个指定音符**触发 | ❌ **无音符 id/索引**，只能收窄 `[starttime,endtime]` | 同上 |
| 30 | osu! 通过/失败反应 | ✅ `Passing`/`Failing` + Fail/Pass 层 | osu! |
| 31 | ADOFAI 在特定地砖/子砖触发 | ✅ `floor`（地砖索引）+ `angleOffset`（度，"0º = on hit"） | 真实 `.adofai` 文件 |
| 32 | ⭐ **ADOFAI 逐判定触发** | ✅ `Set Conditional Events`：Loss/Too Early/Too Late/Early/Late/Perfect/EPerfect/LPerfect | ADOFAI 事件文档 |
| 33 | ADOFAI 震屏/闪光/滤镜/Bloom/粒子 | ✅ 官方数据驱动事件 | 同上 |
| 34 | Phigros 判定线运动 | ✅（moveX/moveY/rotate/alpha/speed，缓动 1–25 + 贝塞尔）**但格式为社区 RPE，非官方** | Phira 文档 |
| 35 | Phigros 逐音符视觉变化 | ✅ **但形式是"逐音符属性"**（alpha/tint/size/yOffset/visibleTime/hitsound/judgeArea），**不是"逐音符事件"** | 同上 |
| 36 | **Phigros 背景内部实现** | **未验证**（视频/Shader 只在社区 Phira/prpr 渲染器里确认） | — |
| 37 | Arcaea 相机/场景/轨道加宽 | ✅ `camera(...)` **6 自由度**（lowiro v1.6.1 实装）、`scenecontrol(...)`、`timinggroup` 标志。命令是官方的，**文档是社区逆向** | Arcaea `.aff` 社区规范 |
| 38 | **Arcaea 背景图 / 曲绘 / 演出素材** | ❌ **手做**；某首歌用哪张 bg 来自 `songlist` 数据，**不来自谱面** | 同上 |
| 39 | Cytus II UI/演出事件、扫描线速度（W/R/G） | ✅ 但来源是**社区编辑器**的格式文档 | Cytoid wiki |
| 40 | **Cytus II 背景技术 / 逐音符爆发 / 震屏** | **未验证** | — |
| 41 | **Muse Dash — 官方口径** | ❌ **谱面只定音符与难度**；官方访谈："音符动画则由我们自己来设计和分配…这些都是苦力活，**必需手动一个一个来**" | 2017 GameRes 制作人访谈（**官方**） |
| 42 | Muse Dash — 社区 MDCP 管线 | ✅ 关键帧化效果轴：场景切换/背景亮度/Bloom/暗角/饱和度/音符可见性/时间重映射/场景变速 + Boss 轨 | MDCP 编辑器文档（**非官方**） |
| 43 | CHUNITHM 舞台按玩法反应 | **未验证** | — |
| 44 | osu! 触发器精灵的生命周期 | ✅ 但 ranking criteria 要求："**Fade out sprites activated from triggers after usage.** Triggers will activate from their first possible command and **stay active until the end of the difficulty**" | osu! Ranking criteria |

#### (c) 证据支持的经验法则

1. **数据驱动"状态"，手做"内容"。** Beat Saber 提供物体并暴露其状态，谱师写状态时间轴；DJMAX 提供成品视频，谱师什么都不写。**单位美术人力换来的作者表达力，前者高得多。**
2. **凡是"必须落在拍上"的，都该数据驱动** —— 灯态、亮度、激光速度、圆环旋转、旋转、位移、换色、生成朝向、相机变换、震屏、闪光、滤镜。
3. **三个杠杆率最高的原语，按顺序**：(a) **具名对象组上的逐拍数值事件**；(b) **index filter + wave/step 分配** —— 把"N 盏灯"变成一次作者手势，**是 BS v3 里最大的省力器**；(c) **带缓动的成组旋转/位移**，把灯光变成编舞。
4. **永远不要指望谱师做基础美术、动画方向或视频内容。** 每一个已发行案例都把这三样留在开发者/美术手里。
5. ⭐ **玩法反应式背景罕见且浅。** 本次调研中**唯一**确证的案例是 BMS/IIDX 的 **POOR BGA**（miss 时贴一张图），加上 osu! 的 hitsound/`HitObjectHit`/`Passing`/`Failing` 触发与 ADOFAI 的逐判定条件事件。**DJMAX 的"反应"只是解锁门控。已发行商业作品里"丰富的逐音符爆发背景"未能核实。**
6. ⭐ **逐音符背景同步是例外，不是常态。** 在 Beat Saber 里，音符同步来自**谱师主动把灯光事件放在音符上**；**引擎并不把音符和灯光耦合起来**。这一条直接反驳了"音频/音符自动驱动背景"的直觉。
7. **给数据模型做版本管理并预留 ID 空间。** Beat Saber 的 14/15 号类型**原本是 Custom Platforms mod 的自定义灯光轨**，后来被改成 360/90 旋转；10 号类型在 1.18.0 前是 BPM 事件。**复用第三方占用的 ID 会破坏旧谱面。**
8. ⭐ **数据驱动的背景，上限取决于它的编写 UI。** Chroma 2.0 **只能靠 ChroMapper**；Chroma 1.0 在超过约 2 万个事件后不可用；MMA2 会剥掉 Chroma 2.0 并把 14 号类型转成 BPM 变化。**数据模型是简单的那一半，编写 UI 才是难的那一半。**
9. **osu! 是最佳模板**，因为它的实现完全可见：**解析一次 → 类型化命令对象 → 编译成框架的 transform sequence**；循环编译成 `.Loop()`；**触发器是"带 `[start,end]` 有效窗口的事件订阅"，不是时间轴条目**；图层用具名整数深度；生命周期预先算好。**这套架构可以干净地映射到 Unity**（Timeline/Playables，或等价的 TransformSequence）。

#### (d) 归纳

> **在已发行的节奏游戏里，"素材"永远是手做的，"素材在什么时刻做什么"永远是数据驱动的。**
> 数据驱动的最佳粒度是 **"具名对象组 × 某时刻 × 某状态/变换"** 与 **"某个具体音符/判定 → 播一次性效果"**，
> **而不是**"让音频分析自己决定画什么"。
>
> 并且：**没有任何一款已发行节奏游戏用 FFT 决定背景内容**；音频频谱可视化在 Beat Saber 里甚至是**引擎侧写死的**，谱师只能点亮那个已存在的频谱对象。

第 3 点与本报告 §1.2 的官方免责声明（`GetSpectrumData` 不适合低延迟关键分析）**互为印证**：工业界的做法是**用谱面表达意图，而不是用音频分析猜测意图**。

⭐ **最后一击**：Cytoid 的官方 storyboard 规范独立推荐 **"standard H.264 `.mp4` file at maximum 720p"**；osu! 的 ranking criteria 独立规定 **"A video must be encoded in H.264"**、上限 **1280×720**、且**必须移除音轨**。两个独立的已发行跨平台音游，其官方背景方案与本仓库现有的 **H.264 + DSP 时钟 + 去掉音轨** 约定**完全一致**。这比任何推理都更有力。

---

## 8. 本仓库现状盘点（推荐路径的输入）

| 已有能力 | 位置 | 对实时 BGA 的意义 |
|---|---|---|
| **DSP 锚定时钟** | `Assets/RhythmDemo/Runtime/SongClock.cs` | 用 `AudioSettings.dspTime` + `AudioSource.PlayScheduled`，0.12s 排程提前量 + `outputDelay` 校准。**已经是样本精确时钟**，不需要 FFT 来做同步 |
| **BPM 网格** | `chart.json` → `ticksPerBeat: 480`、`tempos[]` | 权威节拍来源 |
| **段落标记** | `chart.json` → `sections[].startBeat` / `.name` | **天然的"数据驱动背景切换"钩子**（对应 osu! 的 section 概念） |
| **效果片段轨道** | `chart.json` → `effectClips[]`，C# 侧 `EffectClipData { startTick, durationTicks, name }`，编辑器 `TimelineKind.Effect` 泳道 + 「+ Effect clip」按钮 | **逐段落/逐时刻视觉事件的现成载体**，已可在编辑器里拖拽摆放 |
| **相机运动片段** | `chart.json` → `cameraMotionClips[]`（含 `keys[]`） | 编舞式镜头 |
| **BGA 视频层** | `chart.json` → `videoBga { packageManifest, timeOffsetSeconds, enabled }` | 预渲染层，DSP 时钟对齐 |
| **渲染管线** | BiRP（无 URP） | **排除 VFX Graph 与 Shader Graph** |

> 结论：这个项目**不需要**"音频分析驱动背景"作为主线。它需要的是把已有的 `sections` / `effectClips` 接到一个实时视觉层上；FFT 只作为该视觉层的**装饰性调制输入**。

---

## 9. 推荐路径

### 9.1 总体架构：三层，各司其职

```
┌─ 权威层（样本精确，来自谱面 + DSP 时钟） ────────────────┐
│  SongClock.Time  →  songTime (秒)                        │
│  tempos[]        →  节拍相位                              │
│  sections[]      →  段落/调色/强度切换                    │
│  notes[]         →  逐音符爆发事件                        │
│  effectClips[]   →  编辑器可摆放的视觉事件                │
└──────────────────────────────────────────────────────────┘
              ↓ 结构化参数（Material/Shader/粒子）
┌─ 实时层（BiRP 手写 Shader + 粒子，Windows/Android 同构） ─┐
│  背景 Shader：频谱纹理 + 段落参数 + 节拍脉冲               │
│  逐音符爆发：对象池粒子 / 一次性特效                       │
│  后处理：按段落切换的调色/泛光                             │
└──────────────────────────────────────────────────────────┘
              ↓ 叠加 / 混合
┌─ 预渲染层（ComfyUI/Wan 视频，DSP 时钟对齐） ──────────────┐
│  现有 videoBga 层，短 GOP H.264，AudioDSP timeUpdateMode  │
└──────────────────────────────────────────────────────────┘
              ↑
┌─ 装饰层（唯一用 FFT 的地方） ────────────────────────────┐
│  整体能量 / 低频占比 → 轻度呼吸、发光强度、粒子扰动          │
│  20–50ms 延迟可接受，且绝不用它做触发                       │
└──────────────────────────────────────────────────────────┘
```

**核心判断：把"同步"和"氛围"彻底分开。**
- 一切需要**看起来卡在拍上**的东西，走谱面 + `dspTime`。这条路径是样本精确的，且已经在仓库里跑通了。
- 一切**看起来是连续的、有生命力的**东西，走 FFT。它的延迟不可控，但连续性掩盖了延迟。
- **视频层永远连续播放，绝不逐音符 seek。** 逐音符的视觉反馈由实时层（粒子/Shader）承担，视频只提供"底"。这条不是偏好，是官方文档对 seek 成本与排队行为的直接结论（见 §6.4b）。

### 9.2 哪些部分该做成数据驱动，哪些永远手做

| 背景元素 | 数据驱动？ | 依据 |
|---|---|---|
| 基础画面/美术（构图、材质、光照基调） | ❌ 永远手做 | §7.6 |
| 段落级切换（副歌变色、BPM 变化换风格） | ✅ 谱面 `sections[]` | 仓库已有该字段 |
| 逐音符爆发、命中反馈 | ✅ 谱面 `notes[]` + `songTime` | 判定系统已有时间戳；**Cytoid 的 `noteClear` 触发器证明了这是业界做法**（§7.3b） |
| 节拍脉冲（每拍/每小节缩放、位移） | ✅ `tempos[]` + 节拍相位 | 不需要 FFT |
| 相机运动/编舞 | ✅ `cameraMotionClips[]` | 仓库已有该轨道 |
| 长镜头运镜、AI 生成的镜头内运动 | ❌ 预渲染视频 | 现有 `videoBga` 层 |
| 频谱呼吸、发光强度微动 | ✅ 但走 FFT，且仅作装饰 | §1.2 官方低延迟免责声明 |
| 歌词/文字排版动画 | ⚠️ 半数据驱动（时间戳驱动，排版手做） | — |

⭐ **值得抄的两条具体设计**：

**（一）触发器可锚定到音符（来自 Cytoid，§7.3b）** —— 把 `effectClips[]` 的触发源从"绝对 tick"扩展为**可锚定到音符 ID**，即同时支持
`{ "tick": 1234 }`、`{ "note": "<noteId>", "at": "start" }`、`{ "note": "<noteId>", "at": "clear" }`。
这样谱师在编辑器里摆的效果片段既能对齐节拍，也能直接"挂"在某个音符上随谱面移动而移动。

**（二）事件 = `(beat, targetGroup, action, floatParam)` 四元组（来自 Beat Saber，§7.5）** —— 本仓库 `effectClips[]` 现在更接近"在某时刻放一个特效预设"；补上**目标对象组**与**浮点参数**后，就能用同一套机制表达灯光组、环境物体旋转/位移、整体换色，而**不需要为每种效果新增一种数据结构**。Beat Saber 正是用这一个四元组覆盖了灯光、旋转、位移、换色四类需求。

**这两条都是低成本、高表现力的改动，且都有已发行商业游戏作为先例。**

### 9.3 面向 Android 的务实取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 渲染管线 | **留在 BiRP**，手写 Shader | VFX Graph / Shader Graph 需要 SRP；迁移成本高，收益不匹配 |
| 背景主画面 | **保留预渲染视频** | 视频是 Android 上最省电、最可预测的"复杂画面"载体；H.264 硬解是最优路径 |
| 实时层分辨率 | 低分辨率 RenderTexture（如 1/2 或 1/4）+ 上采样 | 频谱类效果本身模糊，降分辨率几乎无感，移动端收益巨大 **（工程建议，未验证具体数值）** |
| 音频分析 | `OnAudioFilterRead` + 自己的分带，或 LASP；**不用 `GetSpectrumData` 做触发** | §1.2 |
| FFT 尺寸 | ≤512（Android），仅做能量/低频估计 | §1.1 上限 8192，但移动端不必 |
| 频谱上传 | 小纹理 128×1 RFloat 或 `GraphicsBuffer`，`SetGlobalTexture/Buffer` | 避免 GC 与逐材质开销 |
| 视频结构 | 单长视频 + 短 GOP + DSP 时钟；必要时 5 帧交叉溶解接缝 | §6.5 |
| 时钟 | 一律用秒做权威单位，`outputSampleRate` 每帧现读 | §1.4 Android 采样率可变 |
| 逐音符效果 | 对象池 + 预算上限（同时活跃特效数硬上限） | 移动端稳定性 |

### 9.4 建议的实施顺序

| 阶段 | 内容 | 验证方式 |
|---|---|---|
| 1 | 把 `sections[]` 接到 Shader 全局参数（色调/强度），零 FFT | 段落边界与听感一致 |
| 2 | 把 `effectClips[]` 接到对象池特效，编辑器可视化摆放 | 编辑器拖动 → 运行期一致 |
| 3 | ⭐ 让 `effectClips[]` 支持**锚定到音符 ID**（Cytoid 式 `start`/`end`/`at`/`clear`） | 音符移动时效果跟随 |
| 4 | 加一条 `OnAudioFilterRead` → 能量/低频 float → Shader（**仅装饰**） | 关闭后观感差异应很小 |
| 5 | 低分辨率实时层与视频层混合调参 | Android 实机帧率与温度 |
| 6 | （可选）评估 URP 迁移，若迁移则解锁 VFX Graph 的 `Audio Spectrum to AttributeMap` | 迁移成本对照 |

> **顺序理由**：阶段 1–3 全部走"谱面 + DSP 时钟"，**零风险、可预测、Windows/Android 同构**，且直接产出可交付的视觉提升。阶段 4 才引入唯一的不可控项（FFT 延迟）。**不要把这个顺序倒过来。**

---

## 10. 未验证项汇总

**凡本报告正文中标注「未验证」的内容，在此汇总，便于后续单独核实。**

### 10.1 音频与延迟

| 项 | 状态 |
|---|---|
| `OnAudioFilterRead` 在 Android 上的实际行为与回调周期 | **未验证**（官方仅排除 Web 平台） |
| Android 上音频输出的绝对延迟数值 | **未验证**（官方无公开承诺） |
| Android "Best Latency" DSP 档位的稳定性 | **未验证**（Issue Tracker 有爆音个例） |
| Unity 是否有内置 beat/onset 检测 | **API 层面为"无"**（`AudioSettings` 全部成员已列，无此类方法） |

### 10.2 视频

| 项 | 状态 |
|---|---|
| `VideoPlayer` seek 的耗时数值 | **未验证**（官方只说 "may be noticeably long"） |
| H.264 短 GOP 提升 seek 响应 —— 因果链 | **推断**（基于 I 帧随机访问原理，非官方声明） |
| Android 同时可用硬解实例数量上限 | **未验证** |
| Cytoid `videos`（实验性）在移动端的实际表现 | **未验证** |
| **VideoPlayer 音频是否推进 `AudioSettings.dspTime`**（尤其 `Direct` 模式） | **未验证**（无官方说明） |
| Unity 是否使用 MediaCodec / ExoPlayer 解码 Android 视频 | **未验证**（官方文档、博客、发布说明均未提及；勿断言） |
| Unity 是否支持 HAP | ❌ **官方文档完全未提及** → 视为不支持（`KlakHap` 是第三方插件） |
| Unity 是否支持 AV1 | ❌ **官方视频文档完全未提及** → 视为不支持（负面结论） |
| Unity 是否有官方 GOP / all-intra 编码建议 | ❌ **无**（官方"Key encoding values"表不含此参数） |
| "静音视频 + 独立 AudioSource" 是否为官方推荐做法 | ❌ **未验证**：官方只文档化能力，未给处方（本报告的建议属工程推理） |
| `timeUpdateMode = DSPTime` + `timeReference = ExternalTime` 组合的实际效果 | **未验证**（未实测） |
| Android 上 2560×1440 视频在低端机的实际播放成功率 | **未验证**（官方历史文档称 >640×360 并非所有设备支持，失败即不播放） |

### 10.3 生态与包

| 项 | 状态 |
|---|---|
| `Klak` 的 License 逐字核对 | 已核实为 **MIT**（API `spdx_id=mit`） |
| `KlakHap` 的 Unity 版本要求 | ✅ **已核实：Unity 2022.3+** |
| `KlakSyphon` 的 License 文本 | **未验证**（NOASSERTION） |
| `KlakNDI` 自身代码（非 NDI 库）的 License | **未验证**（仓库 LICENSE 只指向 `libndi_licenses.txt`） |
| `Reaktion` / `MidiJack` 的法律许可效力 | ⚠️ **无 LICENSE 文件**，MIT 文本仅在 README |
| `AudioSpectrumTracker` / `UnityAudioVisualizer` / `Rcam` / `VfxGraphTest` / `ffmpegOut` 仓库是否存在 | **未验证**（仅 `jp.keijiro.ffmpeg-out` 1.0.5 经 npm 确认） |
| OSC 方案的端到端延迟（`osc-jack` / 官方 OSC / extOSC） | **未验证**（无任何官方数字） |
| Spout / NDI 在 Unity 内的端到端延迟 | **未验证** |
| NDI 在 Android 上的实际延迟与稳定性 | **未验证**（README 只声明支持） |
| `Lasp` 的 `loopback` 分支的能力与维护状态 | **未验证** |
| `Lasp` 是否能分析 Unity 自身播放的歌曲（而非输入设备） | **未验证**（README 全部围绕 input devices） |

### 10.4 端上推理

| 项 | 状态 |
|---|---|
| Sentis 任何具体 FPS / ms / 模型体积数字 | **未验证**（官方完全无 benchmark） |
| `DepthEstimationSample` 的分辨率 | **未验证**（README 未写） |
| Sentis 2.6 是否仍要求 Android 用 Vulkan（禁 OpenGL ES） | **未验证**（当前文档沉默，依据来自前身 Barracuda） |
| sentis-samples 历史上是否存在 StyleTransfer 样例 | **未验证**（archive.org / api.github.com 不可达） |
| `com.unity.ai.inference` **包本身**的 License 条款 | **未验证**（文档未声明；samples 仓库的 "Experimental/Evaluation" 文本不覆盖包） |
| StreamDiffusion 的最低显卡 / 显存要求 | **未验证**（README 未给，仅一张 RTX 4090 表） |
| StreamDiffusion 笔记本 RTX 3060 可行性 | **未验证** |
| StreamDiffusion 端到端延迟（生成+传输+上屏） | **未验证** |
| StreamDiffusion 的 AMD/ROCm 支持 | **未验证**（README 从未提及，实测为 CUDA-only） |
| `StreamDiffusionTD` 的 License 与归档状态 | **未验证**（GitHub 仓库 404） |

### 10.5 先例调研

| 项 | 状态 |
|---|---|
| **Phigros 官方谱面格式** | **未验证** —— 官方无公开格式、无官方编辑器；公开的 RPE/PEC/PBC 均为社区格式 |
| **Phigros 发行版背景渲染实现** | **未验证** —— 视频/shader 只在社区 Phira/prpr 渲染器确认；Igallta/Spasmodic/Rrhar'il 的特殊背景是视频/Spine/谱面驱动 **未验证** |
| Pigeon Games 关于制谱/背景的任何官方表述 | **未验证** —— `pigeongames.cn` 域名不可达，**完全未取得** |
| **Arcaea 背景技术** | ✅ **静态 JPG（已验证）**。但"Arcaea 完全不用视频"的绝对断言 **未验证**（songlist/aff 文档中未引用视频资源） |
| Arcaea 官方对 `.aff` 的确认 | **未验证** —— 格式为 lowiro 专有，文档全部为社区逆向。**但"无官方编辑器"有官方证据**（招聘页 + DMCA） |
| **Muse Dash 场景是否用 Spine/DragonBones 还是序列帧** | **未验证** |
| Muse Dash MDCP 的效果轴是原生能力还是 mod 实现 | **未验证**（MDCP 明确非官方） |
| **Cytus II 游戏内动画背景的实现技术** | **未验证** |
| Rayark 关于视觉表现的 GDC 演讲或开发博客 | **未验证**（未找到） |
| **BMS BGA 的 `#VIDEOFILE`/`#VIDEOf/s`/`#VIDEOCOLORS`/`#VIDEODLY`/`#MOVIE`/`#SEEKxx`/`#POORBGA`/`#SWBGAxx`/`#CHARFILE`/LAYER 正文** | **未验证**（仅确认目录存在，正文因抓取截断未取到） |
| **`remywiki.com/BGA`** | ❌ **404 —— RemyWiki 没有 BGA 词条** |
| **CHUNITHM 舞台是实时 3D 还是预渲染视频** | **未验证**（推理倾向实时 3D，但无官方声明） |
| CHUNITHM 舞台对玩法的反应、SEGA 内部制作工具 | **未验证** |
| **DJMAX BGA 同步偏移是否可为谱面数据** | ✅ **已验证为"否"** —— 由开发者在补丁里固定。"隐藏 BGA 的识别方式"与"DPC Sequencer 是否触及 BGA" **未验证** |
| **Beat Saber `lightshow` 的官方（Beat Games）文档** | ⚠️ **半验证**：JSON 字段细节来自 **BSMG wiki**（社区权威，非官方）；但**官方文档确实存在**于 `beatsaber.com/documentation/`（术语表、Static Event System、Group Lighting Event System） |
| Beat Saber `AssignObjectPrefab` 是否为真实 API | **未验证**（Heck/Noodle/Chroma 常量中未找到） |
| **"Sabik" 分镜编辑器** | ❌ **官方 wiki 中查无此物**（Storyboard / Scripting / Design tab 及其 Community tools 均无提及），无 osu! 相关一手来源 |
| **SGL（Storyboard Graphing Language）** | ⚠️ **半验证**：确为 MoonShade 开发、Damnae fork 维护的脚本语言，但**仅见于社区论坛帖**，不在官方 wiki；**缩写展开未验证** |
| osu! `HitObjectHit` 触发器的 wiki 收录状态 | ❌ 截至调研时**未被官方 wiki 收录**（仅存在于 lazer 源码与已合并 PR #38335） |
| osu! stable 是否曾正式支持 `.avi`/`.flv` 视频 | **未验证**（lazer 客户端可解码，但官方文档只支持 `.mp4`） |
| osu! wiki 对"视频位于哪一层"的表述 | ❌ **官方 wiki 无此表述**，仅能从客户端源码推得 |
| ADOFAI `angleData` 的 midspin/非 90° 哨兵值、`angleOffset` 数值范围 | **未验证** |
| **ADOFAI `OnHit` / `OnLand` / `OnFalling`、`AddObject` / `AddParticle`** | ❌ **无证据表明存在 —— 不要引用** |
| bmson 规范的 BGA 相关字段 | **未验证** |

**因访问受限而仅能作为"存在性"证据引用的来源（内容全部 未验证）**：`beatsaberquest.com`、`github.com/BeatSaber/BeatSaber-JSON`、`heck.aeroluna.dev`（CF 403）、`web.archive.org`、`en.wikipedia.org`、`djmax.fandom.com`、`chunithm.fandom.com`、`arcaea.fandom.com`、`musedash.fandom.com`、`namu.wiki`、`wikiwiki.jp`、`moegirl.icu`、`blog.drwf.ink`、`rayark.com`、`pigeongames.cn`（DNS ENOTFOUND）、`adofai.fandom.com`、`steamdb.info`、`bilibili.com/opus/*`（验证码）。

### 10.6 工程建议（非事实声明）

| 项 | 状态 |
|---|---|
| 低分辨率实时层在具体机型上的收益数值 | **未验证**（§9.3 中的建议属工程判断） |
| Android 视频解码器实例上限 | **未验证** |

---

## 11. 本报告的方法与可达性说明

| 来源类型 | 可达性 | 说明 |
|---|---|---|
| `docs.unity3d.com`（官方手册/脚本 API/包文档） | ✅ 稳定 | 本报告主要依据 |
| `cdn.jsdelivr.net/npm/<pkg>/README.md` | ✅ 稳定 | **重要技巧**：npm 包的 README 可经 jsDelivr 直接以 Markdown 取到，是核实 Unity 第三方包（版本/平台/License）最可靠的一手途径 |
| `raw.githubusercontent.com`（osu-wiki、sentis-samples 等） | ⚠️ 时通时断 | 成功取到 osu! storyboard 规范 |
| `api.github.com` | ❌ 大量 `403` / `fetch failed` | 仓库元数据改由 npm registry + Atom feed 核实 |
| `github.com` HTML 页面 | ❌ 大量 `fetch failed` | — |
| `web.archive.org`、`huggingface.co` | ❌ 不可达 | — |
| PowerShell 直接发起网络请求 | ❌ 无出口（超时） | 所有网络访问均经 web 工具 |

**因此**：凡依赖 GitHub 仓库页面的项（如 `Reaktion` 的分支列表、`KlakHap` 的版本要求），本报告已尽可能改用 npm registry 与 jsDelivr 交叉核实；**仍无法核实的，一律标注「未验证」而非猜测。**
