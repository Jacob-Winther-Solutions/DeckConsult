using Anthropic.Models.Messages;
using System.Text.Json;

namespace EdhDeckBuilder.Agent.Instrumentation;

/// <summary>
/// Static logger for classification responses. Configured globally via appsettings.
/// When enabled, creates a JSON log file per build session with per-call metrics.
/// </summary>
public static class ClassificationResponseLogger
{
    private static InstrumentationOptions? _options;
    private static string? _currentSessionLogFile;
    private static readonly object _logLock = new();

    /// <summary>Initialize the logger with options from appsettings.</summary>
    public static void Initialize(InstrumentationOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Start a new logging session. Called automatically on the first classification call
    /// if logging is enabled. Can also be called manually to start fresh.
    /// </summary>
    public static void InitializeSessionLogging()
    {
        if (_options?.LogClassificationResponses != true)
            return;

        lock (_logLock)
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EdhDeckBuilder", "logs");
            Directory.CreateDirectory(logDir);

            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
            _currentSessionLogFile = Path.Combine(logDir, $"classification_session_{timestamp}.json");

            // Initialize with empty array
            try
            {
                File.WriteAllText(_currentSessionLogFile, "[]");
            }
            catch { /* best effort */ }
        }
    }

    /// <summary>Log a single classification API call response.</summary>
    public static void LogResponse(int cardCount, int userMessageLength, Message response, long outputTokens)
    {
        if (_options?.LogClassificationResponses != true)
            return;

        // Lazy initialize session on first call
        if (_currentSessionLogFile is null)
            InitializeSessionLogging();

        if (_currentSessionLogFile is null)
            return;

        try
        {
            lock (_logLock)
            {
                ToolUseBlock? toolUse = null;
                foreach (var block in response.Content)
                {
                    if (block.TryPickToolUse(out var tu))
                    {
                        toolUse = tu;
                        break;
                    }
                }

                var toolInputJson = toolUse?.Input != null
                    ? JsonSerializer.Serialize(toolUse.Input)
                    : "(null)";

                var callRecord = new
                {
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    CardCount = cardCount,
                    UserMessageLength = userMessageLength,
                    InputTokens = response.Usage.InputTokens,
                    OutputTokens = outputTokens,
                    Tool = toolUse?.Name,
                    ToolInputLength = toolInputJson.Length,
                    ToolInputSample = toolInputJson[..Math.Min(500, toolInputJson.Length)],
                    ClassificationsCount = GetClassificationsCount(toolUse?.Input),
                };

                // Read existing array, add new record, write back
                var existingJson = File.ReadAllText(_currentSessionLogFile);
                var records = JsonSerializer.Deserialize<List<object>>(existingJson) ?? [];
                records.Add(callRecord);

                var updatedJson = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_currentSessionLogFile, updatedJson);
            }
        }
        catch
        {
            // Best effort — don't crash if logging fails
        }
    }

    private static int GetClassificationsCount(IReadOnlyDictionary<string, JsonElement>? toolInput)
    {
        if (toolInput == null)
            return 0;

        try
        {
            if (toolInput.TryGetValue("classifications", out var elem) && elem.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                return elem.GetArrayLength();
            }
        }
        catch { }

        return 0;
    }
}
