using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Managers;
using Lumina.Excel.GeneratedSheets;
using PayloadType = Lumina.Text.Payloads.PayloadType;

namespace DailyRoutines.Infos;

public class PayloadText
{
    public List<string>? StartFollow { get; private set; }
    public List<string>? EndFollow { get; private set; }
    public List<string>? Countdown { get; private set; }

    public PayloadText()
    {
        StartFollow ??= getLogMessageRowToStringList(52);
        EndFollow ??= getLogMessageRowToStringList(53);
        Countdown ??= getLogMessageRowToStringList(5255);
    }

    private static List<string>? getLogMessageRowToStringList(uint row)
    {
        return LuminaCache.GetRow<LogMessage>(row).Text.Payloads
                          .Where(x => x.PayloadType == PayloadType.Text)
                          .Select(text => text.RawString).ToList();
    }
    
}
