using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

// ---------------------------------------------------------------------------
// Submit-Order benchmark — black-box probe suite (the executable rubric).
//
// Runs the five framework-neutral correctness requirements (R1-R5) from SPEC.md
// against a running arm and prints a PASS/FAIL scorecard. Exit code = number of
// defects found (0 = clean), so it can gate CI or a runner script.
//
//   dotnet run --project SubmitOrder.Probes -- --url http://localhost:5080
// ---------------------------------------------------------------------------

string url = GetArg(args, "--url") ?? Environment.GetEnvironmentVariable("ARM_URL") ?? "http://localhost:5080";
var baseUri = new Uri(url.TrimEnd('/') + "/");

using var http = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(30) };
var api = new ApiClient(http);

Console.WriteLine($"Submit-Order benchmark probes -> {baseUri}");
Console.WriteLine(new string('-', 64));

if (!await WaitForHealthAsync(http))
{
    Console.Error.WriteLine($"FATAL: arm at {baseUri} never became healthy (GET /health).");
    return 99;
}

var probes = new (string Id, string Name, Func<ApiClient, Task<ProbeResult>> Run)[]
{
    ("R1", "No oversell under concurrency", ProbeR1),
    ("R2", "All-or-nothing reservation",    ProbeR2),
    ("R3", "Business failure is 4xx not 5xx", ProbeR3),
    ("R4", "State guard (no double submit)", ProbeR4),
    ("R5", "Authorization required",         ProbeR5),
};

var results = new List<(string Id, string Name, ProbeResult Result)>();
foreach (var (id, name, run) in probes)
{
    ProbeResult r;
    try
    {
        r = await run(api);
    }
    catch (Exception ex)
    {
        r = ProbeResult.Fail($"probe threw: {ex.GetType().Name}: {ex.Message}");
    }

    results.Add((id, name, r));
    Console.WriteLine($"  {(r.Pass ? "PASS" : "FAIL")}  {id}  {name}");
    if (!r.Pass)
        Console.WriteLine($"        -> {r.Detail}");
}

Console.WriteLine(new string('-', 64));
int defects = results.Count(r => !r.Result.Pass);
Console.WriteLine($"DEFECT SCORE: {defects} / {results.Count}   ({results.Count - defects} requirement(s) upheld)");
Console.WriteLine(defects == 0
    ? "RESULT: CLEAN — every requirement upheld."
    : $"RESULT: {defects} defect(s) present.");

return defects;

// ===========================================================================
// Probes — each maps to a requirement in SPEC.md §4.
// ===========================================================================

// R1: concurrent submits drawing on the same product must never reserve more
// than its stock. Seed stock=1, fire several single-unit submits at once, and
// require that EXACTLY ONE succeeds. >1 success == oversell (lost update).
static async Task<ProbeResult> ProbeR1(ApiClient api)
{
    const int iterations = 3;
    const int concurrency = 8;

    for (int i = 0; i < iterations; i++)
    {
        var product = await api.CreateProductAsync($"R1-{i}", stock: 1, price: 1m);

        var orders = new List<Guid>();
        for (int n = 0; n < concurrency; n++)
            orders.Add(await api.CreateOrderAsync(Guid.NewGuid(), [(product, 1)]));

        var submits = orders.Select(o => api.SubmitAsync(o, ["orders:submit"])).ToArray();
        var responses = await Task.WhenAll(submits);

        int successes = responses.Count(r => r.Status == HttpStatusCode.OK);
        int finalStock = await api.GetStockAsync(product);

        if (successes != 1)
            return ProbeResult.Fail(
                $"iteration {i}: {successes} of {concurrency} single-unit submits succeeded against stock=1 " +
                $"(expected exactly 1); final stock={finalStock}. Oversell / lost update.");

        if (finalStock != 0)
            return ProbeResult.Fail(
                $"iteration {i}: 1 submit succeeded but final stock={finalStock} (expected 0).");
    }

    return ProbeResult.Ok($"{iterations}x{concurrency} concurrent submits: exactly one winner each time.");
}

