# 视频 BGA 制作与一键导入

## 谱师最短流程

1. 用任何工具做好视频（AI、Blender、剪辑软件均可），最好把歌曲一起导出。
2. 制谱器在 **Files → Import / replace video**（或 BGA 页）选择视频。程序后台转码、提取音轨、验证解码、挂到 BGA 层。
3. 视频没带歌曲时，点击 **Import / replace song** 选择 WAV/OGG/MP3。
4. 在 Stage / Paths / Notes 进行3D制谱；工作中可保存 JSON，交付时用 **Export .grchart package** 生成单文件。

无须安装 Blender、ComfyUI、Python，也无须制作 shader 或模型。Windows 版所需 FFmpeg 在程序旁 `BGA/Tools/ffmpeg.exe`。源视频不被修改。导入中按钮禁用；失败保留原 BGA；导入不改谱面的 BPM、路径或 Note。

## 稳定交付格式

对经常发布 BGA 的作者，推荐直接交付一个目录：

```text
SummerBga/
  manifest.json
  video.mp4
  audio.wav
```

`manifest.json`：

```json
{
  "schemaVersion": 1,
  "kind": "video-bga",
  "title": "Summer BGA",
  "video": "video.mp4",
  "audio": "audio.wav",
  "durationSeconds": 45,
  "fps": 30,
  "width": 1280,
  "height": 720
}
```

同一个 Import Video 按钮选择这个 manifest 就能导入，无须再次转码。`video` / `audio` 只能指向包内相对路径；不允许越出目录。没有音轨就省略 `audio` 或写空字符串。真实时长、尺寸与帧率以视频解码器为准。

推荐视频：H.264 High、yuv420p、固定 60fps、无 B 帧、每 15 帧一个关键帧、BT.709、MP4 faststart。短 GOP 会增加文件大小，但能提高来回拖动的响应。音频推荐 48kHz 双声道 PCM WAV。程序能转换其他常见视频，不要求谱师手工输入参数。

## `.grchart` 谱面包

`.grchart` 是 ZIP 容器，但扩展名固定为 `.grchart`。制谱器会先校验路径和描述文件，再解包到本机缓存；包损坏时保留当前谱面。标准内容：

```text
package.json                         # 包版本、标题、谱面入口
chart.json                           # BPM、3D Stage、Paths、Notes、偏移
chart.assets/BGA/<资源标识>/
  manifest.json                      # 视频尺寸、帧率、时长与哈希
  video.mp4                          # 已完成的 BGA
  audio.wav                          # 制谱/游玩的音轨
```

若歌曲就是视频内的音轨，`chart.json` 会直接复用 BGA 目录里的 `audio.wav`，不会再复制一份。若另行导入歌曲，才会增加 `chart.assets/Audio/<资源标识>.wav`。包内不需要白模、ComfyUI 工作流、Blender 工程或AI提示词；这些属于制作源文件，不是铺面运行依赖。

本次可直接制谱的包是 `BGA/ChartPackages/Summer20_H3_20s.grchart`，内容为2560×1440、约60fps、19.98秒成片，附三条空白3D路径。重建命令：

```powershell
powershell -ExecutionPolicy Bypass -File BGA/tools/build_summer20_h3_chart_package.ps1
```

当前以 SDR 视频为交付目标，HDR 素材请先在制作软件中做 SDR 色调映射。音视频播放使用音频 DSP 时钟；视频在解码器定位完成且目标帧进入显示队列后恢复播放，避免大幅回拖时重复 Seek 卡住旧画面。

新谱面中的绑定格式：

```json
{
  "videoBga": {
    "packageManifest": "曲名.assets/BGA/资源标识/manifest.json",
    "timeOffsetSeconds": 0,
    "enabled": true
  },
  "audioFile": "曲名.assets/Audio/歌曲标识.wav",
  "audioOffsetSeconds": 0
}
```

视频偏移 +2 表示谱面 2 秒才开始显示视频 0 秒。音频沿用原有约定：`audioTime = songTime + audioOffsetSeconds`。两者是不同参数。隐藏 BGA 不应隐藏歌曲。

## 这次 45 秒 BGA 的来源

