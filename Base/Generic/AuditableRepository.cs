using Microsoft.EntityFrameworkCore;
using Recipe.NetCore.Base.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Recipe.NetCore.Base.Generic
{
    public class AuditableRepository<TEntity, TKey, TDbContext> : Repository<TEntity, TKey, TDbContext>, IAuditableRepository<TEntity, TKey>
            where TEntity : class, IAuditModel<TKey>
            where TKey : IEquatable<TKey>
            where TDbContext : DbContext
    {
        public AuditableRepository(IRequestInfo<TDbContext> requestInfo)
            : base(requestInfo)
        {
        }

        protected override IQueryable<TEntity> DefaultListQuery => base.DefaultListQuery.Where(x => !x.IsDeleted);

        protected override IQueryable<TEntity> DefaultUnOrderedListQuery => base.DefaultUnOrderedListQuery.Where(x => !x.IsDeleted);

        protected override IQueryable<TEntity> DefaultSingleQuery => base.DefaultSingleQuery.Where(x => !x.IsDeleted);

        public override Task<TEntity> Create(TEntity entity)
        {
            entity.CreatedBy = RequestInfo.UserId;
            entity.CreatedOn = DateTime.UtcNow;
            entity.LastModifiedBy = RequestInfo.UserId;
            entity.LastModifiedOn = DateTime.UtcNow;
            entity.IsDeleted = false;
            return base.Create(entity);
        }

        public override Task<TEntity> Add(TEntity entity)
        {
            entity.CreatedBy = RequestInfo.UserId;
            entity.CreatedOn = DateTime.UtcNow;
            entity.LastModifiedBy = RequestInfo.UserId;
            entity.LastModifiedOn = DateTime.UtcNow;
            entity.IsDeleted = false;
            return base.Add(entity);
        }

        public override Task<TEntity> Update(TEntity entity)
        {
            entity.LastModifiedOn = DateTime.UtcNow;
            entity.LastModifiedBy = RequestInfo.UserId;
            return base.Update(entity);
        }

        public override async Task DeleteAsync(TKey id)
        {
            var entity = await GetAsync(id);
            if (entity != null)
            {
                entity.LastModifiedOn = DateTime.UtcNow;
                entity.LastModifiedBy = RequestInfo.UserId;
                entity.IsDeleted = true;
                await base.Update(entity);
            }
        }

        public virtual async Task HardDeleteAsync(TKey id)
        {
            await base.DeleteAsync(id);
        }

        public virtual async Task HardDeleteRangeAsync<TEntityList>(TEntityList entityList) where TEntityList : IQueryable
        {
            await base.DeleteRange(entityList);
        }


        protected void UpdateChildrenWithoutLog<TChildEntity>(ICollection<TChildEntity> childEntities) where TChildEntity : class, IBase<TKey>
        {
            foreach (var entity in childEntities)
            {
                UpdateChildrenWithOutLog(entity);
            }
        }

        public virtual void UpdateChildrenWithOutLog<TChildEntity>(TChildEntity childEntity) where TChildEntity : class, IBase<TKey>
        {
            if (childEntity.Id is long && (Convert.ToInt64(childEntity.Id)) > 0)
            {
                var localEntity = DbContext.Set<TChildEntity>().Local.FirstOrDefault(x => x.Id.Equals(childEntity.Id));
                if (localEntity != null)
                {

                    DbContext.Entry(localEntity).State = EntityState.Detached;

                    DbContext.Entry(childEntity).State = EntityState.Modified;
                }
            }
            else
            {
                DbContext.Entry(childEntity).State = EntityState.Added;
            }
        }
    }
}
