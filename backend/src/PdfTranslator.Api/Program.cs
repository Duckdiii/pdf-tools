using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using PdfTranslator.Api.Data;
using PdfTranslator.Api.Services;

// 1. Tải biến môi trường từ file .env
DotNetEnv.Env.TraversePath().Load();

var possibleEnvPaths = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), "backend", ".env"),
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env"),
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env")
};

foreach (var envPath in possibleEnvPaths)
{
    if (File.Exists(envPath))
    {
        DotNetEnv.Env.Load(envPath);
        Console.WriteLine($"[Config] Loaded environment variables from: {Path.GetFullPath(envPath)}");
        break;
    }
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Lấy connection string từ biến môi trường (ưu tiên) hoặc appsettings.json
var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Default");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw excitingException("Database connection string is missing! Please configure DATABASE_CONNECTION_STRING in .env or appsettings.json.");
}

// 2. Cấu hình CORS cho phép React Frontend gọi API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://localhost:4173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 3. Đăng ký AppDbContext sử dụng PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 4. Đăng ký PDF Extractor Service
builder.Services.AddScoped<IPdfExtractorService, PdfExtractorService>();

// 5. Đăng ký Translation Service với HttpClient quản lý kết nối tự động (Phase 3)
builder.Services.AddHttpClient<ITranslationService, GeminiTranslationService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

// 6. Đăng ký PDF Rebuilder Service để xuất file PDF tiếng Việt (Phase 4)
builder.Services.AddScoped<IPdfRebuilderService, PdfRebuilderService>();

// 7. Đăng ký Hangfire Background Job với PostgreSQL Storage
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer(options =>
{
    // Cấu hình 4 workers để tránh vượt quá connection limit (pool_size 15) của Supabase Session Pooler
    options.WorkerCount = 4;
});

// 8. Đăng ký Translation Pipeline Service xử lý ngầm
builder.Services.AddScoped<ITranslationPipelineService, TranslationPipelineService>();

// 9. Đăng ký Controllers
builder.Services.AddControllers();

// 10. Cấu hình Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//-----------------------------------------------------------------------------------------------------------

var app = builder.Build();

//-----------------------------------------------------------------------------------------------------------
// Cấu hình Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Kích hoạt CORS trước UseAuthorization
app.UseCors("AllowReactApp");

// Kích hoạt Hangfire Dashboard (Truy cập tại /hangfire)
app.UseHangfireDashboard("/hangfire");

app.UseAuthorization();

app.MapControllers();

app.Run();

static InvalidOperationException excitingException(string message) => new(message);
