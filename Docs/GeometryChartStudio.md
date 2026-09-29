# Geometry Chart Studio

这是一个 Windows 桌面 3D 制谱器。当前主流程：**在任意外部工具中完成 BGA 视频 → 一键导入成片 → 制谱器按歌曲时间播放视频，在前面编辑 3D Paths、Notes → 导出单个 `.grchart` 谱面包**。视频谱面只保留 Path，不再区分 Stage/Scene 线与 Path 线。

视频谱面现在使用**统一编辑/播放视图**，直接按原 Preview 的路径、Note 和判定圈效果编辑，不再切换到只读 Preview。相机默认固定 X=0、Y=0、朝向 +Z、FOV=53°，只有沿轨道的**里程**根据时间轴关键帧前进；新增的 **Camera pose** 轨道可以把它从这条直线上挪开、转头和改焦距，全 0 时与旧版逐位相同。右键旋转、中键平移、Caps Lock 飞行、F 聚焦和缩放均禁用；隐藏视频也不会解除约束。视频必须为 **16:9、方形像素**，其他比例明确拒绝，不拉伸也不裁切。视频没有真实景物深度遮挡。

## 当前工作流

1. 在外部完成 BGA 视频。AI、ComfyUI、Blender、剪辑软件都可以；制谱器不依赖生成方式。
2. 在 **Files → Import / replace video** 或 BGA 页点击 **Import Video**，选择 MP4/MOV/MKV/WebM/AVI，或 `video-bga` 包的 `manifest.json`。普通视频自动转为 H.264 High / yuv420p / 60 fps / GOP 15，提取音轨并校验解码后再替换当前 BGA。失败不会清空已有 BGA。静音视频不会覆盖单独选择的歌曲。
3. 在 **Paths** 页制作每条路径独立的屏幕百分比锚点，并在同一页的 **Camera rail** 面板设置公共里程轨道、在 **Camera pose** 面板设置机位；在 **Notes** 页放置音符。打开视频谱面直接进入 Paths。
4. 需要换画面时重新导入视频，不改已有路线、路径、Note 和 BPM。BGA 页可以单独 **Import / replace song**、调整视频时间偏移或隐藏画面。视频与音乐分别解码，避免双重播放音轨。
5. `Ctrl+S` 保存工作副本。交付时选择 **Files → Export .grchart package**，视频、音频、BGA 清单和谱面 JSON 会封装为一个文件。另一位谱师用 **Open chart / package** 直接打开。

播放使用与 `PlayScheduled` 共用的音频 DSP 采样时钟，不累积渲染帧时间，也不会把音频 EOF 时重置到 0 的读取值误当成歌曲回到开头。暂停时直接定位到目标视频帧；连续拖动只保留最新 Seek，避免解码请求堆积。视频早于偏移点时隐藏、结束后停在末帧。BGA 的摄像机运动已经在视频中，不再从 Blender GLB 重建。

详细的一键导入约定、素材目录和复现命令见 [VideoBgaWorkflow.md](VideoBgaWorkflow.md)。

视频谱面顶部只保留三个工作区：**BGA / Paths / Notes**，快捷键分别是 `1`–`3`（`4` 也可进入 Notes）。旧 Stage 入口在视频模式中映射至 Paths。**每个页面的时间轴始终同时包含 Camera rail、Camera pose 和所有 Path 的 X%/Y% 锚点行**；Notes 页面将所有 Note 行放在音频波形正下方，Camera rail / Camera pose / Path XY 保留在下方，轨道较多时可纵向滚动。旧的 Scene、Effects、Motion、Map 编辑入口继续隐藏；无视频的历史谱面仍可使用旧编辑方式。

## 旧版 Blender 3D 包（兼容）

旧版源工程为 `BGA/blender/Firefly_the_Summer_Opening_v03.blend`，导出包为 `BGA/exports/Firefly_the_Summer_Opening_v03/`。以下描述只适用于带 `blenderBga` 字段且未启用新视频的历史谱面；不再是新谱面的默认流程。

包内包含：

- `scene.glb`：模型、层级、材质、骨骼/对象动画、相机和 glTF 支持的灯光；
- `timeline.json`：活动相机切换、对象显隐、灯光状态和世界颜色；
- `background.png`：从 Blender World 节点烘焙的全景背景；
- `manifest.json`：源 `.blend`、SHA-256、Blender 版本、帧率、时长和资源统计。

