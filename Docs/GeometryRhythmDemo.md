# Geometry Rhythm Demo：使用与开发说明

本 Demo 对应浅色抽象几何参考图，使用 Unity 2022.3.62f3c1、Built-in Render Pipeline 和 GameObject。路径、音符、环境都是实时三维网格，不使用背景视频。运行时生成场景，因此编辑模式中主场景只有启动对象；按 Play 后可在 Hierarchy 查看全部几何体和 Note。

## 1. 启动

### Unity 编辑器

1. 等待 Unity 导入 `Assets/RhythmDemo` 并完成编译。
2. 打开 `Assets/RhythmDemo/Scenes/GeometryRhythmDemo.unity`。也可通过菜单 **Geometry Rhythm → Open Demo Scene** 打开。
3. Game 视图分辨率菜单取消 **Low Resolution Aspect Ratios**，选择 **Full HD (1920×1080)**。不要只选宽高比并打开低分辨率预览。
4. 按 Play 先进入标题页，点击 **TAP TO START** 打开真实 JSON 选曲页；多首歌时左右滑动封面切换。
5. **PLAY** 开始手动游玩，**PREVIEW** 开始自动演示；歌曲结束自动进入结算。模式、声音、重开与返回选曲统一放入左上暂停按钮打开的面板。

三页 UI、页面流程、游戏改名、新曲接入和结算规则见 [前端 UI 说明](FrontendUI.md)。

原来的 `SampleScene.unity` 和 `test.cs` 不参与 Demo。新场景不会自动覆盖旧场景，也不会自动替换用户当前打开的场景。

### Windows 可执行版本

运行 `Builds/GeometryRhythmDemo/GeometryRhythmDemo.exe`。复制给别人时需一起拷贝整个目录，不能只拷贝 exe。这是桌面测试播放器，游戏 UI 已按横屏移动端触控设计；尚未生成 Android／iOS 安装包。键盘快捷键仅用于编辑器／桌面调试，不显示在玩家 UI 中。

| 操作 | 功能 |
| --- | --- |
| 鼠标左键／触屏按下 | Tap；同时也可作为 Drag 接触的开始 |
| 按住鼠标并移动／保持手指接触 | Drag |
| 左上暂停按钮／Space／Esc | 暂停、继续 |
| 暂停面板 MODE／调试键 A | 切换自动演示与手动游玩，并重开 |
| 暂停面板 RETRY／调试键 R | 从头开始 |
| 暂停面板 SOUND ON/OFF／调试键 M | 静音开关 |
| 左／右方向键 | 向后／前跳转 8 秒，开启新的练习区间 |

失去窗口焦点时自动暂停，返回后需要手动继续。点击界面按钮不会触发全屏 Note。触点从 UI 上开始时，该次接触不会穿透到游戏判定。

### 清晰度与高帧率

- Game 面板较小时，Unity 会缩小 1080p 画面以容纳预览，这是显示缩放，不是降低游戏内部渲染分辨率。鼠标放在 Game 面板内按 **Shift+Space** 可最大化／还原；检查细节时使用 **Scale = 1x**，不要放大低分辨率画面。高 DPI 屏幕以 Unity 显示的缩放值为准。
- Game 分辨率菜单中的 **VSync (Game view only)** 保持关闭。本机已关闭低分辨率预览，并选用 Full HD；这些属于编辑器本地设置，不会随 Git 传给其他开发者。
- 桌面运行时使用 `Application.targetFrameRate = -1`、`QualitySettings.vSyncCount = 0`，取消原来的 120 FPS 上限；`OnDemandRendering.renderFrameInterval = 1` 保证每帧渲染。
- 移动端不能照搬 `-1`，否则 Unity 默认只运行 30 FPS；代码改为请求当前屏幕刷新率（无有效值时回退 60）。尚未做移动端真机验证，也不保证系统一定开启设备的最高刷新率模式。平台差异见 [Unity 官方说明](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Application-targetFrameRate.html)。
- 保留 4× MSAA，关闭相机动态分辨率；未更改背景、雾、路径和 Note 外观。远景的浅雾是空间表现，不是整屏模糊滤镜。
- Windows 构建默认 1920×1080 窗口、非 Development 模式。实际帧率受 CPU/GPU、驱动限帧、温度和编辑器开销影响；解除上限不等于保证某个 FPS。编辑器可用 Game 右上角 **Stats** 查看当前渲染统计，性能评估以独立程序为准。
- 不限帧会增加功耗与发热，关闭垂直同步也可能出现撕裂。该设置优先满足当前高帧率测试需求；正式版本适合再提供不限帧／120／144／240 和垂直同步选项。

