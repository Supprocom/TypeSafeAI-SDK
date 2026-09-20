using System.Text.Json;
using System.Text.Json.Nodes;

namespace Supprocom.TypeSafeAI.Internal;

internal static class JsonContentGuard
{
    public static JsonNode Clone(JsonNode value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        EnsureSupported(value, parameterName);
        return value.DeepClone();
    }

    public static JsonNode? CloneOrNull(JsonNode? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        EnsureSupported(value, parameterName);
        return value.DeepClone();
    }

    private static void EnsureSupported(JsonNode value, string parameterName)
    {
        if (value.GetValueKind() is not (JsonValueKind.String or JsonValueKind.Object or JsonValueKind.Array))
        {
            throw new ArgumentException(
                "The value must be a JSON string, object, or array.",
                parameterName);
        }
    }
}
