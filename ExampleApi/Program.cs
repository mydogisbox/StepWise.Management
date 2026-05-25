using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddSingleton<Store>();
builder.Services.AddHostedService<OrderProcessor>();

var app = builder.Build();
app.UseCors();

var store = app.Services.GetRequiredService<Store>();
var json = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

string? Auth(HttpContext ctx)
{
    var h = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (h is null || !h.StartsWith("Bearer ")) return null;
    return store.Tokens.TryGetValue(h[7..], out var uid) ? uid : null;
}

bool AdminAuth(HttpContext ctx) =>
    ctx.Request.Headers.TryGetValue("X-Admin-Key", out var k) && k == "admin-secret";

IResult Err401() => Results.Json(new { error = "Unauthorized" }, json, statusCode: 401);
IResult Err403() => Results.Json(new { error = "Forbidden" }, json, statusCode: 403);
IResult Err404(string msg) => Results.Json(new { error = msg }, json, statusCode: 404);
IResult Err422(string msg) => Results.Json(new { error = msg }, json, statusCode: 422);

// ── Auth ──────────────────────────────────────────────────────────────────────

app.MapPost("/auth/login", (LoginRequest req) =>
{
    var user = store.Users.FirstOrDefault(u =>
        string.Equals(u.Email, req.Email, StringComparison.OrdinalIgnoreCase) &&
        u.Password == req.Password);
    if (user is null) return Err422("Invalid email or password.");
    var token = Guid.NewGuid().ToString("N");
    store.Tokens[token] = user.Id;
    return Results.Json(new { token, userId = user.Id, name = user.Name }, json);
});

// ── Products ──────────────────────────────────────────────────────────────────

app.MapGet("/products", (string? category, bool? inStock) =>
{
    List<Product> snapshot;
    lock (store.Products) snapshot = store.Products.ToList();
    var q = snapshot.AsEnumerable();
    if (category is not null)
        q = q.Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase));
    if (inStock is true)
        q = q.Where(p => p.Stock > 0);
    return Results.Json(q.Select(p => new { p.Id, p.Name, p.Category, p.Price, p.Stock }), json);
});

app.MapGet("/products/{id}", (string id) =>
{
    Product? p;
    lock (store.Products) p = store.Products.FirstOrDefault(p => p.Id == id);
    return p is null
        ? Err404($"Product '{id}' not found.")
        : Results.Json(new { p.Id, p.Name, p.Category, p.Price, p.Stock, p.Description }, json);
});

// ── Cart ──────────────────────────────────────────────────────────────────────

app.MapGet("/cart", (HttpContext ctx) =>
{
    var uid = Auth(ctx);
    if (uid is null) return Err401();
    var items = store.GetCart(uid);
    List<Product> products;
    lock (store.Products) products = store.Products.ToList();
    var lines = items.Select(i =>
    {
        var p = products.First(p => p.Id == i.ProductId);
        return new { i.ProductId, p.Name, p.Price, i.Quantity, lineTotal = Math.Round(p.Price * i.Quantity, 2) };
    }).ToList();
    return Results.Json(new { items = lines, total = lines.Sum(l => l.lineTotal) }, json);
});

app.MapPost("/cart/items", (HttpContext ctx, AddCartItemRequest req) =>
{
    var uid = Auth(ctx);
    if (uid is null) return Err401();
    Product? p;
    lock (store.Products) p = store.Products.FirstOrDefault(x => x.Id == req.ProductId);
    if (p is null) return Err404($"Product '{req.ProductId}' not found.");
    if (p.Stock == 0) return Err422($"'{p.Name}' is out of stock.");
    if (req.Quantity <= 0) return Err422("Quantity must be at least 1.");
    if (req.Quantity > p.Stock) return Err422($"Only {p.Stock} units of '{p.Name}' available.");
    store.AddToCart(uid, req.ProductId, req.Quantity);
    return Results.Json(new { productId = req.ProductId, quantity = req.Quantity }, json, statusCode: 201);
});

