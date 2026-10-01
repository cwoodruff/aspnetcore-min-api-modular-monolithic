namespace Admin.Modules.Models;

/// <summary>A customer's purchases as Administration has counted them so far (eventually consistent, ADR-0010).</summary>
internal sealed record CustomerPurchasesApiModel(int CustomerId, decimal TotalSpent, int InvoiceCount, DateTime? LastPurchaseAt);
