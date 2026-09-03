// Đây là nơi cấu hình toàn bộ ứng dụng từ lúc khởi động đến khi nhận request.
using Microsoft.EntityFrameworkCore;
using PdfTranslator.Api.Data;

// 1. Tải biến môi trường từ file .env
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

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

// 4. Đăng ký Controllers
builder.Services.AddControllers();

// 5. Cấu hình Swagger / OpenAPI
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

app.UseAuthorization();

app.MapControllers();

app.Run();

static InvalidOperationException excitingException(string message) => new(message);
