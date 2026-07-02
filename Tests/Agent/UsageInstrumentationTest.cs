namespace EdhDeckBuilder.Tests.Agent;

/// <summary>
/// Usage instrumentation is now built into the Agent layer.
/// See UsageTracker, LlmClassifier.SetUsageTracker(), LlmSelector.SetUsageTracker()
/// and IDeckBuilder.UsageTracker property for integration.
///
/// To measure token usage, run the standalone console app in the scratchpad or
/// create a .NET console project that:
/// 1. Builds a ServiceCollection with AddInfrastructure() + AddAgent()
/// 2. Gets IDeckBuilder and sets UsageTracker property
/// 3. Calls BuildAsync() and prints tracker.FormatTable()
///
/// Example:
///     var tracker = new UsageTracker();
///     deckBuilder.UsageTracker = tracker;
///     var result = await deckBuilder.BuildAsync(...);
///     Console.WriteLine(tracker.FormatTable());
/// </summary>
[Obsolete("See comment above for usage instrumentation integration pattern")]
public class UsageInstrumentationTest
{
}
