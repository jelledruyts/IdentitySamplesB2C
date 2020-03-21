using System;
using System.Collections.Generic;

namespace Sample.Api.AspNetCore.Models
{
    /// <summary>
    /// Represents information about an identity as seen from an application.
    /// </summary>
    public class IdentityInfo
    {
        /// <summary>
        /// The source of the identity information.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// The application from which the identity is observed.
        /// </summary>
        public string Application { get; set; }

        /// <summary>
        /// Determines if the identity is authenticated.
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// The authentication type.
        /// </summary>
        public string AuthenticationType { get; set; }

        /// <summary>
        /// The identity name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The claims.
        /// </summary>
        public IEnumerable<ClaimInfo> Claims { get; set; }

        /// <summary>
        /// The identities as seen from other applications related to the current application.
        /// </summary>
        public IList<IdentityInfo> RelatedApplicationIdentities { get; set; }

        public IdentityInfo()
        {
            this.Claims = Array.Empty<ClaimInfo>();
            this.RelatedApplicationIdentities = new List<IdentityInfo>();
        }
    }
}