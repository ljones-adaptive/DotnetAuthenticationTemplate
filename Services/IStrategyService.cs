using ScalpingApp.Models;

namespace ScalpingApp.Services;

public interface IStrategyService
{
    TradingData ApplyStrategy(
        List<Candle> candles1h,
        List<Candle> candles15m,
        string symbol,
        string date,
        bool isLive);
}
