using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Gui.PartyFinder.Types;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoHideLockedDutyTitle", "AutoHideLockedDutyDescription", ModuleCategories.General)]
public class AutoHideLockedDuty : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithUI => false;

    public void Init()
    {
        Service.PartyFinder.ReceiveListing += OnListReceived;
    }

    public void UI() { }

    private void OnListReceived(PartyFinderListing listing, PartyFinderListingEventArgs args)
    {
        if (listing.Category is DutyCategory.Duty or (DutyCategory)128 && listing.Duty.Value.RowId != 0)
            args.Visible = UIState.IsInstanceContentUnlocked(listing.Duty.Value.Content);
    }

    public void Uninit()
    {
        Service.PartyFinder.ReceiveListing -= OnListReceived;
    }
}
