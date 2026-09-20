using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    [Serializable] sealed class SceneAssetPreset
    {
        public string id, name, kind, sourcePath, animation;
        public bool background;
        public Vector3 scale = Vector3.one;
        public Color color = Color.white;
        public float animationSpeed = 1, animationAmount = 1;
    }
    [Serializable] sealed class EffectAssetPreset
    {
        public string id, name, kind, target, easing;
        public Color color = Color.white;
        public float durationBeats = 2, intensity = 1, frequency = 1;
        public int seed;
    }
    [Serializable] sealed class MotionAssetPreset
    {
        public string id, name, kind, easing;
        public float durationBeats = 4, intensity = 1, frequency = 2;
        public int seed;
        public CameraMotionKeyData[] keys;
    }
    [Serializable] sealed class SceneAssetNameOverride { public string id, name; }
    [Serializable] sealed class UserVisualLibrary
    {
        public SceneAssetPreset[] scenes = new SceneAssetPreset[0];
        public EffectAssetPreset[] effects = new EffectAssetPreset[0];
        public MotionAssetPreset[] motions = new MotionAssetPreset[0];
        public SceneAssetNameOverride[] sceneNames = new SceneAssetNameOverride[0];
    }

    public sealed partial class RuntimeChartEditorController
    {
        readonly List<SceneAssetPreset> sceneLibrary = new List<SceneAssetPreset>();
        readonly List<EffectAssetPreset> effectLibrary = new List<EffectAssetPreset>();
        readonly List<MotionAssetPreset> motionLibrary = new List<MotionAssetPreset>();
        readonly Dictionary<string, Texture2D> sceneThumbnails = new Dictionary<string, Texture2D>();
        UserVisualLibrary userLibrary;
        bool visualLibraryLoaded;
        int selectedSceneAsset, selectedEffectAsset, selectedMotionAsset;
        int selectedSceneObject = -1, selectedEffectClip = -1, selectedMotionClip = -1, selectedMotionKey;
        AuthoredVisualDirector authoredVisuals;
        int sceneAssetRenameIndex = -1;
        string sceneAssetRename = "";
        string VisualLibraryPath => Path.Combine(Application.persistentDataPath, "GeometryChartStudio", "visual-library.json");

        void EnsureVisualLibrary()
        {
            if (visualLibraryLoaded) return;
            visualLibraryLoaded = true;
            sceneLibrary.AddRange(new[]
            {
                ScenePreset("builtin.block", "Architectural Block", "cube", new Vector3(7, 18, 7), new Color(.22f,.28f,.38f), "none"),
                ScenePreset("builtin.light_pillar", "Light Pillar", "cylinder", new Vector3(1.2f, 12, 1.2f), new Color(.25f,.8f,1), "pulse"),
                ScenePreset("builtin.floating_crystal", "Floating Crystal", "sphere", new Vector3(2, 3.5f, 2), new Color(.72f,.35f,1), "float"),
                ScenePreset("builtin.rotor", "Kinetic Rotor", "cylinder", new Vector3(5,.35f,5), new Color(1,.38f,.18f), "rotate"),
                ScenePreset("builtin.screen", "Media Screen", "plane", new Vector3(12,7,1), new Color(.08f,.12f,.2f), "none"),
                ScenePreset("builtin.gateway", "Rhythm Gateway", "cube", new Vector3(14,.65f,1), new Color(.2f,1,.72f), "pulse")
            });
            effectLibrary.AddRange(new[]
            {
                EffectPreset("builtin.flash", "Flash", "flash", "screen", Color.white, .65f, 1, "impact"),
                EffectPreset("builtin.bloom", "Bloom Pulse", "light", "scene", new Color(.55f,.8f,1), 1, 1, "smooth"),
                EffectPreset("builtin.color", "Color Wash", "color", "screen", new Color(.55f,.16f,1), 2, .7f, "smooth"),
                EffectPreset("builtin.fog", "Fog Dive", "fog", "scene", new Color(.08f,.14f,.25f), 4, 1, "smooth"),
                EffectPreset("builtin.light", "Light Sweep", "light", "scene", new Color(.2f,.8f,1), 2, 1, "linear"),
                EffectPreset("builtin.pulse", "Scene Pulse", "scenePulse", "scene", Color.white, 2, 1, "smooth"),
                EffectPreset("builtin.shockwave", "Judgement Shockwave", "shockwave", "judgement", new Color(.25f,.8f,1), 1, 1, "impact"),
                EffectPreset("builtin.particles", "Particle Burst", "particles", "screen", new Color(.8f,.9f,1), 4, 1, "smooth"),
                EffectPreset("builtin.speed", "Speed Lines", "speedLines", "screen", new Color(.45f,.85f,1), 4, 1, "smooth"),
                EffectPreset("builtin.glitch", "Glitch Cut", "glitch", "screen", new Color(1,.15f,.55f), 1, 1, "impact")
            });
            motionLibrary.AddRange(new[]
            {
                MotionPreset("builtin.shake", "Impact Shake", "shake", 1, 1.2f, 6, "impact"),
                MotionPreset("builtin.punch", "Forward Punch", "punch", 2, 1, 1, "smooth"),
                MotionPreset("builtin.roll", "Roll Accent", "roll", 4, 1, 1, "smooth"),
                MotionPreset("builtin.orbit", "Orbit Sweep", "orbit", 8, 1, 1, "smooth"),
                MotionPreset("builtin.dolly", "Dolly In-Out", "dolly", 4, 1, 1, "smooth"),
                MotionPreset("builtin.fov", "FOV Pulse", "fovPulse", 2, 1, 1, "smooth"),
                MotionPreset("builtin.custom", "Blank Custom Motion", "custom", 4, 1, 1, "smooth")
            });
            try { userLibrary = File.Exists(VisualLibraryPath) ? JsonUtility.FromJson<UserVisualLibrary>(File.ReadAllText(VisualLibraryPath)) : null; }
            catch { userLibrary = null; }
            if (userLibrary == null) userLibrary = new UserVisualLibrary();
            if (userLibrary.scenes != null) sceneLibrary.AddRange(userLibrary.scenes);
            if (userLibrary.effects != null) effectLibrary.AddRange(userLibrary.effects);
            if (userLibrary.motions != null) motionLibrary.AddRange(userLibrary.motions);
            if (userLibrary.sceneNames != null) foreach (var rename in userLibrary.sceneNames)
            {
                if (rename == null || string.IsNullOrEmpty(rename.id) || string.IsNullOrWhiteSpace(rename.name)) continue;
                var preset = sceneLibrary.Find(item => item.id == rename.id); if (preset != null) preset.name = rename.name.Trim();
            }
        }

        static SceneAssetPreset ScenePreset(string id, string name, string kind, Vector3 scale, Color color, string animation)
            => new SceneAssetPreset { id = id, name = name, kind = kind, scale = scale, color = color, animation = animation, animationSpeed = 1, animationAmount = 1 };
        static EffectAssetPreset EffectPreset(string id, string name, string kind, string target, Color color, float duration, float intensity, string easing)
            => new EffectAssetPreset { id = id, name = name, kind = kind, target = target, color = color, durationBeats = duration, intensity = intensity, frequency = 1, easing = easing };
        static MotionAssetPreset MotionPreset(string id, string name, string kind, float duration, float intensity, float frequency, string easing)
            => new MotionAssetPreset { id = id, name = name, kind = kind, durationBeats = duration, intensity = intensity, frequency = frequency, easing = easing,
                keys = new[] { new CameraMotionKeyData { time = 0 }, new CameraMotionKeyData { time = 1 } } };

        void SaveUserLibrary()
        {
            try
            {
                userLibrary.scenes = sceneLibrary.FindAll(x => x.id.StartsWith("user.", StringComparison.Ordinal)).ToArray();
                userLibrary.effects = effectLibrary.FindAll(x => x.id.StartsWith("user.", StringComparison.Ordinal)).ToArray();
                userLibrary.motions = motionLibrary.FindAll(x => x.id.StartsWith("user.", StringComparison.Ordinal)).ToArray();
                var names = new List<SceneAssetNameOverride>();
                foreach (var preset in sceneLibrary) if (!preset.id.StartsWith("user.", StringComparison.Ordinal))
                    names.Add(new SceneAssetNameOverride { id = preset.id, name = preset.name });
                userLibrary.sceneNames = names.ToArray();
                Directory.CreateDirectory(Path.GetDirectoryName(VisualLibraryPath));
                File.WriteAllText(VisualLibraryPath, JsonUtility.ToJson(userLibrary, true), new UTF8Encoding(false));
                SetStatus("Reusable asset library saved");
            }
            catch (Exception e) { SetStatus("Library save failed: " + e.Message); }
        }

        void BuildAuthoredVisuals()
        {
            authoredVisuals?.Dispose();
            authoredVisuals = new AuthoredVisualDirector(visualRoot, chart, tempo, spatial, sceneCamera);
            authoredVisuals.Evaluate(songTime);
        }

        void DrawSceneInspector()
        {
            EnsureVisualLibrary();
            GUILayout.Label("Build the world before drawing paths.", smallStyle);
            GUILayout.Label("SCENE ASSET PACK", smallStyle);
            DrawSceneAssetPack();
            if (GUILayout.Button("Place selected asset", selectedButtonStyle)) AddSceneObject(sceneLibrary[selectedSceneAsset]);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Import OBJ")) ImportSceneAsset("obj");
            if (GUILayout.Button("Import image")) ImportSceneAsset("image");
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Import BGA video (MP4 / WebM)")) ImportSceneAsset("video");
            DrawLibraryTransferButtons("scene");
            GUILayout.Space(10);
            GUILayout.Label("Select placed objects directly in the viewport. Caps OFF: click an object. Caps ON: aim with the crosshair and click.", smallStyle);
        }

        void DrawSceneObjectDetails()
        {
            if (!SceneDetailVisible) return;
            float x = ViewWidth - RightPanelWidth, height = ViewHeight - ToolbarHeight - TimelineHeight;
            GUI.Box(new Rect(x, ToolbarHeight, RightPanelWidth, height), "", panelStyle);
            GUILayout.BeginArea(new Rect(x + 14, ToolbarHeight + 12, RightPanelWidth - 28, height - 24));
            sceneDetailScroll = GUILayout.BeginScrollView(sceneDetailScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical(GUILayout.Width(RightPanelWidth - 52));
            GUILayout.Label("OBJECT INSPECTOR", headingStyle);
            var o = chart.sceneObjects[selectedSceneObject]; bool changed = false;
            GUI.SetNextControlName("Edit:SceneObjectName");
            string objectName = GUILayout.TextField(o.name ?? "");
            if (objectName != o.name) { RecordUndo(); o.name = objectName; }
            GUILayout.Label((o.kind ?? "object").ToUpperInvariant() + "  ·  " + (o.assetId ?? "custom"), smallStyle);
            GUILayout.Space(6); GUILayout.Label("TRANSFORM", smallStyle);
            changed |= FloatField("Pos X", ref o.position.x); changed |= FloatField("Pos Y", ref o.position.y); changed |= FloatField("Pos Z", ref o.position.z);
            changed |= FloatField("Rot X", ref o.rotation.x); changed |= FloatField("Rot Y", ref o.rotation.y); changed |= FloatField("Rot Z", ref o.rotation.z);
            changed |= FloatField("Scale X", ref o.scale.x); changed |= FloatField("Scale Y", ref o.scale.y); changed |= FloatField("Scale Z", ref o.scale.z);
            GUILayout.Space(7);
            GUILayout.Label("Animation  " + o.animation, smallStyle);
            foreach (string a in new[] { "none", "float", "rotate", "pulse", "pendulum" })
                if (GUILayout.Button(a, o.animation == a ? selectedButtonStyle : buttonStyle)) { RecordUndo(); o.animation = a; changed = true; }
            changed |= FloatField("Anim speed", ref o.animationSpeed); changed |= FloatField("Anim amount", ref o.animationAmount);
            if (o.kind == "image" || o.kind == "video")
            {
                GUILayout.Space(7);
                if (GUILayout.Button(o.background ? "Full-frame backdrop  ON" : "Full-frame backdrop  OFF",
                    o.background ? selectedButtonStyle : buttonStyle))
                { RecordUndo(); o.background = !o.background; changed = true; }
                if (o.background) GUILayout.Label("Fills the frame from the camera; the transform above is unused.", smallStyle);
            }
            if (changed) RebuildVisuals();
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save as asset")) SaveScenePreset(o);
            if (GUILayout.Button("Update asset")) UpdateScenePreset(o);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Delete scene object"))
            {
                int remove = selectedSceneObject;
                Change(() => { var list = new List<SceneObjectData>(chart.sceneObjects); list.RemoveAt(remove); chart.sceneObjects = list.ToArray(); selectedSceneObject = -1; }, "Scene object deleted");
                UpdateViewportRect();
            }
            GUILayout.EndVertical(); GUILayout.EndScrollView(); GUILayout.EndArea();
        }

        void DrawSceneAssetPack()
        {
            selectedSceneAsset = Mathf.Clamp(selectedSceneAsset, 0, Mathf.Max(0, sceneLibrary.Count - 1));
            for (int i = 0; i < sceneLibrary.Count; i++)
            {
                var preset = sceneLibrary[i]; Rect row = GUILayoutUtility.GetRect(40, 58, GUILayout.ExpandWidth(true));
                if (GUI.Button(row, "", i == selectedSceneAsset ? selectedButtonStyle : buttonStyle)) selectedSceneAsset = i;
                GUI.DrawTexture(new Rect(row.x + 7, row.y + 5, 48, 48), SceneThumbnail(preset), ScaleMode.ScaleToFit, true);
                GUI.Label(new Rect(row.x + 64, row.y + 7, row.width - 70, 24), preset.name, headingStyle);
                GUI.Label(new Rect(row.x + 64, row.y + 30, row.width - 70, 20), (preset.kind ?? "object").ToUpperInvariant(), smallStyle);
            }
            if (sceneLibrary.Count == 0) return;
            if (sceneAssetRenameIndex != selectedSceneAsset)
            { sceneAssetRenameIndex = selectedSceneAsset; sceneAssetRename = sceneLibrary[selectedSceneAsset].name; }
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("Edit:SceneAssetName");
            sceneAssetRename = GUILayout.TextField(sceneAssetRename ?? "");
            if (GUILayout.Button("Rename", GUILayout.Width(74)) && !string.IsNullOrWhiteSpace(sceneAssetRename))
                RenameSceneAsset(selectedSceneAsset, sceneAssetRename, true);
            GUILayout.EndHorizontal();
            DrawDeletePresetButton("scene");
        }

        bool RenameSceneAsset(int index, string value, bool persist)
        {
            if (index < 0 || index >= sceneLibrary.Count || string.IsNullOrWhiteSpace(value)) return false;
            sceneLibrary[index].name = value.Trim(); sceneAssetRename = sceneLibrary[index].name;
            if (persist) SaveUserLibrary(); return true;
        }

        Texture2D SceneThumbnail(SceneAssetPreset preset)
        {
            string key = preset.id + "|" + preset.sourcePath + "|" + preset.kind + "|" + preset.color;
            if (sceneThumbnails.TryGetValue(key, out var cached) && cached != null) return cached;
            if (preset.kind == "image" && !string.IsNullOrEmpty(preset.sourcePath) && File.Exists(preset.sourcePath))
            {
                try
                {
                    var image = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                    if (image.LoadImage(File.ReadAllBytes(preset.sourcePath)))
                    { ownedTextures.Add(image); sceneThumbnails[key] = image; return image; }
                    Destroy(image);
                }
                catch { }
            }
            const int size = 64; var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            Color background = new Color(.055f, .065f, .085f), accent = preset.color; accent.a = 1;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float nx = (x - 31.5f) / 31.5f, ny = (y - 31.5f) / 31.5f;
                bool shape = preset.kind == "sphere" ? nx * nx + ny * ny < .48f :
                    preset.kind == "cylinder" ? Mathf.Abs(nx) < .48f && Mathf.Abs(ny) < .62f :
                    preset.kind == "plane" || preset.kind == "image" || preset.kind == "video" ? Mathf.Abs(nx) < .72f && Mathf.Abs(ny) < .45f :
                    preset.kind == "obj" ? Mathf.Abs(nx) + Mathf.Abs(ny) < .82f : Mathf.Abs(nx) < .55f && Mathf.Abs(ny) < .55f;
                Color color = shape ? Color.Lerp(accent, Color.white, Mathf.Clamp01((ny + 1) * .18f)) : background;
                if (shape && preset.kind == "video" && nx > -.15f && nx < .38f && Mathf.Abs(ny) < (.38f - nx) * .7f) color = Color.white;
                pixels[y * size + x] = color;
            }
            texture.SetPixels(pixels); texture.Apply(); ownedTextures.Add(texture); sceneThumbnails[key] = texture; return texture;
        }

        void DrawEffectInspector()
        {
            EnsureVisualLibrary();
            GUILayout.Label("Drag-ready object and screen effects.", smallStyle);
            GUILayout.Label("EFFECT ASSET PACK", smallStyle);
            DrawPresetButtons(effectLibrary, ref selectedEffectAsset, p => p.name);
            if (GUILayout.Button("Add effect at playhead", selectedButtonStyle)) AddEffectClip(effectLibrary[selectedEffectAsset]);
            DrawLibraryTransferButtons("effect");
            GUILayout.Space(8); GUILayout.Label("EFFECT CLIPS", smallStyle);
            for (int i = 0; i < chart.effectClips.Length; i++) if (GUILayout.Button(chart.effectClips[i].name,
                i == selectedEffectClip ? selectedButtonStyle : buttonStyle)) { selectedEffectClip = i; Seek(tempo.SecondsAtBeat(chart.effectClips[i].startTick / (double)chart.ticksPerBeat)); }
            if (selectedEffectClip < 0 || selectedEffectClip >= chart.effectClips.Length) return;
            var clip = chart.effectClips[selectedEffectClip]; bool changed = false; float beats = clip.durationTicks / (float)chart.ticksPerBeat;
            changed |= FloatField("Duration", ref beats); changed |= FloatField("Intensity", ref clip.intensity); changed |= FloatField("Frequency", ref clip.frequency);
            if (changed) { clip.durationTicks = Mathf.Max(1, Mathf.RoundToInt(beats * chart.ticksPerBeat)); RebuildVisuals(); }
            GUILayout.Label("Target: " + clip.target + " · " + clip.kind, smallStyle);
            if (GUILayout.Button("Target selected scene object", buttonStyle) && selectedSceneObject >= 0 && selectedSceneObject < chart.sceneObjects.Length)
            { RecordUndo(); clip.target = chart.sceneObjects[selectedSceneObject].id; RebuildVisuals(); }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save as asset")) SaveEffectPreset(clip);
            if (GUILayout.Button("Update asset")) UpdateEffectPreset(clip);
            GUILayout.EndHorizontal();
            DrawDeletePresetButton("effect");
            if (GUILayout.Button("Delete effect clip")) DeleteEffectClip();
        }

        void DrawCameraMotionInspector()
        {
            EnsureVisualLibrary();
            GUILayout.Label("Reusable additive motion, layered over the base Camera page.", smallStyle);
            GUILayout.Label("CAMERA MOTION PACK", smallStyle);
            DrawPresetButtons(motionLibrary, ref selectedMotionAsset, p => p.name);
            if (GUILayout.Button("Add motion at playhead", selectedButtonStyle)) AddMotionClip(motionLibrary[selectedMotionAsset]);
            DrawLibraryTransferButtons("motion");
            GUILayout.Space(8); GUILayout.Label("MOTION CLIPS", smallStyle);
            for (int i = 0; i < chart.cameraMotionClips.Length; i++) if (GUILayout.Button(chart.cameraMotionClips[i].name,
                i == selectedMotionClip ? selectedButtonStyle : buttonStyle)) { selectedMotionClip = i; Seek(tempo.SecondsAtBeat(chart.cameraMotionClips[i].startTick / (double)chart.ticksPerBeat)); }
            if (selectedMotionClip < 0 || selectedMotionClip >= chart.cameraMotionClips.Length) return;
            var clip = chart.cameraMotionClips[selectedMotionClip]; float beats = clip.durationTicks / (float)chart.ticksPerBeat; bool changed = false;
            changed |= FloatField("Duration", ref beats); changed |= FloatField("Intensity", ref clip.intensity); changed |= FloatField("Frequency", ref clip.frequency);
            if (changed) { clip.durationTicks = Mathf.Max(1, Mathf.RoundToInt(beats * chart.ticksPerBeat)); timelineCurveRevision++; }
            GUILayout.Label("PROCEDURAL LAYER  " + clip.kind, smallStyle);
            GUILayout.Label("CUSTOM MOTION KEYS", smallStyle);
            if (clip.keys == null || clip.keys.Length == 0) clip.keys = new[] { new CameraMotionKeyData { time = 0 }, new CameraMotionKeyData { time = 1 } };
            GUILayout.BeginHorizontal();
            for (int i = 0; i < clip.keys.Length; i++) { int captured = i; if (GUILayout.Button((clip.keys[i].time * 100).ToString("0") + "%", i == selectedMotionKey ? selectedButtonStyle : buttonStyle)) selectedMotionKey = captured; }
            GUILayout.EndHorizontal();
            selectedMotionKey = Mathf.Clamp(selectedMotionKey, 0, clip.keys.Length - 1); var key = clip.keys[selectedMotionKey];
            changed = FloatField("Key time", ref key.time); changed |= FloatField("Move X", ref key.position.x); changed |= FloatField("Move Y", ref key.position.y); changed |= FloatField("Move Z", ref key.position.z);
            changed |= FloatField("Pitch", ref key.rotation.x); changed |= FloatField("Yaw", ref key.rotation.y); changed |= FloatField("Roll", ref key.rotation.z); changed |= FloatField("FOV offset", ref key.fov);
            key.time = Mathf.Clamp01(key.time); if (changed) { Array.Sort(clip.keys, (a, b) => a.time.CompareTo(b.time)); selectedMotionKey = Array.IndexOf(clip.keys, key); }
            if (GUILayout.Button("Add key at playhead inside clip")) AddMotionKeyAtPlayhead(clip);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save as asset")) SaveMotionPreset(clip);
            if (GUILayout.Button("Update asset")) UpdateMotionPreset(clip);
            GUILayout.EndHorizontal();
            DrawDeletePresetButton("motion");
            if (GUILayout.Button("Preview from clip start  [F5]")) { Seek(tempo.SecondsAtBeat(clip.startTick / (double)chart.ticksPerBeat)); ToggleCameraPreview(); }
            if (GUILayout.Button("Delete motion clip")) DeleteMotionClip();
        }

        void DrawPresetButtons<T>(List<T> list, ref int selected, Func<T, string> label)
        {
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, list.Count - 1));
            for (int i = 0; i < list.Count; i++)
                if (GUILayout.Button(label(list[i]), i == selected ? selectedButtonStyle : buttonStyle)) selected = i;
        }

        void AddSceneObject(SceneAssetPreset preset)
        {
            var o = new SceneObjectData { id = "scene-" + Guid.NewGuid().ToString("N"), name = preset.name, assetId = preset.id,
                kind = preset.kind, sourcePath = preset.sourcePath, background = preset.background, position = PlacementMarkerPosition, scale = preset.scale,
                color = preset.color, animation = preset.animation, animationSpeed = preset.animationSpeed, animationAmount = preset.animationAmount };
            Change(() => { var list = new List<SceneObjectData>(chart.sceneObjects) { o }; chart.sceneObjects = list.ToArray(); selectedSceneObject = chart.sceneObjects.Length - 1; }, "Scene asset placed");
        }
        void AddEffectClip(EffectAssetPreset preset)
        {
            var clip = new EffectClipData { id = "fx-" + Guid.NewGuid().ToString("N"), name = preset.name, presetId = preset.id, startTick = PlayheadTick,
                durationTicks = Mathf.Max(1, Mathf.RoundToInt(preset.durationBeats * chart.ticksPerBeat)), kind = preset.kind, target = preset.target,
                color = preset.color, intensity = preset.intensity, frequency = preset.frequency, easing = preset.easing, seed = preset.seed };
            Change(() => { var list = new List<EffectClipData>(chart.effectClips) { clip }; chart.effectClips = list.ToArray(); selectedEffectClip = chart.effectClips.Length - 1; }, "Effect clip added");
        }
        void AddMotionClip(MotionAssetPreset preset)
        {
            var clip = new CameraMotionClipData { id = "motion-" + Guid.NewGuid().ToString("N"), name = preset.name, presetId = preset.id, startTick = PlayheadTick,
                durationTicks = Mathf.Max(1, Mathf.RoundToInt(preset.durationBeats * chart.ticksPerBeat)), kind = preset.kind, intensity = preset.intensity,
                frequency = preset.frequency, easing = preset.easing, seed = preset.seed, keys = CloneKeys(preset.keys) };
            Change(() => { var list = new List<CameraMotionClipData>(chart.cameraMotionClips) { clip }; chart.cameraMotionClips = list.ToArray(); selectedMotionClip = chart.cameraMotionClips.Length - 1; }, "Camera motion added");
        }
        static CameraMotionKeyData[] CloneKeys(CameraMotionKeyData[] keys)
        {
            if (keys == null) return new CameraMotionKeyData[0]; var result = new CameraMotionKeyData[keys.Length];
            for (int i = 0; i < keys.Length; i++) result[i] = new CameraMotionKeyData { time = keys[i].time, position = keys[i].position, rotation = keys[i].rotation, fov = keys[i].fov };
            return result;
        }

        void AddMotionKeyAtPlayhead(CameraMotionClipData clip)
        {
            float tick = (float)tempo.BeatAtSeconds(songTime) * chart.ticksPerBeat;
            float q = Mathf.Clamp01((tick - clip.startTick) / Mathf.Max(1, clip.durationTicks)); RecordUndo();
            var list = new List<CameraMotionKeyData>(clip.keys); var key = new CameraMotionKeyData { time = q };
            list.Add(key); list.Sort((a, b) => a.time.CompareTo(b.time)); clip.keys = list.ToArray(); selectedMotionKey = Array.IndexOf(clip.keys, key);
            SetStatus("Custom motion key added");
        }

        void SaveScenePreset(SceneObjectData source)
        {
            var preset = ScenePreset("user." + Guid.NewGuid().ToString("N"), source.name + " Preset", source.kind, source.scale, source.color, source.animation);
            preset.sourcePath = source.sourcePath; preset.background = source.background;
            preset.animationSpeed = source.animationSpeed; preset.animationAmount = source.animationAmount; sceneLibrary.Add(preset); selectedSceneAsset = sceneLibrary.Count - 1; SaveUserLibrary();
        }
        void UpdateScenePreset(SceneObjectData source)
        {
            EnsureVisualLibrary(); var preset = sceneLibrary[selectedSceneAsset];
            if (!preset.id.StartsWith("user.", StringComparison.Ordinal)) { SetStatus("Built-in assets are read-only; use Save as asset"); return; }
            preset.name = source.name; preset.kind = source.kind; preset.sourcePath = source.sourcePath; preset.background = source.background; preset.scale = source.scale; preset.color = source.color;
            preset.animation = source.animation; preset.animationSpeed = source.animationSpeed; preset.animationAmount = source.animationAmount; SaveUserLibrary();
        }
        void SaveEffectPreset(EffectClipData source)
        {
            var preset = EffectPreset("user." + Guid.NewGuid().ToString("N"), source.name + " Preset", source.kind, source.target, source.color,
                source.durationTicks / (float)chart.ticksPerBeat, source.intensity, source.easing); preset.frequency = source.frequency; preset.seed = source.seed;
            effectLibrary.Add(preset); selectedEffectAsset = effectLibrary.Count - 1; SaveUserLibrary();
        }
        void UpdateEffectPreset(EffectClipData source)
        {
            EnsureVisualLibrary(); var preset = effectLibrary[selectedEffectAsset];
            if (!preset.id.StartsWith("user.", StringComparison.Ordinal)) { SetStatus("Built-in assets are read-only; use Save as asset"); return; }
            preset.name = source.name; preset.kind = source.kind; preset.target = source.target; preset.color = source.color;
            preset.durationBeats = source.durationTicks / (float)chart.ticksPerBeat; preset.intensity = source.intensity; preset.frequency = source.frequency; preset.seed = source.seed; preset.easing = source.easing; SaveUserLibrary();
        }
        void SaveMotionPreset(CameraMotionClipData source)
        {
            var preset = MotionPreset("user." + Guid.NewGuid().ToString("N"), source.name + " Preset", source.kind,
                source.durationTicks / (float)chart.ticksPerBeat, source.intensity, source.frequency, source.easing);
            preset.seed = source.seed; preset.keys = CloneKeys(source.keys); motionLibrary.Add(preset); selectedMotionAsset = motionLibrary.Count - 1; SaveUserLibrary();
        }
        void UpdateMotionPreset(CameraMotionClipData source)
        {
            EnsureVisualLibrary(); var preset = motionLibrary[selectedMotionAsset];
            if (!preset.id.StartsWith("user.", StringComparison.Ordinal)) { SetStatus("Built-in assets are read-only; use Save as asset"); return; }
            preset.name = source.name; preset.kind = source.kind; preset.durationBeats = source.durationTicks / (float)chart.ticksPerBeat;
            preset.intensity = source.intensity; preset.frequency = source.frequency; preset.seed = source.seed; preset.easing = source.easing; preset.keys = CloneKeys(source.keys); SaveUserLibrary();
        }

        void DrawDeletePresetButton(string category)
        {
            bool user = category == "scene" ? sceneLibrary[selectedSceneAsset].id.StartsWith("user.", StringComparison.Ordinal) :
                category == "effect" ? effectLibrary[selectedEffectAsset].id.StartsWith("user.", StringComparison.Ordinal) : motionLibrary[selectedMotionAsset].id.StartsWith("user.", StringComparison.Ordinal);
            bool enabled = GUI.enabled; GUI.enabled = enabled && user;
            if (GUILayout.Button("Delete selected personal asset"))
            {
                if (category == "scene") { sceneLibrary.RemoveAt(selectedSceneAsset); selectedSceneAsset = Mathf.Max(0, selectedSceneAsset - 1); }
                else if (category == "effect") { effectLibrary.RemoveAt(selectedEffectAsset); selectedEffectAsset = Mathf.Max(0, selectedEffectAsset - 1); }
                else { motionLibrary.RemoveAt(selectedMotionAsset); selectedMotionAsset = Mathf.Max(0, selectedMotionAsset - 1); }
                SaveUserLibrary();
            }
            GUI.enabled = enabled;
        }

        void DrawLibraryTransferButtons(string category)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Import pack")) ImportLibraryPack(category);
            if (GUILayout.Button("Export asset")) ExportLibraryAsset(category);
            GUILayout.EndHorizontal();
        }

        void ImportLibraryPack(string category)
        {
            string path = ChooseVisualPack(false); if (string.IsNullOrEmpty(path)) return;
            try
            {
                var pack = JsonUtility.FromJson<UserVisualLibrary>(File.ReadAllText(path));
                if (pack == null) throw new InvalidDataException("Invalid visual asset pack");
                if (category == "scene" && pack.scenes != null) foreach (var item in pack.scenes) { item.id = "user." + Guid.NewGuid().ToString("N"); sceneLibrary.Add(item); }
                else if (category == "effect" && pack.effects != null) foreach (var item in pack.effects) { item.id = "user." + Guid.NewGuid().ToString("N"); effectLibrary.Add(item); }
                else if (category == "motion" && pack.motions != null) foreach (var item in pack.motions) { item.id = "user." + Guid.NewGuid().ToString("N"); motionLibrary.Add(item); }
                SaveUserLibrary(); SetStatus("Visual asset pack imported");
            }
            catch (Exception e) { SetStatus("Pack import failed: " + e.Message); }
        }

        void ExportLibraryAsset(string category)
        {
            string path = ChooseVisualPack(true); if (string.IsNullOrEmpty(path)) return;
            try
            {
                var pack = new UserVisualLibrary();
                if (category == "scene") pack.scenes = new[] { sceneLibrary[selectedSceneAsset] };
                else if (category == "effect") pack.effects = new[] { effectLibrary[selectedEffectAsset] };
                else pack.motions = new[] { motionLibrary[selectedMotionAsset] };
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllText(path, JsonUtility.ToJson(pack, true), new UTF8Encoding(false)); SetStatus("Visual asset exported");
            }
            catch (Exception e) { SetStatus("Pack export failed: " + e.Message); }
        }

        string ChooseVisualPack(bool save)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            fileDialogOpen = true;
            try
            {
                var dialog = new ChartFileDialog { owner = GetActiveWindow(), title = save ? "Export visual asset" : "Import visual asset pack",
                    filter = "Visual asset pack (*.json)\0*.json\0\0", defaultExtension = "json",
                    initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    flags = 0x00080000 | 0x00000800 | (save ? 0x00000002 : 0x00001000) };
                dialog.file = Marshal.AllocHGlobal(dialog.maxFile * 2);
                try
                {
                    var buffer = new char[dialog.maxFile];
                    if (save) { const string suggested = "visual-asset.json"; suggested.CopyTo(0, buffer, 0, suggested.Length); }
                    Marshal.Copy(buffer, 0, dialog.file, dialog.maxFile);
                    bool accepted = save ? SaveChartDialog(dialog) : OpenChartDialog(dialog); return accepted ? Marshal.PtrToStringUni(dialog.file) : null;
                }
                finally { Marshal.FreeHGlobal(dialog.file); }
            }
            finally { fileDialogOpen = false; }
