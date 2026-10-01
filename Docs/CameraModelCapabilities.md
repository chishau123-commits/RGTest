# 游玩运行时的相机模型：能表达与不能表达的自由度

> 为什么有这份文档：新方向要求"3D 段落的**运镜**从视频分析得出"，而这件事能不能落地、以及能落地到什么程度，**完全取决于相机模型能表达什么**。本文是一次只读侦察（read/grep/glob，未改文件、未跑 Unity）的结果，只记事实与代码依据，不含实现方案。
> 相关：[VideoBgaBranchingPlan.md](VideoBgaBranchingPlan.md)、[ChartEditorCameraPoseVerification.md](ChartEditorCameraPoseVerification.md)、[ChartEditorCameraTweenVerification.md](ChartEditorCameraTweenVerification.md)。

## 0. 结论速览

| 能表达 | 不能表达 |
|---|---|
| `orbit`（偏航）/ `height` / `distance`（推拉，**硬下限 12**）/ `roll`（画面旋转，完整 360°）/ `fov`（**限 [30,85]**） | **自由 6DOF 连续样条**：位置分段线性、朝向 slerp，键边界速度可以不连续 |
| `usePathPose`（6 个路线局部量）、`useWorldPose`（绝对世界位姿） | **镜头硬切**：相机是同一个 `Camera` 上的连续函数，且相邻键 `beat` 必须严格递增 |
| 变焦（`fov` 逐段 Lerp） | **自定义缓动曲线**：只有 5 个预设 |
| 加性补间 `cameraMotionClips`（位置 + 欧拉旋转 + FOV） | **变焦与推拉的区分**：没有任何字段记录"这是变焦还是位移" |
| — | 非 16:9 构图 / aspect / 正交相机 / 传感器参数；时间重映射；曝光 / 快门 / 景深 |

## 1. 谱面侧的相机数据模型

### 1.1 `CameraKey` 的字段语义（`Assets/RhythmDemo/Runtime/ChartData.cs:120-144`）

| 字段 | 语义 |
|---|---|
| `beat` | **拍**，不是秒。秒→拍经 `TempoMap.BeatAtSeconds`（`SpatialDirector.cs:261`） |
| `easing` | **本段起始键的 outgoing 缓动**，缺省 = smoothstep（`SpatialDirector.cs:57-58`） |
| `orbit` | 绕观察点的**水平偏航角（度）**，在**路线局部标架**里（legacy 分支 `:291-294`） |
| `roll` | **相机本地 Z 轴右乘**的画面旋转（`:273`、`:300`），完整 360° |
| `distance` | 观察点到相机的轴向距离，**世界单位，且是路线局部量**；校验下限 **12** |
| `height` | 相机相对观察点的高度，世界单位、**无上下界校验** |
| `fov` | **垂直**视场角（度），校验 **[30,85]** |
| `usePathPose` + `positionForward/X/Y`、`targetForward/X/Y` | `*Forward` 是**沿路线的绝对里程偏移**（加到里程 s 上），`*X/Y` 是**横截面偏移**，经 `StageSpline.OffsetPoint` 落到世界；X/Y **随路线推进而旋转**（`:282-288`） |
| `useWorldPose` + `worldPosition/worldTarget` | **绝对世界坐标**，不跟路线走（`:305`） |

**模式优先级（关键）**：只要整条轨道里有**任意一个** `useWorldPose` 键，整条轨道就切成"固定端点"策略——所有键在**各自的拍点时刻**解析成世界姿态并缓存（`SpatialDirector.cs:83-104`）。没有任何 world 键时，才是逐帧的 legacy / path 语义，而"legacy 还是 path"是**逐帧**按 `a.usePathPose || b.usePathPose` 判的（`:282`），所以相邻两键可以一个 legacy 一个 path。

### 1.2 校验规则（`ChartData.cs:339-354`，完整）

