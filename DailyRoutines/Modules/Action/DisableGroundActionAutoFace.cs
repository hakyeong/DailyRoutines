using DailyRoutines.Infos;

namespace DailyRoutines.Modules;

[ModuleDescription("DisableGroundActionAutoFaceTitle", "DisableGroundActionAutoFaceDescription", ModuleCategories.技能)]
public class DisableGroundActionAutoFace : DailyModuleBase
{
    private static readonly MemoryPatch GroundActionAutoFacePatch =
        new("41 80 7F ?? ?? 74 ?? 49 8D 8E", [0x90, 0x90, 0x90, 0x90, 0x90, 0xEB, 0x28]);

    public override void Init()
    {
        GroundActionAutoFacePatch.Set(true);
    }
}