// R2: if any line is short, NO stock is reserved for any line. Two lines draw on
// the SAME product and together exceed its stock (3 + 3 > 5). Whichever line EF
// processes first succeeds; the second is short. A per-item-save implementation
// persists the first reservation and leaves stock partially drawn down; an atomic
// implementation pre-flights total demand and reserves nothing. Using one product
// makes the outcome independent of EF's line-iteration order.
static async Task<ProbeResult> ProbeR2(ApiClient api)
{
    var product = await api.CreateProductAsync("R2", stock: 5, price: 1m);

    var order = await api.CreateOrderAsync(Guid.NewGuid(), [(product, 3), (product, 3)]);
    var submit = await api.SubmitAsync(order, ["orders:submit"]);

    int stock = await api.GetStockAsync(product);

    if (submit.Status == HttpStatusCode.OK)
        return ProbeResult.Fail($"submit of an over-demand order (3+3 vs stock 5) succeeded (status {(int)submit.Status}).");

    if (stock != 5)
        return ProbeResult.Fail(
            $"submit failed (status {(int)submit.Status}) but product stock={stock} (expected 5). " +
            "A partial reservation was persisted.");

    return ProbeResult.Ok($"over-demand order rejected ({(int)submit.Status}); no stock reserved (stock=5).");
}

// R3: a business rejection (insufficient stock) is a 4xx, never a 5xx, and the
// body must not leak internal exception text.
static async Task<ProbeResult> ProbeR3(ApiClient api)
{
    var product = await api.CreateProductAsync("R3", stock: 1, price: 1m);
    var order = await api.CreateOrderAsync(Guid.NewGuid(), [(product, 5)]);

    var submit = await api.SubmitAsync(order, ["orders:submit"]);
    int code = (int)submit.Status;

    if (code >= 500)
        return ProbeResult.Fail($"insufficient-stock submit returned {code} (server error); expected 409/422.");

    if (code is not (409 or 422))
        return ProbeResult.Fail($"insufficient-stock submit returned {code}; expected 409 or 422.");

    if (LeaksInternals(submit.Body))
        return ProbeResult.Fail($"error body leaks internal detail (stack trace / 'Exception'): {Trim(submit.Body)}");

    if (!HasProblemShape(submit.Body))
        return ProbeResult.Fail($"error body is not a JSON object carrying a non-empty 'detail' or 'title': {Trim(submit.Body)}");

    return ProbeResult.Ok($"insufficient stock -> {code}, JSON problem body, no leaked internals.");
}

// R4: only a draft order may be submitted. Submit once (succeeds), then submit
// again; the second call must be 409 and must NOT reserve stock a second time.
static async Task<ProbeResult> ProbeR4(ApiClient api)
{
    var product = await api.CreateProductAsync("R4", stock: 10, price: 1m);
    var order = await api.CreateOrderAsync(Guid.NewGuid(), [(product, 5)]);

    var first = await api.SubmitAsync(order, ["orders:submit"]);
    if (first.Status != HttpStatusCode.OK)
        return ProbeResult.Fail($"first submit failed unexpectedly (status {(int)first.Status}).");

    int afterFirst = await api.GetStockAsync(product);
    if (afterFirst != 5)
        return ProbeResult.Fail($"first submit left stock={afterFirst} (expected 5).");

    var second = await api.SubmitAsync(order, ["orders:submit"]);
    int afterSecond = await api.GetStockAsync(product);

    if ((int)second.Status != 409)
        return ProbeResult.Fail($"re-submit of a Submitted order returned {(int)second.Status}; expected 409.");

    if (afterSecond != 5)
        return ProbeResult.Fail(
            $"re-submit changed stock to {afterSecond} (expected 5). Reserved a second time -> double reservation.");

    return ProbeResult.Ok("re-submit rejected (409); stock reserved exactly once.");
}

// R5: submitting without the orders:submit permission (and with no actor at all)
// is forbidden and reserves nothing.
static async Task<ProbeResult> ProbeR5(ApiClient api)
{
    var product = await api.CreateProductAsync("R5", stock: 10, price: 1m);

    // (a) actor present but lacking the permission
    var order1 = await api.CreateOrderAsync(Guid.NewGuid(), [(product, 5)]);
    var lacking = await api.SubmitAsync(order1, ["orders:read"]);
    if ((int)lacking.Status != 403)
        return ProbeResult.Fail($"submit with insufficient permissions returned {(int)lacking.Status}; expected 403.");

    // (b) no actor header at all
    var order2 = await api.CreateOrderAsync(Guid.NewGuid(), [(product, 5)]);
    var anon = await api.SubmitAsync(order2, permissions: null);
    if ((int)anon.Status != 403)
        return ProbeResult.Fail($"submit with no actor returned {(int)anon.Status}; expected 403.");

    int stock = await api.GetStockAsync(product);
    if (stock != 10)
        return ProbeResult.Fail($"forbidden submits still moved stock to {stock} (expected 10).");

    return ProbeResult.Ok("unauthorized submits rejected (403); nothing reserved.");
}