app.MapMethods("/cart/items/{productId}", new[] { "PATCH" }, (HttpContext ctx, string productId, UpdateCartItemRequest req) =>
{
    var uid = Auth(ctx);
    if (uid is null) return Err401();
    if (req.Quantity <= 0) return Err422("Quantity must be at least 1.");
    var items = store.GetCart(uid);
    lock (items)
    {
        var idx = items.FindIndex(i => i.ProductId == productId);
        if (idx < 0) return Err404($"Product '{productId}' not in cart.");
        items[idx] = items[idx] with { Quantity = req.Quantity };
    }
    return Results.Json(new { productId, quantity = req.Quantity }, json);
});

app.MapDelete("/cart/items/{productId}", (HttpContext ctx, string productId) =>
{
    var uid = Auth(ctx);
    if (uid is null) return Err401();
    var items = store.GetCart(uid);
    lock (items)
    {
        var item = items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null) return Err404($"Product '{productId}' not in cart.");
        items.Remove(item);
    }
    return Results.NoContent();
});

// ── Orders ────────────────────────────────────────────────────────────────────

app.MapPost("/orders", (HttpContext ctx, PlaceOrderRequest? req) =>
{
    var uid = Auth(ctx);
    if (uid is null) return Err401();
    var cart = store.GetCart(uid);
    if (!cart.Any()) return Err422("Cart is empty.");

    decimal discountPct = 0;
    if (req?.VoucherCode is not null && !store.Vouchers.TryGetValue(req.VoucherCode, out discountPct))
        return Err422($"Voucher code '{req.VoucherCode}' is not valid.");

    List<OrderItem> lineItems;
    lock (store.Products)
    {
        foreach (var ci in cart)
        {
            var idx = store.Products.FindIndex(p => p.Id == ci.ProductId);
            if (idx < 0) return Err422($"Product '{ci.ProductId}' no longer exists.");
            if (store.Products[idx].Stock < ci.Quantity)
                return Err422($"Insufficient stock for '{store.Products[idx].Name}'.");
        }
        lineItems = cart.Select(ci =>
        {
            var idx = store.Products.FindIndex(p => p.Id == ci.ProductId);
            var p = store.Products[idx];
            store.Products[idx] = p with { Stock = p.Stock - ci.Quantity };
            return new OrderItem(p.Id, p.Name, p.Price, ci.Quantity);
        }).ToList();
    }

    var subtotal = Math.Round(lineItems.Sum(i => i.Price * i.Quantity), 2);
    var discount = Math.Round(subtotal * discountPct / 100, 2);
    var now = DateTimeOffset.UtcNow;
    var order = new Order(
        Id: "ord_" + Guid.NewGuid().ToString("N")[..12],
        UserId: uid,
        Status: "pending",
        Subtotal: subtotal,
        Discount: discount,
        Total: subtotal - discount,
        VoucherCode: req?.VoucherCode,
        Items: lineItems,
        CreatedAt: now,
        StatusChangedAt: now);

    store.Orders[order.Id] = order;
    store.Carts[uid] = [];

    return Results.Json(new
    {
        id = order.Id, order.Status, order.Subtotal,
        order.Discount, order.Total, order.VoucherCode, order.CreatedAt
    }, json, statusCode: 201);
});

app.MapGet("/orders", (HttpContext ctx, string? status) =>
{
    var uid = Auth(ctx);
    if (uid is null) return Err401();
    var q = store.Orders.Values.Where(o => o.UserId == uid);
    if (status is not null) q = q.Where(o => o.Status == status);
    return Results.Json(
        q.OrderByDescending(o => o.CreatedAt).Select(o => new { o.Id, o.Status, o.Total, o.CreatedAt }),
        json);
});

app.MapGet("/orders/{id}", (HttpContext ctx, string id) =>
{
    var uid = Auth(ctx);
    if (uid is null) return Err401();
    if (!store.Orders.TryGetValue(id, out var o)) return Err404($"Order '{id}' not found.");
    if (o.UserId != uid) return Err403();
    return Results.Json(new
    {
        o.Id, o.Status, o.Subtotal, o.Discount, o.Total,
        o.VoucherCode, o.CreatedAt, itemCount = o.Items.Count
    }, json);
});

