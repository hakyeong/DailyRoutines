using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace DailyRoutines.Managers;

public class LinkPayloadManager : IDailyManager
{
    private static readonly Dictionary<uint, DalamudLinkPayload> DistributedPayloads = [];

    private void Init() { DistributedPayloads.Clear(); }

    public DalamudLinkPayload Register(Action<uint, SeString> commandAction, out uint id)
    {
        id = GetUniqueID();

        var payload = Service.PluginInterface.AddChatLinkHandler(id, commandAction);
        DistributedPayloads.Add(id, payload);
        return payload;
    }

    public bool TryGetPayload(uint id, out DalamudLinkPayload? payload)
        => DistributedPayloads.TryGetValue(id, out payload);

    public bool Unregister(uint id)
    {
        if (!DistributedPayloads.ContainsKey(id)) return false;

        Service.PluginInterface.RemoveChatLinkHandler(id);
        DistributedPayloads.Remove(id);
        return true;
    }

    private static uint GetUniqueID()
    {
        var counter = 0U;
        while (DistributedPayloads.ContainsKey(counter))
            counter++;

        return counter;
    }

    private void Uninit()
    {
        Service.PluginInterface.RemoveChatLinkHandler();
        DistributedPayloads.Clear();
    }
}
