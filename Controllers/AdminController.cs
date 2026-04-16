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

            
            var end = endDate ?? DateTime.Now;
            var start = startDate ?? end.AddDays(-30);

            
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(o => o.Status == "Выполнен" && o.OrderDate >= start && o.OrderDate <= end)
                .ToListAsync();

            
            var revenueByDay = orders
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Revenue = g.Sum(o => o.TotalAmount)
                })
                .OrderBy(x => x.Date)
                .ToList();

            
            var ordersCountByDay = orders
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Count = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            
            var topProducts = orders
                .SelectMany(o => o.OrderItems)
                .GroupBy(oi => new { oi.ProductID, oi.Product.Name })
                .Select(g => new
                {
                    ProductId = g.Key.ProductID,
                    ProductName = g.Key.Name,
                    TotalQuantity = g.Sum(oi => oi.Quantity),
                    TotalRevenue = g.Sum(oi => oi.Quantity * oi.UnitPrice)
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(5)
                .ToList();

            
            var totalRevenue = orders.Sum(o => o.TotalAmount);
            var totalOrders = orders.Count;
            var averageCheck = totalOrders > 0 ? totalRevenue / totalOrders : 0;

            // Возвращаем все данные в формате JSON
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
    }

}