制谱器用歌曲绝对时间直接采样 3D 动画。因此暂停、Seek、拖动时间轴或低帧率播放都不会产生累计漂移。BGA 场景有独立的运行时根节点，编辑路径和 Note 不会重新载入大型 GLB。

导出器还会逐帧烘焙 Blender 依赖图计算后的活动镜头（位置、朝向、焦距与裁剪面），因此 Track To、Follow Path、父子层级和驱动器造成的运镜不会在制谱器中丢失。

**BGA camera · FOLLOWING** 使用 Blender 当前镜头；切换到 **FREE INSPECTION** 后可以在动画继续运行时自由观察场景。F5 预览始终使用 Blender 镜头。

Blender 的 glTF 导出器不能原样表达 EEVEE/Cycles 合成器节点、任意材质参数动画和 Area Light 类型。这些内容需要在 Unity 中做对应 Shader/后处理扩展；当前桥接会保留可导出的真实 3D 内容，并用 sidecar 补齐相机切换、显隐和灯光数值，Area Light 会由 Unity 光照近似。

## 构建与启动

- Unity 菜单：`Geometry Rhythm → Chart Studio → Create or Open Desktop Editor`
- Windows 构建：`Geometry Rhythm → Chart Studio → Build Windows Editor`
- 输出：`Builds/GeometryChartStudio/GeometryChartStudio.exe`
- 当前主版本入口：`Builds/GeometryChartStudio/Open-Summer20-H3.cmd`（已覆盖为统一视频编辑版本；原程序的被替换文件保存在 `Builds/Backups/`，谱面草稿与素材不变）。
- 可用 `-chart "D:\Charts\example.json"` 或 `-chart "D:\Charts\example.grchart"` 指定谱面/谱面包。

构建将视频导入所需的独立 FFmpeg 程序复制到 `BGA/Tools/`，并优先把独立 Path 格式的 `Summer20_H3_PathOnly_20s.grchart` 放到 `Charts/`（若尚未生成，则附带旧起步包，打开时迁移新坐标）。双击 `Open-Summer20-H3.cmd` 可直接打开本次20秒起步谱面。旧 Blender 资源仅为历史谱面兼容保留，不再显示 Blender 导入按钮。

Blender 自动查找顺序为环境变量 `BLENDER_EXE`、`D:\Programs\Blender\blender.exe`、Blender 5.2 默认安装位置。

## 制谱操作

- **Camera rail / 里程（Paths 页内）**：修改当前时刻的里程（自动插入关键帧）；时间轴 `CAMERA RAIL` 行添加/选择/拖动关键帧。里程必须递增，首末关键帧固定在谱面起终点。点击 `To next key` 切换线性、缓入、缓出、平滑或更平滑补间。它只决定音符和锚点沿轨道走多快，首末关键帧固定、不控制画面构图；不再附带 Scene 线。
- **Camera pose / 机位（Paths 页内）**：给相机加一条独立机位轨道，每个关键帧 7 个数。**Offset X / Y / Z** 把相机从里程直线上挪开（+X 向右、+Y 向上、+Z 沿歌更深）；**Yaw** 左右摇头、**Pitch** 抬头低头、**Roll** 歪头；**FOV** 控制广角/长焦。时间轴 `CAMERA POSE` 行添加/选择/拖动关键帧，右侧状态栏会显示 `camera ON RAIL` 或当前偏离距离与三个角度。`Reset this key to the straight rail` 一键清空这一帧。**全部为 0 时与旧版本逐位相同**，旧谱面无需迁移。
  - 想让谱面跟着视频平移：让 Offset X/Y 跟视频镜头一起动。想做出纵深：Offset Z 往前推、FOV 保持或加大。想压平成 2D 平面感：Offset Z 往后拉、FOV 收窄。想拐弯：让 Offset 或 Yaw 随时间连续变化。
  - 平面视频不会跟着相机转：大幅 Yaw/Pitch 会让 3D 前景和视频画面分家，小角度、推拉和平移最容易融进去。
