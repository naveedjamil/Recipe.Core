using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Recipe.NetCore.Base.Interface;
using System.Threading.Tasks;

namespace Recipe.NetCore.Base.Abstract
{
    public class UnitOfWork<TDbContext> : IUnitOfWork<TDbContext>
            where TDbContext : DbContext
    {
        private readonly IRequestInfo<TDbContext> _requestInfo;

        public UnitOfWork(IRequestInfo<TDbContext> requestInfo)
        {
            this._requestInfo = requestInfo;

        }

        public TDbContext DbContext => this._requestInfo.Context;

        public int Save() => this._requestInfo.Context.SaveChanges();

        public async Task<int> SaveAsync() => await this._requestInfo.Context.SaveChangesAsync();

        public IDbContextTransaction BeginTransaction() => this.DbContext.Database.BeginTransaction();
    }
}
