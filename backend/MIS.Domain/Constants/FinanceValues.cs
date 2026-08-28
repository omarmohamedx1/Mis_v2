namespace MIS.Domain.Constants;

public static class FinanceValues
{
    public static class PeriodStatuses
    {
        public const string Open = "OPEN";
        public const string SoftClosed = "SOFT_CLOSED";
        public const string Closed = "CLOSED";
    }

    public static class JournalStatuses
    {
        public const string Draft = "DRAFT";
        public const string PendingApproval = "PENDING_APPROVAL";
        public const string Approved = "APPROVED";
        public const string Posted = "POSTED";
        public const string Reversed = "REVERSED";
    }

    public static class EventStatuses
    {
        public const string Received = "RECEIVED";
        public const string Posted = "POSTED";
        public const string Failed = "FAILED";
    }

    public static class CollectionReceiptStatuses
    {
        public const string Posted = "POSTED";
        public const string Cleared = "CLEARED";
        public const string Reversed = "REVERSED";
    }

    public static class CollectionChannels
    {
        public const string CashCollector = "CASH_COLLECTOR";
        public const string CashBranch = "CASH_BRANCH";
        public const string BankTransfer = "BANK_TRANSFER";
        public const string Cheque = "CHEQUE";
        public const string Gateway = "GATEWAY";
        public const string DirectClient = "DIRECT_CLIENT";
    }

    public static class CustodyTransactionTypes
    {
        public const string Collection = "COLLECTION";
        public const string Handover = "HANDOVER";
        public const string Reversal = "REVERSAL";
        public const string Shortage = "SHORTAGE";
        public const string Overage = "OVERAGE";
        public const string Adjustment = "ADJUSTMENT";
    }

    public static class AccountTypes
    {
        public const string Asset = "ASSET";
        public const string Liability = "LIABILITY";
        public const string Equity = "EQUITY";
        public const string Revenue = "REVENUE";
        public const string Expense = "EXPENSE";
    }
}
