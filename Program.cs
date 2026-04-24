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
builder.Services.AddScoped<EmailService>();

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

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

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<StationeryDbContext>();

    //Таблица LoginAttempts
    string createLoginAttemptsTable = @"
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

    //Таблица Expenses
    string createExpensesTable = @"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Expenses')
        BEGIN
            CREATE TABLE [Expenses] (
                [ExpenseId] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
                [Category] NVARCHAR(100) NOT NULL,
                [Description] NVARCHAR(200) NOT NULL,
                [Amount] DECIMAL(18,2) NOT NULL,
                [ExpenseDate] DATETIME2 NOT NULL,
                [PaymentMethod] NVARCHAR(50) NULL,
                [IsRecurring] BIT NOT NULL DEFAULT 0,
                [Notes] NVARCHAR(500) NULL
            );
        END
    ";

    //Таблица FinancialSettings
    string createSettingsTable = @"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FinancialSettings')
        BEGIN
            CREATE TABLE [FinancialSettings] (
                [SettingId] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
                [SettingKey] NVARCHAR(50) NOT NULL UNIQUE,
                [SettingValue] NVARCHAR(200) NOT NULL,
                [Description] NVARCHAR(200) NULL
            );
        END
    ";
    //Таблица CartTable
    string createCartTable = @"
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CartItems')
    BEGIN
        CREATE TABLE [CartItems] (
            [Id] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
            [CustomerID] INT NOT NULL,
            [ProductID] INT NOT NULL,
            [Quantity] INT NOT NULL,
            [AddedDate] DATETIME2 NOT NULL,
            CONSTRAINT FK_CartItems_Customers FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID) ON DELETE CASCADE,
            CONSTRAINT FK_CartItems_Products FOREIGN KEY (ProductID) REFERENCES Products(ProductID)
        );
    END
";
    // Таблица Reviews
    string createReviewsTable = @"
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Reviews')
    BEGIN
        CREATE TABLE [Reviews] (
            [Id] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
            [ProductId] INT NULL,
            [CustomerId] INT NOT NULL,
            [Rating] INT NOT NULL,
            [Comment] NVARCHAR(1000) NOT NULL,
            [IsApproved] BIT NOT NULL DEFAULT 0,
            [IsRejected] BIT NOT NULL DEFAULT 0,
            [CreatedAt] DATETIME2 NOT NULL,
            [AdminResponse] NVARCHAR(1000) NULL,
            [AdminResponseDate] DATETIME2 NULL,
            CONSTRAINT FK_Reviews_Products FOREIGN KEY (ProductId) REFERENCES Products(ProductID),
            CONSTRAINT FK_Reviews_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerID)
        );
    END
";
    //Таблица ProductQuestions
    string createProductQuestionsTable = @"
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ProductQuestions')
    BEGIN
        CREATE TABLE [ProductQuestions] (
            [Id] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
            [ProductId] INT NOT NULL,
            [CustomerId] INT NOT NULL,
            [Question] NVARCHAR(1000) NOT NULL,
            [QuestionDate] DATETIME2 NOT NULL,
            [Answer] NVARCHAR(2000) NULL,
            [AnswerDate] DATETIME2 NULL,
            [IsPublished] BIT NOT NULL DEFAULT 0,
            CONSTRAINT FK_ProductQuestions_Products FOREIGN KEY (ProductId) REFERENCES Products(ProductID),
            CONSTRAINT FK_ProductQuestions_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerID)
        );
    END
