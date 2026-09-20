using System.Net;
using System.Text.Json.Nodes;
using Xunit;

namespace Supprocom.TypeSafeAI.Tests;

public sealed class RequestValidationTests
{
    [Fact]
    public void ScoreRequiresBetweenTwoAndTenLevels()
    {
        Assert.Throws<ArgumentException>(() => Question.Score("Rate it", "only one"));
        Assert.Throws<ArgumentException>(() => Question.Score(
            "Rate it",
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10"));
    }

    [Fact]
    public void ChoiceRequiresOneToTwoHundredFiftyFiveLabels()
    {
        Assert.Throws<ArgumentException>(() => Question.Choice("Choose"));
        var labels = Enumerable.Range(0, 256).Select(static index => $"choice-{index}").ToArray();
        Assert.Throws<ArgumentException>(() => Question.Choice("Choose", labels));
    }

    [Fact]
    public void TopLevelJsonValuesRejectNumbersAndBooleans()
    {
        Assert.Throws<ArgumentException>(() => new NoulQuestion(JsonValue.Create(1)));
        Assert.Throws<ArgumentException>(() => new NoulCriteria(JsonValue.Create(true)));
        Assert.Throws<ArgumentException>(() => new SystemOneRequest(
            JsonValue.Create(42)!,
            new Dictionary<string, TypeSafeQuestion>
            {
                ["question"] = Question.Noul("Is this valid?"),
            }));
    }

    [Fact]
    public void EveryQuestionAllowsInstructionsToBeOmitted()
    {
        Assert.Null(Question.Noul().Instructions);
        Assert.Null(Question.Choice(null, "one", "two").Instructions);
        Assert.Null(Question.Score(null, "low", "high").Instructions);
        Assert.Null(new ChoiceQuestion(["one", "two"]).Instructions);
        Assert.Null(new ScoreQuestion([JsonValue.Create("low")!, JsonValue.Create("high")!]).Instructions);
    }

    [Fact]
    public void RequestCopiesMutableInputs()
    {
        var state = JsonNode.Parse("{\"message\":\"original\"}")!;
        var questions = new Dictionary<string, TypeSafeQuestion>
        {
            ["question"] = Question.Noul("Original question"),
        };
        var request = new SystemOneRequest(state, questions);

        state["message"] = "changed";
        questions.Clear();

        Assert.Equal("original", request.State["message"]!.GetValue<string>());
        Assert.Single(request.Questions);
    }

    [Fact]
    public async Task NonJsonNumericValuesThrowTypedSerializationException()
    {
        using var handler = new TestHttpMessageHandler(static (_, _, _) =>
            Task.FromResult(TestHttpMessageHandler.JsonResponse(HttpStatusCode.OK, "{}")));
        using var httpClient = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
        };
        using var client = new TypeSafeClient(httpClient, new TypeSafeClientOptions
        {
            ApiKey = "test-key",
            BaseUrl = new Uri("https://unit.test"),
            Retry = TypeSafeRetryOptions.None,
        });
        var request = new SystemOneRequest(
            "state",
            new Dictionary<string, TypeSafeQuestion>
            {
                ["question"] = new NoulQuestion(new JsonObject { ["invalid"] = double.NaN }),
            });

        await Assert.ThrowsAsync<TypeSafeRequestSerializationException>(
            () => client.SystemOneAsync(request));
        Assert.Empty(handler.Requests);
    }
}
