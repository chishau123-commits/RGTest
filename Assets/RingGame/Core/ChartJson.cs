using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RingGame.Core
{
    // Validate the JSON boundary before deserializing: DTO defaults cannot distinguish absent values from authored zero.
    public static class ChartJson
    {
        public const int MaximumUtf8Bytes = 1024 * 1024;
        public const int MaximumDepth = 64;
        public const int MaximumNotes = 10000;
        public const int MaximumPaths = 10000;
        public const int MaximumActions = 2000;

        public static ChartDocument Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw Error("$", "JSON document is empty.");
            if (json.Length > MaximumUtf8Bytes || Encoding.UTF8.GetByteCount(json) > MaximumUtf8Bytes)
                throw Error("$", "JSON exceeds the 1 MiB UTF-8 size limit.");
            CheckStrictLexicalSyntax(json);
            try
            {
                JToken root;
                using (var input = new StringReader(json))
                using (var reader = NewReader(input))
                {
                    root = JToken.ReadFrom(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        LineInfoHandling = LineInfoHandling.Load,
                        CommentHandling = CommentHandling.Load
                    });
                    if (reader.Read()) throw Error("$", "Only one JSON document is allowed.");
                }
                ValidateDocument(root);
                var serializer = JsonSerializer.Create(new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Error,
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Double,
                    MaxDepth = MaximumDepth,
                    TypeNameHandling = TypeNameHandling.None,
                    MetadataPropertyHandling = MetadataPropertyHandling.Ignore
                });
                ChartDocument document = root.ToObject<ChartDocument>(serializer);
                // Unknown schema/motion/ease and cross-field relationships use the same shared semantic compiler.
                ChartCompiler.Compile(document);
                return document;
            }
            catch (JsonReaderException exception)
            {
                throw Error(Path(exception.Path), "Malformed JSON: " + exception.Message);
            }
            catch (JsonSerializationException exception)
            {
                throw Error(Path(exception.Path), "Value cannot be represented by the chart DTO: " + exception.Message);
            }
        }

        private static JsonTextReader NewReader(TextReader input)
        {
            return new JsonTextReader(input)
            {
                MaxDepth = MaximumDepth,
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double,
                SupportMultipleContent = false,
                CloseInput = true
            };
        }

        // Json.NET accepts several JavaScript extensions. Reject them so accepted files remain ordinary JSON.
        private static void CheckStrictLexicalSyntax(string json)
        {
            bool inString = false;
            char previousOutside = '\0';
            for (int i = 0; i < json.Length; i++)
            {
                char character = json[i];
                if (inString)
                {
                    if (character < 32) throw Error("$", "Unescaped control character in string at character " + i + ".");
                    if (character == '"') { inString = false; continue; }
                    if (character != '\\') continue;
                    if (++i >= json.Length) throw Error("$", "Incomplete string escape.");
                    char escape = json[i];
                    if (escape == 'u')
                    {
                        for (int hex = 0; hex < 4; hex++)
                        {
                            if (++i >= json.Length || !IsHex(json[i])) throw Error("$", "Invalid Unicode escape.");
                        }
                    }
                    else if (escape != '"' && escape != '\\' && escape != '/' && escape != 'b' && escape != 'f' &&
                        escape != 'n' && escape != 'r' && escape != 't') throw Error("$", "Invalid string escape.");
                    continue;
                }
                if (character == '"') { inString = true; previousOutside = '"'; continue; }
                if (character == ' ' || character == '\t' || character == '\r' || character == '\n') continue;
                if (character == '\'' || character == '/') throw Error("$", "Single quotes and comments are not valid JSON.");
                if (character == '+' || character == '.') throw Error("$", "Invalid numeric token at character " + i + ".");
                if (character == '-' || character >= '0' && character <= '9')
                {
                    i = NumberEnd(json, i) - 1;
                    previousOutside = '0';
                    continue;
                }
                if ((character == '}' || character == ']') && previousOutside == ',')
                    throw Error("$", "Trailing commas are not valid JSON.");
                previousOutside = character;
            }
            if (inString) throw Error("$", "Unterminated string.");
            // Token inspection also catches unquoted names and Json.NET's nonfinite numeric literals.
            try
            {
                using (var input = new StringReader(json))
                using (var reader = NewReader(input))
                {
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.PropertyName && reader.QuoteChar != '"')
                            throw Error(Path(reader.Path), "Property names require double quotes.");
                        if (reader.TokenType == JsonToken.Comment || reader.TokenType == JsonToken.Undefined ||
                            reader.TokenType == JsonToken.StartConstructor || reader.TokenType == JsonToken.EndConstructor)
                            throw Error(Path(reader.Path), "Unsupported non-JSON token.");
                        if (reader.TokenType == JsonToken.Float && reader.Value is double &&
                            !ChartCompiler.IsFinite((double)reader.Value))
                            throw Error(Path(reader.Path), "Numeric values must be finite.");
                    }
                }
            }
            catch (JsonReaderException exception)
            {
                throw Error(Path(exception.Path), "Malformed JSON: " + exception.Message);
            }
        }

        private static bool IsHex(char character)
        {
            return character >= '0' && character <= '9' || character >= 'a' && character <= 'f' ||
                character >= 'A' && character <= 'F';
        }

        private static int NumberEnd(string json, int start)
        {
            int index = start;
            if (json[index] == '-') index++;
            if (index >= json.Length || json[index] < '0' || json[index] > '9')
                throw Error("$", "Invalid number at character " + start + ".");
            if (json[index] == '0') index++;
            else while (index < json.Length && json[index] >= '0' && json[index] <= '9') index++;
            if (index < json.Length && json[index] == '.')
            {
                index++;
                int digitStart = index;
                while (index < json.Length && json[index] >= '0' && json[index] <= '9') index++;
                if (index == digitStart) throw Error("$", "Number fraction requires digits at character " + start + ".");
            }
            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-')) index++;
                int digitStart = index;
                while (index < json.Length && json[index] >= '0' && json[index] <= '9') index++;
                if (index == digitStart) throw Error("$", "Number exponent requires digits at character " + start + ".");
            }
            if (index < json.Length && json[index] != ' ' && json[index] != '\t' && json[index] != '\r' &&
                json[index] != '\n' && json[index] != ',' && json[index] != ']' && json[index] != '}')
                throw Error("$", "Invalid number suffix at character " + index + ".");
            return index;
        }

        private static void ValidateDocument(JToken token)
        {
            var root = Object(token, "schemaVersion", "timebase", "settings", "notes", "paths", "actions", "decorations");
            String(root["schemaVersion"]);
            if ((string)root["schemaVersion"] != ChartCompiler.SchemaVersion)
                throw Error("$.schemaVersion", "Unsupported version; expected " + ChartCompiler.SchemaVersion + ".");
            var timebase = Object(root["timebase"], "ppq", "offsetUs", "tempos");
            Integer(timebase["ppq"], true);
            if ((int)timebase["ppq"] != ChartCompiler.Ppq) throw Error(Path(timebase["ppq"].Path), "ppq must equal 960.");
            Integer(timebase["offsetUs"], false);
            var tempos = Array(timebase["tempos"], MaximumNotes);
            foreach (var entry in tempos)
            {
                var tempo = Object(entry, "tick", "bpm");
                Integer(tempo["tick"], false); Number(tempo["bpm"], false);
                Nonnegative(tempo["tick"]); Positive(tempo["bpm"]);
            }
            var settings = Object(root["settings"], "title", "requiredTouches");
            String(settings["title"]); Integer(settings["requiredTouches"], true);
            Nonempty(settings["title"]); Positive(settings["requiredTouches"]);
            var notes = Array(root["notes"], MaximumNotes);
            foreach (var entry in notes)
            {
                var note = ObjectWithOptional(entry,
                    new[] { "id", "tick", "spawnTick", "motion", "target", "radius" }, new[] { "pathId" });
                String(note["id"]); Integer(note["tick"], false); Integer(note["spawnTick"], false);
                String(note["motion"]); Point(note["target"]); Number(note["radius"], true);
                Nonempty(note["id"]); Nonnegative(note["spawnTick"]); Nonnegative(note["tick"]); Positive(note["radius"]);
                if ((long)note["tick"] <= (long)note["spawnTick"])
                    throw Error(Path(note["tick"].Path), "tick must be greater than spawnTick.");
                if (note["pathId"] != null) String(note["pathId"]);
                if ((string)note["motion"] == "arrival" && note["pathId"] == null)
                    throw Error(Path(note.Path) + ".pathId", "Required field is missing for arrival note.");
                if ((string)note["motion"] != "arrival" && (string)note["motion"] != "shrink")
                    throw Error(Path(note["motion"].Path), "Unsupported note motion.");
            }
            var paths = Array(root["paths"], MaximumPaths);
            foreach (var entry in paths)
            {
                var path = Object(entry, "id", "type", "start", "end");
                String(path["id"]); String(path["type"]); Point(path["start"]); Point(path["end"]);
                Nonempty(path["id"]);
                if ((string)path["type"] != "linear") throw Error(Path(path["type"].Path), "Unsupported path type.");
            }
            var actions = Array(root["actions"], MaximumActions);
            foreach (var entry in actions)
            {
                var action = Object(entry, "id", "eventType", "tick", "durationTicks", "position", "rotation", "scale", "ease");
                String(action["id"]); String(action["eventType"]); Integer(action["tick"], false);
                Integer(action["durationTicks"], false); Point(action["position"]);
                Number(action["rotation"], true); Number(action["scale"], true); String(action["ease"]);
                Nonempty(action["id"]); Nonnegative(action["tick"]); Nonnegative(action["durationTicks"]); Positive(action["scale"]);
                if ((string)action["eventType"] != "MoveCamera")
                    throw Error(Path(action["eventType"].Path), "Unsupported eventType.");
                if ((string)action["ease"] != "linear" && (string)action["ease"] != "smooth")
                    throw Error(Path(action["ease"].Path), "Unsupported camera ease.");
            }
            var decorations = Array(root["decorations"], 0);
            foreach (var entry in decorations) Object(entry, "id");
        }

        private static JObject Object(JToken token, params string[] required)
        {
            return ObjectWithOptional(token, required, new string[0]);
        }

        private static JObject ObjectWithOptional(JToken token, string[] required, string[] optional)
        {
            if (token == null || token.Type != JTokenType.Object)
                throw Error(token == null ? "$" : Path(token.Path), "Expected object.");
            var value = (JObject)token;
            var allowed = new HashSet<string>(required, StringComparer.Ordinal);
            foreach (var field in optional) allowed.Add(field);
            foreach (var property in value.Properties())
                if (!allowed.Contains(property.Name)) throw Error(Path(property.Path), "Unknown field.");
            foreach (var field in required)
                if (value.Property(field, StringComparison.Ordinal) == null)
                    throw Error(Path(value.Path) + "." + field, "Required field is missing.");
            return value;
        }

        private static JArray Array(JToken token, int maximumCount)
        {
            if (token == null || token.Type != JTokenType.Array)
                throw Error(token == null ? "$" : Path(token.Path), "Expected array.");
            var value = (JArray)token;
            if (value.Count > maximumCount)
                throw Error(Path(value.Path), "Array exceeds maximum item count " + maximumCount + ".");
            return value;
        }

        private static void Point(JToken token)
        {
            var value = Object(token, "x", "y");
            Number(value["x"], true); Number(value["y"], true);
        }

        private static void String(JToken token)
        {
            if (token == null || token.Type != JTokenType.String)
                throw Error(token == null ? "$" : Path(token.Path), "Expected string.");
        }

        private static void Integer(JToken token, bool int32)
        {
            long value;
            if (token == null || token.Type != JTokenType.Integer ||
                !long.TryParse(token.ToString(Formatting.None), NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out value))
                throw Error(token == null ? "$" : Path(token.Path), "Expected integer within Int64 range.");
            if (int32 && (value < int.MinValue || value > int.MaxValue))
                throw Error(Path(token.Path), "Integer is outside Int32 range.");
        }

        private static void Number(JToken token, bool float32)
        {
            double value;
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float) ||
                !double.TryParse(token.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                !ChartCompiler.IsFinite(value))
                throw Error(token == null ? "$" : Path(token.Path), "Expected finite number.");
            if (float32 && Math.Abs(value) > float.MaxValue)
                throw Error(Path(token.Path), "Number is outside Single range.");
        }

        private static void Nonnegative(JToken token)
        {
            if ((long)token < 0) throw Error(Path(token.Path), "Value must be nonnegative.");
        }

        private static void Positive(JToken token)
        {
            if ((double)token <= 0) throw Error(Path(token.Path), "Value must be positive.");
        }

        private static void Nonempty(JToken token)
        {
            if (string.IsNullOrWhiteSpace((string)token)) throw Error(Path(token.Path), "Value must be nonempty.");
        }

        private static string Path(string path)
        {
            if (string.IsNullOrEmpty(path)) return "$";
            return path[0] == '[' ? "$" + path : "$." + path;
        }

        private static ChartValidationException Error(string path, string message)
        {
            return new ChartValidationException(path + ": " + message);
        }
    }
}
