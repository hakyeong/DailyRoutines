using DailyRoutines.Managers;
using Dalamud.Plugin;

namespace DailyRoutines.IPC;

public abstract class DailyIPCBase
{
    public bool Initialized { get; internal set; }
    public virtual string? InternalName { get; set; }
    protected static DalamudPluginInterface PI => Service.PluginInterface;

    public virtual void Init() { }

    public virtual void Uninit() { }
}
