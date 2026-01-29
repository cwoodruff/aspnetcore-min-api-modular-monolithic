using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class BaseRepository<T> : IRepository<T> where T : BaseEntity
{
#pragma warning disable CA1051
    protected readonly AppDbContext _context;
#pragma warning restore CA1051

    protected BaseRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> EntityExists(int id)
    {
        return await _context.Set<T>().AsNoTracking().AnyAsync(a => a.Id == id);
    }

    public async Task<List<T>> GetAll()
    {
        return await _context.Set<T>().AsNoTracking().ToListAsync();
    }

    //public async Task<T?> GetById(int id) => await _context.Set<T>().AsNoTracking().SingleAsync(e => e.Id == id);

    public async Task<T> Add(T entity)
    {
        await _context.Set<T>().AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> Update(T entity)
    {
        if (!await EntityExists(entity.Id))
        {
            return false;
        }

        _context.Set<T>().Update(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> Delete(int id)
    {
        if (!await EntityExists(id))
        {
            return false;
        }

        var toRemove = await _context.Set<T>().FindAsync(id);
        if (toRemove != null)
        {
            _context.Set<T>().Remove(toRemove);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    public IQueryable<T> GetByCondition(Expression<Func<T, bool>> expression)
    {
        return _context.Set<T>()
            .Where(expression)
            .AsNoTracking();
    }
}
