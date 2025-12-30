using System.Text.Json.Serialization;

namespace FF14RisingstoneCheckIn.Models;

public class ApiResponse<T>
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; set; }
}

public class SignRewardListResponse : ApiResponse<List<SignRewardItem>>
{
}

public struct ResponseData
{
    [JsonPropertyName("return_code")]
    public int ReturnCode { get; set; }

    [JsonPropertyName("error_type")]
    public int ErrorType { get; set; }

    [JsonPropertyName("return_message")]
    public string? ReturnMessage { get; set; }

    [JsonPropertyName("data")]
    public PushData Data { get; set; }
}

public struct PushData
{
    [JsonPropertyName("pushMsgSerialNum")]
    public string? PushMsgSerialNum { get; set; }

    [JsonPropertyName("mappedErrorCode")]
    public int MappedErrorCode { get; set; }

    [JsonPropertyName("failReason")]
    public string? FailReason { get; set; }

    [JsonPropertyName("appId")]
    public int AppID { get; set; }

    [JsonPropertyName("areaId")]
    public int AreaID { get; set; }

    [JsonPropertyName("nextAction")]
    public System.Text.Json.JsonElement? NextAction { get; set; }

    [JsonPropertyName("isNeedFullInfo")]
    public System.Text.Json.JsonElement? IsNeedFullInfo { get; set; }

    [JsonPropertyName("ticket")]
    public string? Ticket { get; set; }
}