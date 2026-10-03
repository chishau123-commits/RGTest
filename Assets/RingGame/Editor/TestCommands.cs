using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using UnityEditor.Build;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace RingGame.Editor
{
    /// <summary>Runs the project's single-frame NUnit EditMode suite before batch -quit.</summary>
    public static class TestCommands
    {
        public static void RunEditMode()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name.StartsWith("RingGame.Tests", StringComparison.Ordinal))
                .ToArray();
            if (assemblies.Length == 0)
                throw new BuildFailedException("No RingGame test assemblies are loaded.");
            EnsureSingleFrameTests(assemblies);
            var output = Path.GetFullPath(ReadResultsPath());
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var callbacks = new ResultsCallbacks(output);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(callbacks);
            try
            {
                api.Execute(new ExecutionSettings(new Filter
                {
                    testMode = TestMode.EditMode,
                    assemblyNames = assemblies.Select(assembly => assembly.GetName().Name).ToArray()
                }) { runSynchronously = true });
                if (!string.IsNullOrEmpty(callbacks.Error))
                    throw new BuildFailedException("EditMode test runner failed: " + callbacks.Error);
                var result = callbacks.Result;
                if (result == null)
                    throw new BuildFailedException("Synchronous test run returned without RunFinished results.");
                if (result.PassCount == 0 || result.FailCount != 0 || result.SkipCount != 0 ||
                    result.InconclusiveCount != 0 || result.ResultState != "Passed")
                    throw new BuildFailedException("EditMode tests did not all pass. Inspect " + output);
                Debug.Log("RING_TESTS_PASSED count=" + result.PassCount + " results=" + output);
            }
            finally
            {
                api.UnregisterCallbacks(callbacks);
                UnityEngine.Object.DestroyImmediate(api);
            }
        }

        private static void EnsureSingleFrameTests(Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            foreach (var type in assembly.GetTypes())
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                   BindingFlags.Static | BindingFlags.Instance))
            foreach (var attribute in method.GetCustomAttributesData())
            {
                var name = attribute.AttributeType.FullName;
                if (name == "UnityEngine.TestTools.UnityTestAttribute" ||
                    name == "UnityEngine.TestTools.UnitySetUpAttribute" ||
                    name == "UnityEngine.TestTools.UnityTearDownAttribute")
                    throw new BuildFailedException("The synchronous batch suite cannot run multi-frame test " +
                        type.FullName + "." + method.Name + ". Use an asynchronous test command for this suite.");
            }
        }

        private static string ReadResultsPath()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (arguments[index] != "-ringTestResults") continue;
                if (index + 1 == arguments.Length || arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new BuildFailedException("Missing -ringTestResults value.");
                return arguments[index + 1];
            }
            return "Builds/Validation/editmode-results.xml";
        }

        private sealed class ResultsCallbacks : IErrorCallbacks
        {
            private readonly string output;
            public ITestResultAdaptor Result { get; private set; }
            public string Error { get; private set; }

            public ResultsCallbacks(string output) { this.output = output; }
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void OnError(string message) { Error = message; }

            public void RunFinished(ITestResultAdaptor result)
            {
                Result = result;
                try { SaveResultToFile(result, output); }
                catch (Exception exception) { Error = "Writing test evidence failed: " + exception; }
            }

            // Test Framework 1.1.33 exposes ToXml(), but no public SaveResultToFile.
            // Preserve its full test tree inside a standard NUnit test-run envelope.
            private static void SaveResultToFile(ITestResultAdaptor result, string path)
            {
                var total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
                using (var writer = XmlWriter.Create(path, new XmlWriterSettings { Indent = true }))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("test-run");
                    writer.WriteAttributeString("result", result.ResultState);
                    writer.WriteAttributeString("total", total.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("testcasecount", total.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("passed", result.PassCount.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("failed", result.FailCount.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("skipped", result.SkipCount.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("inconclusive", result.InconclusiveCount.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("start-time", result.StartTime.ToUniversalTime().ToString("O"));
                    writer.WriteAttributeString("end-time", result.EndTime.ToUniversalTime().ToString("O"));
                    writer.WriteAttributeString("duration", result.Duration.ToString(CultureInfo.InvariantCulture));
                    result.ToXml().WriteTo(writer);
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }
            }
        }
    }
}