app.MapGet("/orders/{id}/items", (HttpContext ctx, string id) =>
{
    var uid = Auth(ctx);
    if (uid is null) return Err401();
    if (!store.Orders.TryGetValue(id, out var o)) return Err404($"Order '{id}' not found.");
    if (o.UserId != uid) return Err403();
    return Results.Json(
        o.Items.Select(i => new { i.ProductId, i.Name, i.Price, i.Quantity, lineTotal = Math.Round(i.Price * i.Quantity, 2) }),
        json);
});

app.MapPost("/orders/{id}/cancel", (HttpContext ctx, string id) =>
{
    var uid = Auth(ctx);
    if (uid is null) return Err401();
    if (!store.Orders.TryGetValue(id, out var o)) return Err404($"Order '{id}' not found.");
    if (o.UserId != uid) return Err403();
    if (o.Status != "pending") return Err422($"Cannot cancel an order with status '{o.Status}'.");
    store.Orders[id] = o with { Status = "cancelled", StatusChangedAt = DateTimeOffset.UtcNow };
    return Results.Json(new { id, status = "cancelled" }, json);
});

// ── Vouchers ──────────────────────────────────────────────────────────────────

app.MapPost("/vouchers/validate", (ValidateVoucherRequest req) =>
{
    if (store.Vouchers.TryGetValue(req.Code, out var pct))
        return Results.Json(new { valid = true, code = req.Code, discountPct = pct }, json);
    return Results.Json(new { valid = false, error = $"Voucher code '{req.Code}' is not valid." }, json, statusCode: 422);
});

// ── Admin ─────────────────────────────────────────────────────────────────────

app.MapPost("/admin/users", (HttpContext ctx, CreateUserRequest req) =>
{
    if (!AdminAuth(ctx)) return Err403();
    var id = req.Id ?? "usr_" + Guid.NewGuid().ToString("N")[..8];
    var user = new User(id, req.Email, req.Password, req.Name);
    lock (store.Users) store.Users.Add(user);
    return Results.Json(new { user.Id, user.Email, user.Name }, json, statusCode: 201);
});

app.MapPost("/admin/products", (HttpContext ctx, CreateProductRequest req) =>
{
    if (!AdminAuth(ctx)) return Err403();
    var id = req.Id ?? "prod_" + Guid.NewGuid().ToString("N")[..8];
    var product = new Product(id, req.Name, req.Category, req.Price, req.Stock, req.Description ?? "");
    lock (store.Products) store.Products.Add(product);
    return Results.Json(new { product.Id, product.Name, product.Category, product.Price, product.Stock }, json, statusCode: 201);
});

app.MapMethods("/admin/products/{id}/stock", new[] { "PATCH" }, (HttpContext ctx, string id, SetStockRequest req) =>
{
    if (!AdminAuth(ctx)) return Err403();
    lock (store.Products)
    {
        var idx = store.Products.FindIndex(p => p.Id == id);
        if (idx < 0) return Err404($"Product '{id}' not found.");
        store.Products[idx] = store.Products[idx] with { Stock = req.Stock };
    }
    return Results.Json(new { id, stock = req.Stock }, json);
});

app.MapDelete("/admin/products/{id}", (HttpContext ctx, string id) =>
{
    if (!AdminAuth(ctx)) return Err403();
    lock (store.Products)
    {
        var idx = store.Products.FindIndex(p => p.Id == id);
        if (idx < 0) return Err404($"Product '{id}' not found.");
        store.Products.RemoveAt(idx);
    }
    return Results.NoContent();
});

app.MapPost("/admin/vouchers", (HttpContext ctx, CreateVoucherRequest req) =>
{
    if (!AdminAuth(ctx)) return Err403();
    store.Vouchers[req.Code] = req.DiscountPct;
    return Results.Json(new { req.Code, req.DiscountPct }, json, statusCode: 201);
});

app.MapDelete("/admin/vouchers/{code}", (HttpContext ctx, string code) =>
{
    if (!AdminAuth(ctx)) return Err403();
    if (!store.Vouchers.Remove(code)) return Err404($"Voucher '{code}' not found.");
    return Results.NoContent();
});

app.Run();

// ── Domain ────────────────────────────────────────────────────────────────────

