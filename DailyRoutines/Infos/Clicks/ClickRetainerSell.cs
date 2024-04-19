using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Infos.Clicks;

public class ClickRetainerSell(nint addon = default) : ClickBase<ClickRetainerSell, AddonRetainerSell>("RetainerSell", addon)
{
    public void ComparePrice() => FireCallback(4);

    public void Confirm() => FireCallback(0);

    public void Decline() => FireCallback(1);

    public static ClickRetainerSell Using(nint addon) => new(addon);
}
