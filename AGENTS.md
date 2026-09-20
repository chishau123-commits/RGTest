# RGTest

Unity 2022.3.62f3c1 节奏游戏（Built-in Render Pipeline）；玩法与 UI 的验证记录在 `Docs/`。

## Submodule: `bgv-chartgen/`

`bgv-chartgen/` 是一个独立仓库（谱面生成器），登记为子模块只是为了让工具链有单一的根目录。

**它不是 Unity 工程的一部分。** 本仓库单独克隆即可正常打开、编译、运行，不需要这个子模块。

该子模块仓库是私有的。没有访问权限时，只有显式递归拉取会失败，跳过即可：

```sh
git clone https://github.com/chishau123-commits/RGTest.git   # 默认不拉子模块，够用
```

若已经用了 `--recurse-submodules` 并看到 `bgv-chartgen` 认证失败：主仓库此时已经克隆完成，忽略那条报错即可，不影响任何 Unity 侧的工作。

## Agent skills

### Issue tracker

Issues live as GitHub issues in `chishau123-commits/RGTest`, operated via the `gh` CLI. See `Docs/agents/issue-tracker.md`.

改动落在 `bgv-chartgen/` 里的，issue 发那个子模块自己的 tracker，不发这里。判断依据见 `Docs/agents/issue-tracker.md` 的 Scope 一节。

### Triage labels

The five canonical triage roles, each label string equal to its name. See `Docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and `Docs/adr/` at the repo root, both created lazily. See `Docs/agents/domain.md`.

`bgv-chartgen/` 是独立仓库，自带 `CONTEXT.md` 与 `docs/adr/`。两边的术语表互不覆盖，各管各的仓库。

> 文档目录在版本库中记为 `Docs/`（大写 D）。引用上述路径时请保持这个大小写，避免出现两个只差大小写的目录。
