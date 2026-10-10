# Turi 角色图

| 类型 | 资源 |
| --- | --- |
| 全身立绘 | `turi-fullbody-pixel-v2.png` |
| 平静（默认） | `turi-head-neutral.png` |
| 开心 | `turi-head-happy.png` |
| 吐槽 | `turi-head-sarcastic.png` |
| 惊讶 | `turi-head-surprised.png` |
| 不满 | `turi-head-annoyed.png` |
| 害羞 | `turi-head-shy.png` |
| 专注 | `turi-head-focused.png` |

头像类型由 `TuriExpression` 定义，通过 `TuriPortraits.Load` 从 Resources 载入。独白统一使用 `RhythmFrontendView.ShowMonologue` 的头像／人名／文本布局；日记解锁时的“独白文本001”使用“专注”头像。
