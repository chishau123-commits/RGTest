# 编辑态播放与时间轴边缘滚动（2026-09-18）

## 当前实现范围（黄球居中、无 3D 自动跟随）

- 放大时间轴后，拖动播放头、音符或关键点到左右边缘，按靠近边缘的程度连续横移；鼠标静止但仍按住时继续，离开或松手停止。速度按时间而非帧数计算，歌曲首尾夹取。播放中的自动跟随改为平滑横移，Caps 飞行捕获鼠标时仍可跟随。
- 编辑态的 3D Playhead 定义为当前判定横截面中心 `RouteAt(DistanceAtTime(t) + NearDepth)`。
- 黄球通过 `sceneCamera.ViewportToWorldPoint(0.5, 0.5, MarkerDistance)` 定位到实际 3D 视口中心，不再依赖播放时间、Playhead 或相机节点。
- 删除黄球/视角跟随函数、缓存位移和相关调用。播放、暂停、Seek、跨节点、编辑已有节点及重播均不自动移动观察视角或黄球。仅保留手动导航、F 聚焦与 Preview 本来的相机运动。Add Key 仍捕获显示的中心黄球世界位置。
- 编辑模式额外绘制紫色当前时间路径，以及紫色 Playhead 十字和时间标签。动态路径使用与 Preview 相同的路径求值和可见性；绿色/黄色常驻轨道和全部静止 Note 保留。播放/拖动播放头只更新必要的覆盖层，不重建全谱 Note 和地图。
- Preview 不含上述紫色辅助线，仍使用原有相机插值及音符运动；退出还原编辑辅助与静止 Note。

## 自动检查

新增 `-chartEditorPlaybackSmoke`，测试不写入用户草稿：

- 100% / 125% / 175% UI 缩放下左右边缘、停留、离开、释放。
- 30/60 fps 一致性、首尾范围、菜单隔离、超出时间轴停止、舞台歌曲后尾段。
- 播放时双向平滑跟随、64 倍时间轴缩放、Caps 捕获鼠标仍跟随。
- 音符拖过原可视范围、单次撤销、Undo 恢复。
- 五种编辑页在首、中、节点边界、末尾播放/暂停/Seek 时相机不动，黄球保持实际视口中心；变速、world/path 节点与编辑节点也不能引起平移。
- UI 缩放、时间轴高度、Caps 切换、手动环绕/飞行后黄球仍居中；Add Key 保存球的位置；常驻路径/Note 对象不动、不重建，紫色路径正常更新。
- Preview 辅助线隔离、相机行为与退出恢复。

运行方式：在桌面播放器加入 `-chartEditorSmoke -chartEditorInteractionSmoke -chartEditorTimelineSmoke -chartEditorWorkspaceSmoke -chartEditorNoteSmoke -chartEditorPlaybackSmoke -chart <测试目录>/unsaved.json -chartEditorCapture <测试目录>`。截图检查使用可见窗口，不使用隐藏窗口。

## 初版结果与交付

- Unity 2022.3.62f3c1 独立工程构建通过：`Builds/chart-editing-playback-build3.log` → `CHART_STUDIO_BUILD_PASS`。
- 最终自检：`Builds/EditingPlaybackQA-Final/`；运行日志 `Builds/chart-editing-playback-smoke-final.log`，无异常，`CHART_STUDIO_SMOKE PASS`。
- 空间交互 36 + 时间轴 36 + 工作区 35 + 静止音符 34 + 本次编辑播放 50，共 **191 项 PASS**。
- 已查看 `editing-live-playback.png`，确认紫色动态路径/Playhead、常驻轨道与 Note 并存，黄球显示 FOLLOW K0；原有工具栏与时间轴正常显示。
- 新版最初独立交付到 `Builds/GeometryChartStudio-EditingPlayback/GeometryChartStudio.exe`。用户随后要求覆盖；确认旧进程已退出后，已更新固定入口 `Builds/GeometryChartStudio/GeometryChartStudio.exe`，133 个构建文件逐个 SHA-256 与通过测试的新版副本一致。未强制关闭程序，也未改写用户草稿；独立新版副本仍保留。
- 构建和截图目录均由 Git 忽略；无需上传这些二进制到 GitHub。

## 中间版本：前方绑定（已撤销，属于需求误解）

