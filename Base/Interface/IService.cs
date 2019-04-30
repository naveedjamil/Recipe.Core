using Microsoft.EntityFrameworkCore;
using Recipe.NetCore.Attribute;
using Recipe.NetCore.Base.Abstract;
using Recipe.NetCore.Enum;
using Recipe.NetCore.Helper;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Recipe.NetCore.Base.Interface
{
    public interface IService<out TDbContext> where TDbContext : DbContext
    {
        IUnitOfWork<TDbContext> UnitOfWork { get; }
    }

    public interface IService<TDTO, TKey> : IService<DbContext>
    {
        [AuditOperationAttribute(OperationType.Read)]
        Task<IEnumerable<TKey>> GetIds();

        [AuditOperationAttribute(OperationType.Read)]
        Task<DataTransferObject<TDTO>> GetAsync(TKey id);

        [AuditOperationAttribute(OperationType.Read)]
        Task<int> GetCount();

        [AuditOperationAttribute(OperationType.Read)]
        Task<DataTransferObject<IList<TDTO>>> GetAllAsync(JsonapiRequest request);

        [AuditOperationAttribute(OperationType.Create)]
        Task<DataTransferObject<TDTO>> CreateAsync(TDTO dtoObject);

        [AuditOperationAttribute(OperationType.Update)]
        Task<DataTransferObject<TDTO>> UpdateAsync(TDTO dtoObject);

        [AuditOperationAttribute(OperationType.Delete)]
        Task DeleteAsync(TKey id);

        [AuditOperationAttribute(OperationType.Create)]
        Task<IList<TDTO>> CreateAsync(IList<TDTO> dtoObjects);

        [AuditOperationAttribute(OperationType.Delete)]
        Task DeleteAsync(IList<TKey> ids);

        [AuditOperationAttribute(OperationType.Update)]
        Task<IList<TDTO>> UpdateAsync(IList<TDTO> dtoObjects);

        [AuditOperationAttribute(OperationType.Delete)]
        Task BulkDelete(IList<TKey> ids);
    }

    public interface IService<TRepository, TEntity, TDTO, TKey> : IService<TDTO, TKey>
    {
        TRepository Repository { get; }
        Task<DataTransferObject<List<TDTO>>> GetAllAsync(DataTransferObject<TEntity> entity);

        Task<DataTransferObject<List<TDTO>>> GetAllPagedAsync(DataTransferObject<TEntity> model);
    }
}

