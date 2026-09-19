# Playhead 稳定标记与相机曲线（2026-09-18）

## 实现

- 编辑态 Playhead 十字改为两根相互独立、始终朝向编辑视角的细线；长度和线宽按屏幕像素换算，不再继承路线坐标架的逐帧细小旋转。
- Camera 视图为 K 点显示标签，并在相邻 K 点的橙色细线中点显示曲线按钮。
- 每段由前一个 K 点保存 outgoing `easing`，支持 Linear、Ease In、Ease Out、Ease In-Out、Smoother。
- 曲线同时控制位置、四元数朝向、FOV 和显式 Roll。缺少字段的旧谱面仍解释为原有 Ease In-Out。
- 新增、同拍替换、删除、撤销、重做与 JSON 保存/载入均保留区段曲线；未恢复黄球或编辑视角跟随。

## 验证

- Windows 构建：`Builds/chart-camera-curves-build-final2.log`，`CHART_STUDIO_BUILD_PASS`。
- 桌面回归：`Builds/chart-camera-curves-smoke-final.log`，交互 36、时间轴 36、工作区 35、静态 Note 34、编辑播放 261、相机补间/曲线 105 项全部通过，`CHART_STUDIO_SMOKE PASS`。
- 游戏回归：`Builds/chart-camera-curves-game-validation.log`，`GEOMETRY_VALIDATION_PASS 249 checks`。
- 截图与逐项报告：`Builds/CameraCurvesQA-Final/`。
- 已覆盖固定发行目录并逐文件校验 133 个 SHA-256。用户草稿交付前后 SHA-256 均为 `00B604C004D133B107FE2A1863AF8CE39E1DF090678A05D15B3F45EF10719E80`，未被修改。
