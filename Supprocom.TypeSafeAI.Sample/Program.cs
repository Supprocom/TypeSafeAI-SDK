using Supprocom.TypeSafeAI;

using var client = new TypeSafeClient();

var response = await client.SystemOneAsync(
    state: "I was charged twice. Please help me get a refund.",
    questions: new Dictionary<string, TypeSafeQuestion>
    {
        ["billing"] = Question.Noul("Is this message about billing?"),
        ["tone"] = Question.Choice(
            "What is the customer's tone?",
            new Dictionary<string, string?>
            {
                ["calm"] = "Neutral or polite",
                ["frustrated"] = "Dissatisfied but constructive",
                ["angry"] = "Hostile or threatening",
            }),
        ["urgency"] = Question.Score(
            "How urgently should a support agent respond?",
            "Can wait",
            "Needs attention this week",
            "Needs attention today"),
    });

Console.WriteLine($"Billing probability: {response.Noul("billing").Probability:P1}");
Console.WriteLine($"Tone: {response.Choice("tone").Choice}");
Console.WriteLine($"Urgency score: {response.Score("urgency").Score:F2}");
Console.WriteLine($"Tokens: {response.Usage.TotalTokens}");
Console.WriteLine($"Request ID: {response.RequestId ?? "not returned"}");