record User(string Id, string Email, string Password, string Name);
record Product(string Id, string Name, string Category, decimal Price, int Stock, string Description);
record CartItem(string ProductId, int Quantity);
record OrderItem(string ProductId, string Name, decimal Price, int Quantity);
record Order(
    string Id, string UserId, string Status,
    decimal Subtotal, decimal Discount, decimal Total,
    string? VoucherCode, List<OrderItem> Items,
    DateTimeOffset CreatedAt, DateTimeOffset StatusChangedAt);

record LoginRequest(string Email, string Password);
record AddCartItemRequest(string ProductId, int Quantity);
record UpdateCartItemRequest(int Quantity);
record PlaceOrderRequest(string? VoucherCode);
record ValidateVoucherRequest(string Code);
record CreateUserRequest(string? Id, string Email, string Password, string Name);
record CreateProductRequest(string? Id, string Name, string Category, decimal Price, int Stock, string? Description);
record SetStockRequest(int Stock);
record CreateVoucherRequest(string Code, decimal DiscountPct);

// ── Store ─────────────────────────────────────────────────────────────────────

class Store
{
    public readonly List<User> Users =
    [
        new("usr_alice", "alice@example.com", "password", "Alice Smith"),
        new("usr_bob",   "bob@example.com",   "password", "Bob Jones"),
    ];

    public readonly List<Product> Products =
    [
        new("prod_01", "Wireless Headphones",                   "electronics", 79.99m,  50, "Over-ear noise-cancelling headphones"),
        new("prod_02", "Mechanical Keyboard",                   "electronics", 129.99m, 30, "Tenkeyless, Cherry MX switches"),
        new("prod_03", "USB-C Hub",                             "electronics", 39.99m,   0, "7-in-1 hub — currently out of stock"),
        new("prod_04", "The Pragmatic Programmer",              "books",       49.95m,  20, "20th anniversary edition"),
        new("prod_05", "Clean Code",                            "books",       34.99m,  15, "A handbook of agile software craftsmanship"),
        new("prod_06", "Designing Data-Intensive Applications", "books",       59.99m,  10, "By Martin Kleppmann"),
        new("prod_07", "Merino Wool T-Shirt",                   "clothing",    44.99m, 100, "Odour-resistant, machine washable"),
        new("prod_08", "Waterproof Jacket",                     "clothing",    89.99m,  25, "Lightweight packable rain jacket"),
        new("prod_09", "Running Shorts",                        "clothing",    29.99m,  60, "Quick-dry stretch fabric"),
        new("prod_10", "Laptop Stand",                          "electronics", 54.99m,  40, "Adjustable aluminium stand"),
    ];

    public readonly Dictionary<string, decimal> Vouchers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SAVE10"] = 10m,
        ["HALF50"] = 50m,
    };

    public readonly ConcurrentDictionary<string, string> Tokens = new();
    public readonly ConcurrentDictionary<string, List<CartItem>> Carts = new();
    public readonly ConcurrentDictionary<string, Order> Orders = new();

    public List<CartItem> GetCart(string userId) =>
        Carts.GetOrAdd(userId, _ => []);

    public void AddToCart(string userId, string productId, int qty)
    {
        var cart = GetCart(userId);
        lock (cart)
        {
            var idx = cart.FindIndex(i => i.ProductId == productId);
            if (idx >= 0)
                cart[idx] = cart[idx] with { Quantity = cart[idx].Quantity + qty };
            else
                cart.Add(new CartItem(productId, qty));
        }
    }
}

// ── Background: Order Status Processor ───────────────────────────────────────

class OrderProcessor(Store store) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);
            var now = DateTimeOffset.UtcNow;
            foreach (var (id, o) in store.Orders)
            {
                var next = o.Status switch
                {
                    "pending"    when (now - o.StatusChangedAt).TotalSeconds >= 3 => "processing",
                    "processing" when (now - o.StatusChangedAt).TotalSeconds >= 5 => "shipped",
                    _ => null
                };
                if (next is not null)
                    store.Orders[id] = o with { Status = next, StatusChangedAt = now };
            }
        }
    }
}
