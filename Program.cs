using Microsoft.EntityFrameworkCore;
using StationeryShop.Data;
using StationeryShop.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add DbContext
builder.Services.AddDbContext<StationeryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add HttpContextAccessor and custom services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CartService>();

// Add logging
builder.Services.AddLogging();
builder.Services.AddScoped<EmailService>();

var app = builder.Build();
// Ручное создание таблицы LoginAttempts
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<StationeryDbContext>();

    string createTableSql = @"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LoginAttempts')
        BEGIN
            CREATE TABLE [LoginAttempts] (
                [Id] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
                [Email] NVARCHAR(100) NOT NULL,
                [AttemptTime] DATETIME2 NOT NULL,
                [IpAddress] NVARCHAR(50) NOT NULL,
                [IsSuccessful] BIT NOT NULL DEFAULT 0,
                [UserAgent] NVARCHAR(500) NULL
            );
            
            CREATE INDEX IX_LoginAttempts_Email_AttemptTime ON [LoginAttempts] ([Email], [AttemptTime]);
            CREATE INDEX IX_LoginAttempts_IpAddress_AttemptTime ON [LoginAttempts] ([IpAddress], [AttemptTime]);
        END
    ";

    context.Database.ExecuteSqlRaw(createTableSql);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.UseSession();

// Инициализация базы данных
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<StationeryDbContext>();
        DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();