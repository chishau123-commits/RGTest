# Geometry Chart Studio

这是游戏共用空间内核之上的第一版电脑端制谱器。它使用普通 Unity Runtime API，而不是只能在 Unity 编辑器中运行的 `EditorWindow`，因此同一个场景可以构建为 Windows 桌面程序。

## 启动与构建

- Unity 菜单：`Geometry Rhythm → Chart Studio → Create or Open Desktop Editor`
- Windows 构建：`Geometry Rhythm → Chart Studio → Build Windows Editor`
- 构建结果：`Builds/GeometryChartStudio/GeometryChartStudio.exe`
- 可以用 `-chart "D:\Charts\example.json"` 指定启动时载入和保存的谱面路径。

播放器自检入口：`-chartEditorSmoke -chartEditorCapture "D:\Capture"`。程序会保存界面截图和 `chart-studio-smoke.txt` 后自动退出。

没有 `-chart` 参数时，草稿位于 `Application.persistentDataPath/geometry-chart-draft.json`。界面左下方可直接修改文件路径。

程序默认以可调整大小的 `1440×900` 窗口启动，不会强制全屏。右上角 **Settings** 可以在 100%、125%、150%、175% 之间调整界面缩放，选择会在下次启动时保留。

## 当前工作流

1. **Stage**：以 Catmull–Rom 样条绘制舞台主轴。按住 Shift 在地面单击可添加点；拖动球形控制点调整位置。路线使用平行传输标架，地图、相机和 Note 路径共享同一局部坐标系。
2. **Camera**：调整桌面视口到期望构图，在当前播放头添加相机关键帧。相机保存为舞台局部的位置和注视目标，而不是不直观的旋转四元数。
3. **Paths**：在当前布局段中拖动路径控制球，调整横向和高度；`bend/lift` 控制远端路径形状。可在播放头添加新的布局段。
4. **Notes**：选择 Path、Tap/Drag 和保护属性，在播放头放置音符。
5. **Map**：用 Seed、走廊宽度、密度和高度变化生成确定性的抽象几何地图。主轴两侧保留安全走廊。

底部时间轴支持播放、逐拍移动、拖动定位以及游戏相机预览。快捷键 `1`–`5` 切换工具，空格播放/暂停，`Ctrl+S` 保存，`Ctrl+Z/Ctrl+Y` 撤销/重做，Delete 删除当前可删除对象。

## 数据兼容

谱面仍为 JSON v1。新增字段 `stagePath` 和 `map` 均为可选字段：旧谱面缺少它们时继续使用原来的公式路线和固定地图。新增相机关键帧通过 `usePathPose` 选择舞台局部姿态，旧的 `orbit/distance/height` 关键帧保持原有求值方式。

当前版本是空间创作闭环 MVP，下一阶段适合补充音频文件浏览器、波形缓存、拍号/变速编辑、框选与批量 Note 操作，以及模块化地图 Prefab 库。
