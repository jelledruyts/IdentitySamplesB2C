namespace Sample.Api.AspNetCore22
{
    public static class Constants
    {
        public static class ClaimTypes
        {
            public const string Scope = "scp";
        }
        
        public static class AuthorizationPolicies
        {
            public const string Baseline = nameof(Baseline);
            public const string ReadIdentity = nameof(ReadIdentity);
            public const string ReadWriteIdentity = nameof(ReadWriteIdentity);
        }

        public static class Scopes
        {
            public static string IdentityRead { get; set; }
            public static string IdentityReadWrite { get; set; }
        }
    }
}