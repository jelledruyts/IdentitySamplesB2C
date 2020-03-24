namespace Sample.Api.AspNetCore
{
    public static class Constants
    {
        public static class ClaimTypes
        {
            public const string Scope = "scp";
            public const string Roles = "roles";
        }
        
        public static class AuthorizationPolicies
        {
            public const string ReadIdentity = nameof(ReadIdentity);
            public const string ReadWriteIdentity = nameof(ReadWriteIdentity);
        }

        public static class Scopes
        {
            public static string IdentityRead { get; set; }
            public static string IdentityReadWrite { get; set; }
        }

        public static class Roles
        {
            public static string IdentityReader { get; set; }
        }
    }
}