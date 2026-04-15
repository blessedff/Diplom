using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StationeryShop.Data;
using StationeryShop.Models;
using StationeryShop.Services;

namespace StationeryShop.Controllers
{
    public class AdminController : Controller
    {
        private readonly StationeryDbContext _context;
        private readonly EmailService _emailService;

        public AdminController(StationeryDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
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
                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .FirstOrDefaultAsync(o => o.OrderID == request.OrderId);

                if (order == null)
                    return Json(new { success = false, message = "Заказ не найден" });

                string oldStatus = order.Status;
                string newStatus = request.Status;

                order.Status = newStatus;
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();

                string message = $"Статус заказа #{order.OrderID} изменён с '{oldStatus}' на '{newStatus}'";

                // Отправляем email уведомление ТОЛЬКО если новый статус = "Готов к выдаче"
                if (newStatus == "Готов к выдаче" && order.Customer != null && !string.IsNullOrEmpty(order.Customer.Email))
                {
                    bool emailSent = await _emailService.SendOrderReadyNotification(
                        order.Customer.Email,
                        order.Customer.FullName,
                        order.OrderID
                    );

                    if (emailSent)
                    {
                        message += $". Уведомление о готовности отправлено на {order.Customer.Email}";
                    }
                    else
                    {
                        message += ". Не удалось отправить уведомление (ошибка почты)";
                    }
                }

                return Json(new { success = true, message = message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Ошибка: {ex.Message}" });
            }
        }

        // Класс для получения данных из JavaScript
        public class UpdateOrderStatusRequest
        {
            public int OrderId { get; set; }
            public string Status { get; set; }
        }

        //Вспомогательный метод для получения числового значения из настроек
        private decimal GetDecimalSetting(Dictionary<string, string> settings, string key, decimal defaultValue)
        {
            if (settings.ContainsKey(key) && decimal.TryParse(settings[key], out var value))
                return value;
            return defaultValue;
        }
        // GET: Admin/Finance
        public async Task<IActionResult> Finance(DateTime? startDate, DateTime? endDate)
        {
            if (!IsAdmin()) return RedirectToHome();

            // Устанавливаем период (по умолчанию текущий месяц)
            var start = startDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var end = endDate ?? DateTime.Now;

            // Получаем все выполненные заказы за период
            var completedOrders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(o => o.Status == "Выполнен" && o.OrderDate >= start && o.OrderDate <= end)
                .ToListAsync();

            // Получаем разовые расходы за период
            var expenses = await _context.Expenses
                .Where(e => e.ExpenseDate >= start && e.ExpenseDate <= end)
                .ToListAsync();

            // Получаем настройки из базы данных
            var settingsList = await _context.FinancialSettings.ToListAsync();
            var settings = settingsList.ToDictionary(s => s.SettingKey, s => s.SettingValue);

            // Читаем настройки
            var taxRate = GetDecimalSetting(settings, "TaxRate", 5.0m);
            var salaryTotal = GetDecimalSetting(settings, "SalaryTotal", 0m);
            var socialTaxRate = GetDecimalSetting(settings, "SocialTaxRate", 34m);
            var warehouseRent = GetDecimalSetting(settings, "WarehouseRent", 0m);
            var pickupPointRent = GetDecimalSetting(settings, "PickupPointRent", 0m);
            var logisticsToWarehouse = GetDecimalSetting(settings, "LogisticsToWarehouse", 0m);
            var logisticsToPickup = GetDecimalSetting(settings, "LogisticsToPickup", 0m);
            var advertising = GetDecimalSetting(settings, "Advertising", 0m);
            var packagingPerOrder = GetDecimalSetting(settings, "PackagingPerOrder", 1.5m);
            var bankService = GetDecimalSetting(settings, "BankService", 0m);
            var hosting = GetDecimalSetting(settings, "Hosting", 0m);
            var utilities = GetDecimalSetting(settings, "Utilities", 0m);
            var officeExpenses = GetDecimalSetting(settings, "OfficeExpenses", 0m);

            // Расчёт выручки
            var totalRevenue = completedOrders.Sum(o => o.TotalAmount);

            // Расчёт себестоимости (нужно добавить поле PurchaseCost в Product)
            // Пока используем 50% от выручки как пример
            var costOfGoods = totalRevenue * 0.5m;

            // Расчёт количества заказов и упаковки
            var totalOrdersCount = completedOrders.Count;
            var packagingCost = totalOrdersCount * packagingPerOrder;

            // Расчёт отчислений ФСЗН (34% от зарплаты)
            var socialTax = salaryTotal * (socialTaxRate / 100);

            // Суммируем разовые расходы
            var otherExpenses = expenses.Sum(e => e.Amount);

            // Формируем отчёт
            var report = new FinancialReport
            {
                StartDate = start,
                EndDate = end,
                TotalRevenue = totalRevenue,
                TotalRevenueCash = 0,      // Не используем
                TotalRevenueCard = 0,      // Не используем
                CostOfGoods = costOfGoods,
                LogisticsToWarehouse = logisticsToWarehouse,
                WarehouseRent = warehouseRent,
                PickupPointRent = pickupPointRent,
                LogisticsToPickup = logisticsToPickup,
                SalaryTotal = salaryTotal,
                SocialTax = socialTax,
                Advertising = advertising,
                Packaging = packagingCost,
                AcquiringFee = 0,           // Не используем
                BankService = bankService,
                Hosting = hosting,
                Utilities = utilities,
                OfficeExpenses = officeExpenses,
                OtherExpenses = otherExpenses,
                TaxRate = taxRate
            };

            // Итоговые расчёты
            report.TotalExpenses = report.CostOfGoods + report.LogisticsToWarehouse + report.WarehouseRent +
                                   report.PickupPointRent + report.LogisticsToPickup + report.SalaryTotal +
                                   report.SocialTax + report.Advertising + report.Packaging + report.AcquiringFee +
                                   report.BankService + report.Hosting + report.Utilities + report.OfficeExpenses +
                                   report.OtherExpenses;

            report.TaxAmount = report.TotalRevenue * (taxRate / 100);
            report.NetProfit = report.TotalRevenue - report.TotalExpenses - report.TaxAmount;
            report.ProfitMargin = report.TotalRevenue > 0 ? (report.NetProfit / report.TotalRevenue) * 100 : 0;

            ViewBag.Report = report;
            ViewBag.Expenses = expenses;

            return View();
        }
    }

}