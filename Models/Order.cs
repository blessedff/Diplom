using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StationeryShop.Models
{
    [Table("Orders")]
    public class Order
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OrderID { get; set; }

        [ForeignKey("Customer")]
        public int CustomerID { get; set; }

        [Required]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Общая стоимость обязательна")]
        [Display(Name = "Общая стоимость")]
        [Range(0.01, 1000000, ErrorMessage = "Стоимость должна быть больше 0")]
        public decimal TotalAmount { get; set; }

        [Required(ErrorMessage = "Статус заказа обязателен")]
        [Display(Name = "Статус заказа")]
        [StringLength(50)]
        public string Status { get; set; } = "Принят";

        [Required(ErrorMessage = "Адрес доставки обязателен")]
        [Display(Name = "Адрес доставки")]
        [StringLength(200, ErrorMessage = "Адрес не должен превышать 200 символов")]
        public string ShippingAddress { get; set; } = string.Empty;

        public virtual Customer? Customer { get; set; }
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}