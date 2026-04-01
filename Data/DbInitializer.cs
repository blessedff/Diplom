using StationeryShop.Models;

namespace StationeryShop.Data
{
    public static class DbInitializer
    {
        public static void Initialize(StationeryDbContext context)
        {
            context.Database.EnsureCreated();

            // Проверяем, есть ли уже данные
            if (context.Categories.Any())
            {
                return; // База уже инициализирована
            }

            // Добавляем категории для магазина канцтоваров
            var categories = new Category[]
                      {
                new Category {
                    Name = "Письменные принадлежности",
                    Description = "Ручки, карандаши, маркеры, стержни"
                },
                new Category {
                    Name = "Бумажная продукция",
                    Description = "Тетради, блокноты, бумага для принтера, альбомы"
                },
                new Category {
                    Name = "Офисные аксессуары",
                    Description = "Степлеры, дыроколы, ножницы, клей, скотч"
                },
                new Category {
                    Name = "Органайзеры и папки",
                    Description = "Папки, файлы, скоросшиватели, органайзеры"
                },
                new Category {
                    Name = "Творчество и хобби",
                    Description = "Краски, кисти, фломастеры, цветная бумага"
                }
                      };

            foreach (var c in categories)
            {
                context.Categories.Add(c);
            }
            context.SaveChanges();


            var products = new Product[]
           {
                // Письменные принадлежности (категория 0)
                new Product {
                    Name = "Ручка шариковая Erich Krause синяя",
                    Description = "Надежная шариковая ручка с синими чернилами, эргономичный корпус",
                    Price = 1.20m,
                    StockQuantity = 150,
                    CategoryID = categories[0].CategoryID
                },
                new Product {
                    Name = "Набор гелевых ручек 6 цветов",
                    Description = "Яркие гелевые ручки для заметок и творчества, тонкий стержень 0.7 мм",
                    Price = 8.50m,
                    StockQuantity = 45,
                    CategoryID = categories[0].CategoryID
                },
                new Product {
                    Name = "Автоматический карандаш 0.5 мм",
                    Description = "Профессиональный автоматический карандаш с металлическим зажимом",
                    Price = 4.80m,
                    StockQuantity = 60,
                    CategoryID = categories[0].CategoryID
                },

                // Бумажная продукция (категория 1)
                new Product {
                    Name = "Тетрадь 48 листов в клетку",
                    Description = "Тетрадь в клетку 48 листов, плотная обложка, качественная бумага",
                    Price = 2.10m,
                    StockQuantity = 200,
                    CategoryID = categories[1].CategoryID
                },
                new Product {
                    Name = "Бумага для принтера Svetocopy А4",
                    Description = "Пачка бумаги 500 листов, плотность 80 г/м², белизна 146%",
                    Price = 18.90m,
                    StockQuantity = 30,
                    CategoryID = categories[1].CategoryID
                },
                new Product {
                    Name = "Ежедневник А5 2024",
                    Description = "Ежедневник на 2024 год, твердая обложка, датированный, 160 страниц",
                    Price = 12.50m,
                    StockQuantity = 40,
                    CategoryID = categories[1].CategoryID
                },

                // Офисные аксессуары (категория 2)
                new Product {
                    Name = "Ножницы офисные 20 см",
                    Description = "Острые ножницы для бумаги с пластиковыми ручками, длина 20 см",
                    Price = 6.80m,
                    StockQuantity = 35,
                    CategoryID = categories[2].CategoryID
                },
                new Product {
                    Name = "Степлер металлический №10",
                    Description = "Надежный степлер для скрепления документов, вмещает 20 скоб",
                    Price = 9.20m,
                    StockQuantity = 25,
                    CategoryID = categories[2].CategoryID
                },
                new Product {
                    Name = "Скотч двусторонний 12мм",
                    Description = "Двусторонний скотч для крепления без следов, ширина 12 мм",
                    Price = 3.50m,
                    StockQuantity = 80,
                    CategoryID = categories[2].CategoryID
                },

                // Органайзеры и папки (категория 3)
                new Product {
                    Name = "Папка-скоросшиватель А4",
                    Description = "Пластиковая папка с арочным механизмом, прозрачный карман",
                    Price = 2.80m,
                    StockQuantity = 70,
                    CategoryID = categories[3].CategoryID
                },
                new Product {
                    Name = "Файлы для документов 100 шт",
                    Description = "Набор файлов для документов А4, толщина 40 мкм, 100 штук",
                    Price = 7.40m,
                    StockQuantity = 50,
                    CategoryID = categories[3].CategoryID
                },
                new Product {
                    Name = "Органайзер настольный 5 отделений",
                    Description = "Настольный органайзер для канцелярии, 5 отделений, пластик",
                    Price = 15.90m,
                    StockQuantity = 20,
                    CategoryID = categories[3].CategoryID
                },

                // Творчество и хобби (категория 4)
                new Product {
                    Name = "Акварельные краски 12 цветов",
                    Description = "Набор акварельных красок для творчества, 12 ярких цветов",
                    Price = 11.20m,
                    StockQuantity = 30,
                    CategoryID = categories[4].CategoryID
                },
                new Product {
                    Name = "Кисти для рисования набор 5 шт",
                    Description = "Набор кистей для акварели и гуаши, натуральный ворс, 5 размеров",
                    Price = 8.70m,
                    StockQuantity = 25,
                    CategoryID = categories[4].CategoryID
                },
                new Product {
                    Name = "Цветная бумага А4 16 листов",
                    Description = "Набор цветной бумаги для детского творчества, 8 цветов, 16 листов",
                    Price = 3.90m,
                    StockQuantity = 60,
                    CategoryID = categories[4].CategoryID
                }
           };

            foreach (var p in products)
            {
                context.Products.Add(p);
            }
            context.SaveChanges();

            // Добавляем администратора
            var admin = new Customer
            {
                FullName = "Администратор Системы",
                Email = "blessed@gmail.com",
               
                Phone = "+375292746680",
                Address = "Главный офис",
                IsAdmin = true,
                Password = "123123",
            };
            context.Customers.Add(admin);

            // Добавляем тестового пользователя
            var testUser = new Customer
            {
                FullName = "Иван Петров",
                Email = "ivan@gmail.com",
             
                Phone = "+375296401050",
                Address = "ул. Примерная, д. 123",
                IsAdmin = false,
                Password = "123123",
            };
            context.Customers.Add(testUser);

            context.SaveChanges();
        }
    }
}