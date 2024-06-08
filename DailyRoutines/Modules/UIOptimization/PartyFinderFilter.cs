using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Interface;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("PartyFinderFilterTitle", "PartyFinderFilterDescription", ModuleCategories.界面优化)]
public class PartyFinderFilter : DailyModuleBase
{
    private int _batchIndex;

    private readonly HashSet<string> _descriptionSet = [];

    private List<string> _blackList = [];

    private bool _isWhiteList;

    private string s = string.Empty;

    public override void Init()
    {
        AddConfig("BlackList", _blackList);
        AddConfig("Reverse", _isWhiteList);
        _blackList = GetConfig<List<string>>("BlackList");
        _isWhiteList = GetConfig<bool>("Reverse");
        Service.PartyFinder.ReceiveListing += OnReceiveListing;
    }

    public override void Uninit()
    {
        Service.PartyFinder.ReceiveListing -= OnReceiveListing;
        base.Uninit();
    }

    public override void ConfigUI()
    {
        var index = 0;
        if (ImGuiOm.ButtonIcon("##add", FontAwesomeIcon.Plus))
            _blackList.Add(string.Empty);

        ImGui.SameLine();
        if (ImGui.Checkbox(Service.Lang.GetText("PartyFinderFilter-WhileList"), ref _isWhiteList))
            UpdateConfig("Reverse", _isWhiteList);

        foreach (var str in _blackList.ToList())
        {
            s = str;
            ImGui.InputText($"##{index}", ref s, 500);
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                try
                {
                    _ = new Regex(s);
                    _blackList[index] = s;
                    UpdateConfig("BlackList", _blackList);
                }
                catch (ArgumentException)
                {
                    NotifyHelper.NotificationWarning(Service.Lang.GetText("PartyFinderFilter-RegexError"));
                    _blackList = GetConfig<List<string>>("BlackList");
                }
            }

            ImGui.SameLine();
            if (ImGuiOm.ButtonIcon($"##delete{index}", FontAwesomeIcon.Trash))
                _blackList.RemoveAt(index);

            index++;
        }
    }

    private void OnReceiveListing(PartyFinderListing listing, PartyFinderListingEventArgs args)
    {
        if (_batchIndex != args.BatchNumber)
        {
            _batchIndex = args.BatchNumber;
            _descriptionSet.Clear();
        }

        args.Visible = args.Visible && Verify(listing);
    }

    private bool Verify(PartyFinderListing listing)
    {
        var description = listing.Description.ToString();
        if (_descriptionSet.Contains(description))
            return false;

        _descriptionSet.Add(description);

        var name = listing.Name.ToString();
        var result = true;
        try
        {
            result = !(_blackList.Any(str => Regex.IsMatch(name, str) || Regex.IsMatch(description, str)) ^ _isWhiteList);
        }
        catch (Exception e)
        {
            Service.Log.Error(GetType().Name + "cause error when filting\n" + e);
        }

        return result;
    }
}
