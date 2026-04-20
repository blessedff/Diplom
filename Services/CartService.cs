using Microsoft.EntityFrameworkCore;
using StationeryShop.Data;
using StationeryShop.Models;
using System.Text.Json;

namespace StationeryShop.Services
{
    public class CartService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly StationeryDbContext _context;

        public CartService(IHttpContextAccessor httpContextAccessor, StationeryDbContext context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        private bool IsAuthenticated()
        {
            return _httpContextAccessor.HttpContext?.Session.GetInt32("CustomerID") != null;
        }

        private int GetCustomerId()
        {
            return _httpContextAccessor.HttpContext?.Session.GetInt32("CustomerID") ?? 0;
        }

        // ==================== ОСНОВНЫЕ МЕТОДЫ ====================

        // Получить все товары в корзине
        public List<CartItem> GetCartItems()
        {
            if (!IsAuthenticated())
                return new List<CartItem>();

            var customerId = GetCustomerId();

            var cartItems = _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.CustomerID == customerId)
                .ToList();

            return cartItems.Select(c => new CartItem
            {
                CartItemId = c.Id,
                ProductId = c.ProductID,
                ProductName = c.Product?.Name ?? "Товар",
                Price = c.Product?.Price ?? 0,
                Quantity = c.Quantity,
                SessionId = customerId.ToString()
            }).ToList();
        }

        // Добавить товар в корзину
        public void AddToCart(Product product, int quantity = 1)
        {
            if (!IsAuthenticated())
                return;

            var customerId = GetCustomerId();

            var existingItem = _context.CartItems
                .FirstOrDefault(c => c.CustomerID == customerId && c.ProductID == product.ProductID);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
                _context.CartItems.Update(existingItem);
            }
            else
            {
                _context.CartItems.Add(new CartTable
                {
                    CustomerID = customerId,
                    ProductID = product.ProductID,
                    Quantity = quantity,
                    AddedDate = DateTime.Now
                });
            }

            _context.SaveChanges();
        }

        // Обновить количество товара
        public void UpdateQuantity(int productId, int quantity)
        {
            if (!IsAuthenticated())
                return;

            var customerId = GetCustomerId();

            var item = _context.CartItems
                .FirstOrDefault(c => c.CustomerID == customerId && c.ProductID == productId);

            if (item != null)
            {
                if (quantity <= 0)
                {
                    _context.CartItems.Remove(item);
                }
                else
                {
                    item.Quantity = quantity;
                    _context.CartItems.Update(item);
                }
                _context.SaveChanges();
            }
        }

        // Удалить товар из корзины
        public void RemoveFromCart(int productId)
        {
            if (!IsAuthenticated())
                return;

            var customerId = GetCustomerId();

            var item = _context.CartItems
                .FirstOrDefault(c => c.CustomerID == customerId && c.ProductID == productId);

            if (item != null)
            {
                _context.CartItems.Remove(item);
                _context.SaveChanges();
            }
        }

        // Очистить всю корзину
        public void ClearCart()
        {
            if (!IsAuthenticated())
                return;

            var customerId = GetCustomerId();

            var items = _context.CartItems
                .Where(c => c.CustomerID == customerId)
                .ToList();

            if (items.Any())
            {
                _context.CartItems.RemoveRange(items);
                _context.SaveChanges();
            }
        }

        // Получить общую сумму корзины
        public decimal GetTotalPrice()
        {
            if (!IsAuthenticated())
                return 0;

            var customerId = GetCustomerId();

            var total = _context.CartItems
                .Where(c => c.CustomerID == customerId)
                .Include(c => c.Product)
                .Sum(c => c.Quantity * (c.Product != null ? c.Product.Price : 0));

            return total;
        }

        // Получить общее количество товаров в корзине
        public int GetTotalItemsCount()
        {
            if (!IsAuthenticated())
                return 0;

            var customerId = GetCustomerId();

            var count = _context.CartItems
                .Where(c => c.CustomerID == customerId)
                .Sum(c => c.Quantity);

            return count;
        }
    }
}