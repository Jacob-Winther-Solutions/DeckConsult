namespace EdhDeckBuilder.Tests.Agent;

/// <summary>
/// Usage instrumentation is built into the Agent layer via `UsageTracker`.
///
/// To measure real token usage on a live build:
///
/// 1. Set up your service collection with AddInfrastructure() + AddAgent()
/// 2. Get IDeckBuilder from the service provider
/// 3. Create a UsageTracker and assign it to deckBuilder.UsageTracker
/// 4. Call BuildAsync(context)
/// 5. Call tracker.FormatTable() to see detailed per-call usage
///
/// Example (in a .NET console app or integration test):
///
///     var services = new ServiceCollection();
///     services.AddInfrastructure();
///     services.AddAgent();
///     var sp = services.BuildServiceProvider();
///     var deckBuilder = sp.GetRequiredService&lt;IDeckBuilder&gt;();
///
///     var tracker = new UsageTracker();
///     deckBuilder.UsageTracker = tracker;
///
///     var result = await deckBuilder.BuildAsync(buildContext);
///
///     Console.WriteLine(tracker.FormatTable());
///     var summary = tracker.GetSummary();
///     Console.WriteLine($"Total cost: ${summary.EstimatedCostUsd:F4}");
///
/// Key files:
/// - EdhDeckBuilder.Agent/Instrumentation/UsageTracker.cs — captures per-call metrics
/// - EdhDeckBuilder.Agent/Llm/LlmClassifier.cs — calls tracker.RecordCall()
/// - EdhDeckBuilder.Agent/Llm/LlmSelector.cs — calls tracker.RecordCall()
/// - EdhDeckBuilder.Agent/Pipeline/DeckBuilder.cs — exposes UsageTracker property
///
/// This test file is intentionally empty (marked obsolete pattern) to document
/// the instrumentation usage without adding expensive live tests to the suite.
/// </summary>
[Obsolete("See comments above for instrumentation usage pattern")]
public class UsageInstrumentationTest
{
}
