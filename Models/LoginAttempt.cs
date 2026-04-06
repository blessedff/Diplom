
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StationeryShop.Models
{
    [Table("LoginAttempts")]
    public class LoginAttempt
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public DateTime AttemptTime { get; set; } = DateTime.Now;

        [Required]
        [StringLength(20)]
        public string IpAddress { get; set; } = string.Empty;

        public bool IsSuccessful { get; set; } = false;

        [StringLength(50)]
        public string? UserAgent { get; set; }
    }
}