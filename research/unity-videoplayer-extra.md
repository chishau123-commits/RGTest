# Unity VideoPlayer — deep-dive technical research (primary sources)

Research date: fetched against **Unity 6.6 (6000.6)** unversioned docs unless a version is stated.
All quotes are verbatim from the cited page. Anything I could not confirm from a Unity-owned primary source is marked **未验证**.

Primary sources used:

| Source class | Where |
|---|---|
| Unity Manual | `https://docs.unity3d.com/Manual/*.html` (redirects to `/6000.6/Documentation/Manual/`) |
| Unity Scripting API | `https://docs.unity3d.com/ScriptReference/*.html` |
| Unity C# reference source (official repo) | `https://github.com/Unity-Technologies/UnityCsReference` (fetched via jsDelivr mirror of `master`) |
| Older pinned manual/scriptref | `https://docs.unity3d.com/2019.4/Documentation/...`, `https://docs.unity3d.com/2022.3/Documentation/...` |

C# reference files inspected (downloaded and grepped locally; `master` branch):

* `Modules/Video/Public/ScriptBindings/VideoPlayer.bindings.cs`
* `Modules/Video/Public/ScriptBindings/VideoClip.bindings.cs`
* `Modules/VideoEditor/Editor/VideoPlayerEditor.cs` (the VideoPlayer custom inspector)
* `Modules/VideoEditor/VideoClipImporterInspector.cs` (the Video Clip Importer custom inspector)
* `Modules/VideoEditor/VideoImporter.bindings.cs` (importer enums + `VideoImporterTargetSettings`)

---

## 0. Two corrections to the brief (important)

**0.1 `VideoPlayer-intro.html` is NOT a platform-limitations page any more.**
In Unity 6.6 the URL `https://docs.unity3d.com/Manual/VideoPlayer-intro.html` renders **"Video Player component targets"** — it documents `Render Mode` targets (camera planes / material / render texture / API Only), not per-platform codec limits. It was already that in 2022.3.

> "Use the Video Player component to play videos (from a URL or video clips) on various surfaces in your scene."
> — https://docs.unity3d.com/Manual/VideoPlayer-intro.html (Unity 6.6) and https://docs.unity3d.com/2022.3/Documentation/Manual/VideoPlayer-intro.html (2022.3)

The current per-platform page is `video-sources-compatibility-target-platforms.html`, and it is now **thin** — it only points at vendor docs:

> "The Video Player component uses the native audio and video decoding libraries of your Unity Editor platform to play video files in the Editor. You need to check that the files meet the system requirements for the target build platform."
> "Note: On older mobile platforms, codec choices are limited. You might need to inspect and convert or re-encode videos that you intend to include in an application running on multiple devices."
> — https://docs.unity3d.com/Manual/video-sources-compatibility-target-platforms.html

Recommendations it gives are external vendor links only: Windows *Supported Media Formats*, H.265; UWP *Supported Codecs*; Android *Supported Media Formats*; iOS *Compare iPhone Models*.

**0.2 The substantive per-platform notes have moved into the Scripting API page.**
The old "Movie File Format Support Notes" block (with the Android notes) used to live in the Manual/ScriptReference. It is present in **2019.4** and gone by **2022.3** (2022.3 replaces it with "Refer to Video file compatibility"). Use the 2019.4 page for the historical platform colour:

> **Movie File Format Support Notes**
> "The VideoPlayer uses native audio and video decoding libraries. It is your responsibility to use videos that match the requirements for the target platform. The VideoClipImporter offers an option to transcode the VideoClip assets into one of **H.264**, **H.265** or **VP8** video codecs, along with a few options to experiment with, such as resolution. This uses the matching codec for audio tracks: AAC and Vorbis respectively."
> — https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Video.VideoPlayer.html

> "**Android Notes**
> * Support for resolutions above 640 x 360 is not available on all devices. Runtime checks are done to verify this and failures will cause the movie to not be played.
> * For Jelly Bean/MR1, movies above 1280 x 720 or with more than 2 audio tracks will not be played due to bugs in the OS libraries.
> * For Lollipop and above, any resolution or number of audio channels may be attempted, but will be constrained by device capabilities.
> * The Vulkan graphics API is supported in Unity 2019.1 and later.
> * **Format compatibility issues are reported in the adb logcat output and are always prefixed with `AndroidVideoMedia`.**
> * Also pay attention to device-specific error messages located near Unity's error messages: they are not available to the engine, but often explain what the compatibility issue is.
> * Playback from asset bundles is only supported for uncompressed bundles, read directly from disk.
> * When targetting Android 9 or newer, and playing over HTTP, the `usesCleartextTraffic` attribute must be added to your Android manifest; or alternatively, cleartext traffic to specific domains must be enabled in your network security config file."
> — https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Video.VideoPlayer.html

