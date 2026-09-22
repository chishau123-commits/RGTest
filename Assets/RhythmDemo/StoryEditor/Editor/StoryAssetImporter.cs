using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GeometryRhythm.StoryEditor
{
    public static class StoryAssetImporter
    {
        static readonly string[] TextureExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".psd" };

        public static bool ImportExpressionFile(StoryProjectData project, StoryCharacterData character,
            StoryExpressionData expression, string sourcePath, out string error)
        {
            error = "";
            if (!CanImport(project, character, expression, sourcePath, out error)) return false;
            if (!TryCopyTexture(project, "Characters/" + SafeName(character.id), SafeName(expression.id),
                    sourcePath, out string assetPath, out error))
                return false;
            expression.assetPath = assetPath;
            if (string.IsNullOrWhiteSpace(expression.label) || expression.label == "Neutral" || expression.label == "平静")
                expression.label = PrettyName(Path.GetFileNameWithoutExtension(sourcePath));
            return true;
        }

        public static bool ImportExpressionFolder(StoryProjectData project, StoryCharacterData character,
            string sourceFolder, out int imported, out string error)
        {
            imported = 0;
            error = "";
            if (project == null || character == null || string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            {
                error = "请选择有效的表情文件夹。";
                return false;
            }

            string[] files = FindTextureFiles(sourceFolder);
            if (files.Length == 0)
            {
                error = "所选文件夹中没有 PNG、JPG、TGA 或 PSD 图片。";
                return false;
            }

            var expressions = new List<StoryExpressionData>(
                character.expressions ?? Array.Empty<StoryExpressionData>());
            foreach (string source in files)
            {
                string baseId = SafeName(Path.GetFileNameWithoutExtension(source));
                var expression = FindExpression(expressions, baseId);
                string id = expression == null ? UniqueId(expressions, baseId) : baseId;
                if (expression == null)
                {
                    expression = new StoryExpressionData { id = id, label = PrettyName(id) };
                    expressions.Add(expression);
                }
                if (ImportExpressionFile(project, character, expression, source, out error)) imported++;
                else if (imported > 0) break;
            }
            character.expressions = expressions.ToArray();
            if (imported == 0 && string.IsNullOrEmpty(error))
                error = "没有导入任何表情文件。";
            return imported > 0;
        }

        public static bool ImportBackgroundFile(StoryProjectData project, StoryBackgroundData background,
            string sourcePath, out string error)
        {
            error = "";
            if (project == null || background == null || string.IsNullOrWhiteSpace(background.id))
            {
                error = "导入图片前必须先填写背景 ID。";
                return false;
            }
            if (!File.Exists(sourcePath) || !IsTexture(sourcePath))
            {
                error = "请选择有效的 PNG、JPG、TGA 或 PSD 图片。";
                return false;
            }
            if (!TryCopyTexture(project, "Backgrounds", SafeName(background.id), sourcePath,
                    out string assetPath, out error))
                return false;
            background.assetPath = assetPath;
            if (string.IsNullOrWhiteSpace(background.label) || background.label == "Background" || background.label == "背景")
                background.label = PrettyName(Path.GetFileNameWithoutExtension(sourcePath));
            return true;
        }

        public static bool ImportBackgroundFolder(StoryProjectData project, string sourceFolder,
            out int imported, out string error)
        {
            imported = 0;
            error = "";
            if (project == null || string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            {
                error = "请选择有效的背景文件夹。";
                return false;
            }

            string[] files = FindTextureFiles(sourceFolder);
            if (files.Length == 0)
            {
                error = "所选文件夹中没有支持的图片文件。";
                return false;
            }

            var backgrounds = new List<StoryBackgroundData>(
                project.backgrounds ?? Array.Empty<StoryBackgroundData>());
            foreach (string source in files)
            {
                string id = UniqueBackgroundId(backgrounds, SafeName(Path.GetFileNameWithoutExtension(source)));
                var background = FindBackground(backgrounds, id);
                if (background == null)
                {
                    background = new StoryBackgroundData { id = id, label = PrettyName(id) };
                    backgrounds.Add(background);
                }
                if (ImportBackgroundFile(project, background, source, out error)) imported++;
                else if (imported > 0) break;
            }
            project.backgrounds = backgrounds.ToArray();
            return imported > 0;
        }

        public static string ChooseTextureFile(string title)
        {
            return EditorUtility.OpenFilePanelWithFilters(title, "",
                new[] { "图片文件", "png,jpg,jpeg,tga,psd", "所有文件", "*" });
        }

        public static string ChooseTextureFolder(string title)
        {
            return EditorUtility.OpenFolderPanel(title, "", "");
        }

        public static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unnamed";
            var chars = value.Trim().ToLowerInvariant().Select(c =>
                char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-').ToArray();
            string result = new string(chars);
            while (result.Contains("--")) result = result.Replace("--", "-");
            result = result.Trim('-');
            return string.IsNullOrEmpty(result) ? "unnamed" : result;
        }

        static bool CanImport(StoryProjectData project, StoryCharacterData character,
            StoryExpressionData expression, string sourcePath, out string error)
        {
            error = "";
            if (project == null || character == null || expression == null)
            {
                error = "请先选择角色表情。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(character.id) || string.IsNullOrWhiteSpace(expression.id))
            {
                error = "导入前必须填写角色 ID 和表情 ID。";
                return false;
            }
            if (!File.Exists(sourcePath) || !IsTexture(sourcePath))
            {
                error = "请选择有效的 PNG、JPG、TGA 或 PSD 图片。";
                return false;
            }
            return true;
        }

        static bool TryCopyTexture(StoryProjectData project, string category, string itemId,
            string sourcePath, out string assetPath, out string error)
        {
            assetPath = "";
            error = "";
            string root = NormalizeAssetRoot(project);
            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            string folder = Path.Combine(root, category.Replace('/', Path.DirectorySeparatorChar));
            string destination = Path.Combine(folder, itemId + extension);
            try
            {
                Directory.CreateDirectory(folder);
                string sourceFull = Path.GetFullPath(sourcePath);
                string destinationFull = Path.GetFullPath(destination);
                if (!string.Equals(sourceFull, destinationFull, StringComparison.OrdinalIgnoreCase))
                    File.Copy(sourceFull, destinationFull, true);
                assetPath = FileUtil.GetProjectRelativePath(destinationFull);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                ConfigureTexture(assetPath);
                return true;
            }
            catch (Exception exception)
            {
                error = "导入失败：" + exception.Message;
                return false;
            }
        }

        static string NormalizeAssetRoot(StoryProjectData project)
        {
            if (project == null || string.IsNullOrWhiteSpace(project.assetRoot))
                return Path.Combine(Application.dataPath, "RhythmDemo", "Resources", "Story");
            string root = project.assetRoot.Replace('\\', '/').TrimEnd('/');
            if (Path.IsPathRooted(root))
            {
                string projectRelative = FileUtil.GetProjectRelativePath(root);
                if (!string.IsNullOrEmpty(projectRelative))
                {
                    project.assetRoot = projectRelative;
                    return Path.GetFullPath(root);
                }
                return root;
            }
            if (!root.StartsWith("Assets/", StringComparison.Ordinal))
                root = "Assets/" + root.TrimStart('/');
            project.assetRoot = root;
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), root));
        }

        static void ConfigureTexture(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        static string[] FindTextureFiles(string folder)
        {
            return Directory.GetFiles(folder)
                .Where(IsTexture)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        static bool IsTexture(string path)
        {
            string extension = Path.GetExtension(path);
            return TextureExtensions.Any(value => string.Equals(value, extension, StringComparison.OrdinalIgnoreCase));
        }

        static StoryExpressionData FindExpression(List<StoryExpressionData> values, string id)
        {
            foreach (var value in values)
                if (value != null && string.Equals(value.id, id, StringComparison.Ordinal)) return value;
            return null;
        }

        static StoryBackgroundData FindBackground(List<StoryBackgroundData> values, string id)
        {
            foreach (var value in values)
                if (value != null && string.Equals(value.id, id, StringComparison.Ordinal)) return value;
            return null;
        }

        static string UniqueId(List<StoryExpressionData> values, string baseId)
        {
            string id = baseId;
            int suffix = 2;
            while (FindExpression(values, id) != null) id = baseId + "-" + suffix++;
            return id;
        }

        static string UniqueBackgroundId(List<StoryBackgroundData> values, string baseId)
        {
            string id = baseId;
            int suffix = 2;
            while (FindBackground(values, id) != null) id = baseId + "-" + suffix++;
            return id;
        }

        static string PrettyName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "未命名";
            string[] words = value.Replace('_', ' ').Replace('-', ' ').Split(' ');
            return string.Join(" ", words.Where(word => word.Length > 0)
                .Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));
        }
    }
}
