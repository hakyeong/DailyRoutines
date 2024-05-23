namespace DailyRoutines.Infos;

public enum ExecuteCommandFlag
{
    /// <summary>
    /// 拔出/收回武器
    /// <para><c>param1</c>: 1 - 拔出, 0 - 收回</para>
    /// <para><c>param2</c>: 未知, 固定为 1</para>
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
    /// 中断咏唱
    /// </summary>
    CancelCast = 105,

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
    /// 更改佩戴的称号
    /// <para><c>param1</c>: 称号 ID</para>
    /// </summary>
    ChangeTitle = 302,

    /// <summary>
    /// 清除场地标点
    /// </summary>
    ClearFieldMarkers = 313,

    /// <summary>
    /// 放置场地标点
    /// <para><c>param1</c>: 标点索引</para>
    /// <para><c>param2</c>: 坐标 X * 1000</para>
    /// <para><c>param3</c>: 坐标 Y * 1000</para>
    /// <para><c>param4</c>: 坐标 Z * 1000</para>
    /// </summary>
    PlaceFieldMarker = 317,

    /// <summary>
    /// 移除场地标点
    /// <para><c>param1</c>: 标点索引</para>
    /// </summary>
    RemoveFieldMarker = 318,

    /// <summary>
    /// 将青魔法师技能交换或应用于有效技能
    /// <para><c>param1</c>: 类型 (0 为应用有效技能, 1 为交换有效技能)</para>
    /// <para><c>param2</c>: 格子序号 (从 0 开始, 小于 24)</para>
    /// <para><c>param3</c>: 技能 ID / 格子序号 (从 0 开始, 小于 24)</para>
    /// </summary>
    AssignBLUActionToSlot = 315,

    /// <summary>
    /// 清除来自木人的仇恨
    /// <para><c>param1</c>: 木人的 Object ID</para>
    /// </summary>
    ResetStrkingDummy = 319,

    /// <summary>
    /// 请求收藏柜的数据
    /// </summary>
    RequestCabinet = 424,

    /// <summary>
    /// 存入物品至收藏柜
    /// <para><c>param1</c>: 物品在 Cabinet.csv 中的对应索引</para>
    /// </summary>
    StoreToCabinet = 425,

    /// <summary>
    /// 从收藏柜中取回物品
    /// <para><c>param1</c>: 物品在 Cabinet.csv 中的对应索引</para>
    /// </summary>
    RestoreFromCabinet = 426,

    /// <summary>
    /// 请求陆行鸟鞍囊的数据
    /// </summary>
    RequestSaddle = 444,

    /// <summary>
    /// 为 临危受命 等级同步
    /// <para><c>param1</c>: FATE ID</para>
    /// <para><c>param2</c>: 是否等级同步 (0 - 否, 1 - 是)</para>
    /// </summary>
    FateLevelSync = 814,

    /// <summary>
    /// 放弃任务投票
    /// </summary>
    VoteAbandon = 808,

    /// <summary>
    /// 离开副本
    /// <para><c>param1</c>: 类型 (0 - 正常退本, 1 - 一段时间未操作)</para>
    /// </summary>
    LeaveDuty = 819,

    /// <summary>
    /// 领取战利水晶
    /// </summary>
    CollectTrophyCrystal = 1200,

    /// <summary>
    /// 请求金碟游乐场面板 整体 信息
    /// </summary>
    RequestGSGeneral = 1850,

    /// <summary>
    /// 请求金碟游乐场面板 萌宠之王 信息
    /// </summary>
    RequestGSLordofVerminion = 2010,

    /// <summary>
    /// 启用/解除自动加入新人频道设置
    /// </summary>
    EnableAutoJoinNoviceNetwork = 2102,

    /// <summary>
    /// 请求投影台数据
    /// </summary>
    RequestPrismBox = 2350,

    /// <summary>
    /// 取出投影台物品
    /// <para><c>param1</c>: 投影台内部物品 ID (MirageManager.Instance().PrismBoxItemIds)</para>
    /// </summary>
    RestorePrsimBoxItem = 2352,

    /// <summary>
    /// 请求投影模板数据
    /// </summary>
    RequestGlamourPlates = 2355,

    /// <summary>
    /// 进入/退出投影模板选择状态
    /// <para><c>param1</c>: 0 - 退出, 1 - 进入</para>
    /// <para><c>param2</c>: 未知, 可能为 0 或 1</para>
    /// </summary>
    EnterGlamourPlateState = 2356,

    /// <summary>
    /// 应用投影模板 (需要先进入投影模板选择状态)
    /// <para><c>param1</c>: 投影模板索引</para>
    /// </summary>
    ApplyGlamourPlate = 2357,

    /// <summary>
    /// 请求金碟游乐场面板 多玛方城战 信息
    /// </summary>
    RequestGSMahjong = 2550,

    /// <summary>
    /// 收取无人岛屯货仓库探索结果
    /// <para><c>param1</c>: 仓库索引</para>
    /// </summary>
    MJIGranaryCollect = 3262,

    /// <summary>
    /// 托管单块无人岛耕地
    /// <para><c>param1</c>: 耕地索引</para>
    /// <para><c>param2</c>: 种子物品 ID</para>
    /// </summary>
    MJIFarmEntrustSingle = 3279,

    /// <summary>
    /// 取消托管单块无人岛耕地
    /// <para><c>param1</c>: 耕地索引</para>
    /// </summary>
    MJIFarmDismiss = 3280,

    /// <summary>
    /// 收取单块无人岛耕地
    /// <para><c>param1</c>: 耕地索引</para>
    /// <para><c>param2</c>: 收取后是否取消托管 (0 - 否, 1 - 是)</para>
    /// </summary>
    MJIFarmCollectSingle = 3281,

    /// <summary>
    /// 收取全部无人岛耕地
    /// <para><c>param1</c>: *(int*)MJIManager.Instance()->GranariesState</para>
    /// </summary>
    MJIFarmCollectAll = 3282,

    /// <summary>
    /// 请求无人岛工房需求数据
    /// </summary>
    MJIFavorStateRequest = 3292,

    /// <summary>
    /// 掷骰子
    /// <para><c>param1</c>: 类型 (固定为 0)</para>
    /// <para><c>param2</c>: 最大值</para>
    /// </summary>
    RollDice = 9000,
}
