using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StationeryShop.Data;
using StationeryShop.Models;
using StationeryShop.Services;

namespace StationeryShop.Controllers
{
    public class OrdersController : Controller
    {
        private readonly StationeryDbContext _context;
        private readonly CartService _cartService;
        private readonly ILogger<OrdersController> _logger;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;

        public OrdersController(StationeryDbContext context, CartService cartService, ILogger<OrdersController> logger, EmailService emailService, IConfiguration configuration)
        {
            _context = context;
            _cartService = cartService;
            _logger = logger;
            _emailService = emailService;
            _configuration = configuration;
        }

        private bool IsAuthenticated()
        {
            return HttpContext.Session.GetInt32("CustomerID") != null;
        }

        private IActionResult RedirectToLogin()
        {
            TempData["Error"] = "Для оформления заказа необходимо авторизоваться";
            return RedirectToAction("Login", "Account");
        }

     
        public async Task<IActionResult> Index()
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

            int? customerId = HttpContext.Session.GetInt32("CustomerID");
            bool isAdmin = HttpContext.Session.GetString("IsAdmin") == "true";

            var query = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .OrderByDescending(o => o.OrderDate)
                .AsQueryable();

            if (!isAdmin)
                query = query.Where(o => o.CustomerID == customerId);

            return View(await query.ToListAsync());
        }

        // =================== СОЗДАНИЕ ЗАКАЗА ===================
        public IActionResult Create()
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

            var cartItems = _cartService.GetCartItems();

            if (!cartItems.Any())
            {
                TempData["Error"] = "Корзина пуста";
                return RedirectToAction("Index", "Cart");
            }

            var customerId = HttpContext.Session.GetInt32("CustomerID");
            var customer = _context.Customers.FirstOrDefault(c => c.CustomerID == customerId);

            var order = new Order
            {
                CustomerID = customerId.Value,
                ShippingAddress = _configuration["PickupPoint:FullAddress"],
                OrderDate = DateTime.Now,
                TotalAmount = _cartService.GetTotalPrice(),
                Status = "Принят"
            };

