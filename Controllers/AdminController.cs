using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StationeryShop.Data;
using StationeryShop.Models;

namespace StationeryShop.Controllers
{
    public class AdminController : Controller
    {
        private readonly StationeryDbContext _context;

        public AdminController(StationeryDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> LoginAttempts()
        {
            if (!IsAdmin())
                return RedirectToHome();

            var attempts = await _context.LoginAttempts
                .OrderByDescending(a => a.AttemptTime)
                .Take(100)
                .ToListAsync();

            return View(attempts);
        }
        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("IsAdmin") == "true";
        }

        private IActionResult RedirectToHome()
        {
            TempData["Error"] = "Доступ запрещен. Требуются права администратора.";
            return RedirectToAction("Index", "Home");
        }

        // Главная страница админки — статистика и обзор

        public IActionResult Analytics()
        {
            if (!IsAdmin()) return RedirectToHome();
            ViewData["Title"] = "Аналитика продаж";
            return View();
        }

        public IActionResult Settings()
        {
            if (!IsAdmin()) return RedirectToHome();
            ViewData["Title"] = "Настройки магазина";
            return View();
        }
        public IActionResult Index()
        {
            if (!IsAdmin())
                return RedirectToHome();

            // Статистика для дашборда
            var totalProducts = _context.Products.Count();
            var totalCategories = _context.Categories.Count();
            var totalCustomers = _context.Customers.Count();
            var totalOrders = _context.Orders.Count();
            var totalRevenue = _context.Orders.Sum(o => o.TotalAmount);

            // Последние заказы
            var recentOrders = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .ToList();

            // Товары с низким запасом
            var lowStockProducts = _context.Products
                .Where(p => p.StockQuantity < 10)
                .OrderBy(p => p.StockQuantity)
                .Take(10)
                .ToList();

            ViewBag.TotalProducts = totalProducts;
            ViewBag.TotalCategories = totalCategories;
            ViewBag.TotalCustomers = totalCustomers;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.RecentOrders = recentOrders;
            ViewBag.LowStockProducts = lowStockProducts;

            return View();
        }

        // Управление товарами
        public IActionResult Products()
        {
            if (!IsAdmin())
                return RedirectToHome();

            var products = _context.Products
                .Include(p => p.Category)
                .ToList();
            ViewBag.Categories = _context.Categories.ToList();
            return View();

            return View(products);
        }

        // Управление категориями
        public IActionResult Categories()
        {
            if (!IsAdmin())
                return RedirectToHome();

            var categories = _context.Categories
                .Include(c => c.Products)
                .ToList();

            return View(categories);
        }

        // Управление заказами
        public IActionResult Orders()
        {
            if (!IsAdmin())
                return RedirectToHome();

            var orders = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            return View(orders);
        }

        // Управление клиентами
        public IActionResult Customers()
        {
            if (!IsAdmin())
                return RedirectToHome();

            var customers = _context.Customers
                .Include(c => c.Orders)
                .ToList();

            return View(customers);
        }