";


    //Добавление начальных настроек
    string insertSettings = @"
        IF NOT EXISTS (SELECT * FROM [FinancialSettings] WHERE [SettingKey] = 'TaxRate')
            INSERT INTO [FinancialSettings] ([SettingKey], [SettingValue], [Description]) 
            VALUES ('TaxRate', '5', 'Ставка налога УСН (в %)');
            
        IF NOT EXISTS (SELECT * FROM [FinancialSettings] WHERE [SettingKey] = 'AcquiringRate')
            INSERT INTO [FinancialSettings] ([SettingKey], [SettingValue], [Description]) 
            VALUES ('AcquiringRate', '1.5', 'Комиссия эквайринга (в %)');
            
        IF NOT EXISTS (SELECT * FROM [FinancialSettings] WHERE [SettingKey] = 'SalaryTotal')
            INSERT INTO [FinancialSettings] ([SettingKey], [SettingValue], [Description]) 
            VALUES ('SalaryTotal', '0', 'Общая зарплата всех сотрудников (BYN/мес)');
            
        IF NOT EXISTS (SELECT * FROM [FinancialSettings] WHERE [SettingKey] = 'SocialTaxRate')
            INSERT INTO [FinancialSettings] ([SettingKey], [SettingValue], [Description]) 
            VALUES ('SocialTaxRate', '34', 'Ставка отчислений ФСЗН (в %)');
            
        IF NOT EXISTS (SELECT * FROM [FinancialSettings] WHERE [SettingKey] = 'WarehouseRent')
            INSERT INTO [FinancialSettings] ([SettingKey], [SettingValue], [Description]) 
            VALUES ('WarehouseRent', '0', 'Аренда склада (BYN/мес)');
            
        IF NOT EXISTS (SELECT * FROM [FinancialSettings] WHERE [SettingKey] = 'PickupPointRent')
            INSERT INTO [FinancialSettings] ([SettingKey], [SettingValue], [Description]) 
            VALUES ('PickupPointRent', '0', 'Аренда пункта выдачи (BYN/мес)');
            
        IF NOT EXISTS (SELECT * FROM [FinancialSettings] WHERE [SettingKey] = 'LogisticsToWarehouse')
            INSERT INTO [FinancialSettings] ([SettingKey], [SettingValue], [Description]) 
            VALUES ('LogisticsToWarehouse', '0', 'Логистика от поставщика до склада (BYN/мес)');
            
        IF NOT EXISTS (SELECT * FROM [FinancialSettings] WHERE [SettingKey] = 'LogisticsToPickup')
            INSERT INTO [FinancialSettings] ([SettingKey], [SettingValue], [Description]) 
            VALUES ('LogisticsToPickup', '0', 'Логистика со склада на ПВЗ (BYN/мес)');
            
        IF NOT EXISTS (SELECT * FROM [FinancialSettings] WHERE [SettingKey] = 'Advertising')
            INSERT INTO [FinancialSettings] ([SettingKey], [SettingValue], [Description]) 
            VALUES ('Advertising', '0', 'Расходы на рекламу (BYN/мес)');
            
        IF NOT EXISTS (SELECT * FROM [FinancialSettings] WHERE [SettingKey] = 'PackagingPerOrder')
            INSERT INTO [FinancialSettings] ([SettingKey], [SettingValue], [Description]) 
            VALUES ('PackagingPerOrder', '1.5', 'Стоимость упаковки на 1 заказ (BYN)');
            
        IF NOT EXISTS (SELECT * FROM [FinancialSettings] WHERE [SettingKey] = 'BankService')
            INSERT INTO [FinancialSettings] ([SettingKey], [SettingValue], [Description]) 
            VALUES ('BankService', '0', 'Банковское обслуживание (BYN/мес)');
            
        IF NOT EXISTS (SELECT * FROM [FinancialSettings] WHERE [SettingKey] = 'Hosting')
            INSERT INTO [FinancialSettings] ([SettingKey], [SettingValue], [Description]) 
            VALUES ('Hosting', '0', 'Хостинг и домен (BYN/мес)');
            
        IF NOT EXISTS (SELECT * FROM [FinancialSettings] WHERE [SettingKey] = 'Utilities')
            INSERT INTO [FinancialSettings] ([SettingKey], [SettingValue], [Description]) 
            VALUES ('Utilities', '0', 'Коммунальные услуги (BYN/мес)');
            
        IF NOT EXISTS (SELECT * FROM [FinancialSettings] WHERE [SettingKey] = 'OfficeExpenses')
            INSERT INTO [FinancialSettings] ([SettingKey], [SettingValue], [Description]) 
            VALUES ('OfficeExpenses', '0', 'Канцтовары и прочее (BYN/мес)');
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Products' AND COLUMN_NAME = 'PurchaseCost')
         BEGIN
         ALTER TABLE [Products] ADD [PurchaseCost] DECIMAL(18,2) NOT NULL DEFAULT 0
        END
    ";


    try
    {
        await context.Database.ExecuteSqlRawAsync(createLoginAttemptsTable);
        Console.WriteLine("Таблица LoginAttempts создана или уже существует");

        await context.Database.ExecuteSqlRawAsync(createExpensesTable);
        Console.WriteLine("Таблица Expenses создана или уже существует");

        await context.Database.ExecuteSqlRawAsync(createSettingsTable);
        Console.WriteLine("Таблица FinancialSettings создана или уже существует");

        await context.Database.ExecuteSqlRawAsync(createCartTable);
        Console.WriteLine("Таблица CartTable создана или уже существует");

        await context.Database.ExecuteSqlRawAsync(createReviewsTable);
        Console.WriteLine("Таблица Reviews создана или уже существует");

        await context.Database.ExecuteSqlRawAsync(createProductQuestionsTable);
        Console.WriteLine("Таблица ProductQuestions создана или уже существует");

        await context.Database.ExecuteSqlRawAsync(insertSettings);
        Console.WriteLine("Начальные настройки добавлены");


    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка при создании таблиц: {ex.Message}");
    }
}

//Инициализация БД
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