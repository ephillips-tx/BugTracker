#nullable disable
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BugTracker.Models
{
    public class Invite
    {
        public int Id { get; set; }

        [DisplayName("Date Sent")]
        public DateTime InviteDate { get; set; }

        [DisplayName("Join Date")]
        public DateTime JoinDate { get; set; }

        [DisplayName("Code")]
        public Guid CompanyToken { get; set; }

        [DisplayName("Company")]
        public int CompanyId { get; set; }

        [DisplayName("Project")]
        public int ProjectId { get; set; }

        [DisplayName("Invitor")]
        public string InvitorId { get; set; }

        [DisplayName("Invitee")]
        public string InviteeId { get; set; }

        [DisplayName("Email")]
        [EmailAddress]
        [StringLength(254, ErrorMessage = "The {0} value cannot exceed {1} characters. ")]
        public string InviteeEmail { get; set; }

        [DisplayName("First Name")]
        public string InviteeFirstName { get; set; }

        [DisplayName("Last Name")]
        public string InviteeLastName { get; set; }

        [DisplayName("Message")]
        [StringLength(10000, ErrorMessage = "The {0} value cannot exceed {1} characters. ")]
        public string Message { get; set; }

        public bool IsValid { get; set; }

        // Navigation Properties
        public virtual Company Company { get; set; }
        public virtual BTUser Invitor { get; set; }
        public virtual BTUser Invitee { get; set; }
        public virtual Project Project { get; set; }
    }
}
