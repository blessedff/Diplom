using Microsoft.AspNetCore.Mvc;
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

        public IActionResult Index()
        {
            return View();
        }
    }
}