            ViewBag.CartItems = cartItems;
            ViewBag.TotalPrice = _cartService.GetTotalPrice();
            ViewBag.PickupAddress = _configuration["PickupPoint:FullAddress"];

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Order order)
        {
            _logger.LogInformation("Начало оформления заказа");

            if (!IsAuthenticated())
                return RedirectToLogin();

            try
            {
                _logger.LogInformation("Проверка аутентификации пройдена");

                var cartItems = _cartService.GetCartItems();
                _logger.LogInformation($"Количество товаров в корзине: {cartItems.Count}");

                if (!cartItems.Any())
                {
                    TempData["Error"] = "Корзина пуста. Добавьте товары перед оформлением заказа.";
                    return RedirectToAction("Index", "Cart");
                }

                // Проверяем наличие товаров на складе
                foreach (var cartItem in cartItems)
                {
                    var product = await _context.Products.FindAsync(cartItem.ProductId);
                    if (product == null)
                    {
                        _logger.LogWarning($"Товар с ID {cartItem.ProductId} не найден");
                        TempData["Error"] = $"Товар '{cartItem.ProductName}' не найден.";
                        return RedirectToAction("Index", "Cart");
                    }

                    if (product.StockQuantity < cartItem.Quantity)
                    {
                        _logger.LogWarning($"Недостаточно товара {cartItem.ProductName}. Нужно: {cartItem.Quantity}, есть: {product.StockQuantity}");
                        TempData["Error"] = $"Товар '{cartItem.ProductName}' недоступен в нужном количестве. Доступно: {product.StockQuantity}";
                        return RedirectToAction("Index", "Cart");
                    }
                }

                _logger.LogInformation("Проверка наличия товаров пройдена");

                // Устанавливаем обязательные поля
                var customerId = HttpContext.Session.GetInt32("CustomerID");
                if (!customerId.HasValue)
                {
                    _logger.LogError("CustomerID не найден в сессии");
                    TempData["Error"] = "Ошибка авторизации. Пожалуйста, войдите снова.";
                    return RedirectToAction("Login", "Account");
                }

                order.CustomerID = customerId.Value;
                order.OrderDate = DateTime.Now;
                order.TotalAmount = _cartService.GetTotalPrice();
                order.Status = "Принят";

                _logger.LogInformation($"Создание заказа: CustomerID={order.CustomerID}, TotalAmount={order.TotalAmount}");

                // Валидация модели
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Модель заказа невалидна:");
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        _logger.LogWarning($"Ошибка валидации: {error.ErrorMessage}");
                    }

                    // Возвращаем к форме с ошибками
                    ViewBag.CartItems = cartItems;
                    ViewBag.TotalPrice = _cartService.GetTotalPrice();
                    return View(order);
                }

                // Создаем заказ
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Заказ создан с ID: {order.OrderID}");

                // Добавляем товары в заказ
                foreach (var cartItem in cartItems)
                {
                    var orderItem = new OrderItem
                    {
                        OrderID = order.OrderID,
                        ProductID = cartItem.ProductId,
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.Price
                    };

                    _context.OrderItems.Add(orderItem);
                    _logger.LogInformation($"Добавлен товар в заказ: {cartItem.ProductName}, количество: {cartItem.Quantity}");

                    // Обновляем количество товара на складе
                    var product = await _context.Products.FindAsync(cartItem.ProductId);
                    if (product != null)
                    {
                        var oldQuantity = product.StockQuantity;
                        product.StockQuantity -= cartItem.Quantity;
                        _context.Products.Update(product);
                        _logger.LogInformation($"Обновлен запас товара {product.Name}: было {oldQuantity}, стало {product.StockQuantity}");
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Все изменения сохранены в БД");

                // Очищаем корзину
                _cartService.ClearCart();
                _logger.LogInformation("Корзина очищена");
                _logger.LogInformation("Начинаем отправку email уведомления для заказа #{OrderID}", order.OrderID);

                try
                {
                    // Получаем данные клиента отдельно
                    _logger.LogInformation("Пытаемся получить данные клиента с ID: {CustomerID}", order.CustomerID);
                    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerID == order.CustomerID);

                    if (customer == null)
                    {
                        _logger.LogWarning("Клиент с ID {CustomerID} не найден", order.CustomerID);
                    }
                    else if (string.IsNullOrEmpty(customer.Email))
                    {
                        _logger.LogWarning("У клиента {CustomerName} (ID: {CustomerID}) отсутствует email", customer.FullName, customer.CustomerID);
                    }
                    else
                    {
                        _logger.LogInformation("Найден клиент: {CustomerName}, Email: {Email}", customer.FullName, customer.Email);
                        _logger.LogInformation("Вызываем EmailService.SendOrderCreatedNotification для заказа #{OrderID}", order.OrderID);

                        bool emailSent = await _emailService.SendOrderCreatedNotification(
                            customer.Email,
                            customer.FullName,
                            order.OrderID
                        );

                        if (emailSent)
                        {
                            _logger.LogInformation("✅ Уведомление о создании заказа #{OrderID} успешно отправлено на {Email}", order.OrderID, customer.Email);
                        }
                        else
                        {
                            _logger.LogWarning("❌ EmailService вернул false при отправке уведомления для заказа #{OrderID}", order.OrderID);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ КРИТИЧЕСКАЯ ОШИБКА при отправке email для заказа #{OrderID}: {ErrorMessage}", order.OrderID, ex.Message);
                }
                return RedirectToAction("Confirm", new { id = order.OrderID });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании заказа");
                TempData["Error"] = $"Произошла ошибка при оформлении заказа: {ex.Message}";
                return RedirectToAction("Index", "Cart");
            }
        }

        // =================== ПОДТВЕРЖДЕНИЕ ЗАКАЗА ===================
        public async Task<IActionResult> Confirm(int id)
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null)
                return NotFound();

            // Проверяем, что заказ принадлежит текущему пользователю или это админ
            var customerId = HttpContext.Session.GetInt32("CustomerID");
            bool isAdmin = HttpContext.Session.GetString("IsAdmin") == "true";

            if (!isAdmin && order.CustomerID != customerId)
                return Forbid();

            return View(order);
        }
        // GET: Orders/ClientReceipt/5 (для клиента)
        public async Task<IActionResult> ClientReceipt(int id)
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null)
                return NotFound();

            // Проверяем права (только владелец заказа)
            var customerId = HttpContext.Session.GetInt32("CustomerID");
            if (order.CustomerID != customerId)
                return Forbid();

            ViewBag.PickupAddress = _configuration["PickupPoint:FullAddress"];
            return View(order);
        }

        // =================== ДЕТАЛИ ЗАКАЗА ===================
        public async Task<IActionResult> Details(int id)
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null)
                return NotFound();

            // Проверяем, что заказ принадлежит текущему пользователю или это админ
            var customerId = HttpContext.Session.GetInt32("CustomerID");
            bool isAdmin = HttpContext.Session.GetString("IsAdmin") == "true";

            if (!isAdmin && order.CustomerID != customerId)
                return Forbid();

            return View(order);
        }
        // GET: Orders/PrintInvoice/5
        public async Task<IActionResult> PrintInvoice(int id)
        {
            // Проверяем авторизацию
            if (!IsAuthenticated())
                return RedirectToLogin();

            // Загружаем заказ со всеми связанными данными
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            // Если заказ не найден
            if (order == null)
                return NotFound();

            // Проверяем права доступа (админ или владелец заказа)
            var customerId = HttpContext.Session.GetInt32("CustomerID");
            bool isAdmin = HttpContext.Session.GetString("IsAdmin") == "true";

            if (!isAdmin && order.CustomerID != customerId)
                return Forbid();

            return View(order);
        }
    }

}