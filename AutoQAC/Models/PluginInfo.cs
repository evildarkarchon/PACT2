// Models/PluginInfo.cs

using System.Collections.Generic;

namespace AutoQAC.Models;

public class PluginInfo
{
    public HashSet<string> CleanResultsUdr { get; } = new();
    public HashSet<string> CleanResultsItm { get; } = new();
    public HashSet<string> CleanResultsNvm { get; } = new();
    public HashSet<string> CleanResultsPartialForms { get; } = new();
    public HashSet<string> CleanFailedList { get; } = new();
    public int PluginsProcessed { get; set; }
    public int PluginsCleaned { get; set; }
    public List<string> LocalSkipList { get; } = new();
}