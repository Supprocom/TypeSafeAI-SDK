using System.Globalization;
using System.Text.Json;

namespace Supprocom.TypeSafeAI.Internal;

internal static class TypeSafeResponseParser
{
    public static SystemOneResponse ParseSystemOne(string body, string? requestId)
    {
        using var document = ParseDocument(body, requestId);
        var root = RequireObject(document.RootElement, "$", body, requestId);
        var model = RequireString(root, "model", "model", body, requestId);
        var answersElement = RequireProperty(root, "answers", "answers", body, requestId);
        RequireKind(answersElement, JsonValueKind.Object, "answers", body, requestId);

        var answers = new Dictionary<string, TypeSafeAnswer>(StringComparer.Ordinal);
        foreach (var property in answersElement.EnumerateObject())
        {
            var path = $"answers.{property.Name}";
            var answer = ParseAnswer(property.Name, property.Value, path, body, requestId);
            if (!answers.TryAdd(property.Name, answer))
            {
                throw Invalid(path, body, requestId);
            }
        }

        if (answers.Count == 0)
        {
            throw Invalid("answers", body, requestId);
        }

        var usageElement = RequireProperty(root, "usage", "usage", body, requestId);
        RequireKind(usageElement, JsonValueKind.Object, "usage", body, requestId);
        var inputTokens = RequireInt64(usageElement, "input_tokens", "usage.input_tokens", body, requestId);
        var outputTokens = RequireInt64(usageElement, "output_tokens", "usage.output_tokens", body, requestId);

        return new SystemOneResponse(
            model,
            answers,
            new TypeSafeUsage(inputTokens, outputTokens),
            requestId,
            root);
    }

    public static IReadOnlyList<TypeSafeModel> ParseModels(string body, string? requestId)
    {
        using var document = ParseDocument(body, requestId);
        var root = RequireObject(document.RootElement, "$", body, requestId);
        var modelsElement = RequireProperty(root, "models", "models", body, requestId);
        RequireKind(modelsElement, JsonValueKind.Array, "models", body, requestId);

        var models = new List<TypeSafeModel>();
        var index = 0;
        foreach (var element in modelsElement.EnumerateArray())
        {
            var path = $"models[{index}]";
            RequireKind(element, JsonValueKind.Object, path, body, requestId);
            models.Add(new TypeSafeModel(
                RequireString(element, "name", $"{path}.name", body, requestId),
                RequireString(element, "description", $"{path}.description", body, requestId),
                RequireString(element, "release_date", $"{path}.release_date", body, requestId)));
            index++;
        }

        return models.AsReadOnly();
    }

    private static TypeSafeAnswer ParseAnswer(
        string id,
        JsonElement element,
        string path,
        string body,
        string? requestId)
    {
        RequireKind(element, JsonValueKind.Object, path, body, requestId);
        var type = RequireString(element, "type", $"{path}.type", body, requestId);

        return type switch
        {
            "noul" => new NoulAnswer(
                id,
                RequireProbability(element, "noul", $"{path}.noul", body, requestId),
                element),
            "choice" => ParseChoice(id, element, path, body, requestId),
            "score" => ParseScore(id, element, path, body, requestId),
            _ => new UnknownAnswer(id, type, element),
        };
    }

    private static ChoiceAnswer ParseChoice(
        string id,
        JsonElement element,
        string path,
        string body,
        string? requestId)
    {
        var probabilitiesElement = RequireProperty(
            element,
            "probabilities",
            $"{path}.probabilities",
            body,
            requestId);
        var probabilities = ReadStringDistribution(
            probabilitiesElement,
            $"{path}.probabilities",
            body,
            requestId);

        var choice = RequireString(element, "choice", $"{path}.choice", body, requestId);
        if (probabilities.Count == 0 || !probabilities.ContainsKey(choice))
        {
            throw Invalid($"{path}.choice", body, requestId);
        }

        return new ChoiceAnswer(
            id,
            choice,
            RequireProbability(element, "confidence", $"{path}.confidence", body, requestId),
            probabilities,
            element);
    }

