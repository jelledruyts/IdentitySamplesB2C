namespace Sample.Client.AspNetCore.Models
{
    public class AccountInvitationViewModel
    {
        public bool CanInviteUsingClientAssertion { get; set; }
        public bool CanInviteUsingInvitationCode { get; set; }
        public string Email { get; set; }
        public string CompanyId { get; set; }
        public string InvitationCode { get; set; }
        public string AuthenticationRequestUrl { get; set; }
    }
}