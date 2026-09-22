using System;
using System.IO;
using UnityEngine;

namespace GeometryRhythm.StoryEditor
{
    public enum StoryNodeKind
    {
        Dialogue,
        Choice,
        Background,
        Stage,
        End
    }

    public static class StoryEditorText
    {
        public static string NodeKind(StoryNodeKind kind)
        {
            switch (kind)
            {
                case StoryNodeKind.Dialogue: return "对白";
                case StoryNodeKind.Choice: return "选项";
                case StoryNodeKind.Background: return "背景";
                case StoryNodeKind.Stage: return "进入关卡";
                default: return "结束";
            }
        }

        public static string Transition(string value)
        {
            switch (value)
            {
                case "cut": return "硬切";
                case "slide": return "滑动";
                default: return "淡入淡出";
            }
        }
    }

    [Serializable]
    public sealed class StoryProjectData
    {
        public int version = 2;
        public string storyId = "main-story";
        public string title = "未命名剧情";
        public string author = "";
        public string assetRoot = "Assets/RhythmDemo/Resources/Story";
        public string sceneMusicResource = "Music/story-main";
        public StoryCharacterData[] characters = Array.Empty<StoryCharacterData>();
        public StoryBackgroundData[] backgrounds = Array.Empty<StoryBackgroundData>();
        public StoryNodeData[] nodes = Array.Empty<StoryNodeData>();
    }

    [Serializable]
    public sealed class StoryCharacterData
    {
        public string id = "heroine";
        public string displayName = "女主角";
        public string colorHex = "#F45B9A";
        public StoryExpressionData[] expressions = Array.Empty<StoryExpressionData>();
    }

    [Serializable]
    public sealed class StoryExpressionData
    {
        public string id = "neutral";
        public string label = "平静";
        public string assetPath = "";
        public float anchorX = .5f;
        public float anchorY = 0f;
    }

    [Serializable]
    public sealed class StoryTextVariantData
    {
        public string variable = "态度";
        public string value = "果断";
        public string text = "";
    }

    [Serializable]
    public sealed class StoryBackgroundData
    {
        public string id = "background";
        public string label = "背景";
        public string assetPath = "";
    }

    [Serializable]
    public sealed class StoryChoiceData
    {
        public string id = "choice";
        public string text = "继续";
        public string variable = "态度";
        public string value = "默认";
    }

    [Serializable]
    public sealed class StoryNodeData
    {
        public string id = "node";
        public StoryNodeKind kind;
        public string title = "节点";

        // Dialogue
        public string speakerId = "";
        public string expressionId = "";
        public string text = "";
        public StoryTextVariantData[] textVariants = Array.Empty<StoryTextVariantData>();

        // Choice
        public StoryChoiceData[] choices = Array.Empty<StoryChoiceData>();

        // Background
        public string backgroundId = "";
        public string transition = "fade";
        public string[] castIds = Array.Empty<string>();

        // Stage
        public string levelId = "0-1";
        public string chartAssetPath = "";
        public bool autoplay;
    }

    public static class StoryProjectFactory
    {
        public static StoryProjectData CreateStarter()
        {
            var heroine = new StoryCharacterData
            {
                id = "heroine",
                displayName = "女主角",
                colorHex = "#F45B9A",
                expressions = new[]
                {
                    new StoryExpressionData
                    {
                        id = "neutral",
                        label = "平静",
                        assetPath = File.Exists(Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "Assets/RhythmDemo/StoryData/Assets/Characters/heroine/neutral.png"))
                            ? "Assets/RhythmDemo/StoryData/Assets/Characters/heroine/neutral.png"
                            : ""
                    },
                    new StoryExpressionData { id = "smile", label = "微笑" },
                    new StoryExpressionData { id = "worried", label = "担心" }
                }
            };
            var background = new StoryBackgroundData
            {
                id = "signal-room",
                label = "信号室"
            };
            var openingBackground = new StoryNodeData
            {
                id = "opening-background",
                kind = StoryNodeKind.Background,
                title = "进入信号室",
                backgroundId = background.id,
                castIds = new[] { heroine.id }
            };
            var dialogue = new StoryNodeData
            {
                id = "intro-dialogue",
                kind = StoryNodeKind.Dialogue,
                title = "开场对白",
                speakerId = heroine.id,
                expressionId = "neutral",
                text = "信号很不稳定，我们只有一次追踪它的机会。"
            };
            var choice = new StoryNodeData
            {
                id = "intro-choice",
                kind = StoryNodeKind.Choice,
                title = "玩家选择",
                text = "接下来要怎么做？",
                choices = new[]
                {
                    new StoryChoiceData
                    {
                        id = "choice-follow", text = "继续追踪信号",
                        variable = "行动方式", value = "果断"
                    },
                    new StoryChoiceData
                    {
                        id = "choice-wait", text = "先停下来分析",
                        variable = "行动方式", value = "谨慎"
                    }
                }
            };
            var reaction = new StoryNodeData
            {
                id = "reaction",
                kind = StoryNodeKind.Dialogue,
                title = "选择后的对白",
                speakerId = heroine.id,
                expressionId = "neutral",
                text = "我们继续追踪信号。",
                textVariants = new[]
                {
                    new StoryTextVariantData
                    {
                        variable = "行动方式", value = "果断",
                        text = "不能再等了，立刻启动追踪器。"
                    },
                    new StoryTextVariantData
                    {
                        variable = "行动方式", value = "谨慎",
                        text = "先确认频段，再开始追踪。"
                    }
                }
            };
            var stage = new StoryNodeData
            {
                id = "stage-01",
                kind = StoryNodeKind.Stage,
                title = "进入关卡 0-1",
                levelId = "0-1",
                chartAssetPath = "Assets/RhythmDemo/Resources/Charts/geometry-demo.json"
            };
            var ending = new StoryNodeData
            {
                id = "ending",
                kind = StoryNodeKind.End,
                title = "章节结束",
                text = "信号最终化作了一段熟悉的旋律。"
            };
            return new StoryProjectData
            {
                storyId = "main-story",
                title = "主线剧情",
                author = Environment.UserName,
                characters = new[] { heroine },
                backgrounds = new[] { background },
                nodes = new[] { openingBackground, dialogue, choice, reaction, stage, ending }
            };
        }
    }
}