    private static ScoreAnswer ParseScore(
        string id,
        JsonElement element,
        string path,
        string body,
        string? requestId)
    {
        var legendElement = RequireProperty(element, "legend", $"{path}.legend", body, requestId);
        RequireKind(legendElement, JsonValueKind.Object, $"{path}.legend", body, requestId);
        var legend = new Dictionary<int, JsonElement>();
        foreach (var property in legendElement.EnumerateObject())
        {
            if (!int.TryParse(property.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var level) || level < 0)
            {
                throw Invalid($"{path}.legend.{property.Name}", body, requestId);
            }

            if (!legend.TryAdd(level, property.Value.Clone()))
            {
                throw Invalid($"{path}.legend.{property.Name}", body, requestId);
            }
        }

        var probabilitiesElement = RequireProperty(
            element,
            "probabilities",
            $"{path}.probabilities",
            body,
            requestId);
        RequireKind(probabilitiesElement, JsonValueKind.Object, $"{path}.probabilities", body, requestId);
        var probabilities = new Dictionary<int, double>();
        foreach (var property in probabilitiesElement.EnumerateObject())
        {
            if (!int.TryParse(property.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var level) ||
                level < 0 ||
                property.Value.ValueKind != JsonValueKind.Number ||
                !property.Value.TryGetDouble(out var probability) ||
                !IsProbability(probability) ||
                !probabilities.TryAdd(level, probability))
            {
                throw Invalid($"{path}.probabilities.{property.Name}", body, requestId);
            }
        }

        if (legend.Count == 0 ||
            probabilities.Count == 0 ||
            !legend.Keys.ToHashSet().SetEquals(probabilities.Keys))
        {
            throw Invalid($"{path}.probabilities", body, requestId);
        }

        var score = RequireDouble(element, "score", $"{path}.score", body, requestId);
        if (score < legend.Keys.Min() || score > legend.Keys.Max())
        {
            throw Invalid($"{path}.score", body, requestId);
        }

        return new ScoreAnswer(
            id,
            score,
            RequireProbability(element, "confidence", $"{path}.confidence", body, requestId),
            legend,
            probabilities,
            element);
    }

    private static Dictionary<string, double> ReadStringDistribution(
        JsonElement element,
        string path,
        string body,
        string? requestId)
    {
        RequireKind(element, JsonValueKind.Object, path, body, requestId);
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Number ||
                !property.Value.TryGetDouble(out var probability) ||
                !IsProbability(probability) ||
                !result.TryAdd(property.Name, probability))
            {
                throw Invalid($"{path}.{property.Name}", body, requestId);
            }
        }

        return result;
    }

    private static JsonDocument ParseDocument(string body, string? requestId)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException exception)
        {
            throw Invalid("$", body, requestId, exception);
        }
    }

    private static JsonElement RequireObject(
        JsonElement element,
        string path,
        string body,
        string? requestId)
    {
        RequireKind(element, JsonValueKind.Object, path, body, requestId);
        return element;
    }

    private static JsonElement RequireProperty(
        JsonElement element,
        string name,
        string path,
        string body,
        string? requestId)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            throw Invalid(path, body, requestId);
        }

        return value;
    }

    private static string RequireString(
        JsonElement element,
        string name,
        string path,
        string body,
        string? requestId)
    {
        var value = RequireProperty(element, name, path, body, requestId);
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not { } text)
        {
            throw Invalid(path, body, requestId);
        }

        return text;
    }

    private static double RequireDouble(
        JsonElement element,
        string name,
        string path,
        string body,
        string? requestId)
    {
        var value = RequireProperty(element, name, path, body, requestId);
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out var number) ||
            !double.IsFinite(number))
        {
            throw Invalid(path, body, requestId);
        }

        return number;
    }

    private static double RequireProbability(
        JsonElement element,
        string name,
        string path,
        string body,
        string? requestId)
    {
        var probability = RequireDouble(element, name, path, body, requestId);
        if (!IsProbability(probability))
        {
            throw Invalid(path, body, requestId);
        }

        return probability;
    }

    private static long RequireInt64(
        JsonElement element,
        string name,
        string path,
        string body,
        string? requestId)
    {
        var value = RequireProperty(element, name, path, body, requestId);
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out var number) ||
            number < 0)
        {
            throw Invalid(path, body, requestId);
        }

        return number;
    }

    private static bool IsProbability(double value) => double.IsFinite(value) && value is >= 0 and <= 1;

    private static void RequireKind(
        JsonElement element,
        JsonValueKind kind,
        string path,
        string body,
        string? requestId)
    {
        if (element.ValueKind != kind)
        {
            throw Invalid(path, body, requestId);
        }
    }

    private static TypeSafeResponseValidationException Invalid(
        string path,
        string body,
        string? requestId,
        Exception? innerException = null) =>
        new(path, body, requestId, innerException);
}
