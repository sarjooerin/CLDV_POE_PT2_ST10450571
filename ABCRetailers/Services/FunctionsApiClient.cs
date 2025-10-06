using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ABCRetailers.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ABCRetailers.Services;

public class FunctionsApiClient : IFunctionsApi
{
    private readonly HttpClient _http;
    private readonly ILogger<FunctionsApiClient> _logger;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    // API Routes
    private const string CustomersRoute = "customers";
    private const string ProductsRoute = "products";
    private const string OrdersRoute = "orders";
    private const string UploadsRoute = "uploads/proof-of-payment";

    public FunctionsApiClient(IHttpClientFactory factory, ILogger<FunctionsApiClient> logger)
    {
        _http = factory.CreateClient("Functions");
        _logger = logger;
    }

    // ---------- Helpers ----------
    private static HttpContent JsonBody(object obj)
        => new StringContent(JsonSerializer.Serialize(obj, _json), Encoding.UTF8, "application/json");

    private async Task<T?> ReadJsonAsync<T>(HttpResponseMessage resp)
    {
        try
        {
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<T>(stream, _json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize JSON response into {Type}", typeof(T).Name);
            return default;
        }
    }

    private static void ValidateFile(IFormFile? file)
    {
        if (file is null || file.Length == 0) return;
        if (file.Length > 50 * 1024 * 1024)
            throw new InvalidOperationException("File exceeds maximum allowed size (50 MB).");
    }

    // ---------- Customers ----------
    public async Task<List<Customer>> GetCustomersAsync()
    {
        try
        {
            return await ReadJsonAsync<List<Customer>>(await _http.GetAsync(CustomersRoute)) ?? new List<Customer>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching customers");
            return new List<Customer>();
        }
    }

    public async Task<Customer?> GetCustomerAsync(string id)
    {
        try
        {
            var resp = await _http.GetAsync($"{CustomersRoute}/{id}");
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            return await ReadJsonAsync<Customer>(resp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching customer with ID {Id}", id);
            return null;
        }
    }

    public async Task<Customer> CreateCustomerAsync(Customer c)
    {
        try
        {
            var payload = new
            {
                c.Name,
                c.Surname,
                c.Username,
                c.Email,
                c.ShippingAddress
            };
            var resp = await _http.PostAsync(CustomersRoute, JsonBody(payload));
            resp.EnsureSuccessStatusCode();
            return await ReadJsonAsync<Customer>(resp) ?? c;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer {Name}", c.Name);
            throw;
        }
    }

    public async Task<Customer> UpdateCustomerAsync(string id, Customer c)
    {
        try
        {
            var payload = new
            {
                c.Name,
                c.Surname,
                c.Username,
                c.Email,
                c.ShippingAddress
            };
            var resp = await _http.PutAsync($"{CustomersRoute}/{id}", JsonBody(payload));
            resp.EnsureSuccessStatusCode();
            return await ReadJsonAsync<Customer>(resp) ?? c;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating customer {Id}", id);
            throw;
        }
    }

    public async Task DeleteCustomerAsync(string id)
    {
        try
        {
            var resp = await _http.DeleteAsync($"{CustomersRoute}/{id}");
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting customer {Id}", id);
            throw;
        }
    }

    // ---------- Products ----------
    public async Task<List<Product>> GetProductsAsync()
    {
        try
        {
            return await ReadJsonAsync<List<Product>>(await _http.GetAsync(ProductsRoute)) ?? new List<Product>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching products");
            return new List<Product>();
        }
    }

    public async Task<Product?> GetProductAsync(string id)
    {
        try
        {
            var resp = await _http.GetAsync($"{ProductsRoute}/{id}");
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            return await ReadJsonAsync<Product>(resp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching product with ID {Id}", id);
            return null;
        }
    }

    public async Task<Product> CreateProductAsync(Product p, IFormFile? imageFile)
    {
        try
        {
            ValidateFile(imageFile);
            using var form = BuildProductForm(p, imageFile);
            var resp = await _http.PostAsync(ProductsRoute, form);
            resp.EnsureSuccessStatusCode();
            return await ReadJsonAsync<Product>(resp) ?? p;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product {ProductName}", p.ProductName);
            throw;
        }
    }

    public async Task<Product> UpdateProductAsync(string id, Product p, IFormFile? imageFile)
    {
        try
        {
            ValidateFile(imageFile);
            using var form = BuildProductForm(p, imageFile);
            var resp = await _http.PutAsync($"{ProductsRoute}/{id}", form);
            resp.EnsureSuccessStatusCode();
            return await ReadJsonAsync<Product>(resp) ?? p;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {Id}", id);
            throw;
        }
    }

    public async Task DeleteProductAsync(string id)
    {
        try
        {
            var resp = await _http.DeleteAsync($"{ProductsRoute}/{id}");
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {Id}", id);
            throw;
        }
    }

    private static MultipartFormDataContent BuildProductForm(Product p, IFormFile? imageFile)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(p.ProductName), "ProductName" },
            { new StringContent(p.Description ?? string.Empty), "Description" },
            { new StringContent(p.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)), "Price" },
            { new StringContent(p.StockAvailable.ToString(System.Globalization.CultureInfo.InvariantCulture)), "StockAvailable" }
        };

