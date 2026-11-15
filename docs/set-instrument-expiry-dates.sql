/*
  set-instrument-expiry-dates.sql

  PURPOSE
    Set the current option expiry for NIFTY, BANKNIFTY and SENSEX in TradingInstruments.Expiry.

  WHY THIS MATTERS
    TradingInstruments.Expiry (yyyy-MM-dd) drives two things:
      * OptionChainWorker only fetches the option chain for instruments whose Expiry is set; once the
        date is in the past it NULLs the column and skips that instrument until a new date is set.
      * IsExpiryDay() is true only on the day that equals Expiry (expiry routing, HeroZero handover,
        first-30-minute no-trade window).
    So after each expiry passes, the symbol silently loses its option chain until this is run again.

  BEFORE RUNNING
    Take the next expiry date for each index from the exchange / Groww option chain page. Do not
    guess it: expiry weekdays have been changed by the exchanges before. Enter a date as 'yyyy-MM-dd'
    or leave it NULL to skip that symbol. Dates in the past are rejected.
    This only UPDATES existing rows (it never inserts), so LotSize / StrikeInterval are untouched.
*/

DECLARE @NiftyExpiry     date = NULL;   -- e.g. '2026-11-24'
DECLARE @BankNiftyExpiry date = NULL;
DECLARE @SensexExpiry    date = NULL;

DECLARE @Today date = CAST(SYSDATETIME() AS date);   -- server clock; use IST-aligned DB server time

IF (@NiftyExpiry IS NOT NULL AND @NiftyExpiry < @Today)
   OR (@BankNiftyExpiry IS NOT NULL AND @BankNiftyExpiry < @Today)
   OR (@SensexExpiry IS NOT NULL AND @SensexExpiry < @Today)
BEGIN
    RAISERROR('An expiry date is in the past. Nothing changed.', 16, 1);
    RETURN;
END

BEGIN TRAN;

UPDATE TradingInstruments
SET Expiry = CONVERT(char(10), @NiftyExpiry, 23), UpdatedOn = SYSDATETIME()
WHERE Symbol = 'NIFTY' AND @NiftyExpiry IS NOT NULL;

UPDATE TradingInstruments
SET Expiry = CONVERT(char(10), @BankNiftyExpiry, 23), UpdatedOn = SYSDATETIME()
WHERE Symbol = 'BANKNIFTY' AND @BankNiftyExpiry IS NOT NULL;

UPDATE TradingInstruments
SET Expiry = CONVERT(char(10), @SensexExpiry, 23), UpdatedOn = SYSDATETIME()
WHERE Symbol = 'SENSEX' AND @SensexExpiry IS NOT NULL;

COMMIT;

/* VERIFY: every active index should show a future date (or today on expiry day). */
SELECT Symbol, Exchange, IsActive, Expiry, UpdatedOn
FROM TradingInstruments
WHERE Symbol IN ('NIFTY','BANKNIFTY','SENSEX')
ORDER BY Symbol;

/* ROLLBACK: note the old values from a SELECT BEFORE running, then restore them, e.g.
   UPDATE TradingInstruments SET Expiry = '<old yyyy-MM-dd or NULL>' WHERE Symbol = 'NIFTY';
*/
