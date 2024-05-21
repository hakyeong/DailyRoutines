namespace DailyRoutines.Infos;

public enum ExecuteCommandFlag
{
    /// <summary>
    /// 选中目标
    /// <para>param1: 目标 Object ID (无目标为: -536870912, 即 int.MinValue / 4)</para>
    /// </summary>
    Target = 3,
    /// <summary>
    /// 传送至指定的以太之光
    /// <para>param1: 以太之光 Index</para>
    /// <para>param2: 以太之光 Sub Index</para>
    /// </summary>
    Teleport = 202,
    /// <summary>
    /// 立即返回至返回点, 若在副本内则返回至副本内重生点
    /// </summary>
    InstantReturn = 214
}
