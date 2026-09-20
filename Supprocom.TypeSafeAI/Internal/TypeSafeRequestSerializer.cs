using System.Text.Json;

namespace Supprocom.TypeSafeAI.Internal;

internal static class TypeSafeRequestSerializer
{
    public static byte[] Serialize(SystemOneRequest request, string model)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("state");
            request.State.WriteTo(writer);
            writer.WriteString("model", model);
            writer.WritePropertyName("questions");
            writer.WriteStartObject();

            foreach (var (id, question) in request.Questions)
            {
                writer.WritePropertyName(id);
                WriteQuestion(writer, question);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteQuestion(Utf8JsonWriter writer, TypeSafeQuestion question)
    {
        writer.WriteStartObject();
        writer.WriteString("type", question.Type);
        if (question.Instructions is not null)
        {
            writer.WritePropertyName("instructions");
            WriteNode(writer, question.Instructions);
        }

        switch (question)
        {
            case NoulQuestion noul when noul.Criteria is not null:
                writer.WritePropertyName("criteria");
                writer.WriteStartObject();
                writer.WritePropertyName("true");
                WriteNode(writer, noul.Criteria.WhenTrue);
                writer.WritePropertyName("false");
                WriteNode(writer, noul.Criteria.WhenFalse);
                writer.WriteEndObject();
                break;

            case ChoiceQuestion choice:
                writer.WritePropertyName("criteria");
                writer.WriteStartObject();
                foreach (var (label, description) in choice.Criteria)
                {
                    writer.WritePropertyName(label);
                    WriteNode(writer, description);
                }

                writer.WriteEndObject();
                break;

            case ScoreQuestion score:
                writer.WritePropertyName("criteria");
                writer.WriteStartArray();
                foreach (var level in score.Criteria)
                {
                    level.WriteTo(writer);
                }

                writer.WriteEndArray();
                break;

            case NoulQuestion:
                break;

            default:
                throw new ArgumentException(
                    $"Unsupported question runtime type '{question.GetType().FullName}'.",
                    nameof(question));
        }

        writer.WriteEndObject();
    }

    private static void WriteNode(Utf8JsonWriter writer, System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is null)
        {
            writer.WriteNullValue();
            return;
        }

        node.WriteTo(writer);
    }
}
