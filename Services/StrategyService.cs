using ScalpingApp.Models;

namespace ScalpingApp.Services;

/// <summary>
/// Implements the "Liquidity Zone + Pitchfork" strategy:
///   1. Mark swing highs/lows on the 1H chart as magic lines (support / resistance).
///   2. On the 15M chart watch for 3-5 emotional candles (sucker move) driving into a zone.
///   3. First reversal candle = pitchfork.  Entry on break of pitchfork high/low.
///   4. Stop just beyond the wick; target = opposite magic line.
/// </summary>
public class StrategyService : IStrategyService
{
    // Swing detection: a candle is a swing high/low if it is the extreme within ±N bars
    private const int SwingWindow = 5;

    // Two levels are "the same zone" if they are within this % of each other
    private const decimal ZoneTolerance = 0.003m;  // 0.3 %

    // Sucker move: min/max consecutive same-direction candles
    private const int SuckerMin = 3;
    private const int SuckerMax = 6;

    // How close (% of price) the sucker-move must reach the zone
    private const decimal ZoneReach = 0.010m;      // 1.0 % (generous — ES can move fast)

    // Magic lines must sit within this % of the day's mid-price to be considered relevant
    private const decimal RelevanceBuffer = 0.04m; // ±4 %

    public TradingData ApplyStrategy(
        List<Candle> candles1h,
        List<Candle> candles15m,
        string symbol,
        string date,
        bool isLive)
    {
        var allLevels  = DetectMagicLines(candles1h);

        // ── Filter to levels that are within ±4% of today's actual trading price ──
        // This removes stale historical zones that are far from the current day.
        List<SwingLevel> magicLines;
        if (candles15m.Any())
        {
            var midPrice   = candles15m.Average(c => (c.High + c.Low) / 2m);
            var lowerBound = midPrice * (1m - RelevanceBuffer);
            var upperBound = midPrice * (1m + RelevanceBuffer);
            magicLines = allLevels
                .Where(l => l.Price >= lowerBound && l.Price <= upperBound)
                .ToList();
        }
        else
        {
            magicLines = allLevels;
        }

        var signals = DetectSignals(candles15m, magicLines);

        return new TradingData
        {
            Candles15m   = candles15m,
            MagicLines   = magicLines,
            Signals      = signals,
            Symbol       = symbol,
            Date         = date,
            IsLive       = isLive
        };
    }

    // ─── Swing level detection (1H) ────────────────────────────────────────────

    private static List<SwingLevel> DetectMagicLines(List<Candle> candles)
    {
        if (candles.Count < SwingWindow * 2 + 1)
            return new List<SwingLevel>();

        var rawHighs = new List<decimal>();
        var rawLows  = new List<decimal>();

        for (int i = SwingWindow; i < candles.Count - SwingWindow; i++)
        {
            var c = candles[i];
            bool isHigh = true, isLow = true;

            for (int j = i - SwingWindow; j <= i + SwingWindow; j++)
            {
                if (j == i) continue;
                if (candles[j].High >= c.High) isHigh = false;
                if (candles[j].Low  <= c.Low)  isLow  = false;
            }

            if (isHigh) rawHighs.Add(c.High);
            if (isLow)  rawLows.Add(c.Low);
        }

        var levels = new List<SwingLevel>();
        levels.AddRange(GroupLevels(rawHighs, "resistance"));
        levels.AddRange(GroupLevels(rawLows,  "support"));

        // Return up to 3 of each type, most-tested first
        return levels
            .GroupBy(l => l.Type)
            .SelectMany(g => g.OrderByDescending(l => l.TouchCount).Take(3))
            .ToList();
    }

