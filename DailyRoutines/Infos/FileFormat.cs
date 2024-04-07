using System.Collections.Generic;
using System;

namespace DailyRoutines.Infos;

public class FileFormat
{
    public class GitHubRelease
    {
        public int id { get; set; }
        public string tag_name { get; set; } = null!;
        public string name { get; set; } = null!;
        public string body { get; set; } = null!;
        public DateTime published_at { get; set; }
        public List<GitHubAsset> assets { get; set; } = null!;
    }

    public class GitHubAsset
    {
        public string name { get; set; } = null!;
        public int download_count { get; set; }
    }

    // 石之家玩家搜索结果
    public class RSPlayerSearchResult
    {
        public int code {get; set; }
        public string msg { get; set; } = null!;
        public List<RSPlayerSearchResultData> data { get; set; } = null!;
    }

    public class RSPlayerSearchResultData
    {
        public uint uuid { get; set; }
        public string avatar { get; set; } = null!;
        public string character_name { get; set; } = null!;
        // 大区名
        public string area_name { get; set; } = null!;
        // 服务器名
        public string group_name { get; set; } = null!;
        public string profile { get; set; } = null!;
        public uint test_limited_badge { get; set; }
        public uint posts2_creator_badge { get; set; }
        public int admin_tag { get; set; }
        public uint fansNum { get; set; }
    }

    public class RSActivityCalendar
    {
        public int code {get; set; }
        public string msg { get; set; } = null!;
        public List<RSActivityCalendarData> data { get; set; } = null!;
    }

    public class RSActivityCalendarData
    {
        public uint id { get; set; }
        public string name { get; set; } = null!;
        public string url { get; set; } = null!;
        public int begin_time { get; set; }
        public int end_time { get; set;}
        public uint weight { get; set; }
        public string color { get; set; } = null!;
        public int type { get; set; }
        public int daoyu_sw { get; set; }
        public string? banner_url { get; set; }
    }

}
