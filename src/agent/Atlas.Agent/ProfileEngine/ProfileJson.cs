using System.Text.Json;

namespace Atlas.Agent.ProfileEngine;

public static class ProfileJson
{
    public static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("json_deserialize_failed");
}