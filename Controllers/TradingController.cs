using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScalpingApp.Services;
using System.Text.Json;

namespace ScalpingApp.Controllers;

[Authorize]
public class TradingController : Controller
{
    private readonly IMarketDataService _market;
    private readonly IStrategyService   _strategy;

    public TradingController(IMarketDataService market, IStrategyService strategy)
    {
        _market   = market;
        _strategy = strategy;
    }

    public IActionResult Index() => View();

    /// <summary>
    /// Returns JSON payload consumed by the chart page.
    /// GET /Trading/Data?symbol=ES%3DF&date=2026-05-10   (historical)
    /// GET /Trading/Data?symbol=ES%3DF&date=today         (live / today)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Data(
        string symbol = "ES=F",
        string date   = "today")
    {
        try
        {
            // ── Parse date ──────────────────────────────────────────────────
            bool isToday =
                date == "today" ||
                date == DateTime.UtcNow.ToString("yyyy-MM-dd");

            DateTime targetDate;
            if (isToday)
                targetDate = DateTime.UtcNow.Date;
            else if (!DateTime.TryParse(date, out var parsed))
                return Json(new { error = "Invalid date format. Use yyyy-MM-dd." });
            else
                targetDate = parsed.Date;

            // ── Market status (US Eastern time) ─────────────────────────────
            var et = GetEasternTime();
            var nowEt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, et);
            bool marketOpen =
                isToday &&
                nowEt.DayOfWeek != DayOfWeek.Saturday &&
                nowEt.DayOfWeek != DayOfWeek.Sunday &&
                nowEt.TimeOfDay >= new TimeSpan(9, 30, 0) &&
                nowEt.TimeOfDay <  new TimeSpan(16, 0, 0);

            // ── Fetch data ──────────────────────────────────────────────────
            // 1H candles: 40 days lookback for swing-level detection
            var hourlyFrom = targetDate.AddDays(-40);
            var hourlyTo   = isToday ? DateTime.UtcNow : targetDate.AddDays(1);
            var candles1h  = await _market.GetCandlesAsync(symbol, "1h", hourlyFrom, hourlyTo);

            // 15M candles: target date only
            var dayFrom    = targetDate;
            var dayTo      = isToday ? DateTime.UtcNow : targetDate.AddDays(1);
            var candles15m = await _market.GetCandlesAsync(symbol, "15m", dayFrom, dayTo);

            if (!candles15m.Any())
                return Json(new
                {
                    error = $"No 15-minute data found for {symbol} on " +
                            $"{targetDate:dd MMM yyyy}. " +
                            "The market may have been closed (weekend / holiday)."
                });

            // ── Apply strategy ──────────────────────────────────────────────
            var result = _strategy.ApplyStrategy(
                candles1h, candles15m, symbol,
                targetDate.ToString("yyyy-MM-dd"), marketOpen);

            result.MarketStatus = marketOpen ? "open" : "closed";

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return Json(result, options);
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    private static TimeZoneInfo GetEasternTime()
    {
        // Linux uses IANA names; Windows uses display names
        try   { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
    }
}
