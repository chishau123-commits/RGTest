using UnityEngine;

namespace GeometryRhythm
{
    public enum TuriExpression
    {
        Neutral,    // 平静
        Happy,      // 开心
        Sarcastic,  // 吐槽
        Surprised,  // 惊讶
        Annoyed,    // 不满
        Shy,        // 害羞
        Focused     // 专注
    }

    public static class TuriPortraits
    {
        const string ResourceRoot = "Characters/Turi/";
        public const string FullBodyResource = ResourceRoot + "turi-fullbody-pixel-v2";

        public static string TypeName(TuriExpression expression)
        {
            switch (expression)
            {
                case TuriExpression.Happy: return "开心";
                case TuriExpression.Sarcastic: return "吐槽";
                case TuriExpression.Surprised: return "惊讶";
                case TuriExpression.Annoyed: return "不满";
                case TuriExpression.Shy: return "害羞";
                case TuriExpression.Focused: return "专注";
                default: return "平静";
            }
        }

        public static string ResourcePath(TuriExpression expression)
        {
            switch (expression)
            {
                case TuriExpression.Happy: return ResourceRoot + "turi-head-happy";
                case TuriExpression.Sarcastic: return ResourceRoot + "turi-head-sarcastic";
                case TuriExpression.Surprised: return ResourceRoot + "turi-head-surprised";
                case TuriExpression.Annoyed: return ResourceRoot + "turi-head-annoyed";
                case TuriExpression.Shy: return ResourceRoot + "turi-head-shy";
                case TuriExpression.Focused: return ResourceRoot + "turi-head-focused";
                default: return ResourceRoot + "turi-head-neutral";
            }
        }

        public static Texture2D Load(TuriExpression expression)
        {
            return Resources.Load<Texture2D>(ResourcePath(expression));
        }
    }
}
