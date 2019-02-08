using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Recipe.NetCore.Base.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using TMH.Common.Helper;

namespace Recipe.NetCore.Base.Generic
{
    public class Repository<TEntity, TKey> : IRepository<TEntity, TKey>
       where TEntity : class, IBase<TKey>
       where TKey : IEquatable<TKey>
    {
        protected IRequestInfo RequestInfo { get; private set; }

        protected DbContext DBContext
        {
            get { return this.RequestInfo.Context; }
        }

        protected DbSet<TEntity> DbSet
        {
            get
            {
                return this.DBContext.Set<TEntity>();
            }
        }

        protected virtual IQueryable<TEntity> DefaultListQuery
        {
            get
            {
                return this.DBContext.Set<TEntity>().AsQueryable().OrderBy(x => x.Id);
            }
        }

        protected virtual IQueryable<TEntity> DefaultUnOrderedListQuery
        {
            get
            {
                return this.DBContext.Set<TEntity>().AsQueryable();
            }
        }

        protected virtual IQueryable<TEntity> DefaultSingleQuery
        {
            get
            {
                return this.DBContext.Set<TEntity>().AsQueryable();
            }
        }

        public Repository(IRequestInfo requestInfo)
        {
            this.RequestInfo = requestInfo;
        }

        public virtual async Task<TEntity> GetAsync(TKey id)
        {
            return await this.DefaultSingleQuery.SingleOrDefaultAsync(x => x.Id.Equals(id));
        }

        public virtual async Task<IEnumerable<TEntity>> GetAsync(IList<TKey> ids)
        {
            return await this.DefaultSingleQuery.Where(x => ids.Contains(x.Id)).ToListAsync();
        }

        public virtual async Task<int> GetCount()
        {
            return await this.DefaultListQuery.CountAsync();
        }

        public virtual async Task<IEnumerable<TEntity>> GetAll(JSONAPIRequest request)
        {
            return await this.DefaultListQuery.GenerateQuery(request).ToListAsync();
        }

        public virtual async Task<TEntity> Add(TEntity entity)    ////Reason for creating this function : when using Create function it was not able to perform cascade functionality for child entities. 
        {
            this.DBContext.Add(entity);
            return entity;
        }
        public virtual async Task<TEntity> Create(TEntity entity)
        {
            this.DBContext.Entry(entity).State = EntityState.Added;
            return entity;
        }

        public virtual async Task<TEntity> Update(TEntity entity)
        {
            var localEntity = this.GetExisting(entity);
            if (localEntity != null && !this.RemoveExistingEntity(localEntity))
            {
                throw new InvalidOperationException("Unexpected Error Occured");
            }
            this.DBContext.Entry(entity).State = EntityState.Modified;
            return entity;
        }

        public virtual async Task DeleteAsync(TKey id)
        {
            var entity = await this.GetAsync(id);
            this.DBContext.Entry(entity).State = EntityState.Deleted;
        }

        public virtual async Task DeleteRange<TEntityList>(TEntityList entityList) where TEntityList : IQueryable
        {
            foreach (var each in entityList)
            {
                this.DBContext.Entry(each).State = EntityState.Deleted;
            }
        }

        public virtual async Task<TEntity> GetEntityOnly(TKey id)
        {
            return await this.DBContext.Set<TEntity>().AsQueryable().SingleOrDefaultAsync(x => x.Id.Equals(id));
        }

        //Use includesInCore param in Ef Core when Includes within Includes needed
        public virtual async Task<TEntity> Find(Expression<Func<TEntity, bool>> filter, List<Expression<Func<TEntity, object>>> includes = null, Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> includesInCore = null)
        {
            IQueryable<TEntity> query = this.DefaultUnOrderedListQuery;
            query = Queryable.Where(query, filter);

            if (includes != null)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }
            else if(includesInCore != null)
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

        private TEntity GetExisting(TEntity entity)
        {
            return this.DBContext.Set<TEntity>().Local.FirstOrDefault(x => x.Id.Equals(entity.Id));
        }

        private bool RemoveExistingEntity(TEntity entity)
        {
            return this.DBContext.Set<TEntity>().Local.Remove(entity);
        }

        public virtual IQueryFilter<TEntity> Query(Expression<Func<TEntity, bool>> queryExpression)
        {
            return new QueryFilter<TEntity, TKey>(this, queryExpression);
        }

        internal async Task<IEnumerable<TEntity>> SelectAsync(Expression<Func<TEntity, bool>> filter = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
            List<Expression<Func<TEntity, object>>> includes = null,
            Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> includeInCore = null,
            int? page = null, int? pageSize = null)
        {
            return (await GetPagedResultAsync(filter, orderBy, includes,  page, pageSize, includeInCore)).Item2;
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