根据后续澄清，移除独立黄球世界偏移，改为固定相机局部位置。跟随时同时平移编辑相机和 orbitPivot，防止环绕更新把相机拉回。按时间差搬移整组对象，因此手动导航不会在下一帧被还原，播放中 Add Key 也不会重复叠加手动位移。Preview 保持原逻辑。

- 构建：`Builds/chart-camera-rig-build.log` → `CHART_STUDIO_BUILD_PASS`。
- 全量回归：空间交互 36 + 时间轴 36 + 工作区 35 + 静止音符 34 + 编辑播放/相机绑定 84 = **225 项 PASS**。运行日志 `Builds/chart-camera-rig-smoke.log` 无异常。
- 测试输出 `Builds/CameraRigQA/`，`chart-studio-smoke.txt` 为 PASS=True；已检查 `editing-live-playback.png`，黄球位于当前 3D 视口中心。
- 新增检查包括五种工具页、播放/暂停、手动转向和飞行、Caps 切换、跨节点、播放中 Add Key、歌曲末尾暂停、Preview 往返；均验证相机局部坐标 `(0, 0, 12)` 与视口中心不变。
- 用户保存并关闭旧版后，已覆盖固定入口 `Builds/GeometryChartStudio/GeometryChartStudio.exe`。133 个构建文件 SHA-256 全部与通过测试的产物一致，用户草稿未改写。`GeometryChartStudio-EditingPlayback` 仍保留修正前副本。

## 历史版本：玩家相机驱动、编辑视角跟随（现已全部撤销）

用户澄清黄球是玩家相机，而非编辑视角的固定前方附着物。移除前方绑定；黄球由上一玩家相机节点和 Playhead 求值，编辑视角随后按黄球实际世界位移平移。播放器界面将黄球标注为 `NEW KEY POSITION / PLAYER CAMERA`，飞行准星单独保持在编辑视口中心。

- 修正回归断言：不再断言固定 12 单位/屏幕中心；验证不同观察距离、起播保留偏移/朝向、播放中转向与移动编辑视角不影响玩家相机、跨节点/Seek 同位移跟随、暂停/Caps/Preview 保留世界位置、Add Key 保存玩家相机而非编辑视角。
- 构建：`Builds/chart-player-camera-follow-build.log` → `CHART_STUDIO_BUILD_PASS`。
- 测试：`Builds/PlayerCameraFollowQA/`；日志 `Builds/chart-player-camera-follow-smoke.log` 无异常。空间交互 36 + 时间轴 36 + 工作区 35 + 静止音符 34 + 编辑播放 92 = **233 项 PASS**，整体烟测 PASS=True。
- 已查看 `editing-live-playback.png`，玩家相机标签、紫色当前轨道与静态 Note 正常显示；观察距离未强制改为 12。
- 确认旧程序已退出后，最终版本已覆盖固定入口 `Builds/GeometryChartStudio/GeometryChartStudio.exe`。133 个文件逐个 SHA-256 与通过测试的构建一致，草稿未改动。

## 当前交付：撤销跟随

用户明确取消所有黄球/编辑视角跟随，要求黄球居中。已删除 `FollowEditingCamera`、`FollowingCameraMarkerPosition`、上一节点查询以及跟随/暂停位移缓存，不是仅隐藏提示或暂停调用。

- 构建：`Builds/chart-no-follow-build2.log` → `CHART_STUDIO_BUILD_PASS`。
- 回归：`Builds/NoFollowQA-Final/`，日志 `Builds/chart-no-follow-smoke-final.log` 无异常，整体 PASS=True。空间交互 36 + 时间轴 36 + 工作区 35 + 静止音符 34 + 居中/无跟随/边缘操作 261，共 402 项 PASS（包括多视图与多时间点矩阵）。
- 居中同时检查规范化视口坐标 `(0.5, 0.5)` 及物理像素中心；对非整数 UI 缩放允许不足 1 像素的光栅取整差。已查看 `editing-live-playback.png`，中心黄球标注为 `NEW KEY POSITION / CENTER`。
- 用户确认保存并关闭旧版后，已覆盖 `Builds/GeometryChartStudio/GeometryChartStudio.exe`，133 个构建文件 SHA-256 与通过测试的产物一致，未改动用户草稿。
