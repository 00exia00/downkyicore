using DownKyi.Core.BiliApi.Dynamic.Models;
using Newtonsoft.Json;

namespace DownKyi.Core.BiliApi.Dynamic;

public static class DynamicApi
{
    private const string FeedUrl =
        "https://api.bilibili.com/x/polymer/web-dynamic/v1/feed/all?type=all&platform=web";

    public static Task<DynamicFeedData?> GetFeedAsync(
        string? offset = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetFeed(offset, cancellationToken), cancellationToken);
    }

    internal static DynamicFeedData? ParseResponse(string response)
    {
        var origin = JsonConvert.DeserializeObject<DynamicFeedOrigin>(response);
        return origin is { Code: 0 } ? origin.Data : null;
    }

    private static DynamicFeedData? GetFeed(string? offset, CancellationToken cancellationToken)
    {
        var url = string.IsNullOrWhiteSpace(offset)
            ? FeedUrl
            : $"{FeedUrl}&offset={Uri.EscapeDataString(offset)}";
        var origin = BiliApiRequest.RequestJson<DynamicFeedOrigin>(
            url,
            "https://t.bilibili.com/",
            nameof(GetFeed),
            nameof(DynamicApi),
            cancellationToken);
        return origin is { Code: 0 } ? origin.Data : null;
    }
}
