using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Recipe.NetCore.Infrastructure
{
    public class ApplicationContext
    {
        protected ApplicationContext() { }

        public static string GetHttpContextItem(IHttpContextAccessor httpContextManager, string keyName)
        {
            if (httpContextManager == null ||
                httpContextManager.HttpContext == null ||
                httpContextManager.HttpContext.User == null ||
                httpContextManager.HttpContext.User.Claims == null ||
                !httpContextManager.HttpContext.User.Claims.Any())
            {
                return null;
            }

            return httpContextManager.HttpContext.User.FindFirst(keyName).Value;
        }

        public static string GetHttpContextRequestItem(IHttpContextAccessor httpContextManager, string keyName)
        {
            if (httpContextManager == null ||
                httpContextManager.HttpContext == null ||
                httpContextManager.HttpContext.Request == null ||
                httpContextManager.HttpContext.Request.Query == null ||
                !httpContextManager.HttpContext.Request.Query.Any())
            {
                return null;
            }

            return httpContextManager.HttpContext.Request.Query[keyName].ToString();
        }

        public static string GetHttpContextHeaderItem(IHttpContextAccessor httpContextManager, string keyName)
        {
            if (httpContextManager == null ||
                httpContextManager.HttpContext == null ||
                httpContextManager.HttpContext.Request == null ||
                httpContextManager.HttpContext.Request.Headers == null ||
                !httpContextManager.HttpContext.Request.Headers.Any())
            {
                return null;
            }

            var headerValues = httpContextManager.HttpContext.Request.Headers[keyName];

            return !string.IsNullOrEmpty(headerValues) ? headerValues.ToString() : string.Empty;
        }
    }
}
