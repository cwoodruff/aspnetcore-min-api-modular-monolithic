using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class CustomerRepository(AppDbContext context) : BaseRepository<Customer>(context), ICustomerRepository
{
    public async Task<List<Customer>> GetBySupportRepId(int id)
    {
        return await _context.Customers
            .Where(a => a.SupportRepId == id)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<CustomerApiModel> GetById(int id)
    {
        return await _context.Customers
            .Where(c => c.Id == id)
            .Select(c => new CustomerApiModel
            {
                Id = c.Id,
                FirstName = c.FirstName,
                LastName = c.LastName,
                // ... other fields ...
                SupportRepId = c.SupportRepId,
                SupportRepName = c.SupportRep != null ? c.SupportRep.FirstName + " " + c.SupportRep.LastName : null,
                SupportRep = c.SupportRep == null
                    ? null
                    : new EmployeeApiModel
                    {
                        Id = c.SupportRep.Id,
                        FirstName = c.SupportRep.FirstName,
                        LastName = c.SupportRep.LastName,
                        Title = c.SupportRep.Title
                        // No Customers collection here
                    },
                Invoices = c.Invoices.Select(i => new InvoiceApiModel
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
                    Customer = null // important: no back-reference
                }).ToList()
            })
            .AsNoTracking()
            .SingleAsync();
    }
}