## 2. 四种音符

Note 的两个独立维度是 `action` 和 `protectedNote`，没有把它们硬编码成四套对象。`tap` 用蓝色环，`drag` 用白色环。保护套额外显示薄外圈和很淡的圆形面。

| action | protectedNote | 外观 | 空间和输入规则 |
| --- | --- | --- | --- |
| tap | false | 蓝色圆环 | 规定时间内按下圆环覆盖的区域，包括中间的空心部分 |
| tap | true | 蓝色圆环＋保护套 | 规定时间内在游戏区域任意位置按下 |
| drag | false | 白色圆环 | 判定时间到来时有持续触点位于圆环覆盖区域 |
| drag | true | 白色圆环＋保护套 | 判定时间到来时屏幕游戏区域有持续触点即可 |

Drag 不要求必须移动，也不要求在判定瞬间重新按下。提前接触需要保持到目标时间；提前碰一下就松开不算命中。多个同刻的全屏 Drag 可共享一个持续触点。

Tap 必须有新的按下事件。一直按住不能连续判定 Tap。一次按下最多消费一颗 Tap：同时有直接点中的普通 Tap 和保护套 Tap 时，优先普通 Tap；同优先级选择时间误差较小的音符，最后按稳定的谱面顺序决定。

全屏判定放宽的是位置约束，不取消时间窗口，也不会由按住自动触发保护套 Tap。

### 时间与计分

- Tap 的 Perfect：时间误差不超过 60 ms。
- Tap 的 Good：误差大于 60 ms、不超过 150 ms。
- Drag 在目标时刻至其后 150 ms 接受接触；前 60 ms 为 Perfect，随后为 Good。
- 超过目标时刻 150 ms 仍未命中则 Miss。
- Perfect 权重 1，Good 权重 0.65，Miss 权重 0。
- 分数为当前完整歌曲／练习区间可判定音符数归一化到 1,000,000。
- 准确率基于已经判定的音符，开头尚未判定时显示 100%。
- Miss 清空 Combo，Perfect 和 Good 增加 Combo。

路径近端两侧的小短线标出当前判定位置。Note 飞到此处时操作。它们属于路径标记，不属于 Note 皮肤。

## 3. 演示内容

歌曲为程序合成的原创演示音轨，不依赖外部音乐资源。总长 64 秒，120 BPM，204 颗音符。

| 时间 | 路径数量 | 演出 |
| --- | --- | --- |
| 0–12 秒 | 4 | 浅色开放几何场景、基础出张 |
| 12–20 秒 | 2 | 双路径与轻微侧向运镜 |
| 20–28 秒 | 1 | 单路径，镜头绕场景侧移 |
| 28–40 秒 | 4 | 曲线空间与倾斜转场 |
| 40–52 秒 | 8 | 镜头拉远、八路径展开 |
| 52–60 秒 | 2 | 返回双路径 |
| 60–64 秒 | 1 | 收尾 |

新路径可提前进入预告区，旧路径有退场缓冲，因此过渡时实际可见的线条数量可能暂时多于 HUD 显示的当前段落路径数。这是为了避免带有预读 Note 的路径突然出现／消失。

## 4. 文件结构

```text
Assets/RhythmDemo/
  Scenes/GeometryRhythmDemo.unity
  Runtime/
    ChartData.cs               JSON DTO、验证、BPM 时间换算
    SongClock.cs               DSP 播放时钟、暂停／跳转、演示音轨
    JudgementEngine.cs         四种判定、状态、计分、投影命中
    SpatialDirector.cs        世界路线、路径、镜头绝对时间求值
    SceneVisuals.cs           网格、材质、Note 池对象、路径、几何场景
    DemoHud.cs                UGUI 界面与按钮
    RhythmDemoController.cs   组装系统、输入、调度、调试入口
  Resources/
    Charts/geometry-demo.json
    Shaders/Flat.shader
    Shaders/Sleeve.shader
    Shaders/Sky.shader
    Materials/StandardReference.mat
  Editor/
    DemoBuildTools.cs         生成／打开场景、批处理构建
    DemoValidation.cs         规则与空间测试
Docs/
  GeometryRhythmDemo.md       本文
  Verification.md            实测结果与截图
```

## 5. JSON 格式 v1

默认文件：`Assets/RhythmDemo/Resources/Charts/geometry-demo.json`。当前所有 Note 均逐颗显式存储，不在播放过程中随机生成谱面。

