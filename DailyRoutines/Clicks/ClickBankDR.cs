using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickBankDR(nint addon = default) : ClickBase<ClickBankDR>("Bank", addon)
{
    /// <summary>
    /// 默认为取出, 切换一次为保管
    /// </summary>
    public void Switch()
    {
        FireCallback(2, 0);
    }

    public void DepositInput(uint amount)
    {
        FireCallback(3, amount);
    }

    public void Confirm()
    {
        FireCallback(0, 0);
    }

    public void Cancel()
    {
        FireCallback(1, 0);
    }
}
