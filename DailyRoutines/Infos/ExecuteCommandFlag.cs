namespace DailyRoutines.Infos;

public enum ExecuteCommandFlag
{
    /// <summary>
    /// 拔出/收回武器
    /// <para><c>param1</c>: 1 - 拔出, 0 - 收回</para>
    /// </summary>
    DrawOrSheatheWeapon = 1,

    /// <summary>
    /// 自动攻击
    /// <para><c>param1</c>: 是否开启自动攻击 (0 - 否, 1 - 是)</para>
    /// <para><c>param2</c>: 目标对象ID</para>
    /// <para><c>param3</c>: 是否为手动操作 (0 - 否, 1 - 是)</para>
    /// </summary>
    AutoAttack = 2,

    /// <summary>
    /// 选中目标
    /// <para><c>param1</c>: 目标 Object ID (无目标为: -536870912, 即 int.MinValue / 4)</para>
    /// </summary>
    Target = 3,

    /// <summary>
    /// 下坐骑
    /// <para><c>param1</c>: 未知, 固定为 1</para>
    /// </summary>
    Dismount = 101,

    /// <summary>
    /// 传送至指定的以太之光
    /// <para><c>param1</c>: 以太之光 ID</para>
    /// <para><c>param2</c>: 是否使用传送券 (0 - 否, 1 - 是)</para>
    /// <para><c>param3</c>: 以太之光 Sub Index</para>
    /// </summary>
    Teleport = 202,

    /// <summary>
    /// 若当前种族不是拉拉菲尔族, 则返回至当前地图的最近安全点
    /// </summary>
    ReturnIfNotLalafell = 213,

    /// <summary>
    /// 立即返回至返回点, 若在副本内则返回至副本内重生点
    /// </summary>
    InstantReturn = 214,

    /// <summary>
    /// 将青魔法师技能应用至有效技能的第X格
    /// <para><c>param2</c>: 格子, 从 0 开始, 小于 24</para>
    /// <para><c>param3</c>: 技能 ID</para>
    /// </summary>
    AssignBLUActionToSlot = 315,

    /// <summary>
    /// 清除来自木人的仇恨
    /// <para><c>param1</c>: 木人的 Object ID</para>
    /// </summary>
    ResetStrkingDummy = 319,
}
