using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace GeometryRhythm.Thart.Editor
{
    /// <summary>
    /// Thart 电脑端 - 铺面 3D 预览
    ///
    /// 模仿 Polytone 的第一人称视角：
    /// 1. 相机贴在判定面正前方、和音符同一高度平视，音符收敛到画面中心的消失点；
    /// 2. 音符是正方形，从远处（画面中心）飞向判定面，越近越大；
    /// 3. 每个音符在判定面上配一个「白色收缩标识」，越接近越小，到达时正好套住音符；
    /// 4. 靠距离雾把远端音符溶进背景，形成纵深。
    ///
    /// 预览里只有音符和判定框，不画任何轨道/隧道几何体。
    /// 预览几何体只在「预览」模式存在/可见，且单独放在一个图层上，主摄像机不渲染它。
    /// 音符位置完全按铺面里每条路径的真实摆放（PathPlacement.x/y）来放，不做任何对齐。
    /// </summary>
    public sealed partial class ThartEditorController
    {
        private Camera previewCam;
        private Transform previewRoot;
        private Transform[] notePool;
        // Polytone 式「白色收缩判定框」：每个音符一个方框，越接近判定面越小
        private Transform[] framePool;

        private Material matMarker;
        private Material matTap;
        private Material matDrag;
        private Material matProtected;

        private bool previewBuilt;
        private string layoutSignatureBuilt = "";
        private int previewLayer = -1;
        private int savedMainCullingMask;
        private bool savedMainCullingMaskValid;

        // 预览里音符是正方形（Polytone 的样子），边长跟着列间距自适应
        private float previewNoteSize = 1.2f;
        // 场地横向留白：取景时在真实列范围两侧各留一点，别贴边
        private float previewFieldPad = 3.2f;

        // 铺面实际的场地范围（重建轨道时算好，相机每帧按它取景）
        private float previewCenterX;
        private float previewCenterY;
        private float previewHalfW = 6f;
        private float previewHalfH = 3f;
        private float previewCamDist = 12f;

        // 距离雾的两个深度阈值（视空间），每帧跟着相机距离与音符飞行距离更新
        private float previewFadeNear = 12f;
        private float previewFadeFar = 40f;

        // 预览渲染到这张贴图，再由 OnGUI 画进主视图区域。
        // 这样整个屏幕的绘制权都归 OnGUI，窗口被拉成任意比例都不会露出没画到的边。
        private RenderTexture previewRT;

        private const int PreviewNotePool = 400;
        private const int PreviewFramePool = 96;
        private const float BaseNoteSize = 1.2f;
        private const float MarkerThickness = 0.075f;
        // Polytone 是「平视隧道」：视野偏广，音符从远处中心飞来
        private const float PreviewFov = 68f;
        private const float LaneLength = 260f;
        // 判定框相对判定面往相机方向挪一点，避免和音符 z-fighting（相机在 -z 侧，所以实际取 -MarkerZ）
        private const float MarkerZ = 0.35f;
        private static readonly Color FogColor = new Color(0.035f, 0.04f, 0.058f, 1f);

        /// <summary>预览是否显示（仅在预览模式且已构建成功时）</summary>
        private bool PreviewVisible
        {
            get { return previewBuilt && mode == ThartEditorMode.Preview; }
        }

        #region 构建

        private void SetupPreview()
        {
            if (previewBuilt) return;

            Shader shader = Shader.Find("Thart/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[Thart] 没有可用的着色器，3D 预览不可用");
                return;
            }

            previewLayer = FindFreeLayer();

            matMarker = MakePreviewMaterial(shader, new Color(1f, 1f, 1f), 0.25f);
            matTap = MakePreviewMaterial(shader, Styles.NoteTap, 0.1f);
            matDrag = MakePreviewMaterial(shader, Styles.NoteDrag, 0.1f);
            matProtected = MakePreviewMaterial(shader, Styles.NoteProtected, 0.1f);

            var rootGo = new GameObject("ThartPreviewRoot");
            previewRoot = rootGo.transform;
            previewRoot.gameObject.layer = previewLayer;

            var camGo = new GameObject("ThartPreviewCamera");
            camGo.layer = previewLayer;
            camGo.transform.SetParent(previewRoot, false);
            previewCam = camGo.AddComponent<Camera>();
            previewCam.clearFlags = CameraClearFlags.SolidColor;
            previewCam.backgroundColor = FogColor;
            previewCam.fieldOfView = PreviewFov;
            previewCam.nearClipPlane = 0.1f;
            previewCam.farClipPlane = LaneLength + 200f;
            previewCam.depth = 1;
            previewCam.cullingMask = 1 << previewLayer;
            previewCam.enabled = false;

            // 主摄像机不渲染预览图层：这才是那些灰色几何体消失的原因
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                savedMainCullingMask = mainCam.cullingMask;
                savedMainCullingMaskValid = true;
                mainCam.cullingMask = savedMainCullingMask & ~(1 << previewLayer);
            }

            // 音符对象池
            notePool = new Transform[PreviewNotePool];
            for (int i = 0; i < PreviewNotePool; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "note" + i;
                go.layer = previewLayer;
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.SetParent(previewRoot, false);
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = matTap;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                notePool[i] = go.transform;
            }

            // 判定框对象池：每个框 = 4 根细方条拼成的空心矩形
            framePool = new Transform[PreviewFramePool];
            for (int i = 0; i < PreviewFramePool; i++)
            {
                var root = new GameObject("marker" + i);
                root.layer = previewLayer;
                root.transform.SetParent(previewRoot, false);

                for (int b = 0; b < 4; b++)
                {
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bar.name = "bar" + b;
                    bar.layer = previewLayer;
                    var bc = bar.GetComponent<Collider>();
                    if (bc != null) Destroy(bc);
                    bar.transform.SetParent(root.transform, false);
                    var br = bar.GetComponent<MeshRenderer>();
                    br.sharedMaterial = matMarker;
                    br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    br.receiveShadows = false;
                }

                root.SetActive(false);
                framePool[i] = root.transform;
            }

            previewBuilt = true;
            previewRoot.gameObject.SetActive(false);
            RebuildPreviewLayout();
        }

        private static Material MakePreviewMaterial(Shader shader, Color color, float emission)
        {
            var m = new Material(shader) { color = color };
            m.SetColor("_FadeColor", FogColor);
            m.SetFloat("_Emission", emission);
            m.SetFloat("_FadeStrength", 1f);
            return m;
        }

        /// <summary>找一个没有被使用的图层，用于把预览几何体与主摄像机隔离</summary>
        private static int FindFreeLayer()
        {
            for (int i = 31; i >= 24; i--)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;
            }
            return 31;
        }

        /// <summary>铺面列布局的指纹，变化时重建轨道</summary>
        private string CurrentLayoutSignature()
        {
            var sb = new StringBuilder();
            int count = chart != null && chart.paths != null ? chart.paths.Length : 0;
            sb.Append(count);
            if (chart != null && chart.sections != null && chart.sections.Length > 0 &&
                chart.sections[0].placements != null)
            {
                foreach (var p in chart.sections[0].placements)
                {
                    sb.Append('|').Append(Mathf.RoundToInt(p.x * 100f))
                      .Append(',').Append(Mathf.RoundToInt(p.y * 100f));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 按当前铺面的真实路径位置算出场地范围、音符尺寸与取景参数。
        /// 预览不画轨道几何体，这里只负责给相机和音符定标。
        /// </summary>
        private void RebuildPreviewLayout()
        {
            if (!previewBuilt) return;

            layoutSignatureBuilt = CurrentLayoutSignature();

            if (chart == null || chart.paths == null || chart.paths.Length == 0)
                return;

            var placements = chart.sections != null && chart.sections.Length > 0
                ? chart.sections[0].placements : null;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            var laneXs = new List<float>();

            for (int i = 0; i < chart.paths.Length; i++)
            {
                string id = chart.paths[i].id;
                float x = 0f, y = 0f;

                if (placements != null)
                {
                    foreach (var p in placements)
                    {
                        if (p.pathId == id) { x = p.x; y = p.y; break; }
                    }
                }

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                laneXs.Add(x);
            }

            if (minX > maxX) { minX = -5f; maxX = 5f; }
            if (minY > maxY) { minY = -1.5f; maxY = 1.5f; }

            // 音符是正方形：边长取最窄列间距的 78%，密排铺面才不会糊成一片
            previewNoteSize = BaseNoteSize;
            float minGap = float.MaxValue;
            if (laneXs.Count >= 2)
            {
                var sorted = new List<float>(laneXs);
                sorted.Sort();
                for (int i = 1; i < sorted.Count; i++)
                {
                    float gap = sorted[i] - sorted[i - 1];
                    if (gap > 0.001f && gap < minGap) minGap = gap;
                }
            }
            if (minGap < float.MaxValue)
                previewNoteSize = Mathf.Clamp(minGap * 0.78f, 0.3f, BaseNoteSize);

            // 场地横向留白：单列时给一个舒服的宽度，多列时贴合列间距
            previewFieldPad = minGap < float.MaxValue
                ? Mathf.Max(previewNoteSize * 1.4f, minGap)
                : previewNoteSize * 2.6f;

            previewCenterX = (minX + maxX) * 0.5f;
            previewCenterY = (minY + maxY) * 0.5f;
            previewHalfW = Mathf.Max(2f, (maxX - minX) * 0.5f + previewFieldPad * 0.5f);
            previewHalfH = Mathf.Max(1.6f, (maxY - minY) * 0.5f + previewNoteSize * 1.6f);
        }

        private void DestroyPreview()
        {
            if (savedMainCullingMaskValid && previewLayer >= 0)
            {
                var mainCam = Camera.main;
                if (mainCam != null) mainCam.cullingMask = savedMainCullingMask;
                savedMainCullingMaskValid = false;
            }

            if (previewRoot != null)
                Destroy(previewRoot.gameObject);
            previewRoot = null;

            if (previewRT != null)
            {
                if (previewCam != null) previewCam.targetTexture = null;
                previewRT.Release();
                Destroy(previewRT);
                previewRT = null;
            }

            previewCam = null;
            notePool = null;
            framePool = null;
            previewBuilt = false;
        }

        #endregion

        #region 每帧更新

        private void UpdatePreview()
        {
            if (!previewBuilt || previewRoot == null) return;

            bool want = PreviewVisible;

            // 非预览模式把整棵预览对象隐藏掉：主摄像机看不到，界面就干净了
            if (previewRoot.gameObject.activeSelf != want)
                previewRoot.gameObject.SetActive(want);

            if (previewCam != null && previewCam.enabled != want)
                previewCam.enabled = want;
            if (!want) return;

            EnsurePreviewTarget();

            if (layoutSignatureBuilt != CurrentLayoutSignature())
                RebuildPreviewLayout();

            UpdatePreviewNotes();
        }

        /// <summary>
        /// 保证预览摄像机渲染到一张和主视图等大的贴图上。
        /// 之前是直接改摄像机的 viewport rect，但那要求「屏幕别的地方一定被别的东西画满」，
        /// 一旦布局和实际渲染尺寸对不上就会露出黑边；渲染到贴图后这个前提就不需要了。
        ///
        /// 贴图固定 16:9：游玩界面就是 16:9，预览按同一个比例取景，
        /// 看到的形状才和实际游玩一致（主视图不是 16:9 时两侧留黑边）。
        /// </summary>
        private void EnsurePreviewTarget()
        {
            if (previewCam == null) return;

            Rect r = GetMainViewFieldPixelRect();
            int w = Mathf.Clamp(Mathf.RoundToInt(r.width), 16, 8192);
            int h = Mathf.Clamp(Mathf.RoundToInt(r.height), 16, 8192);

            if (previewRT == null || previewRT.width != w || previewRT.height != h)
            {
                if (previewRT != null)
                {
                    previewCam.targetTexture = null;
                    previewRT.Release();
                    Destroy(previewRT);
                }

                previewRT = new RenderTexture(w, h, 24, RenderTextureFormat.Default);
                previewRT.name = "ThartPreviewRT";
                previewRT.wrapMode = TextureWrapMode.Clamp;
                previewRT.filterMode = FilterMode.Bilinear;
                previewCam.targetTexture = previewRT;
                previewCam.rect = new Rect(0f, 0f, 1f, 1f);
            }

            UpdatePreviewCamera(w, h);
        }

        /// <summary>
        /// Polytone 式第一人称取景：相机贴在判定面正前方、和音符同一高度平视。
        /// 距离按「铺面实际宽高 + 当前视口宽高比」现算，保证整片场地刚好落在画面内，
        /// 音符在远处收敛到画面中心，越近越大。
        /// </summary>
        private void UpdatePreviewCamera(int rtW, int rtH)
        {
            if (previewCam == null) return;

            float aspect = rtH > 0 ? rtW / (float)rtH : 1.7778f;
            float vHalf = PreviewFov * 0.5f * Mathf.Deg2Rad;
            float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * Mathf.Max(0.2f, aspect));

            float needW = previewHalfW + previewNoteSize * 0.5f + 1.2f;
            float needH = previewHalfH + previewNoteSize + 0.8f;

            float dW = needW / Mathf.Max(0.05f, Mathf.Tan(hHalf));
            float dH = needH / Mathf.Max(0.05f, Mathf.Tan(vHalf));
            previewCamDist = Mathf.Max(dW, dH);

            // 相机与场地中心同高，音符正好落在画面正中
            float camY = previewCenterY;

            // 平视：相机退到判定面后方沿 +Z 望向远处。
            // 注意 Unity 摄像机默认看向自身 +Z，所以相机必须放在负 z，
            // 否则铺面（负 z）会整个落在相机背后。
            previewCam.transform.position = new Vector3(previewCenterX, camY, -previewCamDist);
            previewCam.transform.rotation = Quaternion.identity;

            // 距离雾：起雾点放在音符飞行距离的 60%，远端音符缓缓溶进背景。
            float approach = chart != null ? Mathf.Max(0.6f, chart.approachSeconds) : 1.5f;
            float spawnZ = approach * Mathf.Max(2f, scrollSpeed);
            previewFadeNear = previewCamDist + spawnZ * 0.6f;
            previewFadeFar = previewCamDist + spawnZ * 3.4f;
            ApplyPreviewFade();
        }

        private void ApplyPreviewFade()
        {
            SetFade(matTap, 1f);
            SetFade(matDrag, 1f);
            SetFade(matProtected, 1f);
            // 判定框是「白色收缩标识」，永远保持亮白，不能被雾吃掉
            SetFade(matMarker, 0f);
        }

        private void SetFade(Material m, float strength)
        {
            if (m == null) return;
            m.SetColor("_FadeColor", FogColor);
            m.SetFloat("_FadeNear", previewFadeNear);
            m.SetFloat("_FadeFar", previewFadeFar);
            m.SetFloat("_FadeStrength", strength);
        }

        /// <summary>预览贴图（非预览模式或尚未构建时为 null）</summary>
        private Texture PreviewTexture
        {
            get { return PreviewVisible ? previewRT : null; }
        }

        private void UpdatePreviewNotes()
        {
            if (notePool == null) return;

            for (int i = 0; i < notePool.Length; i++)
            {
                if (notePool[i] != null && notePool[i].gameObject.activeSelf)
                    notePool[i].gameObject.SetActive(false);
            }
            if (framePool != null)
            {
                for (int i = 0; i < framePool.Length; i++)
                {
                    if (framePool[i] != null && framePool[i].gameObject.activeSelf)
                        framePool[i].gameObject.SetActive(false);
                }
            }

            if (chart == null || chart.notes == null || chart.notes.Length == 0 || tempo == null)
                return;

            float approach = Mathf.Max(0.6f, chart.approachSeconds);
            float speed = Mathf.Max(2f, scrollSpeed);

            var placements = chart.sections != null && chart.sections.Length > 0
                ? chart.sections[0].placements : null;

            int used = 0;
            int framesUsed = 0;
            for (int i = 0; i < chart.notes.Length && used < notePool.Length; i++)
            {
                var note = chart.notes[i];
                float beat = note.tick / (float)chart.ticksPerBeat;
                double noteTime = tempo.SecondsAtBeat(beat);
                double dt = noteTime - songTime;

                if (dt > approach || dt < -0.15) continue;

                float x = 0f, y = 0f;
                if (placements != null)
                {
                    foreach (var p in placements)
                    {
                        if (p.pathId == note.pathId) { x = p.x; y = p.y; break; }
                    }
                }

                // 音符从远处（+z）飞向判定面（z=0）
                float z = (float)dt * speed;

                var t = notePool[used];
                if (t == null) break;

                t.gameObject.SetActive(true);
                // 就放在路径的真实摆放位置上，不做任何对齐/吸附
                t.localPosition = new Vector3(x, y, z);
                // Polytone 式正方形音符
                t.localScale = new Vector3(previewNoteSize, previewNoteSize, previewNoteSize * 0.6f);

                var r = t.GetComponent<MeshRenderer>();
                if (r != null)
                {
                    r.sharedMaterial = note.protectedNote
                        ? matProtected
                        : (note.action == "tap" ? matTap : matDrag);
                }

                // Polytone 式收缩判定框：钉在判定面上，音符越近框越小，到达时正好套住音符
                if (framePool != null && framesUsed < framePool.Length)
                {
                    float progress = Mathf.Clamp01(1f - (float)(dt / approach));
                    ShowPreviewFrame(framesUsed, x, y, progress);
                    framesUsed++;
                }

                used++;
            }
        }

        /// <summary>
        /// 在判定面上画一个随时间收缩的白色方框（4 根细方条拼成的空心矩形）。
        /// progress 0 = 音符刚出现在远处（框最大），1 = 音符抵达判定面（框收到和音符同大）。
        /// </summary>
        private void ShowPreviewFrame(int index, float x, float y, float progress)
        {
            var root = framePool[index];
            if (root == null) return;

            root.gameObject.SetActive(true);
            root.localPosition = new Vector3(x, y, -MarkerZ);
            root.localScale = Vector3.one;

            // 先快后慢：越接近判定面收得越慢，看起来是「套住」而不是缩没
            float ease = 1f - Mathf.Pow(1f - progress, 3f);
            float side = Mathf.Lerp(previewNoteSize * 2.6f, previewNoteSize, ease);

            float half = Mathf.Max(0.01f, side * 0.5f);
            float tk = MarkerThickness;

            SetFrameBar(root.GetChild(0), new Vector3(0f, half, 0f), new Vector3(side, tk, tk));
            SetFrameBar(root.GetChild(1), new Vector3(0f, -half, 0f), new Vector3(side, tk, tk));
            SetFrameBar(root.GetChild(2), new Vector3(-half, 0f, 0f), new Vector3(tk, side, tk));
            SetFrameBar(root.GetChild(3), new Vector3(half, 0f, 0f), new Vector3(tk, side, tk));
        }

        private static void SetFrameBar(Transform bar, Vector3 pos, Vector3 scale)
        {
            if (bar == null) return;
            bar.localPosition = pos;
            bar.localScale = scale;
        }

        #endregion
    }
}