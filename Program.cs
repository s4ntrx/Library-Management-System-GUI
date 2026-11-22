using System.Text.Json.Serialization;
using LibraryManagement.Exceptions;
using LibraryManagement.Services;
using LibraryManagement.Storage;
using LibraryManagement.Web;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // --data <file> and --port <number> arrive as configuration values.
    var dataPath = LibraryPaths.Resolve(builder.Configuration["data"]);
    var port = builder.Configuration.GetValue("port", 5080);

    // Loopback only, and only local host names are accepted. Other machines and
    // web pages tricking the browser into using another name cannot reach the library.
    builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
    builder.Configuration["AllowedHosts"] = "localhost;127.0.0.1";
    builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
    builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    var library = new LibraryService(new JsonFileLibraryStore(dataPath));
    builder.Services.AddSingleton(new LibraryGateway(library, TimeProvider.System));

    var app = builder.Build();

    app.Use((context, next) =>
    {
        var headers = context.Response.Headers;
        headers["Content-Security-Policy"] = "default-src 'self'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        return next();
    });

    app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (LibraryException ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new ErrorBody(ex.Message));
        }
        catch (StorageException ex)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ErrorBody(ex.Message));
        }
    });

    var api = app.MapGroup("/api");
    api.MapGet("/books", (string? query, LibraryGateway gateway) => gateway.ListBooks(query));
    api.MapGet("/books/{id:int}", (int id, LibraryGateway gateway) =>
        gateway.FindBook(id) is { } book
            ? Results.Ok(book)
            : Results.NotFound(new ErrorBody($"No book with ID {id} exists.")));
    api.MapPost("/books", (NewBookRequest request, LibraryGateway gateway) =>
        Results.Created("/api/books", gateway.AddBook(request)));
    api.MapPost("/books/{id:int}/borrow", (int id, BorrowRequest request, LibraryGateway gateway) =>
        gateway.Borrow(id, request.MemberId));
    api.MapPost("/books/{id:int}/return", (int id, LibraryGateway gateway) => gateway.Return(id));
    api.MapGet("/members", (LibraryGateway gateway) => gateway.ListMembers());
    api.MapPost("/members", (NewMemberRequest request, LibraryGateway gateway) =>
        Results.Created("/api/members", gateway.AddMember(request)));

    var assembly = typeof(Program).Assembly;
    const string prefix = "wwwroot/";
    foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix)))
    {
        var name = resource[prefix.Length..];
        var contentType = Path.GetExtension(name) switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "text/javascript; charset=utf-8",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
        IResult Serve(HttpContext context)
        {
            context.Response.Headers.CacheControl = "no-cache";
            return Results.Stream(assembly.GetManifestResourceStream(resource)!, contentType);
        }
        app.MapGet("/" + name, Serve);
        if (name == "index.html")
            app.MapGet("/", Serve);
    }

    Console.WriteLine($"Library web interface: http://127.0.0.1:{port}  (Ctrl+C to stop)");
    Console.WriteLine($"Data file: {dataPath}");
    app.Run();
    return 0;
}
catch (StorageException ex)
{
    Console.Error.WriteLine($"Storage error: {ex.Message}");
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Could not start: {ex.Message}");
    Console.Error.WriteLine("If the port is busy, choose another with --port 5081.");
    return 1;
}
