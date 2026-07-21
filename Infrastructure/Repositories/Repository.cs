using System.Linq.Expressions;
using Cs2Admin.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Cs2Admin.API.Infrastructure.Repositories;

public class Repository<T>(ApplicationDbContext context) : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext Context = context;
    protected readonly DbSet<T> DbSet = context.Set<T>();

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await DbSet.ToListAsync();
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await DbSet.FindAsync(id);
    }

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await DbSet.FirstOrDefaultAsync(predicate);
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await DbSet.Where(predicate).ToListAsync();
    }

    public virtual async Task AddAsync(T entity)
    {
        await DbSet.AddAsync(entity);
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> entities)
    {
        await DbSet.AddRangeAsync(entities);
    }

    public virtual void Update(T entity)
    {
        DbSet.Update(entity);
    }

    public virtual void Remove(T entity)
    {
        DbSet.Remove(entity);
    }

    public virtual void RemoveRange(IEnumerable<T> entities)
    {
        DbSet.RemoveRange(entities);
    }

    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await DbSet.AnyAsync(predicate);
    }

    public virtual async Task<int> SaveChangesAsync()
    {
        return await Context.SaveChangesAsync();
    }
}