        // Быстрое обновление количества товара
        [HttpPost]
        public async Task<IActionResult> UpdateProductStock(int productId, int newStock)
        {
            if (!IsAdmin())
                return Json(new { success = false, message = "Доступ запрещен" });

            try
            {
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                    return Json(new { success = false, message = "Товар не найден" });

                product.StockQuantity = newStock;
                _context.Products.Update(product);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Запас обновлен", newStock = product.StockQuantity });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Ошибка: {ex.Message}" });
            }
        }
        // GET: Admin/GetProducts (AJAX) поиск/фильтрация
        [HttpGet]
        public async Task<IActionResult> GetProducts(string search = "", int? categoryId = null, string sortBy = "Name", bool ascending = true)
        {
            if (!IsAdmin()) return Unauthorized();

            var query = _context.Products
                .Include(p => p.Category)
                .AsQueryable();

            // Поиск по названию
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Name.Contains(search));
            }

            // Фильтр по категории
            if (categoryId.HasValue && categoryId > 0)
            {
                query = query.Where(p => p.CategoryID == categoryId);
            }

            // Сортировка
            query = sortBy switch
            {
                "Price" => ascending ? query.OrderBy(p => p.Price) : query.OrderByDescending(p => p.Price),
                "Stock" => ascending ? query.OrderBy(p => p.StockQuantity) : query.OrderByDescending(p => p.StockQuantity),
                "Category" => ascending ? query.OrderBy(p => p.Category.Name) : query.OrderByDescending(p => p.Category.Name),
                _ => ascending ? query.OrderBy(p => p.Name) : query.OrderByDescending(p => p.Name)
            };

            var products = await query.ToListAsync();

            return Json(products.Select(p => new
            {
                p.ProductID,
                p.Name,
                p.Description,
                p.Price,
                p.StockQuantity,
                CategoryName = p.Category?.Name,
                p.CategoryID,
                PhotoBase64 = p.PhotoBase64
            }));
        }

        // GET: Admin/GetCategories
        [HttpGet]
        public async Task<IActionResult> GetCategories(string search = "", string sortBy = "Name", bool ascending = true)
        {
            if (!IsAdmin()) return Unauthorized();

            var query = _context.Categories
                .Include(c => c.Products)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Name.Contains(search));
            }

            query = sortBy switch
            {
                "NameDesc" => query.OrderByDescending(c => c.Name),
                "Products" => query.OrderBy(c => c.Products.Count),
                "ProductsDesc" => query.OrderByDescending(c => c.Products.Count),
                _ => ascending ? query.OrderBy(c => c.Name) : query.OrderByDescending(c => c.Name)
            };

            var categories = await query.ToListAsync();

            return Json(categories.Select(c => new
            {
                c.CategoryID,
                c.Name,
                c.Description,
                ProductsCount = c.Products.Count
            }));
        }

        // GET: Admin/GetOrders
        [HttpGet]
        public async Task<IActionResult> GetOrders(
            string search = "",
            string status = "",
            DateTime? startDate = null,
            DateTime? endDate = null,
            string sortBy = "Date",
            bool ascending = false)
        {
            if (!IsAdmin()) return Unauthorized();

            var query = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .AsQueryable();

            // Фильтр по поиску (номер заказа или имя клиента)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(o =>
                    o.OrderID.ToString().Contains(search) ||
                    (o.Customer != null && o.Customer.FullName.Contains(search)) ||
                    (o.Customer != null && o.Customer.Email.Contains(search)));
            }

            // Фильтр по статусу
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
            }

            // Фильтр по дате
            if (startDate.HasValue)
            {
                query = query.Where(o => o.OrderDate >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                var end = endDate.Value.AddDays(1);
                query = query.Where(o => o.OrderDate < end);
            }

            // Сортировка
            query = sortBy switch
            {
                "Id" => ascending ? query.OrderBy(o => o.OrderID) : query.OrderByDescending(o => o.OrderID),
                "Customer" => ascending ? query.OrderBy(o => o.Customer.FullName) : query.OrderByDescending(o => o.Customer.FullName),
                "Amount" => ascending ? query.OrderBy(o => o.TotalAmount) : query.OrderByDescending(o => o.TotalAmount),
                "Status" => ascending ? query.OrderBy(o => o.Status) : query.OrderByDescending(o => o.Status),
                _ => ascending ? query.OrderBy(o => o.OrderDate) : query.OrderByDescending(o => o.OrderDate)
            };

            var orders = await query.ToListAsync();

            return Json(orders.Select(o => new
            {
                o.OrderID,
                o.OrderDate,
                o.TotalAmount,
                o.Status,
                o.ShippingAddress,
                CustomerName = o.Customer?.FullName ?? "Не указан",
                CustomerEmail = o.Customer?.Email ?? "",
                CustomerPhone = o.Customer?.Phone ?? "",
                ItemsCount = o.OrderItems.Sum(i => i.Quantity)
            }));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus([FromBody] UpdateOrderStatusRequest request)
        {
            if (!IsAdmin())
                return Json(new { success = false, message = "Доступ запрещен" });

            try
            {
                var order = await _context.Orders.FindAsync(request.OrderId);
                if (order == null)
                    return Json(new { success = false, message = "Заказ не найден" });

                order.Status = request.Status;
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Статус обновлен" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Ошибка: {ex.Message}" });
            }
        }

        // Вспомогательный класс для приёма данных
        public class UpdateOrderStatusRequest
        {
            public int OrderId { get; set; }
            public string Status { get; set; }
        }
        //// POST: Admin/UpdateStock (быстрое обновление остатка)
        //[HttpPost]
        //public async Task<IActionResult> UpdateStock([FromBody] UpdateStockRequest request)
        //{
        //    if (!IsAdmin()) return Unauthorized();

        //    var product = await _context.Products.FindAsync(request.ProductId);
        //    if (product == null) return NotFound(new { success = false, message = "Товар не найден" });

        //    product.StockQuantity = request.NewStock;
        //    await _context.SaveChangesAsync();

        //    return Ok(new { success = true, message = "Остаток обновлён" });
        //}


        //public class UpdateStockRequest
        //{
        //    public int ProductId { get; set; }
        //    public int NewStock { get; set; }
        //}
    }
}