外部 Windows 谱面可通过命令行加载：

```powershell
.\GeometryRhythmDemo.exe -demoChart "D:\Charts\my-chart.json"
```

这是桌面文件入口；移动端文件选择和导入 UI 尚未实现。编辑器也可在启动组件的 `Chart Override` 中指定另一个 JSON TextAsset。

### 顶层字段

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| version | int | 当前为 1；不支持的版本会拒绝加载 |
| title / author | string | 作品元数据 |
| ticksPerBeat | int | 每拍 Tick 数，示例为 480 |
| endBeat | number | 作品结束拍，示例 128 |
| approachSeconds | number | Note 提前出现时间，示例 4.5 秒 |
| audioResource | string | Unity Resources 音频路径，不带扩展名；空字符串使用合成音轨 |
| audioOffsetSeconds | number | 谱面时间零对应的音频位置；正数从音频更后面起播，负数先留静音 |
| tempos | array | 从 Tick 0 开始的 BPM 段，按 tick 递增 |
| paths | array | 稳定路径 ID 列表 |
| sections | array | 按起始拍排序的路径布局段 |
| cameraKeys | array | 按拍排序的镜头关键帧 |
| notes | array | Note 列表；加载后按 tick 和 ID 排序 |

### Note 示例

```json
{
  "id": "n0001",
  "tick": 3840,
  "pathId": "p0",
  "action": "tap",
  "protectedNote": true
}
```

480 Tick／拍时，3840 Tick 是第 8 拍；120 BPM 时是第 4 秒。它是一颗在 `p0` 路径上出现的全屏 Tap。

ID 在一份谱面中必须唯一。`pathId` 必须存在，且 Note 的判定时刻该路径必须属于当前布局。动作只接受小写 `tap` 和 `drag`。保护套为显式布尔值。

### BPM 变化

```json
"tempos": [
  { "tick": 0, "bpm": 120 },
  { "tick": 15360, "bpm": 150 }
]
```

第 32 拍切换到 150 BPM。所有 Note、镜头和段落都通过同一份 TempoMap 换算到秒。当前内置合成音乐固定为 120 BPM；修改 BPM 后要换入匹配的歌曲，不能指望演示合成器自动重新编曲。

### 布局段

```json
{
  "startBeat": 24,
  "name": "TWIN PASSAGE",
  "placements": [
    { "pathId": "p0", "x": -6, "y": -1, "bend": -3, "lift": 3 },
    { "pathId": "p1", "x": 6, "y": -1, "bend": 4, "lift": 12 }
  ]
}
```

本段持续到下一段开始，最后一段持续到 endBeat。x、y 是路径近端在世界路线局部坐标中的偏移；bend 控制左右弯曲；lift 控制路径中远段的拱起高度。单位为 Unity 世界单位。段落间的布局用 1.25 秒平滑过渡。

路径数量由 placements 长度决定。当前演示最多八条，代码没有 `new Path[4]` 之类的固定数量限制。不过路径更多时需要重新检查布局、输入密度和设备性能。

v1 使用参数化曲线模板，而非任意控制点文件。这是 Demo 的明确边界。以后增加贝塞尔控制点时，建议增加新的路径形状字段和格式版本，保留现有 ID 与时间语义。

### 镜头关键帧

```json
{
  "beat": 80,
  "orbit": 0,
  "roll": 0,
  "distance": 38,
  "height": 8,
  "fov": 59
}
```

orbit 是围绕观察点的水平角；roll 为画面倾斜角；distance、height 是相对观察点的位置参数；fov 是垂直视野角。关键帧之间用平滑插值，绝对歌曲时间决定当前结果。

当前镜头不依赖 Cinemachine，避免增加包依赖。后续可增加 Cinemachine 适配器，但不要让新的镜头系统自行使用一套不受 SongClock 管理的时间。

## 6. 运行时架构

启动顺序为：读取并验证 JSON → 构建 TempoMap → 准备网格／材质／对象池 → 生成世界几何 → 准备音频 → 创建 HUD → DSP 预定播放。

每帧先取歌曲时间，求值最终镜头、场景、路径、Note；随后读取输入并计算投影命中，再更新判定和 UI。这保证当前 Demo 的显示与命中使用同一帧相机姿态。

### 时间

`SongClock` 保存歌曲时间与 DSP 时间之间的锚点。暂停会冻结歌曲时间并停止音源，继续时从对应采样点重新预定播放。跳转重建时间锚点；Note 的运动从目标时间直接求值，避免累计 deltaTime 漂移。

