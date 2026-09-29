using UnityEngine;

namespace GeometryRhythm
{
    /// <summary>
    /// The playfield is always 16:9.
    ///
    /// Charts place notes in world space and are read through a perspective camera, so
    /// the screen's aspect ratio decides how wide that reading looks: the same chart
    /// stretches on a 20:9 phone and squashes on a 4:3 tablet. Locking the render
    /// viewport to 16:9 (with mattes where the display is not 16:9) keeps one chart
    /// looking the same everywhere, and keeps it the same shape as a Thart capture,
    /// which is recorded inside a 16:9 frame on the tablet.
    ///
    /// Only the render viewport is locked here. Note projection, judgement geometry and
    /// input all keep using <see cref="Camera.WorldToScreenPoint"/>, which reports full
    /// screen pixels including the viewport offset, so touch aiming is unaffected.
    /// </summary>
    public static class GameViewport
    {
        public const float Aspect = 16f / 9f;

        /// <summary>Pixels the camera renders into: a render target when one is set.</summary>
        public static Vector2 PixelSize(Camera camera)
        {
            if (camera != null && camera.targetTexture != null)
                return new Vector2(camera.targetTexture.width, camera.targetTexture.height);
            return new Vector2(Screen.width, Screen.height);
        }

        /// <summary>
        /// Largest centred 16:9 rectangle inside the given pixel size.
        /// Sizes are whole 16x9 blocks so the ratio stays exact after raster rounding,
        /// matching what the chart editor does for its video frame.
        /// </summary>
        public static Rect Frame(Vector2 pixelSize)
        {
            float width = Mathf.Max(16f, pixelSize.x);
            float height = Mathf.Max(9f, pixelSize.y);
            float blocks = Mathf.Max(1f, Mathf.Floor(Mathf.Min(width / 16f, height / 9f)));
            float frameWidth = blocks * 16f;
            float frameHeight = blocks * 9f;
            return new Rect(Mathf.Round((width - frameWidth) * .5f), Mathf.Round((height - frameHeight) * .5f),
                frameWidth, frameHeight);
        }

        /// <summary>The 16:9 rectangle for the current display.</summary>
        public static Rect Frame()
        {
            return Frame(new Vector2(Screen.width, Screen.height));
        }

        /// <summary>
        /// Lock a rendered camera to the 16:9 frame. Offscreen QA renders that own a
        /// render target keep the whole target instead: their canvas/HUD layout is
        /// computed in screen space, and the capture is already a fixed 16:9 frame.
        /// </summary>
        public static void Apply(Camera camera)
        {
            if (camera == null) return;

            if (camera.targetTexture != null)
            {
                var full = new Rect(0f, 0f, 1f, 1f);
                if (camera.rect != full) camera.rect = full;
                return;
            }

            camera.pixelRect = Frame();
        }

        /// <summary>
        /// UI content rectangle: the 16:9 playfield limited to the device safe area.
        /// Boards fitted to this never spill into the mattes.
        /// </summary>
        public static Rect Content(Rect safeArea)
        {
            return Content(Frame(), safeArea);
        }

        /// <summary>Same, for an already-resolved playfield frame.</summary>
        public static Rect Content(Rect frame, Rect safeArea)
        {
            float xMin = Mathf.Max(frame.xMin, safeArea.xMin);
            float yMin = Mathf.Max(frame.yMin, safeArea.yMin);
            float xMax = Mathf.Min(frame.xMax, safeArea.xMax);
            float yMax = Mathf.Min(frame.yMax, safeArea.yMax);
            if (xMax <= xMin || yMax <= yMin) return frame;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