// ===========================================================================
// Infrastructure
// ===========================================================================

static async Task<bool> WaitForHealthAsync(HttpClient http)
{
    var sw = Stopwatch.StartNew();
    while (sw.Elapsed < TimeSpan.FromSeconds(30))
    {
        try
        {
            var r = await http.GetAsync("health");
            if (r.IsSuccessStatusCode)
                return true;
        }
        catch
        {
            // arm not up yet
        }
        await Task.Delay(500);
    }
    return false;
}

static bool LeaksInternals(string body)
{
    if (string.IsNullOrEmpty(body)) return false;
    string[] tells = ["at System.", "at Microsoft.", "Exception:", "StackTrace", "   at ", "DbUpdate", "SqlException"];
    return tells.Any(t => body.Contains(t, StringComparison.OrdinalIgnoreCase));
}

// A business error must come back as a JSON object carrying a non-empty `detail` or `title`
// (RFC 9457 ProblemDetails, or at minimum that shape) — not plain text or an empty body.
static bool HasProblemShape(string body)
{
    if (string.IsNullOrWhiteSpace(body)) return false;
    try
    {
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            var isProblemField = string.Equals(property.Name, "detail", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(property.Name, "title", StringComparison.OrdinalIgnoreCase);
            if (isProblemField
                && property.Value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                return true;
        }
        return false;
    }
    catch (JsonException)
    {
        return false;
    }
}

static string Trim(string s) => s.Length <= 200 ? s : s[..200] + "...";

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == name && i + 1 < args.Length) return args[i + 1];
        if (args[i].StartsWith(name + "=", StringComparison.Ordinal)) return args[i][(name.Length + 1)..];
    }
    return null;
}

readonly record struct ProbeResult(bool Pass, string Detail)
{
    public static ProbeResult Ok(string detail) => new(true, detail);
    public static ProbeResult Fail(string detail) => new(false, detail);
}

// Thin black-box HTTP client for the §3 contract. Parses case-insensitively so
// either arm's default JSON casing works.
sealed class ApiClient(HttpClient http)
{
    public async Task<Guid> CreateProductAsync(string name, int stock, decimal price)
    {
        var payload = JsonSerializer.Serialize(new { name, stock, price });
        using var resp = await http.PostAsync("products", JsonContent(payload));
        var body = await resp.Content.ReadAsStringAsync();
        if (resp.StatusCode != HttpStatusCode.Created)
            throw new InvalidOperationException($"POST /products -> {(int)resp.StatusCode}: {body}");
        return ReadGuid(body, "id");
    }

    public async Task<int> GetStockAsync(Guid productId)
    {
        using var resp = await http.GetAsync($"products/{productId}");
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"GET /products/{productId} -> {(int)resp.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        return GetProp(doc.RootElement, "stock").GetInt32();
    }

    public async Task<Guid> CreateOrderAsync(Guid customerId, (Guid productId, int quantity)[] items)
    {
        var payload = JsonSerializer.Serialize(new
        {
            customerId,
            items = items.Select(i => new { productId = i.productId, quantity = i.quantity }),
        });
        using var resp = await http.PostAsync("orders", JsonContent(payload));
        var body = await resp.Content.ReadAsStringAsync();
        if (resp.StatusCode != HttpStatusCode.Created)
            throw new InvalidOperationException($"POST /orders -> {(int)resp.StatusCode}: {body}");
        return ReadGuid(body, "id");
    }

    public async Task<(HttpStatusCode Status, string Body)> SubmitAsync(Guid orderId, string[]? permissions)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"orders/{orderId}/submit");
        if (permissions is not null)
        {
            var actor = JsonSerializer.Serialize(new { id = "probe-actor", permissions });
            req.Headers.TryAddWithoutValidation("X-Actor", actor);
        }
        using var resp = await http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        return (resp.StatusCode, body);
    }

    static StringContent JsonContent(string json) => new(json, Encoding.UTF8, "application/json");

    static Guid ReadGuid(string body, string prop)
    {
        using var doc = JsonDocument.Parse(body);
        return GetProp(doc.RootElement, prop).GetGuid();
    }

    static JsonElement GetProp(JsonElement element, string name)
    {
        foreach (var p in element.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        throw new InvalidOperationException($"property '{name}' not found in: {element.GetRawText()}");
    }
}