- 原曲：`BGA/audio/track_master.wav`，只使用 0–45 秒；没有另写歌曲或换用合成演示音。
- Blender MCP 白模：`BGA/whitebox45/Firefly_Whitebox_45s.blend`，8 个独立场景与动画相机。
- 白模生成代码：`BGA/tools/build_whitebox_45.py`。
- 白模首尾帧：`BGA/whitebox45/references/`。
- 正式图像参考帧：`BGA/whitebox45/keyframes/`，使用内置 image_gen，根据白模构图生成；完整提示词在 `imagegen-prompts.json`。
- 本地生成脚本：`BGA/tools/comfy_bga45.py`；ComfyUI 地址 `http://127.0.0.1:8188`。
- 可重放 API 工作流、种子和任务 ID：`BGA/whitebox45/comfy_jobs/`。
- 最终制谱器包：`BGA/Video45/`；完整有声预览：`BGA/Firefly_Summer_45s_AI_BGA.mp4`。已验证 45.000 秒、1350 帧、30fps，并完整解码检查。
- ComfyUI 可编辑画布：`BGA/whitebox45/workflows/*.workflow.json`；对应图片已复制到本机 ComfyUI input 目录。也可以用 `export_comfy_workflows.py` 从选定的 API 任务重新生成画布文件。
- 选片记录：`selected-takes.json`。第 1 段使用新的 clean 版本（CFG 4），第 5 段使用 gates_stable 版本（CFG 3.5），其余 CFG 5；所有最终段落均为 24 步。弃用版本保留在 ComfyUI output 与任务历史，不覆盖制作证据。

| 秒数 | 场景与运动 |
|---|---|
| 0–6 | 露珠花园，前推、折射与花粉 |
| 6–12 | 叶片峡谷，追逐与轻微倾斜 |
| 12–18 | 丝带河流，横倾、水面与绸缎运动 |
| 18–24 | 花瓣回廊，轻微旋转与飘落花瓣 |
| 24–30 | 棱镜门，向前穿门与折射高光 |
| 30–36 | 浮岛瀑布，升高揭示与近远景视差 |
| 36–42 | 放射丝带，旋转通道与花粉 |
| 42–45 | 云海露珠，舒缓释放 |

Wan 2.2 TI2V 5B 的基础工作流使用首帧图像和文字控制。白模的最后一帧作为构图参考存档，但此模型工作流不逐帧锁定 Blender 运镜，也不承诺精确复刻相机轨迹。正式视频需看结果选片；其空间感来自画面里的透视和视差，不是把视频恢复成真实场景。

工作流原生生成 640×352、24fps，最后统一封装为 30fps；放大封装不等于原生高清生成。需要更清晰的正式发行版时，可保留同样的白模和参考帧，在更高显存机器用 1280×704 重跑，再导入同一制谱器。

## 和 Kubeez 方法的关系

