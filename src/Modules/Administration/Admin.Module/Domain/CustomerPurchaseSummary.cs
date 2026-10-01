namespace Admin.Modules.Domain;

/// <summary>
/// Administration's own running summary of a customer's purchases, built from InvoiceFinalized events
/// (ADR-0010). Eventually consistent with Orders: it counts only invoices finalized since phase 3,
/// after the orders outbox has delivered them.
/// </summary>
internal sealed class CustomerPurchaseSummary
{
    public int CustomerId { get; set; }

    public decimal TotalSpent { get; set; }

    public int InvoiceCount { get; set; }

    /// <summary>The latest invoice date among the counted purchases.</summary>
    public DateTime? LastPurchaseAt { get; set; }
}
