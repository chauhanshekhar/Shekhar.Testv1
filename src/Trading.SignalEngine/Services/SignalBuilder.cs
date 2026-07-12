using Trading.Domain.Entities;

namespace Trading.Domain;

public class SignalBuilder
{
    private readonly Signal _s = new();

    public SignalBuilder(string symbol)
    {
        _s.Symbol = symbol;
        _s.CreatedOn = IstClock.Now;
    }


    // =============================
    // Direction
    // =============================

    public SignalBuilder CALL()
    {
        _s.SignalType = TradingConst.SignalType.CALL;
        _s.OptionType = TradingConst.OptionType.CE;
        return this;
    }

    public SignalBuilder PUT()
    {
        _s.SignalType = TradingConst.SignalType.PUT;
        _s.OptionType = TradingConst.OptionType.PE;
        return this;
    }


    // =============================
    // Index
    // =============================

    public SignalBuilder AtIndexPrice(decimal price)
    {
        _s.EntryPrice = price;
        return this;
    }


    // Candle + ATR based
    public SignalBuilder WithIndexTargets(
        decimal stopLoss,
        decimal target1,
        decimal target2)
    {
        _s.StopLoss = Math.Round(stopLoss, 2);
        _s.Target1 = Math.Round(target1, 2);
        _s.Target2 = Math.Round(target2, 2);

        return this;
    }


    // fallback only
    public SignalBuilder WithIndexStops(decimal slPoints)
    {
        bool call =
            _s.SignalType == TradingConst.SignalType.CALL;


        if (call)
        {
            _s.StopLoss =
                _s.EntryPrice - slPoints;

            _s.Target1 =
                _s.EntryPrice + slPoints;

            _s.Target2 =
                _s.EntryPrice + (slPoints * 2);
        }
        else
        {
            _s.StopLoss =
                _s.EntryPrice + slPoints;

            _s.Target1 =
                _s.EntryPrice - slPoints;

            _s.Target2 =
                _s.EntryPrice - (slPoints * 2);
        }

        return this;
    }


    // =============================
    // Option
    // =============================

    public SignalBuilder ForOption(
        decimal strike,
        string optionType,
        string optionSymbol,
        decimal optionLtp,
        decimal delta = 0)
    {
        _s.StrikePrice = strike;
        _s.OptionType = optionType;
        _s.OptionSymbol = optionSymbol;
        _s.OptionLtp = optionLtp;


        // fallback option risk
        _s.OptionSL =
            Math.Round(optionLtp * 0.80m, 2);

        _s.OptionT1 =
            Math.Round(optionLtp * 1.15m, 2);

        _s.OptionT2 =
            Math.Round(optionLtp * 1.30m, 2);


        return this;
    }



    // Candle based option target
    public SignalBuilder WithOptionTargets(
        decimal optionSL,
        decimal optionT1,
        decimal optionT2)
    {
        _s.OptionSL =
            Math.Round(optionSL, 2);

        _s.OptionT1 =
            Math.Round(optionT1, 2);

        _s.OptionT2 =
            Math.Round(optionT2, 2);

        return this;
    }



    // =============================
    // Hero Zero
    // =============================

    // Cheap OTM premium, momentum-only entry — aggressive exits: 50% SL, 2x T1, 4x T2.
    public SignalBuilder AsHeroZero()
    {
        _s.StrategyType = TradingConst.StrategyType.HeroZero;

        _s.OptionSL =
            Math.Round(_s.OptionLtp * 0.50m, 2);

        _s.OptionT1 =
            Math.Round(_s.OptionLtp * 2.00m, 2);

        _s.OptionT2 =
            Math.Round(_s.OptionLtp * 4.00m, 2);

        return this;
    }


    // =============================
    // Other info
    // =============================

    public SignalBuilder OnExchange(
        string exchange)
    {
        _s.Exchange = exchange;
        return this;
    }


    public SignalBuilder WithLots(
        int lots)
    {
        _s.Quantity = lots;
        return this;
    }


    public SignalBuilder WithConfidence(
        decimal confidence)
    {
        _s.Confidence = confidence;
        return this;
    }


    public Signal Build()
    {
        return _s;
    }
}