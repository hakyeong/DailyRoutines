using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using Lumina.Excel.GeneratedSheets;
using PayloadType = Lumina.Text.Payloads.PayloadType;

namespace DailyRoutines.Infos;

public class PayloadText
{
    public List<string>? StartFollow { get; private set; }
    public List<string>? EndFollow { get; private set; }
    public List<string>? Countdown { get; private set; }

    public void Init()
    {
        StartFollow ??= GetLogMessageRowToStringList(52);
        EndFollow ??= GetLogMessageRowToStringList(53);
        Countdown ??= GetLogMessageRowToStringList(5255);
    }

    private static List<string> GetLogMessageRowToStringList(uint row)
    {
        return LuminaCache.GetRow<LogMessage>(row).Text.Payloads
                          .Where(x => x.PayloadType == PayloadType.Text)
                          .Select(text => text.RawString).ToList();
    }
    
}
