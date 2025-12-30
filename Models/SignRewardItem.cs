using System.Text.Json.Serialization;

namespace FF14RisingstoneCheckIn.Models;

public class SignRewardItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("begin_date")]
    public string BeginDate { get; set; } = string.Empty;

    [JsonPropertyName("end_date")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("rule")]
    public int Rule { get; set; }

    [JsonPropertyName("item_name")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("item_pic")]
    public string ItemPic { get; set; } = string.Empty;

    [JsonPropertyName("num")]
    public int Num { get; set; }

    [JsonPropertyName("item_desc")]
    public string ItemDesc { get; set; } = string.Empty;

    [JsonPropertyName("is_get")]
    public int IsGet { get; set; }
}

public enum SignRewardItemGetType
{
    Unmet = -1,
    Available = 0,
    Gotten = 1
}
