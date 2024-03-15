using DailyRoutines.Windows;
using Dalamud.Hooking;
using ECommons.Automation;
using System.Linq;
using System.Reflection;

namespace DailyRoutines.Modules;

public abstract class DailyModuleBase
{
    public bool Initialized { get; internal set; }
    protected TaskManager? TaskManager { get; set; }
    protected Overlay? Overlay { get; set; }

    public virtual void Init() { }

    public virtual void ConfigUI() { }

    public virtual void OverlayUI() { }

    public virtual void Uninit()
    {
        if (Overlay != null && P.WindowSystem.Windows.Contains(Overlay)) P.WindowSystem.RemoveWindow(Overlay);
        Overlay = null;

        TaskManager?.Abort();
        TaskManager = null;

        foreach (var field in GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Hook<>))
            {
                var hookInstance = field.GetValue(this);
                hookInstance?.GetType().GetMethod("Dispose")?.Invoke(hookInstance, null);
            }
        }
    }
}
