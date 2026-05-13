namespace ScalpingApp.Models;

public class Candle
{
    public long    Time   { get; set; }   // Unix timestamp (seconds, UTC)
    public decimal Open   { get; set; }
    public decimal High   { get; set; }
    public decimal Low    { get; set; }
    public decimal Close  { get; set; }
    public long    Volume { get; set; }
    public bool    IsBullish => Close >= Open;
}

public class SwingLevel
{
    public decimal Price      { get; set; }
    public string  Type       { get; set; } = ""; // "support" | "resistance"
    public int     TouchCount { get; set; }
}

public class SignalPhase
{
    public long   Time        { get; set; }
    public string Type        { get; set; } = ""; // sucker_move | pitchfork | entry | target | stop
    public int    CandleIndex { get; set; }
}

public class TradeSignal
{
    public long          EntryTime   { get; set; }
    public string        Direction   { get; set; } = ""; // "long" | "short"
    public decimal       Entry       { get; set; }
    public decimal       StopLoss    { get; set; }
    public decimal       Target      { get; set; }
    public string        Outcome     { get; set; } = "open"; // "target" | "stop" | "open"
    public long?         OutcomeTime { get; set; }
    public List<SignalPhase> Phases  { get; set; } = new();
}

public class TradingData
{
    public List<Candle>      Candles15m   { get; set; } = new();
    public List<SwingLevel>  MagicLines   { get; set; } = new();
    public List<TradeSignal> Signals      { get; set; } = new();
    public bool              IsLive       { get; set; }
    public string            MarketStatus { get; set; } = "closed";
    public string            Symbol       { get; set; } = "";
    public string            Date         { get; set; } = "";
    public string?           Error        { get; set; }
}
