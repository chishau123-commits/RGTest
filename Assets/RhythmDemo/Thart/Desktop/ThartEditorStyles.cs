using UnityEngine;

namespace GeometryRhythm.Thart.Editor
{
    /// <summary>
    /// Thart 电脑端视觉样式（深色 + 圆角卡片 + 青色强调）
    /// </summary>
    internal static class Styles
    {
        private static bool initialized;

        // ======== 调色板 ========
        public static readonly Color Bg = new Color(0.055f, 0.058f, 0.078f);
        public static readonly Color BgDeep = new Color(0.035f, 0.037f, 0.052f);
        public static readonly Color Card = new Color(0.098f, 0.104f, 0.137f);
        public static readonly Color CardSoft = new Color(0.127f, 0.135f, 0.176f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.07f);
        public static readonly Color Text = new Color(0.93f, 0.945f, 1f);
        public static readonly Color SubText = new Color(0.68f, 0.71f, 0.82f);
        public static readonly Color DimText = new Color(0.48f, 0.51f, 0.62f);
        public static readonly Color Accent = new Color(0.32f, 0.78f, 1f);
        public static readonly Color AccentDeep = new Color(0.16f, 0.5f, 0.78f);
        public static readonly Color Ok = new Color(0.35f, 0.9f, 0.55f);
        public static readonly Color Warn = new Color(1f, 0.78f, 0.36f);
        public static readonly Color Danger = new Color(0.95f, 0.36f, 0.42f);
        public static readonly Color NoteTap = new Color(0.32f, 0.82f, 1f);
        public static readonly Color NoteDrag = new Color(1f, 0.62f, 0.32f);
        public static readonly Color NoteProtected = new Color(1f, 0.82f, 0.24f);

        // ======== 圆角贴图 ========
        public static Texture2D RoundTexLg;
        public static Texture2D RoundTexSm;
        private static GUIStyle roundLgStyle;
        private static GUIStyle roundSmStyle;

        // ======== 整屏 / 栏底色渐变 ========
        public static Texture2D BackdropTex;
        public static Texture2D ToolbarTex;
        public static Texture2D StatusTex;

        // ======== 滚动条（细、圆角、青色滑块） ========
        /// <summary>只替换滚动条的皮肤；在 BeginScrollView 前后临时换上，避免默认灰色粗条</summary>
        public static GUISkin ScrollSkin;

        // ======== 文字 ========
        public static GUIStyle TitleLabel;
        public static GUIStyle Heading;
        public static GUIStyle SubHeading;
        public static GUIStyle Label;
        public static GUIStyle SmallLabel;
        public static GUIStyle TinyLabel;
        public static GUIStyle StatusLabel;
        public static GUIStyle HintLabel;
        public static GUIStyle CenterLabel;
        public static GUIStyle RightLabel;
        public static GUIStyle BigTitleLabel;
        public static GUIStyle BigTimeLabel;
        public static GUIStyle BigCountdownLabel;
        public static GUIStyle ChipLabel;
        public static GUIStyle TimeReadout;

        // ======== 控件 ========
        public static GUIStyle Foldout;
        public static GUIStyle Toolbar;
        public static GUIStyle ToolbarButton;
        public static GUIStyle Panel;
        public static GUIStyle ViewBackground;
        public static GUIStyle TimelineBackground;
        public static GUIStyle StatusBar;
        public static GUIStyle BigButton;
        public static GUIStyle Button;
        public static GUIStyle SmallButton;
        public static GUIStyle DangerButton;
        public static GUIStyle RadioButton;
        public static GUIStyle TextField;
        public static GUIStyle FileListBox;
        public static GUIStyle FolderRow;
        public static GUIStyle FileRow;

        // 新增
        public static GUIStyle SegOn;
        public static GUIStyle SegOff;
        public static GUIStyle GhostButton;
        public static GUIStyle AccentButton;
        public static GUIStyle RoundPlayButton;
        public static GUIStyle PillOn;
        public static GUIStyle PillOff;
        public static GUIStyle NotePill;

