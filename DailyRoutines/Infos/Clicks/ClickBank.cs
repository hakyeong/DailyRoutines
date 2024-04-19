using ClickLib.Bases;

namespace DailyRoutines.Infos.Clicks;

public class ClickBank(nint addon = default) : ClickBase<ClickBank>("Bank", addon)
{
    /// <summary>
    /// 默认为取出
    /// </summary>
    public void Switch() => FireCallback(2, 0);

    public void DepositInput(uint amount) => FireCallback(3, amount);

    public void Confirm() => FireCallback(0, 0);

    public void Cancel() => FireCallback(1, 0);

    public static ClickBank Using(nint addon) => new(addon);
}
