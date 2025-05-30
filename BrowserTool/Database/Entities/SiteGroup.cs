using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BrowserTool.Database.Entities
{
    [Table("SiteGroups")]
    public class SiteGroup
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        public int SortOrder { get; set; }

        public bool IsEnabled { get; set; }

        public DateTime CreateTime { get; set; }

        public DateTime UpdateTime { get; set; }

        // Navigation property
        public virtual ICollection<SiteItem> Sites { get; set; } = new List<SiteItem>();
    }
} 