(The `AndroidVideoMedia` prefix is the strongest official hint that Unity's Android video path is a Unity-owned JNI/native layer, not a documented third-party player. See §1.4.)

---

## 1. Hardware decoding

### 1.1 Is there a "hardware decoding" toggle? — **No. Definitively no.**

I checked the complete option surface from four independent primary sources. **There is no hardware-decoding checkbox, toggle, or boolean anywhere in the VideoPlayer component, the Video Clip Importer, or their scripting APIs.**

| Where | Evidence | Result |
|---|---|---|
| VideoPlayer inspector (Manual, full property table) | https://docs.unity3d.com/Manual/class-VideoPlayer.html — properties are: Source, Video Clip, URL, Update Mode, Play On Awake, Wait For First Frame, Loop, Skip On Drop, Playback Speed, Render Mode, Camera, Alpha, 3D Layout, Target Texture, Aspect Ratio, Renderer, Auto-Select Property, Audio Output Mode, Controlled Tracks, Track Number, Audio Source, Mute, Volume | **no hardware toggle** |
| Video Clip Importer (Manual, full property table) | https://docs.unity3d.com/Manual/class-VideoClip.html — sRGB (Color Texture), Transcode, Dimensions, Codec, Bitrate Mode, Spatial Quality, Keep Alpha, Deinterlace, Flip Horizontally, Flip Vertically, Import Audio | **no hardware toggle** |
| `UnityEditor.VideoClipImporter` API (all properties) | https://docs.unity3d.com/ScriptReference/VideoClipImporter.html | no `hardware*` member |
| `UnityEditor.VideoImporterTargetSettings` API (all fields) | https://docs.unity3d.com/ScriptReference/VideoImporterTargetSettings.html — aspectRatio, bitrateMode, codec, customHeight, customWidth, enableTranscoding, resizeMode, spatialQuality | **no hardware field** |
| C# reference: VideoPlayer inspector source | `Modules/VideoEditor/Editor/VideoPlayerEditor.cs` (all `L10n.TextContent` labels in `class Styles`) | no "hardware" string anywhere in the file |
| C# reference: Video Clip Importer inspector source | `Modules/VideoEditor/VideoClipImporterInspector.cs` (all `TrTextContent` labels in `class Styles`) | no "hardware" string anywhere in the file |

The `VideoCodec` enum in Unity's public editor API has exactly four members, and no hardware/software axis:

```csharp
// Modules/VideoEditor/VideoImporter.bindings.cs  (UnityCsReference, master)
public enum VideoCodec
{
    Auto = 0,
    H264 = 1,
    H265 = 3,
    VP8  = 2,
}
```
Source: https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/VideoEditor/VideoImporter.bindings.cs

A case-insensitive grep for `hardware|exoplayer|mediacodec` across all five C# files above returned **only four hits, all of them about *audio* hardware**:

```
VideoPlayer.bindings.cs:298: ///<summary>The audio hardware clock.</summary>
VideoPlayer.bindings.cs:459: ///<remarks>Use this enum to mute your audio, output your audio through Unity's audio system, or output the audio directly to the audio hardware.</remarks>
VideoPlayer.bindings.cs:566: ///<summary>Send the embedded audio direct to the platform's audio hardware.</summary>
VideoPlayer.bindings.cs:567: ///<remarks>The VideoPlayer bypasses Unity's AudioSource and plays the video with its original audio directly to audio hardware (speakers, headphones etc.). Useful if you don't want to alter the audio or you want to save resources.</remarks>
```

**Trap to avoid:** the VideoPlayer inspector *does* contain a string containing the word "decoding", but it is the **audio track enable** checkbox, not a hardware toggle:

```csharp
// Modules/VideoEditor/Editor/VideoPlayerEditor.cs, line 101
public readonly string enableDecodingTooltip =
    "Enable decoding for this track.  Only effective when not playing.  When playing from a URL, track details are shown only while playing back.";
```
Source: https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/VideoEditor/Editor/VideoPlayerEditor.cs

### 1.2 What Unity actually says about hardware vs software decoding

> "Unity's video features include the **Hardware-accelerated and software-based decoding** of video sources, Transparency support, multiple Audio sources, and Network streaming."
> — https://docs.unity3d.com/Manual/Video.html

> "**Decoding**
> Unity provides both software and hardware decoding.
> Most modern devices have hardware dedicated to decoding videos. This hardware typically requires less power than other resources like the CPU, so these resources can be used for tasks other than decoding videos. This hardware acceleration uses native custom APIs, which differ between platforms. Unity's video architecture hides these differences by providing a common UI and scripting API to access these capabilities.
> Unity is also capable of software-based video decoding. This uses the **VP8 video codec and Vorbis audio codec**, and is useful for situations where a platform's hardware decoding results in unwanted restrictions with resolution, multiple audio tracks, or alpha transparency support."
> — https://docs.unity3d.com/Manual/VideoSources-VideoFiles.html (Unity 6.6)

The 2022.3 edition of the same section is titled **"Hardware and software decoding"** and says the same thing with one extra clause:

> "This hardware acceleration is made possible by native custom APIs, which vary from platform to platform. Unity's video architecture hides these differences by providing a common UI and Scripting API in order to access these capabilities."
> "Unity is also capable of software-based video decoding. This uses the VP8 video codec and Vorbis audio codec, and is useful for situations where a platform's hardware decoding results in unwanted restrictions in terms of resolution, the presence of multiple audio tracks, or support of alpha channel."
> — https://docs.unity3d.com/2022.3/Documentation/Manual/VideoSources-VideoFiles.html
> (page footer: "2017-06-15 Page published — New feature in Unity 5.6")

**Key takeaway: hardware vs software decode is a property of the *codec + platform*, not a user-selectable switch.** The user-facing lever is *which codec you transcode to*.

### 1.3 Per-codec hardware/software table (Unity's own words)

From the Video Clip Importer **Codec options** table:

| Codec | Verbatim description | Source |
|---|---|---|
| **Auto** | "Choose the best video codec for the target platform automatically." | https://docs.unity3d.com/Manual/class-VideoClip.html |
| **H264** | "Choose the MPEG–4 Advanced Video Coding (AVC) video codec, **supported by hardware on most platforms**." | ibid. |
| **H265** | "Choose the MPEG-H Part 2, or High Efficiency Video Coding (HEVC), video codec, **supported by hardware on some platforms**." | ibid. |
| **VP8** | "Choose the VP8 video codec, **supported by software on most platforms, and by hardware on Android and Web**." | ibid. |

From the **Video encoding compatibility** reference:

| Codec | Advantages (verbatim) | Disadvantages (verbatim) |
|---|---|---|
| H.264 | "Best natively supported codec for hardware acceleration." | *(blank)* |
| H.265 | "Higher compression." | "Limited to supported devices." |
| VP8 | "Cross-platform support and comprehensive feature set." | "Consumes more resources compared to H.264." |

> "**Note:** Android supports VP8 using native libraries, so VP8 might be hardware-assisted on some Android devices."
> — https://docs.unity3d.com/Manual/video-encoding-compatibility.html

Historical formulation (2019.4 ScriptReference, more explicit):

> "**The best natively supported video codec for hardware acceleration is H.264, with VP8 being a software decoding solution that can be used when required. On Android, VP8 is also supported using native libraries and as such may also be hardware-assisted depending on models. H.265 is also available for hardware acceleration where the device supports it.**"
> — https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Video.VideoPlayer.html

### 1.4 Android: MediaCodec / ExoPlayer — **未验证**

I found **no official Unity statement** that the VideoPlayer uses Android `MediaCodec` or `ExoPlayer`. What I can state from primary sources:

| Claim | Status | Evidence |
|---|---|---|
| Unity's C# API/runtime layer exposes no ExoPlayer or MediaCodec type | verified (negative) | grep of the five UnityCsReference video files above: zero matches for `exoplayer` / `mediacodec` |
| Unity's Android video errors are prefixed `AndroidVideoMedia` in logcat | verified | https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Video.VideoPlayer.html |
| `https://docs.unity3d.com/Manual/android-video.html` **does not exist** | verified (404) | Probed on 6.6, 6000.0, 2022.3, 2020.3, 2019.4 — all return HTTP 404. No Android-specific video page exists in the current Manual TOC either. |
| Unity uses ExoPlayer on Android | **未验证** | Only third-party sources surfaced (AVPro Video issue trackers, conference decks). No Unity-owned doc, blog, or release note found. Do not assert this. |
| Android codec/resolution limits | verified (2019.4 only) | The "Android Notes" block quoted in §0.2. Unity 6.6 no longer documents these; it defers to Android's *Supported Media Formats*. |

`https://docs.unity3d.com/Manual/VideoSources-VideoFiles.html` contains **no codec feature table** — only the prose in §1.2. The only codec tables in the current Manual are in `VideoSources-FileCompatibility.html`, `video-encoding-compatibility.html`, and `VideoTransparency-codecs.html` (all transcribed below / in §4, §7).

---

## 2. Seeking API — verbatim transcriptions

All from `https://docs.unity3d.com/ScriptReference/...` (Unity 6.6, 6000.6).

### `VideoPlayer.frame` — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-frame.html

```csharp
public long frame;
```
> **The frame index of the currently available frame in VideoPlayer.texture.**
> The frame index is 0 for the first frame of the clip, 1 for the second frame, and so on. A frame index of -1 indicates that no valid frame is available.
> **Note:** On WebGL, because the frame rate is not known, the frame index assumes a rate of 24FPS. See VideoPlayer.frameRate.

### `VideoPlayer.frameReady` — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-frameReady.html

Parameter: `value` — "The number of the frame that is ready (zero-based index)."

> **The VideoPlayer invokes this event when a new frame is ready to be displayed.**
> Use this event to:
> * Analyze certain frames of a video.
> * Track progress of the video.
> * Play other effects such as animations or sound effects at a certain frame.
> To enable this event so that the VideoPlayer emits it, set the `VideoPlayer.sendFrameReadyEvents` property to `true`. **This event is likely to tax the CPU, so set `VideoPlayer.sendFrameReadyEvents` back to `false` when you don't need it.**
> The VideoPlayer also emits this event if you call `VideoPlayer.Pause` on a VideoPlayer that's not prepared yet or isn't currently playing. When you call `Pause()` on a VideoPlayer that's not prepared or playing, it behaves as if you called `Play()` and then immediately called `Pause()`. **This allows you to seek to a certain point in the video, and pause to give it time to prepare the frame before you play it.**

### `VideoPlayer.StepForward` — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer.StepForward.html

```csharp
public void StepForward();
```
> **Immediately advance the current time by one frame.**
> If the video is currently playing, this method will pause the video before it advances to the next frame. However, if the VideoPlayer isn't prepared, this method will trigger preparation and display the first frame, but will not skip to the next frame. It steps forward from non-initialized to frame 0.
> This method is useful if you want to:
> * Analyze each frame of a video.
> * Debug issues related to the video or elements that play at certain frames.
> * Take finer control over the playback speed, because you can choose exactly when the next frame will appear. **However, the WebGL implementation is unable to provide frame-accurate control due to platform limitations.**

### `VideoPlayer.canStep` — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-canStep.html

```csharp
public bool canStep;
```
> **Returns true if the VideoPlayer can step forward through the video content. (Read Only)**
> Stepping is done with `Video.VideoPlayer.StepForwards`.
> **This value is only valid after the movie has been prepared.** See `VideoPlayer.Prepare`.

(Note: the page literally says `StepForwards`; the real method is `StepForward`.)

### `VideoTimeReference` (enum) — https://docs.unity3d.com/ScriptReference/Video.VideoTimeReference.html

> **Description:** "The clock that the VideoPlayer observes to detect and correct drift."
> "Use these settings to control how the playback time of the video is synchronized with time sources."

| Enum value | Verbatim description |
|---|---|
| `Freerun` | "The video plays without influence from external time sources." |
| `InternalTime` | "The internal reference clock the VideoPlayer observes to detect and correct drift." |
| `ExternalTime` | "The external reference clock the VideoPlayer observes to detect and correct drift." |

Official example on that page shows the external-clock pattern: set `videoPlayer.timeReference = VideoTimeReference.ExternalTime;`, then each frame `videoPlayer.externalReferenceTime = customExternalTime;`.

### `VideoPlayer.clockTime` — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-clockTime.html

```csharp
public double clockTime;
```
> **The clock time that the VideoPlayer follows to schedule its samples. The clock time is expressed in seconds. (Read Only)**

### `VideoPlayer.externalReferenceTime` — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-externalReferenceTime.html

```csharp
public double externalReferenceTime;
```
> **Reference time of the external clock the VideoPlayer uses to correct its drift.**
> Only relevant when `VideoPlayer.timeReference` is set to `VideoTimeReference.ExternalTime`.

### `VideoPlayer.canSetSkipOnDrop` — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-canSetSkipOnDrop.html

```csharp
public bool canSetSkipOnDrop;
```
> **Whether frame-skipping to maintain synchronization can be controlled. (Read Only)**
> When `true`, the value of `VideoPlayer.skipOnDrop` can be changed. When `false`, the value cannot be changed. **This value is only valid after the movie has been prepared.** See `VideoPlayer.Prepare`.

### `VideoPlayer.canSetTime` — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-canSetTime.html

```csharp
public bool canSetTime;
```
> **Whether you can change the current time using the `VideoPlayer.time` or `VideoPlayer.frame` properties. (Read Only)**
> **Seeking is not supported in all contexts. For example, seeking in a HTTP live stream.**
> **This value is only valid after the movie has been prepared.** See `VideoPlayer.Prepare`.

### `VideoPlayer.timeUpdateMode` — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-timeUpdateMode.html

```csharp
public VideoTimeUpdateMode timeUpdateMode;
```
> **The clock source used by the VideoPlayer to derive its current time.**

`VideoTimeUpdateMode` enum — https://docs.unity3d.com/ScriptReference/Video.VideoTimeUpdateMode.html

> "Defines the time source the VideoPlayer uses to update the timing of the video playback. Use these settings to synchronize your video with audio, gameplay scaled time or unscaled time."

| Enum value | Verbatim description |
|---|---|
| `DSPTime` | "Update time based on the DSP (Digital Signal Processing) clock. Use this value to synchronize playback with Audio." |
| `GameTime` | "Update the VideoPlayer's time based on `Time.time`." |
| `UnscaledGameTime` | "Update the VideoPlayer's time based on `Time.unscaledTime`." |

### `VideoPlayer.frameDropped` — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-frameDropped.html

> **Invoked when the video decoder does not produce a frame as per the time source during playback.**

### `VideoPlayer.seekCompleted` — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-seekCompleted.html

**This is the single most important official statement about seek cost and encoding:**

> **Invoke after a seek operation completes.**
> Seek operations are done by changing the time or timeFrames property. **Seek duration may be noticeably long depending on the codec performance and the parameters chosen at encoding time.**

### `VideoPlayer.skipOnDrop` — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-skipOnDrop.html

```csharp
public bool skipOnDrop;
```
> **Whether the VideoPlayer is allowed to skip frames to catch up with current time.**
> Only settable if `VideoPlayer.canSetSkipOnDrop` is `true`.

### `VideoPlayer.timeReference` — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-timeReference.html

```csharp
public VideoTimeReference timeReference;
```
> **The clock that the VideoPlayer observes to detect and correct drift.**

### Additional seek semantics from the C# reference (`VideoPlayer.bindings.cs`, master)

Remarks on `VideoPlayer.time` (source: https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/Video/Public/ScriptBindings/VideoPlayer.bindings.cs):

> "When you set `VideoPlayer.time`, it initiates a seek operation. For example, if you set `VideoPlayer.time = 10`, the VideoPlayer: … 2. Fires the `VideoPlayer.seekCompleted` event when it reaches 10 seconds."
> "If you set time to another value during this operation, the VideoPlayer creates a new seek operation and adds it to a queue. The new operation will start when the previous one completes."

Remarks on `Pause()`:

> "If you seek through to a different point in the video and then call `Pause()` before the VideoPlayer finishes preparation, it triggers preparation and shows the frame that was the seek target. For example, if you set `VideoPlayer.time` to `/10.0f/` and then call `Pause()`, it shows the frame at 10 seconds."

### Inspector-level equivalents (Manual)

| Property | Verbatim description | Source |
|---|---|---|
| **Skip On Drop** | "When you enable this option, and the Video Player component detects drift between the playback position and the game clock, the Video Player skips ahead. When you disable this option, the Video Player doesn't correct for drift and systematically plays all frames." | https://docs.unity3d.com/Manual/class-VideoPlayer.html |
| **Wait For First Frame** | "Wait for the first frame of the source video to be ready for display before playback starts. Clear it to keep the video time in sync with the rest of the game, which might cause the first few frames to be discarded." | ibid. |
| **Update Mode → DSP Time** | "Use the same clock source that processes audio." | ibid. |
| **Update Mode → Game Time** | "Use the same clock source as the game clock. This clock source is affected by the time scaling and capture frame rate settings." | ibid. |
| **Update Mode → Unscaled Game Time** | "Use the same clock source as the game clock but this clock source isn't affected by time scaling or capture frame rate." | ibid. |

---

## 3. HAP / intra-frame / all-intra codecs — **未验证 (no official Unity statement exists)**

**Result: no official Unity documentation, blog, or C# reference source mentions HAP. I could not find any official Unity recommendation to use all-intra / intra-frame encoding for seeking.**

Evidence for the negative:

| Check | Result |
|---|---|
| `VideoCodec` enum in the official C# reference | Only `Auto`, `H264`, `H265`, `VP8`. No HAP, no ProRes-as-codec. Source: https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/VideoEditor/VideoImporter.bindings.cs |
| Case-insensitive grep for `HAP`/`Hap` in the five official video C# files | 7 hits, **all false positives** — the substring "happen" inside comments (`what you want to happen`, `after loop happens`). Zero real HAP codec references. |
| Transcodable codecs per Manual | H.264, H.265, VP8 only — see the Codec options table in §1.3 and https://docs.unity3d.com/Manual/class-VideoClip.html |
| WebM/StreamingAssets codec unlock table | Adds VP9 + Vorbis/Opus only — https://docs.unity3d.com/Manual/VideoSources-FileCompatibility.html |
| Transparent-video codecs | Apple ProRes 4444, WebM/VP8 — https://docs.unity3d.com/Manual/VideoTransparency-codecs.html |
| Per-pixel-alpha *input* codecs are the only "extra" codecs Unity accepts | ProRes 4444 and VP8 (§7) |

**The closest thing to official guidance on encoding-for-seeking** is the `seekCompleted` remark:

> "Seek duration may be noticeably long depending on the **codec performance and the parameters chosen at encoding time**."
> — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-seekCompleted.html

Unity's "Key encoding values" table (`https://docs.unity3d.com/Manual/video-encoding-compatibility.html`) lists **Video Codec, Resolution, Profile, Profile Level, Audio Codec, Audio Channels** — it does **not** mention GOP length, keyframe interval, or intra-frame encoding. So the specific claim "Unity recommends all-intra for seeking" is **未验证**, and the specific claim "Unity supports HAP" is **unsupported by any Unity source found**.

---

## 4. Transcoding

### 4.1 `Transcode` and the transcode options (Video Clip Importer reference)

Source: https://docs.unity3d.com/Manual/class-VideoClip.html

> Page preamble: "Configure the settings of the Video Clip Importer to transcode video files into different formats, ensuring compatibility with your target platforms."
> "**Note:** The transcoding process can result in longer build times and lower video quality."

Base settings table:

| Property | Verbatim description |
|---|---|
| **sRGB (Color Texture)** | "Choose whether the VideoPlayer converts sRGB to Linear color space when it loads the video data into textures. Unity enables this setting by default because most video clips store color data in sRGB color space. For non-color video clips, disable this setting to avoid unnecessary conversions." |
| **Transcode** | "Transcode the source content into a format that's compatible with the target platform. **Note:** Verify that the source format is compatible with each target platform." |

Transcode sub-options (shown only after enabling **Transcode**):

| Property | Verbatim description |
|---|---|
| **Dimensions** | "Control how Unity resizes the source content in pixels. Aspect ratio is controllable for all options." |
| **Codec** | "Choose the codec to encode the video track." |
| **Bitrate Mode** | "How much data a video uses per second. Choose a bitrate relative to the chosen codec's baseline profile. **Note:** Higher bitrates provide a higher quality video, but impose a higher load on network connections or storage." |
| **Spatial Quality** | "How sharp and detailed the image looks within each video frame. **Note:** Resizing images uses less storage but also results in blurriness during playback." |
| **Keep Alpha** | "**Preserve the alpha channel and encode it during transcoding so you can use the video even if the target platform doesn't natively support videos with transparency.** **Note:** This property is only visible for sources that have an alpha channel." |
| **Deinterlace** | "Interlaced videos have two time samples in each frame… Deinterlacing converts interlaced video into progressive frames for smoother display." |
| **Flip Horizontally** | "Flip the source content along a horizontal axis when transcoding." |
| **Flip Vertically** | "Flip the source content along a vertical axis when transcoding." |
| **Import Audio** | "Import the audio tracks when transcoding. **Note:** This property is available only for sources that have audio tracks." |

Dimensions options: Original Size / Three Quarter Res / Half Res / Quarter Res / Square 1024 / Square 512 / Square 256 / Custom Size.
Bitrate Mode options: Low / Medium / High.
Deinterlace options: Off ("The source isn't interlaced and there's no processing to perform. This is the default setting."), Even, Odd.
Spatial Quality: Low ("typically to a quarter of its original dimensions… largest amount of blurriness"), Medium ("typically to half"), High ("Keep the image's original size").

### 4.2 Transcoding steps

Source: https://docs.unity3d.com/Manual/video-transcode-steps.html

> "To enable transcoding for your video files, you need to change the settings in the Video Clip Importer settings. Follow these steps:
> 1. Select your video clip asset. The Inspector window shows the **Video Clip Import Settings**.
> 2. Enable **Transcode**.
> 3. Configure the transcoding settings in the Video Clip Import Settings.
> 4. Once you're satisfied with your settings, select **Apply**.
> Unity transcodes your video file with your new settings."

### 4.3 Additional warnings from the inspector source (verbatim UI strings)

Source: `Modules/VideoEditor/VideoClipImporterInspector.cs` — https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/VideoEditor/VideoClipImporterInspector.cs

| String | Text |
|---|---|
| `transcodeContent` tooltip | "Transcoding a clip gives more flexibility through the options below, but takes more time." |
| `transcodeWarning` | "Not all platforms transcoded. Clip is not guaranteed to be compatible on platforms without transcoding." |
| `transcodeOptionsWarning` | "Global transcode options are not applied on all platforms. You must enable \"Transcode\" for these to take effect." |
| `transcodeSkippedWarning` | "Transcode was skipped. Current clip does not match import settings. Reimport to resolve." |
| `globalTranscodeOptionsContent` | "* Shared setting between multiple platforms." |
| `keepAlphaContent` tooltip | "If the source clip has alpha, it will be preserved during transcoding so that transparency is usable during render." |

`VideoClipImporter.transcodeSkipped` carries a strong warning about transcode cost:

> "When `VideoImporterTargetSettings.enableTranscoding` is set to true, the resulting transcoding operation done at import time may be **quite long, up to many hours** depending on source resolution and content duration. An option to skip this process is offered in the asset import progress bar…"
> — https://docs.unity3d.com/ScriptReference/VideoClipImporter.html

### 4.4 When *not* to transcode

> "If you use video files that the target platform supports, you can manage encoding with an external program for finer control. You can disable the Transcode property in the Video Clip Importer so Unity doesn't modify the video files."
> — https://docs.unity3d.com/Manual/video-encoding-compatibility.html

---

## 5. Audio

### 5.1 Does VideoPlayer audio go through Unity's audio pipeline? — **It depends on Audio Output Mode; "Direct" explicitly bypasses it.**

**Manual, Audio Output Mode dropdown** (https://docs.unity3d.com/Manual/class-VideoPlayer.html):

| Value | Verbatim description |
|---|---|
| **None** | "Audio isn't played." |
| **Audio Source** | "**Audio samples are sent to selected audio sources, enabling Unity's audio processing to be applied.**" |
| **Direct** | "**Audio samples are sent directly to the audio output hardware, bypassing Unity's audio processing.**" |
| **API Only (Experimental)** | "Audio samples are sent to the associated `AudioSampleProvider`." |

**Scripting API enum** `VideoAudioOutputMode` (https://docs.unity3d.com/ScriptReference/Video.VideoAudioOutputMode.html):

> "Places where the audio embedded in a video can be sent.
> Use this enum to mute your audio, output your audio through Unity's audio system, or output the audio directly to the audio hardware."

| Enum value | Verbatim description |
|---|---|
| `None` | "Disable the embedded audio." |
| `AudioSource` | "Send the embedded audio into a specified AudioSource." |
| `Direct` | "Send the embedded audio direct to the platform's audio hardware." |
| `APIOnly` | "Send the embedded audio to the associated AudioSampleProvider." *(page truncated mid-word in the fetched rendering)* |

**`VideoPlayer.audioOutputMode`** — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-audioOutputMode.html

```csharp
public VideoAudioOutputMode audioOutputMode;
```
> "Destination for the audio embedded in the video.
> **Note: WebGL only fully supports `VideoAudioOutputMode.None` and `VideoAudioOutputMode.Direct`. If you set the output mode to `VideoAudioOutputMode.AudioSource`, Unity ignores all AudioSource fields except mute. This is because 3D spatialization of video playback is not available on the web.**"

**C# reference remark for `Direct`** (`VideoPlayer.bindings.cs`, master — this is the clearest statement of the bypass):

> "The VideoPlayer bypasses Unity's AudioSource and plays the video with its original audio directly to audio hardware (speakers, headphones etc.). Useful if you don't want to alter the audio or you want to save resources."
> — https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/Video/Public/ScriptBindings/VideoPlayer.bindings.cs

**Manual notes on the Audio Source / Mute / Volume interplay** (https://docs.unity3d.com/Manual/class-VideoPlayer.html):

> **Audio Source** — "The audio source through which the audio track plays. The targeted audio source can also play Audio Clips. **The audio source's playback controls (Play On Awake and Play() in scripting API) don't apply to the video source's audio track.** This property only appears when the Audio Output Mode is set to Audio Source."
> **Mute** — "Mute the associated audio track. In Audio Source mode, the audio source's control is used. **This property is available only when Audio Output Mode is Direct.**"
> **Volume** — "Volume of the associated audio track. In Audio Source mode, the audio source's volume is used. **This property is available only when Audio Output Mode is Direct.**"

### 5.2 Is `AudioSettings.dspTime` affected?

**未验证 — no official Unity statement found addressing whether VideoPlayer audio clocks/advances `AudioSettings.dspTime` in "Direct" mode.**

What *is* official and relevant (`https://docs.unity3d.com/ScriptReference/AudioSettings-dspTime.html`):

> "Returns the current time of the audio system."
> "This is a value specified in seconds and based on the actual number of samples the audio system processes and is therefore much more precise than the time obtained via the `Time.time` property."

And the coupling Unity *does* document is the other direction: `VideoTimeUpdateMode.DSPTime` = "Update time based on the DSP (Digital Signal Processing) clock. Use this value to synchronize playback with Audio." (https://docs.unity3d.com/ScriptReference/Video.VideoTimeUpdateMode.html), and the Manual's "Update Mode → DSP Time" = "Use the same clock source that processes audio." (https://docs.unity3d.com/Manual/class-VideoPlayer.html). Also `VideoPlayer.bindings.cs` line 298 documents a `clockTime`-related enum member as "The audio hardware clock."

### 5.3 Does the manual recommend muting the video and using a separate AudioSource?

**未验证 — I found no official Unity recommendation to mute the video track and drive audio from a separate `AudioSource`.** Unity documents the *capability* (Audio Output Mode = Audio Source routes samples into a chosen AudioSource, §5.1) but I found no prescriptive guidance.

The closest official pattern is the `frameReady` code sample, which uses a separate `AudioSource.Play()` to trigger a sound at a target frame — i.e. audio *effects* layered over the video, not a replacement for the video's own audio track:

```csharp
void OnFrameReady(VideoPlayer vp, long frameToPlay)
{
    if (frameToPlay == targetFrame) { audioSource.Play(); videoPlayer.sendFrameReadyEvents = false; }
}
```
> — https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-frameReady.html

---

## 6. WebGL / Web

Source: **"Video playback in Web"** — https://docs.unity3d.com/Manual/webgl-video.html

> "Unity Web supports video playback using the VideoPlayer API."

**VideoClips are NOT supported on Web — URL source only:**

> "**VideoClips aren't supported on Web.** Typically, when creating a scene, you import a VideoClip to your Unity project using VideoClipImporter, which is convenient if you want to reuse the same VideoClip across several platforms. When building a Web game that has VideoClip attached however, the Unity console logs the following warning for each VideoClip found in the game:
> `Embedded video clips are not supported by the Web player: %s. \nUse the Video Player component's URL option instead`
> Where `%s` is replaced by the video clip name. At runtime, if your game has VideoClips assigned, then Unity logs a warning message in the developer console of your web browser."

Corroborated by the component reference: **Video Clip** property — "Select the Video Clip to assign to the Video Player component. **This isn't supported on the Web platform.**" (https://docs.unity3d.com/Manual/class-VideoPlayer.html)

**Supported formats on Web** (only these extensions; the browser enforces codecs):

| Format | Extensions |
|---|---|
| MPEG–4 Part 14 | `.mp4` |
| MPEG–4 file used for video downloaded from the Apple iTunes store | `.m4v` |
| Apple's QuickTime movie format | `.mov` |
| Moving Picture Experts Group (MPEG) | `.mpg` |
| MPEG video | `.mpeg` |
| WebM video | `.webm` |
| Ogg video file | `.ogv` |

> "**The only exception to this restriction is if the video URL has no file name extension, in which case, the browser plays the video without any restrictions.**"

**Audio modes on Web:** `VideoAudioOutputMode.None`, `.Direct`, and `.AudioSource` (with all AudioSource fields except `mute` ignored — "3D spatialization of video playback isn't available on the web").

**Frame accuracy / timing limits on Web (verbatim):**

> "The only exceptions are:
> * **Web doesn't support frame accuracy.**
> * The VideoPlayer component doesn't support synchronous playback with `captureFramerate`. By default, it uses the normal asynchronous playback that's described with the Game time update mode.
> * The VideoPlayer component corrects drift between video playback and Unity time by temporarily speeding the playback controls to up or down. **However, because the video support in Safari browser has limitations that prevent this mechanism from operating with precision, the drift correction is disabled.**"

Cross-references confirming Web limits:
* `VideoPlayer.frame` — "On WebGL, because the frame rate is not known, the frame index assumes a rate of 24FPS." (https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-frame.html)
* `VideoPlayer.StepForward` — "the WebGL implementation is unable to provide frame-accurate control due to platform limitations." (https://docs.unity3d.com/ScriptReference/Video.VideoPlayer.StepForward.html)
* `VideoPlayer.audioOutputMode` WebGL note (quoted in §5.1).

---

## 7. iOS / macOS (and the codec picture generally)

**No dedicated iOS or macOS video page exists in the current Manual.** What Unity officially documents:

### 7.1 Editor-platform import formats (`VideoSources-FileCompatibility.html`)

| Extension | Windows | macOS | Linux |
|---|---|---|---|
| `.asf` | X | | |
| `.avi` | X | | |
| `.dv` | X | X | |
| `.m4v` | X | X | |
| `.mov` | X | X | |
| `.mp4` | X | X | |
| `.mpg` | X | X | |
| `.mpeg` | X | X | |
| `.ogv` | X | X | X |
| `.vp8` | X | X | X |
| `.webm` | X | X | X |
| `.wmv` | X | | |

> "The optimal supported video codec for most platforms is H.264. However, the optimal encoding for Linux is a `.webm` container with VP8 for video and Vorbis for audio."
> — https://docs.unity3d.com/Manual/VideoSources-FileCompatibility.html

### 7.2 H.265 hardware/software decoding per platform (Unity's own table)

From https://docs.unity3d.com/Manual/video-encoding-compatibility.html (identical table in 2022.3):

| Platform | Requirements | Encoding/Decoding | Notes |
|---|---|---|---|
| **macOS** | SDK 10.13+ | "Hardware encoding: 6th Generation Intel Core processor / Software encoding: All Macs / Hardware decoding: 6th Generation Intel Core processor / Software decoding: All Macs" | |
| **Windows** | Windows 10 + HEVC extensions | "Encoder / Decoder" | "HEVC extension (Hardware only) / HEVC extension (Hardware + software support)" |
| **UWP** | Windows 10+ | | "If a device lists support for H.265, that might not apply to all devices within the device family." |
| **Android** | 5.0+ | | |
| **iOS** | SDK 11.0+ | "**Hardware decoding: A9 Chip / Software decoding: All iOS Devices**" | |
| **tvOS** | SDK 11.0+ | | |

This is the only per-platform hardware-decoding statement Unity makes explicitly, and it is **only for H.265**.

### 7.3 iOS codec/container support

**未验证 for a definitive iOS codec/container list.** The current Manual delegates it to Apple:

> "**iOS:** Compare iPhone Models"
> — https://docs.unity3d.com/Manual/video-sources-compatibility-target-platforms.html

The only iOS-specific codec facts Unity states directly are in §7.2 (H.265 / A9 hardware decode) and §7.4 (ProRes 4444 on "most Apple platforms").

### 7.4 Transparency codecs (Apple-specific)

Source: https://docs.unity3d.com/Manual/VideoTransparency-codecs.html

> "Unity supports two codecs that have per-pixel alpha: **Apple ProRes 4444**, **WebM with VP8**"

> "**Apple ProRes 4444** — The Apple ProRes 4444 codec is a high-quality version of Apple ProRes for 4:4:4:4 image sources, including alpha channels. Some key information about this codec: Provides the same level of visual fidelity as the source video. **Most Apple platforms have support for it.** Typically produces large files, which increases storage and bandwidth requirements.
> When you import a video that uses this codec, **enable both the Transcode and Keep Alpha options** in the Video Clip Importer. If your video has the Apple ProRes codec but you don't transcode the file, your target platform might not support the video."

> "**WebM with VP8** — A WebM file that uses the VP8 video codec stores alpha information natively. Most Unity-supported platforms can read transparent videos in this format. … **Note: Most of Unity's supported platforms use a software implementation to decode WebM files.** Therefore, you don't need to transcode the files for these platforms. However, **Android's built-in VP8 support doesn't include transparency support, so you must enable transcoding to ensure Unity uses its internal alpha representation.**"

---

## 8. Streaming / URL source notes (relevant to seeking)

| Fact | Source |
|---|---|
| "The Video Player component uses the native audio and video decoding libraries of your Unity Editor platform to play video files in the Editor." | https://docs.unity3d.com/Manual/video-sources-compatibility-target-platforms.html |
| "Seeking is not supported in all contexts. For example, seeking in a HTTP live stream." | https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-canSetTime.html |
| StreamingAssets trick to bypass Editor import limits — "This allows the target platform to read the file directly and bypass the Editor's support limitations. However, the imported video clip isn't visible in the Editor and you can't drag the asset onto a Video Player in the scene." | https://docs.unity3d.com/Manual/VideoSources-FileCompatibility.html |
| StreamingAssets unlocks VP9 on Web/Switch and platform-native WebM codecs on Android | ibid. |

---

## 9. Summary table of the answer to each brief question

| # | Question | Answer | Confidence |
|---|---|---|---|
| 1 | Hardware decoding toggle in inspector/importer? | **No such toggle exists.** Only *Transcode* + *Codec* selection. | Verified, 4 independent primary sources |
| 1 | Which platforms get HW vs SW? | Codec-dependent, not user-selectable: H.264 "hardware on most platforms"; H.265 "hardware on some platforms"; VP8 "software on most platforms, hardware on Android and Web". SW decode path = VP8 video + Vorbis audio. Only explicit per-platform HW statement is the H.265 table (macOS 6th-gen Core, iOS A9). | Verified |
| 1 | Android MediaCodec / ExoPlayer? | **未验证.** No Unity source found. Only official Android-specific artefacts: the `AndroidVideoMedia` logcat prefix (2019.4 docs) and legacy Android Notes. `Manual/android-video.html` does not exist (404 in 6.6/6000.0/2022.3/2020.3/2019.4). | Negative verified; positive 未验证 |
| 2 | Seeking API descriptions | All transcribed verbatim in §2. Key: `seekCompleted` warns seek cost depends on "the codec performance and the parameters chosen at encoding time". | Verified |
| 3 | HAP / all-intra | **No official Unity documentation mentions HAP; 未验证.** `VideoCodec` = Auto/H264/H265/VP8 only. Unity's "Key encoding values" table has no GOP/intra-frame parameter. | Negative verified; recommendation 未验证 |
| 4 | Transcode / Keep Alpha | Transcribed verbatim in §4, incl. "longer build times and lower video quality" and "up to many hours". | Verified |
| 5 | Audio routing | AudioSource = "enabling Unity's audio processing to be applied"; Direct = "bypassing Unity's audio processing". `dspTime` interaction: **未验证**. Mute-and-use-separate-AudioSource recommendation: **未验证**. | Partly verified |
| 6 | WebGL | VideoClips **not supported** (URL only). Format list = .mp4/.m4v/.mov/.mpg/.mpeg/.webm/.ogv, extensionless URLs exempt. No frame accuracy; StepForward not frame-accurate; Safari drift correction disabled. | Verified |
| 7 | iOS/macOS | Only H.265 HW/SW table and ProRes 4444 ("most Apple platforms"). No dedicated iOS/macOS codec page → **未验证** for a full list. | Partly 未验证 |

---

## 10. Method notes / caveats for the reader

* Unversioned `https://docs.unity3d.com/Manual/...` and `/ScriptReference/...` URLs currently serve **Unity 6.6 (6000.6)** (`<link rel="canonical" href="https://docs.unity3d.com/6000.6/Documentation/...">` on every page). Version-pinned URLs used above are stated inline.
* `https://docs.unity3d.com/2021.3/Documentation/...` refused connections during this research; 2019.4 and 2022.3 responded normally. If 2021.3 is needed, retry later.
* `raw.githubusercontent.com` and the GitHub REST tree API were unreliable from this environment; the UnityCsReference files were retrieved through the jsDelivr mirror of the same repo (`cdn.jsdelivr.net/gh/Unity-Technologies/UnityCsReference@master/<path>`) and the GitHub contents API. Paths and line numbers are cited so they can be re-verified.
* I attempted to download Unity's official Manual search index (`https://docs.unity3d.com/Manual/docdata/index.js`, ~17 MB) to do an exhaustive corpus search for "HAP" / "ExoPlayer". **That download timed out and was not completed** — so the HAP/ExoPlayer negative results rest on the page-level and C#-source checks listed above, not on a full-corpus sweep. Treat them as strong but not exhaustive.
