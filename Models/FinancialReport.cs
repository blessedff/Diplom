using System;

namespace StationeryShop.Models
{
    public class FinancialReport
    {
        public decimal TotalRevenue { get; set; }           // Общая выручка
        public decimal TotalRevenueCash { get; set; }       // Выручка наличными
        public decimal TotalRevenueCard { get; set; }       // Выручка картой


        // Прямые расходы
        public decimal CostOfGoods { get; set; }            // Себестоимость товаров
        public decimal LogisticsToWarehouse { get; set; }   // Логистика (поставщик → склад)
        public decimal LogisticsToPickup { get; set; }      // Логистика (склад → ПВЗ)
        public decimal Packaging { get; set; }              // Упаковка

        // Аренда и коммунальные
        public decimal WarehouseRent { get; set; }          // Аренда склада
        public decimal PickupPointRent { get; set; }        // Аренда пункта выдачи
        public decimal Utilities { get; set; }              // Коммунальные услуги

        // Персонал
        public decimal SalaryTotal { get; set; }            // Фонд зарплаты
        public decimal SocialTax { get; set; }              // Отчисления ФСЗН (34% от зарплаты)

        // Маркетинг и обслуживание
        public decimal Advertising { get; set; }            // Реклама
        public decimal AcquiringFee { get; set; }           // Эквайринг (комиссия банка)
        public decimal BankService { get; set; }            // Банковское обслуживание
        public decimal Hosting { get; set; }                // Хостинг и домен

        // Прочие расходы
        public decimal OfficeExpenses { get; set; }         // Канцтовары и прочее
        public decimal OtherExpenses { get; set; }          // Разовые расходы (из таблицы Expenses)

        // Итоги по расходам
        public decimal TotalExpenses { get; set; }          // Сумма всех расходов

        
        public decimal TaxRate { get; set; }                // Ставка налога (УСН 5%)
        public decimal TaxAmount { get; set; }              // Сумма налога к уплате

        
        public decimal NetProfit { get; set; }              // Чистая прибыль
        public decimal ProfitMargin { get; set; }           // Рентабельность (в %)

        
        public DateTime StartDate { get; set; }             // Начало периода
        public DateTime EndDate { get; set; }               // Конец периода
    }
}