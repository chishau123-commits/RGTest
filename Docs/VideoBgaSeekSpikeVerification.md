# 视频 seek 到区段头：量测记录（Spike A）

> 对应 [VideoBgaBranchingPlan.md](VideoBgaBranchingPlan.md) §6 的 Spike A，用于判定**决定 5**（"一条文件多区段 + 同 player seek"是否成立）。
> 术语见 [CONTEXT.md](../CONTEXT.md)。本记录只写实测，不写推测；推断处标【推断】。

## 0. 结论速览

| 问题 | 结果 |
|---|---|
| 跳到一个区段头要多久（解码器） | **帧就绪 22.5–23.1 ms**（`seekCompleted` 应答 14.0–14.7 ms），且与关键帧间距无关 |
| 玩家实际感觉到多久 | **107–111 ms**——比解码器多出约 84 ms，全部来自运行时自己的合并策略 |
| 会不会闪黑 | **不会**。40/40 样本 `black=0`；旧帧被保持住，然后被替换 |
| 会不会跳错帧 | **不会**。40/40 样本 `playerFrame == round(t×60)`，同目标两次像素指纹相同 |
| 关键帧密到每 15 帧必要吗 | **不必要**。1 秒 GOP 的 seek 成本与 0.25 秒 GOP 相同，而文件小 12% |
| 非关键帧 seek 呢 | 1 秒 GOP 内**免费**；4.17 秒 GOP 内 **慢 2.1 倍**（47.4 ms vs 22.7 ms） |
| 换成用户的真实 BGA（30fps 带 B 帧 → RIFE 插帧 60fps 无 B 帧） | 见 §8：**解码器 15 ms、运行时 104 ms、零黑帧、逐位确定** —— 上表的关键结论都在真实素材上被独立复现 |

## 1. 被测对象与素材

被测的是共享运行时 `Assets/RhythmDemo/Runtime/VideoBgaRuntime.cs`（制谱器与游玩端将来共用同一条路径），以及它下面的 Unity `VideoPlayer`。

**关键帧对照组**由 `ffmpeg` 现场生成（源 = `.buildtmp/bga20/video.mp4`，20.0 s / 1920×1080 / 60 fps / 无 B 帧），三个变体只差 `-g`：

| 素材 | `-g` | 关键帧数（1200 帧） | 关键帧间距 | 文件 |
|---|---|---|---|---|
| `original` | 离线装配 | **521** | ~1 帧（大片全内帧） | 18.08 MB |
| `gop15` | 15 | 80 | 0.25 s | 13.24 MB |
| `gop60` | 60 | 20 | 1.00 s | 11.66 MB |
| `gop250` | 250 | 5 | 4.17 s | 11.31 MB |

**顺带纠正一个方案假设**：`original` 的关键帧不是"每 15 帧一个"。`ChartEditorVideoBga.cs:202` 的转码命令确实带 `-g 15 -keyint_min 15 -sc_threshold 0`，但那条路径只作用于**制谱器自己导入的裸视频**；这份 20 秒包是**离线**接缝链装配出来的（[VideoBgaSeamChainVerification.md](VideoBgaSeamChainVerification.md)），交叉溶解强制插入了大量关键帧，实测 521/1200。所以交付包的实际关键帧网格**不能假定**等于导入命令里的数字——区段头必须按**交付文件实测的网格**去对齐，或干脆不依赖对齐（见 §4）。

## 2. 方法

两套量测，分别回答"硬件多快"和"玩家感觉多快"。都在**真实图形窗口**的 player 里跑（`Docs/VideoBgaWorkflow.md:179` 要求），每次调用只跑一个素材。

- **运行时级**（`-chartEditorSeekSpike <manifest>`）：走 `VideoBgaRuntime.Evaluate`，也就是真实调用路径。每帧 `Blit` 到 64×36 读回像素，记录哈希与平均亮度，同时等 `IsSeeking` 落回。
- **解码器级**（`-chartEditorSeekSpikeDecoder <manifest>`）：同一进程里另起一个**裸 `VideoPlayer`**，参数与 `VideoBgaRuntime` 一致（`APIOnly`、`audioOutputMode=None`、`waitForFirstFrame`、`skipOnDrop`、`timeReference=Freerun`、`sendFrameReadyEvents=true`），但**不经过 80 ms 合并宽限**，直接 `player.frame = N`，分别记录 `seekCompleted` 与目标帧 `frameReady` 的时刻。

Harness 在 `Assets/RhythmDemo/ChartEditor/ChartEditorSeekSpike.cs`；构建入口是 `ChartEditorBuildTools.BuildWindowsForVerification()`（正式构建硬依赖 gitignore 的 `/BGA/` 载荷，本机没有，验证构建会降级为 `CHART_STUDIO_PAYLOAD_SKIPPED` 警告）。

