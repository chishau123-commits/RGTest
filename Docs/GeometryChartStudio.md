# Geometry Chart Studio

这是游戏共用空间内核之上的第一版电脑端制谱器。它使用普通 Unity Runtime API，而不是只能在 Unity 编辑器中运行的 `EditorWindow`，因此同一个场景可以构建为 Windows 桌面程序。

## 启动与构建

- Unity 菜单：`Geometry Rhythm → Chart Studio → Create or Open Desktop Editor`
- Windows 构建：`Geometry Rhythm → Chart Studio → Build Windows Editor`
- 构建结果：`Builds/GeometryChartStudio/GeometryChartStudio.exe`
- 最新“稳定 Playhead 十字与可选相机段曲线”版本已覆盖到固定入口 `Builds/GeometryChartStudio/GeometryChartStudio.exe`，并保留 K0→K1 固定端点修正、黄球居中及编辑视角不跟随，后续使用此入口即可。`GeometryChartStudio-EditingPlayback` 保留较早的编辑播放版本，`GeometryChartStudio-StaticNotes` 和 `GeometryChartStudio-Timeline` 为更早版本。
- 可以用 `-chart "D:\Charts\example.json"` 指定启动时载入和保存的谱面路径。

播放器自检入口：`-chartEditorSmoke -chartEditorCapture "D:\Capture"`。程序会保存界面截图和 `chart-studio-smoke.txt` 后自动退出。

额外加 `-chartEditorInteractionSmoke` 会运行坐标轴连续拖拽、撤销/重做、飞行切换、相机落点、路径偏移关键点和 JSON 往返测试，保存 `interaction-checks.txt` 及三个工具页截图。测试不写入用户草稿。

没有 `-chart` 参数时，会尝试载入 `Application.persistentDataPath/geometry-chart-draft.json`。文件操作统一在右上角完成：**Files** 展开 **New / Load / Save As**；**Load / Save As** 使用 Windows 文件选择窗口，支持中文路径，Save As 会提示覆盖已有文件。**Save / Ctrl+S** 保存当前文件，新建的未命名谱面首次保存会让你选位置，`Ctrl+Shift+S` 另存为。左侧只显示文件名，不再通过手写路径切换文件。新建和载入前若有未保存修改，会提供 Save & Continue / Discard changes / Cancel；取消或载入失败不会替换当前谱面。

程序默认以可调整大小的 `1440×900` 窗口启动，不会强制全屏。右上角 **Settings** 可以在 100%、125%、150%、175% 之间调整界面缩放，选择会在下次启动时保留。

右上角只保留 **Files / Settings / Save**。使用说明移到 Settings → Charting guide。界面滚动条改为无箭头、无金属高光的细轨道，滑块绘制宽度约 6 px，保留 12 px 的抓取区域；地图参数滑条也采用扁平样式。

## 当前工作流

1. **Stage**：以 Catmull–Rom 样条绘制舞台主轴。单击球形控制点，再拖动红 X、绿 Y、蓝 Z 坐标轴；Y 可以直接改变高度，不再限制在地面。拖中心小方块可在视图平面内移动。Shift+单击地面添加点；也可用 Insert 在两个点之间插点。按 F 聚焦选中点，方便编辑远处的点。一次连续拖拽对应一次撤销。
2. **Camera**：黄色球表示**即将新增的相机位置**，不是注视目标。把球移到希望的位置，移动播放头，然后按 **Add Key at yellow marker**。新关键帧准确落在球的位置，朝向与当前视图一致；同一拍点再次添加会替换原关键帧。选中已有相机点也可用 XYZ 轴移动；Move selected key to marker 将选中点移到球的位置。

相邻相机点 K 之间显示橙色细线，线段中点的小按钮表示这一段的移动曲线。点击可选择 **Linear / Ease In / Ease Out / Ease In-Out / Smoother**；曲线属于前一个 K 点到后一个 K 点的区段，同时作用于位置、朝向、FOV 和 Roll。旧谱面没有曲线字段时保持原来的 Ease In-Out，不需要迁移。
3. **Paths**：选路径，定位播放头拍点（可在 Beat 输入），按 F 聚焦横截面。拖红 X、绿 Y，或拖中心方块同时改变二维偏移。这个平面始终垂直于舞台路线。操作自动在当前拍点建立偏移关键点；换到另一个拍点继续设置，路径在关键点之间平滑过渡，其他关键点不会整体跟着移动。Previous/Next key 跳转关键点，Delete offset key 删除当前关键点。布局段仍用于原有谱面的路径布局管理。
4. **Notes**：选择 Path、Tap/Drag 和保护属性，在播放头放置音符。**编辑模式显示全部音符**：每个 Note 固定在自身拍点对应的路径判定位置，拖播放头、播放音乐或切换工具都不会让它流动或消失。Note 路径也完整、静止地显示。只有修改音符拍点、路径或舞台数据才会改变其固定位置。点击场景中的音符或时间轴上的音符，再按 **F / Focus note** 聚焦；切到 Camera 后可继续用这个视角布置相机。
5. **Map**：用 Seed、走廊宽度、密度和高度变化生成确定性的抽象几何地图。主轴两侧保留安全走廊。

