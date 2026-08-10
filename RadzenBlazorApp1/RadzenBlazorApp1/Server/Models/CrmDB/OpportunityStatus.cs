using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadzenBlazorApp1.Server.Models.CrmDB
{
    [Table("OpportunityStatuses")]
    public partial class OpportunityStatus
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Name { get; set; }

        public ICollection<Opportunity> Opportunities { get; set; }
    }
}