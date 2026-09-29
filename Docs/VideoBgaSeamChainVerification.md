# 开场 20 秒 BGA 接缝链（2026-09-23）

## 实现

- 新增 `BGA/tools/assemble_bga20.py`：把 `BGA/whitebox45/clips/` 里真实的 ComfyUI 原片按歌曲时间重排成一条连续的 20 秒链，并在每个硬切处替换为短交叉溶解。
- 交付包 `BGA/Video20/`：`video.mp4`（1280×720、30fps、600 帧 = 20.000 秒）、`audio.wav`（原曲前 20 秒，48kHz 双声道 PCM）、`manifest.json`、`Firefly20.chart.json`。
- 每段按等长块裁切，接缝落在 5.933 / 11.867 / 17.600 秒（相对 6/12/18 秒边界漂移 +0.03 / −0.03 / −0.23 秒），溶解固定 5 帧。
- 制谱器默认 BGA 改为 `BGA/Video20/manifest.json`；构建脚本复制 Video20 与 Video45 两个包，并生成 `Open-Firefly20.cmd`。
- 新增 `BGA/tools/analyze_seams.py`（接缝量化）、`probe_xfade_behavior.py`（实测 ffmpeg xfade 的输出长度规则）、`pick_edit_points.py`（按 RMS 找乐句安静点）。

## 验证

- 接缝实测（`BGA/whitebox20/seam-analysis.json`），与视频自身正常帧间差（8.64）相比：

  | 接缝 | 45 秒硬切成片 | 20 秒接缝链 |
  |---|---|---|
  | 6.0 s | 5.86× | 2.33× |
  | 12.0 s | 8.43× | 0.78× |
  | 18.0 s | 11.52× | 0.00× |
  | 平均 | 7.18× | 1.04× |

  平均接缝跳变从 7.18 倍降到 1.04 倍；12.0 s 与 18.0 s 已低于视频自身正常帧间差，18.0 s 两侧帧逐像素相同。
- 成片校验：`BGA/Video20/video.mp4` 恰好 600 帧、20.000 秒、30fps、1280×720；音频为原曲 0–20 秒 48kHz 双声道精确抽取。
- Windows 构建：`BGA/build-video20.log`，exit 0，并复制两个 BGA 包到 `Builds/GeometryChartStudio/BGA/`。
- 制谱器端到端：`BGA/tests/video20/video-bga-checks.txt` 19 项全部 PASS，覆盖导入解码、暂停定位、连续反向定位、解码像素变化、歌曲时钟为准、隐藏 BGA 不隐藏歌曲、便携保存与换目录加载、失败导入保留原 BGA、3D 路径编辑仍可用；导入包 SHA-256 前缀 `955c4d67652d` 与 manifest 一致。
- 真实谱面冒烟：`BGA/tests/video20-realchart/chart-studio.png`，`PASS=True`，时间轴显示 0:00/20.00，BGA 面板显示 20.00 s。

## 已知限制

- 每条原片重采样到 30fps 后只有 181 帧（6.033 秒），比 6 秒歌曲窗口仅多 33 毫秒，因此每个接缝最多只能拿到约 1 帧额外素材。溶解加长不会改善接缝，只会把后面的接缝推离歌曲网格。
- 6.0 s 接缝仍有 2.33×：那里要跨过最大的色调与景别差（青绿微距 → 明亮花瓣回廊），5 帧溶解不足以完全抹平。
- 要真正做成 Kubeez 文档里的帧锁定接缝（connector 首帧 = 前一镜真实末帧、末帧 = 后一镜真实首帧），需要在云端 ComfyUI 用首尾帧工作流生成 connector 片段；本地 ComfyUI 无 Wan 模型（RTX 5060 8GB），未做。