顶部标签只显示 Stage / Camera / Paths / Notes / Map；快捷键 `1`–`5` 仍可切换工具，`Ctrl+S` 保存，`Ctrl+Z/Ctrl+Y` 撤销/重做，Delete 删除当前可删除对象。输入文本时不触发这些场景快捷键。左侧面板内容过长时可以滚动。

## 多轨时间轴

底部现在是类似剪辑软件的操作区。拖动最上沿的分隔条可调整高度，松手后会记住高度；3D 视口和左侧面板同步调整，不能把视口完全挤没。

| 视图 | 下方轨道 | 操作 |
| --- | --- | --- |
| Stage | 音频、舞台控制点、X/Y/Z 曲线 | 点击控制点定位并聚焦；时间由舞台弧长和速度决定，位置仍在 3D 里调整。歌曲结束后的路线尾段也显示。 |
| Camera | 音频、相机关键帧、FOV 曲线 | 拖关键帧改时间，双击空白关键帧轨道在黄球位置添加。第 0 拍关键帧固定。 |
| Paths | 音频、Layout Sections 区段、各路径的 Offset 关键点 | 原左侧 Layout Sections 已移到这里。点片段选区段；拖片段左沿改开始时间；下方工具行可新建、改名、删除区段。偏移关键点独立按路径排列，可拖动改时间。 |
| Notes | 音频、每条路径自己的音符轨道 | 点击选音符，拖动改时间；双击空白音符轨道添加。T 表示 Tap、D 表示 Drag，琥珀色表示保护音符。 |

公共操作：

- 点击或拖动时间尺移动播放头，同时显示分秒和拍点。
- 放大时间轴后，把播放头或正在拖动的关键帧/音符移到左右边缘，可视范围会平滑横移；越靠近边缘速度越快。按住不动也会继续滚动，离开边缘或松手即停止，不会越过歌曲范围。正常播放到可视边缘时也会平滑跟随。
- 鼠标在下半部分操作区时，**Ctrl+滚轮**以鼠标指向的时间为中心缩放，范围 1–64 倍，支持各界面缩放比例；在 3D 视图区不会触发。`+ / -` 以播放头为中心缩放，`Fit` 查看全部。底部横向滚动条平移可视范围；Shift+滚轮也可横向平移，普通滚轮上下浏览轨道。
- `Snap` 循环切换 1 拍、1/2 拍、1/4 拍和关闭吸附。关闭时仍精确到谱面 tick。
- 时间拖拽支持整次撤销/重做。相邻相机关键帧、布局边界和偏移关键点不会互相跨越或重叠；音符可以交换先后，仍保持各自 ID。
- 当前波形来自程序实际播放的演示音频，标记为 DEMO AUDIO；这里没有假设已导入用户歌曲，也没有新增音频导入功能。

时间轴自检：在播放器自检参数中额外加 `-chartEditorTimelineSmoke`，会生成 `timeline-checks.txt` 和四个视图截图。截图测试需使用可见窗口。测试示例内容不会自动保存到用户谱面。

## 编辑态播放辅助

- 黄色 **NEW KEY POSITION** 固定在当前 **3D 视口中心**，与播放时间、Playhead 和已有相机节点无关。调整 UI 缩放、时间轴高度、切换 Caps 或手动转向后仍保持居中。
- **已撤销黄球和编辑器视角的全部自动跟随**。编辑态播放、暂停、拖动时间轴、跨相机节点、重播以及修改已有相机节点，都不会自动平移或旋转观察视角，也不会把黄球移到节点/Playhead 附近。
- 视角只响应手动环绕、平移、飞行和 F 聚焦。**Add Key at yellow marker** 使用中心黄球对应的世界坐标，不使用编辑相机本身的位置。
- **紫色轨道**是当前时间的动态路径，稳定朝向编辑视角的紫色十字与 PLAYHEAD 标签标出当前判定横截面；在编辑态播放或拖动时间轴时更新。十字由两根独立细线绘制，不继承路线坐标架的细小旋转。原有绿色/选中黄色常驻路径，以及全部静止 Note 均保留不动。
- 这些辅助只用于编辑模式；Preview 使用实际相机关键帧补间与音符流动，不显示紫色辅助线。

