using Microsoft.AspNetCore.Mvc;
using ABCRetailers.Models;
using ABCRetailers.Services;

namespace ABCRetailers.Controllers;

public class CustomerController : Controller
{
    private readonly IFunctionsApi _api;
    public CustomerController(IFunctionsApi api) => _api = api;

    // ---------------- List ----------------
    public async Task<IActionResult> Index()
    {
        try
        {
            var customers = await _api.GetCustomersAsync();
            return View(customers);
        }
        catch
        {
            TempData["Error"] = "Unable to load customers.";
            return View(new List<Customer>());
        }
    }

    // ---------------- Create ----------------
    public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        if (!ModelState.IsValid) return View(customer);

        try
        {
            await _api.CreateCustomerAsync(customer);
            TempData["Success"] = "Customer created successfully!";
            return RedirectToAction(nameof(Index));
        }
        catch (HttpRequestException ex)
        {
            ModelState.AddModelError("", $"Error creating customer: {ex.Message}");
            return View(customer);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Unexpected error: {ex.Message}");
            return View(customer);
        }
    }

    // ---------------- Edit ----------------
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var customer = await _api.GetCustomerAsync(id);
        return customer is null ? NotFound() : View(customer);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Customer customer)
    {
        if (!ModelState.IsValid) return View(customer);

        try
        {
            await _api.UpdateCustomerAsync(customer.Id, customer);
            TempData["Success"] = "Customer updated successfully!";
            return RedirectToAction(nameof(Index));
        }
        catch (HttpRequestException ex)
        {
            ModelState.AddModelError("", $"Error updating customer: {ex.Message}");
            return View(customer);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Unexpected error: {ex.Message}");
            return View(customer);
        }
    }

    // ---------------- Delete ----------------
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["Error"] = "Invalid customer ID.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _api.DeleteCustomerAsync(id);
            TempData["Success"] = "Customer deleted successfully!";
        }
        catch (HttpRequestException ex)
        {
            TempData["Error"] = $"Error deleting customer: {ex.Message}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Unexpected error: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
