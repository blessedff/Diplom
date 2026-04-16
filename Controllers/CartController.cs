using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StationeryShop.Data;
using StationeryShop.Models;
using StationeryShop.Services;

namespace StationeryShop.Controllers
{
    public class CartController : Controller
    {
        private readonly StationeryDbContext _context;
        private readonly CartService _cartService;

        public CartController(StationeryDbContext context, CartService cartService)
        {
            _context = context;
            _cartService = cartService;
        }

        private bool IsAuthenticated()
        {
            return HttpContext.Session.GetInt32("CustomerID") != null;
        }

        private IActionResult RedirectToLogin()
        {
            TempData["Error"] = "Для доступа к корзине необходимо авторизоваться";
            return RedirectToAction("Login", "Account");
        }

        // GET: Cart
        public IActionResult Index()
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

            var cartItems = _cartService.GetCartItems();
            ViewBag.TotalPrice = _cartService.GetTotalPrice();
            return View(cartItems);
        }

        // POST: Cart/AddToCart
        [HttpPost]
        public IActionResult AddToCart(int productId, int quantity = 1)
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

            var product = _context.Products.FirstOrDefault(p => p.ProductID == productId);

            if (product == null)
            {
                return NotFound();
            }

            if (product.StockQuantity < quantity)
            {
                TempData["Error"] = $"Недостаточно товара на складе. Доступно: {product.StockQuantity}";
                return RedirectToAction("Index", "Products");
            }

            _cartService.AddToCart(product, quantity);
            TempData["Success"] = "Товар добавлен в корзину!";

            return RedirectToAction("Index", "Products");
        }

        [HttpPost]
        public async Task<IActionResult> AddToCartAjax(int productId, int quantity = 1)
        {
            if (!IsAuthenticated())
                return Json(new { success = false, message = "Для добавления в корзину необходимо авторизоваться" });

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == productId);

            if (product == null)
                return Json(new { success = false, message = "Товар не найден" });

            if (product.StockQuantity < quantity)
                return Json(new { success = false, message = $"Недостаточно товара на складе. Доступно: {product.StockQuantity}" });

            _cartService.AddToCart(product, quantity);

            return Json(new { success = true, message = "Товар добавлен в корзину" });
        }

        // POST: Cart/UpdateQuantity
        [HttpPost]
        public IActionResult UpdateQuantity(int productId, int quantity)
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

            var product = _context.Products.FirstOrDefault(p => p.ProductID == productId);

            if (product != null && quantity > 0 && quantity <= product.StockQuantity)
            {
                _cartService.UpdateQuantity(productId, quantity);
                TempData["Success"] = "Количество обновлено!";
            }
            else
            {
                TempData["Error"] = "Некорректное количество товара";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Cart/RemoveFromCart
        [HttpPost]
        public IActionResult RemoveFromCart(int productId)
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

            _cartService.RemoveFromCart(productId);
            TempData["Success"] = "Товар удален из корзины!";

            return RedirectToAction(nameof(Index));
        }

        // POST: Cart/ClearCart
        [HttpPost]
        public IActionResult ClearCart()
        {
            if (!IsAuthenticated())
                return RedirectToLogin();

            _cartService.ClearCart();
            TempData["Success"] = "Корзина очищена!";

            return RedirectToAction(nameof(Index));
        }

        // GET: Cart/GetCartSummary (для частичного представления)
        public IActionResult GetCartSummary()
        {
            if (!IsAuthenticated())
                return Json(new { totalItems = 0, totalPrice = 0 });

            var totalItems = _cartService.GetTotalItemsCount();
            var totalPrice = _cartService.GetTotalPrice();

            return Json(new { totalItems, totalPrice });
        }
    }
}