编辑播放自检参数：`-chartEditorPlaybackSmoke`，输出 `editing-playback-checks.txt` 和 `editing-live-playback.png`，覆盖边缘连续滚动、帧率/UI 缩放、暂停与撤销、变速及三种相机节点数据、静态对象保持、Preview 隔离。

## 预览

- 按 **F5** 或底部 **Preview**，从当前播放头自动播放；在歌曲结尾进入时从头开始。
- 隐藏左侧编辑面板、舞台/相机辅助线、控制点、黄球和坐标轴，让场景占满窗口内的预览区域；不切换全屏。
- 相机使用游戏的 `SpatialDirector.EvaluateCamera`；音符朝向使用 `SpatialDirector.NotePose`，显示路径、判定位置和地图。Tap 为蓝色、Drag 为绿色、保护音符为黄色，到达判定时间后自动消失。
- **Space** 播放/暂停；**Restart** 从头播放。时间轴仍可拖动播放头、缩放和调整高度，但不能新增或修改关键帧和音符。
- **Esc / F5 / Exit Preview** 返回编辑并暂停，恢复进入前的编辑相机位置、朝向和 FOV，保留预览结束的播放头位置。
- 这是空间与时序的可视预览，不执行点击判定或评分。目前仍使用 DEMO AUDIO，没有新增外部歌曲导入功能。
- **只有 Preview 中 Note 会流动**，并按接近/判定时间显示和消失；退出后恢复所有 Note 的静止编辑位置。

相机轨道包含世界坐标节点（Add Key 的默认格式）时，整条轨道使用固定端点补间：旧式 K0/route 节点先按各自拍点解析为固定世界位置，不再随当前播放时间漂移；位置保留缓入缓出，朝向使用连续四元数插值，接近垂直俯视时不会中途切换 up 轴。最后一个节点之后保持该节点姿态。FOV 与显式 Roll 仍插值。Camera 页会显示 `Fixed endpoints / smooth rotation`。

这只修正已编排的玩家相机轨道，不会恢复黄球或编辑视角的自动跟随，也不需要重新保存或改写已有谱面。

工作区回归测试参数：`-chartEditorWorkspaceSmoke`，覆盖 Ctrl+滚轮事件、缩放锚点/边界、菜单输入隔离、文件另存/载入失败保护、预览相机与只读轨道、退出视角恢复，输出 `workspace-checks.txt` 和三个截图。文件测试仅写入指定自检目录的唯一子目录。

音符编辑态回归测试参数：`-chartEditorNoteSmoke`，覆盖全部工具页与播放头首/中/尾位置、变速和两种路径数据、静止路径、播放时不重建 Note、音符选择/聚焦、Preview 流动与判定位置一致、退出还原、改拍点和撤销；输出 `static-note-checks.txt` 及同一视角下首尾播放头截图。

## 视角操作与 Caps Lock

- **Caps OFF**：右键拖动环绕视角，中键拖动平移，F 聚焦。空格播放/暂停。
- **Caps ON**：切换为创造飞行模式。鼠标控制朝向，WASD 水平移动，空格上升，Ctrl 下降，Shift 加速。进入模式时鼠标在视图区会直接捕获，否则在视图区按右键开始飞行。
- **Esc**：释放鼠标，以便操作左侧面板、Add Key 和时间轴；仍保持 Caps ON。再次在视图区按右键恢复飞行。失去窗口焦点也会释放鼠标。
- 关闭 Caps 后回到环绕编辑，保持切换前的相机位置和朝向；舞台、相机和 Paths 页面共用同一套模式。
- 视图区顶部持续显示 CAPS ON/OFF、当前模式及鼠标是否释放；相机预览期间暂停导航。
- **已取消滚轮缩放**。滚轮只用于界面滚动，不再影响 3D 相机。远近移动用飞行，或 F 聚焦。

相机页的黄球只表示视口中心的放置点；播放和暂停使用同一规则，没有 Playhead/相机节点跟随。Camera 页按 F 可让中心放置点对准选中节点；点击 Add Key 保存显示的黄球位置。