        if (!string.IsNullOrWhiteSpace(p.ImageUrl))
            form.Add(new StringContent(p.ImageUrl), "ImageUrl");

        if (imageFile is not null && imageFile.Length > 0)
        {
            var file = new StreamContent(imageFile.OpenReadStream());
            file.Headers.ContentType = new MediaTypeHeaderValue(imageFile.ContentType ?? "application/octet-stream");
            form.Add(file, "ImageFile", imageFile.FileName);
        }

        return form;
    }

    // ---------- Orders ----------
    public async Task<List<Order>> GetOrdersAsync()
    {
        try
        {
            var dtos = await ReadJsonAsync<List<OrderDto>>(await _http.GetAsync(OrdersRoute)) ?? new List<OrderDto>();
            return dtos.Select(ToOrder).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching orders");
            return new List<Order>();
        }
    }

    public async Task<Order?> GetOrderAsync(string id)
    {
        try
        {
            var resp = await _http.GetAsync($"{OrdersRoute}/{id}");
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            var dto = await ReadJsonAsync<OrderDto>(resp);
            return dto is null ? null : ToOrder(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching order {Id}", id);
            return null;
        }
    }

    public async Task<Order> CreateOrderAsync(string customerId, string productId, int quantity)
    {
        try
        {
            var payload = new { customerId, productId, quantity };
            var resp = await _http.PostAsync(OrdersRoute, JsonBody(payload));
            resp.EnsureSuccessStatusCode();
            var dto = await ReadJsonAsync<OrderDto>(resp);
            return dto is null ? throw new InvalidOperationException("Failed to create order.") : ToOrder(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for Customer {CustomerId}", customerId);
            throw;
        }
    }

    public async Task UpdateOrderStatusAsync(string id, string newStatus)
    {
        try
        {
            var payload = new { status = newStatus };
            var resp = await _http.PatchAsync($"{OrdersRoute}/{id}/status", JsonBody(payload));
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status for {Id}", id);
            throw;
        }
    }

    public async Task DeleteOrderAsync(string id)
    {
        try
        {
            var resp = await _http.DeleteAsync($"{OrdersRoute}/{id}");
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting order {Id}", id);
            throw;
        }
    }

    // ---------- Uploads ----------
    public async Task<string> UploadProofOfPaymentAsync(IFormFile file, string? orderId, string? customerName)
    {
        try
        {
            ValidateFile(file);
            using var form = new MultipartFormDataContent();
            var sc = new StreamContent(file.OpenReadStream());
            sc.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
            form.Add(sc, "ProofOfPayment", file.FileName);

            if (!string.IsNullOrWhiteSpace(orderId)) form.Add(new StringContent(orderId), "OrderId");
            if (!string.IsNullOrWhiteSpace(customerName)) form.Add(new StringContent(customerName), "CustomerName");

            var resp = await _http.PostAsync(UploadsRoute, form);
            resp.EnsureSuccessStatusCode();

            var doc = await ReadJsonAsync<Dictionary<string, string>>(resp);
            return doc?.TryGetValue("fileName", out var name) == true ? name : file.FileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading proof of payment for order {OrderId}", orderId);
            throw;
        }
    }

    // ---------- Mapping ----------
    private static Order ToOrder(OrderDto d)
    {
        var status = Enum.TryParse<OrderStatus>(d.Status, true, out var s) ? s : OrderStatus.Submitted;
        return new Order
        {
            Id = d.Id,
            CustomerId = d.CustomerId,
            ProductId = d.ProductId,
            ProductName = d.ProductName,
            Quantity = d.Quantity,
            UnitPrice = d.UnitPrice,
            OrderDateUtc = d.OrderDateUtc,
            Status = status
        };
    }

    private sealed record OrderDto(
        string Id,
        string CustomerId,
        string ProductId,
        string ProductName,
        int Quantity,
        decimal UnitPrice,
        DateTimeOffset OrderDateUtc,
        string Status
    );
}

// Minimal PATCH extension
internal static class HttpClientPatchExtensions
{
    public static Task<HttpResponseMessage> PatchAsync(this HttpClient client, string requestUri, HttpContent content)
        => client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, requestUri) { Content = content });
}