        public static void EnsureStyles()
        {
            if (!initialized) Init();
        }

        /// <summary>画一个圆角矩形（大圆角）</summary>
        public static void DrawRoundLg(Rect r, Color tint)
        {
            // 9 宫格圆角贴图的 border 是 20：矩形小于 2*border 时切片会互相重叠，
            // 渲染出奇怪的梯形/矩形残影，所以这种情况直接画成实心矩形。
            if (r.width < 40f || r.height < 40f)
            {
                DrawFlat(r, tint);
                return;
            }

            if (roundLgStyle == null)
            {
                roundLgStyle = new GUIStyle { border = new RectOffset(20, 20, 20, 20) };
                roundLgStyle.normal.background = RoundTexLg;
            }
            var old = GUI.color;
            GUI.color = tint;
            GUI.Box(r, GUIContent.none, roundLgStyle);
            GUI.color = old;
        }

        /// <summary>画一个圆角矩形（小圆角）</summary>
        public static void DrawRoundSm(Rect r, Color tint)
        {
            // border 是 10，同样要避免切片重叠
            if (r.width < 20f || r.height < 20f)
            {
                DrawFlat(r, tint);
                return;
            }

            if (roundSmStyle == null)
            {
                roundSmStyle = new GUIStyle { border = new RectOffset(10, 10, 10, 10) };
                roundSmStyle.normal.background = RoundTexSm;
            }
            var old = GUI.color;
            GUI.color = tint;
            GUI.Box(r, GUIContent.none, roundSmStyle);
            GUI.color = old;
        }

        private static void DrawFlat(Rect r, Color tint)
        {
            var old = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>卡片：圆角背景 + 1px 描边</summary>
        public static void DrawCard(Rect r)
        {
            DrawRoundLg(r, Card);
            DrawRoundLgOutline(r, new Color(1f, 1f, 1f, 0.055f));
        }

        /// <summary>
        /// 整屏兜底底色。
        /// 每一帧最先画它，任何模式（含窗口被拉成任意比例）下都不会再露出黑边。
        /// </summary>
        public static void DrawBackdrop(Rect r)
        {
            if (BackdropTex != null) GUI.DrawTexture(r, BackdropTex, ScaleMode.StretchToFill);
            else DrawFlat(r, Bg);
        }

        public static void DrawToolbarBg(Rect r)
        {
            if (ToolbarTex != null) GUI.DrawTexture(r, ToolbarTex, ScaleMode.StretchToFill);
            else DrawFlat(r, Card);
        }

        public static void DrawStatusBg(Rect r)
        {
            if (StatusTex != null) GUI.DrawTexture(r, StatusTex, ScaleMode.StretchToFill);
            else DrawFlat(r, Bg);
        }

        private static void DrawRoundLgOutline(Rect r, Color c)
        {
            // 用「稍大一圈的圆角矩形」充当描边
            DrawRoundLg(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), c);
            DrawRoundLg(r, Card);
        }

        static void Init()
        {
            if (initialized) return;
            initialized = true;

            RoundTexLg = MakeRoundedTex(64, 12, 2);
            RoundTexSm = MakeRoundedTex(32, 8, 2);

            // 卡片之间的缝隙露出的是这张底图：比卡片略深一点点即可，
            // 太深会变成一条条刺眼的黑带（用户反馈「黑色太宽太丑」）。
            BackdropTex = MakeVerticalGradient(256, new Color(0.082f, 0.087f, 0.112f),
                new Color(0.052f, 0.056f, 0.076f));
            ToolbarTex = MakeVerticalGradient(64, new Color(0.112f, 0.120f, 0.158f),
                new Color(0.080f, 0.086f, 0.116f));
            StatusTex = MakeVerticalGradient(48, new Color(0.072f, 0.077f, 0.102f),
                new Color(0.052f, 0.056f, 0.076f));

            InitScrollSkin();

            // ======== 文字 ========
            TitleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Text }
            };

