using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

namespace GeometryRhythm
{
    /// <summary>Deterministic additive camera motion shared by the game and standalone chart studio.</summary>
    public static class CameraMotionEvaluator
    {
        public static void Apply(ChartData chart, TempoMap tempo, Camera camera, double seconds)
        {
            if (chart?.cameraMotionClips == null || tempo == null || camera == null) return;
            float tick = (float)tempo.BeatAtSeconds(seconds) * chart.ticksPerBeat;
            Vector3 position = Vector3.zero, rotation = Vector3.zero;
            float fov = 0;
            foreach (var clip in chart.cameraMotionClips)
            {
                if (clip == null || clip.durationTicks <= 0 || tick < clip.startTick || tick > clip.startTick + clip.durationTicks) continue;
                float q = Mathf.Clamp01((tick - clip.startTick) / clip.durationTicks);
                float envelope = Envelope(q, clip.easing);
                Vector3 p = Vector3.zero, r = Vector3.zero; float z = 0;
                if (clip.keys != null && clip.keys.Length > 0) EvaluateKeys(clip.keys, q, out p, out r, out z);
                float phase = q * Mathf.PI * 2 * Mathf.Max(.01f, clip.frequency);
                switch (clip.kind)
                {
                    case "shake":
                        p += new Vector3(Mathf.Sin(phase * 1.73f + clip.seed), Mathf.Sin(phase * 2.31f + 1.7f), 0) * .32f * envelope;
                        r += new Vector3(Mathf.Sin(phase * 2.7f), Mathf.Sin(phase * 1.9f + 2), Mathf.Sin(phase * 3.1f)) * 1.2f * envelope;
                        break;
                    case "punch": p.z += Mathf.Sin(q * Mathf.PI) * 3.2f; z -= Mathf.Sin(q * Mathf.PI) * 4; break;
                    case "roll": r.z += Mathf.Sin(q * Mathf.PI) * 18; break;
                    case "orbit": r.y += Mathf.Sin(q * Mathf.PI * 2) * 12; p.x += Mathf.Sin(q * Mathf.PI * 2) * 1.4f; break;
                    case "dolly": p.z += Mathf.Sin(q * Mathf.PI) * 4; break;
                    case "fovPulse": z += Mathf.Sin(q * Mathf.PI) * 12; break;
                }
                position += p * clip.intensity;
                rotation += r * clip.intensity;
                fov += z * clip.intensity;
            }
            camera.transform.position += camera.transform.TransformVector(position);
            camera.transform.rotation *= Quaternion.Euler(rotation);
            camera.fieldOfView = Mathf.Clamp(camera.fieldOfView + fov, 20, 100);
        }

        static float Envelope(float q, string easing)
        {
            if (easing == "linear") return 1;
            if (easing == "impact") return Mathf.Exp(-5 * q);
            return Mathf.Sin(q * Mathf.PI);
        }

