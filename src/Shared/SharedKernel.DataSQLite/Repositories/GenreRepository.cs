using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class GenreRepository(AppDbContext context) : BaseRepository<Genre>(context), IGenreRepository
{
    public async Task<Genre?> GetById(int id)
    {
        return await _context.Genres
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id);
    }
}
