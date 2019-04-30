namespace Recipe.NetCore.Constant
{
    public static class Constants
    {
        public static class Strings
        {
            public static class JwtClaimIdentifiers
            {
                public static readonly string Id = "id";
                public static readonly string Email = "email";
                public static readonly string Username = "username";
                public static readonly string TenantId = "tenantId";
                public static readonly string Role = "role";
            }

            public static class JwtClaims
            {
                public static readonly string ApiAccess = "api_access";
            }
        }
    }
}
