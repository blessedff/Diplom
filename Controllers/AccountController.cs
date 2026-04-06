// Controllers/AccountController.cs (обновленная версия)
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StationeryShop.Data;
using StationeryShop.Models;
using StationeryShop.Services;
using System.Net;
using System.Text.Json;

namespace StationeryShop.Controllers
{
    public class AccountController : Controller
    {
        private readonly StationeryDbContext _context;
        private readonly CartService _cartService;
        private readonly ILogger<AccountController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        // Google reCAPTCHA секретный ключ (получить с https://www.google.com/recaptcha)
        private const string RECAPTCHA_SECRET_KEY = "ВАШ_СЕКРЕТНЫЙ_КЛЮЧ";
        private const int MAX_FAILED_ATTEMPTS = 5;
        private const int BLOCK_MINUTES = 15;

        public AccountController(
            StationeryDbContext context,
            CartService cartService,
            ILogger<AccountController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _cartService = cartService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        // ==================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ====================

        private string GetClientIpAddress()
        {
            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(ip) || ip == "::1")
                ip = "127.0.0.1";
            return ip;
        }

        private string GetUserAgent()
        {
            return _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString() ?? "Unknown";
        }

        private async Task<bool> IsBlocked(string email, string ipAddress)
        {
            var blockUntil = DateTime.Now.AddMinutes(-BLOCK_MINUTES);

            var failedAttempts = await _context.LoginAttempts
                .Where(a => (a.Email == email || a.IpAddress == ipAddress)
                            && a.AttemptTime >= blockUntil
                            && !a.IsSuccessful)
                .CountAsync();

            return failedAttempts >= MAX_FAILED_ATTEMPTS;
        }

        private async Task LogLoginAttempt(string email, bool isSuccessful)
        {
            var attempt = new LoginAttempt
            {
                Email = email,
                AttemptTime = DateTime.Now,
                IpAddress = GetClientIpAddress(),
                IsSuccessful = isSuccessful,
                UserAgent = GetUserAgent()
            };

            _context.LoginAttempts.Add(attempt);
            await _context.SaveChangesAsync();

            // Очищаем старые записи (старше 7 дней)
            var weekAgo = DateTime.Now.AddDays(-7);
            var oldAttempts = _context.LoginAttempts.Where(a => a.AttemptTime < weekAgo);
            _context.LoginAttempts.RemoveRange(oldAttempts);
            await _context.SaveChangesAsync();
        }

        private async Task<bool> VerifyRecaptcha(string recaptchaResponse)
        {
            if (string.IsNullOrEmpty(recaptchaResponse))
                return false;

            using var client = new HttpClient();
            var response = await client.PostAsync(
                "https://www.google.com/recaptcha/api/siteverify",
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("secret", RECAPTCHA_SECRET_KEY),
                    new KeyValuePair<string, string>("response", recaptchaResponse)
                })
            );

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<RecaptchaResponse>(json);

