using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class InvoiceRepository(AppDbContext context) : BaseRepository<Invoice>(context), IInvoiceRepository
{
    public async Task<List<Invoice>> GetByCustomerId(int id)
    {
        return await _context.Invoices.Where(a => a.CustomerId == id)
            .AsNoTracking().ToListAsync();
    }

    public async Task<InvoiceApiModel?> GetById(int id)
    {
        return await _context.Invoices
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
                InvoiceLines = i.InvoiceLines.Select(il => new InvoiceLineApiModel
                {
                    Id = il.Id,
                    InvoiceId = il.InvoiceId,
                    TrackId = il.TrackId,
                    UnitPrice = il.UnitPrice,
                    Quantity = il.Quantity,
                    Invoice = null
                }).ToList()
            })
            .AsNoTracking()
            .SingleOrDefaultAsync();
    }
}
