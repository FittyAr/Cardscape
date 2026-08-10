using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadzenBlazorApp1.Server.Models.CrmDB
{
    [Table("Tasks")]
    public partial class CrmTask
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Title { get; set; }

        [Required]
        public int OpportunityId { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [Required]
        public int TypeId { get; set; }

        public int? StatusId { get; set; }

        public Opportunity Opportunity { get; set; }

        public CrmTaskStatus TaskStatus { get; set; }

        public TaskType TaskType { get; set; }
    }
}