- **至少一个键**，且第 0 键 `beat == 0`（float 精确相等）。
- `orbit/roll/height/distance/fov/beat` 全部有限；`distance >= 12`；`fov ∈ [30,85]`。
- `beat` **严格递增**。
- `easing` 必须是白名单之一或空（`SpatialDirector.IsCameraEasing` `:54-56`）。
- `usePathPose` 时 6 个 pose 分量有限；`useWorldPose` 时 6 个分量有限且 `worldTarget != worldPosition`。
- **`beat` 不要求落在歌内**（对比 `VideoChartSpace.cs:314` 对视频相机键有 `tick ≤ end` 的约束）。
- **键数与键距都没有上界校验**。

### 1.3 键之间怎么插值（`SpatialDirector.cs:258-301`）

- 段选择：`while (index + 1 < Length && cameraKeys[index + 1].beat <= beat) index++`（`:262-263`），末键之后 `b == a` → `InverseLerp` 返回 0 → **保持末键姿态**。
- 缓动只有 5 个预设（`:59-70`）：`linear`、`easeIn`(`t²`)、`easeOut`(`1-(1-t)²`)、`easeInOut`(smoothstep)、`smoother`(Perlin 五次)。
- **两套位置/朝向公式**：
  - 轨道内有 world 键 → `position = Vector3.Lerp(P_i, P_{i+1}, q)`，`rotation = Quaternion.Slerp(R_i, R_{i+1}, q) * Quaternion.Euler(0,0,Lerp(roll))`（`:270-273`）。**朝向是端点基准的四元数 slerp**，不再逐帧 LookRotation。
  - 纯 legacy / path → 逐帧 Lerp 标量（`:277-279`）或 6 个 pose 分量（`:286-287`），**再逐帧 `LookRotation(direction, up)`**（`:296-300`）；`up` 在 `|dot| > .999` 时切成 `Vector3.forward`（`:298`）。
- `fov` 所有分支都是 `Mathf.Lerp`（`:274`、`:301`）。
- **密度**：格式无键数上限，但编辑器把新键吸附到 tick（`ChartEditor/RuntimeChartEditorController.cs:693`），同 tick 替换而不新增，所以实际最小间距约 **1 tick**（默认 480 tick/拍）。

### 1.4 两套相机模型是**二选一**，不是叠加

- `VideoChartSpace.Enabled`（`VideoChartSpace.cs:67-68`）为真 → `SpatialDirector.EvaluateCamera` 第一行就 `VideoSpace.ApplyCamera(...); return;`（`:260`），**`cameraKeys` 整条轨道被跳过**。
- 注意判据：`videoSpace != null && (schemaVersion != 0 || cameraZKeys.Length > 0 || sceneAnchors.Length > 0)`——**`cameraPoseKeys` 单独存在并不会开启视频空间**。
- 开启后还必须过 `Validate`（`:292-320`）：schema 1/2、`cameraZKeys.Length >= 2`、tick 与 Z 严格递增、首 tick = 0 且末 tick = `endBeat`。
- 语义差别：`CameraKey` 是"路线局部关键帧"，`VideoCameraPoseKey` 是"沿 +Z 轨道推进的刚体 rig 偏移（`dx/dy/dz`）＋三轴头部角（pitch/yaw/roll）＋fov"（`VideoChartSpace.cs:30-44`）。

## 2. `CameraMotionEvaluator`：**加性**补间

- `VisualAuthoringRuntime.cs:14-47`：遍历 `cameraMotionClips`，按 `startTick/durationTicks` 判活跃，叠加到**已经算好的相机位姿**上：
  - 位置：`camera.transform.position += camera.transform.TransformVector(position)`（**相机局部空间**，`:44`）
  - 旋转：`camera.transform.rotation *= Quaternion.Euler(rotation)`（**:46** 前的 `:45`，相机局部欧拉，`pitch/yaw/roll` 各一路）
  - FOV：`camera.fieldOfView = Mathf.Clamp(camera.fieldOfView + fov, 20, 100)`（`:46`）——**注意这里的 clamp 是 `[20,100]`，与谱面相机键的 `[30,85]` 不是同一个范围**
