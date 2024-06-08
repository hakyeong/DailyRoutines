using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DailyRoutines.Infos;

public class FileFormat
{
    public class GitHubRelease
    {
        public int               id           { get; set; }
        public string            tag_name     { get; set; } = null!;
        public string            name         { get; set; } = null!;
        public string            body         { get; set; } = null!;
        public DateTime          published_at { get; set; }
        public List<GitHubAsset> assets       { get; set; } = null!;
    }

    public class GitHubAsset
    {
        public string name           { get; set; } = null!;
        public int    download_count { get; set; }
    }

    // 石之家玩家搜索结果
    public class RSPlayerSearchResult
    {
        public int                            code { get; set; }
        public string                         msg  { get; set; } = null!;
        public List<RSPlayerSearchResultData> data { get; set; } = null!;
    }

    public class RSPlayerSearchResultData
    {
        public uint   uuid   { get; set; }
        public string avatar { get; set; } = null!;

        public string character_name { get; set; } = null!;

        // 大区名
        public string area_name { get; set; } = null!;

        // 服务器名
        public string group_name           { get; set; } = null!;
        public string profile              { get; set; } = null!;
        public uint   test_limited_badge   { get; set; }
        public uint   posts2_creator_badge { get; set; }
        public int    admin_tag            { get; set; }
        public uint   fansNum              { get; set; }
    }

    // 石之家玩家主页成就搜索结果
    public class RSPlayerAchievementSearch
    {
        public int                                 code { get; set; }
        public string                              msg  { get; set; } = null!;
        public List<RSPlayerAchievementSearchData> data { get; set; } = null!;
    }

    public class RSPlayerAchievementSearchData
    {
        public string event_type { get; set; } = string.Empty;

        // 成就名
        public string detail { get; set; } = string.Empty;

        public uint event_type_id { get; set; }

        // 获取时间
        public DateTime log_time  { get; set; }
        public DateTime part_date { get; set; }
    }

    public class RSActivityCalendar
    {
        public int                          code { get; set; }
        public string                       msg  { get; set; } = null!;
        public List<RSActivityCalendarData> data { get; set; } = null!;
    }

    public class RSActivityCalendarData
    {
        public uint    id         { get; set; }
        public string  name       { get; set; } = null!;
        public string  url        { get; set; } = null!;
        public int     begin_time { get; set; }
        public int     end_time   { get; set; }
        public uint    weight     { get; set; }
        public string  color      { get; set; } = null!;
        public int     type       { get; set; }
        public int     daoyu_sw   { get; set; }
        public string? banner_url { get; set; }
    }

    public class RSGameNews
    {
        public int                  Code    { get; set; }
        public string               Message { get; set; } = null!;
        public List<RSGameNewsData> Data    { get; set; } = null!;
    }

    public class RSGameNewsData
    {
        public uint   Id            { get; set; }
        public string Title         { get; set; } = null!;
        public string Author        { get; set; } = null!;
        public string HomeImagePath { get; set; } = null!;
        public string PublishDate   { get; set; } = null!;
        public string Summary       { get; set; } = null!;
        public int    SortIndex     { get; set; }
    }

