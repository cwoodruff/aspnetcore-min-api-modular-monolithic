using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class InvoiceLineRepository(AppDbContext context) : BaseRepository<InvoiceLine>(context), IInvoiceLineRepository
{
    public async Task<List<InvoiceLine>> GetByInvoiceId(int id)
    {
        return await _context.InvoiceLines.Where(a => a.InvoiceId == id)
            .AsNoTracking().ToListAsync();
    }

    public async Task<List<InvoiceLine>> GetByTrackId(int id)
    {
        return await _context.InvoiceLines.Where(a => a.TrackId == id)
            .AsNoTracking().ToListAsync();
    }

    public async Task<InvoiceLine?> GetById(int id)
    {
        return await _context.InvoiceLines
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id);
    }
}
