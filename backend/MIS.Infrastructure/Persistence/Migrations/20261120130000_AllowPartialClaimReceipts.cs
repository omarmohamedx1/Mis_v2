using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations;

// Review only. Deployment requires explicit approval; no financial rows are changed.
public partial class AllowPartialClaimReceipts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_claim_receipt_postings_ClaimId", "claim_receipt_postings", "finance");
        migrationBuilder.CreateIndex("IX_claim_receipt_postings_ClaimId", "claim_receipt_postings", "ClaimId", "finance");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Never discard or reverse receipts to make the old uniqueness constraint fit.
        migrationBuilder.Sql("""
            DO $$ BEGIN
              IF EXISTS (SELECT 1 FROM finance.claim_receipt_postings
                         WHERE "ReversedAt" IS NULL GROUP BY "ClaimId" HAVING COUNT(*) > 1) THEN
                RAISE EXCEPTION 'Rollback blocked: multiple active claim receipts exist. Review with Finance; no receipts were changed.';
              END IF;
            END $$;
            """);
        migrationBuilder.DropIndex("IX_claim_receipt_postings_ClaimId", "claim_receipt_postings", "finance");
        migrationBuilder.CreateIndex("IX_claim_receipt_postings_ClaimId", "claim_receipt_postings", "ClaimId", "finance",
            unique: true, filter: "\"ReversedAt\" IS NULL");
    }
}
