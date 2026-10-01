# 制谱器是 Thart，谱面格式权威是 .thr，旧制谱器冻结

Status: accepted

仓库里有两套各自完整的制谱器——Thart 与 GeometryChartStudio——而代码和文档都没有裁决哪一套是当前。方向定为：**制谱器是 Thart**，新的制谱功能都做在它里面（视频 BGA 之后搬进来）；**谱面交付的权威格式是 `.thr`**；`GeometryChartStudio` **冻结**：保留代码与构建入口，不再加功能，只作为现成的视频 BGA / 3D 路径通路。

## 为什么

- Thart 已经是一套完整流程（平板录触控 → 电脑端编辑/预览 → `.thr` 谱面包），有自己的场景、构建入口（`Builds/ThartEditor/ThartEditor.exe`）与验证入口。它缺的正是视频 BGA：Thart 目录内 `Video|BGA|VideoPlayer` 零命中，`.thr` 容器只有 `chart.json / touch.json / meta.json / audio/`。
- 两套**共用同一个 `ChartData`**，所以"选权威格式"是一次决定，不是一次迁移：写进 `.thr` 的 `chart.json` 的字段，两个容器都装得下。
- **冻结而不是删除**：`GeometryChartStudio` 是当前唯一能播视频 BGA 的程序。在 Thart 具备视频能力之前删掉它，等于失去全部 BGA 通路。
- 侦察结论原话是"仓库本身没有给出裁决"；没有这份记录，下一个读代码的人仍然会问，而且很可能两套都改。

## 后果

- 新的制谱侧功能（包括分叉点标注）落在 Thart。视频 BGA 搬进 Thart 之前，BGA 相关的验证仍只能在旧制谱器里跑。
- `.thr` 容器的内容清单需要新增 BGA 槽位（当前是 `chart.json / touch.json / meta.json / audio/`）。
- 文档、术语与验证记录里的"制谱器"一律指 Thart；指旧程序时必须写"旧制谱器"。
- [CONTEXT.md](../CONTEXT.md) 已按此更正；[VideoBgaBranchingPlan.md](../VideoBgaBranchingPlan.md) 的数据契约改为写进 `.thr` 的 `chart.json`。
- 旧制谱器上的 seek 量测 harness 视为**过渡宿主**，其归属在"把 `videoBga` 接进游玩运行时"时一并迁走。

## 考虑过的替代

- **两套并存、各管一条线**（BGA/3D 路径留旧、平板触控走 Thart）：被否。产品方向已定 Thart 为制谱器，并存意味着每条制谱功能都要做两遍。
- **删除旧制谱器**：被否，见上——在 Thart 能播视频之前不可逆。