- **Paths / Path anchors**：选路径，在 `X (%)` / `Y (%)` 输入精确坐标，或拖动画面里的黄色十字。Shift+单击画面可把当前时间锚点放到鼠标位置；时间轴可以添加、拖动时间或右键删除。每条 Path 根据自己的锚点独立补间，不受其他 Path 影响。点击任意页面的 Path 行或锚点会直接打开对应 Path 的编辑面板。
- **Notes**：选择 Path、TAP/DRAG 与保护属性，在播放头添加音符；可在 3D 视图或时间轴中选择并移动。
- `Ctrl+S` 保存，`Ctrl+Z/Ctrl+Y` 撤销/重做，Delete 删除当前选中的 rail/pose/Path 锚点或 Note。
- F5 / Space 在当前编辑视图内播放/暂停，面板和画幅不切换。播放中暂时禁用画面拖拽；暂停后继续编辑。

## Note 节拍编辑

Notes 页使用音乐拍线，而不是把等间隔的秒线当作节拍。`B 0` 表示谱面起点；亮线为整拍、更亮线每 4 拍分组（不是自动识别歌曲的拍号），细线为当前吸附细分。缩小时隐藏过密细线，放大后显示；实际吸附精度不随缩放降低。支持变速 BPM 的 TempoMap。

- 点击 **Snap** 循环切换 `1 → 1/2 → 1/4 → 1/8 → 1/16 → 1/3 → 1/6 → 1/12 → OFF`。单位都是**拍的分数**：`1/4` 是四分之一拍，不是四分音符。默认 `1/4`。
- **N / + Note / 点击空白 Note 行**统一使用吸附。鼠标悬停显示黄色落点线；拖动 Note 也遵循同一规则。同一 Path 同一 tick 不重复添加、不允许拖出重叠音符；不同 Path 可以同拍同时发音。结尾吸附到最后一个合法拍点，不落在 `endBeat` 上。
- **← / →** 或 **- Step / + Step**：移动播放头到上/下一个格点；**Shift+←/→**：整拍步进。Notes 页拖动标尺或波形同样吸附；选择 `Snap OFF` 可自由定位。
- **Alt+←/→**：移动选中的 Note 一个吸附格；**Alt+Shift+←/→**：移动 1 tick。**Q / Snap selected**：仅量化所选 Note，显示将移动的毫秒数。已有音符不会在升级、打开文件或切换 Snap 时自动量化。
- 选中 Note 后，侧栏上方显示精确 beat、tick、毫秒时间；输入 **Beat** 并点击 **Set exact** 可精确修改，不强制吸附。一次连续拖动只有一个撤销步骤；插入、微调、量化和删除均可撤销。
- **M / Click ON**：编辑器节拍器，Space 开始试听。整拍点击、每 4 拍重音，与歌曲使用同一 DSP 时钟，Seek 重置相位，暂停取消已调度点击。节拍器只用于编辑，不写入音频或谱面包。
- **Ctrl+滚轮**在鼠标位置缩放；波形按约 1ms 峰值缓存（最长 60 万格），绘制时保留每个像素区间的最大峰值，并正确考虑音频偏移。

### 音符流速与远端出生

视频谱面的音符从 **Path 最远端** 进入，再沿完整轨道接近判定圈。轨道绘制和音符可见性共用同一个远端边界（相机相对深度 100），不再分别使用轨道 100、音符 60 的截断。界面显示 **Spawn: PATH END**，移除独立 Spawn 调节；旧版保存的 Spawn 偏好不再读取，无需用户手动重置。

**Scroll** 默认 **8×**，Notes 侧栏和时间轴的 `- / +` 每次调整 0.25×，范围 **0.5×–48×**；`[` / `]` 也可调整。侧栏提供 **4× / 6× / 8× / 10× / 12×** 快速档位，时间轴 `8x` 恢复推荐速度，侧栏 `1x` 恢复原运动速度。任何倍率下出生边界都与轨道远端一致。

当前 H3 的线性 Camera Z 速度为 5 单位/秒，音符从深度 100 到判定深度 7 的完整飞行时间由旧 3× 的 **6.2 秒**缩短为默认 8× 的 **2.325 秒**（10× 为 1.86 秒，12× 为 1.55 秒）。音符因此更晚从相同的远端进入、以更高速度接近，同时在屏音符更少；不是删除音符或在中段裁掉它们。侧栏 **Ahead here** 显示当前可见范围覆盖的未来时长，考虑 Z 缓动与 BPM 变化，并限制在谱面结束之前；它不是额外的时间截断。

