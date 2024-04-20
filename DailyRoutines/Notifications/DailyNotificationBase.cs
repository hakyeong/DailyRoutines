namespace DailyRoutines.Notifications;

public abstract class DailyNotificationBase
{
    public bool Initialized { get; internal set; }

    public virtual void Init() { }

    public virtual void Uninit() { }
}