            Heading = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Text }
            };

            SubHeading = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = SubText }
            };

            Label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Text }
            };

            SmallLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = SubText }
            };

            TinyLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = DimText }
            };

            StatusLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = SubText }
            };

            HintLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = DimText }
            };

            CenterLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = DimText }
            };

            RightLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = SubText }
            };

            BigTitleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Text }
            };

            BigTimeLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 54,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Accent }
            };

            BigCountdownLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 120,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Warn }
            };

            ChipLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Text }
            };

            TimeReadout = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Text }
            };

            // ======== 折叠 ========
            Foldout = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = SubText },
                hover = { textColor = Text },
                margin = new RectOffset(0, 0, 5, 5)
            };

            // ======== 背景 ========
            Toolbar = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(14, 14, 8, 8),
                normal = { background = MakeTex(2, 2, new Color(0.082f, 0.088f, 0.118f)) }
            };

            Panel = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(16, 16, 16, 16),
                normal = { background = MakeTex(2, 2, Bg) }
            };

            ViewBackground = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(0, 0, 0, 0),
                normal = { background = MakeTex(2, 2, BgDeep) }
            };

            // 预览模式下需要透明，才能露出 3D 摄像机画面
            TimelineBackground = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(0, 0, 0, 0),
                normal = { background = MakeTex(2, 2, new Color(0.075f, 0.08f, 0.108f)) }
            };

            StatusBar = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(14, 14, 3, 3),
                normal = { background = MakeTex(2, 2, new Color(0.068f, 0.072f, 0.098f)) }
            };

            FileListBox = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(0, 0, 0, 0),
                normal = { background = MakeTex(2, 2, new Color(0.06f, 0.064f, 0.088f)) }
            };

            // ======== 按钮 ========
            BigButton = MakeRoundButton(17, FontStyle.Bold,
                new Color(0.16f, 0.52f, 0.82f), new Color(0.24f, 0.66f, 1f), new Color(0.13f, 0.42f, 0.68f));

            Button = MakeRoundButton(13, FontStyle.Normal,
                CardSoft, new Color(0.19f, 0.2f, 0.26f), new Color(0.14f, 0.15f, 0.2f));

            SmallButton = MakeRoundButton(12, FontStyle.Normal,
                new Color(0.16f, 0.17f, 0.22f), new Color(0.24f, 0.26f, 0.33f), new Color(0.13f, 0.14f, 0.19f));

            DangerButton = MakeRoundButton(13, FontStyle.Bold,
                new Color(0.72f, 0.24f, 0.3f), new Color(0.88f, 0.31f, 0.37f), new Color(0.58f, 0.19f, 0.24f));

            AccentButton = MakeRoundButton(13, FontStyle.Bold,
                new Color(0.13f, 0.42f, 0.68f), new Color(0.2f, 0.58f, 0.9f), new Color(0.1f, 0.34f, 0.56f));

            GhostButton = MakeRoundButton(13, FontStyle.Normal,
                new Color(1f, 1f, 1f, 0.05f), new Color(1f, 1f, 1f, 0.12f), new Color(1f, 1f, 1f, 0.08f));

            ToolbarButton = GhostButton;

            SegOff = MakeRoundButton(13, FontStyle.Normal,
                new Color(1f, 1f, 1f, 0.04f), new Color(1f, 1f, 1f, 0.10f), new Color(1f, 1f, 1f, 0.06f));

            SegOn = MakeRoundButton(13, FontStyle.Bold,
                new Color(0.16f, 0.46f, 0.72f), new Color(0.22f, 0.56f, 0.86f), new Color(0.14f, 0.4f, 0.64f));

            PillOff = MakeRoundButton(12, FontStyle.Normal,
                new Color(1f, 1f, 1f, 0.04f), new Color(1f, 1f, 1f, 0.11f), new Color(1f, 1f, 1f, 0.07f));

            PillOn = MakeRoundButton(12, FontStyle.Bold,
                new Color(0.14f, 0.42f, 0.66f), new Color(0.2f, 0.54f, 0.82f), new Color(0.12f, 0.36f, 0.58f));

            RoundPlayButton = MakeRoundButton(18, FontStyle.Bold,
                new Color(0.16f, 0.52f, 0.82f), new Color(0.24f, 0.66f, 1f), new Color(0.13f, 0.42f, 0.68f));

            NotePill = new GUIStyle { border = new RectOffset(8, 8, 8, 8) };
            NotePill.normal.background = RoundTexSm;

            RadioButton = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = SubText },
                hover = { textColor = Text },
                onNormal = { textColor = Accent },
                margin = new RectOffset(0, 0, 3, 3)
            };

            // ======== 输入框 / 文件浏览器 ========
            TextField = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(9, 9, 4, 4),
                normal = { textColor = Text, background = MakeTex(2, 2, new Color(0.15f, 0.16f, 0.21f)) },
                focused = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.19f, 0.21f, 0.28f)) }
            };

            FolderRow = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 2, 2),
                normal = { textColor = new Color(0.88f, 0.82f, 0.55f), background = MakeTex(2, 2, new Color(0.12f, 0.13f, 0.18f)) },
                hover = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.19f, 0.2f, 0.27f)) },
                active = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.16f, 0.16f, 0.23f)) }
            };

            FileRow = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 2, 2),
                normal = { textColor = Text, background = MakeTex(2, 2, new Color(0.13f, 0.14f, 0.19f)) },
                hover = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.21f, 0.24f, 0.31f)) },
                active = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.17f, 0.19f, 0.26f)) }
            };
        }

        private static GUIStyle MakeRoundButton(int fontSize, FontStyle style, Color normal, Color hover, Color active)
        {
            var s = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                fontStyle = style,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 4, 4),
                border = new RectOffset(8, 8, 8, 8),
                normal = { textColor = Text, background = RoundTexSm },
                hover = { textColor = Color.white, background = RoundTexSm },
                active = { textColor = Color.white, background = RoundTexSm }
            };
            // 用一个白色圆角底 + GUI.color 无法做 per-state 颜色，这里改为生成多张染色贴图
            s.normal.background = MakeTintedRound(24, 6, normal);
            s.hover.background = MakeTintedRound(24, 6, hover);
            s.active.background = MakeTintedRound(24, 6, active);
            s.border = new RectOffset(7, 7, 7, 7);
            return s;
        }

        private static Texture2D MakeTintedRound(int size, int radius, Color fill)
        {
            return MakeRoundedTexFilled(size, radius, fill, 0);
        }

        /// <summary>
        /// 生成圆角贴图（带 1px 抗锯齿）。borderWidth > 0 时画描边色。
        /// </summary>
        private static Texture2D MakeRoundedTex(int size, int radius, int borderWidth)
        {
            return MakeRoundedTexFilled(size, radius, new Color(1f, 1f, 1f, 1f), borderWidth);
        }

        private static Texture2D MakeRoundedTexFilled(int size, int radius, Color fill, int borderWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedRectSdf(x + 0.5f, y + 0.5f, size, size, radius);
                    float a = Mathf.Clamp01(0.5f - d);
                    var c = fill;
                    c.a *= a;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private static float RoundedRectSdf(float px, float py, float w, float h, float radius)
        {
            float hw = w * 0.5f;
            float hh = h * 0.5f;
            float dx = Mathf.Abs(px - hw) - (hw - radius);
            float dy = Mathf.Abs(py - hh) - (hh - radius);
            float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return outside + inside - radius;
        }

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private static Texture2D MakeVerticalGradient(int height, Color top, Color bottom)
        {
            var tex = new Texture2D(2, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < height; y++)
            {
                float t = height <= 1 ? 0f : (float)y / (height - 1);
                var c = Color.Lerp(bottom, top, t); // 贴图 y 向上，顶部色放在数组末尾
                tex.SetPixel(0, y, c);
                tex.SetPixel(1, y, c);
            }
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 建一个只改滚动条的 GUISkin。IMGUI 的滑块样式是从 GUI.skin 里取的，
        /// 单靠传 style 参数换不掉默认那条又粗又亮的灰条，所以必须临时换皮肤。
        /// 其它控件样式原样拷贝，保证换了皮肤也不会变样。
        /// </summary>
        private static void InitScrollSkin()
        {
            var src = GUI.skin;
            if (src == null) return;

            ScrollSkin = ScriptableObject.CreateInstance<GUISkin>();
            // font 必须一起拷：GUISkin.font 为 null 时，换肤期间用到的文字会没有字体，
            // 表现为整片文字消失。customStyles 也一并带上，避免别处引用的自定义样式失效。
            ScrollSkin.font = src.font;
            ScrollSkin.customStyles = src.customStyles;
            ScrollSkin.box = src.box;
            ScrollSkin.button = src.button;
            ScrollSkin.toggle = src.toggle;
            ScrollSkin.label = src.label;
            ScrollSkin.textField = src.textField;
            ScrollSkin.textArea = src.textArea;
            ScrollSkin.window = src.window;
            ScrollSkin.horizontalSlider = src.horizontalSlider;
            ScrollSkin.horizontalSliderThumb = src.horizontalSliderThumb;
            ScrollSkin.verticalSlider = src.verticalSlider;
            ScrollSkin.verticalSliderThumb = src.verticalSliderThumb;
            ScrollSkin.horizontalScrollbarLeftButton = src.horizontalScrollbarLeftButton;
            ScrollSkin.horizontalScrollbarRightButton = src.horizontalScrollbarRightButton;
            ScrollSkin.verticalScrollbarUpButton = src.verticalScrollbarUpButton;
            ScrollSkin.verticalScrollbarDownButton = src.verticalScrollbarDownButton;
            ScrollSkin.scrollView = src.scrollView;

            ScrollSkin.verticalScrollbar = MakeScrollbarTrack(true);
            ScrollSkin.verticalScrollbarThumb = MakeScrollbarThumb(true);
            ScrollSkin.horizontalScrollbar = MakeScrollbarTrack(false);
            ScrollSkin.horizontalScrollbarThumb = MakeScrollbarThumb(false);
        }

        private static GUIStyle MakeScrollbarTrack(bool vertical)
        {
            var s = new GUIStyle { name = vertical ? "thart-vscroll" : "thart-hscroll" };
            if (vertical)
            {
                s.fixedWidth = 10f;
                s.stretchHeight = true;
            }
            else
            {
                s.fixedHeight = 10f;
                s.stretchWidth = true;
            }
            var bg = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.03f));
            s.normal.background = bg;
            s.hover.background = bg;
            s.active.background = bg;
            return s;
        }

        private static GUIStyle MakeScrollbarThumb(bool vertical)
        {
            var s = new GUIStyle { name = vertical ? "thart-vthumb" : "thart-hthumb" };
            s.border = new RectOffset(5, 5, 5, 5);
            if (vertical)
            {
                s.fixedWidth = 10f;
                s.margin = new RectOffset(1, 1, 2, 2);
            }
            else
            {
                s.fixedHeight = 10f;
                s.margin = new RectOffset(2, 2, 1, 1);
            }
            s.normal.background = MakeRoundedTexFilled(24, 5, new Color(1f, 1f, 1f, 0.20f), 0);
            s.hover.background = MakeRoundedTexFilled(24, 5, new Color(0.32f, 0.78f, 1f, 0.60f), 0);
            s.active.background = MakeRoundedTexFilled(24, 5, new Color(0.42f, 0.86f, 1f, 0.85f), 0);
            return s;
        }
    }
}
