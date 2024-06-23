using DailyRoutines.Managers;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using DailyRoutines.Helpers;

namespace DailyRoutines.Notifications;

public enum NotifyType
{
    Text,
    TextToTalk,
    Push
}

public abstract class DailyNotificationBase
{
    protected static string CacheDirectory { get; } = Path.Combine(Service.PluginInterface.GetPluginConfigDirectory(), "Cache");

    public bool Initialized { get; internal set; }
    public virtual NotifyType NotifyType { get; internal set; } = NotifyType.Text;

    public virtual void Init() { }

    public virtual void Uninit() { }
}
