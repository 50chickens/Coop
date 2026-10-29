using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Coop.Library.Common;
using Coop.Library.Records;
using ModelContextProtocol.Protocol;

namespace Coop.Services.Surface
{
    public sealed class RecordReader
    {
        private readonly RecordStore store;

        private readonly RecordIndex index;

        public RecordReader(SurfaceConfiguration configuration)
        {
            store = new RecordStore(configuration.RecordStoreRoot);
            index = new RecordIndex(store);
        }

        public CallToolResult Get(JsonObject arguments)
        {
            JsonNode identifier = JsonKinds.NodeOf(arguments, "executionId");
            if (identifier == null)
            {
                return ToolResults.Refusal(Refusals.RequiredField("executionId"));
            }
            if (identifier.GetValueKind() != JsonValueKind.String)
            {
                return ToolResults.Refusal(Refusals.WrongKind("executionId", JsonKinds.Of(identifier), "a JSON string"));
            }
            string executionId = identifier.GetValue<string>();
            RecordLookup lookup = store.Read(executionId);
            if (lookup.Refusal != null)
            {
                return ToolResults.Refusal(Refusals.UnknownExecution(executionId, lookup.Refusal.Message));
            }
            return ToolResults.Json(DocumentOf(lookup.Record));
        }

        public CallToolResult Search(JsonObject arguments)
        {
            RecordSearchQuery query = new RecordSearchQuery();
            JsonNode limit = JsonKinds.NodeOf(arguments, "limit");
            if (limit != null)
            {
                int whole;
                if (limit.GetValueKind() != JsonValueKind.Number || limit.AsValue().TryGetValue(out whole) == false)
                {
                    return ToolResults.Refusal(Refusals.Field("limit", "limit is " + JsonKinds.Of(limit) + " rather than a whole number"));
                }
                query.Limit = whole;
            }
            query.ExecutionId = JsonKinds.TextOf(arguments, "executionId");
            query.Name = JsonKinds.TextOf(arguments, "name");
            query.Category = JsonKinds.TextOf(arguments, "category");
            query.Status = JsonKinds.TextOf(arguments, "status");
            RecordSearchResult result = index.Search(query);
            JsonArray found = new JsonArray();
            result.Records.ToList().ForEach(record => found.Add(DocumentOf(record)));
            JsonObject document = new JsonObject();
            document["records"] = found;
            document["stale"] = result.Stale;
            document["rebuilt"] = result.Rebuilt;
            document["notice"] = result.Notice;
            return ToolResults.Json(document);
        }

        private static JsonNode DocumentOf(ExecutionRecord record)
        {
            return JsonSerializer.SerializeToNode(record, CoopJson.Options);
        }
    }
}