[Kubeez scroll-world-video](https://github.com/KubeezMedia/kubeez-scroll-world-video) 的核心是预生成运动视频和精确定位播放时间，并不是深度重建。这里把滚动进度换成歌曲时间：真实音轨时钟驱动视频；暂停时合并 Seek；短 GOP 加速解码。镜头/特效留在视频，不在制谱器内重做。

当前版本不做视频物体深度遮挡、不提供自由视角、不从视频自动提取谱面、也不自动把路径透视拟合到 AI 运镜。要精确匹配景物，谱师需要按画面调整路径。未来可以单独增加相机/深度 sidecar，但不是当前“一键视频导入”的前提。

## 开场 20 秒的接缝链（`BGA/Video20/`）

45 秒成片是把 8 个镜头在 6/12/18/24/30/36/42 秒硬切的，实测每次跳变约为该视频正常帧间差的 7 倍，18 秒处是整幅画面重置（青绿微距 → 明亮花瓣回廊）。Kubeez 方案要求接缝两侧帧一致；在首尾帧不同的时候，文档规定的兜底做法是**短交叉溶解**。开场 20 秒因此重做成一条接缝链：

| 秒 | 镜头 | 接缝处理 |
|---|---|---|
| 0–6 | 露珠花园 | — |
| 6–12 | 叶片峡谷 | 6.0 s 处 5 帧交叉溶解 |
| 12–18 | 丝带河流 | 12.0 s 处 5 帧交叉溶解 |
| 18–20 | 花瓣回廊（开头 2 秒） | 18.0 s 处 5 帧交叉溶解 |

交付为 `BGA/Video20/`：`video.mp4`、`audio.wav`、`manifest.json`、`Firefly20.chart.json`。构建会把它复制到程序旁 `BGA/Video20/`，并生成 `Open-Firefly20.cmd`。制谱器的默认 BGA 指向这个包。

实测结果（`BGA/tools/analyze_seams.py`，与正常帧间差比较）：

| 接缝 | 硬切版本 | 接缝链版本 |
|---|---|---|
| 6.0 s | 5.86× | 2.33× |
| 12.0 s | 8.43× | 0.78× |
| 18.0 s | 11.52× | 0.00× |
| 平均 | 7.18× | 1.04× |

12.0 s 与 18.0 s 已低于视频自身正常帧间差，18.0 s 两侧帧逐像素相同。6.0 s 仍有 2.33×，因为那里要跨过最大的色调与景别差，而素材只够 5 帧溶解。

### 素材上限（为什么不是 6 帧以上）

每个镜头是 Wan 2.2 的 145 帧 24fps 原片，重采样到 30fps 后**只有 181 帧（6.033 秒）**，比 6 秒的歌曲窗口只多 33 毫秒。因此每个接缝最多只能拿到约 1 帧的额外素材；把溶解加长不会让接缝更好，只会把后面的接缝推离歌曲网格。当前配置（等长块 + 5 帧溶解）把三次接缝分别落在 5.933 / 11.867 / 17.600 秒，漂移 +0.03 / −0.03 / −0.23 秒，且输出正好 600 帧 = 20.000 秒。

要让接缝完全落在 6/12/18 秒并留出更长溶解，需要重渲染这几个镜头、让每条原片比 6 秒多约 0.5 秒。

### 重做与复核

```powershell
python BGA/tools/assemble_bga20.py                 # 重建 Video20 包（默认 5 帧溶解）
python BGA/tools/assemble_bga20.py --crossfade 0.5 # 换溶解长度（会被素材上限挡住并报错）
python BGA/tools/analyze_seams.py --video BGA/Video20/video.mp4 --shots 0:6,6:12,12:18,18:20
```

- `BGA/tools/probe_xfade_behavior.py`：实测 ffmpeg xfade 的 `offset` 与输出长度关系。链式接缝的输出长度是 `offset + 本段帧数 − 溶解帧数`，且如果某个 offset 超过上一段输出的末尾，链会被**静默截断**——所有尺寸计算都以这个实测结论为准。
- `BGA/tools/pick_edit_points.py`：按 RMS 能量找乐句内的安静点，用于挑选剪辑点。
- `BGA/whitebox20/seam-analysis.json`：最近一次接缝实测记录。


## 本地复现与检查

启动已有 ComfyUI 后，生成某一段（例如第一段）：

```powershell
python BGA/tools/comfy_bga45.py --shot 1 --frames 145 --steps 24 --wait
```

第八段使用 `--frames 73`。其他段为 145 帧；24fps 下分别略长于 6 秒/3 秒，成片裁到精确时长。

收集与合成：用带 PyAV 的 ComfyUI Python 运行 `BGA/tools/assemble_bga45.py --assemble`。这个脚本会检查每个实际生成任务成功、提取选定视频、封装、逐帧解码检查、验证精确时长，然后才写最终 manifest；缺少任何生成镜头就拒绝合成。

Windows 构建旁的 `Open-Firefly45.cmd` 打开这次 BGA 的独立示例谱面，不覆盖之前的草稿；示例保留三条空 Note 路径供继续制谱。

运行制谱器端到端检查：

```powershell
GeometryChartStudio.exe -chart fresh-test.json -chartEditorSmoke -chartEditorVideoInput "D:\BGA\manifest.json" -chartEditorCapture "D:\TestResults"
```

测试覆盖：导入、真实音轨、暂停 Seek、连续反向 Seek、解码像素变化、A/V 同步、谱面数据保留、便携保存与换目录加载、失败导入不破坏原 BGA。测试必须在实际图形窗口下运行；Windows 隐藏窗口会影响视频帧呈现和屏幕截图。

本次验证记录：

- `BGA/whitebox45/final-media-validation.json`：10 项媒体检查通过，包括精确 45 秒、1350 帧、无黑帧、BT.709 和与原曲前 45 秒转采样后 PCM 逐字节相同。
- `BGA/tests/final-present-45/video-bga-checks.txt`：21 项编辑器检查通过，包括所有八个场景 Seek、42→23 秒回跳后继续播放、跨镜头音画同步、片尾停止与末帧保持。
- `BGA/video45-build-present.log`：Windows 主程序构建通过，已覆盖 `Builds/GeometryChartStudio/GeometryChartStudio.exe`。

本地 FFmpeg 是 GPL 构建，公开分发前需要补齐对应的许可证及源码合规包；见 `BGA/tools/bin/FFMPEG-NOTICE.txt`。现有工作区构建不是已完成法务审查的公开发行包。
