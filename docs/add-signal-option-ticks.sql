-- Adds the SignalOptionTicks audit table (migration 20261125073727_AddSignalOptionTicks).
-- One row per open signal per ExitWorker cycle: the option premium the exit ladder acted on.
-- Idempotent. Equivalent to `dotnet ef database update`; run EITHER this script OR the EF update,
-- not both. If this script is used, also insert the migration row (last statement) so EF stays in sync.
-- Review before running. Claude does not execute this.

IF OBJECT_ID(N'dbo.SignalOptionTicks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SignalOptionTicks
    (
        Id            bigint        IDENTITY(1,1) NOT NULL CONSTRAINT PK_SignalOptionTicks PRIMARY KEY,
        SignalId      bigint        NOT NULL,
        Symbol        nvarchar(20)  NOT NULL,
        OptionSymbol  nvarchar(50)  NOT NULL,
        TickTime      datetime2     NOT NULL,
        OptionLtp     decimal(18,2) NOT NULL,
        Source        nvarchar(10)  NOT NULL,
        UnderlyingLtp decimal(18,2) NOT NULL,
        OptionSL      decimal(18,2) NOT NULL,
        ProtectedSL   decimal(18,2) NOT NULL,
        Status        nvarchar(30)  NOT NULL
    );

    CREATE INDEX IX_SignalOptionTicks_SignalId_TickTime ON dbo.SignalOptionTicks (SignalId, TickTime);
    CREATE INDEX IX_SignalOptionTicks_TickTime          ON dbo.SignalOptionTicks (TickTime);
END;

-- Keep EF's history in step when this script is used instead of `dotnet ef database update`.
-- (ProductVersion must match the other rows in __EFMigrationsHistory.)
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261125073727_AddSignalOptionTicks')
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT TOP (1) N'20261125073727_AddSignalOptionTicks', ProductVersion
    FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC;

-- ============================ VERIFY ============================
-- Table and both indexes exist (expect 1 table row, 2 index rows):
SELECT t.name AS TableName FROM sys.tables t WHERE t.name = N'SignalOptionTicks';
SELECT i.name AS IndexName FROM sys.indexes i
WHERE i.object_id = OBJECT_ID(N'dbo.SignalOptionTicks') AND i.name LIKE N'IX_SignalOptionTicks_%';
SELECT MigrationId FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261125073727_AddSignalOptionTicks';

-- After the next live position (should be roughly 1 row/second while it is open; Source mostly QUOTE):
-- SELECT SignalId, MIN(TickTime) AS First, MAX(TickTime) AS Last, COUNT(*) AS Rows,
--        SUM(CASE WHEN Source = 'QUOTE' THEN 1 ELSE 0 END) AS Quote,
--        SUM(CASE WHEN Source = 'CHAIN' THEN 1 ELSE 0 END) AS Chain,
--        SUM(CASE WHEN Source = 'NONE'  THEN 1 ELSE 0 END) AS NoPrice
-- FROM dbo.SignalOptionTicks GROUP BY SignalId ORDER BY SignalId DESC;

-- ============================ ROLLBACK ============================
-- Audit-only table, nothing reads it. Safe to drop; the recorder then logs a warning once a minute
-- until redeployed without it.
-- DELETE FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261125073727_AddSignalOptionTicks';
-- DROP TABLE IF EXISTS dbo.SignalOptionTicks;
