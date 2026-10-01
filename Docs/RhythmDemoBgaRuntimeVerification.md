# 游玩运行时播放视频 BGA：验证记录

> 对应 [VideoBgaBranchingPlan.md](VideoBgaBranchingPlan.md) 的**阶段 0b**（游玩运行时能播 BGA）。
> 素材：[`BGA/goodtek60/`](../BGA/goodtek60)（用户指定的 BGA，RIFE 插帧到 60fps，gitignore）。
> 证据截图：[demo-bga-gameplay.png](Screenshots/demo-bga-gameplay.png)。

## 0. 结论速览

| 问题 | 结果 |
|---|---|
| 游玩运行时能播 `chart.videoBga` 吗 | **能**。`-demoBgaSmoke` 自检 **`RESULT=PASS CHECKS=13`**，退出码 0 |
| 画面确实有 BGA 吗 | **有**。相机渲出的画面中心灰度 0.907；截图上 BGA 出现在世界里 |
| 有没有黑帧 | **没有**（五个采样点 `black=0`，画面各不相同） |
| 一个必须知道的事实 | **远平面视频会被运行时自己的世界几何体挡住**——它是"天空背景"，不是全屏 BGA（见 §3） |

## 1. 落地的东西

| 位置 | 内容 |
|---|---|
| `Runtime/RhythmDemoBga.cs`（新） | `RhythmDemoController` 的 partial 分片：加载 / 每帧定位 / 卸载 / `-demoBgaSmoke` 自检 |
| `Runtime/RhythmDemoController.cs` | 类声明改 `partial`；`Initialize()` 里 `LoadVideoBga(chartPath)`；`Update()` 两个分支各挂一次 `UpdateVideoBga`；`OnDestroy()` 里 `DisposeVideoBga`；`-demoBgaSmoke` 直进玩法（跳过前端）并分发到 `RunBgaSmoke` |

**为什么这里用 `Activate()`（相机远平面），而 Thart 必须用 `ActivateAsTexture()`？** 游玩端有真实的 3D 世界相机（`demoCamera`，`RhythmDemoController.cs:122-126` 代码新建），远平面把视频放在一切几何体之后，正是"BGA 是环境"的形态。Thart 的屏幕绘制权归 `OnGUI` 且先整屏铺不透明 backdrop，远平面会被盖住，只能走贴图。两份宿主各取所需，`VideoBgaRuntime` 两个入口并存。

每帧定位：菜单里用预览时间（`UpdateVideoBga(preview,false)`），玩法里用歌曲时钟（`UpdateVideoBga(time,!clock.Paused)`）；偏移取自 `chart.videoBga.timeOffsetSeconds`。探针在跑时 `videoBgaProbe` 让位，避免和自检抢 seek。

## 2. 自检覆盖了什么

`-demoBgaSmoke` 走的是运行时的真实路径（`-demoChart` → `ChartLoader.Parse` → `videoBga` → 相机），并按仓库既有惯例用**离屏相机渲染**取证，不是 `ScreenCapture`（全 Assets 没有一处用它；隐藏的 Windows player 不保证呈现 backbuffer，`SmokeCapture:573-581` 的注释写了原因）。

```
CHART=...\BGA\goodtek60\runtime-chart.json
PASS chart.videoBga 指向的包能加载并解码
STATUS=loaded manifest.json 1920x1080 60fps 124.100s
VIDEO=1920x1080 fps=60 duration=124.100s
SAMPLE t= 12.410 time= 12.417 frame=  745 mean=0.209
SAMPLE t= 37.230 time= 37.233 frame= 2234 mean=0.584
SAMPLE t= 62.050 time= 62.050 frame= 3723 mean=0.577
SAMPLE t= 86.870 time= 86.867 frame= 5212 mean=0.343
SAMPLE t=111.690 time=111.683 frame= 6701 mean=0.076
PASS 五个采样点都没有黑帧（black=0）
PASS 五个采样点画面各不相同（distinct=5/5）
PASS 相机渲出的画面中心不是黑的（grayscale=0.907）
SHOT t=32.000 centreGray=0.907 cornerGray=0.810
RESULT=PASS CHECKS=13
```

`frame` 一栏同时验证了定位精度：`t=62.050` → `frame=3723`，正是 62.05×60 的取整。

## 3. 一个对"完全融入"很要紧的发现

截图里 BGA 只出现在**地平线以上**，下方是运行时自己的浅色世界（雾色地面 + 珍珠色立柱），音符浮在两者之间。

原因很直接：`CameraFarPlane` 只是把视频画在相机远平面上，**任何比远平面近的几何体都会挡住它**。运行时有自己的 `StageVisuals` / 舞台地面 / 立柱，所以 `chart.videoBga` 在游玩端实际是"天空背景"，不是全屏背景。

这对方案的含义：

- **阶段 1（风格一致）不受影响**，它本来就是把 Note 与反馈层的颜色对齐视频。
- 但如果目标是"视频就是整个环境"，运行时需要额外一步：要么在启用 BGA 时隐藏舞台几何体，要么把视频改成**全屏 quad + `GeometryRhythm/Backdrop` 的 Background 队列**做法（现有 `sceneObjects` 全屏视频走的就是这条）。

顺带两条**既存冲突事实**（不是本次引入的）：

- 同一谱面若同时有 `videoBga` 和 `sceneObjects` 里的全屏 background 视频，**两者都会渲染，且后者盖住前者**（backdrop 是 Background 队列 + `ZTest Always` + 不透明 alpha）。运行期没有任何互斥或优先级判断。
- 运行时**不认 `.grchart`/`.thr` 包**（`ChartLoader.Parse` 只吃 JSON 字符串），所以 `videoBga.packageManifest` 必须是能解析到的文件路径：绝对路径，或相对**谱面文件所在目录**。从 `Resources` 载入的谱面没有可解析目录，此时 `LoadVideoBga` 会明确写进 `videoBgaStatus` 并 `Debug.LogWarning`，不会静默不显示。发行时若 BGA 要随包分发，需要先有"把包解到缓存再给运行时一个路径"这一步。

## 4. 复现

```powershell
# 构建游玩端（不依赖 gitignore 的 /BGA/）
Unity.exe -batchmode -nographics -quit -projectPath <项目> `
  -executeMethod GeometryRhythm.Editor.DemoBuildTools.ValidateAndBuild `
  -demoBuildDirectory Builds/GeometryRhythmBga

# 跑 BGA 自检
Builds\GeometryRhythmBga\GeometryRhythmDemo.exe `
  -demoChart BGA\goodtek60\runtime-chart.json -demoBgaSmoke `
  -demoCapture BGA\goodtek60\probe\game3 -screen-fullscreen 0 -screen-width 1280 -screen-height 720
```

测试谱面 `BGA/goodtek60/runtime-chart.json` 是 `Assets/RhythmDemo/Resources/Charts/geometry-demo.json` 的副本加一个 `videoBga` 字段——**必须用运行时本来就认的谱面作底子**：直接拿旧制谱器导出的包会在 `ChartLoader.Parse` 上失败（`unknown camera easing`），那是编辑器的宽松解析与运行时的严格校验之间的差异，与 BGA 无关。