    public class RSPlayerHomeInfo
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("msg")]
        public string? Msg { get; set; }

        [JsonProperty("data")]
        public RSPlayerHomeInfoData? Data { get; set; }
    }

    public class RSPlayerHomeInfoData
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("uuid")]
        public string? Uuid { get; set; }

        [JsonProperty("character_name")]
        public string? CharacterName { get; set; }

        [JsonProperty("area_id")]
        public int AreaId { get; set; }

        [JsonProperty("area_name")]
        public string? AreaName { get; set; }

        [JsonProperty("group_id")]
        public int GroupId { get; set; }

        [JsonProperty("group_name")]
        public string? GroupName { get; set; }

        [JsonProperty("avatar")]
        public string? Avatar { get; set; }

        [JsonProperty("profile")]
        public string? Profile { get; set; }

        [JsonProperty("weekday_time")]
        public string? WeekdayTime { get; set; }

        [JsonProperty("weekend_time")]
        public string? WeekendTime { get; set; }

        [JsonProperty("qq")]
        public string Qq { get; set; } = null!;

        [JsonProperty("career_publish")]
        public int CareerPublish { get; set; }

        [JsonProperty("guild_publish")]
        public int GuildPublish { get; set; }

        [JsonProperty("create_time_publish")]
        public int CreateTimePublish { get; set; }

        [JsonProperty("last_login_time_publish")]
        public int LastLoginTimePublish { get; set; }

        [JsonProperty("play_time_publish")]
        public int PlayTimePublish { get; set; }

        [JsonProperty("house_info_publish")]
        public int HouseInfoPublish { get; set; }

        [JsonProperty("washing_num_publish")]
        public int WashingNumPublish { get; set; }

        [JsonProperty("achieve_publish")]
        public int AchievePublish { get; set; }

        [JsonProperty("resently_publish")]
        public int ResentlyPublish { get; set; }

        [JsonProperty("experience")]
        public string? Experience { get; set; }

        [JsonProperty("theme_id")]
        public string? ThemeId { get; set; }

        [JsonProperty("test_limited_badge")]
        public int TestLimitedBadge { get; set; }

        [JsonProperty("posts2_creator_badge")]
        public int Posts2CreatorBadge { get; set; }

        [JsonProperty("admin_tag")]
        public int AdminTag { get; set; }

        [JsonProperty("publish_tab")]
        public string? PublishTab { get; set; }

        [JsonProperty("achieve_tab")]
        public string? AchieveTab { get; set; }

        [JsonProperty("treasure_times_publish")]
        public int TreasureTimesPublish { get; set; }

        [JsonProperty("kill_times_publish")]
        public int KillTimesPublish { get; set; }

        [JsonProperty("newrank_publish")]
        public int NewrankPublish { get; set; }

        [JsonProperty("crystal_rank_publish")]
        public int CrystalRankPublish { get; set; }

        [JsonProperty("fish_times_publish")]
        public int FishTimesPublish { get; set; }

        [JsonProperty("collapse_badge")]
        public int CollapseBadge { get; set; }

        [JsonProperty("achieveInfo")]
        public List<RSPlayerHomeInfoAchievement> AchieveInfo { get; set; } = [];

        [JsonProperty("careerLevel")]
        public List<RSPlayerHomeInfoCareer> CareerLevel { get; set; } = [];

        [JsonProperty("characterDetail")]
        public List<RSPlayerHomeInfoCharacter> CharacterDetail { get; set; } = [];

        [JsonProperty("followFansiNum")]
        public RSPlayerHomeInfoFollow? FollowFansiNum { get; set; }

        [JsonProperty("interactNum")]
        public int InteractNum { get; set; }

        [JsonProperty("beLikedNum")]
        public string? BeLikedNum { get; set; }

        [JsonProperty("relation")]
        public int Relation { get; set; }
    }

    public class RSPlayerHomeInfoAchievement
    {
        [JsonProperty("medal_id")]
        public string? MedalId { get; set; }

        [JsonProperty("medal_type")]
        public string? MedalType { get; set; }

        [JsonProperty("achieve_id")]
        public string? AchieveId { get; set; }

        [JsonProperty("achieve_time")]
        public string? AchieveTime { get; set; }

        [JsonProperty("group_id")]
        public string? GroupId { get; set; }

        [JsonProperty("character_name")]
        public string? CharacterName { get; set; }

        [JsonProperty("medal_type_id")]
        public string? MedalTypeId { get; set; }

        [JsonProperty("achieve_name")]
        public string? AchieveName { get; set; }

        [JsonProperty("area_id")]
        public string? AreaId { get; set; }

        [JsonProperty("achieve_detail")]
        public string? AchieveDetail { get; set; }

        [JsonProperty("part_date")]
        public string? PartDate { get; set; }
    }

    public class RSPlayerHomeInfoCareer
    {
        [JsonProperty("career")]
        public string? Career { get; set; }

        [JsonProperty("character_level")]
        public string? CharacterLevel { get; set; }

        [JsonProperty("part_date")]
        public string? PartDate { get; set; }

        [JsonProperty("update_date")]
        public string? UpdateDate { get; set; }

        [JsonProperty("career_type")]
        public string? CareerType { get; set; }
    }

    public class RSPlayerHomeInfoCharacter
    {
        [JsonProperty("create_time")]
        public string? CreateTime { get; set; }

        [JsonProperty("gender")]
        public string? Gender { get; set; }

        [JsonProperty("last_login_time")]
        public string? LastLoginTime { get; set; }

        [JsonProperty("race")]
        public string? Race { get; set; }

        [JsonProperty("character_name")]
        public string? CharacterName { get; set; }

        [JsonProperty("area_id")]
        public int AreaId { get; set; }

        [JsonProperty("play_time")]
        public string? PlayTime { get; set; }

        [JsonProperty("house_info")]
        public string? HouseInfo { get; set; }

        [JsonProperty("group_id")]
        public int GroupId { get; set; }

        [JsonProperty("guild_name")]
        public string? GuildName { get; set; }

        [JsonProperty("fc_id")]
        public string? FcId { get; set; }

        [JsonProperty("tribe")]
        public string? Tribe { get; set; }

        [JsonProperty("guild_tag")]
        public string? GuildTag { get; set; }

        [JsonProperty("washing_num")]
        public int WashingNum { get; set; }

        [JsonProperty("treasure_times")]
        public string? TreasureTimes { get; set; }

        [JsonProperty("kill_times")]
        public string? KillTimes { get; set; }

        [JsonProperty("newrank")]
        public string? Newrank { get; set; }

        [JsonProperty("crystal_rank")]
        public string? CrystalRank { get; set; }

        [JsonProperty("fish_times")]
        public string? FishTimes { get; set; }
    }

    public class RSPlayerHomeInfoFollow
    {
        [JsonProperty("followNum")]
        public int FollowNum { get; set; }

        [JsonProperty("fansNum")]
        public int FansNum { get; set; }
    }
}
