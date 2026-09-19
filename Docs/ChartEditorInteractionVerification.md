# 制谱器 3D 交互验证 · 2026-09-18

环境：Unity 2022.3.62f3c1，Windows x64，窗口模式。

## 自动检查

- Windows 构建：`CHART_STUDIO_BUILD_PASS`。
- 既有游戏检查：`GEOMETRY_VALIDATION_PASS 249 checks`，未修改原检查文件。
- 新交互检查：`CHART_STUDIO_INTERACTIONS PASS 36`。
- 可见窗口截图与启动检查：`CHART_STUDIO_SMOKE PASS`。
- 发布位置：`Builds/GeometryChartStudio/GeometryChartStudio.exe`。与验证构建的 `Assembly-CSharp.dll` SHA-256 一致；整个 Builds 目录由 Git 忽略。

交互检查覆盖：三个轴的显示位置与命中位置一致；拖拽期间重建视觉对象后仍连续移动；整次拖拽只记录一个撤销；撤销/重做；飞行方向和速度归一化；模式切换保持视角；普通及飞行模式的黄球相机落点；相同拍点替换；垂直于路线的二维偏移；前后关键点独立；距离插值；编辑器与游戏共享求值；JSON 往返；无效偏移关键点拒绝。

可在项目根目录运行：

```powershell
& .\Builds\GeometryChartStudio\GeometryChartStudio.exe -chartEditorSmoke -chartEditorInteractionSmoke -chartEditorCapture D:\Capture\ChartStudio -chart D:\Capture\ChartStudio\unsaved-test.json
```

该命令使用测试草稿位置且不自动保存谱面，完成后退出。截图检查需要正常可见窗口；隐藏窗口在此显卡/驱动环境中输出黑帧，已增加非黑帧判定，不能把“文件存在”误当成视觉验收通过。最终报告及图片在 `Builds/InteractionQAVisible/`。

## 实际窗口操作

使用 computer-use 在独立制谱器窗口操作，未操作或关闭用户的 Unity 工程：

- 拖动舞台 Y 轴，高度从 0 增大，X/Z 保持不变。
- 最终交付构建中，拖动后按一次 Ctrl+Z，高度恢复为 0，路线与控制点同步恢复。
- Caps Lock 切换显示 CAPS ON / CREATIVE FLIGHT 和 CAPS OFF / ORBIT / PAN。
- 飞行状态按 W 视角前移，Esc 后提示 Mouse released，关闭 Caps 后恢复常规编辑并保持当前视角；测试后 Caps 已恢复关闭。
- 相机页单击 Add Key，新增点在黄色球位置，显示 world pose。
- Paths 页拖动 Y 轴，偏移球及 Note 路径随之移动。
- 在 3D 视口滚动鼠标，视角远近保持不变。
- 已复查 Stage、Camera、Paths 截图，修复缩放坐标轴错位、侧栏横向溢出；相机添加按钮始终置于坐标输入框之前。

其他修正：数字框可输入负数和小数，显示舍入不再写回谱面或污染撤销栈；文本输入不触发场景快捷键；焦点丢失释放鼠标。

本次未重建游戏发行包。新偏移轨道和世界相机姿态已接入共享运行时代码；旧游戏可执行文件需要重新构建才能读取这些新增字段。
