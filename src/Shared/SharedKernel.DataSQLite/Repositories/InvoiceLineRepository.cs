using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class InvoiceLineRepository(AppDbContext context) : BaseRepository<InvoiceLine>(context), IInvoiceLineRepository
{
    public async Task<List<InvoiceLine>> GetByInvoiceId(int id) =>
        await _context.InvoiceLines.Where(a => a.InvoiceId == id)
                .AsNoTracking().ToListAsync();

    public async Task<List<InvoiceLine>> GetByTrackId(int id) =>
        await _context.InvoiceLines.Where(a => a.TrackId == id)
                .AsNoTracking().ToListAsync();

    public async Task<InvoiceLine> GetById(int id) =>
        await _context.InvoiceLines
            .AsNoTracking()
            .SingleAsync(e => e.Id == id);
}
