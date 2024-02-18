using DailyRoutines.Managers;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Infos;

public class ExcelGameData
{
    public Dictionary<uint, Action>? PlayerActions { get; private set; }
    public Dictionary<uint, ContentFinderCondition>? Contents { get; private set; }
    public Dictionary<uint, ENpcResident>? ENpcResidents { get; private set; }
    public Dictionary<uint, string>? ENpcTitles { get; private set; }
    public HashSet<uint>? ContentTerritories { get; private set; }

    public ExcelGameData()
    {
        PlayerActions ??= Service.Data.GetExcelSheet<Action>()
                         ?.Where(x => x.ClassJobCategory.Row > 0 && x.ActionCategory.Row <= 4 && x.RowId > 8)
                         .ToDictionary(x => x.RowId, x => x);

        Contents ??= Service.Data.GetExcelSheet<ContentFinderCondition>()
                          .Where(x => !x.Name.ToString().IsNullOrEmpty())
                          .DistinctBy(x => x.TerritoryType.Row)
                          .ToDictionary(x => x.TerritoryType.Row, x => x);

        ContentTerritories ??= [..Contents.Keys];

        ENpcResidents ??= Service.Data.GetExcelSheet<ENpcResident>()
                          .Where(x => x.Unknown10)
                          .ToDictionary(x => x.RowId, x => x);

        ENpcTitles ??= Service.Data.GetExcelSheet<ENpcResident>()
                            .Where(x => x.Unknown10)
                            .ToDictionary(x => x.RowId, x => x.Title.ExtractText());
    }
}
