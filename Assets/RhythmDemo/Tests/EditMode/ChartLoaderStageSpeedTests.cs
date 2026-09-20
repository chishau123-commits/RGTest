using System;
using NUnit.Framework;

namespace GeometryRhythm.Tests
{
    /// <summary>
    /// Covers the stage-speed rule in <see cref="ChartLoader.Parse"/>: the reader takes
    /// unitsPerSecond from the field rather than the samples, so it must be validated even
    /// when the custom route carries no points at all.
    /// </summary>
    [TestFixture]
    public sealed class ChartLoaderStageSpeedTests
    {
        // Everything except the stage path is fixed and already satisfies Parse, so the only
        // possible failure is the stagePath fragment under test.
        static string ChartJson(string stagePathFragment)
        {
            string stagePath = string.IsNullOrEmpty(stagePathFragment)
                ? string.Empty
                : "\"stagePath\": " + stagePathFragment + ",";
            return @"{
  " + stagePath + @"
  ""version"": 1,
  ""ticksPerBeat"": 480,
  ""endBeat"": 4,
  ""approachSeconds"": 3.4,
  ""audioOffsetSeconds"": 0,
  ""tempos"": [ { ""tick"": 0, ""bpm"": 120 } ],
  ""paths"": [ { ""id"": ""p0"", ""roll"": 0 } ],
  ""sections"": [ { ""startBeat"": 0, ""name"": ""intro"", ""placements"": [ { ""pathId"": ""p0"", ""x"": 0, ""y"": 0, ""bend"": 0, ""lift"": 0 } ] } ],
  ""cameraKeys"": [ { ""beat"": 0, ""orbit"": 0, ""roll"": 0, ""distance"": 25, ""height"": 6, ""fov"": 53 } ],
  ""notes"": [ { ""id"": ""n0"", ""tick"": 0, ""pathId"": ""p0"", ""action"": ""tap"" } ]
}";
        }

        [Test]
        public void ChartWithoutStagePathIsAccepted()
        {
            Assert.DoesNotThrow(() => ChartLoader.Parse(ChartJson(null)));
        }

        [Test]
        public void EmptyStagePathObjectIsAccepted()
        {
            Assert.DoesNotThrow(() => ChartLoader.Parse(ChartJson("{}")));
        }

        [Test]
        public void EmptyPointsWithValidSpeedIsAccepted()
        {
            var chart = ChartLoader.Parse(ChartJson("{ \"unitsPerSecond\": 8, \"points\": [] }"));
            Assert.AreEqual(8f, new StageSpline(chart.stagePath).UnitsPerSecond, 1e-4f);
        }

        [Test]
        public void ZeroSpeedWithEmptyPointsIsRejected()
        {
            Assert.Throws<FormatException>(() =>
                ChartLoader.Parse(ChartJson("{ \"unitsPerSecond\": 0, \"points\": [] }")));
        }

        [Test]
        public void NegativeSpeedWithEmptyPointsIsRejected()
        {
            Assert.Throws<FormatException>(() =>
                ChartLoader.Parse(ChartJson("{ \"unitsPerSecond\": -5, \"points\": [] }")));
        }

        [Test]
        public void SpeedAboveTheLimitWithEmptyPointsIsRejected()
        {
            Assert.Throws<FormatException>(() =>
                ChartLoader.Parse(ChartJson("{ \"unitsPerSecond\": 1000, \"points\": [] }")));
        }

        [Test]
        public void OverflowingSpeedWithEmptyPointsIsRejected()
        {
            // Either the literal overflows float to Infinity and fails the finiteness check,
            // or it stays finite and fails the <= 100 check.
            Assert.Throws<FormatException>(() =>
                ChartLoader.Parse(ChartJson("{ \"unitsPerSecond\": 1e39, \"points\": [] }")));
        }

        [Test]
        public void InvalidSpeedWithPointsIsStillRejected()
        {
            Assert.Throws<FormatException>(() => ChartLoader.Parse(ChartJson(
                "{ \"unitsPerSecond\": 0, \"points\": [ {\"x\":0,\"y\":0,\"z\":0,\"roll\":0}, {\"x\":0,\"y\":0,\"z\":200,\"roll\":0} ] }")));
        }

        [Test]
        public void ValidCustomPathIsAccepted()
        {
            // The song lasts 2 s, so Parse demands 2 * 5 + SpatialDirector.FarDepth = 110
            // world units of route; this straight 200-unit path clears it.
            Assert.DoesNotThrow(() => ChartLoader.Parse(ChartJson(
                "{ \"unitsPerSecond\": 5, \"points\": [ {\"x\":0,\"y\":0,\"z\":0,\"roll\":0}, {\"x\":0,\"y\":0,\"z\":200,\"roll\":0} ] }")));
        }
    }
}
