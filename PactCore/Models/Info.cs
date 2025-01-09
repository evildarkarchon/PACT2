using PACT2.PactCore.Services;
namespace PACT2.PactCore.Models;

public class Info
{
    private static readonly Lazy<Info> LazyInstance = new(() => new Info());

    public static Info Instance => LazyInstance.Value;

    private Info()
    {
        var yamlSettings = YamlSettingsService.Instance;

        // Initialize lists from YAML settings
        XEditListUniversal = yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.XEdit_Lists.Universal") ?? new();
        Fo3SkipList = yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.Skip_Lists.FO3") ?? new();
        FnvSkipList = yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.Skip_Lists.FNV") ?? new();
        Fo4SkipList = yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.Skip_Lists.FO4") ?? new();
        SseSkipList = yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.Skip_Lists.SSE") ?? new();
        Tes4SkipList = yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.Skip_Lists.TES4") ?? new();

        VipSkipList = Fo3SkipList.Concat(FnvSkipList).Concat(Fo4SkipList).Concat(SseSkipList).Concat(Tes4SkipList).ToList();
        
        XEditListFallout3 = yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.XEdit_Lists.FO3") ?? new();
        XEditListNewVegas = yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.XEdit_Lists.FNV") ?? new();
        XEditListFallout4 = yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.XEdit_Lists.FO4") ?? new();
        XEditListFallout4Vr = yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.XEdit_Lists.FO4VR") ?? new();
        XEditListSkyrimSe = yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.XEdit_Lists.SSE") ?? new();
        XEditListSkyrimVr = yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.XEdit_Lists.SkyrimVR") ?? new();
        XEditListOblivion = yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.XEdit_Lists.TES4") ?? new();

        XEditListSpecific = XEditListFallout3
            .Concat(XEditListNewVegas)
            .Concat(XEditListFallout4)
            .Concat(XEditListFallout4Vr)
            .Concat(XEditListSkyrimSe)
            .Concat(XEditListSkyrimVr)
            .Concat(XEditListOblivion)
            .ToList();
    }

    public string? XEditExe { get; set; }
    public string? XEditPath { get; set; }
    public string? LoadOrderTxt { get; set; }
    public string? LoadOrderPath { get; set; }
    public int JournalExpiration { get; set; } = 7;
    public int CleaningTimeout { get; set; } = 300;

    public List<string> XEditListFallout3 { get; private set; }
    public List<string> XEditListNewVegas { get; private set; }
    public List<string> XEditListFallout4 { get; private set; }
    public List<string> XEditListFallout4Vr { get; private set; }
    public List<string> XEditListSkyrimSe { get; private set; }
    public List<string> XEditListOblivion { get; private set; }
    public List<string> XEditListSkyrimVr { get; private set; }
    public List<string> XEditListUniversal { get; private set; }
    public List<string> XEditListSpecific { get; private set; }

    public HashSet<string> CleanResultsUdr { get; } = new();
    public HashSet<string> CleanResultsItm { get; } = new();
    public HashSet<string> CleanResultsNvm { get; } = new();
    public HashSet<string> CleanResultsPartialForms { get; } = new();
    public HashSet<string> CleanFailedList { get; } = new();
    public int PluginsProcessed { get; set; }
    public int PluginsCleaned { get; set; }

    public List<string> LocalSkipList { get; } = new();
    public List<string> Fo3SkipList { get; set; }
    public List<string> FnvSkipList { get; set; }
    public List<string> Fo4SkipList { get; set; }
    public List<string> SseSkipList { get; set; }
    public List<string> Tes4SkipList { get; set; }
    public List<string> VipSkipList { get; set; }

    public string XEditLogTxt { get; set; } = string.Empty;
    public string XEditExcLog { get; set; } = string.Empty;
}