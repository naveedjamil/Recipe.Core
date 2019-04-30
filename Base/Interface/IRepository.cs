using Microsoft.EntityFrameworkCore.Query;
using Recipe.NetCore.Attribute;
using Recipe.NetCore.Enum;
using Recipe.NetCore.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Recipe.NetCore.Base.Interface
{
    public interface IRepository
    {
    }

    public interface IRepository<TEntity, TKey> : IRepository
    {
        [AuditOperationAttribute(OperationType.Read)]
        Task<TEntity> GetAsync(TKey id);

        [AuditOperationAttribute(OperationType.Read)]
        Task<IEnumerable<TEntity>> GetAsync(IList<TKey> ids);

        [AuditOperationAttribute(OperationType.Read)]
        Task<TEntity> GetEntityOnly(TKey id);

        [AuditOperationAttribute(OperationType.Read)]
        Task<int> GetCount();

        [AuditOperationAttribute(OperationType.Read)]
        Task<IEnumerable<TEntity>> GetAll(JsonapiRequest request);

        [AuditOperationAttribute(OperationType.Create)]
        Task<TEntity> Create(TEntity entity);

        [AuditOperationAttribute(OperationType.Create)]
        Task<TEntity> Add(TEntity entity);

        [AuditOperationAttribute(OperationType.Update)]
        Task<TEntity> Update(TEntity entity);

        [AuditOperationAttribute(OperationType.Delete)]
        Task DeleteAsync(TKey id);

        Task<Tuple<int, IEnumerable<TEntity>>> GetPagedResultAsync(Expression<Func<TEntity, bool>> filter = null,
         Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
         List<Expression<Func<TEntity, object>>> includes = null,
          int? page = null,
          int? pageSize = null,
          Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> includeInCore = null);

        Task<TEntity> Find(Expression<Func<TEntity, bool>> filter, List<Expression<Func<TEntity, object>>> includes = null, Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> includesInCore = null);

        Task<List<TEntity>> FindAll(Expression<Func<TEntity, bool>> filter, List<Expression<Func<TEntity, object>>> includes = null, Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> includesInCore = null);

        IQueryFilter<TEntity> Query(Expression<Func<TEntity, bool>> queryExpression);
    }
}
