using EdhDeckBuilder.Agent.Llm.Shared;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using System.Text;
using System.Text.Json.Nodes;

namespace EdhDeckBuilder.Agent.Prompts;

/// <summary>
/// Versioned prompts and tool definition for the commander discovery step.
/// Used by ClaudeCommanderSelector (user's chosen model, temperature 0.6, forced tool call).
/// </summary>
public static class CommanderSelectionPrompt
{
    public const string ToolName = "rank_commanders";

    public const string SystemPrompt =
        """
        You are an expert Magic: the Gathering deck builder ranking legendary creatures for a specific strategy.

        ## Output requirements (MANDATORY — read before deciding what to return)
        - **If the input contains 10 or fewer candidates, you MUST include EVERY candidate in the output.** Do not omit any candidate for any reason, even if you think it is weak, off-theme, or a bad fit. Weak candidates get low ranks — they do not get omitted.
        - If the input contains more than 10 candidates, return exactly the top 10.
        - Ranks MUST be contiguous integers starting at 1 (1, 2, 3, …). Do not skip numbers. Do not treat rank as a rating or letter grade. Rank 1 is the best; the highest rank is the worst.
        - Return ONLY commanders whose `oracle_id` appears in the provided list. Do not invent new ones.

        ## Evaluation criteria (apply in this order)
        1. How directly the commander's abilities support the requested archetype and themes
        2. Whether the commander's color identity enables the key supporting cards the strategy needs
        3. The commander's fit with the target power bracket
        4. Budget considerations when a price ceiling is given

        A weak candidate is still ranked — at the bottom. Do not decide for the user which candidates are "worth" including.
        """;

    public static LlmToolDefinition ToolDefinition { get; } = new()
    {
        Name        = ToolName,
        Description = "Rank EVERY provided candidate commander from best (rank 1) to worst; omit none when the input has 10 or fewer entries. Ranks must be contiguous 1..N.",
        InputSchema = JsonNode.Parse(SchemaJson)!,
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
        var expectedCount = Math.Min(candidates.Count, 10);
        sb.AppendLine(candidates.Count <= 10
            ? $"Evaluate the following {candidates.Count} commander candidates and rank ALL {candidates.Count} of them. Do not omit any."
            : $"Evaluate the following {candidates.Count} commander candidates and return the top {expectedCount}:");

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

    private const string SchemaJson = """
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

}