每个目标点先 park 到远端再测，保证被测量的永远是一次真实 seek。关键帧位置由 `ffprobe` 导出成 `keyframes.txt` 交给 harness，**不靠猜**。

## 3. 解码器级结果

三次重复的中位数（ms）：

| 素材 | 区段头 `seekCompleted` | 区段头 **帧就绪** | 非关键帧 `seekCompleted` | 非关键帧 **帧就绪** |
|---|---|---|---|---|
| `original` | 14.1 | 22.5 | 14.1 | 22.4 |
| `gop15` | 14.3 | 22.6 | 14.2 | 22.6 |
| `gop60` | 14.4 | 22.8 | 14.4 | 22.7 |
| `gop250` | 14.3 | 22.7 | **39.1**（最差 48.0） | **47.4**（最差 56.4） |

读法：

1. **对齐关键帧的 seek 成本是常数**：无论关键帧间距是 0.25 s、1 s 还是 4.17 s，帧就绪都在 22.7 ms 上下。**关键帧密度不买 seek 速度。**
2. **偏离关键帧的代价随"离上一个关键帧多远"增长**，实测约 **0.25 ms/帧**：`gop250` 里离头 100 帧要多花约 25 ms；1 秒 GOP 内（≤60 帧）落在这条曲线的噪声里，测不出差别。
3. `skipOnDrop=true` 意味着运行时允许丢帧追时钟，所以这些数字是"落到目标帧"而不是"逐帧播到目标帧"。

## 4. 运行时级结果与那 84 ms 的来历

| 素材 | 区段头 median | 非关键帧 | 后向 seek | 播放中切换 | 黑帧 |
|---|---|---|---|---|---|
| `original` | 107.5 ms | 107.9 | 107.7 | 110.3 | 0 |
| `gop15` | 107.4 ms | 108.0 | 107.8 | 110.0 | 0 |

**均匀性就是证据**：关键帧间距差 40 倍、前向/后向/播放中切换全都落在 107–111 ms 这个 4 ms 宽的带里。这说明测的不是解码，而是 `VideoBgaRuntime` 自己的策略：

```
107 ms  ≈  80 ms（VideoBgaRuntime.cs:55 的 seekReadyAfter 宽限）
         +  23 ms（解码器把目标帧准备好）
         +   4 ms（轮询粒度）
```

那 80 ms 是刻意加的：`seekCompleted` 在 WMF 上早于呈现，不等一下就会让移动中的歌曲时钟立刻提交下一次 seek（代码注释 `VideoBgaRuntime.cs:52-55`）。它同时也解释了为什么**没有黑帧**——宽限期内旧帧一直留在屏幕上，然后被新帧整体替换（`stale=12`、`distinct=2`）。

## 5. 本记录改变了方案里的什么

1. **决定 5 成立**，且"多区段同文件 seek"是可行形态：解码器 23 ms、精确落帧、零黑帧、确定性可复现。
2. **素材规范可以放松**：[VideoBgaWorkflow.md:41](VideoBgaWorkflow.md) 与转码命令里的"每 15 帧一个关键帧"**对 seek 速度没有贡献**。建议改为 **`-g 60`（1 秒 GOP）**：seek 成本不变、文件小 12%、且 1 秒内的非关键帧 seek 免费。继续加大到 4 秒只再省 3%，却让非对齐 seek 慢 2.1 倍——**不划算**。
3. **区段头对齐关键帧这条保留**，但理由从"否则很慢"降级为"反正不花钱"：它是保险，不是性能前提。
4. **视频层的延迟地板被量化了**：玩家能感觉到的最快画面变化是 **~107 ms**（约 6.5 帧 @60fps），其中 80 ms 是运行时策略、23 ms 是硬件。这给方案原来的判断——**逐 Note 反馈必须留在引擎侧程序化图层，视频层只承载段落与状态**——提供了实测数字，而不再只是引用官方"seek may be noticeably long"。

## 6. 已知限制与保留意见

