using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickTripleTriadCoinExchangeDR(nint addon = default) : ClickBase<ClickTripleTriadCoinExchangeDR>("TripleTriadCoinExchange", addon)
{
    public void Card(int index) => FireCallback(0, index, 0);
}