- 7 种 `kind`：`shake` / `punch` / `roll` / `orbit` / `dolly` / `fovPulse` / `custom`（`keys` 按键的归一化 `time` 做 `Vector3.Lerp`，`:56-65`）；包络 `linear` / `impact = exp(−5q)` / 默认 `sin(πq)`（`:49-54`）。
- **生效条件**：仅当该谱面**不是**视频谱面——`RhythmDemoController.cs:324-325` 的 `if (!VideoChartSpace.Enabled(Chart)) CameraMotionEvaluator.Apply(...)`。也就是说：**视频谱面里 `cameraMotionClips` 会被加载、被校验，但游玩时不会执行。**
- `effectClips` **任何 kind 都不碰相机**：`AuthoredVisualDirector.Evaluate`（`VisualAuthoringRuntime.cs:309-398`）全篇只写 fog / ambient / overlay / 灯光 / 场景缩放，只有 `:381-382`、`:409` **读**相机位置与 FOV 用来挂粒子与铺 overlay。

## 3. 判定与呈现对相机的依赖

- **时间判定完全不看相机**：`JudgementEngine.cs:15` 的注释写明"Logical scoring has no dependency on physics, camera movement or GameObject lifetime"，窗口是 `PerfectWindow = .040` / `GoodWindow = .120`（`:21-22`）。
- **空间命中完全是相机的函数**：`NoteProjection.Contains` 把世界圆盘逐点 `camera.WorldToScreenPoint`（`JudgementEngine.cs:118`、`:123`），`p.z <= nearClipPlane` 直接 false（`:119`、`:124`）；`RhythmDemoController.Inside`（`:270-304`）另加屏幕空间容差矩形（`:287-290`，横向 ±20% 短边、上 +10%、下 −20%）。
  → 相机一动，"同一个世界点"落在不同屏幕像素；相机把判定面推出视口或推过近裁剪面时 `Contains` 返回 false，而 `Engine.Advance` **仍照常判 Miss**（`JudgementEngine.cs:104`）。**时间公平性不破，空间可达性会破**——仓库对此已有兜底注释（`RhythmDemoController.cs:278-279`）。
- **视频 BGA 挂在相机远平面**：`VideoBgaRuntime.cs:127` 的 `renderMode = VideoRenderMode.CameraFarPlane`。
  - 后果一：**相机的平移与推拉不给视频引入任何位移或缩放**（它永远铺满整帧）。
  - 后果二：**朝向会带动它**，而仓库已记录限制——`ChartEditorCameraPoseVerification.md:85`：「平面 BGA 不会随相机旋转。平移、推拉、变焦与小角度转头能和视频画面大致对上；**大角度 yaw/pitch 会让 3D 前景与平面视频的透视分家**。这是素材形态的限制，不是制谱器可以消除的。」
  - 后果三：**变焦在视频上看不出来**（视频铺满整帧），fov 变化只改变 3D 前景。
- 另一条 BGA 通路 `BlenderBgaRuntime` 能直接改写 `output.transform` 与 fov/裁剪面（`BlenderBgaRuntime.cs:218-256`），但它**只被编辑器 new**（`ChartEditorBlenderBga.cs:31`），`RhythmDemoController.cs` 里没有任何引用 → **游玩运行时不可达**。

## 4. 不可表达的自由度（具体清单）