    private static List<SwingLevel> GroupLevels(List<decimal> prices, string type)
    {
        // Cluster nearby prices into zones, tracking touch count
        var clusters = new List<(decimal centroid, int count)>();

        foreach (var p in prices.OrderDescending())
        {
            int idx = clusters.FindIndex(
                c => Math.Abs(c.centroid - p) / c.centroid < ZoneTolerance);

            if (idx >= 0)
            {
                var (centroid, count) = clusters[idx];
                clusters[idx] = ((centroid * count + p) / (count + 1), count + 1);
            }
            else
            {
                clusters.Add((p, 1));
            }
        }

        return clusters.Select(c => new SwingLevel
        {
            Price      = Math.Round(c.centroid, 2),
            Type       = type,
            TouchCount = c.count
        }).ToList();
    }

    // ─── Signal detection (15M) ────────────────────────────────────────────────

    private static List<TradeSignal> DetectSignals(
        List<Candle> candles, List<SwingLevel> magicLines)
    {
        var signals = new List<TradeSignal>();
        if (candles.Count < SuckerMin + 2 || magicLines.Count == 0)
            return signals;

        var supports    = magicLines.Where(l => l.Type == "support")
                                   .OrderByDescending(l => l.Price).ToList();
        var resistances = magicLines.Where(l => l.Type == "resistance")
                                   .OrderBy(l => l.Price).ToList();

        // Track candle indices already consumed by a signal to avoid overlap
        var usedUpTo = -1;

        for (int i = SuckerMin; i < candles.Count - 2; i++)
        {
            if (i <= usedUpTo) continue;

            // Try long: sucker move down into the highest support
            if (supports.Any())
            {
                var support    = supports.First();
                var resistance = resistances.FirstOrDefault()
                                 ?? new SwingLevel { Price = support.Price * 1.02m };

                var sig = TryLong(candles, i, support, resistance);
                if (sig != null)
                {
                    ResolveOutcome(sig, candles, i + 2);
                    signals.Add(sig);
                    usedUpTo = i + 3;
                    continue;
                }
            }

            // Try short: sucker move up into the lowest resistance
            if (resistances.Any())
            {
                var resistance = resistances.First();
                var support    = supports.FirstOrDefault()
                                 ?? new SwingLevel { Price = resistance.Price * 0.98m };

                var sig = TryShort(candles, i, support, resistance);
                if (sig != null)
                {
                    ResolveOutcome(sig, candles, i + 2);
                    signals.Add(sig);
                    usedUpTo = i + 3;
                }
            }
        }

        return signals;
    }

    /// <summary>
    /// Long setup: ≥3 consecutive bearish candles reaching the support zone,
    /// followed by a bullish (pitchfork) candle.
    /// Entry: next candle breaks above pitchfork high.
    /// </summary>
    private static TradeSignal? TryLong(
        List<Candle> candles, int pitchforkIdx,
        SwingLevel support, SwingLevel resistance)
    {
        var pitchfork = candles[pitchforkIdx];
        if (!pitchfork.IsBullish) return null;

        for (int len = SuckerMax; len >= SuckerMin; len--)
        {
            int start = pitchforkIdx - len;
            if (start < 0) continue;

            // All candles in the window must be bearish
            bool allBearish = true;
            for (int j = start; j < pitchforkIdx; j++)
                if (candles[j].IsBullish) { allBearish = false; break; }
            if (!allBearish) continue;

            // The lowest low of the sucker move must reach the support zone
            var suckerLow = candles[start..pitchforkIdx].Min(c => c.Low);
            if (Math.Abs(suckerLow - support.Price) / support.Price > ZoneReach) continue;

            // Build phases
            var phases = new List<SignalPhase>();
            for (int j = start; j < pitchforkIdx; j++)
                phases.Add(new SignalPhase
                    { Time = candles[j].Time, Type = "sucker_move", CandleIndex = j });

            phases.Add(new SignalPhase
                { Time = pitchfork.Time, Type = "pitchfork", CandleIndex = pitchforkIdx });

            phases.Add(new SignalPhase
                { Time = candles[pitchforkIdx + 1].Time, Type = "entry",
                  CandleIndex = pitchforkIdx + 1 });

            var wick  = pitchfork.High - pitchfork.Low;
            var stop  = Math.Min(suckerLow, pitchfork.Low) - wick * 0.1m;

            return new TradeSignal
            {
                EntryTime = candles[pitchforkIdx + 1].Time,
                Direction = "long",
                Entry     = pitchfork.High,
                StopLoss  = Math.Round(stop, 2),
                Target    = resistance.Price,
                Phases    = phases
            };
        }

        return null;
    }

