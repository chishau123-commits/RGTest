# mattpocock/skills，已安装到本仓库

来源：<https://github.com/mattpocock/skills>（MIT）
上游版本：1.2.3
安装日期：2026-09-25
安装范围：upstream 的 promoted 组，`skills/engineering/` 18 个 + `skills/productivity/` 7 个，共 25 个技能。

`misc/`、`in-progress/`、`deprecated/` 三个桶没有安装：upstream 明确它们不进正式发行集（见上游 `CLAUDE.md` 与 `.claude-plugin/plugin.json`）。需要时再从上游按同样的方式取。

## 为什么放在 `.agents/skills/`

- 项目级、随仓库提交，不写用户目录（`~/.claude/skills`、`~/.agents/skills` 都没有动）。
- 这是 upstream 给非 Claude Code 的 harness 准备的形态：纯文件、可编辑、无需插件系统。
- DSH 的项目技能根包含 `<repo>/.agents/skills`，所以本仓库的会话能直接发现它们；Codex 等读 `AGENTS.md` 一系的工具同样认这个路径。

每个技能是一个目录包 `<name>/SKILL.md` 加自己的附属文件（`agents/openai.yaml`、`scripts/`、几个 `*.md` 参考页）。SKILL.md 本体没有改过。

## 请不要覆盖的既有内容

`setup-matt-pocock-skills` 这个技能**已经在本仓库跑过了**，产物就是仓库里既有的那套上下文，重新跑或覆盖会丢掉同事的配置：

- `AGENTS.md` 的 `## Agent skills` 段
- `Docs/agents/issue-tracker.md`、`Docs/agents/triage-labels.md`、`Docs/agents/domain.md`
- `CLAUDE.md`（它 `@AGENTS.md` 导入上面那份）

`SKILL.md` 里提到的 `docs/agents/`（小写）是 upstream 文档的说法；本仓库的约定是 `Docs/`（大写 D），以仓库既有文件为准。

## 怎么更新

从上游重新取一份，按桶覆盖同名目录即可，不要动上面的既有上下文。注意不要用 `npx skills@latest add mattpocock/skills` 无脑重跑：那个交互式安装器会让你重新选技能和 harness，并且可能重写 `AGENTS.md` / `CLAUDE.md`。
