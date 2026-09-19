# Chart Studio 工作区更新验证（2026-09-18）

## 交付

- Windows 程序：`Builds/GeometryChartStudio/GeometryChartStudio.exe`。
- 构建日志：`Builds/chart-workspace-build-final.log`，包含 `CHART_STUDIO_BUILD_PASS`。
- 在隔离的 Unity 临时工程中构建，未切换用户主工程的场景。复制后的 `Assembly-CSharp.dll` 与构建产物 SHA-256 一致：`A4DCED5B8CB3CA0ABCF1D0DCD950F92B0F9FA3022F62B8AA1F9A57E47BEED594`。
- 程序、日志、截图和测试 JSON 均位于 Git 忽略的 `Builds/`。

## 自动回归

已运行 `-chartEditorSmoke -chartEditorInteractionSmoke -chartEditorTimelineSmoke -chartEditorWorkspaceSmoke`。

交付包结果位于 `Builds/WorkspaceReleaseQA/`：

- `interaction-checks.txt`：36 / 36 PASS。
- `timeline-checks.txt`：36 / 36 PASS。
- `workspace-checks.txt`：35 / 35 PASS。
- `chart-studio-smoke.txt`：PASS=True，运行日志无异常。

新增工作区检查覆盖：

1. 细滚动条透明边距和可抓取宽度；100%、125%、175% 下 Ctrl+滚轮事件、鼠标时间锚点、正反缩放、1–64 倍限制、3D 视口不触发时间轴缩放。
2. Files 菜单阻挡背景输入；点击外部关闭不误触时间轴；新建前未保存提示与 Escape 取消。
3. 保存后当前路径和干净基线；另存为中文/空格路径不修改原文件；后续 Save 指向副本；失败保存不切换路径；损坏及无关 JSON 不替换当前谱面；无音符草稿可重新载入。
4. 预览从播放头自动播放、扩展窗口内视口、隐藏编辑辅助物、与共用相机求值器一致、保留静态地图不逐帧重建；Space 暂停；时间轴只读拖动；Esc 退出、恢复位置/朝向/FOV/控制点；歌曲末尾重新进入预览从头播放。

测试 JSON 写入自检目录内的唯一子目录，没有写入用户草稿。

## 画面与桌面窗口

- 检查了 `Builds/WorkspaceQA2/` 的 125% 截图，以及交付包 `Builds/WorkspaceReleaseQA/` 的 175% 截图。
- `workspace-scrollbars.png`：细圆角滚动条，无原生金属高光、无箭头。
- `workspace-files.png`：右上角仅 Files / Settings / Save；下拉包含 New / Load / Save As。
- `workspace-preview.png`：隐藏检查面板和编辑辅助物，相机、路径、音符和判定位置清晰；底部保留播放、定位、缩放及退出预览。
- computer-use 实际点击确认了 Files 菜单和原生 Windows Load 对话框。随后检测到用户窗口操作，停止桌面输入；未继续自动点击原生 Save As/覆盖确认。文件保存/载入底层逻辑已由上述运行时回归覆盖。

## 边界

- 预览为可视化时序检查，不是带点击判定/评分的试玩。仍使用 DEMO AUDIO，没有新增歌曲导入功能。
- 尝试额外运行游戏全量 `DemoValidation.Run` 时，Unity 授权客户端握手报告不支持协议 `1.16.2`（见 `Builds/chart-workspace-game-validation.log`），该次游戏验证没有完成；不计入本次通过结果。本次修改限定于制谱器和其文档，已构建的编辑器播放器回归不受影响。
