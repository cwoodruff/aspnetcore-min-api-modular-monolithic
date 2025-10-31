using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class InvoiceRepository(AppDbContext context) : BaseRepository<Invoice>(context), IInvoiceRepository
{
    public async Task<List<Invoice>> GetByCustomerId(int id) =>
        await _context.Invoices.Where(a => a.CustomerId == id)
                .AsNoTracking().ToListAsync();

    public async Task<InvoiceApiModel> GetById(int id) =>
        await _context.Invoices
            .Where(i => i.Id == id)
            .Select(i => new InvoiceApiModel
            {
                Id = i.Id,
                CustomerId = i.CustomerId,
                InvoiceDate = i.InvoiceDate,
                BillingAddress = i.BillingAddress,
                BillingCity = i.BillingCity,
                BillingState = i.BillingState,
                BillingCountry = i.BillingCountry,
                BillingPostalCode = i.BillingPostalCode,
                Total = i.Total,
                Customer = i.Customer == null ? null : new CustomerApiModel
                {
                    Id = i.Customer.Id,
                    FirstName = i.Customer.FirstName,
                    LastName = i.Customer.LastName,
                    Company = i.Customer.Company,
                    Email = i.Customer.Email,
                    Phone = i.Customer.Phone,
                    SupportRepId = i.Customer.SupportRepId,
                    SupportRepName = i.Customer.SupportRep != null
                        ? (i.Customer.SupportRep.FirstName + " " + i.Customer.SupportRep.LastName)
                        : null,
                    // Keep nested objects shallow
                    Invoices = new List<InvoiceApiModel>(),
                    SupportRep = null
                },
                InvoiceLines = i.InvoiceLines.Select(il => new InvoiceLineApiModel
                {
                    Id = il.Id,
                    InvoiceId = il.InvoiceId,
                    TrackId = il.TrackId,
                    TrackName = il.Track != null ? il.Track.Name : null,
                    UnitPrice = il.UnitPrice,
                    Quantity = il.Quantity,
                    Invoice = null,
                    Track = null
                }).ToList()
            })
            .AsNoTracking()
            .SingleAsync();
}
