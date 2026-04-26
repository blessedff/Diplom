using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StationeryShop.Data;
using StationeryShop.Models;
using StationeryShop.Services;
using System.Net;
using System.Net.Mail;

namespace StationeryShop.Controllers
{
    public class AdminController : Controller
    {
        private readonly StationeryDbContext _context;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;

        public AdminController(StationeryDbContext context, EmailService emailService, IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
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
        //public IActionResult Customers()
        //{
        //    if (!IsAdmin())
        //        return RedirectToHome();

        //    var customers = _context.Customers
        //        .Include(c => c.Orders)
        //        .ToList();

        //    return View(customers);
        //}

        // GET: Admin/Reviews
        public async Task<IActionResult> Reviews(string status = "all", string type = "all", string search = "")
        {
            if (!IsAdmin()) return RedirectToHome();

            var query = _context.Reviews
                .Include(r => r.Customer)
                .Include(r => r.Product)
                .AsQueryable();

            // Фильтр по статусу
            switch (status)
            {
                case "pending":
                    query = query.Where(r => r.IsApproved == false && r.IsRejected == false);
                    break;
                case "approved":
                    query = query.Where(r => r.IsApproved == true);
                    break;
                case "rejected":
                    query = query.Where(r => r.IsRejected == true);
                    break;
            }

            // Фильтр по типу
            if (type == "product")
                query = query.Where(r => r.ProductId != null);
            else if (type == "shop")
                query = query.Where(r => r.ProductId == null);

            // Поиск по email
            if (!string.IsNullOrEmpty(search))
                query = query.Where(r => r.Customer != null && r.Customer.Email.Contains(search));

            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Статистика для карточек
            ViewBag.PendingCount = await _context.Reviews.CountAsync(r => r.IsApproved == false && r.IsRejected == false);
            ViewBag.ApprovedCount = await _context.Reviews.CountAsync(r => r.IsApproved == true);
            ViewBag.RejectedCount = await _context.Reviews.CountAsync(r => r.IsRejected == true);
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentType = type;
            ViewBag.Search = search;

            return View(reviews);
        }

        // POST: Admin/ApproveReview
        [HttpPost]
        public async Task<IActionResult> ApproveReview(int id)
        {
            if (!IsAdmin()) return RedirectToHome();

            var review = await _context.Reviews.FindAsync(id);
            if (review != null)
            {
                review.IsApproved = true;
                review.IsRejected = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Отзыв одобрен!";
            }

            return RedirectToAction("Reviews");
        }

        // POST: Admin/RejectReview
        [HttpPost]
        public async Task<IActionResult> RejectReview(int id)
        {
            if (!IsAdmin()) return RedirectToHome();

            var review = await _context.Reviews.FindAsync(id);
            if (review != null)
            {
                review.IsApproved = false;
                review.IsRejected = true;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Отзыв отклонён!";
            }

            return RedirectToAction("Reviews");
        }

        // POST: Admin/DeleteReview
        [HttpPost]
        public async Task<IActionResult> DeleteReview(int id)
        {
            if (!IsAdmin()) return RedirectToHome();

            var review = await _context.Reviews.FindAsync(id);
            if (review != null)
            {
                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Отзыв удалён!";
            }

            return RedirectToAction("Reviews");
        }

        // POST: Admin/ReplyToReview
        [HttpPost]
        public async Task<IActionResult> ReplyToReview(int id, string reply)
        {
            if (!IsAdmin()) return RedirectToHome();

            var review = await _context.Reviews.FindAsync(id);
            if (review != null)
            {
                review.AdminResponse = reply;
                review.AdminResponseDate = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Ответ сохранён!";
            }

            return RedirectToAction("Reviews");
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

           
            DateTime start;
            DateTime end;

            if (startDate.HasValue && endDate.HasValue)
            {
                start = startDate.Value;
                end = endDate.Value.AddDays(1);
            }
            else
            {
                start = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                end = DateTime.Now.AddDays(1);
            }

            //количество дней в периоде
            var daysInPeriod = (end - start).Days;
            if (daysInPeriod == 0) daysInPeriod = 1;

            // Коэффициент пропорциональности (сколько месяцев в периоде)
            var daysInCurrentMonth = DateTime.DaysInMonth(start.Year, start.Month);
            var monthlyFactor = (decimal)daysInPeriod / daysInCurrentMonth;

            
            var periodTitle = $"С {start:dd.MM.yyyy} по {end.AddDays(-1):dd.MM.yyyy}";

            ViewBag.PeriodTitle = periodTitle;
            ViewBag.MonthlyFactor = monthlyFactor;
            ViewBag.StartDate = start.ToString("yyyy-MM-dd");
            ViewBag.EndDate = end.AddDays(-1).ToString("yyyy-MM-dd");

            
            var completedOrders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
                .Where(o => o.Status == "Выполнен" && o.OrderDate >= start && o.OrderDate <= end)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

           
            var expenses = await _context.Expenses
                .Where(e => e.ExpenseDate >= start && e.ExpenseDate <= end)
                .OrderByDescending(e => e.ExpenseDate)
                .ToListAsync();

            
            var settingsList = await _context.FinancialSettings.ToListAsync();
            var settings = settingsList.ToDictionary(s => s.SettingKey, s => s.SettingValue);

           
            var taxRate = GetDecimalSetting(settings, "TaxRate", 5m);
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

            
            var salaryTotalPeriod = salaryTotal * monthlyFactor;
            var socialTaxPeriod = salaryTotalPeriod * (socialTaxRate / 100);
            var warehouseRentPeriod = warehouseRent * monthlyFactor;
            var pickupPointRentPeriod = pickupPointRent * monthlyFactor;
            var logisticsToWarehousePeriod = logisticsToWarehouse * monthlyFactor;
            var logisticsToPickupPeriod = logisticsToPickup * monthlyFactor;
            var advertisingPeriod = advertising * monthlyFactor;
            var bankServicePeriod = bankService * monthlyFactor;
            var hostingPeriod = hosting * monthlyFactor;
            var utilitiesPeriod = utilities * monthlyFactor;
            var officeExpensesPeriod = officeExpenses * monthlyFactor;

            
            var totalRevenue = completedOrders.Sum(o => o.TotalAmount);
            var totalOrdersCount = completedOrders.Count;
            var totalItemsSold = completedOrders.Sum(o => o.OrderItems.Sum(oi => oi.Quantity));
            var averageCheck = totalOrdersCount > 0 ? totalRevenue / totalOrdersCount : 0;

            
            var costOfGoods = completedOrders.Sum(o => o.OrderItems.Sum(oi => oi.Quantity * (oi.Product?.PurchaseCost ?? 0)));
            if (costOfGoods == 0 && totalRevenue > 0) costOfGoods = totalRevenue * 0.5m;

            
            var packagingCost = totalOrdersCount * packagingPerOrder;

            
            var totalExpenses = costOfGoods + logisticsToWarehousePeriod + warehouseRentPeriod + pickupPointRentPeriod +
                                logisticsToPickupPeriod + salaryTotalPeriod + socialTaxPeriod + advertisingPeriod +
                                packagingCost + bankServicePeriod + hostingPeriod + utilitiesPeriod + officeExpensesPeriod +
                                expenses.Sum(e => e.Amount);

            
            var taxAmount = totalRevenue * (taxRate / 100);
            var netProfit = totalRevenue - totalExpenses - taxAmount;
            var profitMargin = totalRevenue > 0 ? (netProfit / totalRevenue) * 100 : 0;

            
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalExpenses = totalExpenses;
            ViewBag.TaxAmount = taxAmount;
            ViewBag.NetProfit = netProfit;
            ViewBag.ProfitMargin = profitMargin;
            ViewBag.TotalOrdersCount = totalOrdersCount;
            ViewBag.TotalItemsSold = totalItemsSold;
            ViewBag.AverageCheck = averageCheck;
            ViewBag.CostOfGoods = costOfGoods;
            ViewBag.PackagingCost = packagingCost;
            ViewBag.TaxRate = taxRate;
            ViewBag.Orders = completedOrders;
            ViewBag.Expenses = expenses;
            ViewBag.MonthlyFactor = monthlyFactor;

            
            ViewBag.SalaryTotal = salaryTotalPeriod;
            ViewBag.SocialTax = socialTaxPeriod;
            ViewBag.WarehouseRent = warehouseRentPeriod;
            ViewBag.PickupPointRent = pickupPointRentPeriod;
            ViewBag.LogisticsToWarehouse = logisticsToWarehousePeriod;
            ViewBag.LogisticsToPickup = logisticsToPickupPeriod;
            ViewBag.Advertising = advertisingPeriod;
            ViewBag.BankService = bankServicePeriod;
            ViewBag.Hosting = hostingPeriod;
            ViewBag.Utilities = utilitiesPeriod;
            ViewBag.OfficeExpenses = officeExpensesPeriod;
            ViewBag.PackagingPerOrder = packagingPerOrder;

            
            ViewBag.SalaryTotalMonthly = salaryTotal;
            ViewBag.WarehouseRentMonthly = warehouseRent;
            ViewBag.PickupPointRentMonthly = pickupPointRent;
            ViewBag.LogisticsToWarehouseMonthly = logisticsToWarehouse;
            ViewBag.LogisticsToPickupMonthly = logisticsToPickup;
            ViewBag.AdvertisingMonthly = advertising;
            ViewBag.BankServiceMonthly = bankService;
            ViewBag.HostingMonthly = hosting;
            ViewBag.UtilitiesMonthly = utilities;
            ViewBag.OfficeExpensesMonthly = officeExpenses;

            return View();
        }

        // POST: Admin/UpdateExpensesSettings
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateExpensesSettings([FromForm] Dictionary<string, string> formData)
        {
            if (!IsAdmin()) return Unauthorized();

            try
            {
                var settingsKeys = new[]
                {
            "SalaryTotal", "WarehouseRent", "PickupPointRent",
            "LogisticsToWarehouse", "LogisticsToPickup", "Advertising",
            "BankService", "Hosting", "Utilities", "OfficeExpenses"
        };

                foreach (var key in settingsKeys)
                {
                    if (formData.ContainsKey(key))
                    {
                        var value = formData[key];
                        var existing = await _context.FinancialSettings.FirstOrDefaultAsync(s => s.SettingKey == key);

                        if (existing != null)
                        {
                            existing.SettingValue = value;
                            _context.FinancialSettings.Update(existing);
                        }
                        else
                        {
                            _context.FinancialSettings.Add(new FinancialSetting
                            {
                                SettingKey = key,
                                SettingValue = value,
                                Description = GetDescriptionForSetting(key)
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Настройки сохранены" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Вспомогательный метод для получения описания настройки
        private string GetDescriptionForSetting(string key)
        {
            return key switch
            {
                "TaxRate" => "Ставка налога УСН (в %)",
                "AcquiringRate" => "Комиссия эквайринга (в %)",
                "SalaryTotal" => "Общая зарплата всех сотрудников (BYN/мес)",
                "SocialTaxRate" => "Ставка отчислений ФСЗН (в %)",
                "WarehouseRent" => "Аренда склада (BYN/мес)",
                "PickupPointRent" => "Аренда пункта выдачи (BYN/мес)",
                "LogisticsToWarehouse" => "Логистика от поставщика до склада (BYN/мес)",
                "LogisticsToPickup" => "Логистика со склада на ПВЗ (BYN/мес)",
                "Advertising" => "Расходы на рекламу (BYN/мес)",
                "PackagingPerOrder" => "Стоимость упаковки на 1 заказ (BYN)",
                "BankService" => "Банковское обслуживание (BYN/мес)",
                "Hosting" => "Хостинг и домен (BYN/мес)",
                "Utilities" => "Коммунальные услуги (BYN/мес)",
                "OfficeExpenses" => "Канцтовары и прочее (BYN/мес)",
                _ => ""
            };
        }

        [HttpGet]
        public async Task<IActionResult> CalculateSuggestedPrices(decimal purchaseCost)
        {
            if (!IsAdmin()) return Unauthorized();

            
            var settings = await _context.FinancialSettings.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

            var taxRate = GetDecimalSetting(settings, "TaxRate", 5m);
            var desiredMargin = 50m; //наценка
            var packagingPerOrder = GetDecimalSetting(settings, "PackagingPerOrder", 1.5m);

            //расходы за месяц
            var fixedExpenses = GetDecimalSetting(settings, "WarehouseRent", 0) +
                                GetDecimalSetting(settings, "PickupPointRent", 0) +
                                GetDecimalSetting(settings, "SalaryTotal", 0) +
                                GetDecimalSetting(settings, "Advertising", 0) +
                                GetDecimalSetting(settings, "BankService", 0) +
                                GetDecimalSetting(settings, "Hosting", 0) +
                                GetDecimalSetting(settings, "Utilities", 0) +
                                GetDecimalSetting(settings, "OfficeExpenses", 0);

            // Количество товаров, проданных за прошлый месяц
            var startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var soldCount = await _context.OrderItems
                .Where(oi => oi.Order.OrderDate >= startDate && oi.Order.OrderDate < startDate.AddMonths(1))
                .SumAsync(oi => oi.Quantity);

            if (soldCount == 0) soldCount = 100; // Значение по умолчанию

            // Расчёт рекомендуемой цены
            var fixedExpensesPerItem = fixedExpenses / soldCount;
            var recommendedPrice = purchaseCost + fixedExpensesPerItem + (purchaseCost * desiredMargin / 100);

            // Расчёт минимальной цены (безубыток)
            var packagingPerItem = packagingPerOrder / 3; // ~3 товара в заказе
            var breakEvenPrice = (purchaseCost + fixedExpensesPerItem + packagingPerItem) / (1 - taxRate / 100);

            return Json(new
            {
                recommended = Math.Round(recommendedPrice, 2),
                breakEven = Math.Round(breakEvenPrice, 2),
                profitPerUnit = Math.Round(recommendedPrice - purchaseCost, 2)
            });
        }

        // POST: Admin/AddExpense
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExpense(Expense expense)
        {
            if (!IsAdmin()) return RedirectToHome();

            // Валидация
            if (expense.Amount <= 0)
            {
                TempData["Error"] = "Сумма расхода должна быть больше 0";
                return RedirectToAction("Finance");
            }

            if (string.IsNullOrEmpty(expense.Category))
            {
                TempData["Error"] = "Укажите категорию расхода";
                return RedirectToAction("Finance");
            }

            if (string.IsNullOrEmpty(expense.Description))
            {
                TempData["Error"] = "Укажите описание расхода";
                return RedirectToAction("Finance");
            }

            // Устанавливаем дату, если не указана
            if (expense.ExpenseDate == default)
            {
                expense.ExpenseDate = DateTime.Now;
            }

            try
            {
                _context.Expenses.Add(expense);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Расход успешно добавлен!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ошибка при добавлении расхода: {ex.Message}";
            }

            return RedirectToAction("Finance");
        }

        // GET: Admin/GetAnalyticsData
        [HttpGet]
        public async Task<IActionResult> GetAnalyticsData(DateTime? startDate, DateTime? endDate)
        {
            if (!IsAdmin()) return Unauthorized();

            // Устанавливаем период (по умолчанию последние 30 дней)
            var end = endDate ?? DateTime.Now;
            var start = startDate ?? end.AddDays(-30);

            // Приводим даты к началу дня для корректного сравнения
            var startUtc = start.Date;
            var endUtc = end.Date.AddDays(1);

            // Получаем все выполненные заказы за период
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(o => o.Status == "Выполнен" && o.OrderDate >= startUtc && o.OrderDate <= endUtc)
                .ToListAsync();

            // ==================== 1. ВЫРУЧКА ПО ДНЯМ ====================
            var revenueByDay = orders
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Revenue = g.Sum(o => o.TotalAmount)
                })
                .OrderBy(x => x.Date)
                .ToList();

            // ==================== 2. КОЛИЧЕСТВО ЗАКАЗОВ ПО ДНЯМ ====================
            var ordersCountByDay = orders
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Count = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            // ==================== 3. ТОП-5 САМЫХ ПРОДАВАЕМЫХ ТОВАРОВ ====================
            var topProducts = orders
                .SelectMany(o => o.OrderItems)
                .GroupBy(oi => new { oi.ProductID, ProductName = oi.Product != null ? oi.Product.Name : "Неизвестно" })
                .Select(g => new
                {
                    ProductId = g.Key.ProductID,
                    ProductName = g.Key.ProductName,
                    TotalQuantity = g.Sum(oi => oi.Quantity),
                    TotalRevenue = g.Sum(oi => oi.Quantity * oi.UnitPrice)
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(5)
                .ToList();

            // ==================== 4. ОБЩАЯ СТАТИСТИКА ====================
            var totalRevenue = orders.Sum(o => o.TotalAmount);
            var totalOrders = orders.Count;
            var averageCheck = totalOrders > 0 ? totalRevenue / totalOrders : 0;

            // Отладочная информация в консоль сервера
            Console.WriteLine($"=== GetAnalyticsData ===");
            Console.WriteLine($"Период: {start:yyyy-MM-dd} - {end:yyyy-MM-dd}");
            Console.WriteLine($"Найдено заказов: {totalOrders}");
            Console.WriteLine($"Выручка: {totalRevenue}");
            Console.WriteLine($"Топ-5 товаров: {topProducts.Count}");

            // Возвращаем данные
            return Json(new
            {
                revenueByDay = revenueByDay,
                ordersCountByDay = ordersCountByDay,
                topProducts = topProducts,
                totalRevenue = totalRevenue,
                totalOrders = totalOrders,
                averageCheck = averageCheck,
                period = new
                {
                    startDate = start.ToString("yyyy-MM-dd"),
                    endDate = end.ToString("yyyy-MM-dd")
                }
            });
        }
        // GET: Admin/GetOrdersForExport
        [HttpGet]
        public async Task<IActionResult> GetOrdersForExport(DateTime? startDate, DateTime? endDate)
        {
            if (!IsAdmin()) return Unauthorized();

            var end = endDate ?? DateTime.Now;
            var start = startDate ?? end.AddDays(-30);

            var startUtc = start.Date;
            var endUtc = end.Date.AddDays(1);

            var orders = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.OrderDate >= startUtc && o.OrderDate <= endUtc)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    OrderID = o.OrderID,
                    CustomerName = o.Customer != null ? o.Customer.FullName : "Не указан",
                    OrderDate = o.OrderDate.ToString("dd.MM.yyyy HH:mm"),
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                    ShippingAddress = o.ShippingAddress
                })
                .ToListAsync();

            return Json(orders);
        }

        // GET: Admin/GetLoginAttempts
        [HttpGet]
        public async Task<IActionResult> GetLoginAttempts(string search = "", string status = "", DateTime? startDate = null, DateTime? endDate = null, int page = 1, int pageSize = 20)
        {
            if (!IsAdmin()) return Unauthorized();

            var end = endDate ?? DateTime.Now;
            var start = startDate ?? end.AddDays(-30);

            // Добавляем 1 день к конечной дате, чтобы включить весь день
            var endDateAdjusted = end.Date.AddDays(1);

            var query = _context.LoginAttempts
                .Where(a => a.AttemptTime >= start && a.AttemptTime < endDateAdjusted);  // ✅ ПРАВИЛЬНО

            // Фильтр по поиску (email или IP)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a => a.Email.Contains(search) || a.IpAddress.Contains(search));
            }

            // Фильтр по статусу
            if (!string.IsNullOrEmpty(status))
            {
                bool isSuccessful = status == "success";
                query = query.Where(a => a.IsSuccessful == isSuccessful);
            }

            // Общее количество записей
            var totalCount = await query.CountAsync();

            // Пагинация и сортировка (самые новые сначала)
            var attempts = await query
                .OrderByDescending(a => a.AttemptTime)  // Сначала самые новые
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.Email,
                    a.AttemptTime,
                    a.IpAddress,
                    a.IsSuccessful,
                    a.UserAgent
                })
                .ToListAsync();

            // Статистика за ВЕСЬ период (не отфильтрованная)
            var totalAttempts = await _context.LoginAttempts.CountAsync();
            var successfulAttempts = await _context.LoginAttempts.CountAsync(a => a.IsSuccessful);
            var failedAttempts = totalAttempts - successfulAttempts;
            var uniqueIps = await _context.LoginAttempts.Select(a => a.IpAddress).Distinct().CountAsync();

            // Отладочная информация
            Console.WriteLine($"GetLoginAttempts: page={page}, totalCount={totalCount}, attempts={attempts.Count}");

            return Json(new
            {
                attempts = attempts,
                totalCount = totalCount,
                currentPage = page,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                stats = new
                {
                    total = totalAttempts,
                    successful = successfulAttempts,
                    failed = failedAttempts,
                    uniqueIps = uniqueIps
                }
            });
        }

        // POST: Admin/DeleteOldLogins
        [HttpPost]
        public async Task<IActionResult> DeleteOldLogins(int days = 30)
        {
            if (!IsAdmin()) return Unauthorized();

            var cutoffDate = DateTime.Now.AddDays(-days);
            var oldLogins = _context.LoginAttempts.Where(a => a.AttemptTime < cutoffDate);
            var count = oldLogins.Count();

            _context.LoginAttempts.RemoveRange(oldLogins);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Удалено {count} записей старше {days} дней" });
        }

        // GET: Admin/ExportLoginAttempts
        [HttpGet]
        public async Task<IActionResult> ExportLoginAttempts(DateTime? startDate, DateTime? endDate)
        {
            if (!IsAdmin()) return Unauthorized();

            var end = endDate ?? DateTime.Now;
            var start = startDate ?? end.AddDays(-30);

            var attempts = await _context.LoginAttempts
                .Where(a => a.AttemptTime >= start && a.AttemptTime <= end)
                .OrderByDescending(a => a.AttemptTime)
                .Select(a => new
                {
                    a.Email,
                    AttemptTime = a.AttemptTime,
                    a.IpAddress,
                    Status = a.IsSuccessful ? "Успех" : "Ошибка",
                    a.UserAgent
                })
                .ToListAsync();

            return Json(attempts);
        }

        // GET: Admin/ProductQuestions
        public async Task<IActionResult> ProductQuestions()
        {
            if (!IsAdmin()) return RedirectToHome();

            // Получаем все товары для фильтра
            var products = await _context.Products
                .Select(p => new { p.ProductID, p.Name })
                .ToListAsync();

            ViewBag.Products = products;
            return View();
        }

        // GET: Admin/GetProductQuestionsAdmin (AJAX API)
        [HttpGet]
        public async Task<IActionResult> GetProductQuestionsAdmin(
            string search = "",
            int? productId = null,
            string status = "",
            string sort = "newest",
            int page = 1,
            int pageSize = 10)
        {
            if (!IsAdmin()) return Unauthorized();

          
            var query = _context.ProductQuestions
                .Include(q => q.Customer)
                .Include(q => q.Product)
                .AsQueryable();

          
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(q =>
                    q.Question.Contains(search) ||
                    (q.Customer != null && q.Customer.FullName.Contains(search)));
            }

           
            if (productId.HasValue && productId > 0)
            {
                query = query.Where(q => q.ProductId == productId.Value);
            }

           
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "pending")
                    query = query.Where(q => q.Answer == null || q.Answer == "");
                else if (status == "answered")
                    query = query.Where(q => q.Answer != null && q.Answer != "");
            }

            
            query = sort == "newest"
                ? query.OrderByDescending(q => q.QuestionDate)
                : query.OrderBy(q => q.QuestionDate);

            
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            
            var questions = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    q.Id,
                    q.ProductId,
                    ProductName = q.Product != null ? q.Product.Name : "Товар удалён",
                    q.Question,
                    q.QuestionDate,
                    q.Answer,
                    q.AnswerDate,
                    CustomerName = q.Customer != null ? q.Customer.FullName : "Пользователь",
                    CustomerEmail = q.Customer != null ? q.Customer.Email : "",
                    IsAnswered = !string.IsNullOrEmpty(q.Answer)
                })
                .ToListAsync();

            
            var totalQuestions = await _context.ProductQuestions.CountAsync();
            var pendingQuestions = await _context.ProductQuestions
                .Where(q => q.Answer == null || q.Answer == "")
                .CountAsync();
            var answeredQuestions = totalQuestions - pendingQuestions;

            
            return Json(new
            {
                questions = questions,
                totalCount = totalCount,
                totalPages = totalPages,
                currentPage = page,
                stats = new
                {
                    total = totalQuestions,
                    pending = pendingQuestions,
                    answered = answeredQuestions
                }
            });
        }

        // POST: Admin/AnswerQuestion
        [HttpPost]
        public async Task<IActionResult> AnswerQuestion([FromBody] AnswerQuestionRequest request)
        {
            if (!IsAdmin()) return Unauthorized();

            if (request == null || request.Id <= 0)
                return Json(new { success = false, message = "Неверный ID вопроса" });

            if (string.IsNullOrWhiteSpace(request.Answer))
                return Json(new { success = false, message = "Ответ не может быть пустым" });

            try
            {
                var question = await _context.ProductQuestions
                    .Include(q => q.Customer)
                    .Include(q => q.Product)
                    .FirstOrDefaultAsync(q => q.Id == request.Id);

                if (question == null)
                    return Json(new { success = false, message = "Вопрос не найден" });

                // Сохраняем ответ
                question.Answer = request.Answer.Trim();
                question.AnswerDate = DateTime.Now;
                question.IsPublished = true;  // Автоматически публикуем

                await _context.SaveChangesAsync();

                // Отправляем email-уведомление пользователю
                if (question.Customer != null && !string.IsNullOrEmpty(question.Customer.Email))
                {
                    await SendAnswerNotification(question.Customer.Email, question.Customer.FullName, question);
                }

                return Json(new { success = true, message = "Ответ сохранён и опубликован" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Ошибка: {ex.Message}" });
            }
        }

        // POST: Admin/DeleteQuestion
        [HttpPost]
        public async Task<IActionResult> DeleteQuestion([FromBody] DeleteQuestionRequest request)
        {
            if (!IsAdmin()) return Unauthorized();

            if (request == null || request.Id <= 0)
                return Json(new { success = false, message = "Неверный ID вопроса" });

            try
            {
                var question = await _context.ProductQuestions.FindAsync(request.Id);
                if (question == null)
                    return Json(new { success = false, message = "Вопрос не найден" });

                _context.ProductQuestions.Remove(question);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Вопрос удалён" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Ошибка: {ex.Message}" });
            }
        }

        // Вспомогательный метод для отправки email-уведомления
        private async Task SendAnswerNotification(string toEmail, string toName, ProductQuestion question)
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:SmtpServer"];
                var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]);
                var senderEmail = _configuration["EmailSettings:SenderEmail"];
                var senderPassword = _configuration["EmailSettings:SenderPassword"];
                var useSsl = bool.Parse(_configuration["EmailSettings:UseSsl"]);

                string subject = $"Ответ на ваш вопрос о товаре #{question.ProductId}";

                string body = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <style>
                    body {{ font-family: Arial, sans-serif; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background: linear-gradient(135deg, #2c5aa0, #3a7bd5); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0; }}
                    .content {{ padding: 20px; background: #f8f9fa; }}
                    .question-box {{ background: white; padding: 15px; border-radius: 10px; margin-bottom: 20px; border-left: 4px solid #ffc107; }}
                    .answer-box {{ background: white; padding: 15px; border-radius: 10px; border-left: 4px solid #28a745; }}
                    .footer {{ text-align: center; padding: 15px; font-size: 12px; color: #6c757d; }}
                    .button {{ display: inline-block; background: #2c5aa0; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h2>Канцелярский Магазин</h2>
                        <p>Ответ на ваш вопрос</p>
                    </div>
                    <div class='content'>
                        <p>Здравствуйте, <strong>{toName}</strong>!</p>
                        <p>Администратор ответил на ваш вопрос о товаре <strong>{(question.Product != null ? question.Product.Name : "товаре")}</strong>.</p>
                        
                        <div class='question-box'>
                            <strong>📋 Ваш вопрос:</strong>
                            <p style='margin-top: 8px;'>{question.Question}</p>
                        </div>
                        
                        <div class='answer-box'>
                            <strong>💬 Ответ администратора:</strong>
                            <p style='margin-top: 8px;'>{question.Answer}</p>
                        </div>
                        
                    </div>
                    <div class='footer'>
                        <p>Это письмо отправлено автоматически, пожалуйста, не отвечайте на него.</p>
                        <p>© 2026 Канцелярский магазин</p>
                    </div>
                </div>
            </body>
            </html>
        ";

                using var client = new SmtpClient(smtpServer, smtpPort);
                client.EnableSsl = useSsl;
                client.Credentials = new NetworkCredential(senderEmail, senderPassword);

                var mailMessage = new MailMessage();
                mailMessage.From = new MailAddress(senderEmail, "Канцелярский магазин");
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.IsBodyHtml = true;
                mailMessage.To.Add(new MailAddress(toEmail, toName));

                await client.SendMailAsync(mailMessage);

                Console.WriteLine($"Email уведомление отправлено на {toEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки email: {ex.Message}");
            }
        }

        // Вспомогательные классы для приёма данных
        public class AnswerQuestionRequest
        {
            public int Id { get; set; }
            public string Answer { get; set; } = string.Empty;
        }

        public class DeleteQuestionRequest
        {
            public int Id { get; set; }
        }
    }

}