using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fametra.Api.Models
{
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;

        // Navigation
        public ICollection<Member> Members { get; set; } = new List<Member>();
    }
}
