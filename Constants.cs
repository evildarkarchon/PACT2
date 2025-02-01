namespace AutoQAC;

/// <summary>
/// Contains constant error and warning messages used throughout the application.
/// These messages were previously stored in YAML configuration.
/// </summary>
public static class Constants
{
    public static class Errors
    {
        public const string InvalidXEditFile = """
            ❌ ERROR : CANNOT DETERMINE THE SET XEDIT EXECUTABLE FROM PACT SETTINGS!
            Make sure that you have set XEDIT EXE path to a valid .exe file!
            OR try changing XEDIT EXE path to a different XEdit version.
            """;

        public const string InvalidLoadOrderFile = """
            ❌ ERROR : CANNOT PROCESS LOAD ORDER FILE FOR XEDIT IN THIS SITUATION!
            You have to set your load order file path to loadorder.txt and NOT plugins.txt
            This is so PACT can detect the right game. Change the load order file path and try again.
            """;
    }

    public static class Warnings
    {
        public const string InvalidSetup = """
            ❌  WARNING : YOUR PACT INI SETUP IS INCORRECT!
            You likely set the wrong XEdit version for your game.
            Check your EXE or PACT Settings.toml settings and try again.
            """;

        public const string OutdatedVersion = """
            ❌  WARNING : YOUR PACT VERSION IS OUTDATED!
            You can download the latest version from the PACT Nexus Page.
            https://www.nexusmods.com/fallout4/mods/48065
            """;

        public const string UpdateCheckFailed = """
            ❌  WARNING : PACT FAILED TO CHECK FOR UPDATES!
            You can download the latest version from the PACT Nexus Page.
            https://www.nexusmods.com/fallout4/mods/48065
            """;
    }
}
