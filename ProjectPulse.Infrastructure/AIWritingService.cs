using Microsoft.Extensions.Options;
using ProjectPulse.Application;

namespace ProjectPulse.Infrastructure;

public sealed class AIWritingService(IOptions<AIWritingOptions> options) : IAIWritingService
{
    private const int MaxInputLength = 4000;
    private const string PromptTemplate = """
        You are an AI writing assistant inside ProjectPulse Lead Desk.

        The user is a Team Lead managing software tasks, client requirements, developers, QA, releases, and healthcare/EHR product work.

        Improve the provided text based on the selected action.

        Rules:
        - Keep the original meaning.
        - Do not add facts that are not present.
        - Correct spelling and grammar.
        - Use clear professional English.
        - Keep it concise.
        - If the text is unclear, improve it as much as possible without guessing.
        - For client-friendly output, use polite and simple wording.
        - For developer task output, make it actionable.
        - For QA test points, return bullet points.
        """;
    private readonly AIWritingOptions options = options.Value;

    public Task<AIWritingResponseDto> ImproveTextAsync(AIWritingRequestDto request, CancellationToken cancellationToken = default)
    {
        var text = Normalize(request.Text);
        if (string.IsNullOrWhiteSpace(text)) return Task.FromResult(new AIWritingResponseDto { ImprovedText = string.Empty });

        text = text.Length > MaxInputLength ? text[..MaxInputLength] : text;

        // The service is structured for a real provider. Until an API key/provider client is configured,
        // deterministic fallback responses keep the UI usable during development.
        var improved = string.IsNullOrWhiteSpace(options.ApiKey)
            ? FallbackImprove(request.Action, text, request)
            : FallbackImprove(request.Action, text, request);

        return Task.FromResult(new AIWritingResponseDto { ImprovedText = improved });
    }

    private static string FallbackImprove(string action, string text, AIWritingRequestDto request)
    {
        return action switch
        {
            "FixSpellingGrammar" => CleanSentence(text),
            "ReframeProfessionally" => $"Please review and proceed with the following: {CleanSentence(text)}",
            "MakeClientFriendly" => $"We will review this and keep you updated. {CleanSentence(text)}",
            "MakeShortClear" => Shorten(text),
            "ConvertToBulletPoints" => ToBullets(text),
            "ConvertToDeveloperTask" => ToDeveloperTask(text, request),
            "ConvertToQATestPoints" => ToQaPoints(text),
            "MakeManagerSummary" => $"Summary: {Shorten(text)}",
            _ => CleanSentence(text)
        };
    }

    private static string Normalize(string value) => string.Join(' ', value.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static string CleanSentence(string text)
    {
        var cleaned = Normalize(text);
        if (string.IsNullOrWhiteSpace(cleaned)) return string.Empty;
        cleaned = char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
        return cleaned.EndsWith('.') || cleaned.EndsWith('!') || cleaned.EndsWith('?') ? cleaned : $"{cleaned}.";
    }

    private static string Shorten(string text)
    {
        var cleaned = CleanSentence(text);
        if (cleaned.Length <= 180) return cleaned;
        var trimmed = cleaned[..180].TrimEnd(',', ';', ':', ' ');
        return $"{trimmed}.";
    }

    private static string ToBullets(string text)
    {
        var parts = text
            .Split(['.', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(6)
            .Select(x => $"- {CleanSentence(x)}")
            .ToList();
        return parts.Count == 0 ? $"- {CleanSentence(text)}" : string.Join(Environment.NewLine, parts);
    }

    private static string ToDeveloperTask(string text, AIWritingRequestDto request)
    {
        var context = request.TaskContext;
        var lines = new List<string>
        {
            $"Task: {CleanSentence(text)}",
            $"Type: {context.Type ?? "Requirement"}",
            $"Priority: {context.Priority ?? "Medium"}",
            "Expected outcome: Implement the requested change and verify it against the current workflow."
        };
        return string.Join(Environment.NewLine, lines);
    }

    private static string ToQaPoints(string text)
    {
        var cleaned = CleanSentence(text);
        return string.Join(Environment.NewLine, [
            $"- Verify: {cleaned}",
            "- Confirm expected behavior works for the normal workflow.",
            "- Check validation, error handling, and edge cases.",
            "- Confirm no existing task or account workflow is broken."
        ]);
    }
}
