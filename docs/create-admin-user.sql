/* ============================================================================
   CREATE THE FIRST ADMIN USER                                       2026-10-15
   ----------------------------------------------------------------------------
   WHY THIS SCRIPT EXISTS
   ----------------------
   The API offers no way to bootstrap the first account:

     - POST /api/auth/register  is [Authorize(Roles = "Admin,Super Admin")], so it
       needs an admin that does not exist yet (AuthController.cs:56).
     - POST /api/auth/seed      is gated on IsDevelopment() (AuthController.cs:87),
       and running the API in Development also runs Database.Migrate() against
       whatever database it is pointed at (Program.cs:29-33). Against
       TradingDB_Paper that would apply pending migrations to production, so it
       is NOT an acceptable way to create a login.

   This script inserts the row directly instead. Claude does not execute it.

   BEFORE YOU RUN IT
   -----------------
   Replace @PasswordHash below with a hash produced by the SAME hasher the API
   verifies with (ASP.NET Identity PasswordHasher, salted - it cannot be written
   by hand). Generate it with the throwaway utility in the session scratchpad:

       1. Put your chosen password in a file, with no trailing newline:
            printf '%s' 'YOUR_PASSWORD' > pwd.txt
       2. Generate the hash:
            dotnet run --project <scratchpad>/hashgen -- pwd.txt
       3. Delete pwd.txt, and paste the printed value into @PasswordHash.

   The password must be at least 8 characters to match the API's own rule.

   SAFETY
   ------
   - Idempotent: re-running it will not create a duplicate (Email is UNIQUE).
   - Touches only dbo.Users. No trading table, setting or schema is modified.
   - A ROLLBACK block and a VERIFY query are at the bottom.
   ============================================================================ */

SET NOCOUNT ON;
GO

DECLARE @Email        nvarchar(100) = N'shekhar@trading.com';   -- login id
DECLARE @FullName     nvarchar(100) = N'Shekhar';
DECLARE @Role         nvarchar(20)  = N'Admin';                 -- Admin | Trader
DECLARE @PasswordHash nvarchar(500) = N'PASTE_THE_GENERATED_HASH_HERE';

/* ---------------------------------------------------------------------------
   PRE-FLIGHT. Confirm the table really is empty before adding an admin.
   If ExistingUsers > 0, STOP and log in with the existing account instead.
   --------------------------------------------------------------------------- */
SELECT COUNT(*) AS ExistingUsers,
       SUM(CASE WHEN Role IN (N'Admin', N'Super Admin') THEN 1 ELSE 0 END) AS ExistingAdmins
FROM dbo.Users;

/* ---------------------------------------------------------------------------
   GUARD: refuse to run with the placeholder still in place.
   --------------------------------------------------------------------------- */
IF @PasswordHash = N'PASTE_THE_GENERATED_HASH_HERE'
BEGIN
    RAISERROR('Replace @PasswordHash with a generated hash first - see the header.', 16, 1);
    RETURN;
END

/* ---------------------------------------------------------------------------
   INSERT (idempotent).
   --------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM dbo.Users WHERE LOWER(Email) = LOWER(@Email))
BEGIN
    PRINT 'User already exists - nothing inserted.';
END
ELSE
BEGIN
    INSERT INTO dbo.Users (FullName, Email, PasswordHash, IsActive, Role, CreatedOn)
    VALUES (@FullName, LOWER(@Email), @PasswordHash, 1, @Role, SYSDATETIME());

    PRINT CONCAT('Created user: ', @Email, ' (rows = ', @@ROWCOUNT, ')');
END
GO

/* ---------------------------------------------------------------------------
   VERIFY. Expect exactly one row, IsActive = 1, Role = Admin and a
   PasswordHash that is NOT the placeholder. Never select the hash itself.
   --------------------------------------------------------------------------- */
SELECT Id,
       FullName,
       Email,
       Role,
       IsActive,
       CreatedOn,
       LEN(PasswordHash) AS PasswordHashLength
FROM dbo.Users
WHERE LOWER(Email) = LOWER(N'shekhar@trading.com');

/* ---------------------------------------------------------------------------
   ROLLBACK - removes only the row this script created.
   ---------------------------------------------------------------------------
DELETE FROM dbo.Users WHERE LOWER(Email) = LOWER(N'shekhar@trading.com');
SELECT COUNT(*) AS RemainingUsers FROM dbo.Users;
   --------------------------------------------------------------------------- */

/* ============================================================================
   FOLLOW-UP - worth fixing so this script is never needed again
   ----------------------------------------------------------------------------
   The bootstrap gap is a real design problem: a fresh production database
   cannot be given its first administrator through any supported path. The
   options, in order of preference:

     1. Allow /api/auth/seed outside Development when the Users table is empty
        (it already no-ops when any user exists, so the risk is low), instead of
        tying first-run setup to the environment name.
     2. Decouple Database.Migrate() from IsDevelopment() - make it depend on
        Database:AutoMigrate alone, so running in Development against a remote
        database cannot silently migrate it.

   Item 2 is worth doing regardless of this script: as written, ANY developer
   who starts the API in Development while pointed at TradingDB_Paper will apply
   pending migrations to production at startup, with no prompt and no record.
   ============================================================================ */
