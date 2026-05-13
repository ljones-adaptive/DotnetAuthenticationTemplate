using System.Text.Json;
using ScalpingApp.Models;

namespace ScalpingApp.Services;

public class MarketDataService : IMarketDataService
{
    private readonly HttpClient _http;
    private readonly ILogger<MarketDataService> _logger;

    public MarketDataService(HttpClient http, ILogger<MarketDataService> logger)
    {
        _http = http;
        _http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<List<Candle>> GetCandlesAsync(
        string symbol, string interval, DateTime from, DateTime to)
    {
        var p1  = new DateTimeOffset(from, TimeSpan.Zero).ToUnixTimeSeconds();
        var p2  = new DateTimeOffset(to,   TimeSpan.Zero).ToUnixTimeSeconds();
        var sym = Uri.EscapeDataString(symbol);
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{sym}" +
                  $"?interval={interval}&period1={p1}&period2={p2}&includePrePost=false";

        try
        {
            var json = await _http.GetStringAsync(url);
            return Parse(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Market data fetch failed for {Symbol} {Interval}", symbol, interval);
            return new List<Candle>();
        }
    }

    private static List<Candle> Parse(string json)
    {
        var result = new List<Candle>();
        using var doc = JsonDocument.Parse(json);

        var chart = doc.RootElement.GetProperty("chart");
        if (chart.GetProperty("error").ValueKind != JsonValueKind.Null) return result;

        var arr = chart.GetProperty("result");
        if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return result;

        var data       = arr[0];
        var timestamps = data.GetProperty("timestamp").EnumerateArray()
                             .Select(t => t.GetInt64()).ToArray();
        var q     = data.GetProperty("indicators").GetProperty("quote")[0];
        var opens  = q.GetProperty("open").EnumerateArray().ToArray();
        var highs  = q.GetProperty("high").EnumerateArray().ToArray();
        var lows   = q.GetProperty("low").EnumerateArray().ToArray();
        var closes = q.GetProperty("close").EnumerateArray().ToArray();
        var vols   = q.GetProperty("volume").EnumerateArray().ToArray();

        for (int i = 0; i < timestamps.Length; i++)
        {
            if (opens[i].ValueKind  == JsonValueKind.Null ||
                closes[i].ValueKind == JsonValueKind.Null) continue;

            result.Add(new Candle
            {
                Time   = timestamps[i],
                Open   = opens[i].GetDecimal(),
                High   = highs[i].GetDecimal(),
                Low    = lows[i].GetDecimal(),
                Close  = closes[i].GetDecimal(),
                Volume = vols[i].ValueKind != JsonValueKind.Null ? vols[i].GetInt64() : 0
            });
        }

        return result.OrderBy(c => c.Time).ToList();
    }
}
