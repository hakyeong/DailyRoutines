namespace DailyRoutines.Managers;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public string SelectedLanguage { get; set; } = string.Empty;
    public Dictionary<string, bool> ModuleEnabled { get; set; } = new()
    {
        { "AutoMiniCactpot", false },
        { "AutoPunchingMachine", false }
    };


    [NonSerialized]
    private DalamudPluginInterface? pluginInterface;


    public void Initialize(DalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        pluginInterface!.SavePluginConfig(this);
    }

    public void Uninitialize()
    {

    }
}
