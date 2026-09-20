# RGTest

Unity 2022.3.62f3c1 节奏游戏（Built-in Render Pipeline）；玩法与 UI 的验证记录在 `Docs/`。

## Submodule: `bgv-chartgen/`

`bgv-chartgen/` 是一个独立仓库（谱面生成器），登记为子模块只是为了让工具链有单一的根目录。

**它不是 Unity 工程的一部分。** 本仓库单独克隆即可正常打开、编译、运行，不需要这个子模块。

该子模块仓库是私有的，URL 走 HTTPS。没有凭据时 git 不报错，而是**停下来问用户名**——无人值守的 shell 里就是挂住。

```sh
git clone https://github.com/chishau123-commits/RGTest.git   # 默认不拉子模块，够用
GIT_TERMINAL_PROMPT=0 git submodule update --init            # 要递归时，让它失败而不是卡住
```

`bgv-chartgen` 报**认证**失败：主仓库此时已经克隆完成，跳过即可，不影响任何 Unity 侧的工作。

报 **`did not contain <sha>`** 是另一回事，见下一节——那条要处理，不能跳过。

### gitlink 指向子模块的 `main`

登记的 commit 必须在子模块的 `origin/main` 上。指向工作分支的 commit，那条分支被 squash 合并并删除之后，此后每一次递归克隆都会失败。改动指针后、提交前确认：

```sh
cd bgv-chartgen && git branch -r --contains $(git rev-parse HEAD)   # 结果里要有 origin/main
```

## Agent skills

### Issue tracker

Issues live as GitHub issues in `chishau123-commits/RGTest`, operated via the `gh` CLI. See `Docs/agents/issue-tracker.md`.

改动落在 `bgv-chartgen/` 里的，issue 发那个子模块自己的 tracker；没有该仓库访问权限时，发在本仓库并给标题加 `[chartgen]` 前缀。仓库名、路由依据与 `gh` 的仓库解析顺序都在 `Docs/agents/issue-tracker.md` 的 Scope 一节。

### Triage labels

The five canonical triage roles, each label string equal to its name. See `Docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and `Docs/adr/` at the repo root, both created lazily. See `Docs/agents/domain.md`.

`bgv-chartgen/` 是独立仓库，自带 `CONTEXT.md` 与 `docs/adr/`。两边的术语表互不覆盖，各管各的仓库。

> **本仓库**的文档目录记为 `Docs/`（大写 D），引用本仓库路径时保持这个大小写，避免出现两个只差大小写的目录。`bgv-chartgen/` 用的是小写 `docs/`，那是它自己仓库的约定。
