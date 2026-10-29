using System.Text.Json;
using System.Text.Json.Nodes;

namespace Coop.Services.Surface
{
    public static class JsonKinds
    {
        public static string Of(JsonNode node)
        {
            if (node == null)
            {
                return "absent";
            }
            switch (node.GetValueKind())
            {
                case JsonValueKind.Object:
                    return "an object";
                case JsonValueKind.Array:
                    return "an array";
                case JsonValueKind.String:
                    return "a string";
                case JsonValueKind.Number:
                    return "a number";
                case JsonValueKind.True:
                    return "a boolean";
                case JsonValueKind.False:
                    return "a boolean";
                default:
                    return "null";
            }
        }

        public static string TextOf(JsonObject source, string name)
        {
            JsonNode node = NodeOf(source, name);
            if (node == null)
            {
                return null;
            }
            if (node.GetValueKind() != JsonValueKind.String)
            {
                return null;
            }
            return node.GetValue<string>();
        }

        public static JsonNode NodeOf(JsonObject source, string name)
        {
            if (source == null)
            {
                return null;
            }
            JsonNode node;
            if (source.TryGetPropertyValue(name, out node) == false)
            {
                return null;
            }
            if (node == null)
            {
                return null;
            }
            if (node.GetValueKind() == JsonValueKind.Null)
            {
                return null;
            }
            return node;
        }

        public static string Shortened(string text, int length)
        {
            if (text == null)
            {
                return null;
            }
            if (text.Length <= length)
            {
                return text;
            }
            return text.Substring(0, length);
        }
    }
}
