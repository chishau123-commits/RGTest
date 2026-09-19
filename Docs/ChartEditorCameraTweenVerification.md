# K0→K1 相机补间修正（2026-09-18）

## 复现与原因

用户提供 `C:/Users/Chinshyo/AppData/LocalLow/DefaultCompany/Test/geometry-chart-draft.json`。测试仅使用 `Builds/camera-tween-source-chart.json` 副本，原始 SHA-256 为 `00B604C004D133B107FE2A1863AF8CE39E1DF090678A05D15B3F45EF10719E80`。

- K0：第 0 拍，legacy orbit；K1：第 43.935417 拍，world pose，约 21.967709 秒，接近垂直俯视。
- 原混合类型算法把 K0 按每帧当前时间重新求值，再与固定 K1 插值，导致起点漂移。实际谱面中点偏离固定两端补间约 27.1786 个世界单位。
- 原算法先插值 look target，再逐帧 LookRotation；当方向与世界 up 的点积绝对值超过 .999 时突然把 up 换成 forward。只读数值复算在约 19.51331 秒出现约 11.82 度朝向跳变。

## 修正

- `SpatialDirector` 对包含任意 world key 的整条相机轨道采用一致的固定端点策略，将 legacy/path 节点按各自拍点的时间解析并缓存世界姿态。避免混合节点边界重新切换语义。
- 位置按固定端点 Lerp，仍使用拍点上的 SmoothStep 缓入缓出；朝向对端点基准旋转做 Quaternion.Slerp，不在补间中重新选择 up。
- 端点仅在精确接近极点、world up 无法构成有效坐标架时采用 fallback；89 度俯视仍保留有效的 world-up 坐标架。
- 显式 Roll 独立连续插值，保留 360 度完整旋转；FOV 插值不变。最后节点之后保持固定姿态。
- 无 world key 的纯 legacy/path 旧式轨道保持原行为。
- Camera 数值面板、坐标轴拖动、节点格式转换及撤销会刷新固定端点缓存；编辑辅助线与 Preview/游戏共用求值器。
- 未改谱面格式和用户草稿；黄球居中、编辑视角无自动跟随、静止 Note/常驻轨道不变。

## 自检入口

在完整桌面烟测参数后追加 `-chartEditorCameraTweenSmoke -chartEditorCameraTweenInput <谱面副本路径>`，输出 `camera-tween-checks.txt`、`camera-tween-midpoint.png`、`camera-tween-steep.png`。

覆盖混合节点、变速、固定端点/中间点、节点边界、前后夹取、垂直/反向朝向、完整 Roll、Gizmo/Undo 刷新、实际草稿 3000 点采样、Seek 确定性、数据不变、辅助线与 Preview 一致、编辑器不跟随。

## 验证与交付结果

- 最终桌面构建：`Builds/chart-camera-tween-build2.log`，`CHART_STUDIO_BUILD_PASS`。
- 最终桌面烟测：`Builds/chart-camera-tween-smoke-final.log`，共 496 项通过（交互 36、时间轴 36、工作区 35、静态 Note 34、编辑播放 261、相机补间 94），`CHART_STUDIO_SMOKE PASS`。
- 实际谱面中点位置误差为 0（原偏离约 27.1786）；3000 个采样间的最大朝向变化约 0.051934 度。准确到达 K1，随机/逆向 Seek、辅助线与 Preview 一致性均通过。
- 游戏回归：`Builds/chart-camera-tween-game-validation.log`，`GEOMETRY_VALIDATION_PASS 249 checks`。
- 截图及逐项报告：`Builds/CameraTweenQA-Final/`。已检查俯视段预览截图；连续性依据数值采样验证。
- 确认原入口未运行后，已覆盖 `Builds/GeometryChartStudio/GeometryChartStudio.exe` 所在发行目录，133 个发行文件的 SHA-256 均与测试构建一致。
- 交付后再次校验原始草稿 SHA-256，与上述测试前哈希完全一致；未修改用户谱面。
