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

        public AccountController(StationeryDbContext context, CartService cartService)
        {
            _context = context;
            _cartService = cartService;
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
                // Проверяем входные параметры
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    ViewBag.Error = "Email и пароль обязательны для заполнения";
                    return View();
                }

                var customer = _context.Customers
                    .FirstOrDefault(c => c.Email == email && c.Password == password);

                if (customer != null)
                {
                    // Сохраняем старый ID сессии перед аутентификацией
                    var oldSessionId = HttpContext.Session.Id;

                    // Устанавливаем данные сессии
                    HttpContext.Session.SetInt32("CustomerID", customer.CustomerID);
                    HttpContext.Session.SetString("CustomerName", customer.FullName ?? "");
                    HttpContext.Session.SetString("IsAdmin", customer.IsAdmin ? "true" : "false");

                    // Переносим корзину из анонимной сессии в сессию пользователя
                    try
                    {
                        _cartService.TransferCart(oldSessionId, customer.CustomerID);
                    }
                    catch (Exception ex)
                    {
                        // Логируем ошибку, но не прерываем процесс входа
                        Console.WriteLine($"Ошибка при переносе корзины: {ex.Message}");
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
                // Логируем ошибку для отладки
                Console.WriteLine($"Ошибка при входе: {ex.Message}");
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
        public IActionResult Register(Customer customer)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(customer);

                // Проверяем, нет ли уже пользователя с таким email
                if (_context.Customers.Any(c => c.Email == customer.Email))
                {
                    ModelState.AddModelError("Email", "Пользователь с таким email уже существует");
                    return View(customer);
                }

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
                Console.WriteLine($"Ошибка при регистрации: {ex.Message}");
                ViewBag.Error = "Произошла ошибка при регистрации. Попробуйте еще раз.";
                return View(customer);
            }
        }

        // GET: Account/Profile - Личный кабинет
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

        // POST: Account/UpdateProfile - Обновление профиля
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

                // Если указан новый пароль, обновляем его
                if (!string.IsNullOrEmpty(updatedCustomer.Password))
                {
                    existingCustomer.Password = updatedCustomer.Password;
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
                Console.WriteLine($"Ошибка при обновлении профиля: {ex.Message}");
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
                Console.WriteLine($"Ошибка при выходе: {ex.Message}");
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
    }
}