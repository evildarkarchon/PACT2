using PACT2.PactCore.Services;
namespace PACT2.PactCore.Models;

public class Info
{
    private static readonly Lazy<Info> LazyInstance = new(() => new Info());
    private readonly YamlSettingsService _yamlSettings;

    public static Info Instance => LazyInstance.Value;

    private Info()
    {
        _yamlSettings = YamlSettingsService.Instance;
        
        // Initialize lists from YAML settings
        XEditListUniversal = _yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.XEdit_Lists.Universal") ?? new();
        Fo3SkipList = _yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.Skip_Lists.FO3") ?? new();
        FnvSkipList = _yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.Skip_Lists.FNV") ?? new();
        Fo4SkipList = _yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.Skip_Lists.FO4") ?? new();
        SseSkipList = _yamlSettings.GetSetting<List<string>>("PACT Data/PACT Main.yaml", "PACT_Data.Skip_Lists.SSE") ?? new();

        VipSkipList = Fo3SkipList.Concat(FnvSkipList).Concat(Fo4SkipList).Concat(SseSkipList).ToList();

        XEditListSpecific = XEditListFallout3
            .Concat(XEditListNewVegas)
            .Concat(XEditListFallout4)
            .Concat(XEditListSkyrimSe)
            .ToList();

        // Initialize HashSets
        LowerSpecific = new HashSet<string>(XEditListSpecific.Select(x => x.ToLower()));
        LowerUniversal = new HashSet<string>(XEditListUniversal.Select(x => x.ToLower()));
    }

    public string? XEditExe { get; set; }
    public string? XEditPath { get; set; }
    public string? LoadOrderTxt { get; set; }
    public string? LoadOrderPath { get; set; }
    public int JournalExpiration { get; set; } = 7;
    public int CleaningTimeout { get; set; } = 300;

    public List<string> XEditListFallout3 { get; private set; } = new();
    public HashSet<string> LowerFo3 { get; } = new();
    public List<string> XEditListNewVegas { get; private set; } = new();
    public HashSet<string> LowerFnv { get; } = new();
    public List<string> XEditListFallout4 { get; private set; } = new();
    public HashSet<string> LowerFo4 { get; } = new();
    public List<string> XEditListSkyrimSe { get; private set; } = new();
    public List<string> SkyrimVrList { get; private set; } = new();
    public HashSet<string> LowerSse { get; } = new();
    public List<string> XEditListUniversal { get; private set; } = new();
    public List<string> XEditListSpecific { get; private set; } = new();
    public HashSet<string> LowerSpecific { get; } = new();
    public HashSet<string> LowerUniversal { get; } = new();

    public HashSet<string> CleanResultsUdr { get; } = new();
    public HashSet<string> CleanResultsItm { get; } = new();
    public HashSet<string> CleanResultsNvm { get; } = new();
    public HashSet<string> CleanResultsPartialForms { get; } = new();
    public HashSet<string> CleanFailedList { get; } = new();
    public int PluginsProcessed { get; set; }
    public int PluginsCleaned { get; set; }

    public List<string> LocalSkipList { get; } = new();
    public List<string> Fo3SkipList { get; private set; } = new();
    public List<string> FnvSkipList { get; private set; } = new();
    public List<string> Fo4SkipList { get; private set; } = new();
    public List<string> SseSkipList { get; private set; } = new();
    public List<string> VipSkipList { get; private set; } = new();

    public string XEditLogTxt { get; set; } = string.Empty;
    public string XEditExcLog { get; set; } = string.Empty;
}