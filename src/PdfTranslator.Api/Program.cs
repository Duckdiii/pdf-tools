using Microsoft.EntityFrameworkCore;
using PdfTranslator.Api.Data;

// 1. Tải biến môi trường từ file .env (tìm ở thư mục hiện tại và các thư mục cha)
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Lấy connection string từ biến môi trường (ưu tiên) hoặc appsettings.json
var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Default");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw excitingException("Database connection string is missing! Please configure DATABASE_CONNECTION_STRING in .env or appsettings.json.");
}

// 2. Đăng ký AppDbContext sử dụng PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 3. Đăng ký Controllers
builder.Services.AddControllers();

// 4. Cấu hình Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Cấu hình Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

static InvalidOperationException excitingException(string message) => new(message);
