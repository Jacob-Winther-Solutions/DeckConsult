using Anthropic.Models.Messages;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using System.Text;
using System.Text.Json;

namespace EdhDeckBuilder.Agent.Prompts;

/// <summary>
/// Versioned prompts and tool definition for the commander discovery step.
/// Used by LlmCommanderSelector (user's chosen model, temperature 0.6, forced tool call).
/// </summary>
public static class CommanderSelectionPrompt
{
    public const string ToolName = "rank_commanders";

    public const string SystemPrompt =
        """
        You are an expert Magic: the Gathering Commander deck builder.
        Evaluate the provided legendary creatures and return the top 10 commanders
        that best fit the requested strategy. If there are 10 or fewer candidates total, return all of them.

        For each candidate consider:
        - How directly the commander's abilities support the requested archetype and themes
        - Whether the commander's color identity enables the key supporting cards the strategy needs
        - The commander's fit with the target power bracket
        - Budget considerations when a price ceiling is given

        Return ONLY commanders from the provided list. Do not invent new ones.
        """;

    /// <summary>Tool definition with cache control so Anthropic can cache the schema across calls.</summary>
    public static Tool Tool { get; } = new()
    {
        Name = ToolName,
        Description = "Rank the candidate commanders from best (rank 1) to worst for the requested strategy.",
        InputSchema = BuildSchema(),
        Strict = true,
        CacheControl = new CacheControlEphemeral(),
    };

    public static string FormatUserMessage(IReadOnlyList<Card> candidates, CommanderDiscoveryRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Strategy:");
        sb.Append("- Archetypes: ");
        sb.AppendLine(request.Archetypes.Count > 0
            ? string.Join(", ", request.Archetypes)
            : "None specified");

        sb.Append("- Themes: ");
        sb.AppendLine(request.Themes.Count > 0
            ? string.Join(", ", request.Themes)
            : "None specified");

        var bracket = BracketLibrary.All[request.Bracket];
        sb.AppendLine($"- Bracket: {(int)request.Bracket} — {bracket.Name}: {bracket.Description}");

        sb.Append("- Budget: ");
        sb.AppendLine(request.MaxCardPriceUsd.HasValue
            ? $"max ${request.MaxCardPriceUsd:F2} per card"
            : "No limit");

        sb.Append("- Additional notes: ");
        sb.AppendLine(string.IsNullOrWhiteSpace(request.Description)
            ? "None"
            : request.Description);

        sb.AppendLine();
        sb.AppendLine($"Evaluate the following {candidates.Count} commander candidates and return the top 5–10:");

        foreach (var card in candidates)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"oracle_id: {card.OracleId}");
            sb.AppendLine($"Name: {card.Name}");
            sb.AppendLine($"Type: {card.TypeLine}");
            sb.AppendLine($"Color Identity: {FormatColorIdentity(card.ColorIdentity)}");

            var truncatedText = card.OracleText;
            if (truncatedText.Length > 200)
                truncatedText = truncatedText[..200] + "…";

            if (!string.IsNullOrWhiteSpace(truncatedText))
                sb.AppendLine($"Text: {truncatedText}");
        }

        return sb.ToString();
    }

    private static string FormatColorIdentity(Color color)
    {
        if (color == Color.None)
            return "Colorless";

        var colors = new List<string>();
        if ((color & Color.White) != 0) colors.Add("W");
        if ((color & Color.Blue) != 0) colors.Add("U");
        if ((color & Color.Black) != 0) colors.Add("B");
        if ((color & Color.Red) != 0) colors.Add("R");
        if ((color & Color.Green) != 0) colors.Add("G");

        return string.Join(", ", colors);
    }

    private static InputSchema BuildSchema()
    {
        const string json = """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "rankings": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "additionalProperties": false,
                    "properties": {
                      "oracle_id": { "type": "string" },
                      "rank":      { "type": "integer" },
                      "rationale": { "type": "string" }
                    },
                    "required": ["oracle_id","rank","rationale"]
                  }
                }
              },
              "required": ["rankings"]
            }
            """;

        using var doc = JsonDocument.Parse(json);
        return InputSchema.FromRawUnchecked(
            doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()));
    }
}
