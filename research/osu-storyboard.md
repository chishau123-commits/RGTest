# osu! Storyboard System — Primary-Source Technical Research

**Purpose:** background/authoring investigation for a rhythm game (Unity project at `D:\MyDataInD\Unity\RhythmGame`).
**Date of research:** this session.
**Method:** all claims below are traced to (a) the official osu! wiki (`osu.ppy.sh/wiki/en/...`), which is served from the official `ppy/osu-wiki` repository, or (b) the official open-source client `ppy/osu` (lazer) source, or (c) the official `.osu`/`.osb` file-format documentation. Exact quotes are given verbatim (wiki British spellings preserved). Anything I could not confirm from a primary source is explicitly marked **未验证**.

## 0. Access / reproduction notes (important — corrects a common misconception)

| Item | Finding |
|---|---|
| `osu.ppy.sh` wiki pages | **Fully readable anonymously.** There is *no* login wall on wiki content. |
| Why fetches look "walled" | The rendered wiki page is **122–174 KB**, of which the first **~113 KB is nav/locale boilerplate**. Tooling that truncates at 50 000 chars truncates *inside the nav*, so the article body (which begins at the marker `osu-md osu-md--wiki`) is never reached. The string `Sign In To Proceed` belongs to a hidden site-wide modal, not to the article. |
| Working retrieval method | `Invoke-WebRequest` → save HTML → cut substring from `osu-md osu-md--wiki` to the next `<footer`/`user-verification-popup` → HTML→Markdown. The wiki emits clean semantic HTML (`h1–h4`, `p`, `ul/li`, `table/thead/tbody/tr/td`, `pre>code`). Converter used: `research/_raw/convert.ps1`; raw HTML + converted Markdown cached in `research/_raw/`. |
| GitHub direct | **Unreachable** from this environment: `raw.githubusercontent.com`, `github.com` (flaky), `api.github.com`, `cdn.jsdelivr.net`, `cdn.statically.io`, `r.jina.ai`, `bgithub.xyz`, `gitclone.com`, `kkgithub` (DNS). |
| GitHub via proxy | **Works:** `https://ghproxy.net/https://raw.githubusercontent.com/ppy/osu/master/<path>` returns HTTP 200. `https://gh-proxy.com/...` is a working fallback. `github.com/ppy/osu/pull/38335` (HTML) also loaded successfully once. |
| Pages that do **not** exist | `Storyboard/Scripting/Events`, `Storyboard/Scripting/Trigger`, `Beatmap/Video` → connection reset / HTTP 404. `/Beatmap/Video/en.md` is **absent from the `ppy/osu-wiki` repo** (verified by 404 on the raw repo path): there is no "Beatmap/Video" page. Trigger documentation lives on **Compound Commands**; video requirements live on **Ranking criteria** and **Guides/Compressing files**. |

Canonical GitHub URLs are cited below; the bytes were retrieved through the `ghproxy.net` raw proxy because direct GitHub is blocked here.

---

## 1. What a storyboard is, technically

### 1.1 Definition and storage

