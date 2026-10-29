using System.Text.Json;
using System.Text.Json.Serialization;

namespace Coop.Library.Common
{
    public static class CoopJson
    {
        private static readonly JsonSerializerOptions options = Create();

        public static JsonSerializerOptions Options
        {
            get
            {
                return options;
            }
        }

        private static JsonSerializerOptions Create()
        {
            JsonSerializerOptions created = new JsonSerializerOptions();
            created.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            created.PropertyNameCaseInsensitive = true;
            created.WriteIndented = false;
            created.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            return created;
        }
    }
}
