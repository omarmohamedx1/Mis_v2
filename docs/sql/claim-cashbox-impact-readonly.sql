-- Run only on the intended database using a read-only role/transaction.
-- Aggregate results contain no claim/customer personal data. Never repair via this query.
WITH affected AS (
    SELECT b."Id", b."Amount", b."DeletedAt", j."Status", j."Id" AS journal_id
    FROM "AccountingBookEntries" b
    JOIN finance.journal_entries j ON j."Id" = b."JournalEntryId"
    WHERE (b."SourceKey" LIKE 'CLAIM:%'
        OR EXISTS (SELECT 1 FROM finance.claim_receipt_postings p WHERE p."AccountingBookEntryId" = b."Id"))
      AND (b."Category" = 'INCOME' OR j."EntryType" = 'COMPANY_INCOME'
        OR EXISTS (SELECT 1 FROM finance.journal_entry_lines l JOIN finance.accounts a ON a."Id" = l."AccountId"
                   WHERE l."JournalEntryId" = j."Id" AND a."IsCashAccount"))
)
SELECT COUNT(*) AS historical_claim_generated_operating_entries,
       COUNT(*) FILTER (WHERE "Status" = 'POSTED' AND "DeletedAt" IS NULL) AS active_posted_entries,
       COALESCE(SUM("Amount") FILTER (WHERE "Status" = 'POSTED' AND "DeletedAt" IS NULL), 0) AS active_posted_amount,
       COUNT(*) FILTER (WHERE "Status" = 'REVERSED') AS reversed_entries,
       COUNT(*) FILTER (WHERE "DeletedAt" IS NOT NULL) AS soft_deleted_entries
FROM affected;