#else
            SetStatus("Pack transfer currently requires Windows"); return null;
#endif
        }

        void DeleteEffectClip() => Change(() => { var list = new List<EffectClipData>(chart.effectClips); list.RemoveAt(selectedEffectClip); chart.effectClips = list.ToArray(); selectedEffectClip = Mathf.Min(selectedEffectClip, chart.effectClips.Length - 1); }, "Effect clip deleted");
        void DeleteMotionClip() => Change(() => { var list = new List<CameraMotionClipData>(chart.cameraMotionClips); list.RemoveAt(selectedMotionClip); chart.cameraMotionClips = list.ToArray(); selectedMotionClip = Mathf.Min(selectedMotionClip, chart.cameraMotionClips.Length - 1); }, "Camera motion deleted");

        void ImportSceneAsset(string kind)
        {
            string path = ChooseVisualAsset(kind == "obj" ? "Import Wavefront OBJ" : kind == "video" ? "Import MP4 or WebM BGA" : "Import PNG or JPG",
                kind == "obj" ? "Wavefront OBJ (*.obj)\0*.obj\0\0" : kind == "video" ? "Video (*.mp4;*.webm)\0*.mp4;*.webm\0\0" : "Images (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0\0");
            if (string.IsNullOrEmpty(path)) return;
            var preset = ScenePreset("user." + Guid.NewGuid().ToString("N"), Path.GetFileNameWithoutExtension(path), kind,
                kind == "image" || kind == "video" ? new Vector3(12, 7, 1) : Vector3.one, Color.white, "none");
            preset.sourcePath = path; preset.background = kind == "video"; sceneLibrary.Add(preset); selectedSceneAsset = sceneLibrary.Count - 1; SaveUserLibrary(); AddSceneObject(preset);
        }

        string ChooseVisualAsset(string title, string filter)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            fileDialogOpen = true;
            try
            {
                var dialog = new ChartFileDialog { owner = GetActiveWindow(), title = title, filter = filter,
                    initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), flags = 0x00080000 | 0x00001000 | 0x00000800 };
                dialog.file = Marshal.AllocHGlobal(dialog.maxFile * 2);
                try { Marshal.Copy(new char[dialog.maxFile], 0, dialog.file, dialog.maxFile); return OpenChartDialog(dialog) ? Marshal.PtrToStringUni(dialog.file) : null; }
                finally { Marshal.FreeHGlobal(dialog.file); }
            }
            finally { fileDialogOpen = false; }
#else
            SetStatus("Asset import currently requires Windows"); return null;
#endif
        }
    }
}
