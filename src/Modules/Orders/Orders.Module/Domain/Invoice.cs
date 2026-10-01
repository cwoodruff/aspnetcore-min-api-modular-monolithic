namespace Orders.Modules.Domain;

internal sealed class Invoice
{
    public int Id { get; set; }

    public int? CustomerId { get; set; }

    public DateTime InvoiceDate { get; set; }

    public string? BillingAddress { get; set; }

    public string? BillingCity { get; set; }

    public string? BillingState { get; set; }

    public string? BillingCountry { get; set; }

    public string? BillingPostalCode { get; set; }

    public decimal Total { get; set; }

    /// <summary>Draft until finalized; finalizing publishes InvoiceFinalized (ADR-0008).</summary>
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public ICollection<InvoiceLine> InvoiceLines { get; set; } = new List<InvoiceLine>();
}

internal enum InvoiceStatus
{
    Draft,
    Finalized
}
