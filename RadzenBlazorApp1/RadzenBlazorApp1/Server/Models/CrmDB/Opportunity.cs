using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadzenBlazorApp1.Server.Models.CrmDB
{
    [Table("Opportunities")]
    public partial class Opportunity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        public int ContactId { get; set; }

        [Required]
        public int StatusId { get; set; }

        [Required]
        public DateTime CloseDate { get; set; }

        public Contact Contact { get; set; }

        public OpportunityStatus OpportunityStatus { get; set; }

        public ICollection<CrmTask> Tasks { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }
    }
}