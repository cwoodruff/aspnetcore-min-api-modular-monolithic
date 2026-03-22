using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class MediaTypeRepository(AppDbContext context) : BaseRepository<MediaType>(context), IMediaTypeRepository
{
    public async Task<MediaType?> GetById(int id)
    {
        return await _context.MediaTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id);
    }
}
