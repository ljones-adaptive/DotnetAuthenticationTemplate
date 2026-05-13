using ScalpingApp.Models;

namespace ScalpingApp.Services;

public interface IMarketDataService
{
    Task<List<Candle>> GetCandlesAsync(string symbol, string interval, DateTime from, DateTime to);
}
