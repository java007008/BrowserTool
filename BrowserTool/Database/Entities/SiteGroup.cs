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

        /// <summary>
        /// 是否默认展开（用于控制一级菜单的默认展开状态）
        /// </summary>
        public bool IsDefaultExpanded { get; set; } = false;

        public DateTime CreateTime { get; set; }

        public DateTime UpdateTime { get; set; }

        // Navigation property
        public virtual ICollection<SiteItem> Sites { get; set; } = new List<SiteItem>();
    }
} 