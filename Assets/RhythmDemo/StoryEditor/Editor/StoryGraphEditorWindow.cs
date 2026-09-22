using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GeometryRhythm.StoryEditor
{
    /// <summary>
    /// Deliberately simple story authoring tool. Steps are always linear; choices only set
    /// variables that later dialogue can use for local text differences.
    /// </summary>
    public sealed class StoryGraphEditorWindow : EditorWindow
    {
        enum AssetSection
        {
            Cast,
            Backgrounds,
            Project
        }

        const float ToolbarHeight = 44;
        const float StatusHeight = 38;
        const float SequenceWidth = 300;
        const float AssetWidth = 350;
        const string LastProjectKey = "RhythmHacker.StoryEditor.LastProject";

        readonly Stack<string> undo = new Stack<string>();
        readonly Stack<string> redo = new Stack<string>();

        StoryProjectData project;
        List<StoryDiagnostic> diagnostics = new List<StoryDiagnostic>();
        string filePath;
        string selectedNodeId = "";
        string selectedCharacterId = "";
        string selectedBackgroundId = "";
        string status = "就绪";
        double statusUntil;
        bool mutationThisFrame;
        bool showOverview;
        bool showPreview;
        bool previewRunning;
        bool previewAutoPlay;
        int previewIndex;
        double previewAdvanceAt;
        readonly Dictionary<string, string> previewVariables = new Dictionary<string, string>(StringComparer.Ordinal);

        AssetSection assetSection;
        Vector2 sequenceScroll;
        Vector2 centerScroll;
        Vector2 assetScroll;
        Vector2 diagnosticsScroll;

        GUIStyle titleStyle;
        GUIStyle sectionStyle;
        GUIStyle smallStyle;
        GUIStyle tinyStyle;
        GUIStyle statusStyle;
        GUIStyle cardTitleStyle;
        GUIStyle textPreviewStyle;
        GUIStyle previewSpeakerStyle;
        GUIStyle previewTextStyle;
        GUIStyle previewChoiceStyle;
        GUIStyle previewOverlayTitleStyle;

        [MenuItem("节奏黑客/剧情编辑器/打开剧情编辑器", false, 100)]
        [MenuItem("Window/剧情编辑器 %#g", false, 1900)]
        public static void OpenWindow()
        {
            var window = GetWindow<StoryGraphEditorWindow>();
            window.titleContent = new GUIContent("剧情编辑器");
            window.minSize = new Vector2(1180, 700);
            window.Show();
        }

        [MenuItem("节奏黑客/剧情编辑器/新建剧情项目", false, 101)]
        public static void CreateNewProject()
        {
            var window = GetWindow<StoryGraphEditorWindow>();
            window.titleContent = new GUIContent("剧情编辑器");
            window.minSize = new Vector2(1180, 700);
            window.NewProject(true);
            window.Show();
        }

        [MenuItem("节奏黑客/剧情编辑器/校验当前剧情", false, 102)]
        public static void ValidateCurrentStory()
        {
            var window = GetWindow<StoryGraphEditorWindow>();
            window.ValidateProject();
            window.Show();
        }

        internal void LoadProjectForTools(string path)
        {
            LoadProject(path, true);
        }

        void OnEnable()
        {
            titleContent = new GUIContent("剧情编辑器");
            minSize = new Vector2(1180, 700);
            string remembered = EditorPrefs.GetString(LastProjectKey, "");
            if (!string.IsNullOrEmpty(remembered) && File.Exists(remembered))
                LoadProject(remembered, false);
            else
                NewProject(false);
        }

        void OnGUI()
        {
            EnsureProject();
            EnsureStyles();
            HandleKeyboard();
            DrawToolbar();

            float bodyHeight = Mathf.Max(1, position.height - ToolbarHeight - StatusHeight);
            var sequenceRect = new Rect(0, ToolbarHeight, SequenceWidth, bodyHeight);
            var centerRect = new Rect(SequenceWidth, ToolbarHeight,
                Mathf.Max(1, position.width - SequenceWidth - AssetWidth), bodyHeight);
            var assetRect = new Rect(position.width - AssetWidth, ToolbarHeight, AssetWidth, bodyHeight);
            DrawSequence(sequenceRect);
            DrawCenter(centerRect);
            DrawAssets(assetRect);
            DrawStatus();
        }

        void Update()
        {
            if (!previewRunning || !previewAutoPlay || project == null) return;
            if (EditorApplication.timeSinceStartup >= previewAdvanceAt) AdvancePreview();
            Repaint();
        }

        void EnsureProject()
        {
            if (project != null) return;
            project = StoryProjectFactory.CreateStarter();
            filePath = DefaultProjectPath();
            NormalizeProject();
            EnsureSelection();
        }

        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 17,
                normal = { textColor = new Color(.94f, .96f, 1f) }
            };
            sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(.68f, .8f, .92f) }
            };
            smallStyle = new GUIStyle(EditorStyles.label) { fontSize = 11, wordWrap = true };
            smallStyle.normal.textColor = new Color(.68f, .72f, .78f);
            tinyStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 9 };
            tinyStyle.normal.textColor = new Color(.55f, .6f, .67f);
            statusStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11
            };
            statusStyle.normal.textColor = new Color(.87f, .91f, .96f);
            cardTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                clipping = TextClipping.Clip
            };
            textPreviewStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            textPreviewStyle.normal.textColor = new Color(.84f, .87f, .92f);
            previewSpeakerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                normal = { textColor = Color.white }
            };
            previewTextStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 15,
                wordWrap = true,
                normal = { textColor = new Color(.96f, .97f, 1f) }
            };
            previewChoiceStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 14, 6, 6)
            };
            previewOverlayTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 26,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        void DrawToolbar()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, ToolbarHeight), new Color(.075f, .083f, .10f));
            EditorGUI.DrawRect(new Rect(0, ToolbarHeight - 1, position.width, 1), new Color(.2f, .24f, .3f));
            GUILayout.BeginArea(new Rect(10, 6, position.width - 20, 34));
            GUILayout.BeginHorizontal();
            GUILayout.Label("剧情编辑器", titleStyle, GUILayout.Width(92));
            if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(52))) NewProject(true);
            if (GUILayout.Button("打开", EditorStyles.toolbarButton, GUILayout.Width(52))) OpenProject();
            if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(52))) SaveProject(false);
            if (GUILayout.Button("另存为", EditorStyles.toolbarButton, GUILayout.Width(62))) SaveProject(true);
            GUILayout.Space(8);
            if (GUILayout.Button("撤销", EditorStyles.toolbarButton, GUILayout.Width(52))) Undo();
            if (GUILayout.Button("重做", EditorStyles.toolbarButton, GUILayout.Width(52))) Redo();
            GUILayout.FlexibleSpace();
            GUILayout.Label(project.title + "   /   " + Path.GetFileName(filePath), smallStyle);
            GUILayout.Space(8);
            if (GUILayout.Button("校验", EditorStyles.toolbarButton, GUILayout.Width(58))) ValidateProject();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawSequence(Rect area)
        {
            EditorGUI.DrawRect(area, new Color(.088f, .095f, .115f));
            EditorGUI.DrawRect(new Rect(area.xMax - 1, area.y, 1, area.height), new Color(.18f, .21f, .26f));
            GUILayout.BeginArea(new Rect(area.x + 12, area.y + 12, area.width - 24, area.height - 24));
            GUILayout.Label("剧情步骤", sectionStyle);
            GUILayout.Label("流程永远从上往下播放，不需要连线。", smallStyle);
            GUILayout.Space(7);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 对白")) AddStep(StoryNodeKind.Dialogue);
            if (GUILayout.Button("+ 选项")) AddStep(StoryNodeKind.Choice);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 背景")) AddStep(StoryNodeKind.Background);
            if (GUILayout.Button("+ 进入关卡")) AddStep(StoryNodeKind.Stage);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("+ 结束")) AddStep(StoryNodeKind.End);
            GUILayout.Space(8);

            sequenceScroll = EditorGUILayout.BeginScrollView(sequenceScroll);
            for (int i = 0; i < project.nodes.Length; i++)
            {
                var node = project.nodes[i];
                if (node == null) continue;
                bool selected = node.id == selectedNodeId;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                string label = (i + 1).ToString("00") + "  " + StoryEditorText.NodeKind(node.kind) + "  " + node.title;
                bool nextSelected = GUILayout.Toggle(selected, label, EditorStyles.toolbarButton);
                if (nextSelected && !selected)
                {
                    selectedNodeId = node.id;
                    showOverview = false;
                    showPreview = false;
                    GUI.FocusControl(null);
                    Repaint();
                }
                GUILayout.Label(StepSummary(node), tinyStyle);
                GUILayout.BeginHorizontal();
                GUI.enabled = i > 0;
                if (GUILayout.Button("上移", EditorStyles.miniButtonLeft, GUILayout.Width(44))) MoveStep(i, -1);
                GUI.enabled = i < project.nodes.Length - 1;
                if (GUILayout.Button("下移", EditorStyles.miniButtonMid, GUILayout.Width(44))) MoveStep(i, 1);
                GUI.enabled = true;
                if (GUILayout.Button("复制", EditorStyles.miniButtonMid, GUILayout.Width(44))) DuplicateStep(i);
                if (GUILayout.Button("删除", EditorStyles.miniButtonRight, GUILayout.Width(44))) DeleteStep(i);
                GUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUILayout.Space(3);
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawCenter(Rect area)
        {
            EditorGUI.DrawRect(area, new Color(.055f, .061f, .074f));
            EditorGUI.DrawRect(new Rect(area.x, area.y, 1, area.height), new Color(.18f, .21f, .26f));
            EditorGUI.DrawRect(new Rect(area.xMax - 1, area.y, 1, area.height), new Color(.18f, .21f, .26f));
            GUILayout.BeginArea(new Rect(area.x + 14, area.y + 12, area.width - 28, area.height - 24));
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Toggle(!showOverview && !showPreview, "编辑步骤",
                    EditorStyles.toolbarButton, GUILayout.Width(82)))
            {
                showOverview = false;
                showPreview = false;
            }
            if (GUILayout.Toggle(showPreview, "实时预览", EditorStyles.toolbarButton, GUILayout.Width(82)))
            {
                showPreview = true;
                showOverview = false;
            }
            if (GUILayout.Toggle(showOverview, "脚本总览", EditorStyles.toolbarButton, GUILayout.Width(82)))
            {
                showOverview = true;
                showPreview = false;
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("选项只改变后续文本，不改变流程", smallStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            centerScroll = EditorGUILayout.BeginScrollView(centerScroll);
            if (showPreview) DrawRealtimePreview(area.width - 28);
            else if (showOverview) DrawScriptOverview();
            else DrawCurrentStepEditor();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawCurrentStepEditor()
        {
            var node = SelectedNode();
            if (node == null)
            {
                GUILayout.Space(30);
                GUILayout.Label("先在左侧选择一个步骤", titleStyle);
                GUILayout.Label("如果还没有步骤，可以直接点击左侧的“+ 对白”。", smallStyle);
                return;
            }

            string before = Snapshot();
            mutationThisFrame = false;
            EditorGUI.BeginChangeCheck();
            GUILayout.Label("当前步骤", sectionStyle);
            node.kind = NodeKindPopup("步骤类型", node.kind);
            node.title = EditorGUILayout.TextField("步骤名称", node.title);
            node.id = EditorGUILayout.TextField("步骤 ID", node.id);
            GUILayout.Space(10);
            switch (node.kind)
            {
                case StoryNodeKind.Dialogue:
                    DrawDialogueEditor(node);
                    break;
                case StoryNodeKind.Choice:
                    DrawChoiceEditor(node);
                    break;
                case StoryNodeKind.Background:
                    DrawBackgroundEditor(node);
                    break;
                case StoryNodeKind.Stage:
                    DrawStageEditor(node);
                    break;
                case StoryNodeKind.End:
                    DrawEndEditor(node);
                    break;
            }
            bool changed = EditorGUI.EndChangeCheck();
            if (changed || mutationThisFrame) PushUndo(before);
        }

        void DrawDialogueEditor(StoryNodeData node)
        {
            CharacterPopup("说话角色", ref node.speakerId);
            var character = FindCharacter(node.speakerId);
            ExpressionPopup(character, "表情", ref node.expressionId);
            GUILayout.Label("默认对白", sectionStyle);
            GUILayout.Label("没有匹配到文本差异时，播放这里的内容。", smallStyle);
            node.text = EditorGUILayout.TextArea(node.text, GUILayout.MinHeight(100));

            Texture2D preview = LoadTexture(ExpressionPath(character, node.expressionId));
            if (preview != null)
            {
                var rect = GUILayoutUtility.GetRect(140, 150, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit, true);
            }

            GUILayout.Space(14);
            GUILayout.Label("文本差异", sectionStyle);
            GUILayout.Label("由前面的选项设置变量。这里填写变量匹配时要替换成的文本。", smallStyle);
            if (node.textVariants == null) node.textVariants = Array.Empty<StoryTextVariantData>();
            int remove = -1;
            for (int i = 0; i < node.textVariants.Length; i++)
            {
                var variant = node.textVariants[i] ?? new StoryTextVariantData();
                node.textVariants[i] = variant;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.BeginHorizontal();
                GUILayout.Label("差异 " + (i + 1), sectionStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(46))) remove = i;
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                variant.variable = EditorGUILayout.TextField("变量", variant.variable);
                variant.value = EditorGUILayout.TextField("等于", variant.value);
                GUILayout.EndHorizontal();
                variant.text = EditorGUILayout.TextArea(variant.text, GUILayout.MinHeight(56));
                EditorGUILayout.EndVertical();
                GUILayout.Space(3);
            }
            if (remove >= 0)
            {
                var list = new List<StoryTextVariantData>(node.textVariants);
                list.RemoveAt(remove);
                node.textVariants = list.ToArray();
                mutationThisFrame = true;
            }
            if (GUILayout.Button("+ 添加文本差异"))
            {
                var list = new List<StoryTextVariantData>(node.textVariants)
                {
                    new StoryTextVariantData { variable = "变量", value = "选项值", text = "替换后的文本" }
                };
                node.textVariants = list.ToArray();
                mutationThisFrame = true;
            }
        }

        void DrawChoiceEditor(StoryNodeData node)
        {
            GUILayout.Label("选项提示", sectionStyle);
            node.text = EditorGUILayout.TextArea(node.text, GUILayout.MinHeight(64));
            GUILayout.Space(10);
            GUILayout.Label("选项内容", sectionStyle);
            GUILayout.Label("每个选项只负责设置一个变量。后续对白用“变量 + 选项值”决定显示哪段文本。", smallStyle);
            if (node.choices == null) node.choices = Array.Empty<StoryChoiceData>();
            int remove = -1;
            for (int i = 0; i < node.choices.Length; i++)
            {
                var choice = node.choices[i] ?? new StoryChoiceData();
                node.choices[i] = choice;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.BeginHorizontal();
                GUILayout.Label("选项 " + (i + 1), sectionStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(46))) remove = i;
                GUILayout.EndHorizontal();
                choice.text = EditorGUILayout.TextField("显示文本", choice.text);
                choice.variable = EditorGUILayout.TextField("设置变量", choice.variable);
                choice.value = EditorGUILayout.TextField("设置为", choice.value);
                EditorGUILayout.EndVertical();
                GUILayout.Space(3);
            }
            if (remove >= 0)
            {
                var list = new List<StoryChoiceData>(node.choices);
                list.RemoveAt(remove);
                node.choices = list.ToArray();
                mutationThisFrame = true;
            }
            if (GUILayout.Button("+ 添加选项"))
            {
                var list = new List<StoryChoiceData>(node.choices)
                {
                    new StoryChoiceData
                    {
                        id = UniqueChoiceId(node, "choice"),
                        text = "新选项",
                        variable = node.choices.Length > 0 && node.choices[0] != null
                            ? node.choices[0].variable : "行动方式",
                        value = "选项" + (node.choices.Length + 1)
                    }
                };
                node.choices = list.ToArray();
                mutationThisFrame = true;
            }
        }

        void DrawBackgroundEditor(StoryNodeData node)
        {
            BackgroundPopup(ref node.backgroundId);
            DrawCastSelection(node);
            int transition = Mathf.Max(0, Array.IndexOf(new[] { "fade", "cut", "slide" }, node.transition));
            transition = EditorGUILayout.Popup("切换方式", transition, new[] { "淡入淡出", "硬切", "滑动" });
            node.transition = new[] { "fade", "cut", "slide" }[transition];
            var background = FindBackground(node.backgroundId);
            Texture2D preview = LoadTexture(background == null ? "" : background.assetPath);
            if (preview != null)
            {
                var rect = GUILayoutUtility.GetRect(240, 150, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, preview, ScaleMode.ScaleAndCrop, true);
            }
        }

        void DrawCastSelection(StoryNodeData node)
        {
            if (node.castIds == null) node.castIds = Array.Empty<string>();
            GUILayout.Label("场景角色", sectionStyle);
            GUILayout.Label("勾选这个场景里同时出现的角色。未说话角色会自动变暗。", smallStyle);
            if (project.characters == null || project.characters.Length == 0) return;
            var selected = new List<string>(node.castIds);
            foreach (var character in project.characters)
            {
                if (character == null) continue;
                bool included = selected.Contains(character.id);
                bool next = GUILayout.Toggle(included, character.displayName, EditorStyles.miniButton);
                if (next == included) continue;
                if (next) selected.Add(character.id);
                else selected.Remove(character.id);
            }
            node.castIds = selected.ToArray();
        }

        void DrawStageEditor(StoryNodeData node)
        {
            node.levelId = EditorGUILayout.TextField("关卡 ID", node.levelId);
            var chart = LoadTextAsset(node.chartAssetPath);
            var next = (TextAsset)EditorGUILayout.ObjectField("谱面 JSON", chart, typeof(TextAsset), false);
            if (next != chart) node.chartAssetPath = next == null ? "" : AssetDatabase.GetAssetPath(next);
            node.autoplay = EditorGUILayout.Toggle("自动演示", node.autoplay);
            GUILayout.Label("播放到这个步骤时进入关卡；关卡结束后继续执行下面的步骤。", smallStyle);
        }

        void DrawEndEditor(StoryNodeData node)
        {
            GUILayout.Label("结束文本", sectionStyle);
            node.text = EditorGUILayout.TextArea(node.text, GUILayout.MinHeight(90));
            GUILayout.Label("结束步骤放在列表最后即可，后续步骤会被游戏运行时忽略。", smallStyle);
        }

        void DrawRealtimePreview(float availableWidth)
        {
            GUILayout.Label("实时预览", titleStyle);
            GUILayout.Label("这里显示当前编辑数据的即时效果，不需要保存。选项会设置变量，后续对白会自动匹配文本差异。", smallStyle);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(previewRunning ? "重新开始" : "开始预览", GUILayout.Width(86)))
                StartPreview(0, true);
            if (GUILayout.Button("从当前步骤预览", GUILayout.Width(116)))
                StartPreview(SelectedIndex(), true);
            GUI.enabled = previewRunning;
            if (GUILayout.Button("下一步", GUILayout.Width(66))) AdvancePreview();
            GUI.enabled = true;
            if (GUILayout.Button(previewAutoPlay ? "暂停自动播放" : "自动播放", GUILayout.Width(100)))
            {
                previewAutoPlay = !previewAutoPlay;
                previewAdvanceAt = EditorApplication.timeSinceStartup + 1.8;
            }
            if (GUILayout.Button("清空变量", GUILayout.Width(76)))
            {
                previewVariables.Clear();
                SetStatus("已清空预览变量。");
                Repaint();
            }
            GUILayout.EndHorizontal();
            string variableText = previewVariables.Count == 0
                ? "当前变量：无"
                : "当前变量：" + string.Join("   ", previewVariables
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value => value.Key + " = " + value.Value));
            GUILayout.Label(variableText, smallStyle);
            GUILayout.Space(8);

            float frameWidth = Mathf.Clamp(availableWidth - 8, 360f, 860f);
            float frameHeight = frameWidth * 9f / 16f;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var frame = GUILayoutUtility.GetRect(frameWidth, frameHeight);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUI.BeginGroup(frame);
            DrawPreviewFrame(new Rect(0, 0, frameWidth, frameHeight));
            GUI.EndGroup();
            GUILayout.Space(10);
        }

        void DrawPreviewFrame(Rect frame)
        {
            EditorGUI.DrawRect(frame, new Color(.025f, .03f, .042f));
            if (!previewRunning || project.nodes == null || project.nodes.Length == 0)
            {
                GUI.Label(new Rect(0, frame.height * .42f, frame.width, 40), "点击“开始预览”", previewOverlayTitleStyle);
                return;
            }

            previewIndex = Mathf.Clamp(previewIndex, 0, project.nodes.Length - 1);
            var node = project.nodes[previewIndex];
            if (node == null)
            {
                GUI.Label(new Rect(0, frame.height * .42f, frame.width, 40), "当前步骤为空", previewOverlayTitleStyle);
                return;
            }

            string backgroundId = ResolvePreviewBackground(previewIndex);
            var background = FindBackground(backgroundId);
            Texture2D backgroundTexture = LoadTexture(background == null ? "" : background.assetPath);
            if (backgroundTexture != null)
                GUI.DrawTexture(frame, backgroundTexture, ScaleMode.ScaleAndCrop, true);
            else
                EditorGUI.DrawRect(frame, new Color(.08f, .10f, .14f));

            if (node.kind == StoryNodeKind.Background)
            {
                DrawPreviewOverlay(frame, "背景切换",
                    background == null ? "未选择背景" : background.label,
                    "继续", () => AdvancePreview());
                return;
            }
            if (node.kind == StoryNodeKind.Stage)
            {
                DrawPreviewOverlay(frame, "进入关卡 " + node.levelId,
                    node.autoplay ? "自动演示" : "正常游玩",
                    "模拟通关并继续", () => AdvancePreview());
                return;
            }
            if (node.kind == StoryNodeKind.End)
            {
                DrawPreviewOverlay(frame, "剧情结束",
                    string.IsNullOrWhiteSpace(node.text) ? "章节结束" : node.text,
                    "重新开始", () => StartPreview(0, true));
                return;
            }

            var speakerNode = FindPreviewSpeaker(previewIndex);
            Texture2D characterTexture = speakerNode == null
                ? null
                : LoadTexture(ExpressionPath(FindCharacter(speakerNode.speakerId), speakerNode.expressionId));
            if (characterTexture != null)
            {
                var expression = FindExpression(
                    FindCharacter(speakerNode.speakerId), speakerNode.expressionId);
                float characterWidth = frame.width * .48f;
                float characterHeight = frame.height * .76f;
                float anchorX = expression == null ? .5f : expression.anchorX;
                float anchorY = expression == null ? .5f : expression.anchorY;
                float characterX = Mathf.Clamp((frame.width - characterWidth) * anchorX, 0, frame.width - characterWidth);
                float characterY = Mathf.Clamp(frame.height * .04f + frame.height * .18f * (1f - anchorY),
                    0, frame.height - characterHeight);
                GUI.DrawTexture(new Rect(characterX, characterY, characterWidth, characterHeight),
                    characterTexture, ScaleMode.ScaleToFit, true);
            }

            var box = new Rect(frame.width * .06f, frame.height * .66f, frame.width * .88f, frame.height * .27f);
            EditorGUI.DrawRect(box, new Color(.015f, .02f, .03f, .9f));
            EditorGUI.DrawRect(new Rect(box.x, box.y, 3, box.height), new Color(.32f, .78f, 1f));

            if (node.kind == StoryNodeKind.Choice)
            {
                GUI.Label(new Rect(box.x + 16, box.y + 8, box.width - 32, 26),
                    string.IsNullOrWhiteSpace(node.text) ? "请选择" : node.text, previewSpeakerStyle);
                if (node.choices != null && node.choices.Length > 0)
                {
                    float top = box.y + 42;
                    float available = Mathf.Max(24, box.height - 50);
                    float height = Mathf.Clamp(available / node.choices.Length - 4, 24, 40);
                    for (int i = 0; i < node.choices.Length; i++)
                    {
                        var choice = node.choices[i];
                        if (choice == null) continue;
                        var buttonRect = new Rect(box.x + 18, top + i * (height + 4), box.width - 36, height);
                        if (GUI.Button(buttonRect, "  " + choice.text + "    ["
                                + choice.variable + " = " + choice.value + "]", previewChoiceStyle))
                            ChoosePreview(choice);
                    }
                }
                return;
            }

            string speaker = speakerNode == null ? "" : FindCharacter(speakerNode.speakerId)?.displayName;
            if (string.IsNullOrEmpty(speaker)) speaker = "旁白";
            string dialogueText = ResolvePreviewText(speakerNode ?? node);
            GUI.Label(new Rect(box.x + 18, box.y + 10, box.width - 36, 26), speaker, previewSpeakerStyle);
            GUI.Label(new Rect(box.x + 18, box.y + 43, box.width - 36, box.height - 52),
                dialogueText, previewTextStyle);
        }

        void DrawPreviewOverlay(Rect frame, string title, string body, string button, Action action)
        {
            float width = frame.width * .72f;
            float height = frame.height * .5f;
            var panel = new Rect((frame.width - width) * .5f, (frame.height - height) * .5f, width, height);
            EditorGUI.DrawRect(panel, new Color(.01f, .015f, .025f, .94f));
            GUI.Label(new Rect(panel.x + 20, panel.y + 20, panel.width - 40, 42),
                title, previewOverlayTitleStyle);
            GUI.Label(new Rect(panel.x + 40, panel.y + 74, panel.width - 80, panel.height - 136),
                body, previewTextStyle);
            if (GUI.Button(new Rect(panel.x + 40, panel.yMax - 52, panel.width - 80, 34), button))
                action();
        }

        void StartPreview(int index, bool clearVariables)
        {
            if (project.nodes == null || project.nodes.Length == 0)
            {
                SetStatus("没有可以预览的剧情步骤。", true);
                return;
            }
            previewRunning = true;
            previewIndex = Mathf.Clamp(index < 0 ? 0 : index, 0, project.nodes.Length - 1);
            if (clearVariables) previewVariables.Clear();
            previewAutoPlay = false;
            previewAdvanceAt = EditorApplication.timeSinceStartup + 1.8;
            Repaint();
        }

        void AdvancePreview()
        {
            if (!previewRunning || project.nodes == null || project.nodes.Length == 0) return;
            previewIndex = Mathf.Clamp(previewIndex, 0, project.nodes.Length - 1);
            var node = project.nodes[previewIndex];
            if (node != null && (node.kind == StoryNodeKind.Choice || node.kind == StoryNodeKind.End))
            {
                previewAutoPlay = false;
                return;
            }
            if (previewIndex >= project.nodes.Length - 1)
            {
                previewAutoPlay = false;
                return;
            }
            previewIndex++;
            previewAdvanceAt = EditorApplication.timeSinceStartup + 2.3;
            Repaint();
        }

        void ChoosePreview(StoryChoiceData choice)
        {
            if (choice == null) return;
            previewVariables[choice.variable] = choice.value;
            previewIndex = Mathf.Min(previewIndex + 1, project.nodes.Length - 1);
            previewAdvanceAt = EditorApplication.timeSinceStartup + 1.8;
            Repaint();
        }

        string ResolvePreviewText(StoryNodeData node)
        {
            if (node == null) return "";
            if (node.textVariants != null)
            {
                foreach (var variant in node.textVariants)
                {
                    if (variant == null) continue;
                    if (previewVariables.TryGetValue(variant.variable, out string value) &&
                        string.Equals(value, variant.value, StringComparison.Ordinal))
                        return variant.text;
                }
            }
            return node.text;
        }

        string ResolvePreviewBackground(int index)
        {
            string result = "";
            for (int i = 0; i <= index && i < project.nodes.Length; i++)
            {
                var node = project.nodes[i];
                if (node != null && node.kind == StoryNodeKind.Background && !string.IsNullOrEmpty(node.backgroundId))
                    result = node.backgroundId;
            }
            if (string.IsNullOrEmpty(result) && project.backgrounds != null)
                result = project.backgrounds.Length > 0 && project.backgrounds[0] != null
                    ? project.backgrounds[0].id : "";
            return result;
        }

        StoryNodeData FindPreviewSpeaker(int index)
        {
            for (int i = Mathf.Min(index, project.nodes.Length - 1); i >= 0; i--)
            {
                var node = project.nodes[i];
                if (node != null && node.kind == StoryNodeKind.Dialogue) return node;
            }
            return null;
        }

        void DrawScriptOverview()
        {
            GUILayout.Label("脚本总览", titleStyle);
            GUILayout.Label("按执行顺序预览。选项不会改变步骤顺序，只会替换后续匹配到的文本。", smallStyle);
            GUILayout.Space(10);
            if (project.nodes == null || project.nodes.Length == 0)
            {
                GUILayout.Label("还没有剧情步骤。", smallStyle);
                return;
            }
            for (int i = 0; i < project.nodes.Length; i++)
            {
                var node = project.nodes[i];
                if (node == null) continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label((i + 1).ToString("00") + "  " + StoryEditorText.NodeKind(node.kind) + "  /  " + node.title,
                    sectionStyle);
                switch (node.kind)
                {
                    case StoryNodeKind.Dialogue:
                        var speaker = FindCharacter(node.speakerId);
                        GUILayout.Label((speaker == null ? "未选择角色" : speaker.displayName) + "：" + node.text,
                            textPreviewStyle);
                        if (node.textVariants != null)
                        {
                            foreach (var variant in node.textVariants)
                            {
                                if (variant == null) continue;
                                GUILayout.Label("若 " + variant.variable + " = " + variant.value + "：" + variant.text,
                                    smallStyle);
                            }
                        }
                        break;
                    case StoryNodeKind.Choice:
                        GUILayout.Label(node.text, textPreviewStyle);
                        if (node.choices != null)
                        {
                            foreach (var choice in node.choices)
                            {
                                if (choice == null) continue;
                                GUILayout.Label("• " + choice.text + "   [" + choice.variable + " = " + choice.value + "]",
                                    smallStyle);
                            }
                        }
                        break;
                    case StoryNodeKind.Background:
                        var background = FindBackground(node.backgroundId);
                        GUILayout.Label("切换到：" + (background == null ? "未选择" : background.label), smallStyle);
                        break;
                    case StoryNodeKind.Stage:
                        GUILayout.Label("进入关卡 " + node.levelId + "   /   " +
                            (node.autoplay ? "自动演示" : "正常游玩"), smallStyle);
                        break;
                    case StoryNodeKind.End:
                        GUILayout.Label(node.text, textPreviewStyle);
                        break;
                }
                EditorGUILayout.EndVertical();
                GUILayout.Space(4);
            }
        }

        void DrawAssets(Rect area)
        {
            EditorGUI.DrawRect(area, new Color(.082f, .089f, .107f));
            GUILayout.BeginArea(new Rect(area.x + 12, area.y + 12, area.width - 24, area.height - 24));
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            AssetTab("角色", AssetSection.Cast);
            AssetTab("背景", AssetSection.Backgrounds);
            AssetTab("项目", AssetSection.Project);
            GUILayout.EndHorizontal();
            GUILayout.Space(7);
            assetScroll = EditorGUILayout.BeginScrollView(assetScroll);
            if (assetSection == AssetSection.Cast) DrawCastAssets();
            else if (assetSection == AssetSection.Backgrounds) DrawBackgroundAssets();
            else DrawProjectAssets();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void AssetTab(string label, AssetSection section)
        {
            if (GUILayout.Toggle(assetSection == section, label, EditorStyles.toolbarButton) && assetSection != section)
            {
                assetSection = section;
                GUI.FocusControl(null);
            }
        }

        void DrawCastAssets()
        {
            string before = Snapshot();
            mutationThisFrame = false;
            EditorGUI.BeginChangeCheck();
            GUILayout.Label("角色资源", sectionStyle);
            if (GUILayout.Button("+ 新增角色"))
            {
                var list = new List<StoryCharacterData>(project.characters ?? Array.Empty<StoryCharacterData>());
                string id = UniqueCharacterId("character");
                var character = new StoryCharacterData { id = id, displayName = "新角色" };
                list.Add(character);
                project.characters = list.ToArray();
                selectedCharacterId = id;
                mutationThisFrame = true;
            }
            if (project.characters == null || project.characters.Length == 0)
            {
                GUILayout.Label("还没有角色。", smallStyle);
                EditorGUI.EndChangeCheck();
                return;
            }
            foreach (var character in project.characters)
            {
                if (character == null) continue;
                if (GUILayout.Button(character.displayName,
                        character.id == selectedCharacterId ? EditorStyles.toolbarButton : GUI.skin.button))
                    selectedCharacterId = character.id;
            }
            var selected = FindCharacter(selectedCharacterId);
            if (selected == null)
            {
                selected = project.characters.FirstOrDefault(value => value != null);
                if (selected != null) selectedCharacterId = selected.id;
            }
            if (selected == null)
            {
                EditorGUI.EndChangeCheck();
                return;
            }

            GUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            string oldId = selected.id;
            selected.id = EditorGUILayout.TextField("角色 ID", selected.id);
            if (oldId != selected.id) RenameSpeaker(oldId, selected.id);
            selected.displayName = EditorGUILayout.TextField("显示名称", selected.displayName);
            Color currentColor = ParseColor(selected.colorHex);
            Color nextColor = EditorGUILayout.ColorField("名字颜色", currentColor);
            if (nextColor != currentColor) selected.colorHex = "#" + ColorUtility.ToHtmlStringRGB(nextColor);
            if (GUILayout.Button("删除角色"))
            {
                if (EditorUtility.DisplayDialog("删除角色",
                        "确定删除“" + selected.displayName + "”并清除对白引用吗？", "删除", "取消"))
                {
                    RemoveCharacter(selected.id);
                    EditorGUI.EndChangeCheck();
                    PushUndo(before);
                    return;
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Label("表情差分", sectionStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 表情槽", EditorStyles.miniButton, GUILayout.Width(70)))
            {
                var expressions = new List<StoryExpressionData>(
                    selected.expressions ?? Array.Empty<StoryExpressionData>())
                {
                    new StoryExpressionData
                    {
                        id = UniqueExpressionId(selected, "expression"),
                        label = "新表情"
                    }
                };
                selected.expressions = expressions.ToArray();
                mutationThisFrame = true;
            }
            GUILayout.EndHorizontal();

            if (selected.expressions == null) selected.expressions = Array.Empty<StoryExpressionData>();
            int removeExpression = -1;
            for (int i = 0; i < selected.expressions.Length; i++)
            {
                var expression = selected.expressions[i] ?? new StoryExpressionData();
                selected.expressions[i] = expression;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.BeginHorizontal();
                GUILayout.Label(expression.label, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(46))) removeExpression = i;
                GUILayout.EndHorizontal();
                string oldExpressionId = expression.id;
                expression.id = EditorGUILayout.TextField("表情 ID", expression.id);
                if (oldExpressionId != expression.id)
                    RenameExpression(selected.id, oldExpressionId, expression.id);
                expression.label = EditorGUILayout.TextField("显示名称", expression.label);
                expression.anchorX = EditorGUILayout.Slider("锚点 X", expression.anchorX, 0f, 1f);
                expression.anchorY = EditorGUILayout.Slider("锚点 Y", expression.anchorY, 0f, 1f);
                Texture2D texture = LoadTexture(expression.assetPath);
                var assigned = (Texture2D)EditorGUILayout.ObjectField(
                    "差分图片", texture, typeof(Texture2D), false);
                if (assigned != texture)
                    expression.assetPath = assigned == null ? "" : AssetDatabase.GetAssetPath(assigned);
                if (texture != null)
                {
                    var previewRect = GUILayoutUtility.GetRect(100, 110, GUILayout.ExpandWidth(true));
                    GUI.DrawTexture(previewRect, texture, ScaleMode.ScaleToFit, true);
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("导入图片"))
                {
                    string source = StoryAssetImporter.ChooseTextureFile("导入角色表情");
                    if (!string.IsNullOrEmpty(source))
                    {
                        if (!StoryAssetImporter.ImportExpressionFile(
                                project, selected, expression, source, out string error))
                            SetStatus(error, true);
                        else
                            SetStatus("已导入表情：" + selected.id + "/" + expression.id);
                        mutationThisFrame = true;
                    }
                }
                if (GUILayout.Button("导入文件夹"))
                {
                    string source = StoryAssetImporter.ChooseTextureFolder("导入表情差分文件夹");
                    if (!string.IsNullOrEmpty(source))
                    {
                        if (!StoryAssetImporter.ImportExpressionFolder(
                                project, selected, source, out int imported, out string error))
                            SetStatus(error, true);
                        else
                            SetStatus("已导入 " + imported + " 个表情差分。");
                        mutationThisFrame = true;
                    }
                }
                GUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUILayout.Space(4);
            }
            if (removeExpression >= 0)
            {
                string removedId = selected.expressions[removeExpression].id;
                var list = new List<StoryExpressionData>(selected.expressions);
                list.RemoveAt(removeExpression);
                selected.expressions = list.ToArray();
                ClearExpressionReferences(selected.id, removedId);
                mutationThisFrame = true;
            }
            bool changed = EditorGUI.EndChangeCheck();
            if (changed || mutationThisFrame) PushUndo(before);
        }

        void DrawBackgroundAssets()
        {
            string before = Snapshot();
            mutationThisFrame = false;
            EditorGUI.BeginChangeCheck();
            GUILayout.Label("背景资源", sectionStyle);
            if (GUILayout.Button("+ 新增背景"))
            {
                var list = new List<StoryBackgroundData>(project.backgrounds ?? Array.Empty<StoryBackgroundData>());
                string id = UniqueBackgroundId("background");
                list.Add(new StoryBackgroundData { id = id, label = "新背景" });
                project.backgrounds = list.ToArray();
                selectedBackgroundId = id;
                mutationThisFrame = true;
            }
            if (GUILayout.Button("批量导入文件夹"))
            {
                string source = StoryAssetImporter.ChooseTextureFolder("导入背景文件夹");
                if (!string.IsNullOrEmpty(source))
                {
                    if (!StoryAssetImporter.ImportBackgroundFolder(
                            project, source, out int imported, out string error))
                        SetStatus(error, true);
                    else
                        SetStatus("已导入 " + imported + " 张背景。");
                    mutationThisFrame = true;
                }
            }
            if (project.backgrounds == null || project.backgrounds.Length == 0)
            {
                GUILayout.Label("还没有背景。", smallStyle);
                EditorGUI.EndChangeCheck();
                return;
            }
            foreach (var background in project.backgrounds)
            {
                if (background == null) continue;
                if (GUILayout.Button(background.label,
                        background.id == selectedBackgroundId ? EditorStyles.toolbarButton : GUI.skin.button))
                    selectedBackgroundId = background.id;
            }
            var selected = FindBackground(selectedBackgroundId);
            if (selected == null)
            {
                selected = project.backgrounds.FirstOrDefault(value => value != null);
                if (selected != null) selectedBackgroundId = selected.id;
            }
            if (selected == null)
            {
                EditorGUI.EndChangeCheck();
                return;
            }

            GUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            string oldId = selected.id;
            selected.id = EditorGUILayout.TextField("背景 ID", selected.id);
            if (oldId != selected.id) RenameBackground(oldId, selected.id);
            selected.label = EditorGUILayout.TextField("显示名称", selected.label);
            Texture2D texture = LoadTexture(selected.assetPath);
            var assigned = (Texture2D)EditorGUILayout.ObjectField("背景图片", texture, typeof(Texture2D), false);
            if (assigned != texture)
                selected.assetPath = assigned == null ? "" : AssetDatabase.GetAssetPath(assigned);
            if (GUILayout.Button("导入图片"))
            {
                string source = StoryAssetImporter.ChooseTextureFile("导入背景图片");
                if (!string.IsNullOrEmpty(source))
                {
                    if (!StoryAssetImporter.ImportBackgroundFile(project, selected, source, out string error))
                        SetStatus(error, true);
                    else
                        SetStatus("已导入背景：" + selected.id);
                    mutationThisFrame = true;
                }
            }
            if (texture != null)
            {
                var rect = GUILayoutUtility.GetRect(180, 100, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleAndCrop, true);
            }
            if (GUILayout.Button("删除背景"))
            {
                if (EditorUtility.DisplayDialog("删除背景",
                        "确定删除“" + selected.label + "”并清除背景步骤引用吗？", "删除", "取消"))
                {
                    RemoveBackground(selected.id);
                    EditorGUI.EndChangeCheck();
                    PushUndo(before);
                    return;
                }
            }
            EditorGUILayout.EndVertical();
            bool changed = EditorGUI.EndChangeCheck();
            if (changed || mutationThisFrame) PushUndo(before);
        }

        void DrawProjectAssets()
        {
            string before = Snapshot();
            mutationThisFrame = false;
            EditorGUI.BeginChangeCheck();
            GUILayout.Label("项目设置", sectionStyle);
            project.storyId = EditorGUILayout.TextField("剧情 ID", project.storyId);
            project.title = EditorGUILayout.TextField("标题", project.title);
            project.author = EditorGUILayout.TextField("作者", project.author);
            project.assetRoot = EditorGUILayout.TextField("资源根目录", project.assetRoot);
            project.sceneMusicResource = EditorGUILayout.TextField("场景音乐资源", project.sceneMusicResource);
            GUILayout.Space(8);
            GUILayout.Label("项目文件", sectionStyle);
            EditorGUILayout.SelectableLabel(filePath, EditorStyles.textField, GUILayout.Height(20));
            if (GUILayout.Button("另存到新文件")) SaveProject(true);
            GUILayout.Space(10);
            GUILayout.Label("校验结果", sectionStyle);
            if (GUILayout.Button("校验剧情与资源")) ValidateProject();
            diagnosticsScroll = EditorGUILayout.BeginScrollView(diagnosticsScroll, GUILayout.MaxHeight(300));
            if (diagnostics == null || diagnostics.Count == 0)
            {
                GUILayout.Label("暂无问题。", smallStyle);
            }
            else
            {
                foreach (var diagnostic in diagnostics)
                {
                    Color previous = GUI.color;
                    GUI.color = diagnostic.severity == StoryDiagnosticSeverity.Error
                        ? new Color(1f, .62f, .62f) : new Color(1f, .84f, .52f);
                    if (GUILayout.Button(diagnostic.ToString(), EditorStyles.wordWrappedMiniLabel) &&
                        !string.IsNullOrEmpty(diagnostic.nodeId))
                    {
                        selectedNodeId = diagnostic.nodeId;
                        showOverview = false;
                        showPreview = false;
                    }
                    GUI.color = previous;
                }
            }
            EditorGUILayout.EndScrollView();
            bool changed = EditorGUI.EndChangeCheck();
            if (changed || mutationThisFrame) PushUndo(before);
        }

        void DrawStatus()
        {
            float y = position.height - StatusHeight;
            EditorGUI.DrawRect(new Rect(0, y, position.width, StatusHeight), new Color(.065f, .072f, .088f));
            EditorGUI.DrawRect(new Rect(0, y, position.width, 1), new Color(.2f, .24f, .3f));
            int errors = diagnostics == null
                ? 0 : diagnostics.Count(value => value.severity == StoryDiagnosticSeverity.Error);
            int warnings = diagnostics == null
                ? 0 : diagnostics.Count(value => value.severity == StoryDiagnosticSeverity.Warning);
            string message = Time.realtimeSinceStartup < statusUntil ? status : "就绪";
            if (errors > 0 || warnings > 0) message += "     错误 " + errors + "   警告 " + warnings;
            GUI.Label(new Rect(12, y + 9, position.width - 220, 21), message, statusStyle);
            if (GUI.Button(new Rect(position.width - 210, y + 6, 92, 25), "校验")) ValidateProject();
            if (GUI.Button(new Rect(position.width - 110, y + 6, 96, 25), "保存项目")) SaveProject(false);
        }

        void HandleKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || EditorGUIUtility.editingTextField) return;
            bool command = (current.modifiers & (EventModifiers.Control | EventModifiers.Command)) != 0;
            if (command && current.keyCode == KeyCode.S) SaveProject(current.shift);
            else if (command && current.keyCode == KeyCode.Z) Undo();
            else if (command && current.keyCode == KeyCode.Y) Redo();
            else if (command && current.keyCode == KeyCode.D) DuplicateStep(SelectedIndex());
            else if (current.keyCode == KeyCode.Delete) DeleteStep(SelectedIndex());
        }

        void NewProject(bool confirm)
        {
            if (confirm && !EditorUtility.DisplayDialog("新建剧情项目",
                    "确定放弃当前剧情并新建项目吗？", "新建", "取消")) return;
            project = StoryProjectFactory.CreateStarter();
            filePath = DefaultProjectPath();
            undo.Clear();
            redo.Clear();
            diagnostics.Clear();
            selectedNodeId = project.nodes.Length > 0 ? project.nodes[0].id : "";
            selectedCharacterId = project.characters.Length > 0 ? project.characters[0].id : "";
            selectedBackgroundId = project.backgrounds.Length > 0 ? project.backgrounds[0].id : "";
            showOverview = false;
            showPreview = false;
            SetStatus("已新建剧情项目。");
            Repaint();
        }

        void OpenProject()
        {
            string selected = EditorUtility.OpenFilePanel("打开剧情项目", DefaultProjectDirectory(), "json");
            if (!string.IsNullOrEmpty(selected)) LoadProject(selected, true);
        }

        void LoadProject(string path, bool showStatus)
        {
            try
            {
                project = JsonUtility.FromJson<StoryProjectData>(File.ReadAllText(path));
                if (project == null) throw new FormatException("文件中没有剧情数据。");
                filePath = path;
                NormalizeProject();
                EnsureSelection();
                undo.Clear();
                redo.Clear();
                EditorPrefs.SetString(LastProjectKey, path);
                ValidateProject();
                if (showStatus) SetStatus("已载入：" + path);
                Repaint();
            }
            catch (Exception exception)
            {
                SetStatus("载入失败：" + exception.Message, true);
                if (project == null) NewProject(false);
            }
        }

        void SaveProject(bool saveAs)
        {
            try
            {
                if (saveAs || string.IsNullOrWhiteSpace(filePath))
                {
                    string selected = EditorUtility.SaveFilePanel("保存剧情项目",
                        DefaultProjectDirectory(), project.storyId + ".json", "json");
                    if (string.IsNullOrEmpty(selected)) return;
                    filePath = selected;
                }
                NormalizeProject();
                string directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(filePath, JsonUtility.ToJson(project, true));
                string relative = FileUtil.GetProjectRelativePath(filePath);
                if (!string.IsNullOrEmpty(relative))
                    AssetDatabase.ImportAsset(relative, ImportAssetOptions.ForceUpdate);
                EditorPrefs.SetString(LastProjectKey, filePath);
                diagnostics = StoryProjectValidator.Validate(project, false);
                int errors = diagnostics.Count(value => value.severity == StoryDiagnosticSeverity.Error);
                SetStatus(errors == 0 ? "剧情项目已保存。" : "已保存，但存在 " + errors + " 个错误。", errors > 0);
            }
            catch (Exception exception)
            {
                SetStatus("保存失败：" + exception.Message, true);
            }
        }

        void ValidateProject()
        {
            if (project == null) return;
            NormalizeProject();
            diagnostics = StoryProjectValidator.Validate(project, true);
            assetSection = AssetSection.Project;
            int errors = diagnostics.Count(value => value.severity == StoryDiagnosticSeverity.Error);
            int warnings = diagnostics.Count(value => value.severity == StoryDiagnosticSeverity.Warning);
            SetStatus(errors == 0
                ? "校验通过，共 " + warnings + " 个警告。"
                : "校验失败，共 " + errors + " 个错误、" + warnings + " 个警告。", errors > 0);
            Repaint();
        }

        void AddStep(StoryNodeKind kind)
        {
            PushUndo(Snapshot());
            var node = new StoryNodeData
            {
                id = UniqueNodeId(kind.ToString().ToLowerInvariant()),
                kind = kind,
                title = DefaultNodeTitle(kind)
            };
            switch (kind)
            {
                case StoryNodeKind.Dialogue:
                    if (project.characters != null && project.characters.Length > 0 &&
                        project.characters[0] != null)
                    {
                        node.speakerId = project.characters[0].id;
                        if (project.characters[0].expressions != null &&
                            project.characters[0].expressions.Length > 0 &&
                            project.characters[0].expressions[0] != null)
                            node.expressionId = project.characters[0].expressions[0].id;
                    }
                    node.text = "在这里输入对白。";
                    break;
                case StoryNodeKind.Choice:
                    node.text = "请选择";
                    node.choices = new[]
                    {
                        new StoryChoiceData
                        {
                            id = "choice-1", text = "选项 A",
                            variable = "行动方式", value = "A"
                        },
                        new StoryChoiceData
                        {
                            id = "choice-2", text = "选项 B",
                            variable = "行动方式", value = "B"
                        }
                    };
                    break;
                case StoryNodeKind.Background:
                    if (project.backgrounds != null && project.backgrounds.Length > 0 &&
                        project.backgrounds[0] != null)
                        node.backgroundId = project.backgrounds[0].id;
                    break;
                case StoryNodeKind.Stage:
                    node.levelId = "0-1";
                    node.chartAssetPath = "Assets/RhythmDemo/Resources/Charts/geometry-demo.json";
                    break;
                case StoryNodeKind.End:
                    node.text = "剧情在这里结束。";
                    break;
            }

            int selectedIndex = SelectedIndex();
            int insertIndex = selectedIndex >= 0 ? selectedIndex + 1 : project.nodes.Length;
            var list = new List<StoryNodeData>(project.nodes);
            list.Insert(insertIndex, node);
            project.nodes = list.ToArray();
            selectedNodeId = node.id;
            showOverview = false;
            showPreview = false;
            SetStatus("已新增步骤：" + StoryEditorText.NodeKind(kind));
            Repaint();
        }

        void MoveStep(int index, int direction)
        {
            if (project.nodes == null || index < 0 || index >= project.nodes.Length) return;
            int target = index + direction;
            if (target < 0 || target >= project.nodes.Length) return;
            PushUndo(Snapshot());
            var list = new List<StoryNodeData>(project.nodes);
            var value = list[index];
            list[index] = list[target];
            list[target] = value;
            project.nodes = list.ToArray();
            selectedNodeId = value.id;
            SetStatus("步骤顺序已调整。");
            Repaint();
        }

        void DuplicateStep(int index)
        {
            if (project.nodes == null || index < 0 || index >= project.nodes.Length) return;
            PushUndo(Snapshot());
            var clone = JsonUtility.FromJson<StoryNodeData>(JsonUtility.ToJson(project.nodes[index]));
            clone.id = UniqueNodeId(clone.kind.ToString().ToLowerInvariant());
            clone.title += " 副本";
            if (clone.kind == StoryNodeKind.Choice && clone.choices != null)
            {
                for (int i = 0; i < clone.choices.Length; i++)
                    if (clone.choices[i] != null)
                        clone.choices[i].id = UniqueChoiceId(clone, clone.choices[i].id);
            }
            var list = new List<StoryNodeData>(project.nodes);
            list.Insert(index + 1, clone);
            project.nodes = list.ToArray();
            selectedNodeId = clone.id;
            SetStatus("已复制步骤。");
            Repaint();
        }

        void DeleteStep(int index)
        {
            if (project.nodes == null || index < 0 || index >= project.nodes.Length) return;
            if (project.nodes.Length <= 1)
            {
                SetStatus("剧情至少需要保留一个步骤。", true);
                return;
            }
            PushUndo(Snapshot());
            var list = new List<StoryNodeData>(project.nodes);
            list.RemoveAt(index);
            project.nodes = list.ToArray();
            int next = Mathf.Clamp(index, 0, project.nodes.Length - 1);
            selectedNodeId = project.nodes[next] == null ? "" : project.nodes[next].id;
            SetStatus("已删除步骤。");
            Repaint();
        }

        static string StepSummary(StoryNodeData node)
        {
            if (node == null) return "";
            switch (node.kind)
            {
                case StoryNodeKind.Dialogue:
                    return string.IsNullOrWhiteSpace(node.text) ? "空对白" : Shorten(node.text, 28);
                case StoryNodeKind.Choice:
                    return (node.choices == null ? 0 : node.choices.Length) + " 个选项";
                case StoryNodeKind.Background:
                    return string.IsNullOrWhiteSpace(node.backgroundId) ? "未选择背景" : node.backgroundId;
                case StoryNodeKind.Stage:
                    return "关卡 " + node.levelId;
                default:
                    return "剧情结束";
            }
        }

        static string Shorten(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
            return value.Substring(0, max) + "…";
        }

        static string DefaultNodeTitle(StoryNodeKind kind)
        {
            switch (kind)
            {
                case StoryNodeKind.Dialogue: return "对白";
                case StoryNodeKind.Choice: return "玩家选择";
                case StoryNodeKind.Background: return "切换背景";
                case StoryNodeKind.Stage: return "进入关卡";
                default: return "剧情结束";
            }
        }

        void NormalizeProject()
        {
            bool legacy = project.version < 2;
            if (legacy) project.version = 2;
            if (project.characters == null) project.characters = Array.Empty<StoryCharacterData>();
            if (project.backgrounds == null) project.backgrounds = Array.Empty<StoryBackgroundData>();
            if (project.nodes == null) project.nodes = Array.Empty<StoryNodeData>();
            if (string.IsNullOrWhiteSpace(project.assetRoot))
                project.assetRoot = "Assets/RhythmDemo/Resources/Story";
            if (string.IsNullOrWhiteSpace(project.sceneMusicResource))
                project.sceneMusicResource = "Music/story-main";
            foreach (var character in project.characters)
            {
                if (character == null) continue;
                if (character.expressions == null) character.expressions = Array.Empty<StoryExpressionData>();
                if (string.IsNullOrWhiteSpace(character.colorHex)) character.colorHex = "#F45B9A";
            }
            foreach (var node in project.nodes)
            {
                if (node == null) continue;
                if (node.choices == null) node.choices = Array.Empty<StoryChoiceData>();
                if (node.textVariants == null) node.textVariants = Array.Empty<StoryTextVariantData>();
                if (node.castIds == null) node.castIds = Array.Empty<string>();
                if (string.IsNullOrWhiteSpace(node.id)) node.id = UniqueNodeId("step");
                if (string.IsNullOrWhiteSpace(node.title)) node.title = DefaultNodeTitle(node.kind);
                if (!legacy || node.kind != StoryNodeKind.Choice || node.choices == null) continue;
                for (int i = 0; i < node.choices.Length; i++)
                {
                    var choice = node.choices[i];
                    if (choice == null) continue;
                    if (string.IsNullOrWhiteSpace(choice.variable)) choice.variable = node.id + "-选择";
                    if (string.IsNullOrWhiteSpace(choice.value)) choice.value = "选项" + (i + 1);
                }
            }
        }

        void EnsureSelection()
        {
            if (SelectedNode() == null && project.nodes.Length > 0)
                selectedNodeId = project.nodes[0] == null ? "" : project.nodes[0].id;
            if (FindCharacter(selectedCharacterId) == null && project.characters.Length > 0)
                selectedCharacterId = project.characters[0] == null ? "" : project.characters[0].id;
            if (FindBackground(selectedBackgroundId) == null && project.backgrounds.Length > 0)
                selectedBackgroundId = project.backgrounds[0] == null ? "" : project.backgrounds[0].id;
        }

        int SelectedIndex()
        {
            if (project == null || project.nodes == null) return -1;
            for (int i = 0; i < project.nodes.Length; i++)
                if (project.nodes[i] != null && project.nodes[i].id == selectedNodeId) return i;
            return -1;
        }

        StoryNodeData SelectedNode()
        {
            int index = SelectedIndex();
            return index < 0 ? null : project.nodes[index];
        }

        StoryNodeData FindNode(string id)
        {
            if (project == null || string.IsNullOrEmpty(id)) return null;
            foreach (var node in project.nodes)
                if (node != null && node.id == id) return node;
            return null;
        }

        StoryCharacterData FindCharacter(string id)
        {
            if (project == null || project.characters == null || string.IsNullOrEmpty(id)) return null;
            foreach (var character in project.characters)
                if (character != null && character.id == id) return character;
            return null;
        }

        static StoryExpressionData FindExpression(StoryCharacterData character, string id)
        {
            if (character == null || character.expressions == null || string.IsNullOrEmpty(id)) return null;
            foreach (var expression in character.expressions)
                if (expression != null && expression.id == id) return expression;
            return null;
        }

        StoryBackgroundData FindBackground(string id)
        {
            if (project == null || project.backgrounds == null || string.IsNullOrEmpty(id)) return null;
            foreach (var background in project.backgrounds)
                if (background != null && background.id == id) return background;
            return null;
        }

        void CharacterPopup(string label, ref string value)
        {
            int count = project.characters == null ? 0 : project.characters.Length;
            string[] options = new string[count + 1];
            options[0] = "无";
            int current = 0;
            for (int i = 0; i < count; i++)
            {
                var character = project.characters[i];
                options[i + 1] = character == null ? "（缺失）" : character.displayName;
                if (character != null && character.id == value) current = i + 1;
            }
            int next = EditorGUILayout.Popup(label, current, options);
            if (next != current)
                value = next == 0 || project.characters[next - 1] == null ? "" : project.characters[next - 1].id;
        }

        static void ExpressionPopup(StoryCharacterData character, string label, ref string value)
        {
            int count = character == null || character.expressions == null ? 0 : character.expressions.Length;
            string[] options = new string[count + 1];
            options[0] = "无";
            int current = 0;
            for (int i = 0; i < count; i++)
            {
                var expression = character.expressions[i];
                options[i + 1] = expression == null ? "（缺失）" : expression.label;
                if (expression != null && expression.id == value) current = i + 1;
            }
            int next = EditorGUILayout.Popup(label, current, options);
            if (next != current)
                value = next == 0 || character.expressions[next - 1] == null
                    ? "" : character.expressions[next - 1].id;
        }

        void BackgroundPopup(ref string value)
        {
            int count = project.backgrounds == null ? 0 : project.backgrounds.Length;
            string[] options = new string[count + 1];
            options[0] = "无";
            int current = 0;
            for (int i = 0; i < count; i++)
            {
                var background = project.backgrounds[i];
                options[i + 1] = background == null ? "（缺失）" : background.label;
                if (background != null && background.id == value) current = i + 1;
            }
            int next = EditorGUILayout.Popup("背景", current, options);
            if (next != current)
                value = next == 0 || project.backgrounds[next - 1] == null ? "" : project.backgrounds[next - 1].id;
        }

        static StoryNodeKind NodeKindPopup(string label, StoryNodeKind value)
        {
            var values = new[]
            {
                StoryNodeKind.Dialogue,
                StoryNodeKind.Choice,
                StoryNodeKind.Background,
                StoryNodeKind.Stage,
                StoryNodeKind.End
            };
            var labels = new[] { "对白", "选项", "背景", "进入关卡", "结束" };
            int current = Mathf.Max(0, Array.IndexOf(values, value));
            int next = EditorGUILayout.Popup(label, current, labels);
            return values[Mathf.Clamp(next, 0, values.Length - 1)];
        }

        string UniqueNodeId(string baseId)
        {
            string id = StoryAssetImporter.SafeName(baseId);
            int suffix = 2;
            while (FindNode(id) != null) id = StoryAssetImporter.SafeName(baseId) + "-" + suffix++;
            return id;
        }

        string UniqueCharacterId(string baseId)
        {
            string id = StoryAssetImporter.SafeName(baseId);
            int suffix = 2;
            while (FindCharacter(id) != null) id = StoryAssetImporter.SafeName(baseId) + "-" + suffix++;
            return id;
        }

        string UniqueBackgroundId(string baseId)
        {
            string id = StoryAssetImporter.SafeName(baseId);
            int suffix = 2;
            while (FindBackground(id) != null) id = StoryAssetImporter.SafeName(baseId) + "-" + suffix++;
            return id;
        }

        static string UniqueExpressionId(StoryCharacterData character, string baseId)
        {
            string id = StoryAssetImporter.SafeName(baseId);
            int suffix = 2;
            while (FindExpression(character, id) != null)
                id = StoryAssetImporter.SafeName(baseId) + "-" + suffix++;
            return id;
        }

        static string UniqueChoiceId(StoryNodeData node, string baseId)
        {
            string id = StoryAssetImporter.SafeName(baseId);
            int suffix = 2;
            while (ChoiceExists(node, id)) id = StoryAssetImporter.SafeName(baseId) + "-" + suffix++;
            return id;
        }

        static bool ChoiceExists(StoryNodeData node, string id)
        {
            if (node == null || node.choices == null) return false;
            foreach (var choice in node.choices)
                if (choice != null && choice.id == id) return true;
            return false;
        }

        void RenameSpeaker(string oldId, string newId)
        {
            if (string.IsNullOrEmpty(oldId) || oldId == newId) return;
            foreach (var node in project.nodes)
                if (node != null && node.speakerId == oldId) node.speakerId = newId;
        }

        void RenameExpression(string characterId, string oldId, string newId)
        {
            if (string.IsNullOrEmpty(oldId) || oldId == newId) return;
            foreach (var node in project.nodes)
            {
                if (node == null || node.speakerId != characterId || node.expressionId != oldId) continue;
                node.expressionId = newId;
            }
        }

        void RenameBackground(string oldId, string newId)
        {
            if (string.IsNullOrEmpty(oldId) || oldId == newId) return;
            foreach (var node in project.nodes)
                if (node != null && node.backgroundId == oldId) node.backgroundId = newId;
        }

        void ClearExpressionReferences(string characterId, string expressionId)
        {
            foreach (var node in project.nodes)
                if (node != null && node.speakerId == characterId && node.expressionId == expressionId)
                    node.expressionId = "";
        }

        void RemoveCharacter(string id)
        {
            var characters = new List<StoryCharacterData>(project.characters);
            characters.RemoveAll(value => value == null || value.id == id);
            project.characters = characters.ToArray();
            foreach (var node in project.nodes)
            {
                if (node == null || node.speakerId != id) continue;
                node.speakerId = "";
                node.expressionId = "";
            }
            selectedCharacterId = characters.Count > 0 ? characters[0].id : "";
            SetStatus("已删除角色。");
            Repaint();
        }

        void RemoveBackground(string id)
        {
            var backgrounds = new List<StoryBackgroundData>(project.backgrounds);
            backgrounds.RemoveAll(value => value == null || value.id == id);
            project.backgrounds = backgrounds.ToArray();
            foreach (var node in project.nodes)
                if (node != null && node.backgroundId == id) node.backgroundId = "";
            selectedBackgroundId = backgrounds.Count > 0 ? backgrounds[0].id : "";
            SetStatus("已删除背景。");
            Repaint();
        }

        static Texture2D LoadTexture(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return null;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        static TextAsset LoadTextAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return null;
            return AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        }

        static string ExpressionPath(StoryCharacterData character, string expressionId)
        {
            var expression = FindExpression(character, expressionId);
            return expression == null ? "" : expression.assetPath;
        }

        static Color ParseColor(string html)
        {
            return ColorUtility.TryParseHtmlString(string.IsNullOrEmpty(html) ? "#F45B9A" : html,
                out Color color) ? color : new Color(1f, .35f, .6f);
        }

        string Snapshot()
        {
            return project == null ? "" : JsonUtility.ToJson(project, false);
        }

        void PushUndo(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            undo.Push(json);
            while (undo.Count > 60) TrimStack(undo);
            redo.Clear();
        }

        static void TrimStack(Stack<string> stack)
        {
            string[] values = stack.ToArray();
            stack.Clear();
            for (int i = values.Length - 2; i >= 0; i--) stack.Push(values[i]);
        }

        void Undo()
        {
            if (undo.Count == 0) return;
            redo.Push(Snapshot());
            Restore(undo.Pop());
            SetStatus("已撤销。");
            Repaint();
        }

        void Redo()
        {
            if (redo.Count == 0) return;
            undo.Push(Snapshot());
            Restore(redo.Pop());
            SetStatus("已重做。");
            Repaint();
        }

        void Restore(string json)
        {
            project = JsonUtility.FromJson<StoryProjectData>(json);
            NormalizeProject();
            EnsureSelection();
        }

        void SetStatus(string message, bool error = false)
        {
            status = message;
            statusUntil = Time.realtimeSinceStartup + 8;
            if (error) Debug.LogWarning("剧情编辑器：" + message);
        }

        static string DefaultProjectDirectory()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Assets", "RhythmDemo", "Resources", "Story");
        }

        static string DefaultProjectPath()
        {
            return Path.Combine(DefaultProjectDirectory(), "main-story.json");
        }
    }
}
