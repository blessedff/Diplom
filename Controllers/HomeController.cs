using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StationeryShop.Data;
using StationeryShop.Models;

namespace StationeryShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly StationeryDbContext _context;

        public HomeController(StationeryDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var reviews = await _context.Reviews
                .Include(r => r.Customer)
                .Where(r => r.IsApproved == true && r.IsRejected == false && r.ProductId == null)
                .OrderByDescending(r => r.CreatedAt)
                .Take(100)
                .ToListAsync();

            return View(reviews);
        }
    }
}