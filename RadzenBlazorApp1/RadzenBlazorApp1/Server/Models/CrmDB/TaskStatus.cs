using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadzenBlazorApp1.Server.Models.CrmDB
{
    [Table("TaskStatuses")]
    public partial class CrmTaskStatus
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Name { get; set; }

        public ICollection<CrmTask> Tasks { get; set; }
    }
}