using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace GeometryRhythm
{
    /// <summary>Loads and seeks a Blender-authored, glTF-backed BGA package.</summary>
    public sealed class BlenderBgaRuntime : IDisposable
    {
        [Serializable] public sealed class Manifest
        {
            public int schemaVersion;
            public string name, sourceBlend, sourceSha256, blenderVersion, sceneName, model, timeline, renderEngine;
            public float fps, durationSeconds;
            public int frameStart, frameEnd, objectCount, cameraCount, materialCount;
        }
        [Serializable] sealed class Timeline
        {
            public float fps;
            public int frameStart, frameEnd;
            public CameraCut[] cameraCuts;
            public VisibilityTrack[] visibilityTracks;
            public LightTrack[] lightTracks;
            public float[] worldColor;
            public string worldPanorama;
            public CameraPose[] cameraTrack;
        }
        [Serializable] sealed class CameraCut { public int frame; public string value; }
        [Serializable] sealed class VisibilityTrack { public string objectName; public BoolKey[] keys; }
        [Serializable] sealed class BoolKey { public int frame; public bool value; }
        [Serializable] sealed class LightTrack { public string objectName; public LightKey[] keys; }
        [Serializable] sealed class LightKey { public int frame; public LightValue value; }
        [Serializable] sealed class LightValue { public float energy; public float[] color; public bool enabled; }
        [Serializable] sealed class CameraPose
        {
            public int frame;
            public string name;
            public float[] position, forward, up;
            public float fieldOfView, nearClip, farClip, orthographicSize;
            public bool orthographic;
        }

        public Manifest Package { get; private set; }
        public string ManifestPath { get; private set; }
        public string Status { get; private set; } = "No BGA package";
        public bool IsLoaded { get; private set; }

        readonly Transform parent;
        GameObject root;
        GltfImport importer;
        Timeline timeline;
        Animation animation;
        AnimationState animationState;
        Texture2D worldPanorama;
        Material worldMaterial;
        Material previousSkybox;
        UnityEngine.Rendering.AmbientMode previousAmbientMode;
        Color previousAmbientLight;
        bool worldApplied;
        readonly Dictionary<string, Transform> objects = new Dictionary<string, Transform>(StringComparer.Ordinal);
        readonly Dictionary<string, Camera> cameras = new Dictionary<string, Camera>(StringComparer.Ordinal);
        readonly Dictionary<string, Renderer[]> renderers = new Dictionary<string, Renderer[]>(StringComparer.Ordinal);
        readonly Dictionary<string, Light> lights = new Dictionary<string, Light>(StringComparer.Ordinal);

        public BlenderBgaRuntime(Transform parent) { this.parent = parent; }

        public async Task<bool> LoadAsync(string manifestPath, Action<string> report = null)
        {
            DisposePackage();
            try
            {
                ManifestPath = Path.GetFullPath(manifestPath);
                Package = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
                if (Package == null || string.IsNullOrWhiteSpace(Package.model) || string.IsNullOrWhiteSpace(Package.timeline))
                    throw new InvalidDataException("Invalid Blender BGA manifest");
                string directory = Path.GetDirectoryName(ManifestPath);
                string modelPath = Path.Combine(directory, Package.model);
                string timelinePath = Path.Combine(directory, Package.timeline);
                if (!File.Exists(modelPath)) throw new FileNotFoundException("BGA 3D scene is missing", modelPath);
                if (!File.Exists(timelinePath)) throw new FileNotFoundException("BGA timeline is missing", timelinePath);
                timeline = JsonUtility.FromJson<Timeline>(File.ReadAllText(timelinePath));
                root = new GameObject("Blender BGA · " + Package.name);
                root.transform.SetParent(parent, false);
                Status = "Loading Blender 3D scene…"; report?.Invoke(Status);
                importer = new GltfImport();
                var settings = new ImportSettings
                {
                    AnimationMethod = AnimationMethod.Legacy,
                    NodeNameMethod = NameImportMethod.Original,
                    GenerateMipMaps = true
                };
                if (!await importer.LoadFile(modelPath, null, settings))
                    throw new InvalidDataException("glTFast could not decode the exported Blender scene");
                if (!await importer.InstantiateMainSceneAsync(root.transform))
                    throw new InvalidDataException("glTFast could not instantiate the exported Blender scene");
                IndexScene();
                animation = root.GetComponentInChildren<Animation>(true);
                if (animation != null && animation.clip != null)
                {
                    animation.Play(animation.clip.name);
                    animationState = animation[animation.clip.name];
                    animationState.wrapMode = WrapMode.ClampForever;
                    animationState.speed = 0;
                }
                ApplyWorld();
                IsLoaded = true;
                Status = Package.sceneName + " · " + Package.objectCount + " objects · " + Package.cameraCount + " cameras";
                report?.Invoke(Status);
                return true;
            }
            catch (Exception e)
            {
                Status = "BGA load failed: " + e.Message;
                report?.Invoke(Status); Debug.LogException(e); DisposePackage(); return false;
            }
        }

        void IndexScene()
        {
            objects.Clear(); cameras.Clear(); renderers.Clear(); lights.Clear();
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
                if (!objects.ContainsKey(item.name)) objects.Add(item.name, item);
            foreach (var pair in objects)
            {
                var camera = pair.Value.GetComponentInChildren<Camera>(true);
                if (camera != null) { camera.enabled = false; cameras[pair.Key] = camera; }
                var foundRenderers = pair.Value.GetComponentsInChildren<Renderer>(true);
                if (foundRenderers.Length > 0) renderers[pair.Key] = foundRenderers;
                var light = pair.Value.GetComponentInChildren<Light>(true);
                if (light != null) lights[pair.Key] = light;
            }
            // glTF has no Area Light type. Keep Blender's animated light object and
            // approximate unsupported softboxes with a Unity point light.
            if (timeline?.lightTracks != null)
                foreach (var track in timeline.lightTracks)
                    if (!lights.ContainsKey(track.objectName) && objects.TryGetValue(track.objectName, out Transform lightObject))
                    {
                        var fallback = lightObject.gameObject.AddComponent<Light>();
                        fallback.type = LightType.Point; fallback.range = 24; fallback.shadows = LightShadows.Soft;
                        lights[track.objectName] = fallback;
                    }
        }

        void ApplyWorld()
        {
            if (timeline?.worldColor == null || timeline.worldColor.Length < 3) return;
            if (!worldApplied)
            {
                previousSkybox = RenderSettings.skybox;
                previousAmbientMode = RenderSettings.ambientMode;
                previousAmbientLight = RenderSettings.ambientLight;
                worldApplied = true;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(timeline.worldColor[0], timeline.worldColor[1], timeline.worldColor[2]);
            if (string.IsNullOrWhiteSpace(timeline.worldPanorama)) return;
            string panoramaPath = Path.Combine(Path.GetDirectoryName(ManifestPath), timeline.worldPanorama);
            Shader shader = Resources.Load<Shader>("BlenderBgaPanoramicSky");
            if (!File.Exists(panoramaPath) || shader == null) return;
            worldPanorama = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
            {
                name = "Blender BGA World",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            if (!worldPanorama.LoadImage(File.ReadAllBytes(panoramaPath), false)) return;
            worldMaterial = new Material(shader) { name = "Blender BGA Skybox" };
            worldMaterial.SetTexture("_MainTex", worldPanorama);
            RenderSettings.skybox = worldMaterial;
            DynamicGI.UpdateEnvironment();
        }

        public void Evaluate(double songSeconds, Camera outputCamera, bool followCamera)
        {
            if (!IsLoaded || timeline == null) return;
            float localTime = Mathf.Clamp((float)songSeconds, 0, Package.durationSeconds);
            if (animationState != null)
            {
                animationState.time = Mathf.Min(localTime, Mathf.Max(0, animationState.length - .0001f));
                animation.Sample();
            }
            float sampleFrame = timeline.frameStart + localTime * Mathf.Max(1, timeline.fps);
            int frame = Mathf.FloorToInt(sampleFrame);
            EvaluateVisibility(frame); EvaluateLights(frame);
            if (followCamera && outputCamera != null) ApplyCamera(sampleFrame, outputCamera);
        }

        void EvaluateVisibility(int frame)
        {
            if (timeline.visibilityTracks == null) return;
            foreach (var track in timeline.visibilityTracks)
            {
                if (track.keys == null || !renderers.TryGetValue(track.objectName, out Renderer[] targets)) continue;
                bool visible = track.keys.Length == 0 || track.keys[0].value;
                for (int i = 0; i < track.keys.Length && track.keys[i].frame <= frame; i++) visible = track.keys[i].value;
                foreach (var renderer in targets) if (renderer != null) renderer.enabled = visible;
            }
        }

        void EvaluateLights(int frame)
        {
            if (timeline.lightTracks == null) return;
            foreach (var track in timeline.lightTracks)
            {
                if (track.keys == null || track.keys.Length == 0 || !lights.TryGetValue(track.objectName, out Light light)) continue;
                LightValue value = track.keys[0].value;
                for (int i = 0; i < track.keys.Length && track.keys[i].frame <= frame; i++) value = track.keys[i].value;
                light.enabled = value.enabled;
                // Blender sun strength maps directly; high-energy area/point lights need photometric scaling in Unity.
                light.intensity = value.energy > 10 ? value.energy * .01f : value.energy;
                if (value.color != null && value.color.Length >= 3) light.color = new Color(value.color[0], value.color[1], value.color[2]);
            }
        }

        void ApplyCamera(float frame, Camera output)
        {
            if (timeline.cameraTrack != null && timeline.cameraTrack.Length > 0)
            {
                ApplyBakedCamera(frame, output);
                return;
            }
            if (timeline.cameraCuts == null || timeline.cameraCuts.Length == 0) return;
            string name = timeline.cameraCuts[0].value;
            for (int i = 0; i < timeline.cameraCuts.Length && timeline.cameraCuts[i].frame <= frame; i++) name = timeline.cameraCuts[i].value;
            if (!cameras.TryGetValue(name, out Camera source)) return;
            output.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            output.orthographic = source.orthographic; output.fieldOfView = source.fieldOfView;
            output.orthographicSize = source.orthographicSize;
            output.nearClipPlane = Mathf.Max(.01f, source.nearClipPlane); output.farClipPlane = source.farClipPlane;
        }

        void ApplyBakedCamera(float frame, Camera output)
        {
            CameraPose[] track = timeline.cameraTrack;
            int index = Mathf.Clamp(Mathf.FloorToInt(frame) - timeline.frameStart, 0, track.Length - 1);
            CameraPose pose = track[index];
            CameraPose next = track[Mathf.Min(index + 1, track.Length - 1)];
            float amount = next.frame == pose.frame ? 0 : Mathf.Clamp01((frame - pose.frame) / (next.frame - pose.frame));
            Vector3 position = Vector3.Lerp(ReadVector(pose.position), ReadVector(next.position), amount);
            Quaternion rotation = Quaternion.Slerp(ReadRotation(pose), ReadRotation(next), amount);
            output.transform.SetPositionAndRotation(position, rotation);
            output.orthographic = pose.orthographic;
            output.fieldOfView = Mathf.Lerp(pose.fieldOfView, next.fieldOfView, amount);
            output.orthographicSize = Mathf.Lerp(pose.orthographicSize, next.orthographicSize, amount);
            output.nearClipPlane = Mathf.Max(.01f, Mathf.Lerp(pose.nearClip, next.nearClip, amount));
            output.farClipPlane = Mathf.Max(output.nearClipPlane + .01f, Mathf.Lerp(pose.farClip, next.farClip, amount));
            if (worldMaterial != null) output.clearFlags = CameraClearFlags.Skybox;
            else if (timeline.worldColor != null && timeline.worldColor.Length >= 3)
            {
                output.clearFlags = CameraClearFlags.SolidColor;
                output.backgroundColor = new Color(timeline.worldColor[0], timeline.worldColor[1], timeline.worldColor[2]);
            }
        }

        static Vector3 ReadVector(float[] value) => value != null && value.Length >= 3
            ? new Vector3(value[0], value[1], value[2]) : Vector3.zero;

        static Quaternion ReadRotation(CameraPose pose)
        {
            Vector3 forward = ReadVector(pose.forward);
            Vector3 up = ReadVector(pose.up);
            return forward.sqrMagnitude > .000001f && up.sqrMagnitude > .000001f
                ? Quaternion.LookRotation(forward, up) : Quaternion.identity;
        }

        public void Dispose() => DisposePackage();
        void DisposePackage()
        {
            if (worldApplied)
            {
                RenderSettings.skybox = previousSkybox;
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientLight = previousAmbientLight;
                worldApplied = false;
            }
            if (worldMaterial != null) { UnityEngine.Object.Destroy(worldMaterial); worldMaterial = null; }
            if (worldPanorama != null) { UnityEngine.Object.Destroy(worldPanorama); worldPanorama = null; }
            IsLoaded = false; animation = null; animationState = null; timeline = null; Package = null;
            objects.Clear(); cameras.Clear(); renderers.Clear(); lights.Clear();
            if (root != null) { UnityEngine.Object.Destroy(root); root = null; }
            importer?.Dispose(); importer = null;
        }
    }
}
