using ClickLib.Bases;

namespace DailyRoutines.Infos.Clicks;

public class ClickGrandCompanySupplyList(nint addon = default) : ClickBase<ClickGrandCompanySupplyList>("GrandCompanySupplyList", addon)
{
    public void ItemEntry(int index) => FireCallback(1, index, 0);

    // 筹备军需品
    public void Supply() => SwitchCategory(0);

    // 筹备补给品
    public void Provisioning() => SwitchCategory(1);

    // 筹备稀有品
    public void ExpertDelivery() => SwitchCategory(2);

    public void SwitchCategory(int index) => FireCallback(0, index, 0);

    public static ClickGrandCompanySupplyList Using(nint addon) => new(addon);
}
