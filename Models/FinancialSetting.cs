using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StationeryShop.Models
{
    [Table("FinancialSettings")]
    public class FinancialSetting
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SettingId { get; set; }

        [Required]
        [StringLength(50)]
        public string SettingKey { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string SettingValue { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }
    }
}