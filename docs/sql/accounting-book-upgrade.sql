START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    ALTER TABLE finance.accounts ADD "IsCashAccount" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE TABLE "AccountingRecurringExpenses" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "DueDay" integer NOT NULL,
        "AccountId" uuid NOT NULL,
        "CashAccountId" uuid NOT NULL,
        "StartsOn" date NOT NULL,
        "EndsOn" date,
        "Notes" character varying(1000),
        "IsActive" boolean NOT NULL,
        "CreatedById" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AccountingRecurringExpenses" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AccountingRecurringExpenses_Users_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_AccountingRecurringExpenses_accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES finance.accounts ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_AccountingRecurringExpenses_accounts_CashAccountId" FOREIGN KEY ("CashAccountId") REFERENCES finance.accounts ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE TABLE "AccountingBookEntries" (
        "Id" uuid NOT NULL,
        "DocumentNumber" character varying(40) NOT NULL,
        "Category" character varying(40) NOT NULL,
        "TransactionDate" date NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "AccountId" uuid NOT NULL,
        "CounterAccountId" uuid NOT NULL,
        "Description" character varying(1000) NOT NULL,
        "Reference" character varying(160),
        "JournalEntryId" uuid NOT NULL,
        "EmployeeId" uuid,
        "RelatedAdvanceId" uuid,
        "RecurringExpenseId" uuid,
        "SourceKey" character varying(150),
        "CreatedById" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AccountingBookEntries" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_AccountingBookEntries_Amount" CHECK ("Amount" > 0),
        CONSTRAINT "FK_AccountingBookEntries_AccountingBookEntries_RelatedAdvanceId" FOREIGN KEY ("RelatedAdvanceId") REFERENCES "AccountingBookEntries" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_AccountingBookEntries_AccountingRecurringExpenses_Recurring~" FOREIGN KEY ("RecurringExpenseId") REFERENCES "AccountingRecurringExpenses" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_AccountingBookEntries_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_AccountingBookEntries_Users_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_AccountingBookEntries_accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES finance.accounts ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_AccountingBookEntries_accounts_CounterAccountId" FOREIGN KEY ("CounterAccountId") REFERENCES finance.accounts ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_AccountingBookEntries_journal_entries_JournalEntryId" FOREIGN KEY ("JournalEntryId") REFERENCES finance.journal_entries ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    UPDATE finance.accounts SET "IsCashAccount" = FALSE
    WHERE "Id" = '10000000-0000-0000-0001-000000110100';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    UPDATE finance.accounts SET "IsCashAccount" = FALSE
    WHERE "Id" = '10000000-0000-0000-0001-000000110200';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    UPDATE finance.accounts SET "IsCashAccount" = FALSE
    WHERE "Id" = '10000000-0000-0000-0001-000000111100';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    UPDATE finance.accounts SET "IsCashAccount" = FALSE
    WHERE "Id" = '10000000-0000-0000-0001-000000112100';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    UPDATE finance.accounts SET "IsCashAccount" = FALSE
    WHERE "Id" = '10000000-0000-0000-0001-000000112200';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    UPDATE finance.accounts SET "IsCashAccount" = FALSE
    WHERE "Id" = '10000000-0000-0000-0001-000000112300';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    UPDATE finance.accounts SET "IsCashAccount" = FALSE
    WHERE "Id" = '10000000-0000-0000-0002-000000210100';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    UPDATE finance.accounts SET "IsCashAccount" = FALSE
    WHERE "Id" = '10000000-0000-0000-0002-000000210200';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    UPDATE finance.accounts SET "IsCashAccount" = FALSE
    WHERE "Id" = '10000000-0000-0000-0004-000000410100';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    UPDATE finance.accounts SET "IsCashAccount" = FALSE
    WHERE "Id" = '10000000-0000-0000-0006-000000610100';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    INSERT INTO finance.accounts ("Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector")
    VALUES ('10000000-0000-0000-0001-000000110300', 'ASSET', '110300', NULL, TRUE, TRUE, '10000000-0000-0000-0000-000000000001', 'خزينة الشركة التشغيلية', 'Company operating cash', 'DEBIT', NULL, TRUE, FALSE, FALSE, FALSE);
    INSERT INTO finance.accounts ("Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector")
    VALUES ('10000000-0000-0000-0001-000000110400', 'ASSET', '110400', NULL, TRUE, TRUE, '10000000-0000-0000-0000-000000000001', 'بنك الشركة التشغيلي', 'Company operating bank', 'DEBIT', NULL, TRUE, FALSE, FALSE, FALSE);
    INSERT INTO finance.accounts ("Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector")
    VALUES ('10000000-0000-0000-0001-000000113100', 'ASSET', '113100', NULL, TRUE, FALSE, '10000000-0000-0000-0000-000000000001', 'سلف الموظفين', 'Employee advances', 'DEBIT', NULL, TRUE, FALSE, FALSE, FALSE);
    INSERT INTO finance.accounts ("Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector")
    VALUES ('10000000-0000-0000-0003-000000310100', 'EQUITY', '310100', NULL, TRUE, FALSE, '10000000-0000-0000-0000-000000000001', 'أرصدة افتتاحية', 'Opening balances', 'CREDIT', NULL, TRUE, FALSE, FALSE, FALSE);
    INSERT INTO finance.accounts ("Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector")
    VALUES ('10000000-0000-0000-0006-000000610110', 'EXPENSE', '610110', NULL, TRUE, FALSE, '10000000-0000-0000-0000-000000000001', 'مصاريف نثرية', 'Petty expenses', 'DEBIT', NULL, TRUE, FALSE, FALSE, FALSE);
    INSERT INTO finance.accounts ("Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector")
    VALUES ('10000000-0000-0000-0006-000000610120', 'EXPENSE', '610120', NULL, TRUE, FALSE, '10000000-0000-0000-0000-000000000001', 'مصاريف شهرية ثابتة', 'Fixed monthly expenses', 'DEBIT', NULL, TRUE, FALSE, FALSE, FALSE);
    INSERT INTO finance.accounts ("Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector")
    VALUES ('10000000-0000-0000-0006-000000610130', 'EXPENSE', '610130', NULL, TRUE, FALSE, '10000000-0000-0000-0000-000000000001', 'انتقالات', 'Transportation expenses', 'DEBIT', NULL, TRUE, FALSE, FALSE, FALSE);
    INSERT INTO finance.accounts ("Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector")
    VALUES ('10000000-0000-0000-0006-000000610140', 'EXPENSE', '610140', NULL, TRUE, FALSE, '10000000-0000-0000-0000-000000000001', 'مرتبات وأجور', 'Salaries and wages', 'DEBIT', NULL, TRUE, FALSE, FALSE, FALSE);
    INSERT INTO finance.accounts ("Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector")
    VALUES ('10000000-0000-0000-0006-000000610150', 'EXPENSE', '610150', NULL, TRUE, FALSE, '10000000-0000-0000-0000-000000000001', 'مكافآت', 'Bonuses and rewards', 'DEBIT', NULL, TRUE, FALSE, FALSE, FALSE);
    INSERT INTO finance.accounts ("Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector")
    VALUES ('10000000-0000-0000-0006-000000610160', 'EXPENSE', '610160', NULL, TRUE, FALSE, '10000000-0000-0000-0000-000000000001', 'مصاريف قضائية', 'Judicial expenses', 'DEBIT', NULL, TRUE, FALSE, FALSE, FALSE);
    INSERT INTO finance.accounts ("Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector")
    VALUES ('10000000-0000-0000-0006-000000610170', 'EXPENSE', '610170', NULL, TRUE, FALSE, '10000000-0000-0000-0000-000000000001', 'عمولات المحصلين', 'Collector commissions', 'DEBIT', NULL, TRUE, FALSE, FALSE, FALSE);
    INSERT INTO finance.accounts ("Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector")
    VALUES ('10000000-0000-0000-0006-000000610180', 'EXPENSE', '610180', NULL, TRUE, FALSE, '10000000-0000-0000-0000-000000000001', 'عمولات السوبر فايزر', 'Supervisor commissions', 'DEBIT', NULL, TRUE, FALSE, FALSE, FALSE);
    INSERT INTO finance.accounts ("Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector")
    VALUES ('10000000-0000-0000-0006-000000610190', 'EXPENSE', '610190', NULL, TRUE, FALSE, '10000000-0000-0000-0000-000000000001', 'عمولات ضمن المرتبات', 'Commissions included in payroll', 'DEBIT', NULL, TRUE, FALSE, FALSE, FALSE);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE INDEX "IX_AccountingBookEntries_AccountId" ON "AccountingBookEntries" ("AccountId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE INDEX "IX_AccountingBookEntries_Category_TransactionDate" ON "AccountingBookEntries" ("Category", "TransactionDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE INDEX "IX_AccountingBookEntries_CounterAccountId" ON "AccountingBookEntries" ("CounterAccountId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE INDEX "IX_AccountingBookEntries_CreatedById" ON "AccountingBookEntries" ("CreatedById");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE UNIQUE INDEX "IX_AccountingBookEntries_DocumentNumber" ON "AccountingBookEntries" ("DocumentNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE INDEX "IX_AccountingBookEntries_EmployeeId" ON "AccountingBookEntries" ("EmployeeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE UNIQUE INDEX "IX_AccountingBookEntries_JournalEntryId" ON "AccountingBookEntries" ("JournalEntryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE INDEX "IX_AccountingBookEntries_RecurringExpenseId" ON "AccountingBookEntries" ("RecurringExpenseId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE INDEX "IX_AccountingBookEntries_RelatedAdvanceId" ON "AccountingBookEntries" ("RelatedAdvanceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE UNIQUE INDEX "IX_AccountingBookEntries_SourceKey" ON "AccountingBookEntries" ("SourceKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE INDEX "IX_AccountingRecurringExpenses_AccountId" ON "AccountingRecurringExpenses" ("AccountId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE INDEX "IX_AccountingRecurringExpenses_CashAccountId" ON "AccountingRecurringExpenses" ("CashAccountId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    CREATE INDEX "IX_AccountingRecurringExpenses_CreatedById" ON "AccountingRecurringExpenses" ("CreatedById");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016150900_AddAccountingBookAndRecurring') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261016150900_AddAccountingBookAndRecurring', '10.0.4');
    END IF;
END $EF$;
COMMIT;