倍率仅作用于音符到判定面的剩余距离。音符沿原 Path 接近，判定拍点、判定位置、BPM、音频偏移、Path 百分比锚点、Camera Z、FOV 均不改。播放中调速不会重启音频时钟。1× 恢复该谱面的完整阅读窗口（视频谱面按轨道里程，无视频谱面按 `approachSeconds`）。无视频谱面没有可伸缩的相机里程，因此共享流速改为缩放它自己的 `approachSeconds` 窗口：`窗口 = approachSeconds × 8 / 流速`，并限制在 0.35–12 秒；推荐速度 8× 得到的正是谱面写入的 `approachSeconds`，所以默认设置下旧谱面逐位不变（`ChartEditorNoteSpeedChecks` 会断言这一点）。打开谱面、拖动播放头或从中途开始时，直接还原该时刻的音符位置，不将已经在路上的音符送回远端、推迟判定或凭空增加音乐前奏；此修复不添加开头预滚。

流速仍作为本机个人阅读偏好保存，编辑器和游戏运行时代码共享；不计入谱面撤销栈、不把当前谱面标记为修改、不写入 `.grchart`。本次更新将旧版保存的低流速（如 3×、6×）提升为 8×，不是只修改首次安装默认值。在新版自行选择速度后会记录设置版本，之后重开尊重该选择，包括主动调低的速度；读取设置和测试不会改写个人偏好。已有谱面包无需转换或增加文件。

回归测试参数：`-chart <H3.grchart> -chartEditorSmoke -chartEditorNoteSpeedSmoke -chartEditorCapture <输出目录>`。报告为 `note-speed-checks.txt`，生成 1× / 3× / 8× 密集谱面截图，同一测试时刻在屏音符由 3× 的 75 个降为 8× 的 28 个。另在 3× / 8× / 16× 分别生成远端出生、接近、到达判定的三帧截图，并按真实播放时钟逐帧验证完整飞行，确认实际 LineRenderer 终点与音符首帧中心在世界坐标及视频百分比投影中重合。测试同时覆盖旧偏好升级与新版主动选择慢速后的保留，不改写源包或个人设置。**无视频谱面**的窗口缩放也在同一份报告里断言（推荐速度等于写入的 `approachSeconds`，速度翻倍时接近速度翻倍，判定锚点不动）：当前 71 项全部 PASS。游戏侧的同一条不变量由 `ChartImportValidation` 对曲库每一张谱面复验。

如果拍线整体偏离歌曲，展开侧栏 **Timing / BPM** 校准：

1. **BPM** 修改播放头所在的现有 tempo 段，不自动检测音乐、不新增变速点；其他 tempo 段保留。
2. **Audio ms** 表示谱面第 0 拍对应的音频文件时间。正数跳过音频开头，负数延后音频开始。修改时同步反向补偿视频 offset，保持既有音画相对同步。
3. 点击 **Apply timing calibration** 才应用，可整次撤销。BPM 会改变已有 Note、Path 锚点和 Camera Z 关键帧对应的秒数，但不改它们的 tick、XY、Z。请先保存，再根据波形与节拍器试听校准。当前起步谱面仍保留原来的 180 BPM / 0 offset，并未宣称已自动识别正确首拍。

回归测试：构建后以 `-chart <H3.grchart> -chartEditorSmoke -chartEditorBeatSmoke -chartEditorCapture <输出目录>` 启动，生成 `beat-editing-checks.txt`、Notes/校准面板截图及仅用于测试的 round-trip 包。测试不改写传入谱面包。

## 视频坐标与文件包

锚点采用视频左上角 `(0%, 0%)`、右下角 `(100%, 100%)`。窗口面板、时间轴、留黑均不计入。视口按整数像素的 16:9 画幅适配窗口和 UI 缩放，避免光栅取整改变百分比映射。

每个锚点包含 `tick, xPercent, yPercent`，**在锚点自己的时间**落在指定视频位置。该时间先由 TempoMap 换算为秒，插值得到该时刻的**相机位姿**（轨道里程 + 机位 offset + 朝向 + FOV），再把百分比反投影到相机前方 7 单位的判定面，得到固定的世界点。其他时间该点随相机推进产生透视变化，而不是永远贴住屏幕。Note 在 1× 流速时固定在击打时间对应的世界点；倍率为 `r` 时，相机相对深度为 `7 + r × (里程(hitTime) - 里程(currentTime))`，在该深度采样原 Path。到击打时刻始终回到同一判定面与锚点，不叠加旧版非视频滚动公式。

