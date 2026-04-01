using System.Text.Json;
using StationeryShop.Models;

namespace StationeryShop.Services
{
    public class CartService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CartService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private string GetUserId()
        {
            var customerId = _httpContextAccessor.HttpContext.Session.GetInt32("CustomerID");
            return customerId?.ToString() ?? _httpContextAccessor.HttpContext.Session.Id;
        }

        private List<CartItem> GetCartFromSession()
        {
            var session = _httpContextAccessor.HttpContext.Session;
            var cartJson = session.GetString($"Cart_{GetUserId()}");

            if (string.IsNullOrEmpty(cartJson))
                return new List<CartItem>();

            return JsonSerializer.Deserialize<List<CartItem>>(cartJson) ?? new List<CartItem>();
        }

        private void SaveCartToSession(List<CartItem> cart)
        {
            var session = _httpContextAccessor.HttpContext.Session;
            var cartJson = JsonSerializer.Serialize(cart);
            session.SetString($"Cart_{GetUserId()}", cartJson);
        }

        public void AddToCart(Product product, int quantity = 1)
        {
            var cart = GetCartFromSession();
            var existingItem = cart.FirstOrDefault(item => item.ProductId == product.ProductID);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.Add(new CartItem
                {
                    ProductId = product.ProductID,
                    ProductName = product.Name,
                    Price = product.Price,
                    Quantity = quantity,
                    SessionId = GetUserId()
                });
            }

            SaveCartToSession(cart);
        }

        public void RemoveFromCart(int productId)
        {
            var cart = GetCartFromSession();
            var itemToRemove = cart.FirstOrDefault(item => item.ProductId == productId);

            if (itemToRemove != null)
            {
                cart.Remove(itemToRemove);
                SaveCartToSession(cart);
            }
        }

        public void UpdateQuantity(int productId, int quantity)
        {
            var cart = GetCartFromSession();
            var item = cart.FirstOrDefault(item => item.ProductId == productId);

            if (item != null)
            {
                if (quantity <= 0)
                {
                    cart.Remove(item);
                }
                else
                {
                    item.Quantity = quantity;
                }
                SaveCartToSession(cart);
            }
        }

        public List<CartItem> GetCartItems()
        {
            return GetCartFromSession();
        }

        public decimal GetTotalPrice()
        {
            var cart = GetCartFromSession();
            return cart.Sum(item => item.TotalPrice);
        }

        public int GetTotalItemsCount()
        {
            var cart = GetCartFromSession();
            return cart.Sum(item => item.Quantity);
        }

        public void ClearCart()
        {
            SaveCartToSession(new List<CartItem>());
        }

        // Новый метод для переноса корзины при авторизации
        // Новый метод для переноса корзины при авторизации
        public void TransferCart(string fromSessionId, int toCustomerId)
        {
            try
            {
                var session = _httpContextAccessor.HttpContext.Session;
                var oldCartJson = session.GetString($"Cart_{fromSessionId}");

                if (!string.IsNullOrEmpty(oldCartJson))
                {
                    var oldCart = JsonSerializer.Deserialize<List<CartItem>>(oldCartJson) ?? new List<CartItem>();
                    if (oldCart.Any())
                    {
                        // Обновляем SessionId для всех элементов
                        foreach (var item in oldCart)
                        {
                            item.SessionId = toCustomerId.ToString();
                        }

                        var newCartJson = JsonSerializer.Serialize(oldCart);
                        session.SetString($"Cart_{toCustomerId}", newCartJson);

                        // Очищаем старую корзину
                        session.Remove($"Cart_{fromSessionId}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем выполнение
                Console.WriteLine($"Ошибка при переносе корзины: {ex.Message}");
            }
        }
    }
}