`inputOffsetMilliseconds` 是输入校准值，正值会让输入对应的判定时间更晚。它与谱面的 `audioOffsetSeconds` 用途不同。

### 空间

`SpatialDirector.Route` 定义当前演示的弯曲世界路线。摄像机沿路线前进，路径根据深度采样这条世界路线，再叠加自身的 x/y/bend/lift。Note 的深度由距离判定的剩余时间计算。

当 `songTime == hitTime` 时，Note 必然位于 `NearDepth`，与该路径的判定点重合。Note 的朝向由路径切线与世界的上方向计算；它有真正的厚度，也会呈现椭圆透视。

这里使用平缓、向前延伸的世界路线，尚未实现任意三维回环的平行运输标架。完全垂直的路径或 360° 扭转，应当在后续控制点系统中补充稳定的方向框架。

### 输入与命中

`NoteProjection` 把圆盘边缘的 24 个点投影到屏幕，以凸多边形测试点击范围，再增加约屏幕短边 1.3%（限制在 8–20 px）的容差。因此点中空心中心也有效，Note 不需要朝向相机。

鼠标和多点触控使用 Unity 旧输入 API，适合现有项目的默认配置。当前时间精度受到每帧输入采样限制；正式移动端版本建议增加带时间戳的输入适配层，以及高速 Drag 的轨迹段检测。尚未宣称实现毫秒级设备延迟测量。

### 对象与资源

Note 对象池按实际谱面的预读窗口峰值预热；音符复用共享网格与材质。命中反馈也复用对象。运行时生成的资源由 VisualLibrary 管理，离开场景后销毁。

几何环境使用固定种子 20260917 生成。大型倾斜块体、三角碎片、地面切面与路径处于同一坐标系。当前场景为该 64 秒 Demo 预生成 30 段几何，并按距离启停；它不是任意长歌曲的正式资源流式加载器。

### 预览与成绩

向前跳转时，之前的 Note 标记为 Skipped，当前练习区间重新计分。倒退也会清除并重新建立成绩。自动演示始终标记 AUTOPLAY PREVIEW，不能当作手动成绩。

## 7. 后续独立制谱器

建议制谱器与游戏共用 ChartData、TempoMap、JudgementEngine 的逻辑以及 SpatialDirector 的求值规则，分开各自 UI。JSON 不保存 GameObject 实例引用，不保存 UnityEditor 对象。

制谱器第一批功能可包括音乐波形、节拍网格、四类 Note 编辑、布局段编辑、镜头关键帧、任意时间预览、保存与格式验证。后续再扩展任意曲线控制点、独立场景资源、路径分支和更复杂演出轨。

需要保持的约定：稳定 ID、格式版本、统一 Tick／BPM 换算、Tap／Drag 与 protectedNote 独立、按绝对时间求值、预览跳转不累积历史状态。

## 8. 构建与检查

在 Unity 菜单选择 **Geometry Rhythm → Validate Chart and Rules** 可运行不依赖额外测试包的规则检查。

批处理入口：`GeometryRhythm.Editor.DemoBuildTools.ValidateAndBuild`。它会创建 Demo 场景、运行检查并构建 Windows 非开发版。请在独立验证副本中执行，避免与已经打开的 Unity 项目争用。

独立播放器可执行真实渲染截图与自动判定自检：

```powershell
.\GeometryRhythmDemo.exe -demoSmoke -demoCapture "D:\Captures" -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile "D:\Captures\player.log"
```

它会保存六个时间点的 PNG 及 player-smoke.txt，随后退出。截图包含实际运行的三维场景和 HUD。

## 9. 当前范围与限制

已包含四种 Note、JSON 载入、真实几何、动态路径数量、基础曲线运镜、DSP 驱动、暂停／练习跳转、自动演示、手动鼠标与多触点输入、基本反馈和完整计分。

当前已接入标题、选曲和结算菜单，尚未实现成绩存档、正式场景资源导入、任意长度流式关卡、复杂路径图、完整的遮挡／可读性自动分析。独立制谱器与自定义舞台路径的现状见 [制谱器说明](GeometryChartStudio.md)。移动端真机延迟、多指手感和热性能仍需后续实机测试。

Note、路径和 HUD 没有大面积后处理 Bloom，优先保持清晰可读。菜单、结算和 HUD 已更新为深色青紫赛博朋克视觉；Note 形状、玩法和三维舞台配色保持不变，详见 [UI 说明](FrontendUI.md)。Demo 是可修改的实时实现，不声称逐像素复刻任何商业游戏。
