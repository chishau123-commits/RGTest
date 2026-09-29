using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeometryRhythm
{
    public static class MeshFactory
    {
        // Extruded annulus: front/back plus inner/outer walls. Shared by all note instances.
        public static Mesh Ring(float inner, float outer, float thickness, int segments = 64)
        {
            var vertices = new Vector3[segments * 4];
            var triangles = new int[segments * 24];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                Vector3 r = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                vertices[4 * i] = r * outer + Vector3.forward * thickness / 2;
                vertices[4 * i + 1] = r * inner + Vector3.forward * thickness / 2;
                vertices[4 * i + 2] = r * outer - Vector3.forward * thickness / 2;
                vertices[4 * i + 3] = r * inner - Vector3.forward * thickness / 2;
                int n = ((i + 1) % segments) * 4, v = i * 4, k = i * 24;
                int[] faces = {v,n,v+1, n,n+1,v+1, v+2,v+3,n+2,n+2,v+3,n+3,
                    v,v+2,n,n,v+2,n+2, v+1,n+1,v+3,n+1,n+3,v+3};
                Array.Copy(faces, 0, triangles, k, faces.Length);
            }
            var mesh = new Mesh { name = "Shared extruded circle" };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
        public static Mesh Shard()
        {
            var m = new Mesh { name = "Shared triangular shard" };
            Vector3 a = new Vector3(-.7f,-.5f,0), b = new Vector3(.7f,-.5f,0), c = new Vector3(0,.8f,0), d = new Vector3(.1f,0,.22f);
            m.vertices = new[] { a,c,b, a,b,d, b,c,d, c,a,d };
            m.triangles = new[] {0,1,2,3,4,5,6,7,8,9,10,11};
            m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
    }

    /// <summary>Owns generated meshes/materials so domain reloads and scene restarts do not leak them.</summary>
    public sealed class VisualLibrary : IDisposable
    {
        public readonly Mesh Ring, ShellRing, Disc, Shard;
        public readonly Material Tap, Drag, TapShell, DragShell, Border, Line, Marker, Pearl, Sand, Stone, Sky, Spark;
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        public VisualLibrary()
        {
            Ring = Own(MeshFactory.Ring(.76f, 1, .095f));
            ShellRing = Own(MeshFactory.Ring(1.20f, 1.25f, .055f));
            Disc = Own(MeshFactory.Ring(0, 1.23f, .012f));
            Shard = Own(MeshFactory.Shard());
            Tap = Flat(new Color(.13f,.48f,.96f));
            Drag = Flat(new Color(.985f,.991f,1));
            Border = Flat(new Color(.62f,.70f,.77f));
            Line = Flat(new Color(.62f,.73f,.79f));
            Marker = Flat(new Color(.55f,.61f,.65f));
            Spark = Flat(new Color(1,.91f,.70f));
            TapShell = Transparent(new Color(.35f,.68f,1,.13f));
            DragShell = Transparent(new Color(1,1,1,.28f));
            Pearl = Lit(new Color(.89f,.875f,.85f));
            Sand = Lit(new Color(.82f,.80f,.765f));
            Stone = Lit(new Color(.76f,.745f,.72f));
            Sky = Own(new Material(Shader.Find("GeometryRhythm/Sky")));
        }
        T Own<T>(T value) where T : UnityEngine.Object { owned.Add(value); return value; }
        Material Flat(Color color) { var m = Own(new Material(Shader.Find("GeometryRhythm/Flat"))); m.color = color; return m; }
        Material Transparent(Color color) { var m = Own(new Material(Shader.Find("GeometryRhythm/Sleeve"))); m.color = color; return m; }
        Material Lit(Color color)
        {
            var m = Own(new Material(Shader.Find("Standard"))); m.color = color;
            m.SetFloat("_Glossiness", .08f); m.SetFloat("_Metallic", 0); return m;
        }
        public static MeshRenderer MeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; return renderer;
        }
        public void Dispose() { foreach (var obj in owned) if (obj != null) UnityEngine.Object.Destroy(obj); }
    }

    public sealed class NoteVisual
    {
        public readonly Transform Transform;
        public readonly Vector2[] HitPolygon = new Vector2[24];
        readonly MeshRenderer face, shell, rim;
        readonly GameObject backing;
        readonly VisualLibrary library;
        public NoteVisual(Transform parent, VisualLibrary library)
        {
            this.library = library;
            Transform = new GameObject("Pooled Note").transform; Transform.SetParent(parent, false);
            var outline = VisualLibrary.MeshObject("Fine edge", Transform, library.Ring, library.Border);
            outline.transform.localScale = Vector3.one * 1.035f;
            outline.transform.localPosition = new Vector3(0,0,-.015f);
            backing = outline.gameObject;
            face = VisualLibrary.MeshObject("Ring", Transform, library.Ring, library.Tap);
            shell = VisualLibrary.MeshObject("Protection sleeve", Transform, library.Disc, library.TapShell);
            shell.transform.localPosition = new Vector3(0,0,-.08f);
            rim = VisualLibrary.MeshObject("Protection rim", Transform, library.ShellRing, library.Tap);
            Transform.gameObject.SetActive(false);
        }
        public void Bind(NoteData note)
        {
            bool tap = note.action == "tap";
            face.sharedMaterial = tap ? library.Tap : library.Drag;
            rim.sharedMaterial = tap ? library.Tap : library.Border;
            shell.sharedMaterial = tap ? library.TapShell : library.DragShell;
            rim.gameObject.SetActive(note.protectedNote); shell.gameObject.SetActive(note.protectedNote);
            backing.SetActive(!tap);
            Transform.name = note.id + " / " + note.action + (note.protectedNote ? " / protected" : " / local");
            Transform.localScale = Vector3.one * 1.0f;
            Transform.gameObject.SetActive(true);
        }
        public void Release() => Transform.gameObject.SetActive(false);
    }

    public sealed class PathVisual
    {
        readonly LineRenderer line;
        readonly Transform marker;
        readonly Vector3[] vertices = new Vector3[80];
        readonly string id;
        readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        public PathVisual(string id, Transform parent, VisualLibrary library)
        {
            this.id = id;
            var go = new GameObject("Path / " + id); go.transform.SetParent(parent, false);
            line = go.AddComponent<LineRenderer>(); line.sharedMaterial = library.Line;
            line.positionCount = vertices.Length; line.useWorldSpace = true; line.widthMultiplier = .042f;
            line.numCornerVertices = 3; line.numCapVertices = 4;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            // Two small world-space timing ticks, separate from the Note skin.
            marker = new GameObject("Judgement ticks").transform; marker.SetParent(go.transform, false);
            for (int i = 0; i < 2; i++)
            {
                var tick = GameObject.CreatePrimitive(PrimitiveType.Cube); tick.name = "Timing tick";
                UnityEngine.Object.Destroy(tick.GetComponent<Collider>());
                tick.transform.SetParent(marker, false); tick.transform.localPosition = new Vector3(i == 0 ? -.95f : .95f,0,0);
                tick.transform.localScale = new Vector3(.28f,.035f,.035f);
                tick.GetComponent<Renderer>().sharedMaterial = library.Marker;
            }
        }
        public void Evaluate(SpatialDirector spatial, double time)
        {
            float visibility = spatial.Visibility(id, time);
            line.enabled = visibility > .01f; marker.gameObject.SetActive(visibility > .01f);
            if (!line.enabled) return;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = spatial.Point(id, Mathf.Lerp(SpatialDirector.NearDepth - 3, SpatialDirector.FarDepth, i / (float)(vertices.Length-1)), time);
            line.SetPositions(vertices); line.widthMultiplier = .085f * visibility;
            marker.position = spatial.Point(id, SpatialDirector.NearDepth, time);
            marker.rotation = Quaternion.LookRotation(spatial.Point(id, SpatialDirector.NearDepth + .1f,time) - marker.position, Vector3.up);
        }
    }

    public sealed class StageVisuals
    {
        struct Floater { public Transform Transform; public Vector3 Position; public Quaternion Rotation; public float Phase; }
        readonly List<Floater> floaters = new List<Floater>();
        readonly List<GameObject> chunks = new List<GameObject>();
        readonly List<float> chunkDistances = new List<float>();
        readonly VisualLibrary library;
        readonly SpatialDirector spatial;
        public StageVisuals(Transform parent, VisualLibrary library, SpatialDirector spatial)
        {
            this.library = library; this.spatial = spatial;
            var rng = new System.Random(20260917);
            for (int section = 0; section < 30; section++)
            {
                float s = section * 16 - 24;
                var root = new GameObject("World geometry / " + section);
                root.transform.SetParent(parent, false); chunks.Add(root); chunkDistances.Add(s);
                Vector3 centre = spatial.RouteAt(s);
                Quaternion frame = spatial.RouteRotationAt(s);
                for (int side = -1; side <= 1; side += 2)
                {
                    float height = 9 + (float)rng.NextDouble() * 17;
                    var block = GameObject.CreatePrimitive(PrimitiveType.Cube); block.name = "Folded monolith";
                    UnityEngine.Object.Destroy(block.GetComponent<Collider>());
                    block.transform.SetParent(root.transform, false);
                    block.transform.position = centre + frame * new Vector3(side * (25 + (float)rng.NextDouble() * 10),height/2-9,0);
                    block.transform.localScale = new Vector3(3+(float)rng.NextDouble()*8,height,2+(float)rng.NextDouble()*4);
                    block.transform.rotation = frame * Quaternion.Euler(8+(float)rng.NextDouble()*17,(float)rng.NextDouble()*40,side*(10+(float)rng.NextDouble()*23));
                    block.GetComponent<Renderer>().sharedMaterial = section % 3 == 0 ? library.Stone : library.Sand;
                }
                for (int j = 0; j < 4; j++)
                {
                    var shape = VisualLibrary.MeshObject("Floating facet",root.transform,library.Shard,j%2==0?library.Pearl:library.Sand).transform;
                    int side = j%2==0?1:-1;
                    shape.position = centre + frame * new Vector3(side*(9+(float)rng.NextDouble()*22),7+(float)rng.NextDouble()*18,(float)rng.NextDouble()*14);
                    shape.rotation = Quaternion.Euler((float)rng.NextDouble()*180,(float)rng.NextDouble()*180,(float)rng.NextDouble()*180);
                    shape.localScale = Vector3.one*(.6f+(float)rng.NextDouble()*3.5f);
                    floaters.Add(new Floater { Transform = shape, Position = shape.position, Rotation = shape.rotation, Phase = (float)rng.NextDouble()*6 });
                }
                // Ground facets lie beneath the play corridor and continue through each bend.
                var ground = VisualLibrary.MeshObject("Ground facet",root.transform,library.Shard,library.Pearl).transform;
                ground.position = centre + Vector3.down * 9;
                ground.rotation = frame * Quaternion.Euler(90,0,section*37);
                ground.localScale = new Vector3(65,40,6);
            }
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Matte ground";
            UnityEngine.Object.Destroy(floor.GetComponent<Collider>()); floor.transform.SetParent(parent,false);
            floor.transform.position = new Vector3(0,-11,200); floor.transform.localScale = new Vector3(250,1,650);
            floor.GetComponent<Renderer>().sharedMaterial = library.Pearl;
        }
        public void Evaluate(double time, double beat)
        {
            float s = spatial.DistanceAtTime(time);
            for (int i=0;i<chunks.Count;i++)
            {
                bool active = chunkDistances[i] > s-65 && chunkDistances[i] < s+160;
                if (chunks[i].activeSelf != active) chunks[i].SetActive(active);
            }
            foreach (var f in floaters)
            {
                if (!f.Transform.gameObject.activeInHierarchy) continue;
                float t=(float)time;
                f.Transform.position = f.Position + Vector3.up*Mathf.Sin(t*.38f+f.Phase)*.6f;
                f.Transform.rotation = f.Rotation * Quaternion.Euler(t*3,t*5,Mathf.Sin((float)beat*Mathf.PI*.5f+f.Phase)*5);
            }
        }
    }
}
