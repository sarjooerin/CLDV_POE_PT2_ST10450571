using Microsoft.AspNetCore.Mvc;
using ABCRetailers.Models;
using ABCRetailers.Models.ViewModels;
using ABCRetailers.Services;

namespace ABCRetailers.Controllers
{
    public class OrderController : Controller
    {
        private readonly IFunctionsApi _api;
        private readonly ILogger<OrderController> _logger;

        public OrderController(IFunctionsApi api, ILogger<OrderController> logger)
        {
            _api = api;
            _logger = logger;
        }

        // ---------------- LIST ----------------
        public async Task<IActionResult> Index()
        {
            try
            {
                var orders = await _api.GetOrdersAsync();
                return View(orders.OrderByDescending(o => o.OrderDateUtc).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading orders");
                TempData["Error"] = "Unable to load orders.";
                return View(new List<Order>());
            }
        }

        // ---------------- CREATE (GET) ----------------
        public async Task<IActionResult> Create()
        {
            var vm = new OrderCreateViewModel();
            await PopulateDropdowns(vm);
            return View(vm);
        }

        // ---------------- CREATE (POST) ----------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrderCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(model);
                return View(model);
            }

            try
            {
                var customer = await _api.GetCustomerAsync(model.CustomerId);
                var product = await _api.GetProductAsync(model.ProductId);

                if (customer is null || product is null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid customer or product selected.");
                    await PopulateDropdowns(model);
                    return View(model);
                }

                if (product.StockAvailable < model.Quantity)
                {
                    ModelState.AddModelError("Quantity", $"Insufficient stock. Available: {product.StockAvailable}");
                    await PopulateDropdowns(model);
                    return View(model);
                }

                var saved = await _api.CreateOrderAsync(model.CustomerId, model.ProductId, model.Quantity);
                TempData["Success"] = "Order created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                ModelState.AddModelError(string.Empty, $"Error creating order: {ex.Message}");
                await PopulateDropdowns(model);
                return View(model);
            }
        }

        // ---------------- DETAILS ----------------
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            try
            {
                var order = await _api.GetOrderAsync(id);
                return order is null ? NotFound() : View(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading order details");
                TempData["Error"] = "Unable to load order details.";
                return RedirectToAction(nameof(Index));
            }
        }

        // ---------------- EDIT (GET) ----------------
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            try
            {
                var order = await _api.GetOrderAsync(id);
                return order is null ? NotFound() : View(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading order for edit");
                TempData["Error"] = "Unable to load order for edit.";
                return RedirectToAction(nameof(Index));
            }
        }

        // ---------------- EDIT (POST) ----------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Order posted)
        {
            if (!ModelState.IsValid) return View(posted);

            try
            {
                await _api.UpdateOrderStatusAsync(posted.Id, posted.Status.ToString());
                TempData["Success"] = "Order updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order");
                ModelState.AddModelError(string.Empty, $"Error updating order: {ex.Message}");
                return View(posted);
            }
        }

        // ---------------- DELETE ----------------
        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid order ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _api.DeleteOrderAsync(id);
                TempData["Success"] = "Order deleted successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting order");
                TempData["Error"] = $"Error deleting order: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ---------------- AJAX: price/stock lookup ----------------
        [HttpGet]
        public async Task<JsonResult> GetProductPrice(string productId)
        {
            try
            {
                var product = await _api.GetProductAsync(productId);
                if (product != null)
                {
                    return Json(new
                    {
                        success = true,
                        price = product.Price,
                        stock = product.StockAvailable,
                        productName = product.ProductName
                    });
                }
                return Json(new { success = false });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        // ---------------- AJAX: status update ----------------
        [HttpPost]
        public async Task<JsonResult> UpdateOrderStatus(string id, string newStatus)
        {
            try
            {
                await _api.UpdateOrderStatusAsync(id, newStatus);
                return Json(new { success = true, message = $"Order status updated to {newStatus}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ---------------- Helper ----------------
        private async Task PopulateDropdowns(OrderCreateViewModel model)
        {
            model.Customers = await _api.GetCustomersAsync();
            model.Products = await _api.GetProductsAsync();
        }
    }
}
