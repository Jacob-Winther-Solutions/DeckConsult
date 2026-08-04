using System.Text.Json.Nodes;

namespace EdhDeckBuilder.Agent.Llm.Gemini;

/// <summary>
/// Response schemas for the three Gemini structured-output calls, expressed in Gemini's
/// OpenAPI 3.0 subset (uppercase types, no <c>oneOf</c>, no <c>$ref</c>).
/// <para>
/// <b>propertyOrdering</b> matters here: it fixes the JSON key order in the model's output,
/// which both stabilizes tokens across calls and — because Gemini emits keys in the order
/// listed — lets us put reasoning fields before the answer they justify. For selection this
/// means <c>rationale</c> before <c>rank</c>, which measurably improves ranking quality.
/// </para>
/// </summary>
internal static class GeminiSchemas
{
    private const string RoleEnumJson =
        """["Land","Ramp","CardAdvantage","TargetedDisruption","MassDisruption","Tutor","Protection","Recursion","Plan","Payoff","Synergy","Unmatched"]""";

    private const string RelationEnumJson =
        """["Always","Modal","Transform"]""";

    public static JsonNode BuildClassificationSchema(bool includeReasoning)
    {
        var reasoningField = includeReasoning
            ? """, "reasoning": { "type": "STRING" } """
            : "";

        var reasoningOrdering = includeReasoning ? ",\"reasoning\"" : "";

        var json =
            $$"""
            {
              "type": "OBJECT",
              "properties": {
                "classifications": {
                  "type": "ARRAY",
                  "items": {
                    "type": "OBJECT",
                    "properties": {
                      "oracle_id":    { "type": "STRING" },
                      "primary_role": { "type": "STRING", "format": "enum", "enum": {{RoleEnumJson}} },
                      "secondary": {
                        "type": "ARRAY",
                        "items": {
                          "type": "OBJECT",
                          "properties": {
                            "role":     { "type": "STRING", "format": "enum", "enum": {{RoleEnumJson}} },
                            "relation": { "type": "STRING", "format": "enum", "enum": {{RelationEnumJson}} },
                            "weight":   { "type": "NUMBER" }
                          },
                          "propertyOrdering": ["role","relation","weight"],
                          "required": ["role","relation","weight"]
                        }
                      },
                      "land_credit": { "type": "NUMBER" }
                      {{reasoningField}}
                    },
                    "propertyOrdering": ["oracle_id","primary_role","secondary","land_credit"{{reasoningOrdering}}],
                    "required": ["oracle_id","primary_role","secondary","land_credit"]
                  }
                }
              },
              "propertyOrdering": ["classifications"],
              "required": ["classifications"]
            }
            """;

        return JsonNode.Parse(json)!;
    }

    public static JsonNode BuildSelectionSchema() =>
        JsonNode.Parse(
            """
            {
              "type": "OBJECT",
              "properties": {
                "selections": {
                  "type": "ARRAY",
                  "items": {
                    "type": "OBJECT",
                    "properties": {
                      "oracle_id": { "type": "STRING" },
                      "rationale": { "type": "STRING" },
                      "rank":      { "type": "INTEGER" }
                    },
                    "propertyOrdering": ["oracle_id","rationale","rank"],
                    "required": ["oracle_id","rationale","rank"]
                  }
                }
              },
              "propertyOrdering": ["selections"],
              "required": ["selections"]
            }
            """)!;

    public static JsonNode BuildCommanderSelectionSchema() =>
        JsonNode.Parse(
            """
            {
              "type": "OBJECT",
              "properties": {
                "rankings": {
                  "type": "ARRAY",
                  "items": {
                    "type": "OBJECT",
                    "properties": {
                      "oracle_id": { "type": "STRING" },
                      "rationale": { "type": "STRING" },
                      "rank":      { "type": "INTEGER" }
                    },
                    "propertyOrdering": ["oracle_id","rationale","rank"],
                    "required": ["oracle_id","rationale","rank"]
                  }
                }
              },
              "propertyOrdering": ["rankings"],
              "required": ["rankings"]
            }
            """)!;

    public static JsonNode BuildGapPrioritizationSchema() =>
        JsonNode.Parse(
            """
            {
              "type": "OBJECT",
              "properties": {
                "prioritized_roles": {
                  "type": "ARRAY",
                  "items": { "type": "STRING" }
                }
              },
              "propertyOrdering": ["prioritized_roles"],
              "required": ["prioritized_roles"]
            }
            """)!;

    public static JsonNode BuildUpgradeSelectionSchema() =>
        JsonNode.Parse(
            """
            {
              "type": "OBJECT",
              "properties": {
                "suggestions": {
                  "type": "ARRAY",
                  "items": {
                    "type": "OBJECT",
                    "properties": {
                      "add_oracle_id":  { "type": "STRING" },
                      "add_rationale":  { "type": "STRING" },
                      "cut_oracle_id":  { "type": "STRING" },
                      "cut_rationale":  { "type": "STRING" }
                    },
                    "propertyOrdering": ["add_oracle_id","add_rationale","cut_oracle_id","cut_rationale"],
                    "required": ["add_oracle_id","add_rationale","cut_oracle_id","cut_rationale"]
                  }
                }
              },
              "propertyOrdering": ["suggestions"],
              "required": ["suggestions"]
            }
            """)!;
}
