using DailyRoutines.Infos;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoSkipDutyCutsceneTitle", "AutoSkipDutyCutsceneDescription", ModuleCategories.战斗)]
public class AutoSkipDutyCutscene : DailyModuleBase
{
    private static readonly MemoryPatch IsPlayCutscenePatch =
        new("0F B6 D3 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8B 5C 24 ?? B8 ?? ?? ?? ?? 48 83 C4 ?? 5F C3 CC CC CC CC CC CC CC CC CC CC CC CC 48 83 EC", [0x31, 0xD2, 0x90]);

    public override void Init()
    {
        if (!IsPlayCutscenePatch.IsValid) return;

        IsPlayCutscenePatch.Set(true);
    }

    public override void Uninit()
    {
        IsPlayCutscenePatch.Set(false);
    }
}
