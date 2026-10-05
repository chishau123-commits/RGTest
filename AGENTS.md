# RGTest

Unity 2022.3.62f3c1 节奏游戏（Built-in Render Pipeline）；玩法与 UI 的验证记录在 `Docs/`。

本仓库单独克隆即可打开、编译与运行。仓库不登记外部私有工具为子模块。

## Agent skills

### Issue tracker

Issues live as GitHub issues in `chishau123-commits/RGTest`, operated via the `gh` CLI. See `Docs/agents/issue-tracker.md`.

本仓库的 tracker 只管理 RGTest 自身的工作。外部独立工具的工单不路由到这里。

### Triage labels

The five canonical triage roles, each label string equal to its name. See `Docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and `Docs/adr/` at the repo root, both created lazily. See `Docs/agents/domain.md`.

> **本仓库**的文档目录记为 `Docs/`（大写 D），引用本仓库路径时保持这个大小写，避免出现两个只差大小写的目录。
