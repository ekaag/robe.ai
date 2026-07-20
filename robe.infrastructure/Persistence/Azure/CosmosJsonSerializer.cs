using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;

namespace Robe.Infrastructure.Persistence.Azure;

// Matches the camelCase + camelCase-enum JSON convention robe.api already uses for its
// HTTP responses (see Program.cs AddJsonOptions), so Garment documents read the same way
// in Cosmos as they do over the wire. This also satisfies Cosmos's requirement that the
// document's unique id be serialized as a literal lowercase "id" property (Garment.Id ->
// "id") and that the partition key path (/userId) match the serialized property name
// (Garment.UserId -> "userId") — camelCase gives us both for free.
internal sealed class CosmosJsonSerializer : CosmosSerializer
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (stream.Length == 0) return default!;
            return JsonSerializer.Deserialize<T>(stream, Options)!;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, input, Options);
        stream.Position = 0;
        return stream;
    }
}
