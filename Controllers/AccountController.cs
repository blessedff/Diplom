// AccountController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StationeryShop.Data;
using StationeryShop.Models;
using StationeryShop.Services;

namespace StationeryShop.Controllers
{
    public class AccountController : Controller
    {
        private readonly StationeryDbContext _context;
        private readonly CartService _cartService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(StationeryDbContext context, CartService cartService, ILogger<AccountController> logger)
        {
            _context = context;
            _cartService = cartService;
            _logger = logger;
        }

        // GET: Account/Login
        public IActionResult Login()
        {
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string email, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    ViewBag.Error = "Email и пароль обязательны для заполнения";
                    return View();
                }

                var customer = _context.Customers
                    .FirstOrDefault(c => c.Email == email);

                // Проверяем пароль с помощью BCrypt
                if (customer != null && VerifyPassword(password, customer.Password))
                {
                    var oldSessionId = HttpContext.Session.Id;

                    HttpContext.Session.SetInt32("CustomerID", customer.CustomerID);
                    HttpContext.Session.SetString("CustomerName", customer.FullName ?? "");
                    HttpContext.Session.SetString("IsAdmin", customer.IsAdmin ? "true" : "false");

                    try
                    {
                        _cartService.TransferCart(oldSessionId, customer.CustomerID);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при переносе корзины");
                    }

                    TempData["Success"] = $"Добро пожаловать, {customer.FullName}!";
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ViewBag.Error = "Неверный email или пароль";
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при входе");
                ViewBag.Error = "Произошла ошибка при входе. Попробуйте еще раз.";
                return View();
            }
        }

        // GET: Account/Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(Customer customer, string confirmPassword)
        {
            try
            {
                // Проверяем, что пароль и подтверждение совпадают
                if (customer.Password != confirmPassword)
                {
                    ModelState.AddModelError("", "Пароли не совпадают");
                    return View(customer);
                }

                if (!ModelState.IsValid)
                    return View(customer);

                // Проверяем, нет ли уже пользователя с таким email
                if (_context.Customers.Any(c => c.Email == customer.Email))
                {
                    ModelState.AddModelError("Email", "Пользователь с таким email уже существует");
                    return View(customer);
                }

                // Хешируем пароль перед сохранением
                customer.Password = HashPassword(customer.Password);
                customer.IsAdmin = false; // обычный пользователь

                _context.Customers.Add(customer);
                _context.SaveChanges();

                // Автоматически логиним после регистрации
                HttpContext.Session.SetInt32("CustomerID", customer.CustomerID);
                HttpContext.Session.SetString("CustomerName", customer.FullName ?? "");
                HttpContext.Session.SetString("IsAdmin", "false");

                TempData["Success"] = $"Регистрация успешна! Добро пожаловать, {customer.FullName}!";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при регистрации");
                ViewBag.Error = "Произошла ошибка при регистрации. Попробуйте еще раз.";
                return View(customer);
            }
        }

        // GET: Account/Profile
        public IActionResult Profile()
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

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

        // POST: Account/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateProfile(Customer updatedCustomer)
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

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

                // Обновляем только разрешенные поля
                existingCustomer.FullName = updatedCustomer.FullName;
                existingCustomer.Email = updatedCustomer.Email;
                existingCustomer.Phone = updatedCustomer.Phone;
                existingCustomer.Address = updatedCustomer.Address;

                // Если указан новый пароль, хешируем и обновляем его
                if (!string.IsNullOrEmpty(updatedCustomer.Password))
                {
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

        private bool IsAuthenticated()
        {
            return HttpContext.Session.GetInt32("CustomerID") != null;
        }

        private IActionResult RedirectToLogin()
        {
            TempData["Error"] = "Для доступа необходимо авторизоваться";
            return RedirectToAction("Login");
        }

        // ==================== МЕТОДЫ ДЛЯ РАБОТЫ С ПАРОЛЯМИ ====================

        /// <summary>
        /// Хеширование пароля с помощью BCrypt
        /// </summary>
        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        /// <summary>
        /// Проверка пароля с хешем
        /// </summary>
        private bool VerifyPassword(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}