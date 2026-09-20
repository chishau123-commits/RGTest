using NUnit.Framework;
using UnityEngine;

namespace GeometryRhythm.Tests
{
    /// <summary>
    /// Covers <see cref="Backdrop"/>: which scene objects become full-frame backdrops, and the
    /// local scale that makes a unit quad cover the camera frame without squashing the source.
    /// </summary>
    [TestFixture]
    public sealed class BackdropTests
    {
        const float Fov = 60f;
        const float CameraAspect = 16f / 9f;
        const float Distance = 1f;
        const float Tolerance = 1e-4f;

        static float FrameHeight => 2 * Distance * Mathf.Tan(Fov * Mathf.Deg2Rad * 0.5f);
        static float FrameWidth => FrameHeight * CameraAspect;

        static SceneObjectData MakeObject(string kind, bool background)
            => new SceneObjectData { kind = kind, background = background };

        [Test]
        public void IsBackdropNeedsTheFlag()
        {
            Assert.IsFalse(Backdrop.IsBackdrop(MakeObject("video", false)));
            Assert.IsFalse(Backdrop.IsBackdrop(MakeObject("image", false)));
        }

        [Test]
        public void IsBackdropAcceptsImageAndVideo()
        {
            Assert.IsTrue(Backdrop.IsBackdrop(MakeObject("image", true)));
            Assert.IsTrue(Backdrop.IsBackdrop(MakeObject("video", true)));
        }

        [Test]
        public void IsBackdropRejectsOtherKinds()
        {
            Assert.IsFalse(Backdrop.IsBackdrop(MakeObject("cube", true)));
            Assert.IsFalse(Backdrop.IsBackdrop(MakeObject("sphere", true)));
            Assert.IsFalse(Backdrop.IsBackdrop(MakeObject("plane", true)));
            Assert.IsFalse(Backdrop.IsBackdrop(MakeObject("obj", true)));
        }

        [Test]
        public void IsBackdropRejectsNull()
        {
            Assert.IsFalse(Backdrop.IsBackdrop(null));
        }

        [Test]
        public void MatchingAspectFillsTheFrameExactly()
        {
            var scale = Backdrop.Scale(Fov, CameraAspect, CameraAspect, Distance);
            Assert.AreEqual(FrameWidth, scale.x, Tolerance);
            Assert.AreEqual(FrameHeight, scale.y, Tolerance);
        }

        [Test]
        public void WiderSourceKeepsTheFrameHeight()
        {
            var scale = Backdrop.Scale(Fov, CameraAspect, 21f / 9f, Distance);
            Assert.AreEqual(FrameHeight, scale.y, Tolerance);
            Assert.Greater(scale.x, FrameWidth);
        }

        [Test]
        public void TallerSourceKeepsTheFrameWidth()
        {
            var scale = Backdrop.Scale(Fov, CameraAspect, 4f / 3f, Distance);
            Assert.AreEqual(FrameWidth, scale.x, Tolerance);
            Assert.Greater(scale.y, FrameHeight);
        }

        [Test]
        public void UnknownSourceAspectFallsBackToTheCamera()
        {
            var expected = Backdrop.Scale(Fov, CameraAspect, CameraAspect, Distance);
            foreach (float sourceAspect in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                var scale = Backdrop.Scale(Fov, CameraAspect, sourceAspect, Distance);
                Assert.AreEqual(expected.x, scale.x, Tolerance, "width for aspect " + sourceAspect);
                Assert.AreEqual(expected.y, scale.y, Tolerance, "height for aspect " + sourceAspect);
            }
        }

        [Test]
        public void EveryAspectCoversTheFrame()
        {
            foreach (float sourceAspect in new[] { 0.5f, 1f, 4f / 3f, 16f / 9f, 21f / 9f, 32f / 9f })
            {
                var scale = Backdrop.Scale(Fov, CameraAspect, sourceAspect, Distance);
                Assert.GreaterOrEqual(scale.x, FrameWidth - Tolerance, "width for aspect " + sourceAspect);
                Assert.GreaterOrEqual(scale.y, FrameHeight - Tolerance, "height for aspect " + sourceAspect);
            }
        }
    }
}
