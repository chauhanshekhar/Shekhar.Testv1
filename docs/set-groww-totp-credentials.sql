/*
  set-groww-totp-credentials.sql

  PURPOSE
    Enable fully automatic Groww token refresh (TOTP flow, no daily approval on Groww Cloud).
    GrowwTokenService uses TOTP when SystemSettings 'GrowwTotpSecret' holds a Base32 value;
    otherwise it falls back to the checksum/"approval" flow that needs a daily human approval.

  BEFORE RUNNING
    1. Groww Cloud > API Keys > generate the TOTP token. Copy the API key and the Base32 TOTP secret.
       The TOTP secret is letters A-Z and digits 2-7 only. It is NOT the API secret.
    2. Replace the two placeholders below. Do NOT save this file with real values and do NOT commit it.
       Run it against the database the Worker actually uses (this was missing in TradingDB_Paper).
    3. Restart the Worker afterwards (it refreshes at startup and at 06:10 IST).

  SECURITY
    Values are plaintext in SystemSettings and in backups. Never SELECT * from this table.
    Leave @ApiKey as NULL to keep the existing GrowwApiKey unchanged.
*/

DECLARE @ApiKey     nvarchar(max) = NULL;                    -- optional: new API key, or NULL to keep existing
DECLARE @TotpSecret nvarchar(max) = N'<PASTE_BASE32_TOTP_SECRET>';

-- Guard: refuse to run with the placeholder or a non-Base32 value.
IF @TotpSecret LIKE N'<%' OR @TotpSecret LIKE N'%[^A-Za-z2-7 =-]%' OR LEN(LTRIM(RTRIM(@TotpSecret))) = 0
BEGIN
    RAISERROR('GrowwTotpSecret is a placeholder or not Base32 (A-Z, 2-7). Nothing changed.', 16, 1);
    RETURN;
END

BEGIN TRAN;

IF EXISTS (SELECT 1 FROM SystemSettings WHERE [Key] = 'GrowwTotpSecret')
    UPDATE SystemSettings SET [Value] = @TotpSecret, UpdatedOn = SYSDATETIME() WHERE [Key] = 'GrowwTotpSecret';
ELSE
    INSERT INTO SystemSettings ([Key], [Value], UpdatedOn) VALUES ('GrowwTotpSecret', @TotpSecret, SYSDATETIME());

IF @ApiKey IS NOT NULL AND LEN(LTRIM(RTRIM(@ApiKey))) > 0
BEGIN
    IF EXISTS (SELECT 1 FROM SystemSettings WHERE [Key] = 'GrowwApiKey')
        UPDATE SystemSettings SET [Value] = @ApiKey, UpdatedOn = SYSDATETIME() WHERE [Key] = 'GrowwApiKey';
    ELSE
        INSERT INTO SystemSettings ([Key], [Value], UpdatedOn) VALUES ('GrowwApiKey', @ApiKey, SYSDATETIME());
END

COMMIT;

/* ---------------------------------------------------------------------------
   VERIFY (shows set/empty only, never the values)
   Expect GrowwApiKey = set, GrowwTotpSecret = set with a fresh UpdatedOn.
   After the Worker restart, GrowwAccessToken.UpdatedOn should move to today, and the Worker log
   should say "Groww access token refreshed automatically" (not "checksum (approval) token flow").
--------------------------------------------------------------------------- */
SELECT [Key],
       CASE WHEN LEN(LTRIM(RTRIM([Value]))) > 0 THEN 'set' ELSE 'empty' END AS State,
       UpdatedOn
FROM SystemSettings
WHERE [Key] IN ('GrowwApiKey','GrowwTotpSecret','GrowwApiSecret','GrowwAccessToken')
ORDER BY [Key];

/* ---------------------------------------------------------------------------
   ROLLBACK (removes the TOTP secret; the code reverts to the checksum/approval flow).
   If you also changed GrowwApiKey, restore the old value by hand first; it was not saved here.

   DELETE FROM SystemSettings WHERE [Key] = 'GrowwTotpSecret';
--------------------------------------------------------------------------- */
