using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StationeryShop.Models
{
    [Table("Products")]
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProductID { get; set; }

        [Required(ErrorMessage = "Поле Название обязательно для заполнения")]
        [Display(Name = "Название товара")]
        [StringLength(100, ErrorMessage = "Название не должно превышать 100 символов")]
        public string Name { get; set; }

        [Display(Name = "Описание")]
        [StringLength(500, ErrorMessage = "Описание не должно превышать 500 символов")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Поле Цена обязательно для заполнения")]
        [Display(Name = "Цена")]
        [Range(0.01, 100000, ErrorMessage = "Цена должна быть в диапазоне от 0.01 до 100000")]
        public decimal Price { get; set; }

        [Display(Name = "Себестоимость (цена закупки)")]
        [Range(0.01, 100000, ErrorMessage = "Себестоимость должна быть в диапазоне от 0.01 до 100000")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchaseCost { get; set; }

        [Display(Name = "Количество на складе")]
        [Range(0, 10000, ErrorMessage = "Количество должно быть в диапазоне от 0 до 10000")]
        public int StockQuantity { get; set; }

        [Display(Name = "Фото")]
        [Column(TypeName = "varbinary(max)")]
        [DataType(DataType.Upload)]
        [MaxLength(10485760)]
        public byte[]? Photo { get; set; }

        [NotMapped]
        public string? PhotoBase64 => Photo != null ? System.Convert.ToBase64String(Photo) : null;

        [Required(ErrorMessage = "Поле Категория обязательно для заполнения")]
        [ForeignKey("Category")]
        public int CategoryID { get; set; }
        public virtual Category? Category { get; set; }

        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}