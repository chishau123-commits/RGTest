using UnityEngine;

namespace GeometryRhythm
{
    /// <summary>Shared landscape UI safe-area fit. Only UI transforms move; the world camera
    /// and gameplay hit projection are untouched. Coordinates are physical screen pixels.</summary>
    public static class MobileUiLayout
    {
        public const float Width=1600,Height=900,TouchTarget=112;
        // Assigned only by the explicit frontend smoke test, never from normal player settings.
        public static Rect? SimulatedSafeArea;
        public static Rect SafeArea => SimulatedSafeArea??Screen.safeArea;
        public static float FitScale(Vector2 canvasSize,Vector2 screenSize,Rect safe,out Vector2 center)
        {
            float sx=canvasSize.x/Mathf.Max(1,screenSize.x),sy=canvasSize.y/Mathf.Max(1,screenSize.y);
            center=new Vector2((safe.center.x-screenSize.x*.5f)*sx,(safe.center.y-screenSize.y*.5f)*sy);
            return Mathf.Min(safe.width*sx/Width,safe.height*sy/Height);
        }
        public static void Apply(RectTransform board,Canvas canvas)
        {
            float scale=FitScale(((RectTransform)canvas.transform).rect.size,new Vector2(Screen.width,Screen.height),SafeArea,out var center);
            board.anchoredPosition=center;board.localScale=Vector3.one*scale;
        }
    }
}