| Claim | Exact quote | Source |
|---|---|---|
| A storyboard is an animated background | "A **storyboard** (SB) is a custom-made animated background that accompanies a beatmap, often for decorative and sometimes for gameplay purposes." | [Storyboard](https://osu.ppy.sh/wiki/en/Storyboard) |
| Storage form | "Storyboards are stored in beatmap folders as either standalone `.osb` files or extensions to the `[Events]` section of a `.osu` file. Because of this, it's possible to create different storyboards for difficulties within a beatmap." | [Storyboard](https://osu.ppy.sh/wiki/en/Storyboard) |
| What scripting is | "**Storyboard scripting** is the process of editing osu! storyboards via their `.osb` and `.osu` files. These files define images and effects that the game client renders into background animations during gameplay." | [Storyboard scripting](https://osu.ppy.sh/wiki/en/Storyboard/Scripting) |
| `.osb` vs `.osu` scope | "This guide describes the lines of scripting code that are placed into the .osb or .osu file, under `[Events]`. Commands in the .osb file for the beatmap will appear in all difficulties, while those that appear in the .osu file will only appear in that given difficulty." | [General rules](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/General_Rules) |
| `.osb` MIME type | "`.osb` … `x-osu-storyboard` … osu! storyboard" | [osu! file formats](https://osu.ppy.sh/wiki/en/Client/File_formats) |
| `.osb` page is a stub | The whole page body is: "**.osb** is a file format containing information about an osu! storyboard." (no version line, sections or grammar documented there) | [.osb (file format)](https://osu.ppy.sh/wiki/en/Client/File_formats/osb_%28file_format%29) |

### 1.2 File structure

**Version line.** The `.osu` format documents the version line explicitly:

> "The first line of the file specifies the file format version. For example, `osu file format v14` is the latest version. (v128 for osu!(lazer).)"
> — [.osu (file format)](https://osu.ppy.sh/wiki/en/Client/File_formats/osu_%28file_format%29)

The same version line exists in `.osb`, and this is confirmed by the client's decoder registration — `.osb` files are detected by the identical header prefix:

```csharp
public static void Register()
{
    // note that this isn't completely correct
    AddDecoder<Storyboard>(@"osu file format v", m => new LegacyStoryboardDecoder(Parsing.ParseInt(m.Split('v').Last())));
    AddDecoder<Storyboard>(@"[Events]", _ => new LegacyStoryboardDecoder());
    SetFallbackDecoder<Storyboard>(() => new LegacyStoryboardDecoder());
}
```
— [`osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L33–39

**Sections.** `.osu` sections, from the official format page:

> "`[General]` … `[Editor]` … `[Metadata]` … `[Difficulty]` … **`[Events]`** — *Beatmap and storyboard graphic events* — **Comma-separated lists** … `[TimingPoints]` … `[Colours]` … `[HitObjects]`"
> — [.osu (file format)](https://osu.ppy.sh/wiki/en/Client/File_formats/osu_%28file_format%29)

The `.osb` decoder handles exactly three sections — `General`, `Events`, `Variables`:

```csharp
case Section.General:   handleGeneral(storyboard, line); return;
case Section.Events:    handleEvents(line, isPrimaryStream); return;
case Section.Variables: handleVariables(line); return;
```
— [`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L60–73

`[General]` recognises only `UseSkinSprites` and `WidescreenStoryboard` in `.osb` ([same file](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L78–92).

**Event type numbers** (the authoritative enumeration of `[Events]` line types):

```csharp
internal enum LegacyEventType
{
    Background = 0,
    Video = 1,
    Break = 2,
    Colour = 3,
    Sprite = 4,
    Sample = 5,
    Animation = 6
}
```
— [`osu.Game/Beatmaps/Legacy/LegacyEventType.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Legacy/LegacyEventType.cs)

> ⚠️ Note: `Background`, `Video`, `Break`, `Sprite`, `Sample`, `Animation` are **event types**, not all "storyboard object" types. Only `Sprite` and `Animation` open an object that subsequent commands attach to; `Sample` is a standalone one-liner; `Background`/`Video` are beatmap-level resources declared in the same section.

### 1.3 Exact `Sprite` and `Animation` syntax

> Basic image: `Sprite,(layer),(origin),"(filepath)",(x),(y)`
> Moving image: `Animation,(layer),(origin),"(filepath)",(x),(y),(frameCount),(frameDelay),(looptype)`
> — [Storyboard objects](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Objects)

Field semantics, quoted:

| Field | Wiki wording |
|---|---|
| `(layer)` | "**(layer)** is the **layer the object appears on.**" |
| `(origin)` | "**(origin)** is where on the **image should osu! consider that image's origin (coordinate) to be.** This affects the (x) and (y) values, as well as several other command-specific behaviours. For example, choosing (origin) = TopLeft will let the (x),(y) values determine, where the top left corner of the image itself should be on the screen." |
| `(filepath)` | "If you have a subfolder inside your Song Folder, you need to include that, as well… Start listing directories only from the Song Folder, where the .osu or .osb file is (i.e., a relative filepath). It should not have something like `C:` anywhere in it." / "Animations are referred to without their number. So if you have `sample0.png` and `sample1.png` as two frames to make a single animation, you want to refer to it as `sample.png`." |
| quotes | "The `""`s are technically optional, but they're required if your filename or subfolder name has spaces." |
| `(x)`,`(y)` | "**(x)** and **(y)** are the **x-/y-coordinates of where the object should be, by default respectively.**" |
| `(frameCount)` | "**(frameCount)** indicates **how many frames the animation has.**" |
| `(frameDelay)` | "**(frameDelay)** indicates **how many milliseconds should be in between each frame.**" |
| `(looptype)` | "**LoopForever** (default if you leave this value off…) / **LoopOnce** (the animation will stop on the last frame and continue to display that last frame…)" |

Important non-obvious rule, quoted:

> "Take note that *there is no indication of when the object should appear*. That is entirely up to the commands themselves. The order of the object declarations in the .osu or .osb file only affects what overlaps what; it has no bearing on when the object appears…"
> — [Storyboard objects](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Objects)

**Layer values** ([Storyboard objects](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Objects)):

| Value | Layer |
|---|---|
| 0 | Background |
| 1 | Fail |
| 2 | Pass |
| 3 | Foreground |

The client additionally accepts two more layer names not listed in that table (`Overlay`, `Video`):

```csharp
internal enum LegacyStoryLayer
{
    Background = 0,
    Fail = 1,
    Pass = 2,
    Foreground = 3,
    Overlay = 4,
    Video = 5
}
```
— [`osu.Game/Beatmaps/Legacy/LegacyStoryLayer.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Legacy/LegacyStoryLayer.cs)

**Origin values** ([Storyboard objects](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Objects)) — the client enum confirms the exact spelling and order:

| Value | Origin | `LegacyOrigins` ordinal |
|---|---|---|
| 0 | TopLeft | 0 |
| 1 | Centre | 1 |
| 2 | CentreLeft | 2 |
| 3 | TopRight | 3 |
| 4 | BottomCentre | 4 |
| 5 | TopCentre | 5 |
| 6 | Custom ("same effect as TopLeft, but should not be used") | 6 |
| 7 | CentreRight | 7 |
| 8 | BottomLeft | 8 |
| 9 | BottomRight | 9 |

```csharp
internal enum LegacyOrigins
{ TopLeft, Centre, CentreLeft, TopRight, BottomCentre, TopCentre, Custom, CentreRight, BottomLeft, BottomRight }
```
— [`osu.Game/Beatmaps/Legacy/LegacyOrigins.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Legacy/LegacyOrigins.cs)

> Note the spelling is **`Centre`**, not `Center` (British spelling) — this is parser-visible.

Parser mapping of origin strings (note `Custom` and any unknown value fall back to `TopLeft`):

```csharp
private Anchor parseOrigin(string value)
{
    var origin = Enum.Parse<LegacyOrigins>(value);
    switch (origin)
    {
        case LegacyOrigins.TopLeft:      return Anchor.TopLeft;
        case LegacyOrigins.TopCentre:    return Anchor.TopCentre;
        ...
        default:                         return Anchor.TopLeft;
    }
}
```
— [`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L346–382

**Screen / coordinate system** ([General rules](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/General_Rules)):

> "The editor screen is 640 x 480 pixels and the general play area is 510 x 385 pixels."
> "Coordinates are specified with positive values for `X` going to the **right**, positive values for `Y` going **down**, and the origin (0,0) being placed at the upper-left corner of the screen."

| | x | y |
|---|---|---|
| Screen | 0–640 | 0–480 |
| Play area | 60–570 | 55–440 |

**Layer semantics** ([General rules](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/General_Rules)):

> "All storyboard sprites are placed below the hit objects except Overlay, which is still placed below the skin. So, even the "highest" (Overlay) layer in the storyboard will always be above hit objects, but behind the HP bar, the cursor, etc."
> "These are the five storyboard layers, in increasing order of priority: Background / Fail … / Pass … / Foreground / Overlay (displayed above hit objects, use with caution)"
> "Note that the "Fail" and "Pass" layers are never on-screen simultaneously, unlike in the design tab."

Overlap rules, quoted: different layers draw in the layer order; same layer draws in declaration order; and "Commands from the .osb file take precedence over those from the .osu file within the layers, as if the commands from the .osb were appended to the end of the .osu commands."

**Comments** ([General rules](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/General_Rules)):

> "Single-line C-style comments can be added… `// This is a comment.`"
> "Unlike in C/C++/C#/Java, comments can not be put on a line after a valid command. Block comments are unavailable as well."

**Indentation defines nesting** — the parser counts leading spaces/underscores as "depth"; depth 0 = object declaration, depth 1 = command on the object, depth ≥2 = command inside a loop/trigger:

```csharp
int depth = 0;
foreach (char c in line)
{
    if (c == ' ' || c == '_') depth++;
    else break;
}
line = line.Substring(depth);
string[] split = line.Split(',');
if (depth == 0) { /* object declaration */ }
else { if (depth < 2) currentCommandsGroup = storyboardSprite?.Commands; /* command */ }
```
— [`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L98–200

**`[Variables]`** (`.osb` only) — [Variables](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Variables):

> "**Variables** are custom aliases for other values, typically long or common strings, which can be reused elsewhere in `.osb` files. These cannot be changed dynamically during gameplay, meaning they are constant values. They are *not supported* in `.osu` files."
> ```
> [Variables]
> $colour_green=0,255,0
> $sample_path="Sample.png"
> ```

Resolved by single-pass plain-text substitution at load time ([`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L390–414).

**`Sample`** (storyboard audio) — a standalone object declaration, *not* a command — [Audio samples](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Audio):

> "`Sample,<time>,<layer_num>,"<filepath>",<volume>`"
> "**Audio files** (WAV, MP3, and OGG) can be played at specified points in time. They are like object declarations, not commands, so they aren't used in loops or triggers."
> "`<layer_num>` is a *numerical value* corresponding to the layer" (0 Background / 1 Fail / 2 Pass / 3 Foreground)

---

## 2. The command list

### 2.1 Command-line grammar

> "`_(event),(easing),(starttime),(endtime),(params...)`"
> — "_ can be a space instead of an underscore." — "(starttime) and (endtime) are the starting and ending times of the command, respectively in milliseconds (ms)."
> — [Storyboard scripting commands](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Commands)

### 2.2 Full command table

| Letter | Name | Exact parameter signature (wiki) | What it affects |
|---|---|---|---|
| `F` | Fade | `_F,(easing),(starttime),(endtime),(start_opacity),(end_opacity)` | "The opacity of the object (how transparent it is). 0 to 1, with decimals accepted. 0 is invisible, 1 is fully visible." Default `1` |
| `M` | Move | `M,(easing),(starttime),(endtime),(start_x),(start_y),(end_x),(end_y)` | "The location of the object in the play area." Default = the object declaration's location |
| `MX` | Move X | `_MX,(easing),(starttime),(endtime),(start_x),(end_x)` | "Like Move, but only changes X-coordinate. Y-coordinate stays the same." |
| `MY` | Move Y | `_MY,(easing),(starttime),(endtime),(start_y),(end_y)` | "Like Move, but only changes Y-coordinate. X-coordinate stays the same." |
| `S` | Scale | `_S,<easing>,<starttime>,<endtime>,<start_scale>,<end_scale>` | "The size of the object relative to its original size (as it appears in its file)." Default `1` |
| `V` | Vector Scale | `_V,(easing),(starttime),(endtime),(start_scale_x),(start_scale_y),(end_scale_x),(end_scale_y)` | "This is the same as S, except X and Y scale separately." |
| `R` | Rotate | `_R,<easing>,<starttime>,<endtime>,<start_rotate>,<end_rotate>` | "The amount an object is rotated from its original image, **in radians, clockwise**." Default `0` |
| `C` | Colour | `_C,(easing),(starttime),(endtime),(start_r),(start_g),(start_b),(end_r),(end_g),(end_b)` | "The virtual light source colour on the object. The colours of the pixels on the object are determined subtractively." Triplet 0–255; default `(255,255,255)` |
| `P` | Parameter | `_P,(easing),(starttime),(endtime),(parameter)` | see 2.4 |
| `L` | Loop | `_L,(starttime),(loopcount)` | compound — see 2.5 |
| `T` | Trigger | `_T,(triggerType),(starttime),(endtime)` | compound — see §3 |

All quotes in the table above: [Storyboard scripting commands](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Commands).

The legacy Cheat Sheet (derived from Echo's *Official Specifications* forum post) lists the same letters with terser names: `F` fade, `M` move, `S` scale, `V` vector scale (width and height separately), `R` rotate, `C` colour, `L` loop, `T` "Event-triggered loop", `P` Parameters — [Cheat sheet](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Cheat_Sheet).

**Letter → decoder function** (confirms letters are 1:1 with internal properties, and two non-obvious conversions — radians→degrees for `R`, 0–255→0–1 for `C`):

| Letter | Decoder behaviour | Source |
|---|---|---|
| `F` | `AddAlpha(easing, startTime, endTime, startValue, endValue)` | [`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L236–242 |
| `S` | `AddScale(...)` | L244–250 |
| `V` | `AddVectorScale(... Vector2(startX,startY), Vector2(endX,endY))` | L252–260 |
| `R` | `AddRotation(..., float.RadiansToDegrees(startValue), float.RadiansToDegrees(endValue))` | L262–268 |
| `M` | `AddX(...)` **and** `AddY(...)` (one line → two commands) | L270–279 |
| `MX` | `AddX(...)` | L281–287 |
| `MY` | `AddY(...)` | L289–295 |
| `C` | `AddColour(..., new Color4(startRed / 255f, startGreen / 255f, startBlue / 255f, 1), ...)` | L297–309 |
| `P` | `AddBlendingParameters` / `AddFlipH` / `AddFlipV` | L311–332 |
| `L` | `AddLoopingGroup(startTime, Math.Max(0, repeatCount - 1))` | L217–223 |
| `T` | `AddTriggerGroup(triggerName, startTime, endTime, groupNumber)` | L206–215 |

Unknown letters are hard errors: `default: throw new InvalidDataException($@"Unknown command type: {commandType}");` (L334–335). Likewise unknown event types: `throw new InvalidDataException($@"Unknown event type: {split[0]}");` (L116–117).

Command storage is per-property, each list sorted by start time ([`StoryboardCommandGroup.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Commands/StoryboardCommandGroup.cs) L16–54): `X`, `Y`, `Scale`, `VectorScale`, `Rotation`, `Colour`, `Alpha`, `BlendingParameters`, `FlipH`, `FlipV`.

### 2.3 Easing table (transcribed in full)

> "The valid values for easing are:" — [Storyboard scripting commands](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Commands)

| Value | Description | | Value | Description |
|---|---|---|---|---|
| 0 | Linear: no easing | | 18 | Expo In |
| 1 | Easing Out: the changes happen fast at first, but then slow down toward the end | | 19 | Expo Out |
| 2 | Easing In: the changes happen slowly at first, but then speed up toward the end | | 20 | Expo In/Out |
| 3 | Quad In | | 21 | Circ In |
| 4 | Quad Out | | 22 | Circ Out |
| 5 | Quad In/Out | | 23 | Circ In/Out |
| 6 | Cubic In | | 24 | Elastic In |
| 7 | Cubic Out | | 25 | Elastic Out |
| 8 | Cubic In/Out | | 26 | ElasticHalf Out |
| 9 | Quart In | | 27 | ElasticQuarter Out |
| 10 | Quart Out | | 28 | Elastic In/Out |
| 11 | Quart In/Out | | 29 | Back In |
| 12 | Quint In | | 30 | Back Out |
| 13 | Quint Out | | 31 | Back In/Out |
| 14 | Quint In/Out | | 32 | Bounce In |
| 15 | Sine In | | 33 | Bounce Out |
| 16 | Sine Out | | 34 | Bounce In/Out |
| 17 | Sine In/Out | | | |

> ⚠️ Correction to the question's assumption: the wiki table does **not** label `3` as "Easing InQuad". The wiki's own wording is exactly: `0` "Linear: no easing", `1` "Easing Out: …", `2` "Easing In: …", and from `3` onward it uses short names `Quad In`, `Quad Out`, `Quad In/Out`, … The article also points at an external reference: "(easing) indicates if the command should "accelerate". See [Easing Functions Cheat Sheet](http://easings.net)."

The older Cheat Sheet only documents 0/1/2:
> `0` none / `1` start fast and slow down / `2` start slow and speed up — [Cheat sheet](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Cheat_Sheet)

In code the value is a direct cast to the framework enum, so the numeric range is defined by `osu.Framework.Graphics.Easing`:
```csharp
var easing = (Easing)Parsing.ParseInt(split[1]);
```
— [`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L230

### 2.4 `P` (Parameter) — exact values

> "Unlike the other commands, which can be seen as setting endpoints along continually-tracked values, the Parameter command apply ONLY while they are active, i.e.,you can't put a command from timestamps 1000 to 2000 and expect the value to apply at time 3000, even if the object's other commands aren't finished by that point."
> where (parameter) is one of the following:
> - "H" - flip the image horizontally (**NOT** the same as rotating the object 180 degrees, i.e., pi radians). [Horizontal Flip]
> - "V" - flip the image vertically. [Vertical Flip]
> - "A" - use additive-colour blending instead of alpha-blending
> — [Storyboard scripting commands](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Commands)

Cheat Sheet wording: "**p**: the effect parameter to apply: H for horizontal flip, V for vertical flip, and A for additive blend mode (as opposed to alpha-blend)" — [Cheat sheet](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Cheat_Sheet).

Decoder (note the `startTime == endTime` distinction — a zero-duration `P` becomes a persistent state, otherwise it reverts by inheriting):

```csharp
case "P":
{
    string type = split[4];
    switch (type)
    {
        case "A":
            currentCommandsGroup?.AddBlendingParameters(easing, startTime, endTime, BlendingParameters.Additive,
                startTime == endTime ? BlendingParameters.Additive : BlendingParameters.Inherit);
            break;
        case "H":
            currentCommandsGroup?.AddFlipH(easing, startTime, endTime, true, startTime == endTime);
            break;
        case "V":
            currentCommandsGroup?.AddFlipV(easing, startTime, endTime, true, startTime == endTime);
            break;
    }
    break;
}
```
— [`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L311–332

### 2.5 Compound commands `L` and `T`

> "These are more complicated commands that don't do anything by themselves. Instead, they **provide conditions for when other events happen.**"
> — [Compound commands](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Compound_Commands)

**`L` (Loop):**
```
_L,(starttime),(loopcount)
__(event),(easing),(relative_starttime),(relative_endtime),(params...)
// More events allowed
```
> "Loops are done on commands within an object, not across several objects."
> "(relative_starttime) is the amount of time **since the start of that iteration** that this event should begin"
> — [Compound commands](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Compound_Commands)

Cheat Sheet adds: "Note that events inside a loop should be timed with a **zero-base**… The loop event's start time will be added to this value at game runtime." ([Cheat sheet](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Cheat_Sheet))

Command-line storage: a loop is modelled as commands re-based onto the loop start time:
```csharp
: base(command.Easing, loopingGroup.LoopStartTime + command.StartTime, loopingGroup.LoopStartTime + command.EndTime, command.StartValue, command.EndValue)
```
— [`StoryboardLoopingGroup.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Commands/StoryboardLoopingGroup.cs) L55

### 2.6 Shorthand (3 forms)

> "To make life easier, there are three cases of **shorthand** when writing storyboard commands."
> — [Shorthand](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Shorthand)

| # | Rule | Form |
|---|---|---|
| 1 | "Same Event, Same Duration, Sequentially" | `_(event),(easing),(starttime_of_first),(endtime_of_first),(value(s)_1),(value(s)_2),(value(s)_3),(value(s)_4)` → three chained commands, each shifted by `duration = endtime_of_first − starttime_of_first` |
| 2 | "Start and End Values are the Same" | `_(event),(easing),(starttime),(endtime),(value(s))` is treated as `…,(value(s)),(value(s))` |
| 3 | "Start and End Times are the Same" | `_(event),(easing),(starttime),,(params...)` — "you can leave out the endtime (though you still need the comma before and after where it would be)" |

Decoder support for form 3: `if (string.IsNullOrEmpty(split[3])) split[3] = split[2];` ([`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L227–228). Forms 1 and 2 are not expanded by the decoder — they are handled by the parser's index bounds (`split.Length > 5 ? … : startValue`) or by the beatmap editor's save step.

---

## 3. Triggers — the critical question

### 3.1 Wiki documentation, transcribed in full

> "In addition to the "implicit" player feedback via the separate Pass/Fail layers, you can use one of several **Trigger conditions** to cause a series of events to happen whenever that condition is fulfilled within a certain time period. The official specification calls these "trigger loops" due to their syntactic similarity to Loops (L), but they aren't loops at all, so here they are simply called "Triggers"."
>
> ```
> _T,(triggerType),(starttime),(endtime)
> __(event),(easing),(relative_starttime),(relative_endtime),(params...)
> // More events allowed
> ```
>
> "(triggerType) indicates the trigger condition and can be one of the following:
>
> **HitSound[SampleSet] [AdditionsSampleSet] [Addition] [CustomSampleSet]**, where:
> - *SampleSet* and *AdditionsSampleSet* are one of All / Normal / Soft / Drum.
> - *Addition* is one of Whistle / Finish / Clap.
> - *CustomSampleSet* is the custom sample number, 0 for Default.
>
> All of these are optional, examples:
> - HitSound (any hitsound is played)
> - HitSoundClap (any clap hitsound is played)
> - HitSoundFinish (any finish hitsound is played)
> - HitSoundWhistle (any whistle hitsound is played)
> - HitSoundDrumWhistle (a whistle hitsound is played with the drum addition sample set)
> - HitSoundSoft (any hitsound is played with the soft sample set)
> - HitSoundAllSoft (any hitsound is played with the soft addition sample set)
> - HitSoundDrumClap0 (the default clap from the drum sampleset is played)
> - HitSound6 (any hitsound is played with the custom sample set 6)
>
> - **Passing** (transition from fail state to pass state)
> - **Failing** (transition from pass state to fail state)
>
> - (starttime) is the timestamp at which the trigger becomes valid
> - (endtime) is the timestamp at which the trigger stops being valid
> - (relative_starttime) is the amount of time **since the trigger event** that this event should begin
> - (relative_endtime) is the amount of time **since the trigger event** that this event should end
> - (group_number) (optional, default value is 0 for no group) allows triggers on the same sprite to be grouped so that all triggers of the group are stopped when one trigger starts.
>
> If a trigger condition occurs while another trigger is running, the earlier trigger is stopped, and the new trigger starts. Triggers will not occur until other commands are finished, so it's usually best to either use only triggers on an object declaration or not at all."
>
> — [Compound commands](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Compound_Commands)

**Exact spelling confirmed: yes — `HitSoundClap`, `HitSoundFinish`, `HitSoundWhistle`, `Passing`, `Failing` are all real, and the grammar is a prefix-composed family, not a fixed list.**

### 3.2 What the older Cheat Sheet says (narrower list)

> "Current triggers supported are:
> - HitSoundClap
> - HitSoundFinish
> - HitSoundWhistle
> - Passing (transition from fail state to pass state)
> - Failing (transition from pass state to fail state)
>
> Trigger loops are zero-based similar to normal loops. If two overlap, the first will be halted and replaced by a new loop from the beginning. If they overlap any existing storyboarded events, they will not trigger until those transformations are not in effect."
> — [Cheat sheet](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Cheat_Sheet)

> ⚠️ The Cheat Sheet's 5-item list is **incomplete/outdated** relative to the Compound Commands page: the Cheat Sheet omits the whole `HitSound<bank><bank><addition><suffix>` combinator space (`HitSound`, `HitSoundSoft`, `HitSoundAllSoft`, `HitSoundDrumWhistle`, `HitSoundDrumClap0`, `HitSound6`, …) that the Compound Commands page documents.

### 3.3 The authoritative runtime trigger list (client source)

The decoder does **not** validate trigger names — it stores whatever string appears:

```csharp
case "T":
{
    string triggerName = split[1];
    double startTime = split.Length > 2 ? Parsing.ParseDouble(split[2]) : double.MinValue;
    double endTime = split.Length > 3 ? Parsing.ParseDouble(split[3]) : double.MaxValue;
    // negation as per https://github.com/peppy/osu-stable-reference/blob/c34a74fb61c17c5667486a12548485d1f03baa2e/osu!/GameplayElements/HitObjectManager_LoadSave.cs#L736
    int groupNumber = split.Length > 4 ? -Parsing.ParseInt(split[4]) : 0;
    currentCommandsGroup = storyboardSprite?.AddTriggerGroup(triggerName, startTime, endTime, groupNumber);
    break;
}
```
— [`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L206–215

The **real** list is the dispatch in the trigger controller:

```csharp
public void Bind<TDrawable>(TDrawable drawable, StoryboardTriggerGroup triggerGroup)
{
    switch (triggerGroup.TriggerName)
    {
        case @"Passing":            bindPassing(drawable, triggerGroup, true);  break;
        case @"Failing":            bindPassing(drawable, triggerGroup, false); break;
        case @"HitObjectHit":       bindHitObjectHit(drawable, triggerGroup);   break;
        case string s when s.StartsWith(HitSampleTriggerDefinition.PREFIX, StringComparison.OrdinalIgnoreCase):
                                    bindHitSample(drawable, triggerGroup);      break;
    }
}
```
— [`osu.Game/Storyboards/Drawables/StoryboardTriggerController.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/StoryboardTriggerController.cs) L43–64

| Trigger | Exact spelling | Client behaviour (source) |
|---|---|---|
| Pass/Fail state | `Passing`, `Failing` | Bound to a `Bindable<bool> Passing`; fires `playTrigger` only when the bindable's new value equals the trigger's polarity |
| **Any hit object hit** | **`HitObjectHit`** | Fires when `gameplayState.LastJudgementResult` changes and the new result satisfies `IsNotNull() && IsHit && Type.IsScorable()` |
| Hitsound family | `HitSound` **prefix, case-insensitive** | Parsed by regex; fires when `gameplayState.LastPlayedSamples` changes and the played `HitSampleInfo[]` matches the definition |

The hitsound grammar is enforced by this regex — this is the *exact* accepted syntax, wider than the wiki's prose:

```csharp
public const string PREFIX = @"HitSound";

private static readonly Regex parse_regex = new Regex(
    @$"(?i)^{PREFIX}(?<bank1>(All|Normal|Soft|Drum))?(?<bank2>(All|Normal|Soft|Drum))?(?<name>(Whistle|Clap|Finish))?(?<suffix>\d+)?$",
    RegexOptions.Compiled);
```
— [same file](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/StoryboardTriggerController.cs) L109–121

Everything is optional, so `HitSound`, `HitSoundClap`, `HitSoundDrumWhistle`, `HitSoundAllSoft`, `HitSoundDrumClap0`, `HitSound6` all parse. Bank mapping: `normal → BANK_NORMAL`, `soft → BANK_SOFT`, `drum → BANK_DRUM`, and `"all" falls into this case which is intended as it means no filter anyway.` The disambiguation rule for a single bank followed by an addition name:

```csharp
// https://github.com/peppy/osu-stable-reference/blob/baa8705f782c0de2b10a7387d78014c61c8b17fb/osu!/GameplayElements/Events/Trigger/EventTriggerHitSound.cs#L70-L80
bool bank1IsAddition = bank1 != null && bank2 == null && name != null;
```
— [same file](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/StoryboardTriggerController.cs) L138–139

Trigger activation is guarded by the active window and by events, not by polling:

```csharp
private void bindHitSample<TDrawable>(...)
{
    if (!HitSampleTriggerDefinition.TryParse(triggerGroup.TriggerName, out var definition)) return;

    lastPlayedSamples.BindValueChanged(val =>
    {
        if (val.NewValue == null) return;
        if (!triggerGroup.ActiveAt(drawable.Time.Current)) return;
        if (definition.Value.Matches(val.NewValue.OfType<HitSampleInfo>()))
            playTrigger(drawable, triggerGroup);
    });
}
```
— [same file](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/StoryboardTriggerController.cs) L89–105, with `ActiveAt` = `TriggerStartTime <= time && time <= TriggerEndTime` ([`StoryboardTriggerGroup.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Commands/StoryboardTriggerGroup.cs) L21)

### 3.4 ⚠️ Documented stable-vs-lazer divergence (verbatim from source)

```csharp
// lazer treats "addition bank" and "addition samples" slightly differently than stable does.
// in stable, "normal bank" and "addition bank" were just properties of a hitsound.
// a hitsound did *need* to have an addition sound to have a defined bank for it.
// contrary to this, lazer will just not emit an addition `HitSampleInfo` at all,
// and because `HitSampleInfo` is what stores the bank, there is nowhere for the addition bank definition to go.
// this is why the behaviour of the trigger as written below is not 100% accurate to stable.
// the difference is perceivable the most on triggers like `HitSoundAllSoft` which will sometimes not trigger
// on objects that do not have an addition sound, even if the "soft" bank should be inherited via a "green line" or baseline beatmap sample set.
```
— [`StoryboardTriggerController.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/StoryboardTriggerController.cs) L177–184

### 3.5 **CAN A STORYBOARD TRIGGER A VISUAL EVENT ON A SPECIFIC HIT / NOTE / HITSOUND?**

**Answer, scoped precisely:**

| Sub-question | Verified answer | Evidence |
|---|---|---|
| Visual event on a **hitsound**? | **Yes.** `HitSound*` triggers fire on the game's *last played samples* and match against sample bank/addition/suffix. | [Compound commands](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Compound_Commands) + [`StoryboardTriggerController.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/StoryboardTriggerController.cs) L89–105 |
| Visual event on **any hit object being hit**? | **Yes** — `HitObjectHit`, added in osu!(lazer). This is the per-object trigger. It fires on *any* successfully hit, scorable judgement. | [`StoryboardTriggerController.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/StoryboardTriggerController.cs) L56–58, L78–85 |
| Visual event on **one specific, individually identified note**? | **No.** | There is no note/object ID, index or timestamp reference anywhere in the trigger grammar. `HitSampleTriggerDefinition` holds only `RawName`, `NormalBank`, `AdditionBank`, `AdditionName`, `Suffix` ([L111–115](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/StoryboardTriggerController.cs)); `StoryboardTriggerGroup` holds only `TriggerName`, `TriggerStartTime`, `TriggerEndTime`, `GroupNumber`. The *only* targeting mechanism is the validity window `[starttime, endtime]`. |
| How do you approximate per-note triggering? | Narrow the trigger's `starttime`/`endtime` to bracket the note's timestamp. That is the documented/implemented mechanism. | `ActiveAt(double time) => TriggerStartTime <= time && time <= TriggerEndTime` |
| Do `HitSound*` triggers relate to **the beatmap's own hitsound data**? | **Yes** — they are driven by the hitsounds the game actually plays for hit objects. The wiki's own example `HitSoundDrumClap0` = "the default clap from the drum sampleset is played"; `HitSound6` = "any hitsound is played with the custom sample set 6" — i.e. the trigger's *CustomSampleSet* number corresponds to the `hitSample` **index** field of a hit object (`.osu` format: "`index` (Integer): Index of the sample. If this is `0`, the timing point's sample index will be used instead."). | [Compound commands](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Compound_Commands), [.osu (file format) § Hitsounds](https://osu.ppy.sh/wiki/en/Client/File_formats/osu_%28file_format%29) |
| Is `HitObjectHit` on the wiki yet? | **No.** It is documented only by the merged PR and the client source. | [PR #38335](https://github.com/ppy/osu/pull/38335) |

**PR evidence for `HitObjectHit`:**

> Title: "Implement storyboard passing, failing, & hit object hit triggers" — author `bdach`, repository `ppy/osu`, PR **#38335**, state **`"state":"MERGED"`**.
> Body excerpt: "RFC / Part of #2084 / Passing & failing triggers / Caveats here are very similar to #28480 in that when these are triggered will not match stable because stable's code surrounding that area is on…"
> — [github.com/ppy/osu/pull/38335](https://github.com/ppy/osu/pull/38335)

**Practical implication for a Unity re-implementation:** model triggers as *event subscriptions with an active time window*, not as per-frame script evaluation. `HitSound*` = subscribe to "samples played by the last hit"; `HitObjectHit` = subscribe to "last judgement result changed to a hit and is scorable"; `Passing`/`Failing` = subscribe to the pass/fail state bindable. To emulate "flash on note #137", narrow the window to note #137's timestamp (and, if available, discriminate by its hitsound configuration).

**Ranking-criteria caution about triggers (relevant to authoring):**

> "**Fade out sprites activated from triggers after usage.** Triggers will activate from their first possible command and stay active until the end of the difficulty, which is why fading these out when done is preferable."
> "**Avoid illogical, conflicting and obsolete commands.** Commands that have their ending time before their start time or are bound to impossible to reach triggers are either not working as intended or obsolete…"
> — [Ranking criteria § Storyboarding](https://osu.ppy.sh/wiki/en/Ranking_criteria)

---

## 4. Video backgrounds

### 4.1 Does osu! support video backgrounds?

**Yes.** The `.osu` format documents a `Video` event type in `[Events]`, and the client places it in a dedicated storyboard layer.

> "**Background** (***BG***) images and videos can be added to beatmaps."
> — [Beatmap/Background](https://osu.ppy.sh/wiki/en/Beatmap/Background)

> Note: this is the *whole* substantive body of that page (it is flagged `stub: true` in the `ppy/osu-wiki` repo). There is **no** `Beatmap/Video` page — `wiki/Beatmap/Video/en.md` returns 404 from the osu-wiki repository, and `https://osu.ppy.sh/wiki/en/Beatmap/Video` resets the connection.

### 4.2 Formats — the current official statement

> "**osu! supports video encoded in the H.264 format with the `.mp4` file extension.** Other formats, such as H.265, VP9, and AV1, and file extensions such as `.mkv` and `.mov`, are currently not supported."
> "**The ranking criteria specify a maximum video resolution of 1280x720 pixels.**"
> — [Guides/Compressing files § Video](https://osu.ppy.sh/wiki/en/Guides/Compressing_files)

Ranking criteria (rules, verbatim):

> "**A video's dimensions must not exceed a width of 1280 and a height of 720 pixels.** Additionally, upscaling lower resolution video to a higher resolution should be avoided. This ensures video files do not become excessively large or resource intensive."
> "**A video must be encoded in H.264.**"
> "**A video's offset must be correct if it synchronizes with the song.** An incorrect offset can result in a misleading visual representation of the song. If the same video appears in multiple difficulties, it must always have the same offset(s)."
> "**A video's audio track must be removed from the video file.** The audio track in a video is not used in osu!, and removing it reduces the file size of the beatmap. This includes videos with muted audio tracks."
> "**Videos which are substantially AI-generated must not be used.**"
> — [Ranking criteria § Video and background](https://osu.ppy.sh/wiki/en/Ranking_criteria)

### 4.3 ⚠️ Correction: `.avi` / `.flv`

The question's premise ("I believe `.avi`/`.flv`/`.mp4`") is **half right, depending on which layer you mean**:

| Layer | Statement | Source |
|---|---|---|
| **Documented/ranking-supported** | H.264 in `.mp4` **only**. `.mkv` and `.mov` explicitly "not supported". | [Compressing files](https://osu.ppy.sh/wiki/en/Guides/Compressing_files) |
| **Actually accepted by the osu!(lazer) client's file filter** | `.mp4`, `.mov`, `.avi`, `.flv`, `.mpg`, `.wmv`, `.m4v` | [`osu.Game/Utils/SupportedExtensions.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Utils/SupportedExtensions.cs) |

```csharp
public static readonly string[] VIDEO_EXTENSIONS = [@".mp4", @".mov", @".avi", @".flv", @".mpg", @".wmv", @".m4v"];
public static readonly string[] AUDIO_EXTENSIONS = [@".mp3", @".ogg", @".wav"];
public static readonly string[] IMAGE_EXTENSIONS = [@".jpg", @".jpeg", @".png"];
```
— [`osu.Game/Utils/SupportedExtensions.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Utils/SupportedExtensions.cs)

So `.avi`/`.flv` are **decodable** by lazer (ffmpeg-backed) but are **not** the documented/rankable format. Whether osu!**stable** ever officially supported `.avi`/`.flv` is **未验证** — I found no current official page stating it.

**Extension is checked before a video is accepted at all** (an image mis-declared as video is silently downgraded to a background):
```csharp
// This avoids potential weird crashes when ffmpeg attempts to parse an image file as a video
// (see https://github.com/ppy/osu/issues/22829#issuecomment-1465552451).
if (!SupportedExtensions.VIDEO_EXTENSIONS.Contains(Path.GetExtension(path).ToLowerInvariant()))
    break;
```
— [`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L142–148

### 4.4 What layer does the video occupy?

**A dedicated `Video` layer — the deepest layer, behind `Background`.**

```csharp
layers.Add("Video", new StoryboardVideoLayer("Video", 4, false));
layers.Add("Background", new StoryboardLayer("Background", 3));
layers.Add("Fail", new StoryboardLayer("Fail", 2) { VisibleWhenPassing = false, });
layers.Add("Pass", new StoryboardLayer("Pass", 1) { VisibleWhenFailing = false, });
layers.Add("Foreground", new StoryboardLayer("Foreground", minimumLayerDepth = 0));
...
layers.Add("Overlay", new StoryboardLayer("Overlay", int.MinValue));
```
— [`osu.Game/Storyboards/Storyboard.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Storyboard.cs) L66–72

The decoder routes video into that layer by name:

```csharp
storyboard.GetLayer("Video").Add(storyboardSprite = new StoryboardVideo(source, path, offset));
```
— [`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L150

Layer depth ordering in lazer (higher depth = further back): `Video (4) → Background (3) → Fail (2) → Pass (1) → Foreground (0) → Overlay (int.MinValue = frontmost)`.

The legacy numeric layer enum also reserves a video slot: `Overlay = 4, Video = 5` ([`LegacyStoryLayer.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Legacy/LegacyStoryLayer.cs)).

### 4.5 Can video and storyboard coexist?

**Yes — verified from three independent primary sources:**

1. They are declared in the *same* `[Events]` section: the `.osu` format page defines Background, Video, Breaks and Storyboards as sibling event categories ([.osu (file format) § Events](https://osu.ppy.sh/wiki/en/Client/File_formats/osu_%28file_format%29)).
2. The client models video as a `StoryboardVideo : StoryboardSprite` living inside the same `Storyboard` object graph as ordinary sprites, in its own layer ([`StoryboardVideo.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/StoryboardVideo.cs), [`Storyboard.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Storyboard.cs) L57 `public StoryboardVideo? PrimaryVideo => GetLayer(@"Video").Elements.OfType<StoryboardVideo>().FirstOrDefault();`).
3. The Design tab provides a combined toggle: "Shows the **readings** and a **toggle to add a background image/video.**" ([Design tab](https://osu.ppy.sh/wiki/en/Client/Beatmap_editor/Design))

Additional verified behaviours around video:
- Video/background events are excluded from storyboard lifetime computation **to match osu!stable**: `// Video and background events are not included to match stable.` on both `EarliestEventTime` and `LatestEventTime` ([`Storyboard.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Storyboard.cs) L38, L51).
- A storyboard can suppress the beatmap background: `ReplacesBackground` returns true when the `Background` layer contains an element whose path equals `BeatmapInfo.Metadata.BackgroundFile` ([`Storyboard.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Storyboard.cs) L84–99). The wiki describes the same mechanism from the authoring side ([SB load § Disable your background image](https://osu.ppy.sh/wiki/en/Client/Beatmap_editor/SB_load)).
- The wiki's *explicit* statement about relative draw order of video vs. storyboard layers is **未验证** — no wiki page states where the video sits relative to `Background`/`Foreground`. The layering above comes from client source only.

---

## 5. Authoring tools

### 5.1 Official in-game editor — the Design tab

> "The **Storyboard Editor** is a section of the in-game Beatmap Editor, under the Design tab, which enables simple Storyboarding. It is a good introduction to the fundamental concept before the more advanced Storyboard Scripting."
> — [Design tab](https://osu.ppy.sh/wiki/en/Client/Beatmap_editor/Design)

The beatmap editor has four sections: "Compose / Design / Timing / Song setup", where "Design offers a way to make storyboards, the visual effects that accompany beatmaps. Because storyboards often have complex effects warranting usage of many storyboard commands, mappers also use storyboard scripting directly without entering the design tab." — [Beatmap editor](https://osu.ppy.sh/wiki/en/Client/Beatmap_editor)

**What it can do** ([Design tab](https://osu.ppy.sh/wiki/en/Client/Beatmap_editor/Design)):

| Capability | Detail |
|---|---|
| Sprite placement | "click on "Sprite Library" and select your picture. Make sure your element doesn't surpass 800x600 px. That is the maximum threshold." |
| Keyframes | "**adding anchor points (Start/End points).** It works pretty much the same way as bookmarks." |
| Commands exposed | "Five of the commands have been put in; Move, Scale, Fade, Rotate and Colour." |
| Additional effects | "Vector Scale" and "Horizontal/Vertical Flip" (hover over left bar) |
| Extra toggles | "Tweening", "Easing In/Out", "Origin", "Diff. Specific" |
| Layer toggles | Background / Failing / Passing / Foreground / HitObjects (automatically disabled) |
| Save target | "`.osb`: "Design" base (BG, Video, SB) for each difficulty of the beatmap to follow." vs "`.osu`: Difficulty-specific file." |
| Readings | "**SB Load** is the amount of processing power required to play the storyboard *alone* only. Generally, keep the SB load as low as possible (1.00~2.00) during playtime" |

**Limitations — verbatim:**

> "- No sound effect support, this is not a big problem as sound effects can distract players, especially if they are near hit objects. Use of sound effects should be done by advanced mappers only and with the guidance of a BAT.
> - **No loop or trigger support.**
> - **No Move-X/Move-Y commands.**
> Sprite coordinate is *always* 320,240. You will need to use Move command once to set the location (endpoint not required).
>   - If you are *also* doing Storyboard Scripting, you will need to *read an extra line* per object done in Design tab."
> — [Design tab § Limitations](https://osu.ppy.sh/wiki/en/Client/Beatmap_editor/Design)

Also from the same page: "To use the Loop and Parameters, you will need to do some Storyboard Scripting to utilise them." — i.e. **loops, triggers and `P` are scripting-only.**

**SB Load** definition (a metric worth copying for a Unity implementation):

> "**SB Load** (short for storyboard load) is a number used in Storyboarding to indicate how much more load the Storyboard is causing on the graphics program. It is a measure of how many times the full 640x480 area needs to be redrawn in a frame."
> "Without any storyboarding, this value is 1x… Including a single image that takes up exactly half of the screen would result in 1.5x; two images that overlap entirely and take up half of the screen would result in 2x."
> "It's best if a map never exceeds 5x SB Load."
> — [SB load](https://osu.ppy.sh/wiki/en/Client/Beatmap_editor/SB_load)

### 5.2 "Sabik"

**未验证 / not documented.** I found **no** mention of "Sabik" anywhere in the official osu! wiki (including the `Storyboard`, `Storyboard scripting`, and `Design tab` pages and their "Community tools"/"Source" sections). Web searches for `osu storyboard Sabik` returned no osu!-related primary source (only unrelated non-osu documents). **Conclusion: "Sabik" is not an officially documented osu! storyboard tool as far as primary sources show.**

### 5.3 "SGL" (Storyboard Graphing Language)

**Not documented on the official osu! wiki.** Searches of the official wiki return nothing for SGL; the only osu!-hosted source is a **community forum thread**, not wiki content:

> Thread title: "[Guide] SGL & Optimization" — `https://osu.ppy.sh/community/forums/topics/378938`
> First post: "Hi everyone! I would like to introduce my storyboarding guide to you. **I would like to focus on SGL coding, which is basically "programming" language developed by MoonShade and forked by Damnae.** Work with SGL is very similar to any other programming language… To start coding the storyboards, make sure to: Own **Damnae's release of SGL Editor**. MoonShade's version is outdated and is missing some functions implemented in this release."
> — [\[Guide\] SGL & Optimization](https://osu.ppy.sh/community/forums/topics/378938)

| Claim | Status |
|---|---|
| SGL is a scripting language that compiles to storyboard code, originally by **MoonShade**, forked and maintained by **Damnae** (author of storybrew) | **Verified** (community forum post, first-party author statement — but *community*, not official docs) |
| SGL is documented on the official osu! wiki | **False** — not present |
| The expansion "**S**toryboard **G**raphing **L**anguage" | **未验证** — the forum post never expands the acronym |

### 5.4 Community tools named by official wiki

The **only** community tool named anywhere in the official storyboard documentation is storybrew:

> "Various tools have been made by the community to abstract and build upon storyboard scripting, such as Damnae's storybrew."
> — [Storyboard scripting § Community tools](https://osu.ppy.sh/wiki/en/Storyboard/Scripting)

The `Design tab` page's "Source" section cites community guides: "m980's basic explanation" and "Kite's Basic Manual Storyboarding Guide".

> ⚠️ The wiki itself notes the scripting route is the norm: "Storyboarding is often very difficult… osu! offers a built-in editor inside of the beatmap editor to aid the creation of storyboards, although **most avid storyboarders opt to program via storyboard scripting directly**. Many creators choose to write programs in full-featured programming languages to generate storyboard scripts, because complex visual effects can require a great amount of storyboard code to produce." — [Storyboard](https://osu.ppy.sh/wiki/en/Storyboard)

---

## 6. The `[Events]` section in `.osu` — exact syntax

All of the following is quoted from [.osu (file format) § Events](https://osu.ppy.sh/wiki/en/Client/File_formats/osu_%28file_format%29):

> "*Event syntax:* `eventType,startTime,eventParams`"
> - "**`eventType` (String or Integer):** Type of the event. Some events may be referred to by either a name or a number."
> - "**`startTime` (Integer):** Start time of the event, in milliseconds from the beginning of the beatmap's audio. For events that do not use a start time, the default is `0`."
> - "**`eventParams` (Comma-separated list):** Extra parameters specific to the event's type."

### 6.1 Background — YES, declared in `[Events]`

> "*Background syntax:* `0,0,filename,xOffset,yOffset`"
> - "**`filename` (String):** Location of the background image relative to the beatmap directory. Double quotes are usually included surrounding the filename, but they are not required."
> - "**`xOffset` (Integer)** and **`yOffset` (Integer):** Offset in osu! pixels from the centre of the screen. For example, an offset of `50,100` would have the background shown 50 osu! pixels to the right and 100 osu! pixels down from the centre of the screen. If the offset is `0,0`, writing it is optional."

So `0,0,"bg.jpg",0,0` is valid, and `0,0,"bg.jpg"` is equivalent.

### 6.2 Video — YES, declared in `[Events]`

> "*Video syntax:* `Video,startTime,filename,xOffset,yOffset`"
> "`Video` may be replaced by `1`."
> - "**`filename` (String)**, **`xOffset` (Integer)**, and **`yOffset` (Integer)** behave exactly as in backgrounds."

So both `Video,0,"video.mp4"` and `1,0,"video.mp4"` are valid event lines.

> ⚠️ **Documentation vs. implementation mismatch (verified):** the wiki names the second field `startTime`, but the client reads it as a **video offset in milliseconds** and stores it as the video's `StartTime`:
> ```csharp
> int offset = Parsing.ParseInt(split[1]);
> string path = CleanFilename(split[2]);
> ...
> storyboard.GetLayer("Video").Add(storyboardSprite = new StoryboardVideo(source, path, offset));
> ```
> — [`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L137–151
> ```csharp
> // This is just required to get a valid StartTime based on the incoming offset.
> // Actual fades are handled inside DrawableStoryboardVideo for now.
> StartTime = offset;
> ```
> — [`StoryboardVideo.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/StoryboardVideo.cs)
> Also note the client decoder ignores `xOffset`/`yOffset` for videos — `StoryboardVideo` is constructed as `base(source, path, Anchor.Centre, Vector2.Zero)`, i.e. always centred at zero offset.

### 6.3 Breaks (same section)

> "*Break syntax:* `2,startTime,endTime`" — "`2` may be replaced by `Break`."

### 6.4 Storyboards (same section)

> "Storyboards can be defined in a separate optional storyboard file with the `.osb` extension. External storyboards are shared between all difficulties in a beatmap."
> "Each beatmap may contain its own difficulty-specific storyboard, either in conjunction with the external storyboard or by itself."

### 6.5 Which file may declare what (verified from source)

`Background` in a `.osb` is parsed only for its offset — the filename itself is taken from the `.osu`:

```csharp
case LegacyEventType.Background:
{
    // the actual filename is handled in `LegacyBeatmapDecoder`.
    // this only handles the background offset, because it does not logically belong in `Beatmap` or related classes.
    if (split.Length > 4)
    {
        float x = Parsing.ParseFloat(split[3]);
        float y = Parsing.ParseFloat(split[4]);
        storyboard.BackgroundOffset = new Vector2(x, y);
    }
    break;
}
```
— [`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L123–135

And in the beatmap decoder, `Background` sets the metadata background file while `Video` with a non-video extension is downgraded:
```csharp
case LegacyEventType.Background:
    beatmap.BeatmapInfo.Metadata.BackgroundFile = CleanFilename(split[2]);
...
case LegacyEventType.Video:
    // Some very old beatmaps had incorrect type specifications for their backgrounds (ie. using 1 for VIDEO
    // instead of 0 for BACKGROUND). To handle this gracefully, check the file extension against known supported
    // video extensions and handle similar to a background if it doesn't match.
    if (!SupportedExtensions.VIDEO_EXTENSIONS.Contains(Path.GetExtension(filename).ToLowerInvariant()))
        beatmap.BeatmapInfo.Metadata.BackgroundFile = filename;
```
— [`osu.Game/Beatmaps/Formats/LegacyBeatmapDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyBeatmapDecoder.cs) L457–471

Elements loaded from the external `.osb` are tagged differently from per-difficulty ones:
```csharp
var source = isPrimaryStream ? StoryboardElementSource.Beatmap : StoryboardElementSource.Shared;
```
— [`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L119

### 6.6 Related `[General]` storyboard toggles

> "`UseSkinSprites: 1` — Allows storyboards to refer to the player's current skin's sprites by their filenames."
> — [.osu file toggles](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/osu%21_File_Toggles)

`.osu` `[General]` also has `WidescreenStoryboard` ("Whether or not the storyboard allows widescreen viewing", default 0) and `EpilepsyWarning` ("Whether or not a warning about flashing colours should be shown at the beginning of the map", default 0) — [.osu (file format) § General](https://osu.ppy.sh/wiki/en/Client/File_formats/osu_%28file_format%29). The client's `.osb` `[General]` handler reads `UseSkinSprites` and `WidescreenStoryboard` ([`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L78–92).

---

## 7. Is the storyboard evaluated at runtime? (how commands are evaluated)

**Answer: Yes, but not by a per-frame script interpreter.** The script is parsed **once** at load into typed command objects, which are then **compiled into `osu.Framework` transform sequences** attached to drawables; the framework's transform/clock system then evaluates those interpolations per frame. Triggers are **not** part of that timeline at all — they are **event subscriptions** bound to gameplay bindables.

### 7.1 Parse once → typed command objects

Every command is a typed object carrying start/end time, start/end value and easing:

```csharp
public abstract class StoryboardCommand<T> : IStoryboardCommand, IComparable<StoryboardCommand<T>>
{
    public double StartTime { get; }
    public double EndTime { get; }
    public T StartValue { get; }
    public T EndValue { get; }
    public Easing Easing { get; }
    public double Duration => EndTime - StartTime;

    protected StoryboardCommand(Easing easing, double startTime, double endTime, T startValue, T endValue)
    {
        if (endTime < startTime)
            endTime = startTime;
        ...
    }
```
— [`osu.Game/Storyboards/Commands/StoryboardCommand.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Commands/StoryboardCommand.cs) L11–32

> Note the defensive clamp: `if (endTime < startTime) endTime = startTime;` — end-before-start is tolerated at load, not rejected.

### 7.2 Interpolation + easing happen in the framework transform system

```csharp
public class StoryboardAlphaCommand : StoryboardCommand<float>
{
    public override string PropertyName => nameof(Drawable.Alpha);

    public override void ApplyInitialValue<TDrawable>(TDrawable d) => d.Alpha = StartValue;

    public override TransformSequence<TDrawable> ApplyTransforms<TDrawable>(TDrawable d)
        => d.FadeTo(StartValue).Then().FadeTo(EndValue, Duration, Easing);
}
```
— [`osu.Game/Storyboards/Commands/StoryboardAlphaCommand.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Commands/StoryboardAlphaCommand.cs)

This is the key architectural fact: **the storyboard's easing numbers map directly onto the framework's easing, and interpolation is delegated to `TransformSequence`** (`FadeTo` for `F`, with `MoveTo`/`ScaleTo`/`RotateTo`/`Colour` equivalents for `M`/`S`/`R`/`C` etc.). There is no hand-written `Lerp` per frame in `osu.Game`.

### 7.3 Loops compile to a framework `.Loop(...)`

```csharp
private class StoryboardLoopingCommand<T> : StoryboardCommand<T>, IStoryboardLoopingCommand
{
    public StoryboardLoopingCommand(StoryboardCommand<T> command, StoryboardLoopingGroup loopingGroup)
        // In an ideal world, we would multiply the command duration by TotalIterations in command end time.
        // Unfortunately this would clash with how stable handled end times, and results in some storyboards playing outro
        // sequences for minutes or hours.
        : base(command.Easing, loopingGroup.LoopStartTime + command.StartTime, loopingGroup.LoopStartTime + command.EndTime, command.StartValue, command.EndValue)
    { ... }

    public override TransformSequence<TDrawable> ApplyTransforms<TDrawable>(TDrawable d)
    {
        double loopingGroupDuration = loopingGroup.Duration;

        if (loopingGroupDuration == 0 || loopingGroup.TotalIterations == 0)
            return command.ApplyTransforms(d);

        return command.ApplyTransforms(d).Loop(loopingGroupDuration - Duration, loopingGroup.TotalIterations);
    }
}
```
— [`StoryboardLoopingGroup.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Commands/StoryboardLoopingGroup.cs) L44–74

Also note the counter-intuitive iteration bookkeeping: the decoder stores `Math.Max(0, repeatCount - 1)` and the group then computes `TotalIterations = repeatCount + 1`, so `loopcount` in the script equals total playbacks ([`LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) L221 + [`StoryboardLoopingGroup.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Commands/StoryboardLoopingGroup.cs) L31–37).

### 7.4 Commands are applied once, in chronological order, at drawable load

```csharp
public void ApplyTransforms<TDrawable>(TDrawable drawable, StoryboardTriggerController triggerController)
    where TDrawable : Drawable, IFlippable, IVectorScalable
{
    HashSet<string> appliedProperties = new HashSet<string>();

    // For performance reasons, we need to apply the commands in chronological order.
    // Not doing so will cause many functions to be interleaved, resulting in O(n^2) complexity.
    IEnumerable<IStoryboardCommand> commands = Commands.AllCommands;
    commands = commands.Concat(LoopingGroups.SelectMany(l => l.AllCommands));

    foreach (var command in commands.OrderBy(c => c.StartTime))
    {
        if (appliedProperties.Add(command.PropertyName))
            command.ApplyInitialValue(drawable);

        using (drawable.BeginAbsoluteSequence(command.StartTime))
            command.ApplyTransforms(drawable);
    }

    foreach (var triggerGroup in TriggerGroups)
        triggerController.Bind(drawable, triggerGroup);
}
```
— [`osu.Game/Storyboards/StoryboardSprite.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/StoryboardSprite.cs) L138–159

Called exactly once, from the drawable's dependency-load step — **not per frame**:

```csharp
[BackgroundDependencyLoader]
private void load(Storyboard storyboard, StoryboardTriggerController triggerController)
{
    ...
    Sprite.ApplyTransforms(this, triggerController);
}
```
— [`osu.Game/Storyboards/Drawables/DrawableStoryboardSprite.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/DrawableStoryboardSprite.cs) L109–121 (identical pattern in `DrawableStoryboardAnimation.cs` L117–127)

### 7.5 Lifetimes are precomputed, and trigger firing is re-based on the current clock

Drawable lifetimes come from precomputed sprite times:
```csharp
LifetimeStart = sprite.StartTime;
LifetimeEnd = sprite.EndTimeForDisplay;
```
— [`DrawableStoryboardSprite.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/DrawableStoryboardSprite.cs) L105–106

Triggers, when fired, animate **relative to the moment of firing**:
```csharp
private static void playTrigger<TDrawable>(TDrawable drawable, StoryboardTriggerGroup triggerGroup)
{
    if (!triggerGroup.ActiveAt(drawable.Time.Current)) return;

    foreach (var command in triggerGroup.AllCommands.OrderBy(c => c.StartTime))
    {
        using (drawable.BeginDelayedSequence(command.StartTime))
            command.ApplyTransforms(drawable);
    }
}
```
— [`StoryboardTriggerController.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/StoryboardTriggerController.cs) L217–228

And `StartTime` itself is a non-trivial computed property that special-cases alpha commands so that invisible-until-then sprites get correct lifetimes ([`StoryboardSprite.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/StoryboardSprite.cs) L28–84).

### 7.6 Summary table for the Unity port

| Mechanism | osu! implementation | Artefact to replicate |
|---|---|---|
| Parsing | One-shot line parser, depth-sensitive, comma-split | `LegacyStoryboardDecoder.cs` |
| Commands | Typed objects per property, sorted by `StartTime` | `StoryboardCommandGroup.cs` |
| Easing | `Easing` enum cast from int, delegated to framework | [Commands § easing table](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Commands) |
| Interpolation | `TransformSequence` (`FadeTo`/`MoveTo`/…) evaluated by framework clock | `StoryboardAlphaCommand.cs` and siblings |
| Loops | Framework `.Loop(durationGap, iterations)` | `StoryboardLoopingGroup.cs` |
| Triggers | Bindable event subscriptions with `[start,end]` active window | `StoryboardTriggerController.cs` |
| Layers | Named layers with integer depth | `Storyboard.cs` L66–72 |
| Lifetime | Precomputed `StartTime` / `EndTimeForDisplay` | `StoryboardSprite.cs` |

---

## 8. Open items / explicitly 未验证

| Item | Status |
|---|---|
| "Sabik" as an osu! storyboard tool | **未验证** — no mention in official wiki or client; web search found no osu!-related primary source. |
| SGL = "Storyboard Graphing Language" | **未验证** as an expansion. SGL's existence and authorship (MoonShade → Damnae) *is* verified, but only from a community forum post, not official docs. |
| `.avi` / `.flv` being *officially* supported historically (osu!stable) | **未验证** — the current official wiki documents H.264/`.mp4` only; lazer's file filter accepts `.avi`/`.flv`/`.mov`/`.mpg`/`.wmv`/`.m4v`, but that is decoder tolerance, not a documented authoring format. |
| Wiki's exact statement that video and storyboard *coexist* and where the video draws | **Partially verified.** Coexistence follows from shared `[Events]` syntax + a shared `Storyboard` graph + the Design tab's combined BG/video toggle. Video's **layer position** (its own `Video` layer at depth 4, behind `Background`) is verified **only** from `Storyboard.cs` source — no wiki page states it. |
| `Overlay` layer | The Objects page's numeric layer table lists only 0–3; the General rules page names `Overlay` as a fifth layer; the client enum has `Overlay = 4` and `Video = 5`. Consistent, but the numeric table is incomplete on the wiki. |
| Video offset vs. start time semantics | The wiki says `Video,startTime,…`; the decoder treats the field as an **offset** and ignores video x/y offsets. Divergence verified in source, not acknowledged on the wiki. |
| `HitObjectHit` trigger on the wiki | Not documented on the wiki as of this research; verified from merged [PR #38335](https://github.com/ppy/osu/pull/38335) and current `master` source. |

## 9. Source index

**Official wiki** (all fetched and transcribed in full for this report):
[Storyboard](https://osu.ppy.sh/wiki/en/Storyboard) ·
[Storyboard scripting](https://osu.ppy.sh/wiki/en/Storyboard/Scripting) ·
[General rules](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/General_Rules) ·
[Objects](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Objects) ·
[Commands](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Commands) ·
[Compound commands](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Compound_Commands) ·
[Audio samples](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Audio) ·
[Variables](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Variables) ·
[Shorthand](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Shorthand) ·
[Cheat sheet](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/Cheat_Sheet) ·
[.osu file toggles](https://osu.ppy.sh/wiki/en/Storyboard/Scripting/osu%21_File_Toggles) ·
[Beatmap editor](https://osu.ppy.sh/wiki/en/Client/Beatmap_editor) ·
[Design tab](https://osu.ppy.sh/wiki/en/Client/Beatmap_editor/Design) ·
[SB load](https://osu.ppy.sh/wiki/en/Client/Beatmap_editor/SB_load) ·
[Song setup](https://osu.ppy.sh/wiki/en/Client/Beatmap_editor/Song_setup) ·
[.osu (file format)](https://osu.ppy.sh/wiki/en/Client/File_formats/osu_%28file_format%29) ·
[.osb (file format)](https://osu.ppy.sh/wiki/en/Client/File_formats/osb_%28file_format%29) ·
[osu! file formats](https://osu.ppy.sh/wiki/en/Client/File_formats) ·
[Beatmap/Background](https://osu.ppy.sh/wiki/en/Beatmap/Background) ·
[Ranking criteria](https://osu.ppy.sh/wiki/en/Ranking_criteria) ·
[Guides/Compressing files](https://osu.ppy.sh/wiki/en/Guides/Compressing_files)

**Official client source** (`ppy/osu`, `master`):
[`Storyboards/Storyboard.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Storyboard.cs) ·
[`Storyboards/StoryboardSprite.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/StoryboardSprite.cs) ·
[`Storyboards/StoryboardAnimation.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/StoryboardAnimation.cs) ·
[`Storyboards/StoryboardVideo.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/StoryboardVideo.cs) ·
[`Storyboards/StoryboardLayer.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/StoryboardLayer.cs) ·
[`Storyboards/StoryboardVideoLayer.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/StoryboardVideoLayer.cs) ·
[`Storyboards/Commands/StoryboardCommand.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Commands/StoryboardCommand.cs) ·
[`Storyboards/Commands/StoryboardCommandGroup.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Commands/StoryboardCommandGroup.cs) ·
[`Storyboards/Commands/StoryboardAlphaCommand.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Commands/StoryboardAlphaCommand.cs) ·
[`Storyboards/Commands/StoryboardLoopingGroup.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Commands/StoryboardLoopingGroup.cs) ·
[`Storyboards/Commands/StoryboardTriggerGroup.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Commands/StoryboardTriggerGroup.cs) ·
[`Storyboards/Drawables/StoryboardTriggerController.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/StoryboardTriggerController.cs) ·
[`Storyboards/Drawables/DrawableStoryboardSprite.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Storyboards/Drawables/DrawableStoryboardSprite.cs) ·
[`Beatmaps/Formats/LegacyStoryboardDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyStoryboardDecoder.cs) ·
[`Beatmaps/Formats/LegacyBeatmapDecoder.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Formats/LegacyBeatmapDecoder.cs) ·
[`Beatmaps/Legacy/LegacyEventType.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Legacy/LegacyEventType.cs) ·
[`Beatmaps/Legacy/LegacyStoryLayer.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Legacy/LegacyStoryLayer.cs) ·
[`Beatmaps/Legacy/LegacyOrigins.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Beatmaps/Legacy/LegacyOrigins.cs) ·
[`Utils/SupportedExtensions.cs`](https://github.com/ppy/osu/blob/master/osu.Game/Utils/SupportedExtensions.cs) ·
[PR #38335](https://github.com/ppy/osu/pull/38335)

**Community (labelled as such):**
[\[Guide\] SGL & Optimization](https://osu.ppy.sh/community/forums/topics/378938) ·
storybrew (named by the official wiki as "Damnae's storybrew")

**Local artefacts produced:** `research/_raw/*.html` (raw wiki HTML, including `osu.ppy.sh/wiki/en/...` pages), `research/_raw/*.md` (converted article Markdown), `research/_raw/osu-src/*.cs` (retrieved client sources), `research/_raw/convert.ps1` (HTML→Markdown converter), `research/_raw/fetch.ps1`, `research/_raw/fetch2.ps1`, `research/_raw/gh-fetch.ps1`, `research/_raw/gh-fetch2.ps1`.
