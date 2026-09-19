# 场景 / 动效 / 运镜验证

环境：Windows x64 / Unity 2022.3.62f3c1。

## 构建与入口

- 交付构建：`Builds/GeometryChartStudio-VisualAuthoring/GeometryChartStudio.exe`
- Unity 构建报告：`Builds/visual-authoring-build-final.log`
- 自动检查目录：`Builds/VisualAuthoringFullQAFinal/`

## 自动覆盖

`-chartEditorVisualSmoke` 检查以下 12 项：

- 三类内置素材包存在，包含 6 个场景素材、10 个动效素材和 7 个运镜素材。
- 场景素材实例化为完整谱面数据。
- 动效和运镜素材生成可拖动的持续时间片段。
- Scene 是独立的非时间化搭建页；Effects 与 Motion 各自具有音频和片段轨。
- 自定义运镜关键点在反复 Seek 时得到完全一致的位置、旋转和 FOV。
- 场景实例在 Runtime 制铺器中真实生成。
- `sceneObjects`、`effectClips`、`cameraMotionClips` 及自定义运镜关键点通过 JSON 往返保持。

最终结果：`visual-authoring-checks.txt` 12/12 通过，`chart-studio-smoke.txt` 为 `PASS=True`。同时原有交互 36、时间轴 36、工作区 35、静态 Note 43、编辑播放 351、相机补间 105 项报告均为 0 失败；游戏规则回归为 249/249。三页截图均通过非空画面检查。
