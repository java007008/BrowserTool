using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BrowserTool.Database.Entities
{
    [Table("SiteItems")]
    public class SiteItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int GroupId { get; set; }

        [Required]
        [MaxLength(200)]
        public string DisplayName { get; set; }

        [Required]
        [MaxLength(500)]
        public string Url { get; set; }

        public int SortOrder { get; set; }

        [MaxLength(200)]
        public string Username { get; set; }

        [MaxLength(200)]
        public string Password { get; set; }

        [MaxLength(200)]
        public string CommonUsername { get; set; }

        [MaxLength(200)]
        public string CommonPassword { get; set; }

        public bool UseCommonCredentials { get; set; }

        public bool AutoLogin { get; set; }

        public bool IsEnabled { get; set; }

        [MaxLength(1000)]
        public string Icon { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        [MaxLength(500)]
        public string Tags { get; set; }

        public DateTime CreateTime { get; set; }

        public DateTime UpdateTime { get; set; }

        public DateTime? LastAccessTime { get; set; }

        public int AccessCount { get; set; }

        [MaxLength(200)]
        public string UsernameSelector { get; set; }

        [MaxLength(200)]
        public string PasswordSelector { get; set; }

        [MaxLength(200)]
        public string CaptchaSelector { get; set; }

        [MaxLength(200)]
        public string LoginButtonSelector { get; set; }

        [MaxLength(500)]
        public string LoginPageFeature { get; set; }

        [MaxLength(200)]
        public string CaptchaValue { get; set; }

        public int CaptchaMode { get; set; }

        [MaxLength(200)]
        public string GoogleSecret { get; set; }

        // Navigation property
        [ForeignKey("GroupId")]
        public virtual SiteGroup Group { get; set; }
    }
} 