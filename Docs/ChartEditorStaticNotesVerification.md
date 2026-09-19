# 编辑态静止音符验证（2026-09-18）

## 行为

- 编辑模式不再按接近时间隐藏音符，所有 Note 均作为场景对象持续存在。
- 固定位置为 `SpatialDirector.NotePose(note, note.HitTime)`：在音符自身拍点计算位置/朝向，因此与 Preview 到达判定时间的位置完全一致。改变当前播放头或播放编辑音频不改变 Note 的位置；修改谱面拍点/路径/舞台仍正常更新。
- 编辑态 Note 路径完整、静止显示，不再是跟随播放头移动的局部片段。Paths 的编辑横截面仍跟随播放头，便于编辑各拍点偏移。
- Preview 保留原有接近窗口、流动和判定后消失；退出后还原所有静止 Note。
- 可在 Notes 页点击 3D Note 或时间轴选择，再按 F / Focus note 聚焦。编辑态播放音乐时只更新必要的路径横截面，不再每帧重建全谱音符。

## 结果

Unity 独立工程构建：`Builds/chart-static-notes-build2.log` → `CHART_STUDIO_BUILD_PASS`。

播放器运行参数包含 `-chartEditorSmoke -chartEditorInteractionSmoke -chartEditorTimelineSmoke -chartEditorWorkspaceSmoke -chartEditorNoteSmoke`，结果位于 `Builds/StaticNotesQA/`：

- 坐标轴/空间交互：36 / 36 PASS。
- 时间轴：36 / 36 PASS。
- 工作区/预览/文件：35 / 35 PASS。
- 静止音符：34 / 34 PASS。
- 合计 141 项，通过；运行日志无异常；`chart-studio-smoke.txt` 为 PASS=True。

新增检查覆盖五个工具页、播放头首/中/尾、变速、曲线/倾斜舞台、新 offsetKeys 和旧 section placement、全谱路径、播放不重建 Note、选择/聚焦、Preview 流动与判定落点一致、退出还原、改拍点与撤销、空谱面。

已检查 `static-notes-start.png` 与 `static-notes-end.png`：相机视角不变、播放头从 0 秒到 64 秒，Note 与路径仍位于相同场景位置。

## 交付

用户关闭旧窗口后，新版已更新到固定入口 `Builds/GeometryChartStudio/GeometryChartStudio.exe`。复制后逐一校验了 133 个构建文件的 SHA-256，均与已通过测试的 `GeometryChartStudio-StaticNotes` 包一致；该副本保留，用户草稿未改写。构建及测试输出均被 Git 忽略。
