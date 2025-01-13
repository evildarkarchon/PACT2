// Models/PactConfiguration.cs

namespace AutoQAC.Models;

public class AutoQacConfiguration
{
    /// <summary>
    /// Whether PACT should check for updates on startup.
    /// </summary>
    public bool UpdateCheck { get; set; } = true;

    /// <summary>
    /// Whether to show extra stats about cleaned plugins in the output.
    /// </summary>
    public bool StatLogging { get; set; } = true;

    /// <summary>
    /// How long (in seconds) PACT should wait for xEdit to clean any plugin.
    /// If it takes longer than this amount, the plugin will be immediately skipped.
    /// </summary>
    public int CleaningTimeout { get; set; } = 300;

    /// <summary>
    /// How long (in days) PACT should wait until the logging journal is cleared.
    /// If PACT Journal.txt is older than this amount, it is immediately deleted.
    /// </summary>
    public int JournalExpiration { get; set; } = 7;

    /// <summary>
    /// Path to the load order file (loadorder.txt / plugins.txt).
    /// </summary>
    public string LoadOrderPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the xEdit executable (FO3Edit.exe / FNVEdit.exe / FO4Edit.exe / SSEEdit.exe).
    /// xEdit.exe is also supported, but requires that LoadOrderPath be set to loadorder.txt only.
    /// </summary>
    public string XEditPath { get; set; } = string.Empty;

    /// <summary>
    /// Allow xEdit to use partial flags.
    /// This is an extremely experimental feature that requires xEdit version >= 4.1.5b.
    /// Use at your own risk. No support will be provided for this feature.
    /// </summary>
    public bool PartialForms { get; set; }

    /// <summary>
    /// Enables features that help debug PACT.
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    /// Last detected game mode. Used internally, not part of settings file.
    /// </summary>
    public string? LastGameMode { get; set; }
}