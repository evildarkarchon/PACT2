using System.Collections.Generic;
using Mutagen.Bethesda.Starfield;

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

    public static class Update
    {
        public const string Version = "2.0";
        public const string ReleaseDate = "2022-03-29";
        public const string Site = "https://github.com/evildarkarchon/AutoQAC/releases/latest";
    }

    public static class XEditLists
    {
        public static readonly string[] Fo3 = ["FO3Edit.exe", "FO3Edit64.exe"];
        public static readonly string[] Fo4 = ["FO4Edit.exe", "FO4Edit64.exe"];

        public static readonly string[] Sse =
            ["SSEEdit.exe", "SSEEdit64.exe", "TESVEdit.exe", "TESVEdit64.exe", "TES5Edit.exe", "TES5Edit64.exe", 
                "TES5VREdit.ese, TES5VREdit64.ese"
            ];
        public static readonly string[] Fnv = ["FNVEdit.exe", "FNVEdit64.exe"];
        public static readonly string[] Tes4 = ["TES4Edit.exe", "TES4Edit64.exe"];
        public static readonly string[] XEdit = ["xEdit.exe", "xEdit64.exe"];
    }

    public static class SkipLists
    {
        public static readonly IReadOnlyList<string> Fo3 =
        [
            string.Empty, "Fallout3.esm", "Anchorage.esm", "ThePitt.esm", "BrokenSteel.esm", "PointLookout.esm",
            "Zeta.esm", "Unofficial Fallout 3 Patch.esm"
        ];

        public static readonly IReadOnlyList<string> Fo4 =
        [
            string.Empty, "Fallout4.esm", "DLCRobot.esm", "DLCCoast.esm", "DLCNukaWorld.esm", "DLCWorkshop01.esm",
            "DLCWorkshop02.esm", "DLCWorkshop03.esm", "Unofficial Fallout 4 Patch.esp", "PPF.esm", "PRP.esp",
            "PRP-Compat",
            "SS2.esm", "SS2_XPAC_Chapter2.esm", "SS2_XPAC_Chapter3.esm", "SS2Extemded.esp"
        ];

        public static readonly IReadOnlyList<string> Skyrim =
        [
            string.Empty, "Skyrim.esm", "Update.esm", "Dawnguard.esm", "Hearthfire.esm", "Dragonborn.esm",
            "Unoffcial Skyrim Special Edition Patch.esp", "Unofficial Skyrim Legendary Edition Patch.esp",
            "_ResourcePack.esl"
        ];

        public static readonly IReadOnlyList<string> Fnv =
        [
            string.Empty, "FalloutNV.esm", "DeadMoney.esm", "HonestHearts.esm", "OldWorldBlues.esm", "LonesomeRoad.esm",
            "GunRunnersArsenal.esm", "TribalPack.esm", "MercenaryPack.esm", "ClassicPack.esm", "CaravanPack.esm",
            "YUP - Base Game + All DLC.esm", "Unofficial Patch NVSE Plus.esp", "TaleOfTwoWastelands.esm", "TTWInteriors_Core.esm",
            "TTWInteriorsProject_Combo.esm", "TTWInteriorsProject_CombatHotfix.esm", "TTWInteriorsProject_Merged.esm", "TTWInteriors_Core_Hotfix.esm"
        ];

        public static readonly IReadOnlyList<string> Tes4 =
        [
            string.Empty, "Oblivion.esm", "Knights.esp", "DLCVileLair.esp", "DLCThievesDen.esp", "DLCSpellTomes.esp", "DLCShiveringIsles.esp",
            "DLCOrrery.esp", "DLCMehrunesRazor.esp", "DLCHorseArmor.esp", "DLCFrostCrag.esp", "DLCBattlehornCastle.esp",
            "Unofficial Oblivion Patch.esp", "UDP Vampire Aging & Face Fix.esp", "DLCBattlehornCastle - Unofficial Patch.esp",
            "DLCFrostCrag - Unofficial Patch.esp", "DLCHorseArmor - Unofficial Patch.esp", "DLCMehrunesRazor - Unofficial Patch.esp",
            "DLCVileLair - Unofficial Patch.esp", "DLCThievesDen - Unofficial Patch.esp", "DLCSpellTomes - Unofficial Patch.esp",
            "DLCOrrery - Unofficial Patch.esp"
        ];
    }
}