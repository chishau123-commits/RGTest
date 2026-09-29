# Agent skills for code-generated video — research report

**Target audience:** a user running DeepSeek Harness (Claude-Code-like agent) who wants skills that make a coding agent *write code that renders video*.
**All star/fork/license/date figures observed 2026-09-25.** Anything I could not verify is marked **unverified**.

---

## 0. Executive summary

| Question | Answer |
|---|---|
| Is there an official Remotion skill? | **Yes.** [`remotion-dev/skills`](https://github.com/remotion-dev/skills) (4,709★), docs at <https://www.remotion.dev/docs/ai/skills>, install `npx skills add remotion-dev/skills` or in-project `npx remotion skills add`. Generic across Claude Code / Codex / Kimi Code / Cursor / OpenCode. |
| Best-supported portable install path | `npx skills add <owner>/<repo>` — the **`skills` CLI from `vercel-labs/skills`** (npm `skills` **1.7.0**, **MIT**, requires **Node ≥ 22.20.0**, bins `skills` and `add-skill`). Its own keyword list advertises ~100 supported harnesses including Claude Code, **Codex**, **gemini-cli**, **kimi-code-cli**, **Cursor**, **opencode**, **qwen-code**, **zed**, Windsurf, Copilot, OpenClaw, **hermes-agent**. **This is the key portability fact** — skills installed this way are not Claude-locked. |
| Is the curated index useful? | Barely. `ella-wang-opus/awesome-video-skills` is 3★ / 0 forks, no license, 24 open issues. It's a usable *list* but it contains **zero real AI-video-API skills** and does not separate the two categories. |
| Highest-adoption community skills | `browser-use/video-use` (27,040★), `Vincentwei1021/video-shotcraft` (9,526★), `affaan-m/ECC` (267,281★, contains a real `manim-video` skill), `Vincentwei1021/anything2explainer` (2,058★) |
| Official Manim skill? | **No** official one from `3b1b/manim` or `ManimCommunity/manim`. Community skills exist: `Yusuke710/manim-skill`, plus `manim-video` inside `affaan-m/ECC`. |
| AI-video-model-API skills (Sora/Veo/Kling/Runway)? | Yes, mostly **vendor-official**: `runwayml/skills`, `black-forest-labs/skills`, `MiniMax-AI/MiniMax-MCP`, official Kling skill on ClawHub. See §4. |
| Fallback with no skill at all | 4 commands: `npx create-video@latest --yes --blank my-video` → `npm i` → `npm run dev` → `npx remotion render`. See §6. |

> **Licensing traps to flag:** `remotion-dev/remotion` itself is **not OSI-licensed** (`NOASSERTION`; source-available with a paid commercial tier at <https://www.remotion.dev/docs/license/pricing>), and `Vincentwei1021/anything2explainer` is **PolyForm Noncommercial** (commercial use needs the author's permission). `remotion-dev/skills` and `BayramAnnakov/remotion-video-director` have **no license file at all**.

---

## 1. The curated index

### `ella-wang-opus/awesome-video-skills`
<https://github.com/ella-wang-opus/awesome-video-skills>

- **What it is:** a curated, video-first index of open-source agent skills. Not itself a skill.
- **Maturity:** **3★ / 0 forks**, **no license**, created 2026-08-28, `pushed_at` 2026-08-29, **24 open issues** (the issues *are* the entry-submission/voting mechanism).
- **Scope rule (stated in README):** entries must provide "an installable `SKILL.md` workflow or a clearly documented agent-skill suite for **Claude or Codex**"; "general video libraries and prompt-only collections are out of scope."
- **Language:** bilingual — `README.md` (EN) + `README.zh-CN.md` (中文). "Last reviewed · August 2026".
- **Key limitations:** does **not** split code-rendered from AI-video-API skills; ~90% code-rendered; the only hosted-model-adjacent entry is `Alisa0808/vibe-creating-skill` (prompt rewriting for Seedance/Kling/Veo — *not* API calls). **Do not treat it as an authority on AI-video APIs.**

**Full decoded entry list** (18 core + 5 visual-support + 6 visual-resource entries):

**Core video production (18):**

| Skill | Focus | Stack |
|---|---|---|
| [Alisa0808/vox-director](https://github.com/Alisa0808/vox-director) | Paper-collage explainer w/ narration, music, captions | code-rendered |
| [Alisa0808/vibe-creating-skill](https://github.com/Alisa0808/vibe-creating-skill) | Rewrites ideas into text-to-video prompts for Seedance/Kling/Veo | **prompt-only** |
| [Alisa0808/coflow](https://github.com/Alisa0808/coflow/tree/main/coflow/skills/coflow-video) | Media canvas → video, Codex | code-rendered |
| [Vincentwei1021/video-shotcraft](https://github.com/Vincentwei1021/video-shotcraft) | Cinematic product promos | Remotion |
| [Vincentwei1021/video-talkcraft](https://github.com/Vincentwei1021/video-talkcraft) | Voiceover explainers, word-level sync | Remotion |
| [AbubakrChan/product-launch-motion](https://github.com/AbubakrChan/product-launch-motion) | Word-locked product films, camera motion, loudness | code-rendered |
| [appergb/ClipSkills](https://github.com/appergb/ClipSkills) | Editing craft suite, 12 tool-agnostic modules | ffmpeg-ish |
| [theSamPadilla/montaj](https://github.com/theSamPadilla/montaj) | CLI-first agent-native editor | app + skill contracts |
| [TomGranot/faceless-channel-factory](https://github.com/TomGranot/faceless-channel-factory) | Faceless channel system w/ rights evidence | pipeline |
| [bryanwhl/ffmpeg-video-editor](https://github.com/bryanwhl/ffmpeg-video-editor) | Trim/transcode/subs/overlays/grading/loudness | **ffmpeg CLI** |
| [zenstory-ai/drama-skills](https://github.com/zenstory-ai/drama-skills) | 10-skill short-drama suite | mixed |
| [calesthio/OpenMontage](https://github.com/calesthio/OpenMontage) | Full agentic production studio | mixed |
| [heygen-com/hyperframes](https://github.com/heygen-com/hyperframes) | 20 skills: HTML/CSS/media → deterministic MP4 | **HTML + seekable animation** |
| [remotion-dev/skills](https://github.com/remotion-dev/skills) | Official Remotion toolkit | **Remotion** |
| [haidrrrry/claude-remotion-skill](https://github.com/haidrrrry/claude-remotion-skill) | Motion design, B-roll, captions, SFX, frame QA | Remotion |
| [Bomx/super-video-maker-skill](https://github.com/Bomx/super-video-maker-skill) | Full pipeline: avatars, AI B-roll, screen rec, captions | mixed |
| [BayramAnnakov/remotion-video-director](https://github.com/BayramAnnakov/remotion-video-director) | Creative direction / deliberation | Remotion |
| [hyt315/notebook-video](https://github.com/hyt315/notebook-video) | Notebook-style educational video, **Chinese TTS timing** | code-rendered |

**Visual support (5):** [kylezantos/design-motion-principles](https://github.com/kylezantos/design-motion-principles), [MengTo/Skills → agent-skills/web-design/gsap](https://github.com/MengTo/Skills/tree/main/agent-skills/web-design/gsap), [nateherkai/scroll-craft](https://github.com/nateherkai/scroll-craft), [LiamGvchi/gc-minimal-zine-poster](https://github.com/LiamGvchi/gc-minimal-zine-poster), [anthropics/claude-code → frontend-design skill](https://github.com/anthropics/claude-code/tree/main/plugins/frontend-design/skills/frontend-design).

**Visual resources (6, no SKILL.md):** [MengTo/threeui](https://github.com/MengTo/threeui), [motiondivision/motion](https://github.com/motiondivision/motion), [pmndrs/react-three-fiber](https://github.com/pmndrs/react-three-fiber), [magicuidesign/magicui](https://github.com/magicuidesign/magicui), [airbnb/lottie-web](https://github.com/airbnb/lottie-web).

---

## 2. Official / first-party: Remotion

### `remotion-dev/remotion` — the framework
<https://github.com/remotion-dev/remotion>
- **60,385★ / 4,661 forks**, created 2020-06-23, `pushed_at` 2026-09-25 (active same-day), TypeScript, docs <https://www.remotion.dev/docs/>, npm `remotion` 4.0.529.
- **License: `NOASSERTION`** — *not* OSI. Source-available; companies above a size threshold need a paid license.
- This is a **library/app**, not a skill.

### `remotion-dev/skills` — the official agent skill ✅
<https://github.com/remotion-dev/skills> · docs: <https://www.remotion.dev/docs/ai/skills>
- **4,709★ / 529 forks**, created 2026-01-19, pushed 2026-09-25, **no license file**. Also vendored in-monorepo at `remotion-dev/remotion/tree/main/packages/skills`.
- **It is a skill**: `SKILL.md` + scripts, following the <https://agentskills.io> convention.
- **12 skills:** `remotion-best-practices` (umbrella), `remotion-create`, `remotion-markup`, `remotion-studio`, `remotion-render`, `remotion-maps`, `remotion-captions`, `remotion-saas`, `remotion-interactivity`, `remotion-docs`, `remotion-upgrade`, `remotion-multimedia`.
- **Install (three ways):**
  ```bash
  npx skills add remotion-dev/skills          # global, any agent
  npx remotion skills add                     # in-project; installs to .agents/skills
  # or during scaffold: bun create video   (offers skills)
  ```
  `npx remotion skills update` refreshes them.
- **Portability: generic.** Docs name Claude Code, Codex, **Kimi Code**, and Cursor; the coding-agents page adds **OpenCode**. Install target is `.agents/skills`, with `.claude/skills` symlinked for Claude Code compatibility — i.e. deliberately harness-neutral. The `/remotion-*` names are slash-command *labels*, not Claude-only tooling.
- **Chinese docs: no** (EN only). Localized pages exist for e.g. `/docs/ai/skills`? **unverified** — not observed.

### Official Remotion AI surface (all verified, HTTP 200)
| Page | URL | Note |
|---|---|---|
| AI hub | <https://www.remotion.dev/docs/ai> | landing page |
| Coding Agents | <https://www.remotion.dev/docs/ai/coding-agents> | exact no-skill walkthrough |
| Skills | <https://www.remotion.dev/docs/ai/skills> | the official skill |
| Plugins | <https://www.remotion.dev/docs/ai/plugins> | first-party plugins |
| Claude Code plugin | <https://www.remotion.dev/docs/ai/claude-code-plugin> | `claude plugin marketplace add remotion-dev/claude-code-plugin` → `claude plugin install remotion@remotion` |
| Codex / Cursor / Copilot / Kimi plugins | `/docs/ai/codex-plugin`, [cursor.com/marketplace/remotion](https://cursor.com/marketplace/remotion), `/docs/ai/github-copilot-plugin`, `/docs/ai/kimi-code-plugin` | |
| WebMCP | <https://www.remotion.dev/docs/ai/webmcp> | live Studio tool surface; docs say **only ChatGPT Codex** supports it, I/O "not currently stable" |
| MCP | <https://www.remotion.dev/docs/ai/mcp> | **DEPRECATED** — "New installations are not recommended"; hosted MCP shuts down **no earlier than 2026-08-31**. Use `/remotion-docs` instead. Issue [#9055](https://github.com/remotion-dev/remotion/issues/9055) |

**404s (don't chase):** `/docs/ai-agents`, `/docs/claude`, `/docs/cursor`, `/docs/copilot`.

**Docs for agents:** `https://www.remotion.dev/llms.txt` **exists** (real link index). `llms-full.txt` **404**, `/docs/llms.txt` **404**. Better: append **`.md`** to any doc URL, or send `Accept: text/markdown` — both officially documented.

**Prompts / elements galleries:** <https://remotion.dev/prompts> · <https://remotion.dev/elements>

---

## 3. Community skills, code-rendered (the main body)

### Tier A — high adoption

#### `browser-use/video-use` — video **editing** with coding agents
<https://github.com/browser-use/video-use>
- **27,040★ / 3,203 forks**, MIT, created 2026-04-12, pushed 2026-09-24, 119 open issues.
- **What it does:** drop raw footage in a folder, chat, get `final.mp4`. Cuts filler words and dead space, auto color-grades, 30 ms audio fades at cuts, burns subtitles, self-evaluates the render at every cut boundary.
- **Stack:** **ffmpeg** for the edit; **ElevenLabs Scribe** for word-level transcription + speaker diarization; animation overlays generated via **HyperFrames, Remotion, Manim, or PIL** "spawned in parallel sub-agents, one per animation".
- **Skill or app?** A **skill** — `SKILL.md` + `install.md` + `helpers/`, and the README is explicit that the helper scripts are where the editing lives. Outputs go to `<videos_dir>/edit/`.
- **Install:**
  ```bash
  # agent-driven (recommended, described as working on Claude Code, Codex, Hermes, Openclaw):
  #   "Set up https://github.com/browser-use/video-use for me. Read install.md first ..."
  # manual:
  git clone https://github.com/browser-use/video-use ~/Developer/video-use
  ln -sfn ~/Developer/video-use ~/.claude/skills/video-use     # or ~/.codex/skills/video-use
  cd ~/Developer/video-use && uv sync      # or: pip install -e .
  brew install ffmpeg                      # required; yt-dlp optional
  cp .env.example .env                     # ELEVENLABS_API_KEY=...
  ```
- **Portability:** mostly generic. Explicitly lists Claude Code, Codex, Hermes, Openclaw and "any agent with shell access". **But** it leans on **parallel sub-agents** for animations and on `project.md` session memory — an agent without subagents still works, just serially.
- **License:** MIT. **Chinese docs: no** (EN only).
- **Requires a paid key:** ElevenLabs API key (free tier exists).

#### `Vincentwei1021/video-shotcraft` — cinematic product promo
<https://github.com/Vincentwei1021/video-shotcraft> · gallery <https://vincentwei1021.github.io/video-shotcraft/>
- **9,526★ / 865 forks**, **Apache-2.0**, created 2026-07-19, pushed 2026-09-23, homepage set, GitHub Pages on.
- **What it does:** turns Claude Code/Codex into a motion-design studio for promo/launch/demo films. Ships **157 shot recipe cards**, **214 styles**, 214 motion previews, a validated **"Ink Press"** template (36.2 s, 1920×1080, 30 fps, 10 shots), 149 SFX in 16 categories, a **Motion Workbench** browser timeline editor, and **JianYing / CapCut CN project export**.
- **Stack: Remotion** (React + TypeScript), real page captures, 2.5D camera moves.
- **Skill or app?** A **skill** (`SKILL.md` + `references/` + `demos/` + `template/` + `workbench/`), with a runnable template bundled.
- **Install:**
  ```bash
  npx skills add Vincentwei1021/video-shotcraft
  # or
  git clone https://github.com/Vincentwei1021/video-shotcraft.git
  cd video-shotcraft
  ln -s "$(pwd)" ~/.claude/skills/video-shotcraft   # Claude Code
  ln -s "$(pwd)" ~/.codex/skills/video-shotcraft    # Codex
  ```
  Also: "hand the repo link to your agent and say *Install this skill for me: <url>*".
- **Portability: generic.** README targets "Claude Code or Codex … or a similar agent"; no Claude-only tools required. The `/plugin`-style install is not used.
- **Chinese support: excellent** — trilingual `README.md` (EN) / `README_CN.md` (中文) / `README_JA.md` (日本語); the Workbench guide is Chinese-only.
- **Headless/CI gotchas documented:** `--concurrency=1` on ≤2-core boxes; use `chrome-headless-shell` not full Chrome; `--browser-executable=<path>` if remotion.media is blocked.

#### `affaan-m/ECC` — huge multi-harness skill collection containing real video skills
<https://github.com/affaan-m/ECC>
- **267,281★ / 39,930 forks**, MIT, created 2026-01-18, pushed 2026-09-24, 292 skills / 68 agents / 94 commands. **Not video-focused** — but it *does* contain verified video skills:
  - **`skills/manim-video/SKILL.md`** (verified by reading it) — Manim technical explainers. Requires `manim` CLI + `ffmpeg`; hands off to `video-editing` and `remotion-video-creation`. Ships `assets/network_graph_scene.py`; smoke test `manim -ql assets/network_graph_scene.py NetworkGraphExplainer`.
  - `skills/remotion-video-creation`, `skills/video-editing`, `skills/fal-ai-media` (AI-video APIs — see §4).
- **Install (multi-harness, this is the interesting part):**
  ```bash
  npx ecc-universal@2.2.2 setup                 # Claude Code plugin (guided)
  npx ecc-universal@2.2.2 install --guided      # choose Claude Code / Codex / Kimi Code
  ./install.sh --profile minimal --target gemini   # also: cursor, zed, opencode, qwen, hermes, openclaw, ...
  ```
  Claude Code native path: `/plugin marketplace add https://github.com/affaan-m/ECC` then `/plugin install ecc@ecc`.
- **Portability:** broad but **uneven** — the README carries its own "Platform Support" parity matrix: Claude Code is primary, Codex partial, others "capability-limited adapters". Treat video skills as portable, the hooks/agents layer as not.
- **Chinese support:** yes — `README.zh-CN.md` plus zh-TW, ja-JP, ko-KR and 9 more.
- **Caveat:** ECC is a whole agent *harness*, not a video tool. Installing it to get `manim-video` is heavy.

#### `Vincentwei1021/anything2explainer` — topic → narrated explainer
<https://github.com/Vincentwei1021/anything2explainer>
- **2,058★ / 290 forks**, **PolyForm Noncommercial 1.0.0** (commercial use needs the author's authorization; made videos are yours), created 2026-09-08, pushed 2026-09-18.
- **What it does:** topic (or article) in → 1280×720 30 fps H.264 explainer with synchronized voiceover, word-aligned subtitles, chapter cards, HUD, chapter progress bar, plus the full paper trail (research doc, narration, storyboard, per-shot source, QC reports).
- **Stack: Remotion 4** (React + TypeScript). TTS: `edge-tts` (zh, default `zh-CN-YunxiNeural`) / `kokoro-82m` (en, `am_liam`) / `kokoro_onnx` / `piper` / bring-your-own audio. `ffmpeg` required.
- **Skill or app?** A **skill** — "not a CLI"; `SKILL.md` (9 stages, 4 human checkpoints) + `reference/` specs + a compilable Remotion template + working scripts.
- **Install:**
  ```bash
  git clone https://github.com/Vincentwei1021/anything2explainer.git
  ln -s "$PWD/anything2explainer" ~/.claude/skills/anything2explainer
  ln -s "$PWD/anything2explainer" ~/.codex/skills/anything2explainer
  # deps: Node >= 18, ffmpeg, and a python venv with
  #   pip install 'edge-tts==7.2.8' numpy pillow scipy
  ```
- **Portability: good, and explicitly claimed:** FAQ says it is written for Claude Code and Codex (those are what it was run with) but "the skill itself is plain Markdown plus a Remotion project, so any agent that reads `SKILL.md`-style skill folders and can run shell commands should be able to follow it." It does use **parallel build agents** (5–7 shots each, up to 14) and **QC agents** — a big throughput advantage if your harness has subagents, a wall-clock cost if not.
- **Chinese support: yes, first-class** — separate `README_ZH.md`, `lang: 'zh' | 'en'` in `src/config.ts` driving typography, subtitle budgets and default voice; both an English and a Chinese reference film.
- **Platform:** macOS verified; Linux/Raspberry Pi notes included; **Windows untested**. Disk: ~2–3 GB. Time: ~1–3 h.
- **Not for:** vertical 9:16 (landscape only), talking heads, live-action-heavy films, replicating existing videos.

### Tier B — smaller but real

#### `Vincentwei1021/video-talkcraft` — voiceover-driven explainers
<https://github.com/Vincentwei1021/video-talkcraft> · gallery <https://vincentwei1021.github.io/video-talkcraft/>
- **1,225★ / 113 forks**, license **NOASSERTION**, created 2026-08-22, pushed 2026-09-22.
- Sibling of video-shotcraft. Script + finished voiceover in → every motion beat locks to the voice; word-level timestamps aligned locally (median 20–40 ms/char), **109 motion recipe cards**, 7-layer anti-slideshow shot system, plain-cut subtitles, triple-gate QA. **Stack: Remotion.** Same recipe-card + template workflow.
- Skill + bundled Remotion project. Same install pattern as video-shotcraft (`npx skills add Vincentwei1021/video-talkcraft` — **unverified specifically**, but it is the same author/series; the repo page shows a live gallery and the same layout).
- **Chinese support:** the series is Chinese-authored; docs are EN primary with CN/JP siblings in the same family (**unverified** for this exact repo).

#### `Changroro/code-video` — **not** Remotion; HTML canvas + headless Chrome + ffmpeg
<https://github.com/Changroro/code-video>
- **34★ / 7 forks**, **MIT**, created 2026-09-23, pushed 2026-09-25, JavaScript.
- Research a topic → 1080p MP4 in one of ~20+ styles (hand-drawn + 8-bit minis default, brand motion graphics, sand art, lyric video, CRT terminal, thermal receipt, transit map, blueprint, brand pastiches, "any site's `DESIGN.md`"). Music and SFX are **synthesized in code**; deterministic. A 45 s 1080p video renders in ~1 min on a 10-core laptop; a whole run took 4–13 min in their tests.
- **Stack:** every frame is a function of time drawn on an **HTML canvas**, captured by **headless Chrome** in parallel pages as JPEG, piped to **ffmpeg**. `template/kit.js` + one module per style. **No video-generation model, no stock footage.**
- **Skill or app?** A **skill** (`SKILL.md`, `references/`, `scripts/`, `template/`) plus a Claude Code plugin manifest at `.claude-plugin/plugin.json`.
- **Install:**
  ```bash
  npx skills add Changroro/code-video -g
  # Claude plugin marketplace:
  #   /plugin marketplace add Changroro/plugins
  #   /plugin install code-video@changroro
  # manual:
  git clone https://github.com/Changroro/code-video ~/.claude/skills/code-video   # or ~/.codex/skills/
  ```
  Requirements: **Node.js 18+ with npm, ffmpeg built with libx264, Google Chrome, and [uv](https://docs.astral.sh/uv/)** (uv for helper scripts; numpy/scipy/librosa installed on the fly).
- **Portability: generic** — the `npx skills add` path is first-class; the `/plugin` path is Claude-only but optional.
- **Chinese support: no — the second language is Korean** (`README.ko.md`). Worth knowing given the "Chinese-authored repos" assumption.
- **Design note:** states an A/B finding that stripping process rules made a video 23–51% cheaper with no visible quality drop.

#### `SkillMedev/remotion-video-production` — three minimal Remotion skills
<https://github.com/SkillMedev/remotion-video-production>
- **0★ / 0 forks**, **MIT**, created 2026-06-21, pushed 2026-07-05, ~20 KB. Org: Skill Me (vendor catalog).
- Three sequential skills: **Remotion Setup** (Node check, `create-video` scaffold, skills install, folder conventions, Google Fonts, smoke-test render), **Remotion Compose** (natural-language brief → complete React/TS composition using `useCurrentFrame`, `interpolate`, `spring`, `AbsoluteFill`, `Sequence`, registered in `Root.tsx`), **Remotion Render** (CLI render, 1080p/4K/9:16 presets, concurrency, codec, batch variants from JSON, Lambda).
- **Skill, not app.** Plain `SKILL.md` files.
- **Install:**
  ```bash
  npx skills add SkillMedev/remotion-video-production
  # or copy skills/<slug>/SKILL.md into your skills directory
  # or: https://skillme.dev/pack/remotion-video-production
  ```
- **Portability: generic in substance** (plain SKILL.md; README says "Skills are portable `SKILL.md` files"), though written for Claude Code.
- **Chinese support: no.** The RN/docs are EN.
- **Verdict:** the cleanest *pedagogical* option (it's basically the official tutorial as three skills), but zero adoption signal — the vendored canonical copies live in the Skill Me catalog.

#### `haidrrrry/claude-remotion-skill`
<https://github.com/haidrrrry/claude-remotion-skill>
- **195★ / 17 forks**, **MIT**, created 2026-06-13, pushed 2026-08-12, TypeScript.
- Motion graphics / AI video editing / B-roll / captions / sound design on **Remotion**. Its pitch is explicitly *craft over framework*: "Remotion is never the bottleneck — motion-design craft is." Encodes **10 non-negotiable motion rules** (springs+bezier not linear interpolation, 3-property entrances, 3–6 frame stagger, five-layer stack bg→assets→graphics→grade→grain+vignette, Ken Burns on every still, `OffthreadVideo` for footage, timing derived from `fps`, one `theme.ts`) plus a **mandatory render → extract frames with ffmpeg → inspect → fix → re-render** loop.
- **Skill:** folder `remotion-motion-graphics/` with `SKILL.md` + `references/motion-patterns.md` (17 copy-paste components) + `references/design-rules.md` + `assets/theme.ts`. Its `examples/` is a runnable Remotion project with four finished compositions.
- **Install (plain folder copy — no marketplace needed):**
  ```bash
  git clone https://github.com/haidrrrry/claude-remotion-skill.git
  mkdir -p ~/.claude/skills
  cp -r claude-remotion-skill/remotion-motion-graphics ~/.claude/skills/   # per-user
  # or per-project:  mkdir -p .claude/skills && cp -r ... .claude/skills/
  ```
  Claude Desktop / Claude.ai: upload `remotion-motion-graphics.skill` in Settings → Capabilities → Skills.
- **Portability: high.** It is a plain `SKILL.md` folder with no Claude-only tools — copy it into whatever skills directory your harness reads. Its only dependency is ffmpeg (for the frame-inspection loop) and Remotion. Verified self-check: the skill should read itself *before* writing code and must extract+inspect frames before delivering.
- **Chinese: no.** (Author is on Instagram as `@haidercodes`.)

#### `BayramAnnakov/remotion-video-director`
<https://github.com/BayramAnnakov/remotion-video-director>
- **41★ / 4 forks**, **NO license file**, created 2026-03-13, pushed 2026-03-13 (stale), no primary language.
- Interactive **Claude Code** skill for Remotion videos via expert-guided deliberation — strategy, scenario design, production, voiceover, music, review. Ships a promo asset.
- Skill. **Claude-specific framing** ("Interactive Claude Code skill"); portability to DeepSeek Harness **unverified**. **Chinese: no.**

#### `cajias/agentic-video-skills` — vendored marketplace (prompt/storyboard, not code-rendered)
<https://github.com/cajias/agentic-video-skills>
- **2★ / 0 forks**, **no license**, created 2026-06-11, pushed 2026-06-11 (never updated), branch `master`, Python, 184 KB.
- An **aggregate Claude Code plugin marketplace** of *vendored* (copied, not forked) community skills, with attribution in `NOTICE`:
  | Plugin | What it does | Upstream | Upstream license |
  |---|---|---|---|
  | `prompt-master` | Crafts/refines generative video & image prompts | `nidhinjs/prompt-master` | MIT |
  | `director` | Shot direction, camera, scene blocking | `wuwangzhang1216/DirectorSKILL` | MIT (SKILL.md) |
  | `ai-video-storyboard` | Storyboards / shot lists | `aicontentskills/ai-video-storyboard-skill` | **none** |
- **Install (Claude-only):**
  ```text
  /plugin marketplace add cajias/agentic-video-skills
  /plugin install prompt-master@agentic-video-skills
  /plugin install director@agentic-video-skills
  /plugin install ai-video-storyboard@agentic-video-skills
  ```
- **Portability: Claude-specific** — entirely `/plugin`-driven. Not useful for DeepSeek Harness as-is, though the *upstream* repos may be.
- **Important:** these are **prompt/direction/storyboard** skills, **not** code-rendered video skills.

#### `bryanwhl/ffmpeg-video-editor` — the ffmpeg entry in the index
<https://github.com/bryanwhl/ffmpeg-video-editor>
- **7★ / 1 fork**, **MIT**, Shell, created 2026-03-03, pushed 2026-03-03 (stale), no description.
- Per the index: trimming, transcoding, subtitles, overlays, grading, audio normalization, transitions, social exports with **ffmpeg CLI**.
- Skill (Shell + instructions). **Install path unverified.** **Chinese: unverified.**

### Notable absences
- **`anthropics/skills`** (<https://github.com/anthropics/skills>) — Anthropic's official skills repo. Verified by reading its README: it covers *Creative & Design, Development & Technical, Enterprise & Communication, Document* skills. **No video / ffmpeg / animation skill.** Install via `/plugin marketplace add anthropics/skills` then `example-skills` or `document-skills`.
- **`openai/skills`** (<https://github.com/openai/skills>) — **the README says the repository is deprecated**; current examples moved to [openai/plugins](https://github.com/openai/plugins) with the [Build plugins](https://developers.openai.com/codex/plugins/build) guide. A `sora` skill has been reported in this repo via third-party mirrors, but **I could not verify it directly** — treat "official OpenAI Sora skill" as **unverified**, and note the skill catalog itself is deprecated.

---

## 4. AI-video-model API skills (a **separate** category)

These call hosted generative video models. They are **not** code-rendered, they need **paid metered API keys**, and they barely appear at all in the curated index of §1. **The good ones live in vendor org repos — search by vendor org, not by "awesome" lists.**

### 4.1 Vendor-official skill packages (highest trust)

#### `runwayml/skills` — official Runway ✅ best single pick
<https://github.com/runwayml/skills>
- **70★ / 17 forks**, **MIT**, created 2026-02-10, pushed 2026-08-28, Python.
- API `https://api.dev.runwayml.com` (`X-Runway-Version: 2024-11-06`). Models: `gen4.5`, `gen4_turbo`, `gen4_aleph`, **`veo3` / `veo3.1` / `veo3.1_fast`** (Google, resold), **`seedance2`** (ByteDance, resold), `gen4_image`, ElevenLabs audio, `gwm1_avatars`.
- **Skill suite** — 12 `SKILL.md` dirs (`rw-generate-video`, `rw-generate-image`, `rw-generate-audio`, `use-runway-api`, `rw-check-org-details`, `runway-dev{,-models,-model-routers,-characters,-recipes,-workflows}`), plus Python scripts `generate_video.py` / `generate_audio.py` / `get_task.py`. Packaged as a plugin too (`.claude-plugin/plugin.json`, `.cursor-plugin/plugin.json`).
- **Install:** `npx skills add runwayml/skills` **or** `claude plugin marketplace add anthropics/claude-plugins-community` → `claude plugin install runway-api-skills@claude-community`.
- **Portability: generic** — README names Claude Code, Cursor, Codex, "other compatible agents".
- Needs `RUNWAYML_API_SECRET`, **$10 minimum prepaid credits**. Skill docs publish per-model credit costs (`veo3` 40 credits/s, `gen4_turbo` 5 credits/s) — useful for cost gating. **Chinese: no.**

#### `black-forest-labs/skills` — official BFL / FLUX 3 video
<https://github.com/black-forest-labs/skills>
- **120★ / 10 forks**, **MIT**, pushed 2026-09-09, branch **`master`** (not `main`).
- API `https://api.bfl.ai` (`x-key` header). Video skills: `flux-3-video` (router) + `flux-3-prompt-doctor`, `-cinematic-inserts`, `-keyframes-continuation`, `-audio-dialogue`, `-generate`, `-archival-formats`, `-product-ads`; plus `flux-image-best-practices`, `bfl-api`. Modes `t2v` / `i2v` / `v2v` / `draft_enhance`; `draft: true` gives a cheap preview + `draft_cache`.
- **Install:** `npx skills add black-forest-labs/skills` (or `--skill flux-3-video`); Claude path `/plugin marketplace add black-forest-labs/skills` → `/plugin install flux-3-video@black-forest-labs`.
- **Portability: generic** — targets the agentskills.io spec. **Chinese: no.**

#### `Pika-Labs/Pika-Plugins` — official Pika
<https://github.com/Pika-Labs/Pika-Plugins>
- **All three surfaces**: remote **MCP server** + **9 skills** + **Claude Code plugin**, v1.4.0. **Apache-2.0** (per README). Stars/forks **unverified** (rate limit).
- Remote MCP `https://mcp.pika.me/api/mcp` — 58 atomic tools, OAuth; internally routes models incl. SeeDance.
- **Install:** MCP `claude mcp add --transport http pika https://mcp.pika.me/api/mcp`; skills `npx skills add Pika-Labs/Pika-Plugins`; plugin `claude plugin marketplace add Pika-Labs/Pika-Plugins` → `claude plugin install pika@pika-plugins`.
- **Portability: mixed** — MCP and skills paths are generic (50+ agents via `vercel-labs/skills`); the plugin path needs Claude Code ≥ v2.0.12.
- **No BYO provider keys** — draws on Pika credits. `/pika:*` workflows: podcast, explainer, ugc-ads, app-sizzle, founder-product-video, baseball-trend, kiss-cam, app-store-screens, build-a-brand. **Chinese: no.**

#### `google/skills` — ⚠️ **no dedicated Veo skill**
<https://github.com/google/skills>
- **Apache-2.0**; stars **unverified**. Catalog launched ~2026-08-11 (secondary source).
- Veo is reachable only *inside* `skills/cloud/gemini-api/` + `references/media_generation.md`, via `google-genai`: `client.models.generate_videos(model="veo-3.1-fast-generate-001", ...)`. Also covers Gemini Omni video (`gemini-omni-1.1-flash-preview`; t2v/i2v/reference_to_video/edit, 720p, SynthID + C2PA).
- **Install:** `npx skills add google/skills`; per-harness plugins (`claude plugin marketplace add google/skills`, `codex plugin marketplace add google/skills`).
- **Portability: generic** (Gemini CLI, Claude Code, Antigravity, Codex). **Enterprise framing** — needs Google Cloud / Gemini Enterprise Agent Platform credentials, not a consumer key. **Chinese: no.**

#### OpenAI `sora` skill — **removed**
- `openai/skills` <https://github.com/openai/skills> README now says **"This repository is deprecated"** → `openai/plugins`.
- The Sora skill **did exist** at `skills/.curated/sora/SKILL.md`, **verified at pinned commit `724cd511c96593f642bddf13187217aa155d2554`**. `main` now **404s** (deleted). It used the OpenAI Python SDK with `sora-2` (default) / `sora-2-pro`, 1280×720 default, seconds `4/8/12/16/20`, character references, edit, extend (max 6×20 s = 120 s), a local batch queue via bundled `scripts/sora.py`, `OPENAI_API_KEY`, and a **verified org** account.
- To use it today you must pin that commit:
  `https://raw.githubusercontent.com/openai/skills/724cd511c96593f642bddf13187217aa155d2554/skills/.curated/sora/SKILL.md`
- `openai/plugins` lists **no video plugin** — closest is `plugins/remotion`, which is code-rendered.

### 4.2 Vendor-official MCP servers

| Repo | Model / API | Install | Portability | Maturity (2026-09-25) | Chinese |
|---|---|---|---|---|---|
| [MiniMax-AI/MiniMax-MCP](https://github.com/MiniMax-AI/MiniMax-MCP) | MiniMax **Hailuo** `generate_video` / `query_video_generation`; `MiniMax-Hailuo-02`, 6s/10s, 768P/1080P; +TTS, voice clone, image | `uvx minimax-mcp -y` in client config; stdio **and** SSE | Generic MCP (Claude Desktop, Cursor, Windsurf, OpenAI Agents) | **1,585★ / 283 forks, MIT, pushed 2026-08-20** | **YES** — `README-CN.md`; mainland host `api.minimaxi.com` vs global `api.minimax.io`; key must match region |
| [lumalabs/luma-api-mcp](https://github.com/lumalabs/luma-api-mcp) | Luma **Ray** (`ray-2`/`ray-flash-2`/`ray-1-6`, 540p–4k, 5s/9s, loop, frame0/frame1 keyframes) + Photon image | `sh setup.sh` (prompts for key) | Generic MCP | stars/license **unverified** | no |
| [runwayml/runway-api-mcp-server](https://github.com/runwayml/runway-api-mcp-server) | Runway Dev MCP (project context for building Runway into your app) | TS project, self-host | Generic MCP | **22★ / 13 forks, MIT, pushed 2026-08-17** | no |
| Pika MCP (see above) | Pika 58 tools: image/video/lipsync/music | remote HTTP MCP, OAuth | Generic | Apache-2.0, stars unverified | no |

### 4.3 Multi-model aggregator skills (Veo + Kling + Sora + Seedance behind one key)

#### `SamurAIGPT/Generative-Media-Skills`
<https://github.com/SamurAIGPT/Generative-Media-Skills>
- **4,325★ / 496 forks**, **MIT**, pushed 2026-09-08, Shell. **The highest-adoption AI-video skill found.**
- Wraps the **muapi.ai** aggregator (100+ models: Midjourney v7, Flux Kontext, **Seedance 2.0**, **Kling 3.0**, **Veo3**, Suno) and also exposes an **MCP server** (`muapi mcp serve`, 19 typed tools incl. `muapi_video_generate` over 13 models, `muapi_video_from_image` over 16).
- **Skill library + recipes + MCP server**: 41 workflow recipes, `core/` primitives + `library/` expert skills (Cinema Director, Seedance 2, AI Clipping).
- **Install:** `npm i -g muapi-cli` → `muapi auth configure` → `npx skills add SamurAIGPT/Generative-Media-Skills --all` (or `--skill`, `-a claude-code -a cursor`).
- **Portability: generic** (Claude Code, Cursor, Gemini CLI, OpenCode, MCP, Windsurf). Needs `MUAPI_API_KEY`. **Chinese: no.**

#### `prime-skills/runcomfy-agent-skills`
<https://github.com/prime-skills/runcomfy-agent-skills>
- **52★ / 10 forks**, **MIT**, created 2026-04-30, pushed 2026-05-15.
- Wraps the **RunComfy Model API** via a `runcomfy` CLI. Catalog: **HappyHorse 1.0**, **Wan 2-7** (open weights, `audio_url` lip-sync), **ByteDance Seedance v2 Pro/Fast** + 1-5/1-0, **Kling 3.0 4K/Pro/Standard** + 2-6 + O1 + motion-control, **Google Veo 3-1** (+Fast, extend-video), **MiniMax Hailuo 2-3**, **ByteDance Dreamina 3-0**.
- **Skill router** (`ai-video-generation/SKILL.md`) + siblings `image-to-video`, `video-extend`, `video-edit`, `ai-avatar-video`, `lipsync`, `face-swap`, `runcomfy-cli`.
- **Install:** `npm i -g @runcomfy/cli`; `runcomfy login` or `RUNCOMFY_TOKEN`; `npx skills add agentspace-so/runcomfy-agent-skills --skill ai-video-generation -g`.
- ⚠️ **Inconsistency to flag:** the repo lives at `prime-skills/runcomfy-agent-skills` but the install line **inside** SKILL.md and all skills.sh links use a **different owner**, `agentspace-so/runcomfy-agent-skills`. Verify before installing.
- **Generic** (agentskills.io; declares `allowed-tools: Bash(runcomfy *)`). Notable: a real security section — allowlisted endpoints (`model-api.runcomfy.net`), 2 GiB download cap, prompt-injection warnings for reference assets. **Chinese: no.**

#### `runapi-ai/*` skill family
<https://github.com/runapi-ai> — aggregator **runapi.ai**. Each repo ~28–40 KB, **Apache-2.0**, **0★**: [`kling`](https://github.com/runapi-ai/kling) (pushed 2026-08-21), [`runway-aleph`](https://github.com/runapi-ai/runway-aleph) (2026-08-12), plus `runapi-veo-3-1`, `runapi-runway`, `runapi-kling`, `runapi-runway-aleph` on ClawHub. Companion SDKs **0–1★**.
Kling models: `kling-3.0`, `-turbo`, `-omni`, `kling-o1`, `kling-2.6`, `kling-2.5-turbo`; t2v/i2v/motion-control/avatar/Omni reference-edit.
**Install:** `npx skills add runapi-ai/kling -g`. **Generic** (Claude Code, Codex, Gemini CLI, Cursor). Needs `RUNAPI_API_KEY`; **output URLs expire in 7 days**.

#### Other aggregator / wrapper skills
| Repo | Stars / forks | License | Pushed | Note |
|---|---|---|---|---|
| [vargHQ/skills](https://github.com/vargHQ/skills) | 21 / 2 | MIT | 2026-08-24 | kling, flux, elevenlabs, lipsync, image-to-video, text-to-video. Claude Code, Cursor, Windsurf, OpenCode, ClawHub |
| [gooseworks-ai/goose-video](https://github.com/gooseworks-ai/goose-video) | unverified | MIT (README) | unverified | Video-ad skill templates orchestrating **Veo 3 / Seedance / Higgsfield / ElevenLabs** through **fal.ai**. Needs `FAL_KEY` + `ELEVENLABS_API_KEY`. Quotes real per-cut costs (~$15–25) |
| [deapi-ai/claude-code-skills](https://github.com/deapi-ai/claude-code-skills) | 21 / 3 | MIT | 2026-07-03 | video/image/TTS/transcription via **deAPI** |
| [AceDataCloud/Skills](https://github.com/AceDataCloud/Skills) | 17 / 1 | NOASSERTION | 2026-09-24 | music/image/**video**. Sibling [`SeedanceMCP`](https://github.com/AceDataCloud/SeedanceMCP) (19★, MIT, 2026-09-04) |
| [machina-exm/film-studio-skills](https://github.com/machina-exm/film-studio-skills) | **140** / 23 | none | 2026-08-14 | 7 chained skills for AI-film character consistency. **Model-agnostic** — interviews you about *your* model; calls no vendor API. Also Hermes (`hermes skills tap add`), Codex, OpenCode |
| `writingmate/skills` → `writingmate-mcp` | unverified | MIT (SKILL.md) | unverified | Remote MCP `writingmate.ai/api/mcp`: **Seedance, Sora, Veo, Kling, PixVerse**. `npx skills add writingmate/skills --skill writingmate-mcp` |
| `fal-ai-media` inside `affaan-m/ECC` | — (part of ECC, 267,281★) | MIT | 2026-09-24 | **fal.ai MCP**: text-to-image (Nano Banana 2/Pro), **text/image-to-video (Seedance 1.0 Pro, Kling Video v3 Pro, Veo 3)**, TTS (CSM-1B), video-to-audio (ThinkSound). MCP tools `search`/`find`/`generate`/`result`/`status`/`cancel`/`estimate_cost`/`models`/`upload`; server added as `npx -y fal-ai-mcp-server` with `FAL_KEY`. Install `npx skills add https://github.com/affaan-m/everything-claude-code/tree/main/skills/fal-ai-media`. The skill itself is flagged **"drift-prone"** — verify model IDs/pricing before promising a model or cost |

### 4.4 Official Kling skill, and a warning about resellers

- **`klingai-dev/klingai` — OFFICIAL Kling AI skill** (hosted on ClawHub): <https://clawhub.ai/klingai-dev/skills/klingai>
  - v2.0.0, **MIT-0**, updated ~2 days before 2026-09-25. Node.js 18+, `KLING_API_KEY` (device-bind flow `--bind-url`, storage `~/.config/kling`).
  - Subcommands video/image/element/account; models `kling-3.0`, `-turbo`, `-omni`, `kling-o1`, `kling-2.6`, `kling-2.5-turbo`; images `kling-v3`, `kling-v3-omni`, `kling-image-o1`.
  - **Bilingual output (EN/中文)** with 中文 trigger words (可灵 / 文生视频 / 图生视频 / 分镜). Install: `openclaw skills install @klingai-dev/klingai`.
  - The official skill warns: *"every submit is charged; do not submit speculatively."* **This is stronger than the 0★ `runapi-ai/kling` wrapper.**
- ⚠️ **Reseller-wrapper warning:** many ClawHub "veo / sora / kling" skills are thin wrappers around resellers (PoYo, APIDot, vwu.ai, NanoPhoto, WaveSpeed, 青虎AI/LinkPix, AI Hive, SkillBoss). Each needs *that reseller's* key, and some (e.g. `wubin1836/*-alternative`) **don't call the named model at all** — they substitute Seedance. ClawHub video search returns ~24 Veo skills, ~24 Kling, ~17 Sora, ~12 Runway; most are 0★ reseller wrappers.
- **Prompt-only, not API-calling** (don't confuse these with API skills):
  | Repo | Stars / forks | License | Pushed | Note |
  |---|---|---|---|---|
  | [beshuaxian/higgsfield-seedance2-jineng](https://github.com/beshuaxian/higgsfield-seedance2-jineng) | **863** / 163 | none | 2026-04-09 | "15 Claude prompt skills", 2-second hook framework, camera encyclopedia. **Chinese + English.** No API calls |
  | [AKCodez/higgsfield-claude-skills](https://github.com/AKCodez/higgsfield-claude-skills) | **376** / 65 | none | 2026-04-13 | 19 skills for Higgsfield AI / Seedance 2.0 — driven by **Playwright browser automation**, not an API key. Claude-specific |
  | [neopen/story-shot-agent](https://github.com/neopen/story-shot-agent) | **204** / 38 | MIT | 2026-09-23 | Script → shot prompts for **Sora/Veo/Runway**; exposes MCP/REST/Function-Calling/A2A. **Chinese + English.** Generates prompts, doesn't call the APIs |
- **Notable MCP servers:** [Doriandarko/sora-mcp](https://github.com/Doriandarko/sora-mcp) (**209★**, MIT, but **stale since 2025-10-08**); [ffroliva/gflow-cli](https://github.com/ffroliva/gflow-cli) (**227★** / 62 forks, MIT, pushed 2026-09-23) — drives **Google Flow → Veo + Imagen**, ships an MCP server, but is self-described **unofficial/alpha, not affiliated with Google**, and **automates the web UI** rather than the official Veo API — a different trust class; [stabgan/openrouter-mcp-multimodal](https://github.com/stabgan/openrouter-mcp-multimodal) (92★, Apache-2.0, pushed 2026-09-25) — Veo 3.1/Sora/Seedance/Wan via OpenRouter (reseller). Smaller ones: `PixVerseAI/PixVerse-MCP` 52★, `Cripacx/mediagen` 51★, `199-mcp/mcp-kling` 41★ (**stale, 2025-06-14**), `kevinten-ai/mcp-video-gen` 11★, `strato-space/media-gen-mcp` 9★ (sora-2; stale 2026-01-30).

### 4.5 Marketplaces — how they work, and are they Claude-only?

| Marketplace | What it is | Install | Claude-only? |
|---|---|---|---|
| **`skills.sh` / `vercel-labs/skills`** | The de-facto cross-agent installer (npm `skills` 1.7.0, MIT, Node ≥ 22.20.0). Auto-detects installed agents (Claude Code, Cursor, Codex, OpenCode, Cline, "50+") and writes each skill to the right path | `npx skills add <owner>/<repo>` | **No** — this is the portability layer |
| **`clawhub.ai`** | Public registry for **OpenClaw** skills *and* plugins (OpenClaw Foundation; site on Convex, repo `openclaw/clawhub`) | `openclaw skills install @owner/slug` (native) or `clawhub install @owner/slug` (writes `./skills` + `.clawhub/lock.json`); `clawhub update --all`. Publish: `npm i -g clawhub`, `clawhub login`, `clawhub skill publish` | **No** — OpenClaw-native. It also **mirrors skills.sh** entries as "External source". Trust: open upload gated on GitHub account age, automated scans with public summaries, reports/moderation |
| **`agentskillexchange.com`** | Backed by public repo [`agentskillexchange/skills`](https://github.com/agentskillexchange/skills) — **MIT**, **3,055 published skills, 2,554 security-reviewed**, 17 categories, **25★ / 26 forks**, last activity **2026-08-05**. Ships `CATALOG.md` (815 KB), `skills.json`, `llms.txt` | `clawhub install <slug>`, or clone + `cp -R skills/skills/<slug> ~/.agent-skills/<slug>`, or `npm exec --package=skills@1.5.7 -- skills add agentskillexchange/skills --skill <slug>` (**pin the version** — they flag the `skills` npm pkg as third-party) | **No** — lists OpenClaw, Claude Code, Codex, Copilot, Gemini, Cursor, MCP, LangChain, OpenAI Agents, Hermes |
| **`anthropics/claude-plugins-community`** | **4,410★ / 312 forks, Apache-2.0, pushed 2026-08-25.** Official Claude community plugin marketplace, **read-only mirror** (submit at clau.de/plugin-directory-submission). This is where vendors like Runway actually publish | `claude plugin marketplace add anthropics/claude-plugins-community` | **Yes, by construction** |

**Site caveat:** `agentskillexchange.com` pages are **JS-rendered** — a direct fetch returns only the `<title>`. Use the repo, `skills.json`, or the SkillsMP mirror to read `SKILL.md` text.

### 4.6 Keys — none of this is free

**Essentially every item in this section needs a paid, metered API key.**
- **Direct vendor:** Runway (`RUNWAYML_API_SECRET`, **$10 min prepaid**), BFL (`x-key`, per-image/per-MP), Pika (OAuth/`MCP_AUTH_TOKEN`, Pika credits, **no BYO keys**), Luma (key from lumalabs.ai/api/keys), MiniMax (`MINIMAX_API_KEY`, **region-matched host**), Kling (`KLING_API_KEY`, device-bind; *"every submit is charged"*), OpenAI Sora (`OPENAI_API_KEY` + **verified org**; asset URLs expire ~1 h, batch 24 h).
- **Aggregators / resellers** (one key, many models): muapi, RunComfy, RunAPI (**output URLs expire in 7 days**), writingmate (OAuth or `dk_*`), deAPI, AceDataCloud, varg, fal.ai (`FAL_KEY`), SkillBoss/PoYo/APIDot/WaveSpeed/NanoPhoto/青虎.
- **Google's path** needs Google Cloud / Gemini Enterprise Agent Platform credentials (ADC or `GOOGLE_API_KEY` in Express Mode) — not a consumer key.
- Only free-ish exception found: `kevinten-ai/mcp-video-gen` advertises CogVideoX as "free unlimited" — **unverified**.

### 4.7 How to think about this category — bottom line
1. **One trustworthy, vendor-blessed video skill:** `runwayml/skills` (70★, MIT, active) — you also get Veo 3.x and Seedance 2 through Runway, installable via both `npx skills add` and the official Claude community marketplace.
2. **Veo/Kling/Sora/Seedance breadth behind one key:** `SamurAIGPT/Generative-Media-Skills` (4,325★, MIT) or RunComfy. muapi has the bigger install base + MCP mode; RunComfy has better model-selection docs.
3. **Hailuo/MiniMax:** official `MiniMax-MCP` (1,585★) — and it has Chinese docs.
4. **Kling:** prefer the **official `klingai-dev/klingai`** on ClawHub (MIT-0, bilingual) over the 0★ community wrappers.
5. **Neither marketplace separates AI-video-API from code-rendered**, and ClawHub's video shelf is dominated by low-trust reseller wrappers. Prefer vendor org repos.
6. Two integration shapes exist: **skills wrapping a REST API** (Runway, BFL, Kling) and **MCP servers** (MiniMax, Luma, Pika, muapi, fal.ai, Writingmate). For a harness-agnostic setup, **MCP is the more portable of the two**.

---

## 5. Manim, MoviePy, FFmpeg

### Manim frameworks — and the official-skill question
| Repo | Stars / forks | License | Last push | Official agent skill? |
|---|---|---|---|---|
| [3b1b/manim](https://github.com/3b1b/manim) | **94,243 / 7,738** | MIT | 2026-09-09 | **No.** Root listing verified: code/config/docs only. No `SKILL.md` / `AGENTS.md` / `CLAUDE.md`. |
| [ManimCommunity/manim](https://github.com/ManimCommunity/manim) | **41,050 / 3,129** | MIT | 2026-09-22 | **No.** Root listing verified: code/config/docs/tests only. |

**Neither project publishes an official agent skill.** All Manim skills below are third-party.

#### `adithya-s-k/manim_skill` — the best-maintained Manim skill ✅
<https://github.com/adithya-s-k/manim_skill>
- **1.1k★** (live shields.io), **MIT**.
- Two skills: `skills/manimce-best-practices` (Manim Community Edition, `from manim import *`) and `skills/manimgl-best-practices` (ManimGL/3b1b, `from manimlib import *`). Each has a `SKILL.md` + `rules/*.md` covering animations, scenes, mobjects, 3D, camera, interactivity — **plus runnable tests**.
- **Install:** `npx skills add adithya-s-k/manim_skill` or per-skill `npx skills add adithya-s-k/manim_skill/skills/manimce-best-practices`.
- **Portability: generic** — Agent Skills open standard; listed as working with Claude/Copilot/Cursor. **No Claude-only features.**
- Needs Python 3.7+, FFmpeg, LaTeX. **Docs: English only.**
- **This is the one to install for Manim**, and it is what `Yusuke710/manim-skill` itself points at for best practices.

#### `Yusuke710/manim-skill` — Plan → Code → Render → Iterate plugin
<https://github.com/Yusuke710/manim-skill> · homepage <https://www.manimate.ai/>
- **157★ / 9 forks**, **MIT**, created 2026-01-14, pushed 2026-01-26.
- Claude autonomously plans scenes, writes Manim Python, renders until all scenes succeed, then opens browser video viewers for your feedback and refines.
- **Install:**
  ```bash
  brew install cairo pkg-config ffmpeg
  uv tool install manim
  # in Claude Code:
  /plugin marketplace add Yusuke710/manim-skill
  /plugin install manim-skill/manim-skill
  ```
- **Portability: Claude-specific install**, and the workflow leans on Claude Code's **plan mode** + browser viewer. Prefer `adithya-s-k/manim_skill` on a non-Claude harness.
- **Docs: English only** (Japanese-authored, but EN README).

#### Other Manim skills
| Repo | Stars | License | Last push | Note |
|---|---|---|---|---|
| [HarleyCoops/Math-To-Manim](https://github.com/HarleyCoops/Math-To-Manim) | **~2.7k** (shields) | **unverified** | **unverified** | Three AI pipelines (Claude/Gemini/Kimi), 55+ worked examples, ships a Claude Code plugin. Install reported (secondary source) as `git clone` then `claude --plugin-dir ./Math-To-Manim/skill` |
| [Pluviobyte/rnskill](https://github.com/Pluviobyte/rnskill) | 1,610 / 187 | NOASSERTION | 2026-09-21 | Chinese skill collection containing a `manim-video` skill. **Chinese docs.** |
| [Science-Prof-Robot/recursive-math-animator](https://github.com/Science-Prof-Robot/recursive-math-animator) | 13 / 1 | MIT | 2026-08-12 | Manim + `manim-voiceover` + git scene versioning + optional Gemini TTS. Cursor **and** Claude Code. |
| [gqy20/manim-agent](https://github.com/gqy20/manim-agent) | 6 / 4 | none | 2026-04-28 | Chinese "AI 驱动的数学动画视频自动生成系统" with a `manim-production` skill. Low maturity. **Chinese docs.** |

### MoviePy
- [Zulko/moviepy](https://github.com/Zulko/moviepy) — **14,918★ / 2,112 forks**, **MIT**, pushed **2026-08-26**, branch `master`, Python **3.9+**, docs <https://zulko.github.io/moviepy/>. **Install: `pip install moviepy`** (dev: clone + `pip install -e .`).
- **Heads-up: MoviePy v2.0 introduced major breaking changes.** v1 is unmaintained; migration guide at <https://zulko.github.io/moviepy/getting_started/updating_to_v2.html>. Agents trained on older snippets emit v1 API (`subclip`, `set_duration`, `set_position`) that no longer exists — v2 uses `subclipped`, `with_duration`, `with_position`, `with_volume_scaled`. Verified from the README.
- MoviePy converts media to numpy arrays, so it is **5–50× slower than ffmpeg** for plain cuts/transcodes. Prefer ffmpeg for those.
- **No official MoviePy skill exists.** Two verified third-party `SKILL.md` packagings:

#### `digitalsamba/claude-code-video-toolkit` → `.claude/skills/moviepy/SKILL.md`
<https://github.com/digitalsamba/claude-code-video-toolkit>
- **2,124★ / 366 forks**, **MIT**, pushed 2026-09-21. **SKILL.md verified real.**
- MoviePy **2.x** for deterministic text/PIL overlays on LTX-2/SadTalker output, lower thirds, audio-anchored timelines, `build.py` projects, with runnable `examples/quick-spot/build.py` and `examples/data-viz-chart/build.py`. Stack: moviepy 2.x + Pillow + matplotlib.
- Also ships `ffmpeg`, `remotion`, `elevenlabs`, `ltx2`, `playwright-recording`, `frontend-design`, `runpod` skills — **this repo is a strong one-stop shop.**
- **Install:** `git clone … && uv sync && claude` (needs Node 18+, Claude Code, uv; FFmpeg optional).
- **Portability: Claude Code-native** (`/setup`, `/video` slash commands, `.claude/skills/`) — **but** it ships experimental migration scripts to Codex (`uv run scripts/migrate_to_codex.py`) and Kiro CLI, so it is not hard-locked. **Docs: English.**

#### `damionrashford/media-os` → `skills/media-moviepy/SKILL.md`
<https://github.com/damionrashford/media-os>
- **SKILL.md verified real:** a thorough MoviePy 2.x guide (concat/subclip/resize/crop/rotate, `TextClip`+ImageMagick, per-frame numpy `image_transform`, crossfades, `write_videofile` tuning, troubleshooting) plus a bundled `scripts/moviepy_cli.py`. Uses `${CLAUDE_SKILL_DIR}` references.
- Stars / license / last push: **unverified** (API rate limit).

### FFmpeg, subtitles, TTS

#### `kajisho5/ffmpeg-skill` — the strongest pure-FFmpeg skill ✅
<https://github.com/kajisho5/ffmpeg-skill>
- **1,401★ / 106 forks**, **MIT**, pushed **2026-09-25** (shields: commit "today" — very actively maintained).
- `SKILL.md` + `scripts/` (**42 typed tools**) + `references/` + `mcp/server.py`. Probe → edit losslessly → verify workflow: cut, join, silence removal, fit-duration + crop, captions **and karaoke**, overlays, motion graphics, HDR→SDR, loudness, multicam, delivery checks, batch.
- **Stack:** FFmpeg CLI driven from **Python 3.9 stdlib only** — no shell, no raw filter strings.
- **Install (multi-target, genuinely generic):**
  ```bash
  npx ffmpeg-skill          # -> ~/.claude/skills/ffmpeg-skill
  npx ffmpeg-skill --cursor # -> ~/.cursor/skills/
  npx ffmpeg-skill --codex  # -> ~/.agents/skills/
  # also --all, --project, --dir, --uninstall
  # or: claude plugin install kajisho5/ffmpeg-skill
  ```
- **Portability: excellent** — explicit Claude Code / Cursor / Codex targets, "anything reading SKILL.md".
- Requires FFmpeg 5.0+ built with `subtitles`/libass, `drawtext`, `zscale`, `loudnorm`, `xfade`; Python 3.9+; Node 16+ only for the installer. Optional Whisper for `caption.py --transcribe`. **Docs: English** (`README.zh-CN.md` → 404).

#### `kajisho5/subtitle-skill`
<https://github.com/kajisho5/subtitle-skill>
- **2★**, **MIT**. Typed `SubtitleDocument` → SRT/WebVTT `generate`; `render` burns/muxes by delegating burn-in to ffmpeg-skill's `caption.py`. Deliberately **no AI decision-making**; deterministic cache; `contract --json` / `doctor --json`.
- **Install:** `pip install -e .` (**not on PyPI**) then `subtitle-skill install` (`--cursor/--codex/--all/--project/--dir/--uninstall`). Generic; needs ffmpeg-skill for `render`. English.
- Same author also asserts (stars unverified): `transcription-skill`, `media-analysis-skill`, `video-editing-skill`, `audio-production-skill`, `thumbnail-skill`, `color-grading-skill`, `motion-graphics-skill`, `qc-skill`, `video-production-agent`, `AI-video-production-OS`.

#### `op7418/Youtube-clipper-skill`
<https://github.com/op7418/Youtube-clipper-skill>
- **2.2k★** (shields). yt-dlp download → AI semantic chapters → FFmpeg frame-accurate clips → **bilingual zh/en subtitle translation** → burn subtitles with styling.
- **Install:** `npx skills add https://github.com/op7418/Youtube-clipper-skill`. Needs yt-dlp, ffmpeg w/ libass, pysrt. Generic.

#### `aahl/skills` — TTS / ASR
<https://github.com/aahl/skills>
- **160★** (shields). Skills: `edge-tts` (via `uvx edge-tts`), `zai-tts` (GLM-TTS), `qwen-asr`.
- **Install:** `npx skills add aahl/skills`. Generic. README is **English with some Chinese entries**.

#### Other FFmpeg / editing / Chinese-language skills
| Repo | Stars / forks | License | Last push | Note |
|---|---|---|---|---|
| [znyupup/ai-video-editing-skill](https://github.com/znyupup/ai-video-editing-skill) | 135 / 22 | MIT | 2026-04-27 | **Chinese-only** SKILL.md: raw travel footage → finished vlog. ffmpeg + Whisper + vision API (智谱 GLM-4.6V-Flash / GPT-4o / Qwen-VL), 24 war-stories, storyboard generators. Explicitly **"不需要 moviepy"**. Hosts: Claude Code / Hermes / OpenClaw / GPT. |
| [cyuanxv/ai-mandrama-skills](https://github.com/cyuanxv/ai-mandrama-skills) | 24 / 7 | MIT | 2026-05-17 | **Chinese** Claude Code pack: Dreamina + **edge-tts** + ffmpeg for AI 漫剧/短剧. |
| [ychoi-kr/claude-ffmpeg-skill](https://github.com/ychoi-kr/claude-ffmpeg-skill) | 52 / 5 | MIT | 2025-10-19 | older ffmpeg skill |
| [fabriqaai/ffmpeg-analyse-video-skill](https://github.com/fabriqaai/ffmpeg-analyse-video-skill) | 31 / 4 | none | 2026-02-15 | frame extraction + vision summaries |
| [bryanwhl/ffmpeg-video-editor](https://github.com/bryanwhl/ffmpeg-video-editor) | 7 / 1 | MIT | 2026-03-03 | the index's only ffmpeg entry |
| [martinholovsky/claude-skills-generator](https://github.com/martinholovsky/claude-skills-generator) | unverified | unverified | unverified | `skills/text-to-speech/SKILL.md` **verified real**: Kokoro TTS ≥0.3.0, streaming, caching. Frontmatter pins `model: sonnet` (Claude-flavoured). |
| [wilwaldon/Claude-Code-Video-Toolkit](https://github.com/wilwaldon/Claude-Code-Video-Toolkit) | 83 / 12 | none | 2026-02-26 | **read-only index, not installable** |
| [FireRedTeam/FireRed-OpenStoryline](https://github.com/FireRedTeam/FireRed-OpenStoryline) | 3,438 | Apache-2.0 | 2026-07-31 | agentic video-editing **app** with "Style Skills" — not a skill |

**Could not verify:** `steipete/video-frames` — README 404s on both `main` and `master`. Treat as unverified.

### Skill-directory sweep — the aggregators are video-poor
| Aggregator | Stars | Video content? |
|---|---|---|
| [anthropics/skills](https://github.com/anthropics/skills) | **178,116 / 21,100**, no license | **Zero.** Exactly 18 skills (academy-guide, algorithmic-art, brand-guidelines, canvas-design, claude-api, discernment-nudge, doc-coauthoring, docx, frontend-design, internal-comms, mcp-builder, pdf, pptx, skill-creator, slack-gif-creator, theme-factory, web-artifacts-builder, webapp-testing, xlsx). Video-adjacent only via `frontend-design`. |
| [VoltAgent/awesome-agent-skills](https://github.com/VoltAgent/awesome-agent-skills) | **34,844**, MIT | No ffmpeg/manim/moviepy/subtitle/whisper entries. Video-adjacent: `remotion-dev/remotion`, `google-labs-code/remotion`, `veniceai/venice-video`, `fal-ai-community/fal-generate|fal-video-edit|fal-upscale|fal-lip-sync|fal-audio`, **`openai/sora`**, `openai/speech`, `openai/transcribe` |
| [wshobson/agents](https://github.com/wshobson/agents) | 94 plugins / 202 agents / 183 skills / MIT | **No video/ffmpeg/manim skill found** (docs not exhaustively enumerated) |
| [Prat011/awesome-llm-skills](https://github.com/Prat011/awesome-llm-skills) | Apache-2.0 | Video-adjacent only: "Video Downloader", `youtube-transcript`, "Slack GIF Creator", "Canvas Design", `imagen`, "Taisly Agent Kit" |
| [vivy-yi/awesome-skills](https://github.com/vivy-yi/awesome-skills) | MIT, 341 repos | Only Manim entry is `adithya-s-k/manim_skill`; no MoviePy/FFmpeg entries |

**Important negative result:** the curated index (§1) contains **no Manim entry and no MoviePy entry**, and its only FFmpeg entry is the 7★ `bryanwhl` repo. The genuinely good Manim/FFmpeg/TTS skills above had to be found independently — the index is not sufficient.

---

## 6. Minimal manual fallback — no skill at all

### Remotion (4 commands, recommended fallback)
Requires **Node.js ≥ 16** (Bun ≥ 1.0.3 also works). macOS 15+; Linux needs glibc ≥ 2.35; Alpine/nixOS unsupported.

```bash
npx create-video@latest --yes --blank my-video   # or --hello-world
cd my-video
npm i
npm run dev                                       # Remotion Studio preview; docs also show: npx remotion studio
npx remotion render HelloWorld out/video.mp4      # output arg optional, defaults to out/
npx remotion render                               # no args -> interactive composition picker
```
- `npx remotion render <entry-point|serve-url>? <composition-id> <output-location>` — default codec H.264. Flags: `--codec h264|h265|av1|vp8|vp9|mp3|aac|wav|prores|png`, `--frames`, `--scale`, `--concurrency`, `--crf`, `--props` (use a **file** on Windows — inline JSON loses quotes).
- Composition ID `HelloWorld` is confirmed for the `hello-world` template. For `--blank` the preview URL suggests **`MyComp`** — high confidence, not a literal doc statement, so `npx remotion render` with no ID is the safe path.
- **Templates that actually exist** (19 free): `blank`, `hello-world`, `next`, `vercel`, `recorder`, `prompt-to-motion-graphics`, `render-server`, `electron`, `react-router`, `three`, `still`, `audiogram`, `music-visualization`, `prompt-to-video`, `skia`, `overlay`, `code-hike`, `stargazer`, `tiktok`. Paid: `editor-starter`.
  - ⚠️ `javascript`, `text-to-speech`, and `captions` are **not** template IDs. Closest: `hello-world` (basic TS), `tiktok` (word-by-word captions, auto-installs Whisper.cpp), `audiogram`.
  - `--yes` is documented as "useful for scripting and **AI agents like Claude Code**", but it does **not** install agent skills and **fails inside an existing Git repo**.
- The documented shape is **two files**, not one:
  ```tsx
  // src/MyComposition.tsx
  import {AbsoluteFill, useCurrentFrame} from 'remotion';
  export const MyComposition = () => {
    const frame = useCurrentFrame();
    return <AbsoluteFill style={{justifyContent:'center', alignItems:'center', fontSize:100, backgroundColor:'white'}}>
      The current frame is {frame}.
    </AbsoluteFill>;
  };
  // src/Root.tsx
  import {Composition} from 'remotion';
  export const RemotionRoot = () => (
    <Composition id="MyComposition" durationInFrames={150} fps={30} width={1920} height={1080} component={MyComposition} />
  );
  ```
- **Absolutely minimal agent prompt (from the official docs):** *"Ensure Node.js is installed. Install Remotion Skills: `npx -y skills@latest add remotion-dev/skills -g -y`. Then use them to create a video."*

### Manim
```bash
# system deps (macOS)
brew install cairo pkg-config ffmpeg
uv tool install manim            # or: pip install manim

# write a Scene, then smoke-test at low quality:
manim -ql scene.py MyScene       # -p also previews; q = l|m|h|k
manim -pqh scene.py Demo         # -> media/videos/scene/1080p60/Demo.mp4
# ManimGL (3b1b) instead:
pip install manimgl && manimgl scene.py Demo --write_file
```
Minimal working scene:
```python
from manim import *
class Demo(Scene):
    def construct(self):
        self.play(Create(Circle()))
        self.wait(1)
```

### MoviePy
```bash
pip install moviepy
```
```python
from moviepy import VideoFileClip, TextClip, CompositeVideoClip
c = VideoFileClip("in.mp4").subclipped(10, 20).resized(width=1280)   # NOTE: v2 API
t = TextClip(text="Hello", font_size=70, color="white", duration=c.duration).with_position("center")
CompositeVideoClip([c, t]).write_videofile("out.mp4", codec="libx264", audio_codec="aac", fps=30)
```

### ffmpeg (usually the better choice than MoviePy — 5–50× faster)
```bash
ffmpeg -ss 10 -to 20 -i in.mp4 -c copy cut.mp4                                    # lossless trim
ffmpeg -i in.mp4 -vf "scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2,fps=30" \
       -c:v libx264 -crf 18 -preset slow -movflags +faststart out.mp4              # normalize to 1080p30
ffmpeg -i in.mp4 -vf "subtitles=captions.srt" -c:a copy burned.mp4                 # burn subs (needs libass)
ffmpeg -f concat -safe 0 -i list.txt -c copy joined.mp4                            # concat (list.txt: file 'a.mp4')
# text overlay — drawtext needs an explicit fontfile path on Windows:
ffmpeg -i in.mp4 -vf "drawtext=fontfile=/Windows/Fonts/arial.ttf:text='Hello':fontsize=48:x=(w-tw)/2:y=h-th-40" out.mp4
uvx edge-tts --voice zh-CN-XiaoxiaoNeural --text "你好" --write-media vo.mp3        # free TTS voiceover
```
**Note:** the earlier `-framerate 30 -i frame_%04d.png` PNG-sequence form I first wrote here was not doc-verified; the commands above were checked against MoviePy's README and standard FFmpeg usage. `drawtext`/`subtitles` require FFmpeg builds with those filters (libass).

---

## 7. Portability verdict for DeepSeek Harness

**Portable as-is (no Claude-only features):**
- `remotion-dev/skills` — explicitly multi-harness, installs to `.agents/skills`.
- `Vincentwei1021/video-shotcraft` — targets Claude Code *or* Codex, `npx skills add` supported.
- `Changroro/code-video` — `npx skills add` is a first-class path.
- `SkillMedev/remotion-video-production` — plain `SKILL.md`, `npx skills add`.
- `haidrrrry/claude-remotion-skill` — plain `SKILL.md` folder, copy into any skills directory; no Claude-only tools.
- `affaan-m/ECC` video skills — ECC ships adapters for many harnesses, but parity is partial.
- `runwayml/skills`, `black-forest-labs/skills` — `npx skills add` alongside the Claude path.
- MCP-based options (MiniMax, fal.ai) — MCP is harness-agnostic; strongest bet if you already run MCP.

**Portable in principle, but written for Claude (check before relying):**
- `Vincentwei1021/anything2explainer` — plain Markdown + shell, but its 9-stage flow assumes **parallel sub-agents** and QC agents for throughput.
- `browser-use/video-use` — plain Markdown + shell, but spawns **parallel sub-agents** per animation.
- `Yusuke710/manim-skill` — leans on Claude Code plan mode + browser viewer; install path is a Claude plugin marketplace.

**Claude-locked (skip for DeepSeek Harness):**
- `cajias/agentic-video-skills` — `/plugin marketplace` only, and it's prompt/storyboard skills anyway.
- Anything gated behind `claude plugin install <slug>@claude-community` with no `npx skills add` fallback.

**Practical recommendation order for DeepSeek Harness:**
1. `npx skills add remotion-dev/skills` — official, actively maintained, generic. Pair with the §6 fallback commands. **(Check Node ≥ 22.20.0 for the `skills` CLI itself.)**
2. Add `Vincentwei1021/video-shotcraft` if you want an opinionated production system (best docs, Apache-2.0, trilingual).
3. Add `haidrrrry/claude-remotion-skill` for motion-design *craft* rules + a frame-inspection QA loop — plain folder copy, no marketplace needed.
4. For post-production: **`npx ffmpeg-skill`** (1,401★, 42 typed tools, pushed the day of this research, explicit `--cursor` / `--codex` targets) — this is the single best FFmpeg skill found.
5. For math/technical explainers: `npx skills add adithya-s-k/manim_skill` (1,101★, generic, tested) — **not** the Claude-only `Yusuke710/manim-skill` unless you want the plan→render→iterate plugin.
6. For a bundle of moviepy + ffmpeg + remotion + elevenlabs skills in one repo: `digitalsamba/claude-code-video-toolkit` (2,124★), noting it is Claude-Code-native with an experimental Codex migration script.
7. Only reach for §4 API-model skills if you actually want hosted generation and have keys + credits.

---

## 8. Method & known limitations of this report

- All figures are from the **GitHub REST API** (`api.github.com/repos/...`) and repo READMEs fetched on **2026-09-25**. The unauthenticated API rate limit was exhausted mid-research, so a few metadata items remain **unverified** — notably Zulko/moviepy's star/fork/push figures, `adithya-s-k/manim_skill`, and `Vincentwei1021/video-talkcraft`'s exact `npx skills add` support. Their *licenses and install commands* were verified from READMEs where noted.
- Star counts are a **popularity** signal only. Several high-star repos here (ECC at 267k, video-use at 27k) are broad agent harnesses where video is one small feature.
- The environment's "current date" is late **September 2026**; several of these repos are only weeks old, so the ecosystem is moving fast. Re-check before installing.
- `raw.githubusercontent.com` and `api.github.com` were intermittently flaky during this session; a few fetch attempts failed transiently and were retried.
