namespace Trading.Backtest.Discovery;

/// <summary>Research grid, not proposed live settings. Includes no-RSI/no-PCR baselines.</summary>
public static class DiscoveryRuleFactory
{
    public static IReadOnlyList<DiscoveryRule> Build()
    {
        var rules = new List<DiscoveryRule>();
        decimal?[][] rsiBands =
        [
            [null, null],
            [40m, 50m], [45m, 60m], [50m, 65m], [55m, 70m], [60m, 80m]
        ];
        decimal[] atrFloors = [0.02m, 0.03m, 0.04m];
        foreach (var side in Enum.GetValues<DiscoverySide>())
        foreach (var five in new[] { false, true })
        foreach (var band in rsiBands)
        foreach (var atr in atrFloors)
        foreach (var pcr in new decimal?[] { null, 0.80m, 0.90m, 1.00m })
        {
            var rsiText = band[0] is null ? "RSI-ANY" : $"RSI-{band[0]:0}-{band[1]:0}";
            var pcrText = pcr is null ? "PCR-ANY" : side == DiscoverySide.Call ? $"PCR-GE-{pcr:0.00}" : $"PCR-LE-{pcr:0.00}";
            var name = $"{side}-EMA1-5M{(five ? "-ON" : "-OFF")}-{rsiText}-ATRGE-{atr:0.00}-{pcrText}";
            rules.Add(new DiscoveryRule(name, side, five, band[0], band[1], atr, pcr));
        }
        return rules;
    }
}
