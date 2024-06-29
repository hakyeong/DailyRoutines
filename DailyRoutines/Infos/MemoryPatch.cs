using DailyRoutines.Managers;
using Dalamud;

namespace DailyRoutines.Infos;

public class MemoryPatch
{
    private        nint    Ptr           { get; set; }
    private static byte[]? OrigBytes     { get; set; }
    private static byte[]? OverrideBytes { get; set; }

    public bool IsValid => Ptr != nint.Zero && OverrideBytes != null;

    public MemoryPatch(string signature, byte[] patch)
    {
        if (Service.SigScanner.TryScanText(signature, out var ptr))
        {
            Ptr = ptr;
            OverrideBytes = patch;
        }
    }

    public void Set(bool isEnabled)
    {
        if (!IsValid) return;

        if (isEnabled)
        {
            if (SafeMemory.ReadBytes(Ptr, OverrideBytes.Length, out var origBytes))
                OrigBytes ??= origBytes;

            SafeMemory.WriteBytes(Ptr, OverrideBytes);
        }
        else
        {
            if (OrigBytes == null) return;

            SafeMemory.WriteBytes(Ptr, OrigBytes);
            OrigBytes = null;
        }
    }
}
