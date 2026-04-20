using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StationeryShop.Data;
using StationeryShop.Models;

namespace StationeryShop.Controllers
{
    public class ReviewsController : Controller
    {
        private readonly StationeryDbContext _context;

        public ReviewsController(StationeryDbContext context)
        {
            _context = context;
        }

        private bool IsAuthenticated()
        {
            return HttpContext.Session.GetInt32("CustomerID") != null;
        }

        private int GetCustomerId()
        {
            return HttpContext.Session.GetInt32("CustomerID") ?? 0;
        }

        // GET: Reviews
        public IActionResult Index()
        {
            // Получаем только одобренные отзывы
            var reviews = _context.Reviews
                .Include(r => r.Customer)
                .Include(r => r.Product)
                .Where(r => r.IsApproved == true)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            // Общий рейтинг магазина (все одобренные отзывы)
            var averageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;

            ViewBag.AverageRating = averageRating;
            ViewBag.TotalReviews = reviews.Count;

            return View(reviews);
        }

        // POST: Reviews/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(int? productId, int rating, string comment)
        {
            if (!IsAuthenticated())
            {
                TempData["Error"] = "Для отправки отзыва необходимо авторизоваться";
                return RedirectToAction("Login", "Account");
            }

            if (rating < 1 || rating > 5)
            {
                TempData["Error"] = "Оценка должна быть от 1 до 5";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(comment))
            {
                TempData["Error"] = "Текст отзыва не может быть пустым";
                return RedirectToAction("Index");
            }

            var review = new Review
            {
                ProductId = productId,
                CustomerId = GetCustomerId(),
                Rating = rating,
                Comment = comment,
                IsApproved = false,  
                CreatedAt = DateTime.Now
            };

            _context.Reviews.Add(review);
            _context.SaveChanges();

            TempData["Success"] = "Спасибо за отзыв! Он будет опубликован после проверки модератором.";
            return RedirectToAction("Index");
        }

        // GET: Reviews/GetProducts (для выпадающего списка)
        public IActionResult GetProducts(string search = "")
        {
            var products = _context.Products
                .Where(p => string.IsNullOrEmpty(search) || p.Name.Contains(search))
                .OrderBy(p => p.Name)
                .Select(p => new { id = p.ProductID, name = p.Name })
                .Take(20)
                .ToList();

            return Json(products);
        }
    }
}