            return result?.Success == true;
        }

        // ==================== LOGIN ====================

        [HttpGet]
        public IActionResult Login()
        {
            // Если пользователь уже авторизован — перенаправляем
            if (IsAuthenticated())
                return RedirectToAction("Index", "Home");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string recaptchaToken)
        {
            // Если уже авторизован
            if (IsAuthenticated())
                return RedirectToAction("Index", "Home");

            // Проверка reCAPTCHA
            var isRecaptchaValid = await VerifyRecaptcha(recaptchaToken);
            if (!isRecaptchaValid)
            {
                ViewBag.Error = "Подтвердите, что вы не робот";
                return View();
            }

            // Проверка на блокировку
            var ipAddress = GetClientIpAddress();
            if (await IsBlocked(email, ipAddress))
            {
                ViewBag.Error = $"Слишком много неудачных попыток. Попробуйте через {BLOCK_MINUTES} минут.";
                return View();
            }

            // Поиск пользователя
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email == email);

            // Проверка пароля
            bool isValid = customer != null && VerifyPassword(password, customer.Password);

            // Логируем попытку
            await LogLoginAttempt(email, isValid);

            if (isValid)
            {
                // Успешный вход — очищаем старые попытки для этого email
                var oldAttempts = _context.LoginAttempts
                    .Where(a => a.Email == email && !a.IsSuccessful);
                _context.LoginAttempts.RemoveRange(oldAttempts);
                await _context.SaveChangesAsync();

                // Устанавливаем сессию
                var oldSessionId = HttpContext.Session.Id;
                HttpContext.Session.SetInt32("CustomerID", customer.CustomerID);
                HttpContext.Session.SetString("CustomerName", customer.FullName ?? "");
                HttpContext.Session.SetString("IsAdmin", customer.IsAdmin ? "true" : "false");

                // Регенерируем ID сессии для защиты от фиксации сессии
                HttpContext.Session.SetString("SessionRegenerated", "true");

                // Переносим корзину
                try
                {
                    _cartService.TransferCart(oldSessionId, customer.CustomerID);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при переносе корзины");
                }

                _logger.LogInformation($"Успешный вход: {email}, IP: {ipAddress}");
                TempData["Success"] = $"Добро пожаловать, {customer.FullName}!";
                return RedirectToAction("Index", "Home");
            }
            else
            {
                // Проверяем, сколько осталось попыток
                var blockUntil = DateTime.Now.AddMinutes(-BLOCK_MINUTES);
                var recentFailed = await _context.LoginAttempts
                    .Where(a => (a.Email == email || a.IpAddress == ipAddress)
                                && a.AttemptTime >= blockUntil
                                && !a.IsSuccessful)
                    .CountAsync();

                var remainingAttempts = MAX_FAILED_ATTEMPTS - recentFailed;

                if (remainingAttempts > 0)
                {
                    ViewBag.Error = $"Неверный email или пароль. Осталось попыток: {remainingAttempts}";
                }
                else
                {
                    ViewBag.Error = $"Аккаунт заблокирован на {BLOCK_MINUTES} минут. Попробуйте позже.";
                }

                _logger.LogWarning($"Неудачный вход: {email}, IP: {ipAddress}");
                return View();
            }
        }

        // ==================== REGISTER ====================

        [HttpGet]
        public IActionResult Register()
        {
            if (IsAuthenticated())
                return RedirectToAction("Index", "Home");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Customer customer, string confirmPassword, string recaptchaToken)
        {
            // Проверка reCAPTCHA
            var isRecaptchaValid = await VerifyRecaptcha(recaptchaToken);
            if (!isRecaptchaValid)
            {
                ModelState.AddModelError("", "Подтвердите, что вы не робот");
                return View(customer);
            }

            // Проверка пароля
            if (customer.Password != confirmPassword)
            {
                ModelState.AddModelError("", "Пароли не совпадают");
                return View(customer);
            }

            // Проверка сложности пароля
            if (!IsPasswordStrong(customer.Password))
            {
                ModelState.AddModelError("Password", "Пароль должен содержать минимум 8 символов, включая цифры и заглавные буквы");
                return View(customer);
            }

            if (!ModelState.IsValid)
                return View(customer);

            // Проверка на существующего пользователя
            if (await _context.Customers.AnyAsync(c => c.Email == customer.Email))
            {
                ModelState.AddModelError("Email", "Пользователь с таким email уже существует");
                return View(customer);
            }

            // Хешируем пароль
            customer.Password = HashPassword(customer.Password);
            customer.IsAdmin = false;

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            // Автоматический вход после регистрации
            HttpContext.Session.SetInt32("CustomerID", customer.CustomerID);
            HttpContext.Session.SetString("CustomerName", customer.FullName ?? "");
            HttpContext.Session.SetString("IsAdmin", "false");

            _logger.LogInformation($"Новая регистрация: {customer.Email}, IP: {GetClientIpAddress()}");
            TempData["Success"] = $"Регистрация успешна! Добро пожаловать, {customer.FullName}!";
            return RedirectToAction("Index", "Home");
        }

        // ==================== ДРУГИЕ МЕТОДЫ ====================

        private bool IsAuthenticated()
        {
            return HttpContext.Session.GetInt32("CustomerID") != null;
        }

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private bool VerifyPassword(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }

        private bool IsPasswordStrong(string password)
        {
            return password.Length >= 8 &&
                   password.Any(char.IsDigit) &&
                   password.Any(char.IsUpper);
        }

        // Класс для ответа от Google reCAPTCHA
        private class RecaptchaResponse
        {
            public bool Success { get; set; }
            public string? Challenge_ts { get; set; }
            public string? Hostname { get; set; }
            public List<string>? ErrorCodes { get; set; }
        }
    }
}