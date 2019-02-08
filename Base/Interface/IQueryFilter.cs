using Microsoft.EntityFrameworkCore.Query;
using Recipe.NetCore.Base.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Recipe.NetCore.Base.Interface
{
    public interface IQueryFilter<TEntity> 
    {
        IQueryFilter<TEntity> OrderBy(Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderByExpression);

        IQueryFilter<TEntity> Include(Expression<Func<TEntity, object>> expression);

        IQueryFilter<TEntity> IncludeInCore(Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> expression);

        Task<Tuple<int, IEnumerable<TEntity>>> SelectPageAsync(Expression<Func<TEntity, bool>> filter = null,
            Func<IQueryable<TEntity>,  IOrderedQueryable<TEntity>> orderBy = null,
            List<Expression<Func<TEntity, object>>> includes = null, int? page = null, int? pageSize = null);

        IEnumerable<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> selector = null);

        IEnumerable<TEntity> Select();

        Task<IEnumerable<TEntity>> SelectAsync();
    }
}
