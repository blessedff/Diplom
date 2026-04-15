using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StationeryShop.Models
{
    [Table("Customers")]
    public class Customer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CustomerID { get; set; }

        [Required(ErrorMessage = "Поле ФИО обязательно для заполнения")]
        [Display(Name = "ФИО")]
        [StringLength(100, ErrorMessage = "ФИО не должно превышать 100 символов")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Поле Email обязательно для заполнения")]
        [EmailAddress(ErrorMessage = "Некорректный формат Email")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Поле Пароль обязательно для заполнения")]
        [Display(Name = "Пароль")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Пароль должен быть от 8 символов")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Поле Телефон обязательно для заполнения")]
        [Display(Name = "Телефон")]
        [Phone(ErrorMessage = "Некорректный формат телефона")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Поле Адрес обязательно для заполнения")]
        [Display(Name = "Адрес")]
        [StringLength(200, ErrorMessage = "Адрес не должен превышать 200 символов")]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "Администратор")]
        public bool IsAdmin { get; set; } = false;

        [NotMapped]
        public string? RecaptchaToken { get; set; }

        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}