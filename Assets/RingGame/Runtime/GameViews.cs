using System;
using System.Collections.Generic;
using RingGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RingGame.Runtime
{
    // Every note primitive lives in chart space and shares the same stage transform.
    internal sealed class NoteView
    {
        readonly GameObject root;
        readonly SpriteRenderer target, moving;
        readonly LineRenderer ring, approach, path;
        readonly Color color;

        public NoteView(Transform stage, CompiledNote note, Sprite disc, Material lineMaterial)
        {
            root = new GameObject(note.Id); root.transform.SetParent(stage, false);
            color = note.Motion == NoteMotion.Arrival ? new Color(1f, .64f, .32f) : new Color(.25f, .88f, 1f);
            target = Disc("Target", root.transform, disc, new Color(color.r, color.g, color.b, .16f), 1);
            moving = Disc("Arrival", root.transform, disc, new Color(color.r, color.g, color.b, .6f), 3);
            ring = Line("Receiving ring", root.transform, lineMaterial, color, .58f, 4);
            approach = Line("Approach", root.transform, lineMaterial, color, .38f, 2);
            path = Line("Path", root.transform, lineMaterial, new Color(color.r, color.g, color.b, .25f), .24f, 0);
            path.loop = false;
            path.positionCount = 2;
            path.SetPositions(new[] { (Vector3)note.PathStart, (Vector3)note.Target });
        }

        public void Draw(CompiledNote note, double visualTime, NoteState state, double lateTail)
        {
            var value = NoteEvaluator.Evaluate(note, visualTime);
            // Keep the receiving outline through the late window; never auto-score at alignment.
            bool visible = visualTime >= note.SpawnSeconds && visualTime <= note.HitSeconds+lateTail && state == NoteState.Pending;
            root.SetActive(visible);
            if (!visible) return;
            SetDisc(target, note.Target, note.Radius);
            SetCircle(ring, note.Target, note.Radius);
            bool arrival = note.Motion == NoteMotion.Arrival;
            moving.gameObject.SetActive(arrival);
            path.gameObject.SetActive(arrival);
            approach.gameObject.SetActive(!arrival);
            if (arrival) SetDisc(moving, value.MovingCenter, value.MovingRadius);
            else SetCircle(approach, note.Target, value.ApproachRadius);
        }

        public void Destroy() { UnityEngine.Object.Destroy(root); }

        internal static SpriteRenderer Disc(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = sprite; r.color = color; r.sortingOrder = order;
            return r;
        }

        internal static LineRenderer Line(string name, Transform parent, Material material, Color color, float width, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var r = go.AddComponent<LineRenderer>(); r.useWorldSpace = false; r.sharedMaterial = material;
            r.startColor = r.endColor = color; r.startWidth = r.endWidth = width; r.loop = true;
            r.sortingOrder = order; r.numCornerVertices = 2; r.numCapVertices = 2;
            return r;
        }

        internal static void SetDisc(SpriteRenderer r, Vector2 point, float radius)
        { r.transform.localPosition = point; r.transform.localScale = Vector3.one * (2 * radius); }

        internal static void SetCircle(LineRenderer r, Vector2 center, float radius)
        {
            const int segments = 72;
            r.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 2 * Mathf.PI / segments;
                r.SetPosition(i, new Vector3(center.x + radius * Mathf.Cos(angle), center.y + radius * Mathf.Sin(angle), 0));
            }
        }
    }

    internal sealed class GameHud
    {
        internal sealed class Control { public RectTransform Rect; public Text Text; public Action Action; }
        readonly RectTransform canvas;
        readonly Font font;
        readonly List<Control> controls = new List<Control>();
        public Text Title, Status, Score, Hint, Configuration;
        public Control Play, Restart, Preview;
        public readonly List<Control> Calibration = new List<Control>();
        public Rect Viewport { get; private set; }
        public float UiScale { get; private set; }

        public GameHud()
        {
            var go = new GameObject("HUD"); var c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 100; canvas = go.GetComponent<RectTransform>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Title = Label("RING / LAB", 25, new Color(.3f, .92f, 1));
            Status = Label("", 17, Color.white); Score = Label("", 17, Color.white);
            Hint = Label("", 19, new Color(.75f, .82f, .9f));
            Configuration = Label("", 14, new Color(.65f, .75f, .85f));
            Play = Button("PLAY"); Restart = Button("RESTART"); Preview = Button("PREVIEW: OFF");
            foreach (string caption in new[] { "INPUT -5", "INPUT +5", "VISUAL -5", "VISUAL +5", "DISPLAY -5", "DISPLAY +5" })
                Calibration.Add(Button(caption));
        }

        Text Label(string value, int size, Color color)
        {
            var go = new GameObject("Label"); go.transform.SetParent(canvas, false);
            var t = go.AddComponent<Text>(); t.font = font; t.text = value; t.fontSize = size; t.color = color;
            t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
            return t;
        }

        Control Button(string caption)
        {
            var go = new GameObject(caption); go.transform.SetParent(canvas, false);
            go.AddComponent<Image>().color = new Color(.12f, .2f, .29f, .95f);
            var text = Label(caption, 15, Color.white); text.transform.SetParent(go.transform, false);
            var tr = text.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero; text.alignment = TextAnchor.MiddleCenter;
            var control = new Control { Rect = go.GetComponent<RectTransform>(), Text = text };
            controls.Add(control); return control;
        }

        static void Place(RectTransform transform, Rect rect)
        {
            transform.anchorMin = transform.anchorMax = Vector2.zero; transform.pivot = Vector2.zero;
            transform.anchoredPosition = rect.position; transform.sizeDelta = rect.size;
        }

        public void Layout(bool showCalibration)
        {
            var safe = Screen.safeArea; UiScale = Mathf.Clamp(safe.height / 720f, .7f, 2.5f);
            float u = UiScale;
            Place(Title.rectTransform, new Rect(safe.x + 16*u, safe.yMax-47*u, 175*u, 32*u));
            Place(Status.rectTransform, new Rect(safe.x + 200*u, safe.yMax-47*u, safe.width-610*u, 32*u));
            Place(Play.Rect, new Rect(safe.xMax-374*u, safe.yMax-49*u, 106*u, 35*u));
            Place(Restart.Rect, new Rect(safe.xMax-258*u, safe.yMax-49*u, 112*u, 35*u));
            Place(Preview.Rect, new Rect(safe.xMax-136*u, safe.yMax-49*u, 120*u, 35*u));
            Place(Score.rectTransform, new Rect(safe.x+16*u, safe.yMax-77*u, safe.width-32*u, 24*u));
            Place(Hint.rectTransform, new Rect(safe.x+16*u, safe.y+41*u, safe.width-32*u, 29*u));
            Place(Configuration.rectTransform, new Rect(safe.x+16*u, safe.y+12*u, safe.width-32*u, 22*u));
            for (int i=0;i<Calibration.Count;i++)
            {
                Calibration[i].Rect.gameObject.SetActive(showCalibration);
                Place(Calibration[i].Rect, new Rect(safe.x+(16+120*i)*u, safe.y+78*u, 112*u, 32*u));
            }
            // Constant 16:9 chart viewport. Bars/HUD never change the chart's coordinate aspect.
            var available = new Rect(safe.x, safe.y+118*u, safe.width, Mathf.Max(100, safe.height-206*u));
            float width = Mathf.Min(available.width, available.height * 16f/9f);
            float height = width * 9f/16f;
            Viewport = new Rect(available.center.x-width/2, available.center.y-height/2, width, height);
        }

        public bool Consume(Vector2 point)
        {
            foreach (var c in controls)
                if (c.Rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(c.Rect, point))
                { c.Action?.Invoke(); return true; }
            return !Viewport.Contains(point);
        }
    }
}
