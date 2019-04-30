using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recipe.NetCore.Base.Interface;
using Recipe.NetCore.Constant;
using System;
using System.Linq;

namespace Recipe.NetCore.Infrastructure
{
    public class RequestInfo<TDbContext> : IRequestInfo<TDbContext> where TDbContext : DbContext
    {
        private readonly IServiceScope Scope;
        private readonly IHttpContextAccessor contextAccessor;

        public RequestInfo(IServiceProvider serviceProvider, IHttpContextAccessor _contextAccessor)
        {
            contextAccessor = _contextAccessor;
            Scope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        }

        public string Role => ApplicationContext.GetHttpContextHeaderItem(contextAccessor, Constants.Strings.JwtClaimIdentifiers.Role);

        public string UserName => ApplicationContext.GetHttpContextHeaderItem(contextAccessor, Constants.Strings.JwtClaimIdentifiers.Username);

        public long UserId => IsRequestContainHeaderItem(Constants.Strings.JwtClaimIdentifiers.Id) ? 0 : Convert.ToInt32(ApplicationContext.GetHttpContextHeaderItem(contextAccessor, Constants.Strings.JwtClaimIdentifiers.Id));

        public string Email => ApplicationContext.GetHttpContextHeaderItem(contextAccessor, Constants.Strings.JwtClaimIdentifiers.Email);

        public int TenantId => IsRequestContainHeaderItem(Constants.Strings.JwtClaimIdentifiers.TenantId) ? 1 : Convert.ToInt32(ApplicationContext.GetHttpContextHeaderItem(contextAccessor, Constants.Strings.JwtClaimIdentifiers.TenantId));

        public TDbContext Context => Scope.ServiceProvider.GetRequiredService<TDbContext>();

        private bool IsRequestContainHeaderItem(string key)
        {
            return (contextAccessor.HttpContext == null
                || contextAccessor.HttpContext.Request == null
                || contextAccessor.HttpContext.Request.Headers == null
                || !contextAccessor.HttpContext.Request.Headers.Any()
                || !contextAccessor.HttpContext.Request.Headers.ContainsKey($"{key}"));
        }
    }
}
