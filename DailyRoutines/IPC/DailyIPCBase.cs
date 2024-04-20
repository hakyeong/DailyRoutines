namespace DailyRoutines.IPC;

public abstract class DailyIPCBase
{
    public bool Initialized { get; internal set; }
    public virtual string? InternalName { get; set; }

    public virtual void Init() { }

    public virtual void Uninit() { }
}
