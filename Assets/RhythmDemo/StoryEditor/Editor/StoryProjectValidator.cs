using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GeometryRhythm.StoryEditor
{
    public enum StoryDiagnosticSeverity
    {
        Warning,
        Error
    }

    public sealed class StoryDiagnostic
    {
        public StoryDiagnosticSeverity severity;
        public string message;
        public string nodeId;

        public override string ToString()
        {
            string location = string.IsNullOrEmpty(nodeId) ? "" : " [" + nodeId + "]";
            string level = severity == StoryDiagnosticSeverity.Error ? "错误" : "警告";
            return level + location + "  " + message;
        }
    }

    public static class StoryProjectValidator
    {
        public static List<StoryDiagnostic> Validate(StoryProjectData project, bool validateAssets)
        {
            var result = new List<StoryDiagnostic>();
            if (project == null)
            {
                Add(result, StoryDiagnosticSeverity.Error, "", "剧情项目为空。");
                return result;
            }

            if (project.version != 2)
                Add(result, StoryDiagnosticSeverity.Error, "", "不支持的剧情格式版本：" + project.version + "。");
            if (string.IsNullOrWhiteSpace(project.storyId))
                Add(result, StoryDiagnosticSeverity.Error, "", "必须填写剧情 ID。");
            if (string.IsNullOrWhiteSpace(project.title))
                Add(result, StoryDiagnosticSeverity.Warning, "", "剧情标题为空。");
            if (string.IsNullOrWhiteSpace(project.sceneMusicResource))
                Add(result, StoryDiagnosticSeverity.Warning, "", "剧情没有配置场景音乐。");
            else if (validateAssets && Resources.Load<AudioClip>(project.sceneMusicResource) == null)
                Add(result, StoryDiagnosticSeverity.Error, "",
                    "场景音乐资源不存在：" + project.sceneMusicResource + "。");

            var characters = new Dictionary<string, StoryCharacterData>(StringComparer.Ordinal);
            if (project.characters == null)
                Add(result, StoryDiagnosticSeverity.Error, "", "缺少角色列表。");
            else
            {
                foreach (var character in project.characters)
                {
                    if (character == null || string.IsNullOrWhiteSpace(character.id))
                    {
                        Add(result, StoryDiagnosticSeverity.Error, "", "存在未填写 ID 的角色。");
                        continue;
                    }
                    if (!characters.ContainsKey(character.id))
                        characters.Add(character.id, character);
                    else
                        Add(result, StoryDiagnosticSeverity.Error, "", "角色 ID 重复：" + character.id + "。");

                    var expressionIds = new HashSet<string>(StringComparer.Ordinal);
                    if (character.expressions == null)
                    {
                        Add(result, StoryDiagnosticSeverity.Warning, "", "角色没有表情配置：" + character.id + "。");
                        continue;
                    }
                    foreach (var expression in character.expressions)
                    {
                        if (expression == null || string.IsNullOrWhiteSpace(expression.id))
                        {
                            Add(result, StoryDiagnosticSeverity.Error, "", "角色 " + character.id + " 存在未填写 ID 的表情。");
                            continue;
                        }
                        if (!expressionIds.Add(expression.id))
                            Add(result, StoryDiagnosticSeverity.Error, "", "角色 " + character.id + " 的表情 ID 重复：" + expression.id + "。");
                        if (validateAssets && string.IsNullOrWhiteSpace(expression.assetPath))
                            Add(result, StoryDiagnosticSeverity.Warning, "", "表情 " + character.id + "/" + expression.id + " 尚未导入图片。");
                        else if (validateAssets && !AssetPathExists(expression.assetPath, typeof(Texture2D)))
                            Add(result, StoryDiagnosticSeverity.Error, "", "表情图片不存在：" + expression.assetPath + "。");
                    }
                }
            }

            var backgrounds = new Dictionary<string, StoryBackgroundData>(StringComparer.Ordinal);
            if (project.backgrounds == null)
                Add(result, StoryDiagnosticSeverity.Error, "", "缺少背景列表。");
            else
            {
                foreach (var background in project.backgrounds)
                {
                    if (background == null || string.IsNullOrWhiteSpace(background.id))
                    {
                        Add(result, StoryDiagnosticSeverity.Error, "", "存在未填写 ID 的背景。");
                        continue;
                    }
                    if (!backgrounds.ContainsKey(background.id))
                        backgrounds.Add(background.id, background);
                    else
                        Add(result, StoryDiagnosticSeverity.Error, "", "背景 ID 重复：" + background.id + "。");
                    if (validateAssets && string.IsNullOrWhiteSpace(background.assetPath))
                        Add(result, StoryDiagnosticSeverity.Warning, "", "背景 " + background.id + " 尚未导入图片。");
                    else if (validateAssets && !AssetPathExists(background.assetPath, typeof(Texture2D)))
                        Add(result, StoryDiagnosticSeverity.Error, "", "背景图片不存在：" + background.assetPath + "。");
                }
            }

            var nodes = new Dictionary<string, StoryNodeData>(StringComparer.Ordinal);
            if (project.nodes == null || project.nodes.Length == 0)
            {
                Add(result, StoryDiagnosticSeverity.Error, "", "剧情没有任何节点。");
                return result;
            }

            foreach (var node in project.nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.id))
                {
                    Add(result, StoryDiagnosticSeverity.Error, "", "存在未填写 ID 的节点。");
                    continue;
                }
                if (!nodes.ContainsKey(node.id))
                    nodes.Add(node.id, node);
                else
                    Add(result, StoryDiagnosticSeverity.Error, node.id, "节点 ID 重复。");
            }

            var hasEnd = false;
            for (int i = 0; i < project.nodes.Length; i++)
            {
                var node = project.nodes[i];
                if (node == null) continue;
                switch (node.kind)
                {
                    case StoryNodeKind.Dialogue:
                        ValidateDialogue(result, node, characters);
                        break;
                    case StoryNodeKind.Choice:
                        ValidateChoice(result, node, project.nodes);
                        break;
                    case StoryNodeKind.Background:
                        if (string.IsNullOrWhiteSpace(node.backgroundId) || !backgrounds.ContainsKey(node.backgroundId))
                            Add(result, StoryDiagnosticSeverity.Error, node.id, "背景不存在：" + node.backgroundId + "。");
                        if (node.castIds != null)
                        {
                            var castIds = new HashSet<string>(StringComparer.Ordinal);
                            foreach (string castId in node.castIds)
                            {
                                if (string.IsNullOrWhiteSpace(castId) || !characters.ContainsKey(castId))
                                    Add(result, StoryDiagnosticSeverity.Error, node.id,
                                        "场景角色不存在：" + castId + "。");
                                else if (!castIds.Add(castId))
                                    Add(result, StoryDiagnosticSeverity.Error, node.id,
                                        "场景角色重复：" + castId + "。");
                            }
                        }
                        break;
                    case StoryNodeKind.Stage:
                        if (string.IsNullOrWhiteSpace(node.levelId))
                            Add(result, StoryDiagnosticSeverity.Error, node.id, "必须填写关卡 ID。");
                        if (string.IsNullOrWhiteSpace(node.chartAssetPath))
                            Add(result, StoryDiagnosticSeverity.Error, node.id, "必须指定关卡谱面。");
                        else if (validateAssets && !AssetPathExists(node.chartAssetPath, typeof(TextAsset)))
                            Add(result, StoryDiagnosticSeverity.Error, node.id, "关卡谱面不存在：" + node.chartAssetPath + "。");
                        break;
                    case StoryNodeKind.End:
                        hasEnd = true;
                        break;
                    default:
                        Add(result, StoryDiagnosticSeverity.Error, node.id, "未知节点类型：" + node.kind + "。");
                        break;
                }
            }

            if (!hasEnd)
                Add(result, StoryDiagnosticSeverity.Warning, "", "剧情没有结束步骤。");
            for (int i = 0; i < project.nodes.Length; i++)
                if (project.nodes[i] != null && project.nodes[i].kind == StoryNodeKind.End && i != project.nodes.Length - 1)
                    Add(result, StoryDiagnosticSeverity.Warning, project.nodes[i].id, "结束步骤后面还有内容，游戏运行时会忽略这些内容。");
            return result;
        }

        public static bool HasErrors(List<StoryDiagnostic> diagnostics)
        {
            if (diagnostics == null) return true;
            foreach (var diagnostic in diagnostics)
                if (diagnostic.severity == StoryDiagnosticSeverity.Error) return true;
            return false;
        }

        static void ValidateDialogue(List<StoryDiagnostic> result, StoryNodeData node,
            Dictionary<string, StoryCharacterData> characters)
        {
            if (string.IsNullOrWhiteSpace(node.speakerId) || !characters.TryGetValue(node.speakerId, out var character))
            {
                Add(result, StoryDiagnosticSeverity.Error, node.id, "说话角色不存在：" + node.speakerId + "。");
                return;
            }

            bool expressionFound = false;
            if (character.expressions != null)
            {
                foreach (var expression in character.expressions)
                {
                    if (expression == null || expression.id != node.expressionId) continue;
                    expressionFound = true;
                    break;
                }
            }
            if (!expressionFound)
                Add(result, StoryDiagnosticSeverity.Error, node.id, "该角色没有这个表情：" + node.expressionId + "。");
            if (string.IsNullOrWhiteSpace(node.text))
                Add(result, StoryDiagnosticSeverity.Warning, node.id, "对白文本为空。");
            ValidateTextVariants(result, node);
        }

        static void ValidateTextVariants(List<StoryDiagnostic> result, StoryNodeData node)
        {
            if (node.textVariants == null || node.textVariants.Length == 0) return;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var variant in node.textVariants)
            {
                if (variant == null)
                {
                    Add(result, StoryDiagnosticSeverity.Error, node.id, "文本差异列表中存在空项。");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(variant.variable) || string.IsNullOrWhiteSpace(variant.value))
                {
                    Add(result, StoryDiagnosticSeverity.Error, node.id, "文本差异缺少变量名或选项值。");
                    continue;
                }
                string key = variant.variable + "\n" + variant.value;
                if (!keys.Add(key))
                    Add(result, StoryDiagnosticSeverity.Error, node.id,
                        "文本差异重复：" + variant.variable + " = " + variant.value + "。");
                if (string.IsNullOrWhiteSpace(variant.text))
                    Add(result, StoryDiagnosticSeverity.Warning, node.id,
                        "文本差异为空：" + variant.variable + " = " + variant.value + "。");
            }
        }

        static void ValidateChoice(List<StoryDiagnostic> result, StoryNodeData node,
            StoryNodeData[] allNodes)
        {
            if (string.IsNullOrWhiteSpace(node.text))
                Add(result, StoryDiagnosticSeverity.Warning, node.id, "选项提示为空。");
            if (node.choices == null || node.choices.Length == 0)
            {
                Add(result, StoryDiagnosticSeverity.Error, node.id, "选项节点没有任何选项。");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var variableValues = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var choice in node.choices)
            {
                if (choice == null)
                {
                    Add(result, StoryDiagnosticSeverity.Error, node.id, "选项列表中存在空项。");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(choice.id) || !ids.Add(choice.id))
                    Add(result, StoryDiagnosticSeverity.Error, node.id, "选项 ID 为空或重复：" + choice.id + "。");
                if (string.IsNullOrWhiteSpace(choice.text))
                    Add(result, StoryDiagnosticSeverity.Warning, node.id, "选项文本为空。");
                if (string.IsNullOrWhiteSpace(choice.variable) || string.IsNullOrWhiteSpace(choice.value))
                {
                    Add(result, StoryDiagnosticSeverity.Error, node.id,
                        "选项 " + choice.id + " 缺少变量名或选项值。");
                    continue;
                }
                if (!variableValues.TryGetValue(choice.variable, out var values))
                {
                    values = new HashSet<string>(StringComparer.Ordinal);
                    variableValues.Add(choice.variable, values);
                }
                if (!values.Add(choice.value))
                    Add(result, StoryDiagnosticSeverity.Error, node.id,
                        "同一选项中的变量 " + choice.variable + " 重复使用值 " + choice.value + "。");
            }
            ValidateVariantUsage(result, node, allNodes, variableValues);
        }

        static void ValidateVariantUsage(List<StoryDiagnostic> result, StoryNodeData choiceNode,
            StoryNodeData[] allNodes, Dictionary<string, HashSet<string>> variables)
        {
            if (allNodes == null) return;
            int choiceIndex = Array.IndexOf(allNodes, choiceNode);
            if (choiceIndex < 0) return;
            foreach (var pair in variables)
            {
                bool used = false;
                for (int i = choiceIndex + 1; i < allNodes.Length && !used; i++)
                {
                    var node = allNodes[i];
                    if (node == null || node.textVariants == null) continue;
                    foreach (var variant in node.textVariants)
                    {
                        if (variant == null || variant.variable != pair.Key) continue;
                        used = true;
                        break;
                    }
                }
                if (!used)
                    Add(result, StoryDiagnosticSeverity.Warning, choiceNode.id,
                        "变量“" + pair.Key + "”没有影响后续任何文本。");
            }
        }

        static bool AssetPathExists(string path, Type type)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!path.StartsWith("Assets/", StringComparison.Ordinal)) return false;
            return AssetDatabase.LoadAssetAtPath(path, type) != null;
        }

        static void Add(List<StoryDiagnostic> result, StoryDiagnosticSeverity severity, string nodeId, string message)
        {
            result.Add(new StoryDiagnostic { severity = severity, nodeId = nodeId, message = message });
        }
    }
}