- **解码器级探针不校验画面内容**：它用 `APIOnly`，`landed=True` 的含义是"解码器报告目标帧已就绪"，不是"画面确实是那一帧"。画面正确性由运行时级那一轮用像素指纹 + 平均亮度验证（49 个样本）。
- **非关键帧代价的斜率是 3 个点拟合出来的**（6 帧 / 24 帧 / 100 帧），标为【推断】。要更准需要在同一 GOP 内取更多距离点。
- **Harness 有间歇性不落盘**：两批共 8 次调用里，有 2 次 player 在约 7 秒内退出且没有写出报告（`exit=0`，日志正常、无异常），单独重跑即成功。**每次调用只跑一个素材，并在读数据前确认 `seek-spike-checks.txt` 存在**；连续启动 player 之间留 8 秒以上间隔。
- **单机单卡**：Windows / NVIDIA RTX 5060 Laptop / Direct3D 11 / WMF 解码。**Android 的数字一个都没测**（Spike B 的范围），不要把这里的 23 ms 外推到移动端硬解。
- **构建会弄脏工作树**：`BuildWindowsForVerification()` 会经由 `EnsureRuntimeGltfShaders()` 往 `Assets/RhythmDemo/Resources/BlenderBgaShaders/` 拷着色器、经 `CreateScene()` 重写 `Assets/RhythmDemo/Scenes/GeometryChartStudio.unity`，并改动 `ProjectSettings/ProjectSettings.asset`。这三处都不是本项工作的改动，跑完构建后用 `git checkout --` 复原，否则 review 时会被噪音淹没。
- **文件体积对比不是同质量对比**：`original` 的 18.08 MB 来自不同 CRF/preset，只能同 `gop15/60/250` 三者之间比（同一命令、只差 `-g`）。
- 素材与 fixture 全部在 gitignore 的 `BGA/spike/` 下；生成用的 `ffmpeg`/`ffprobe` 来自本机既有安装（`D:\Programs\Trae CN\resources\app\bin\`），**未把二进制放进仓库**。

## 7. 复现

```powershell
# 1. 验证构建（不需要 /BGA/ 载荷）
Unity.exe -batchmode -nographics -quit -projectPath <项目> `
  -executeMethod GeometryRhythm.Editor.ChartEditorBuildTools.BuildWindowsForVerification

# 2. 解码器级：跳区段头 vs 跳非关键帧
Builds\GeometryChartStudio\GeometryChartStudio.exe -chartEditorSmoke `
  -chartEditorSeekSpikeDecoder BGA\spike\gop60\manifest.json `
  -chartEditorSeekSpikeKeyframes BGA\spike\gop60\keyframes.txt `
  -chartEditorSeekSpikeRepeats 3 `
  -chartEditorCapture BGA\spike\decoder\gop60 -chart fresh.json

# 3. 运行时级（真实调用路径 + 像素校验）
Builds\GeometryChartStudio\GeometryChartStudio.exe -chartEditorSmoke `
  -chartEditorSeekSpike BGA\spike\gop60\manifest.json `
  -chartEditorSeekSpikeKeyframes BGA\spike\gop60\keyframes.txt `
  -chartEditorSeekSpikeRepeats 3 `
  -chartEditorCapture BGA\spike\out\gop60 -chart fresh.json
```

报告：`<capture>\seek-spike-checks.txt`、`<capture>\seek-spike.json`；日志标记 `BGA_SEEK_SPIKE_PASS/FAIL VERDICT=...` 与 `DECODER_PROBE_COMPLETE`。

<details>
<summary>关键帧列表怎么来</summary>

```powershell
ffprobe -v error -select_streams v:0 -show_entries frame=key_frame,pts_time -of csv=p=0 video.mp4 |
  Where-Object { $_ -match '^1,' } | ForEach-Object { ($_ -split ',')[1] } | Set-Content keyframes.txt
```

对照组生成（同一命令，只改 `-g`）：

```powershell
ffmpeg -y -i src.mp4 -an -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p `
  -g 60 -keyint_min 60 -sc_threshold 0 -bf 0 -r 60 -movflags +faststart out.mp4
```

`-sc_threshold 0` 关掉场景切换插入的关键帧，否则"关键帧间距"这个自变量就不干净了。
</details>

## 8. 真实素材复测：用户提供的 BGA，30fps 原始 → RIFE 插帧 60fps

前七节用的是从 20 秒生成片派生的 fixture。本节是**用户指定的那条 BGA**（`BV12K4y1P7ps`，EBIMAYO - GOODTEK (Rework) [BGA]），并且按用户要求**插帧到 60fps**。全部量测跑在 **ThartEditor.exe** 里（`-thartSeekSpike` / `-thartSeekSpikeDecoder`），不再是旧制谱器。

### 8.1 两版素材

| | 30fps 原始 | **60fps 插帧** |
|---|---|---|
| 来源 | B 站匿名可得档位 `30080`（1920×1080 avc1，29–30fps，2616k） | 由左列用 RIFE 2× 插帧后重编 |
| 处理 | `-c copy` remux（保原画质） | RIFE `rife-v4.6` → x264 |
| 编码 | 原样 | `-bf 0 -g 60 -keyint_min 60 -sc_threshold 0 -preset slow -crf 23` |
| 时长 / 帧数 | 124.100 s / 3723 | **124.100 s / 7446** |
| 关键帧 | 25 个，每 **5 s** | **125 个，每 1 s** |
| B 帧 | **有**（`has_b_frames=4`） | **无** |
| 体积 / 码率 | 38.7 MB / 2616 kbps | **107.0 MB / 7236 kbps** |
| 包 | `BGA/goodtek/`（gitignore） | `BGA/goodtek60/`（gitignore） |

### 8.2 插帧流水线（可复现）

```powershell
# 1) 抽帧（3723 帧，实测 17 秒）
ffmpeg -i download/video.mp4 -vsync 0 frames-in/%05d.png
# 2) RIFE 2×（7446 帧，实测 2 分 2 秒；模型 rife-v4.6）
rife-ncnn-vulkan -i frames-in -o frames-out -m rife-v4.6 -f "%08d.png" -j 2:4:4
# 3) 按仓库规格编码
ffmpeg -framerate 60 -i frames-out/%08d.png -c:v libx264 -profile:v high -level 5.1 `
  -bf 0 -g 60 -keyint_min 60 -sc_threshold 0 -preset slow -crf 23 -pix_fmt yuv420p `
  -color_primaries bt709 -color_trc bt709 -colorspace bt709 `
  -bsf:v h264_metadata=colour_primaries=1:transfer_characteristics=1:matrix_coefficients=1 `
  -movflags +faststart video60.mp4
