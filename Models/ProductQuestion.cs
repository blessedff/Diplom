using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StationeryShop.Models
{
    [Table("ProductQuestions")]
    public class ProductQuestion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [ForeignKey("Product")]
        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        [ForeignKey("Customer")]
        public int CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }

        [Required]
        [StringLength(1000)]
        [Display(Name = "Вопрос")]
        public string Question { get; set; } = string.Empty;

        [Display(Name = "Дата вопроса")]
        public DateTime QuestionDate { get; set; } = DateTime.Now;

        [StringLength(2000)]
        [Display(Name = "Ответ администратора")]
        public string? Answer { get; set; }

        [Display(Name = "Дата ответа")]
        public DateTime? AnswerDate { get; set; }

        [Display(Name = "Опубликован")]
        public bool IsPublished { get; set; } = false;

        [NotMapped]
        public string CustomerName => Customer?.FullName ?? "Пользователь";
    }
}