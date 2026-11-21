using System.Globalization;

namespace Trading.Backtest.Discovery;

public static class DiscoveryReportWriter
{
    public static void Write(string directory, IReadOnlyList<DiscoveryTrade> trades, int minimumTrades)
    {
        Directory.CreateDirectory(directory);
        var groups = trades.GroupBy(t => new { t.Symbol, t.RuleName, Side = t.Side.ToString(), t.GapGroup, t.SessionGroup })
            .Select(g =>
            {
                var x = new DiscoverySummary { Symbol = g.Key.Symbol, RuleName = g.Key.RuleName, Side = g.Key.Side,
                    GapGroup = g.Key.GapGroup, SessionGroup = g.Key.SessionGroup };
                foreach (var t in g)
                {
                    x.Trades++;
                    if (t.Result == "TARGET") { x.Targets++; x.GrossWinR += Math.Max(0, t.RMultiple); }
                    else if (t.Result == "STOP") { x.Stops++; x.GrossLossR += Math.Abs(Math.Min(0, t.RMultiple)); }
                    else { x.Neither++; if (t.RMultiple >= 0) x.GrossWinR += t.RMultiple; else x.GrossLossR += Math.Abs(t.RMultiple); }
                }
                return x;
            })
            .Where(x => x.Trades >= minimumTrades)
            .OrderByDescending(x => x.NetR).ToList();

        var ci = CultureInfo.InvariantCulture;
        using (var w = new StreamWriter(Path.Combine(directory, "discovery-ranking.csv")))
        {
            w.WriteLine("Symbol,Side,Rule,GapGroup,Session,Trades,Targets,Stops,Neither,WinRatePct,ProfitFactor,NetR");
            foreach (var x in groups)
                w.WriteLine($"{x.Symbol},{x.Side},{Csv(x.RuleName)},{x.GapGroup},{x.SessionGroup},{x.Trades},{x.Targets},{x.Stops},{x.Neither},{x.WinRatePct.ToString("F2", ci)},{x.ProfitFactor.ToString("F2", ci)},{x.NetR.ToString("F2", ci)}");
        }
        using (var w = new StreamWriter(Path.Combine(directory, "discovery-candidates.csv")))
        {
            w.WriteLine("Symbol,EntryIst,ExitIst,Side,GapGroup,Session,Rule,Entry,Exit,Target,Stop,Result,RMultiple");
            foreach (var t in trades)
                w.WriteLine($"{t.Symbol},{t.EntryIst:yyyy-MM-dd HH:mm:ss},{t.ExitIst:yyyy-MM-dd HH:mm:ss},{t.Side},{t.GapGroup},{t.SessionGroup},{Csv(t.RuleName)},{t.Entry.ToString("F2",ci)},{t.Exit.ToString("F2",ci)},{t.Target.ToString("F2",ci)},{t.Stop.ToString("F2",ci)},{t.Result},{t.RMultiple.ToString("F3",ci)}");
        }
        File.WriteAllText(Path.Combine(directory, "README.txt"),
            "Historical signal discovery output. Candidates are freshly generated from raw IndicatorSnapshots.\n" +
            "This first version measures underlying-index ATR target/stop outcomes using future 1-minute candles.\n" +
            "It does NOT claim option-premium P&L. OptionMinuteHistory is a separate source and must be joined for option execution validation.\n" +
            "Same-bar target/stop collisions are conservatively counted as STOP. Validate on date-separated holdout sessions before considering any live rule.\n");
    }

    private static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
}
