using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Recipe.NetCore.Base.Interface;
using Recipe.NetCore.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Recipe.NetCore.Base.Generic
{
    public class Repository<TEntity, TKey, TDbContext> : IRepository<TEntity, TKey>
        where TEntity : class, IBase<TKey>
        where TKey : IEquatable<TKey>
        where TDbContext : DbContext
    {
        protected IRequestInfo<TDbContext> RequestInfo { get; private set; }

        protected DbContext DbContext => this.RequestInfo.Context;

        protected DbSet<TEntity> DbSet => this.DbContext.Set<TEntity>();

        protected virtual IQueryable<TEntity> DefaultListQuery => this.DbContext.Set<TEntity>().AsQueryable().OrderBy(x => x.Id);

        protected virtual IQueryable<TEntity> DefaultUnOrderedListQuery => this.DbContext.Set<TEntity>().AsQueryable();

        protected virtual IQueryable<TEntity> DefaultSingleQuery => this.DbContext.Set<TEntity>().AsQueryable();

        public Repository(IRequestInfo<TDbContext> requestInfo) => this.RequestInfo = requestInfo;

        public virtual async Task<TEntity> GetAsync(TKey id) => await this.DefaultSingleQuery.SingleOrDefaultAsync(x => x.Id.Equals(id));

        public virtual async Task<IEnumerable<TEntity>> GetAsync(IList<TKey> ids) => await this.DefaultSingleQuery.Where(x => ids.Contains(x.Id)).ToListAsync();

        public virtual async Task<int> GetCount() => await this.DefaultListQuery.CountAsync();

        public virtual async Task<IEnumerable<TEntity>> GetAll(JsonapiRequest request) => await this.DefaultListQuery.GenerateQuery(request).ToListAsync();

        public virtual async Task<TEntity> Add(TEntity entity)    ////Reason for creating this function : when using Create function it was not able to perform cascade functionality for child entities. 
        {
            this.DbContext.Add(entity);
            return entity;
        }

        public virtual async Task<TEntity> Create(TEntity entity)
        {
            this.DbContext.Entry(entity).State = EntityState.Added;
            return entity;
        }

        public virtual async Task<TEntity> Update(TEntity entity)
        {
            var localEntity = this.GetExisting(entity);
            if (localEntity != null )
            {
                this.DbContext.Entry(localEntity).State = EntityState.Detached;
            }
            
            this.DbContext.Entry(entity).State = EntityState.Modified;
            return entity;
        }

        public virtual async Task DeleteAsync(TKey id)
        {
            var entity = await this.GetAsync(id);
            if (entity!=null)
            this.DbContext.Entry(entity).State = EntityState.Deleted;
        }

        public virtual async Task DeleteRange<TEntityList>(TEntityList entityList) where TEntityList : IQueryable
        {
            foreach (var each in entityList)
            {
                this.DbContext.Entry(each).State = EntityState.Deleted;
            }
        }

        public virtual async Task<TEntity> GetEntityOnly(TKey id) => await this.DbContext.Set<TEntity>().AsQueryable().SingleOrDefaultAsync(x => x.Id.Equals(id));

        //Use includesInCore param in Ef Core when Includes within Includes needed
        public virtual async Task<TEntity> Find(Expression<Func<TEntity, bool>> filter, List<Expression<Func<TEntity, object>>> includes = null, Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> includesInCore = null)
        {
            IQueryable<TEntity> query = this.DefaultUnOrderedListQuery;
            query = Queryable.Where(query, filter);

            if (includes != null)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }
            else if (includesInCore != null)
            {
                query = includesInCore(query);
            }

            return await query.FirstOrDefaultAsync();
        }

        //Use includesInCore param in Ef Core when Includes within Includes needed
        public virtual async Task<List<TEntity>> FindAll(Expression<Func<TEntity, bool>> filter, List<Expression<Func<TEntity, object>>> includes = null, Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> includesInCore = null)
        {
            IQueryable<TEntity> query = this.DefaultUnOrderedListQuery;
            query = Queryable.Where(query, filter);

            if (includes != null)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }
            else if (includesInCore != null)
            {
                query = includesInCore(query);
            }

            return await query.ToListAsync();
        }

        private TEntity GetExisting(TEntity entity) => this.DbContext.Set<TEntity>().Local.FirstOrDefault(x => x.Id.Equals(entity.Id));

        
        public virtual IQueryFilter<TEntity> Query(Expression<Func<TEntity, bool>> queryExpression) => new QueryFilter<TEntity, TKey, TDbContext>(this, queryExpression);

        internal async Task<IEnumerable<TEntity>> SelectAsync(Expression<Func<TEntity, bool>> filter = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
            List<Expression<Func<TEntity, object>>> includes = null,
            Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> includeInCore = null,
            int? page = null, int? pageSize = null)
        {
            return (await GetPagedResultAsync(filter, orderBy, includes, page, pageSize, includeInCore)).Item2;
        }

        /// <summary>
        /// GetPagedResultAsync
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="orderBy"></param>
        /// <param name="Includes">This is old way of including entities in Ef. Should be removed in Ef Core</param>
        /// <param name="page"></param>
        /// <param name="pageSize"></param>
        /// /// <param name="includeInCore">This should be used in Ef Core moving fwd for including entities </param>
        /// <returns></returns>
        public virtual async Task<Tuple<int, IEnumerable<TEntity>>> GetPagedResultAsync(Expression<Func<TEntity, bool>> filter = null,
            Func<IQueryable<TEntity>,
            IOrderedQueryable<TEntity>> orderBy = null,
            List<Expression<Func<TEntity, object>>> includes = null,
            int? page = null,
            int? pageSize = null,
            Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> includeInCore = null)
        {
            IQueryable<TEntity> query = this.DefaultUnOrderedListQuery;
            int totalCount = 0;

            if (includes != null && includes.Count > 0)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }

            if (includeInCore != null)
            {
                query = includeInCore(query);
            }

            if (orderBy != null)
            {
                query = orderBy(query);
            }
            else
            {
                query = query.OrderBy(x => x.Id);
            }

            if (filter != null)
            {
                query = query.Where(filter);
            }
            totalCount = await query.CountAsync();

            if (page != null)
            {
                query = query.Skip(((int)page - 1) * (int)pageSize);
            }

            if (pageSize != null)
            {
                query = query.Take((int)pageSize);
            }

            var data = await query.ToListAsync();
            return new Tuple<int, IEnumerable<TEntity>>(totalCount, data);
        }
    }
}