1. **镜头切换 / 硬切**：相机是**同一个 `Camera` 上的连续函数**；虽然可以在两个相邻键之间放一个姿态跳变，但 `beat` 必须**严格递增**（`ChartData.cs:345`），所以"同拍两键"不合法；最小间隔约 1 tick（受 ticksPerBeat 限制）。
2. **非 16:9 构图 / aspect / 正交相机 / 传感器参数**：视口恒 16:9（`GameViewport.cs:21,58-70`），视频谱面还把 `aspect` 写死 `16/9` 并自建投影矩阵（`VideoChartSpace.cs:163-164`），且 `ApplyCamera` 强制 `orthographic = false`（`:162`）。
3. **自定义变速曲线**：只有 5 个固定预设（`SpatialDirector.cs:54-70`），未知值在加载时被拒（`ChartData.cs:346`）；缓动是"段起始键的 outgoing"单一属性，无法表达"同段内先加速再减速再匀速"。补间剪辑另有一套 3 档包络（`VisualAuthoringRuntime.cs:49-54`），那是**包络**不是曲线。
4. **变焦与推拉的区分**：谱面只给 `fov` 与相机位置，两者在投影上可互相替代，**没有任何字段记录"这是变焦还是位移"**。
5. **6DOF 连续样条轨迹**：world 键可行，但端点间是 `Vector3.Lerp` + `Quaternion.Slerp`，**分段独立 easing、无跨键连续性**（速度在键边界可以不连续）。
6. **时间重映射 / 变速运镜**：相机时间始终等于歌曲时间。
7. **曝光 / 快门 / 景深 / 运动模糊**：无字段。
8. **相机高度或位置的显式上下界**：没有。真正约束画面可用性的不是谱面校验，而是"**判定必须在屏幕内**"这条运行时事实（见 §3）。

## 5. 对"从视频生成运镜"的直接含义（事实性，不含方案）

1. 表观运动的四个分量里，**只有一部分有对应自由度**：旋转 → `roll`；平移 → `orbit`（小角度）/ `height`；缩放 → `distance`（**受下限 12 约束**）。而 **`fov` 不适合承载缩放**，因为 fov 变化在远平面视频上**看不出来**，只会让前景与视频分家。
2. **镜头硬切无法用相机表达**，只能交给视频自身的硬切。
3. **"整层平移"能被呼应的幅度天然受限**：仓库自己记录了"大角度 yaw/pitch 会让 3D 前景与平面视频的透视分家"。
4. **变焦与推拉在单目视频里不可分辨**，映射时必须二选一（目前没有字段能同时记录两者）。
5. 若谱面带 `videoSpace`，**`cameraKeys` 被整体绕过、`cameraMotionClips` 在游玩时不执行**——运镜将无处安放。
6. 相机运动会影响**空间命中**（判定用 `WorldToScreenPoint` 投影），而不是时间判定。

## 6. 复现（要读的文件）

```text
Assets/RhythmDemo/Runtime/ChartData.cs                    # CameraKey :120-144、校验 :339-354、CameraMotionClipData :210-233
Assets/RhythmDemo/Runtime/SpatialDirector.cs              # 插值 :258-301、固定端点缓存 :83-104、缓动白名单 :54-70
Assets/RhythmDemo/Runtime/StageSpline.cs                  # 路线与 OffsetPoint :120-127
Assets/RhythmDemo/Runtime/VideoChartSpace.cs              # Enabled :67-68、rig 键 :30-44、ApplyCamera :158-165、Validate :292-320
Assets/RhythmDemo/Runtime/VisualAuthoringRuntime.cs       # CameraMotionEvaluator :14-47、AuthoredVisualDirector.Evaluate :309-398
Assets/RhythmDemo/Runtime/RhythmDemoController.cs         # 相机调用点 :324-325、输入命中 :270-304
Assets/RhythmDemo/Runtime/JudgementEngine.cs              # 窗口 :21-22、NoteProjection.Contains :111-140
Assets/RhythmDemo/Runtime/VideoBgaRuntime.cs              # 远平面 :127
Docs/ChartEditorCameraPoseVerification.md                 # :85 平面 BGA 不随相机旋转
```
