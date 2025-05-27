using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using System.Linq.Expressions;

namespace PoultrySlaughterPOS.Repositories
{
    /// <summary>
    /// Base repository implementation providing thread-safe CRUD operations using DbContextFactory pattern.
    /// Optimized for high-concurrency POS terminal scenarios with proper resource disposal and error handling.
    /// </summary>
    /// <typeparam name="TEntity">Entity type implementing database model</typeparam>
    /// <typeparam name="TKey">Primary key type for the entity</typeparam>
    public abstract class BaseRepository<TEntity, TKey> : IBaseRepository<TEntity, TKey> where TEntity : class
    {
        protected readonly IDbContextFactory<PoultryDbContext> _contextFactory;
        protected readonly ILogger<BaseRepository<TEntity, TKey>> _logger;

        protected BaseRepository(IDbContextFactory<PoultryDbContext> contextFactory, ILogger<BaseRepository<TEntity, TKey>> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Abstract method to get the DbSet for the specific entity
        protected abstract DbSet<TEntity> GetDbSet(PoultryDbContext context);
        protected abstract Expression<Func<TEntity, bool>> GetByIdPredicate(TKey id);

        #region Basic CRUD Operations

        public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);
                var predicate = GetByIdPredicate(id);

                return await dbSet.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving {EntityType} with ID {Id}", typeof(TEntity).Name, id);
                throw;
            }
        }

        public virtual async Task<TEntity?> GetByIdAsync(TKey id, params Expression<Func<TEntity, object>>[] includes)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                var dbSet = GetDbSet(context);
                var query = dbSet.AsNoTracking();

                // Apply includes for related entities
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }

                var predicate = GetByIdPredicate(id);
                return await query.FirstOrDefaultAsync(predicate).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving {EntityType} with ID {Id} and includes", typeof(TEntity).Name, id);
                throw;
            }
        }

        public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);

                return await dbSet.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all {EntityType} entities", typeof(TEntity).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<TEntity>> GetAllAsync(params Expression<Func<TEntity, object>>[] includes)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                var dbSet = GetDbSet(context);
                var query = dbSet.AsNoTracking();

                // Apply includes for related entities
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }

                return await query.ToListAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all {EntityType} entities with includes", typeof(TEntity).Name);
                throw;
            }
        }

        #endregion

        #region Advanced Querying

        public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);

                return await dbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding {EntityType} entities with predicate", typeof(TEntity).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, params Expression<Func<TEntity, object>>[] includes)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                var dbSet = GetDbSet(context);
                var query = dbSet.AsNoTracking().Where(predicate);

                // Apply includes for related entities
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }

                return await query.ToListAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding {EntityType} entities with predicate and includes", typeof(TEntity).Name);
                throw;
            }
        }

        public virtual async Task<TEntity?> FindSingleAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);

                return await dbSet.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding single {EntityType} entity with predicate", typeof(TEntity).Name);
                throw;
            }
        }

        public virtual async Task<TEntity?> FindSingleAsync(Expression<Func<TEntity, bool>> predicate, params Expression<Func<TEntity, object>>[] includes)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                var dbSet = GetDbSet(context);
                var query = dbSet.AsNoTracking().Where(predicate);

                // Apply includes for related entities
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }

                return await query.FirstOrDefaultAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding single {EntityType} entity with predicate and includes", typeof(TEntity).Name);
                throw;
            }
        }

        #endregion

        #region Pagination Support

        public virtual async Task<(IEnumerable<TEntity> Items, int TotalCount)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<TEntity, bool>>? filter = null,
            Expression<Func<TEntity, object>>? orderBy = null,
            bool ascending = true,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);
                var query = dbSet.AsNoTracking();

                // Apply filter if provided
                if (filter != null)
                {
                    query = query.Where(filter);
                }

                // Get total count for pagination metadata
                var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

                // Apply ordering
                if (orderBy != null)
                {
                    query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
                }

                // Apply pagination
                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return (items, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged {EntityType} entities. Page: {PageNumber}, Size: {PageSize}",
                    typeof(TEntity).Name, pageNumber, pageSize);
                throw;
            }
        }

        #endregion

        #region Aggregation Operations

        public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);

                return await dbSet.CountAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting {EntityType} entities", typeof(TEntity).Name);
                throw;
            }
        }

        public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);

                return await dbSet.CountAsync(predicate, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting {EntityType} entities with predicate", typeof(TEntity).Name);
                throw;
            }
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);

                return await dbSet.AnyAsync(predicate, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of {EntityType} entity with predicate", typeof(TEntity).Name);
                throw;
            }
        }

        #endregion

        #region Modification Operations

        public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);

                var entityEntry = await dbSet.AddAsync(entity, cancellationToken).ConfigureAwait(false);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully added {EntityType} entity", typeof(TEntity).Name);
                return entityEntry.Entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding {EntityType} entity", typeof(TEntity).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);

                var entitiesList = entities.ToList();
                await dbSet.AddRangeAsync(entitiesList, cancellationToken).ConfigureAwait(false);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully added {Count} {EntityType} entities", entitiesList.Count, typeof(TEntity).Name);
                return entitiesList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding range of {EntityType} entities", typeof(TEntity).Name);
                throw;
            }
        }

        public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);

                var entityEntry = dbSet.Update(entity);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully updated {EntityType} entity", typeof(TEntity).Name);
                return entityEntry.Entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating {EntityType} entity", typeof(TEntity).Name);
                throw;
            }
        }

        public virtual async Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);
                var predicate = GetByIdPredicate(id);

                var entity = await dbSet.FirstOrDefaultAsync(predicate, cancellationToken).ConfigureAwait(false);
                if (entity == null)
                {
                    _logger.LogWarning("{EntityType} with ID {Id} not found for deletion", typeof(TEntity).Name, id);
                    return false;
                }

                dbSet.Remove(entity);
                var affectedRows = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully deleted {EntityType} entity with ID {Id}", typeof(TEntity).Name, id);
                return affectedRows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting {EntityType} entity with ID {Id}", typeof(TEntity).Name, id);
                throw;
            }
        }

        public virtual async Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);

                dbSet.Remove(entity);
                var affectedRows = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully deleted {EntityType} entity", typeof(TEntity).Name);
                return affectedRows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting {EntityType} entity", typeof(TEntity).Name);
                throw;
            }
        }

        public virtual async Task<int> DeleteRangeAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);

                var entities = await dbSet.Where(predicate).ToListAsync(cancellationToken).ConfigureAwait(false);
                if (!entities.Any())
                {
                    _logger.LogInformation("No {EntityType} entities found matching deletion criteria", typeof(TEntity).Name);
                    return 0;
                }

                dbSet.RemoveRange(entities);
                var affectedRows = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully deleted {Count} {EntityType} entities", entities.Count, typeof(TEntity).Name);
                return affectedRows;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting range of {EntityType} entities", typeof(TEntity).Name);
                throw;
            }
        }

        #endregion

        #region Bulk Operations

        public virtual async Task<int> BulkUpdateAsync(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, TEntity>> updateExpression, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var dbSet = GetDbSet(context);

                // For now, implement as individual updates
                // In production, consider using EF Core bulk extensions for better performance
                var entities = await dbSet.Where(predicate).ToListAsync(cancellationToken).ConfigureAwait(false);

                foreach (var entity in entities)
                {
                    var updatedEntity = updateExpression.Compile()(entity);
                    context.Entry(entity).CurrentValues.SetValues(updatedEntity);
                }

                var affectedRows = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully bulk updated {Count} {EntityType} entities", entities.Count, typeof(TEntity).Name);
                return affectedRows;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk updating {EntityType} entities", typeof(TEntity).Name);
                throw;
            }
        }

        #endregion

        #region Raw SQL Support

        public virtual async Task<IEnumerable<TEntity>> FromSqlRawAsync(string sql, params object[] parameters)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                var dbSet = GetDbSet(context);

                return await dbSet.FromSqlRaw(sql, parameters).AsNoTracking().ToListAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing raw SQL query for {EntityType}", typeof(TEntity).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> ExecuteQueryAsync<T>(string sql, params object[] parameters) where T : class
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

                return await context.Set<T>().FromSqlRaw(sql, parameters).AsNoTracking().ToListAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing raw SQL query for type {Type}", typeof(T).Name);
                throw;
            }
        }

        #endregion
    }
}