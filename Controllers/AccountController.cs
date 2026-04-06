using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StationeryShop.Data;
using StationeryShop.Models;
using StationeryShop.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StationeryShop.Controllers
{
    public class AccountController : Controller
    {
        private readonly StationeryDbContext _context;
        private readonly CartService _cartService;
        private readonly ILogger<AccountController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        // reCAPTCHA ключи из конфигурации
        private readonly string _recaptchaSecretKey;
        private readonly int _maxFailedAttempts;
        private readonly int _blockMinutes;
        private readonly string _recaptchaSiteKey;

        public AccountController(
            StationeryDbContext context,
            CartService cartService,
            ILogger<AccountController> logger,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _context = context;
            _cartService = cartService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;

            // Читаем настройки из appsettings.json
            _recaptchaSecretKey = _configuration["ReCaptcha:SecretKey"] ?? "";
            _recaptchaSiteKey = _configuration["ReCaptcha:SiteKey"] ?? "";
            _maxFailedAttempts = _configuration.GetValue<int>("Security:MaxFailedLoginAttempts", 5);
            _blockMinutes = _configuration.GetValue<int>("Security:LoginBlockMinutes", 15);
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
            var blockUntil = DateTime.Now.AddMinutes(-_blockMinutes);

            var failedAttempts = await _context.LoginAttempts
                .Where(a => (a.Email == email || a.IpAddress == ipAddress)
                            && a.AttemptTime >= blockUntil
                            && !a.IsSuccessful)
                .CountAsync();

            return failedAttempts >= _maxFailedAttempts;
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
            {
                Console.WriteLine("recaptchaResponse ПУСТОЙ");
                return false;
            }

            using var client = new HttpClient();
            var response = await client.PostAsync(
                "https://www.google.com/recaptcha/api/siteverify",
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("secret", _recaptchaSecretKey),
                    new KeyValuePair<string, string>("response", recaptchaResponse)
                })
            );

            var json = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Ответ Google: {json}");

            try
            {
                var result = JsonSerializer.Deserialize<RecaptchaResponse>(json);
                return result?.Success == true;
            }
            catch
            {
                return false;
            }
        }

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

        // ==================== LOGIN (ВХОД) ====================

        [HttpGet]
        public IActionResult Login()
        {
            Console.WriteLine("=== GET Login вызван ===");

            if (IsAuthenticated())
                return RedirectToAction("Index", "Home");

            ViewBag.ReCaptchaSiteKey = _recaptchaSiteKey;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string recaptchaToken)
        {
            Console.WriteLine($"=== POST Login вызван ===");
            Console.WriteLine($"Email: {email}");
            Console.WriteLine($"recaptchaToken: {(string.IsNullOrEmpty(recaptchaToken) ? "ПУСТО" : recaptchaToken.Substring(0, Math.Min(20, recaptchaToken.Length)))}");

            ViewBag.ReCaptchaSiteKey = _recaptchaSiteKey;

            if (IsAuthenticated())
            {
                Console.WriteLine("Пользователь уже авторизован, редирект на Home");
                return RedirectToAction("Index", "Home");
            }

            // Проверка reCAPTCHA
            Console.WriteLine("Проверяем reCAPTCHA...");

            //Отключение капчи временно + вьюшка аккаунт/логин
            //var isRecaptchaValid = await VerifyRecaptcha(recaptchaToken);
            //Console.WriteLine($"reCAPTCHA результат: {isRecaptchaValid}");

            //if (!isRecaptchaValid)
            //{
            //    ViewBag.Error = "Подтвердите, что вы не робот";
            //    return View();
            //}
            var isRecapthaValid = true;
            // Проверка на блокировку
            var ipAddress = GetClientIpAddress();
            Console.WriteLine($"IP адрес: {ipAddress}");

            if (await IsBlocked(email, ipAddress))
            {
                Console.WriteLine("Аккаунт заблокирован!");
                ViewBag.Error = $"Слишком много неудачных попыток. Попробуйте через {_blockMinutes} минут.";
                return View();
            }

            // Поиск пользователя
            Console.WriteLine($"Ищем пользователя с email: {email}");
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email == email);

            // Проверка пароля
            bool isValid = customer != null && VerifyPassword(password, customer.Password);
            Console.WriteLine($"Пользователь найден: {customer != null}, Пароль верен: {isValid}");

            // Логируем попытку
            await LogLoginAttempt(email, isValid);

            if (isValid)
            {
                Console.WriteLine("УСПЕШНЫЙ ВХОД!");

                // Успешный вход — очищаем старые попытки
                var oldAttempts = _context.LoginAttempts
                    .Where(a => a.Email == email && !a.IsSuccessful);
                _context.LoginAttempts.RemoveRange(oldAttempts);
                await _context.SaveChangesAsync();

                var oldSessionId = HttpContext.Session.Id;
                Console.WriteLine($"Старая сессия: {oldSessionId}");

                // Устанавливаем новую сессию
                HttpContext.Session.SetInt32("CustomerID", customer.CustomerID);
                HttpContext.Session.SetString("CustomerName", customer.FullName ?? "");
                HttpContext.Session.SetString("IsAdmin", customer.IsAdmin ? "true" : "false");

                Console.WriteLine($"Установлена сессия: CustomerID={customer.CustomerID}, Name={customer.FullName}");

                // Переносим корзину
                try
                {
                    _cartService.TransferCart(oldSessionId, customer.CustomerID);
                    Console.WriteLine("Корзина перенесена");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при переносе корзины");
                    Console.WriteLine($"Ошибка переноса корзины: {ex.Message}");
                }

                _logger.LogInformation($"Успешный вход: {email}, IP: {ipAddress}");
                TempData["Success"] = $"Добро пожаловать, {customer.FullName}!";

                Console.WriteLine("РЕДИРЕКТ на Home/Index");
                return RedirectToAction("Index", "Home");
            }
            else
            {
                Console.WriteLine("НЕВЕРНЫЙ ПАРОЛЬ или пользователь не найден");

                var blockUntil = DateTime.Now.AddMinutes(-_blockMinutes);
                var recentFailed = await _context.LoginAttempts
                    .Where(a => (a.Email == email || a.IpAddress == ipAddress)
                                && a.AttemptTime >= blockUntil
                                && !a.IsSuccessful)
                    .CountAsync();

                var remainingAttempts = _maxFailedAttempts - recentFailed;

                if (remainingAttempts > 0)
                {
                    ViewBag.Error = $"Неверный email или пароль. Осталось попыток: {remainingAttempts}";
                }
                else
                {
                    ViewBag.Error = $"Аккаунт заблокирован на {_blockMinutes} минут. Попробуйте позже.";
                }

                _logger.LogWarning($"Неудачный вход: {email}, IP: {ipAddress}");
                return View();
            }
        }

        // ==================== REGISTER (РЕГИСТРАЦИЯ) ====================

        [HttpGet]
        public IActionResult Register()
        {
            Console.WriteLine("=== GET Register вызван ===");

            if (IsAuthenticated())
                return RedirectToAction("Index", "Home");

            ViewBag.ReCaptchaSiteKey = _recaptchaSiteKey;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Customer customer, string confirmPassword)
        {
            Console.WriteLine($"=== POST Register вызван ===");
            Console.WriteLine($"Email: {customer.Email}");
            Console.WriteLine($"RecaptchaToken: {(string.IsNullOrEmpty(customer.RecaptchaToken) ? "ПУСТО" : customer.RecaptchaToken.Substring(0, Math.Min(20, customer.RecaptchaToken.Length)))}");

            ViewBag.ReCaptchaSiteKey = _recaptchaSiteKey;

            // Проверка reCAPTCHA
            var isRecaptchaValid = await VerifyRecaptcha(customer.RecaptchaToken);
            Console.WriteLine($"reCAPTCHA результат: {isRecaptchaValid}");

            if (!isRecaptchaValid)
            {
                ModelState.AddModelError("", "Подтвердите, что вы не робот");
                return View(customer);
            }

            // Проверка совпадения паролей
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
            Console.WriteLine($"Пользователь создан с ID: {customer.CustomerID}");

            // Автоматический вход после регистрации
            HttpContext.Session.SetInt32("CustomerID", customer.CustomerID);
            HttpContext.Session.SetString("CustomerName", customer.FullName ?? "");
            HttpContext.Session.SetString("IsAdmin", "false");

            _logger.LogInformation($"Новая регистрация: {customer.Email}, IP: {GetClientIpAddress()}");
            TempData["Success"] = $"Регистрация успешна! Добро пожаловать, {customer.FullName}!";

            Console.WriteLine("РЕДИРЕКТ на Home/Index");
            return RedirectToAction("Index", "Home");
        }

        // ==================== PROFILE (ПРОФИЛЬ) ====================

        [HttpGet]
        public IActionResult Profile()
        {
            if (!IsAuthenticated())
            {
                TempData["Error"] = "Для доступа необходимо авторизоваться";
                return RedirectToAction("Login");
            }

            var customerId = HttpContext.Session.GetInt32("CustomerID");
            var customer = _context.Customers
                .Include(c => c.Orders)
                .FirstOrDefault(c => c.CustomerID == customerId);

            if (customer == null)
            {
                TempData["Error"] = "Пользователь не найден";
                return RedirectToAction("Login");
            }

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateProfile(Customer updatedCustomer)
        {
            if (!IsAuthenticated())
                return RedirectToAction("Login");

            try
            {
                var customerId = HttpContext.Session.GetInt32("CustomerID");
                var existingCustomer = _context.Customers.Find(customerId);

                if (existingCustomer == null)
                {
                    TempData["Error"] = "Пользователь не найден";
                    return RedirectToAction("Login");
                }

                // Проверяем, не используется ли email другим пользователем
                if (_context.Customers.Any(c => c.Email == updatedCustomer.Email && c.CustomerID != customerId))
                {
                    ModelState.AddModelError("Email", "Этот email уже используется другим пользователем");
                    return View("Profile", existingCustomer);
                }

                // Обновляем разрешенные поля
                existingCustomer.FullName = updatedCustomer.FullName;
                existingCustomer.Email = updatedCustomer.Email;
                existingCustomer.Phone = updatedCustomer.Phone;
                existingCustomer.Address = updatedCustomer.Address;

                // Если указан новый пароль, хешируем
                if (!string.IsNullOrEmpty(updatedCustomer.Password))
                {
                    if (!IsPasswordStrong(updatedCustomer.Password))
                    {
                        ModelState.AddModelError("Password", "Пароль должен содержать минимум 8 символов, включая цифры и заглавные буквы");
                        return View("Profile", existingCustomer);
                    }
                    existingCustomer.Password = HashPassword(updatedCustomer.Password);
                }

                _context.Customers.Update(existingCustomer);
                _context.SaveChanges();

                // Обновляем имя в сессии
                HttpContext.Session.SetString("CustomerName", existingCustomer.FullName);

                TempData["Success"] = "Профиль успешно обновлен!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении профиля");
                TempData["Error"] = "Произошла ошибка при обновлении профиля";
                return RedirectToAction("Profile");
            }
        }

        // ==================== LOGOUT (ВЫХОД) ====================

        public IActionResult Logout()
        {
            try
            {
                HttpContext.Session.Clear();
                TempData["Success"] = "Вы успешно вышли из системы.";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выходе");
                TempData["Error"] = "Произошла ошибка при выходе из системы.";
                return RedirectToAction("Index", "Home");
            }
        }

        // ==================== КЛАСС ДЛЯ ОТВЕТА RECAPTCHA ====================

        private class RecaptchaResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("challenge_ts")]
            public string? ChallengeTs { get; set; }

            [JsonPropertyName("hostname")]
            public string? Hostname { get; set; }

            [JsonPropertyName("error-codes")]
            public List<string>? ErrorCodes { get; set; }
        }
    }
}