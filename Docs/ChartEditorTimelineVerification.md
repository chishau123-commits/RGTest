# 多轨时间轴验证

环境：Windows x64 / Unity 2022.3.62f3c1。时间轴数据仍写入原来的谱面字段，没有另建不兼容的时间轴格式。

## 检查入口

```powershell
& .\Builds\GeometryChartStudio\GeometryChartStudio.exe -chartEditorSmoke -chartEditorInteractionSmoke -chartEditorTimelineSmoke -chart D:\Capture\Timeline\unsaved.json -chartEditorCapture D:\Capture\Timeline
```

需要可见窗口，隐藏窗口的图形捕获可能全黑。测试不保存谱面；截图用的示例区段、音符和关键帧仅存在于自检进程内。

## 自动覆盖

时间轴套件 36 项：

- 面板最小/最大高度和小窗口约束；调整高度后相机像素视口同步。
- Stage、Camera、Paths、Notes 的不同轨道组成。
- 舞台控制点的弧长映射，包括歌曲结束之后的路线尾段。
- 相机拖拽、单次撤销/重做、零拍保护及相邻关键帧边界。
- 连续布局片段、独立起点拖拽、首区段零拍保护。
- 偏移关键点拖拽；音符排序、稳定 ID、选中状态和结束边界。
- 放大/平移后的坐标反算；1/4 拍和关闭吸附后的 tick 精度。
- 时间尺鼠标事件连续定位、区域外松手终止拖拽。
- 时间轴与 3D 编辑器输入隔离。
- 真实播放音频的波形缓存；修改结果通过谱面解析器。

另外回归原有 36 项编辑器交互检查和 249 项游戏检查。

本次执行结果：时间轴 36/36、交互 36/36、游戏 249/249 均通过；125% 和 175% 窗口截图自检均通过。报告保存在被 Git 忽略的 `Builds/TimelineQAFinal/` 和 `Builds/TimelineQA175/`。

## 视觉检查

四个视图独立截图。检查标签移除数字、公共播放头/刻度/波形、布局片段移出侧栏、轨道滚动条、分隔条和不同窗口缩放。曲线使用缓存纹理，避免缩放 GUI 中旋转线段裁剪造成断裂。

使用 computer-use 在临时构建窗口验证了分隔条上下拖动、视口同步、Paths 轨道、新增区段、拖动区段左边界和时间拖动撤销。临时测试进程没有保存谱面，也没有关闭用户原有的制谱器窗口或 Unity 工程。测试结束恢复时间轴约 310 的高度。

完整操作说明见 `GeometryChartStudio.md` 的“多轨时间轴”。

## 本次交付

为保留仍在运行的旧版窗口，本次新版程序另放在 `Builds/GeometryChartStudio-Timeline/GeometryChartStudio.exe`。该目录仍由 `/Builds/` 的 Git 忽略规则覆盖。未覆盖运行中的旧版程序；之后通过 Unity 构建菜单仍会输出到标准 `Builds/GeometryChartStudio/`。

最终交付构建的报告和四视图截图：`Builds/TimelineReleaseQA/`。
