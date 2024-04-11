using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AddSearchFFXIVscTitle", "AddSearchFFXIVscDescription", ModuleCategories.InterfaceExpand)]
public class AddSearchFFXIVsc : DailyModuleBase
{
    public override string? Author { get; set; } = "HSS";

    private static Item? _lastItem;
    private const int ChatLogContextItemId = 0x948;
    private const string Url = "https://www.ffxivsc.cn/#/search?text={0}&type=armor";

    private static readonly MenuItem _inventoryItem = new MenuItem
    {
        IsEnabled = true,
        IsReturn = false,
        PrefixChar = 'D',
        Name = Service.Lang.GetText("AddSearchFFXIVsc-contextMenuText"),
        OnClicked = OnClick,
        IsSubmenu = false,
        PrefixColor = 541,
    };

    public override void Init()
    {
        
        Service.ContextMenu.OnMenuOpened += OnMenuOpened;
    }

    private static unsafe void OnMenuOpened(MenuOpenedArgs args)
    {
        if (args.MenuType is ContextMenuType.Inventory)
        {
            var arg = (MenuTargetInventory)args.Target;
            if (arg.TargetItem.HasValue && HandleItem(arg.TargetItem.Value.ItemId))
            {
                args.AddMenuItem(_inventoryItem);
            }
        }
        else
        {
            switch (args.AddonName)
            {
                case "ItemSearch" when args.AgentPtr != nint.Zero:
                {
                    if (HandleItem((uint)AgentContext.Instance()->UpdateCheckerParam))
                        args.AddMenuItem(_inventoryItem);

                    break;
                }
                case "ChatLog":
                {
                    var agent = Service.Gui.FindAgentInterface("ChatLog");
                    if (agent == nint.Zero || !ValidateChatLogContext(agent))
                        return;

                    if (HandleItem(*(uint*)(agent + ChatLogContextItemId)))
                    {
                        args.AddMenuItem(_inventoryItem);
                    }

                    break;
                }
            }
        }
    }

    private static void OnClick(MenuItemClickedArgs _)
    {
        if (_lastItem != null)
        {
            Util.OpenLink(string.Format(Url, _lastItem.Name));
        }
    }

    private static bool HandleItem(uint id)
    {
        return Service.PresetData.EquipmentItems.TryGetValue(id, out _lastItem);
    }

    private static unsafe bool ValidateChatLogContext(nint agent)
        => *(uint*)(agent + ChatLogContextItemId + 8) == 3;

    public override void Uninit()
    {
        Service.ContextMenu.OnMenuOpened -= OnMenuOpened;
        base.Uninit();
    }
}