    /// <summary>
    /// Short setup: ≥3 consecutive bullish candles reaching the resistance zone,
    /// followed by a bearish (pitchfork) candle.
    /// </summary>
    private static TradeSignal? TryShort(
        List<Candle> candles, int pitchforkIdx,
        SwingLevel support, SwingLevel resistance)
    {
        var pitchfork = candles[pitchforkIdx];
        if (pitchfork.IsBullish) return null;

        for (int len = SuckerMax; len >= SuckerMin; len--)
        {
            int start = pitchforkIdx - len;
            if (start < 0) continue;

            bool allBullish = true;
            for (int j = start; j < pitchforkIdx; j++)
                if (!candles[j].IsBullish) { allBullish = false; break; }
            if (!allBullish) continue;

            var suckerHigh = candles[start..pitchforkIdx].Max(c => c.High);
            if (Math.Abs(suckerHigh - resistance.Price) / resistance.Price > ZoneReach) continue;

            var phases = new List<SignalPhase>();
            for (int j = start; j < pitchforkIdx; j++)
                phases.Add(new SignalPhase
                    { Time = candles[j].Time, Type = "sucker_move", CandleIndex = j });

            phases.Add(new SignalPhase
                { Time = pitchfork.Time, Type = "pitchfork", CandleIndex = pitchforkIdx });

            phases.Add(new SignalPhase
                { Time = candles[pitchforkIdx + 1].Time, Type = "entry",
                  CandleIndex = pitchforkIdx + 1 });

            var wick = pitchfork.High - pitchfork.Low;
            var stop = Math.Max(suckerHigh, pitchfork.High) + wick * 0.1m;

            return new TradeSignal
            {
                EntryTime = candles[pitchforkIdx + 1].Time,
                Direction = "short",
                Entry     = pitchfork.Low,
                StopLoss  = Math.Round(stop, 2),
                Target    = support.Price,
                Phases    = phases
            };
        }

        return null;
    }

    // ─── Outcome resolution ────────────────────────────────────────────────────

    private static void ResolveOutcome(
        TradeSignal signal, List<Candle> candles, int fromIdx)
    {
        for (int i = fromIdx; i < candles.Count; i++)
        {
            var c = candles[i];
            if (signal.Direction == "long")
            {
                if (c.High >= signal.Target)
                {
                    signal.Outcome     = "target";
                    signal.OutcomeTime = c.Time;
                    signal.Phases.Add(new SignalPhase
                        { Time = c.Time, Type = "target", CandleIndex = i });
                    return;
                }
                if (c.Low <= signal.StopLoss)
                {
                    signal.Outcome     = "stop";
                    signal.OutcomeTime = c.Time;
                    signal.Phases.Add(new SignalPhase
                        { Time = c.Time, Type = "stop", CandleIndex = i });
                    return;
                }
            }
            else
            {
                if (c.Low <= signal.Target)
                {
                    signal.Outcome     = "target";
                    signal.OutcomeTime = c.Time;
                    signal.Phases.Add(new SignalPhase
                        { Time = c.Time, Type = "target", CandleIndex = i });
                    return;
                }
                if (c.High >= signal.StopLoss)
                {
                    signal.Outcome     = "stop";
                    signal.OutcomeTime = c.Time;
                    signal.Phases.Add(new SignalPhase
                        { Time = c.Time, Type = "stop", CandleIndex = i });
                    return;
                }
            }
        }
    }
}
