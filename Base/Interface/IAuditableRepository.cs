using Recipe.NetCore.Attribute;
using Recipe.NetCore.Enum;
using System.Linq;
using System.Threading.Tasks;

namespace Recipe.NetCore.Base.Interface
{
    public interface IAuditableRepository<TEntity, TKey>: IRepository<TEntity, TKey>
    {
        [AuditOperationAttribute(OperationType.Delete)]
        Task HardDeleteAsync(TKey id);
        Task HardDeleteRangeAsync<TEntityList>(TEntityList entityList) where TEntityList : IQueryable;
    }
}