        static void EvaluateKeys(CameraMotionKeyData[] keys, float q, out Vector3 p, out Vector3 r, out float fov)
        {
            int a = 0;
            while (a + 1 < keys.Length && keys[a + 1].time <= q) a++;
            int b = Mathf.Min(a + 1, keys.Length - 1);
            float mix = a == b ? 0 : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(keys[a].time, keys[b].time, q));
            p = Vector3.Lerp(keys[a].position, keys[b].position, mix);
            r = Vector3.Lerp(keys[a].rotation, keys[b].rotation, mix);
            fov = Mathf.Lerp(keys[a].fov, keys[b].fov, mix);
        }
    }

    /// <summary>Full-frame backdrop placement for image and video scene objects. Kept apart from
    /// the director so the fit can be exercised without building a scene.</summary>
    public static class Backdrop
    {
        /// <summary>Clearance in front of the near plane: far enough not to be clipped by it,
        /// close enough that nothing can ever get between the backdrop and the camera.</summary>
        public const float NearClearance = .02f;
        /// <summary>Background queue. Later backdrops are offset upwards so their draw order is
        /// the order they appear in the chart rather than whatever the renderer decides.</summary>
        public const int Queue = 1000;

        public static bool IsBackdrop(SceneObjectData data)
            => data != null && data.background && (data.kind == "image" || data.kind == "video");

        public static float Distance(Camera camera) => camera.nearClipPlane + NearClearance;

        /// <summary>Local scale of a unit quad parented to the camera that covers the whole frame
        /// at <paramref name="distance"/>. The source keeps its own aspect ratio and the
        /// overflowing axis is cropped rather than squashed; a source whose aspect is not known
        /// yet falls back to the camera's, which fills the frame exactly.</summary>
        public static Vector2 Scale(float verticalFieldOfView, float cameraAspect, float sourceAspect, float distance)
        {
            float frameHeight = 2 * distance * Mathf.Tan(verticalFieldOfView * Mathf.Deg2Rad * .5f);
            float frameWidth = frameHeight * cameraAspect;
            if (float.IsNaN(sourceAspect) || float.IsInfinity(sourceAspect) || sourceAspect <= 0) sourceAspect = cameraAspect;
            return sourceAspect > cameraAspect
                ? new Vector2(frameHeight * sourceAspect, frameHeight)
                : new Vector2(frameWidth, frameWidth / sourceAspect);
        }
    }

    /// <summary>Builds user-authored scene objects and evaluates reusable effect clips.</summary>
    public sealed class AuthoredVisualDirector : IDisposable
    {
        sealed class SceneRuntime
        {
            public SceneObjectData data;
            public Transform transform;
            public Material material;
            public VideoPlayer video;
            public bool backdrop;
            public double videoTime;
        }
        sealed class EffectRuntime
        {
            public EffectClipData data;
            public Transform root;
            public readonly List<Transform> particles = new List<Transform>();
            public readonly List<Vector3> particleBase = new List<Vector3>();
        }

        readonly ChartData chart;
        readonly TempoMap tempo;
        readonly SpatialDirector spatial;
        readonly Camera camera;
        readonly Transform root, sceneRoot, effectRoot;
        readonly List<SceneRuntime> scenes = new List<SceneRuntime>();
        readonly List<EffectRuntime> effects = new List<EffectRuntime>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly Color originalFog, originalAmbient, originalBackgroundColor;
        readonly float originalFogEnd;
        readonly bool originalFogEnabled;
        readonly CameraClearFlags originalClearFlags;
        int backdrops;
        readonly Light effectLight;
        readonly Transform overlay;
        readonly Material overlayMaterial;

        public AuthoredVisualDirector(Transform parent, ChartData chart, TempoMap tempo, SpatialDirector spatial, Camera camera)
        {
            this.chart = chart; this.tempo = tempo; this.spatial = spatial; this.camera = camera;
            root = new GameObject("Authored visuals").transform; root.SetParent(parent, false);
            sceneRoot = new GameObject("Reusable scene objects").transform; sceneRoot.SetParent(root, false);
            effectRoot = new GameObject("Reusable effect clips").transform; effectRoot.SetParent(root, false);
            originalFog = RenderSettings.fogColor; originalFogEnd = RenderSettings.fogEndDistance;
            originalFogEnabled = RenderSettings.fog; originalAmbient = RenderSettings.ambientLight;
            originalClearFlags = camera.clearFlags; originalBackgroundColor = camera.backgroundColor;
            var lightObject = new GameObject("Effect light", typeof(Light)); lightObject.transform.SetParent(effectRoot, false);
            effectLight = lightObject.GetComponent<Light>(); effectLight.type = LightType.Directional; effectLight.shadows = LightShadows.None;
            effectLight.transform.rotation = Quaternion.Euler(30, -45, 0); effectLight.enabled = false;
            overlayMaterial = Own(new Material(Shader.Find("Sprites/Default"))) as Material;
            overlayMaterial.renderQueue = 5000;
            var overlayObject = GameObject.CreatePrimitive(PrimitiveType.Quad); overlayObject.name = "Screen effect overlay";
            UnityEngine.Object.Destroy(overlayObject.GetComponent<Collider>()); overlay = overlayObject.transform; overlay.SetParent(camera.transform, false);
            overlay.GetComponent<Renderer>().sharedMaterial = overlayMaterial; overlay.gameObject.SetActive(false);
            BuildScene(); BuildEffects();
            // Place them once up front rather than waiting for the first Evaluate: the demo shows
            // the 3D environment behind its menus without advancing the chart, and an unplaced
            // backdrop would sit at the camera origin inside the near plane -- invisible, over a
            // frame the clear below has already blacked out.
            foreach (var scene in scenes) if (scene.backdrop) PlaceBackdrop(scene);
            // The skybox is drawn after the opaque pass and would cover a backdrop that writes no
            // depth, so a chart with one stops clearing to the sky and lets the picture do it.
            if (backdrops > 0) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; }
        }

        T Own<T>(T value) where T : UnityEngine.Object { if (value != null) owned.Add(value); return value; }

        void BuildScene()
        {
            if (chart.sceneObjects == null) return;
            foreach (var data in chart.sceneObjects)
            {
                if (data == null) continue;
                // A backdrop with no readable file would paint the whole frame in its tint colour.
                // Leave it as the world-space quad it used to be, which at least stays out of the way --
                // but say so, because a silent fallback is indistinguishable from the feature not working.
                bool backdrop = Backdrop.IsBackdrop(data) && !string.IsNullOrEmpty(data.sourcePath) && File.Exists(data.sourcePath);
                if (Backdrop.IsBackdrop(data) && !backdrop)
                    Debug.LogWarning("Scene object '" + data.id + "' asks for a full-frame backdrop but its source is unreadable: " +
                        (string.IsNullOrEmpty(data.sourcePath) ? "(no sourcePath)" : data.sourcePath) +
                        ". Falling back to a world-space quad.");
                var go = CreateObject(data);
                if (go == null) continue;
                go.name = string.IsNullOrEmpty(data.name) ? data.id : data.name;
                go.transform.SetParent(backdrop ? camera.transform : sceneRoot, false);
                Material material = CreateMaterial(data, backdrop);
                if (backdrop) material.renderQueue = Backdrop.Queue + backdrops++;
                foreach (var renderer in go.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = material;
                    if (!backdrop) continue;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
                VideoPlayer video = null;
                if (data.kind == "video" && !string.IsNullOrEmpty(data.sourcePath) && File.Exists(data.sourcePath))
                {
                    video = go.AddComponent<VideoPlayer>(); video.source = VideoSource.Url; video.url = data.sourcePath;
                    video.renderMode = VideoRenderMode.MaterialOverride; video.targetMaterialRenderer = go.GetComponent<Renderer>();
                    video.targetMaterialProperty = "_MainTex"; video.audioOutputMode = VideoAudioOutputMode.None;
                    video.isLooping = true; video.playOnAwake = false; video.skipOnDrop = true; video.Prepare();
                }
                scenes.Add(new SceneRuntime { data = data, transform = go.transform, material = material, video = video, backdrop = backdrop });
            }
        }

        GameObject CreateObject(SceneObjectData data)
        {
            if (data.kind == "obj" && !string.IsNullOrEmpty(data.sourcePath) && File.Exists(data.sourcePath))
            {
                var mesh = LoadObj(data.sourcePath); if (mesh == null) return null;
                var go = new GameObject("Imported OBJ", typeof(MeshFilter), typeof(MeshRenderer));
                go.GetComponent<MeshFilter>().sharedMesh = mesh; return go;
            }
            PrimitiveType type = data.kind == "sphere" ? PrimitiveType.Sphere : data.kind == "cylinder" ? PrimitiveType.Cylinder :
                data.kind == "plane" || data.kind == "image" || data.kind == "video" ? PrimitiveType.Quad : PrimitiveType.Cube;
            var primitive = GameObject.CreatePrimitive(type); UnityEngine.Object.Destroy(primitive.GetComponent<Collider>()); return primitive;
        }

        Material CreateMaterial(SceneObjectData data, bool backdrop)
        {
            var material = Own(new Material(Shader.Find(backdrop ? "GeometryRhythm/Backdrop" :
                data.kind == "image" ? "Unlit/Transparent" : data.kind == "video" ? "Unlit/Texture" : "Standard")));
            material.color = data.color;
            if (!backdrop && data.kind != "image") { material.SetFloat("_Glossiness", .25f); material.SetFloat("_Metallic", .12f); }
            if (data.kind == "image" && !string.IsNullOrEmpty(data.sourcePath) && File.Exists(data.sourcePath))
            {
                var texture = Own(new Texture2D(2, 2, TextureFormat.RGBA32, false));
                if (texture.LoadImage(File.ReadAllBytes(data.sourcePath))) material.mainTexture = texture;
            }
            return material;
        }

        void BuildEffects()
        {
            if (chart.effectClips == null) return;
            foreach (var data in chart.effectClips)
            {
                if (data == null) continue;
                var runtime = new EffectRuntime { data = data };
                runtime.root = new GameObject("Effect / " + data.name).transform; runtime.root.SetParent(effectRoot, false);
                if (data.kind == "shockwave")
                {
                    var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); UnityEngine.Object.Destroy(ring.GetComponent<Collider>());
                    ring.transform.SetParent(runtime.root, false); ring.transform.localScale = new Vector3(1, .015f, 1);
                    ring.GetComponent<Renderer>().sharedMaterial = SimpleMaterial(data.color);
                }
                else if (data.kind == "particles" || data.kind == "speedLines")
                {
                    var rng = new System.Random(data.seed);
                    for (int i = 0; i < 18; i++)
                    {
                        var particle = GameObject.CreatePrimitive(data.kind == "speedLines" ? PrimitiveType.Cube : PrimitiveType.Sphere);
                        UnityEngine.Object.Destroy(particle.GetComponent<Collider>()); particle.transform.SetParent(runtime.root, false);
                        particle.transform.localPosition = new Vector3((float)rng.NextDouble() * 24 - 12, (float)rng.NextDouble() * 14 - 7, (float)rng.NextDouble() * 50);
                        particle.transform.localScale = data.kind == "speedLines" ? new Vector3(.035f, .035f, 2.8f) : Vector3.one * (.06f + (float)rng.NextDouble() * .15f);
                        particle.GetComponent<Renderer>().sharedMaterial = SimpleMaterial(data.color); runtime.particles.Add(particle.transform); runtime.particleBase.Add(particle.transform.localPosition);
                    }
                }
                runtime.root.gameObject.SetActive(false); effects.Add(runtime);
            }
        }

        Material SimpleMaterial(Color color)
        {
            var material = Own(new Material(Shader.Find("Sprites/Default"))); material.color = color; return material;
        }

        public void Evaluate(double seconds)
        {
            float beat = (float)tempo.BeatAtSeconds(seconds);
            float tick = beat * chart.ticksPerBeat;
            foreach (var scene in scenes)
            {
                var d = scene.data; float t = (float)seconds * d.animationSpeed;
                if (scene.backdrop) PlaceBackdrop(scene);
                else
                {
                    scene.transform.localPosition = d.position;
                    scene.transform.localRotation = Quaternion.Euler(d.rotation);
                    scene.transform.localScale = d.scale;
                    if (d.animation == "float") scene.transform.localPosition += Vector3.up * Mathf.Sin(t * Mathf.PI * 2) * d.animationAmount;
                    else if (d.animation == "rotate") scene.transform.localRotation *= Quaternion.Euler(0, t * 45 * d.animationAmount, 0);
                    else if (d.animation == "pulse") scene.transform.localScale *= 1 + Mathf.Sin(t * Mathf.PI * 2) * .12f * d.animationAmount;
                    else if (d.animation == "pendulum") scene.transform.localRotation *= Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2) * 20 * d.animationAmount);
                }
                ScrubVideo(scene, seconds);
            }
            RenderSettings.fog = originalFogEnabled; RenderSettings.fogColor = originalFog;
            RenderSettings.fogEndDistance = originalFogEnd; RenderSettings.ambientLight = originalAmbient;
            effectLight.enabled = false; sceneRoot.localScale = Vector3.one;
            Color overlayColor = Color.clear;
            foreach (var effect in effects)
            {
                var d = effect.data;
                bool active = d.durationTicks > 0 && tick >= d.startTick && tick <= d.startTick + d.durationTicks;
                effect.root.gameObject.SetActive(active); if (!active) continue;
                float q = Mathf.Clamp01((tick - d.startTick) / d.durationTicks);
                float envelope = d.easing == "linear" ? 1 : d.easing == "impact" ? Mathf.Exp(-6 * q) : Mathf.Sin(q * Mathf.PI);
                float amount = Mathf.Max(0, d.intensity) * envelope;
                if (d.kind == "flash" || d.kind == "color" || d.kind == "glitch")
                {
                    float flicker = d.kind == "glitch" ? (.35f + .65f * Mathf.Abs(Mathf.Sin(q * 91 + d.seed))) : 1;
                    Color c = d.color; c.a = Mathf.Max(overlayColor.a, Mathf.Clamp01(amount * (d.kind == "color" ? .28f : .72f) * flicker));
                    overlayColor = Color.Lerp(overlayColor, c, c.a);
                }
                else if (d.kind == "fog") { RenderSettings.fog = true; RenderSettings.fogColor = Color.Lerp(originalFog, d.color, Mathf.Clamp01(amount)); RenderSettings.fogEndDistance = Mathf.Lerp(originalFogEnd, 28, Mathf.Clamp01(amount)); }
                else if (d.kind == "light") { effectLight.enabled = true; effectLight.color = d.color; effectLight.intensity = amount * 2.5f; effectLight.transform.rotation = Quaternion.Euler(25, q * 300 - 150, 0); }
                else if (d.kind == "scenePulse")
                {
                    float pulse = 1 + amount * .08f * Mathf.Sin(q * Mathf.PI * 2 * Mathf.Max(1, d.frequency));
                    var target = scenes.Find(s => s.data.id == d.target);
                    if (target != null) target.transform.localScale *= pulse; else sceneRoot.localScale *= pulse;
                }
                else if (d.kind == "shockwave")
                {
                    effect.root.position = spatial.Point(chart.paths[0].id, SpatialDirector.NearDepth, seconds);
                    effect.root.rotation = spatial.RouteRotationAt(spatial.DistanceAtTime(seconds) + SpatialDirector.NearDepth);
                    effect.root.localScale = Vector3.one * Mathf.Lerp(.2f, 10, q) * Mathf.Max(.1f, d.intensity);
                }
                else if (d.kind == "particles" || d.kind == "speedLines")
                {
                    var target = scenes.Find(s => s.data.id == d.target);
                    effect.root.position = target != null ? target.transform.position : camera.transform.position + camera.transform.forward * 8;
                    effect.root.rotation = target != null ? target.transform.rotation : camera.transform.rotation;
                    for (int i = 0; i < effect.particles.Count; i++)
                    {
                        float travel = q * (d.kind == "speedLines" ? 42 : 8) * Mathf.Max(.1f, d.intensity);
                        effect.particles[i].localPosition = effect.particleBase[i] + Vector3.back * travel;
                    }
                }
            }
            overlay.gameObject.SetActive(overlayColor.a > .002f);
            if (overlay.gameObject.activeSelf)
            {
                float z = camera.nearClipPlane + .025f, height = 2 * z * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
                overlay.localPosition = new Vector3(0, 0, z); overlay.localRotation = Quaternion.identity;
                overlay.localScale = new Vector3(height * camera.aspect, height, 1); overlayMaterial.color = overlayColor;
            }
        }

        void PlaceBackdrop(SceneRuntime scene)
        {
            float distance = Backdrop.Distance(camera);
            Vector2 scale = Backdrop.Scale(camera.fieldOfView, camera.aspect, SourceAspect(scene), distance);
            scene.transform.localPosition = new Vector3(0, 0, distance);
            scene.transform.localRotation = Quaternion.identity;
            scene.transform.localScale = new Vector3(scale.x, scale.y, 1);
        }

        /// <summary>Zero until the source is readable; a video reports its size only once prepared.</summary>
        static float SourceAspect(SceneRuntime scene)
        {
            if (scene.video != null && scene.video.isPrepared && scene.video.height > 0)
                return scene.video.width / (float)scene.video.height;
            var texture = scene.material == null ? null : scene.material.mainTexture;
            return texture != null && texture.height > 0 ? texture.width / (float)texture.height : 0;
        }

        /// <summary>Chart time owns the clock, but the decoder has to be allowed to run: Prepare()
        /// alone hands nothing to a MaterialOverride target, and seeking every frame never settles
        /// into steady playback. So the player runs while the chart advances and is corrected only
        /// when it actually drifts, and is pinned to an exact frame whenever the chart is not
        /// moving -- paused, seeking, or restarting. Seek and playback land on the same frame
        /// either way, which is what the deterministic-pose rule elsewhere in the runtime wants.</summary>
        static void ScrubVideo(SceneRuntime scene, double seconds)
        {
            var video = scene.video;
            if (video == null || !video.isPrepared || video.length <= 0) return;
            double target = seconds % video.length;
            // A backwards or large jump is a seek, not playback, however small the forward case is.
            bool advancing = seconds > scene.videoTime && seconds - scene.videoTime < .5;
            scene.videoTime = seconds;
            if (!advancing)
            {
                if (video.isPlaying) video.Pause();
                if (Math.Abs(video.time - target) > .05) video.time = target;
                return;
            }
            if (!video.isPlaying) video.Play();
            // Well inside a frame at any sane rate, so this corrects drift without chasing it.
            if (Math.Abs(video.time - target) > .25) video.time = target;
        }

        public bool TryPickSceneObject(Ray ray, out SceneObjectData picked)
        {
            picked = null; float nearest = float.PositiveInfinity;
            foreach (var scene in scenes)
            {
                if (scene.backdrop || scene.transform == null || !scene.transform.gameObject.activeInHierarchy) continue;
                bool hit = false; float distance = float.PositiveInfinity;
                foreach (var renderer in scene.transform.GetComponentsInChildren<Renderer>())
                {
                    Bounds bounds = renderer.bounds;
                    if (bounds.extents.x < .08f || bounds.extents.y < .08f || bounds.extents.z < .08f)
                        bounds.Expand(new Vector3(Mathf.Max(0, .16f - bounds.size.x), Mathf.Max(0, .16f - bounds.size.y), Mathf.Max(0, .16f - bounds.size.z)));
                    if (bounds.IntersectRay(ray, out float candidate) && candidate >= 0 && candidate < distance)
                    { distance = candidate; hit = true; }
                }
                if (hit && distance < nearest) { nearest = distance; picked = scene.data; }
            }
            return picked != null;
        }

        Mesh LoadObj(string path)
        {
            try
            {
                var source = new List<Vector3>(); var vertices = new List<Vector3>(); var triangles = new List<int>();
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("v "))
                    {
                        string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        source.Add(new Vector3(float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture), float.Parse(p[3], CultureInfo.InvariantCulture)));
                    }
                    else if (line.StartsWith("f "))
                    {
                        string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 2; i + 1 < p.Length; i++)
                        {
                            int[] ids = { ObjIndex(p[1], source.Count), ObjIndex(p[i], source.Count), ObjIndex(p[i + 1], source.Count) };
                            foreach (int id in ids) { vertices.Add(source[id]); triangles.Add(vertices.Count - 1); }
                        }
                    }
                }
                if (triangles.Count == 0) return null;
                var mesh = Own(new Mesh { name = Path.GetFileNameWithoutExtension(path), indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 });
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            }
            catch { return null; }
        }
        static int ObjIndex(string token, int count)
        {
            int slash = token.IndexOf('/'); if (slash >= 0) token = token.Substring(0, slash);
            int value = int.Parse(token, CultureInfo.InvariantCulture); return value < 0 ? count + value : value - 1;
        }

        public void Dispose()
        {
            RenderSettings.fog = originalFogEnabled; RenderSettings.fogColor = originalFog;
            RenderSettings.fogEndDistance = originalFogEnd; RenderSettings.ambientLight = originalAmbient;
            if (camera != null) { camera.clearFlags = originalClearFlags; camera.backgroundColor = originalBackgroundColor; }
            if (overlay != null) UnityEngine.Object.Destroy(overlay.gameObject);
            // Backdrops hang off the camera rather than off root, so root alone does not take them down.
            foreach (var scene in scenes) if (scene.backdrop && scene.transform != null) UnityEngine.Object.Destroy(scene.transform.gameObject);
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            foreach (var item in owned) if (item != null) UnityEngine.Object.Destroy(item);
        }
    }
}