例：在 Paths 的第 0 拍设置 X=-4、Y=0，第 16 拍设置 X=0、Y=4，第 32 拍设置 X=4、Y=0，即可得到沿场景主路线逐渐抬升再下降的 Note 路径。

## 数据兼容

谱面仍为 JSON v1。`stagePath` 和 `map` 是可选字段；游戏运行时仍可读取旧谱面的公式路线和固定地图。没有任何 `useWorldPose` 节点的旧式相机轨道保留原有动态路线求值；包含世界节点的轨道整体采用固定端点补间，混合类型节点各按自身时间解析。新捕获的关键帧仍使用 `useWorldPose`、`worldPosition`、`worldTarget` 保存精确的世界位置和注视点。

可选的 `paths[].offsetKeys` 保存 `{tick,x,y}`。tick 经过 TempoMap 换算为舞台距离（加判定面 NearDepth），沿该距离平滑插值，游戏与编辑器使用同一求值器。空轨道继续沿用原来的 section placement/bend/lift。首次编辑偏移时会以布局段和结尾的偏移初始化边界关键点；启用轨道后由关键点直接控制 XY，不再叠加旧的远端 bend/lift 或收缩。新增格式需要使用本次更新后的游戏源码构建游戏；此前已经打包的旧游戏不会识别新字段。

Windows 程序及测试产物只放在被 Git 忽略的 `Builds/`，不会因为位于项目内就上传到 GitHub。

当前版本是空间创作闭环 MVP，下一阶段适合补充音频文件浏览器、波形缓存、拍号/变速编辑、框选与批量 Note 操作，以及模块化地图 Prefab 库。

## 场景、动效与运镜素材库

工具栏现在按制作顺序提供 **Scene / Stage / Paths / Notes / Camera / Effects / Motion / Map**。Scene 是铺面起点：先搭建完整世界，再布置舞台路线与 Note 路径。三个视觉页面都使用同一种复用模型：内置素材只读；铺师将素材放入谱面后可修改实例，并可另存为个人素材、更新个人素材、删除个人素材，或通过 JSON 素材包导入/导出。实例保存完整参数，旧谱面不会因为本机素材库被修改或删除而损坏。

### Scene

- 内置 Architectural Block、Light Pillar、Floating Crystal、Kinetic Rotor、Media Screen、Rhythm Gateway。
- Place selected asset 把物体放到当前视口中心标记，可编辑位置、旋转、缩放以及 none / float / rotate / pulse / pendulum 动画。
- Import OBJ 支持 Wavefront OBJ，Import image 支持 PNG / JPG / JPEG，Import BGA video 支持 MP4 / WebM。视频平面由歌曲时间驱动，暂停和 Seek 后仍保持同步；导入结果都可保存到个人场景素材库。
- 当前导入素材记录原文件路径；分享给其他电脑前必须同时携带原文件。发行资源收集器是后续独立步骤。

### Effects

- 内置 Flash、Bloom Pulse、Color Wash、Fog Dive、Light Sweep、Scene Pulse、Judgement Shockwave、Particle Burst、Speed Lines、Glitch Cut。
- 在播放头添加后形成有持续时间的时间轴片段，可拖动改时间，并在左栏修改持续拍数、强度、频率和目标对象。
- Target selected scene object 可把支持目标的效果绑定到 Scene 物体；没有指定对象时作用于整个场景或画面。

### Motion

- 运镜是基础 Camera 关键帧之上的非破坏性叠加层。内置 Impact Shake、Forward Punch、Roll Accent、Orbit Sweep、Dolly In-Out、FOV Pulse、Blank Custom Motion。
- Motion 片段可拖动改时间、调整长度/强度/频率；F5 预览基础镜头与运镜叠加后的最终结果。
- 每个片段都有归一化的自定义关键点，包含本地位移、Pitch/Yaw/Roll 和 FOV 偏移。铺师可在片段内播放头位置新增关键点，再保存或更新为个人运镜素材。
- 运镜和所有物体动画都直接由歌曲时间求值，Seek、暂停、低帧率和重复预览保持确定性。

个人素材库保存在 `Application.persistentDataPath/GeometryChartStudio/visual-library.json`。谱面新增可选字段 `sceneObjects`、`effectClips`、`cameraMotionClips`；旧 v1 JSON 无需迁移。

视觉素材回归入口：`-chartEditorSmoke -chartEditorVisualSmoke -chartEditorCapture <目录>`。报告为 `visual-authoring-checks.txt`，并输出 Scene、Effects、Motion 三页截图。