因为相机的**位置和朝向都可以关键帧**，世界里的轨道会真的拐弯：每段锚点之间的位置由**里程**（而不是世界 Z）分段线性插值，所以相机转头超过 90°、世界 Z 不再递增时，轨道也不会折叠或错段。判定面中心和屏幕的 up 轴同样跟随机位，因此相机 roll 时 Note 与判定圈会跟着画面一起倾斜，而不是跟着世界倾斜。

`cameraZKeys` 现在只是**音符里程**（音符多快飞过来、判定面在第几米），`cameraPoseKeys` 只是**画面构图**（相机在哪、朝哪、多大焦距）。两者互不干扰：改机位不会改变音符到达时刻、出生远端、判定位置或谱面数据。

旧世界路线、相机和 offset 字段保留在文件中，不删除。旧视频谱面首次打开会初始化新的线性里程轨道、一条全 0 的机位轨道和默认百分比锚点；全 0 的机位轨道与旧版本逐位相同，不需要迁移。不会声称旧世界坐标能无损等价转换成新模型，需在统一视图中重新校准。无视频的历史谱面保留旧模式。

已有 `videoSpace.schemaVersion=1` 的百分比谱面会迁移为版本 2：取旧 Scene 与每条 Path 的全部锚点时间并集，将当时的实际位置写入该 Path，然后清空旧 Scene 轨道。因旧曲线在 Z 上为分段线性相加，此过程保留整条曲线形状。原文件在打开时不会被重写，保存/导出才写入新格式。为避免裁掉原有画面外曲线，版本 2 允许小于 0 或大于 100 的有限百分比，表示视频边界以外；鼠标拖放仍限制在画面内。

`.grchart` 仍是携带 `package.json`、`chart.json`、视频、音频和 BGA manifest 的便携 ZIP。新增数据直接存入 `chart.json`，不需要额外相机文件、白模或 Blender 工程：

```json
{
  "videoSpace": {
    "schemaVersion": 2,
    "cameraZKeys": [
      { "tick": 0, "z": 0, "easing": "linear" },
      { "tick": 28800, "z": 100, "easing": "linear" }
    ],
    "cameraPoseKeys": [
      { "tick": 0, "dx": 0, "dy": 0, "dz": 0, "yaw": 0, "pitch": 0, "roll": 0, "fov": 53, "easing": "linear" },
      { "tick": 9600, "dx": 4, "dy": 1, "dz": 0, "yaw": 12, "pitch": -4, "roll": 0, "fov": 53, "easing": "smoother" },
      { "tick": 28800, "dx": -3, "dy": 2, "dz": -8, "yaw": -8, "pitch": 0, "roll": 5, "fov": 45, "easing": "linear" }
    ]
  },
  "paths": [{ "id": "p0", "screenAnchors": [
    { "tick": 0, "xPercent": 34, "yPercent": 70 },
    { "tick": 28800, "xPercent": 34, "yPercent": 70 }
  ] }]
}
```

上例仅展示新字段（480 ticks/拍、180 BPM、20 秒），不是独立完整谱面。读取程序必须支持 `videoSpace.schemaVersion=2` 并采用共享的 `VideoChartSpace` / `SpatialDirector`，不能将百分比误读成世界 XY。非递增 Z、非有限百分比、重复锚点时间及非 16:9 素材均拒绝。

`cameraPoseKeys` 是**可选**的：整个数组缺失，或存在但每帧的 offset 与角度全为 0，都会得到与旧版本逐位一致的轴对齐相机。每个关键帧的 `dx/dy/dz` 是相对轨道直线的米数偏移，`yaw/pitch/roll` 是度，`fov` 省略（读回 0）时按 53° 处理，`easing` 与该段补间一致。校验要求时间递增、数值有限、`fov` 为 0 或落在 30–85；时间不要求覆盖全曲，范围外按首/末关键帧保持。为了让读取端不必猜，建议按上面的写法写出完整字段。

谱面仍为 JSON v1。新流程增加可选的 `videoBga` 和 `audioFile`；老的 `blenderBga` 字段仍可读取：

```json
{
  "sourceBlend": "BGA/blender/Firefly_the_Summer_Opening_v03.blend",
  "packageManifest": "BGA/exports/Firefly_the_Summer_Opening_v03/manifest.json",
  "sourceSha256": "...",
  "timeOffsetSeconds": 0,
  "enabled": true,
  "followCamera": true
}
```

旧谱面的 `sceneObjects`、`effectClips`、`cameraMotionClips` 会继续反序列化，但新工作流不再创建、编辑或播放这些内容。
