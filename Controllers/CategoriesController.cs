using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StationeryShop.Data;
using StationeryShop.Models;

namespace StationeryShop.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly StationeryDbContext _context;

        public CategoriesController(StationeryDbContext context)
        {
            _context = context;
        }

        // GET: Categories
        public IActionResult Index()
        {
            var categories = _context.Categories
                .Include(c => c.Products)
                .ToList();

            ViewBag.IsAdmin = IsAdmin();
            return View(categories);
        }

        // GET: Categories/Details/5
        public IActionResult Details(int? id)
        {
            if (id == null) return NotFound();

            var category = _context.Categories
                .Include(c => c.Products) // Загружаем товары
                .FirstOrDefault(m => m.CategoryID == id);

            if (category == null) return NotFound();

            return View(category);
        }
        // GET: Categories/DetailsModal/5 - для модального окна
        public IActionResult DetailsModal(int? id)
        {
            if (id == null) return NotFound();

            var category = _context.Categories
                .Include(c => c.Products)
                .FirstOrDefault(m => m.CategoryID == id);

            if (category == null) return NotFound();

            return PartialView("_CategoryModal", category);
        }
        // GET: Categories/Products/5 - товары в категории
        public IActionResult Products(int? id)
        {
            if (id == null) return NotFound();

            var category = _context.Categories
                .Include(c => c.Products)
                .FirstOrDefault(m => m.CategoryID == id);

            if (category == null) return NotFound();

            ViewBag.CategoryName = category.Name;
            ViewBag.IsAdmin = IsAdmin();
            ViewBag.IsAuthenticated = HttpContext.Session.GetInt32("CustomerID") != null;

            return View(category.Products.ToList());
        }

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("IsAdmin") == "true";
        }

        // GET: Categories/Create
        public IActionResult Create()
        {
            if (!IsAdmin()) return RedirectToAction("Index");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Category category)
        {
            if (!IsAdmin())
                return Forbid();

            if (ModelState.IsValid)
            {
                _context.Categories.Add(category);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Categories/Edit/5
        public IActionResult Edit(int? id)
        {
            if (!IsAdmin()) return RedirectToAction("Index");
            if (id == null) return NotFound();

            var category = _context.Categories.Find(id);
            if (category == null) return NotFound();

            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Category category)
        {
            if (!IsAdmin())
                return Forbid();

            if (id != category.CategoryID)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(category);
                    _context.SaveChanges();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Categories.Any(e => e.CategoryID == category.CategoryID))
                        return NotFound();
                    else
                        throw;
                }
            }
            return View(category);
        }

        // GET: Categories/Delete/5
        public IActionResult Delete(int? id)
        {
            if (!IsAdmin()) return RedirectToAction("Index");
            if (id == null) return NotFound();

            var category = _context.Categories.FirstOrDefault(m => m.CategoryID == id);
            if (category == null) return NotFound();

            return View(category);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Index");

            var category = _context.Categories.Find(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
                _context.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}