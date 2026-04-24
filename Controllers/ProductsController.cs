using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StationeryShop.Data;
using StationeryShop.Models;

namespace StationeryShop.Controllers
{
    public class ProductsController : Controller
    {
        private readonly StationeryDbContext _context;

        public ProductsController(StationeryDbContext context)
        {
            _context = context;
        }

        // GET: Products
        public IActionResult Index()
        {
            var products = _context.Products
                .Include(p => p.Category)
                .ToList();

            // Определяем, является ли пользователь администратором
            ViewBag.IsAdmin = IsAdmin();
            ViewBag.IsAuthenticated = HttpContext.Session.GetInt32("CustomerID") != null;

            return View(products);
        }
        // GET: Products/DetailsModal/5 - для модального окна
        public IActionResult DetailsModal(int? id)
        {
            if (id == null) return NotFound();

            var product = _context.Products
                .Include(p => p.Category)
                .FirstOrDefault(m => m.ProductID == id);

            if (product == null) return NotFound();

            return PartialView("_ProductModal", product);
        }

        // GET: Products/Details/5
        public IActionResult Details(int? id)
        {
            if (id == null) return NotFound();

            var product = _context.Products
                .Include(p => p.Category)
                .FirstOrDefault(m => m.ProductID == id);

            if (product == null) return NotFound();

            return View(product);
        }

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("IsAdmin") == "true";
        }

        // GET: Products/Create
        public IActionResult Create()
        {
            if (!IsAdmin()) return RedirectToAction("Index");

            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? photoFile)
        {
            if (!IsAdmin())
                return Forbid();

            if (ModelState.IsValid)
            {
                if (photoFile != null && photoFile.Length > 0)
                {
                    using var ms = new MemoryStream();
                    await photoFile.CopyToAsync(ms);
                    product.Photo = ms.ToArray();
                }

                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction("Products", "Admin");
            }

            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "Name", product.CategoryID);
            return View(product);
        }

        // GET: Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (!IsAdmin()) return RedirectToAction("Index");
            if (id == null) return NotFound();

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "Name", product.CategoryID);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, IFormFile? photoFile)
        {
            if (!IsAdmin())
                return Forbid();

            if (id != product.ProductID)
                return NotFound();

            if (photoFile != null && photoFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await photoFile.CopyToAsync(ms);
                product.Photo = ms.ToArray();
            }
            else
            {
                var existingProduct = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductID == id);
                product.Photo = existingProduct?.Photo;
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                    return RedirectToAction("Products", "Admin");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Products.Any(e => e.ProductID == product.ProductID))
                        return NotFound();
                    else
                        throw;
                }
            }

            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "Name", product.CategoryID);
            return View(product);
        }

        // GET: Products/Delete/5
        public IActionResult Delete(int? id)
        {
            if (!IsAdmin()) return RedirectToAction("Index");
            if (id == null) return NotFound();

            var product = _context.Products
                .Include(p => p.Category)
                .FirstOrDefault(m => m.ProductID == id);

            if (product == null) return NotFound();

            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Index");

            var product = _context.Products.Find(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                _context.SaveChanges();
            }

            return RedirectToAction("Products", "Admin");
        }

        // GET: Products/GetProductRating
        [HttpGet]
        public IActionResult GetProductRating(int productId)
        {
            var reviews = _context.Reviews
                .Where(r => r.ProductId == productId && r.IsApproved == true)
                .ToList();

            var averageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;
            var totalReviews = reviews.Count;

            return Json(new { averageRating = Math.Round(averageRating, 1), totalReviews = totalReviews });
        }

        // GET: Products/GetProductQuestions
        [HttpGet]
        public async Task<IActionResult> GetProductQuestions(int productId)
        {
            var questions = await _context.ProductQuestions
                .Include(q => q.Customer)
                .Where(q => q.ProductId == productId && q.IsPublished == true)
                .OrderByDescending(q => q.QuestionDate)
                .Select(q => new
                {
                    q.Id,
                    q.Question,
                    q.QuestionDate,
                    q.Answer,
                    q.AnswerDate,
                    CustomerName = q.Customer != null ? q.Customer.FullName : "Пользователь"
                })
                .ToListAsync();

            return Json(questions);
        }

        [HttpPost]
        public async Task<IActionResult> AddQuestion([FromBody] AddQuestionRequest request)
        {
            // Проверяем, авторизован ли пользователь
            var customerId = HttpContext.Session.GetInt32("CustomerID");
            if (customerId == null)
                return Json(new { success = false, message = "Необходимо авторизоваться" });

            // Проверяем, существует ли товар
            var product = await _context.Products.FindAsync(request.ProductId);
            if (product == null)
                return Json(new { success = false, message = "Товар не найден" });

            // Проверяем длину вопроса
            if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 1000)
                return Json(new { success = false, message = "Вопрос должен быть от 1 до 1000 символов" });

            // Создаём новый вопрос
            var question = new ProductQuestion
            {
                ProductId = request.ProductId,
                CustomerId = customerId.Value,
                Question = request.Question.Trim(),
                QuestionDate = DateTime.Now,
                IsPublished = false
            };

            _context.ProductQuestions.Add(question);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Вопрос отправлен на модерацию" });
        }

        public class AddQuestionRequest
        {
            public int ProductId { get; set; }
            public string Question { get; set; } = string.Empty;
        }
    }
}