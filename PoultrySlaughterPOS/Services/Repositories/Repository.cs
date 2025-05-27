using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using System.Linq.Expressions;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Generic repository implementation providing optimized Entity Framework Core operations
    /// with comprehensive error handling and performance monitoring
    /// </summary>
    /// <typeparam name="T">Entity type implementing class constraint</typeparam>
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly PoultryDbContext _context;
        protected readonly DbSet<T> _dbSet;
        protected readonly ILogger<Repository<T>> _logger;

        public Repository(PoultryDbContext context, ILogger<Repository<T>> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = context.Set<T>();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet.FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entity {EntityType} with ID {Id}", typeof(T).Name, id);
                throw;
            }
        }

        public virtual async Task<T?> GetAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet.FirstOrDefaultAsync(predicate, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving single entity {EntityType} with predicate", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet.ToListAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet.Where(predicate).ToListAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding entities {EntityType} with predicate", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet.AnyAsync(predicate, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence for entity {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                return predicate == null
                    ? await _dbSet.CountAsync(cancellationToken).ConfigureAwait(false)
                    : await _dbSet.CountAsync(predicate, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting entities {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            try
            {
                var entry = await _dbSet.AddAsync(entity, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Added entity {EntityType} to context", typeof(T).Name);
                return entry.Entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding entity {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            try
            {
                var entityList = entities.ToList();
                await _dbSet.AddRangeAsync(entityList, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Added {Count} entities of type {EntityType} to context", entityList.Count, typeof(T).Name);
                return entityList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding range of entities {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            try
            {
                _dbSet.Update(entity);
                _logger.LogDebug("Updated entity {EntityType} in context", typeof(T).Name);
                return await Task.FromResult(entity).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating entity {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
                if (entity == null)
                {
                    _logger.LogWarning("Entity {EntityType} with ID {Id} not found for deletion", typeof(T).Name, id);
                    return false;
                }

                _dbSet.Remove(entity);
                _logger.LogDebug("Marked entity {EntityType} with ID {Id} for deletion", typeof(T).Name, id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting entity {EntityType} with ID {Id}", typeof(T).Name, id);
                throw;
            }
        }

        public virtual async Task<bool> DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            try
            {
                _dbSet.Remove(entity);
                _logger.LogDebug("Marked entity {EntityType} for deletion", typeof(T).Name);
                return await Task.FromResult(true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting entity {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<int> DeleteRangeAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                var entities = await _dbSet.Where(predicate).ToListAsync(cancellationToken).ConfigureAwait(false);
                _dbSet.RemoveRange(entities);
                _logger.LogDebug("Marked {Count} entities of type {EntityType} for deletion", entities.Count, typeof(T).Name);
                return entities.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting range of entities {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                IQueryable<T> query = _dbSet;

                if (filter != null)
                    query = query.Where(filter);

                if (orderBy != null)
                    query = orderBy(query);

                return await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged entities {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<TResult>> SelectAsync<TResult>(
            Expression<Func<T, TResult>> selector,
            Expression<Func<T, bool>>? predicate = null,
            CancellationToken cancellationToken = default) where TResult : class
        {
            try
            {
                IQueryable<T> query = _dbSet;

                if (predicate != null)
                    query = query.Where(predicate);

                return await query
                    .Select(selector)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting projected entities {EntityType} to {ResultType}", typeof(T).Name, typeof(TResult).Name);
                throw;
            }
        }
    }
}