```

实测事实：

- **帧序是 `原, 合成, 原, 合成 …`**，输出编号从 **1** 开始（`%08d`）。用 PSNR 验证：`out#1` 对 `src#1`、`out#3` 对 `src#2` 都是 **`inf`**——**真实帧逐位未改**，插帧只新增合成帧，不劣化原帧。
- 60 个源帧 → 120 个输出帧（正好 2N），所以 3723 → 7446，@60fps 恰好 124.100 s，**与原片时长一致**，音画不需要重新对齐。
- 工具与模型留在 `BGA/tools-interp/`（gitignore）；生成用的 `ffmpeg` 是 Majdata 那个 5.1.2 静态构建——**Trae CN 的构建没有 `minterpolate`**，两个可用构建的能力不同。
- 编码档位的取舍：CRF 20 出 **151.2 MB / 10.2 Mbps**，超出方案里"主片 ≤ 120 MB"的预算，而源片只有 2.6 Mbps——多花的码率买不到源里没有的细节。**CRF 23 preset slow 出 107.0 MB / 7.24 Mbps**，落在预算内，采用。

### 8.3 结果

| 指标 | 30fps 原始（带 B 帧，5 s GOP） | **60fps 插帧（无 B 帧，1 s GOP）** |
|---|---|---|
| 解码器帧就绪 · 区段头（5 s 网格 / 1 s 网格） | 60.6–61.2 ms | **10.7–19.7 ms**（median 15.2） |
| 解码器帧就绪 · 非关键帧 | 27.8–28.0 ms | 14.9–15.7 ms（median 15.3） |
| 运行时 · 区段头 median | 141.7 ms | **103.8 ms**（worst 108.0） |
| 运行时 · 非关键帧 | 108.3 ms | 108.4 ms |
| 运行时 · 后向 / 播放中切换 | 141.5 / 143.0 ms | 108.4 / 109.8 ms |
| 黑帧 | 0（30 样本） | **0（30 样本）** |
| 精确落帧 | 30/30 | **30/30** |
| 逐位不确定的目标 | **6 / 6** | **0 / 6** |
| 退出码 | FAIL（确定性门槛） | **PASS** |

### 8.4 这一节带来的四个结论

1. **`80 ms + 解码器帧就绪` 这个模型在真实素材上第三次、第四次成立**：30fps 时 `80+61=141` 对上 141.7，60fps 时 `80+15=95` 对上 103.8（差值落在轮询与 `.51/fps` 容差上）。运行时那 ~80 ms 是**策略**，不是硬件。
2. **B 帧会破坏暂停 seek 的像素级确定性**：带 B 帧时 `player.frame` 每次都报对（30/30），但纹理里的像素在重复测量之间会变（6/6 目标不确定）——解码器"报告的帧"与"当下呈现的帧"不是同一帧。重编成 `-bf 0` 后归零。**所以交付素材不应该带 B 帧**，这与仓库既有的转码命令一致。
3. **在 30fps 那条素材上，跳关键帧反而比跳非关键帧慢一倍多**（61 vs 28 ms）——合理推断是 IDR 帧本身远大于 P 帧，解码一帧 IDR 更贵；`seekCompleted` 的应答时间同步变化（57 vs 24 ms）说明慢在解码器内部而不是等待。到了 60fps / `-g 60`，两者拉平（15.2 vs 15.3 ms）。**结论：`-g 60` 的 1 秒网格下，"区段头必须落在关键帧上"既不是性能前提，也不再是性能负担。**
4. **确定性门槛已改成"落定后一致"**：原来在 seek 宣告完成的那一刻就取指纹，会撞上目标帧的呈现延迟——那是抖动不是不确定性。现在落定后多采 3 帧，用最后那帧代表该目标的画面。改之前 60fps 素材上仍有 1/6 假阳性；改之后 0/6。
