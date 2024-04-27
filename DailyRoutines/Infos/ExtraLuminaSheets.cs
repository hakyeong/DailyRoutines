using Lumina.Excel;
using Lumina;
using Lumina.Data;
using Lumina.Text;

namespace DailyRoutines.Infos;

[Sheet("leve/CraftLeveClient")]
public class CraftLeveClient : ExcelRow
{
    public SeString? Name { get; set; }
    public SeString? Text { get; set; }

    public override void PopulateData(RowParser parser, GameData gameData, Language language)
    {
        base.PopulateData(parser, gameData, language);

        Name = parser.ReadColumn<SeString>(0);
        Text = parser.ReadColumn<SeString>(1);
    }
}

[Sheet("custom/004/HouFixCompanySubmarine_00447")]
public class CompanySubmarine : ExcelRow
{
    public SeString? Name { get; set; }
    public SeString? Text { get; set; }

    public override void PopulateData(RowParser parser, GameData gameData, Language language)
    {
        base.PopulateData(parser, gameData, language);

        Name = parser.ReadColumn<SeString>(0);
        Text = parser.ReadColumn<SeString>(1);
    }
}
