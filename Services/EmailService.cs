using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace StationeryShop.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Отправляет уведомление о создании заказа
        /// </summary>
        public async Task<bool> SendOrderCreatedNotification(string toEmail, string toName, int orderId)
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:SmtpServer"];
                var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]);
                var senderEmail = _configuration["EmailSettings:SenderEmail"];
                var senderPassword = _configuration["EmailSettings:SenderPassword"];
                var useSsl = bool.Parse(_configuration["EmailSettings:UseSsl"]);

                string subject = $"Канцелярский магазин. Заказ #{orderId} принят в обработку!";

                string body = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='utf-8'>
                        <style>
                            body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 0; padding: 0; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background: linear-gradient(135deg, #2c5aa0, #3a7bd5); color: white; padding: 25px; text-align: center; border-radius: 10px 10px 0 0; }}
                            .header h2 {{ margin: 0; }}
                            .content {{ padding: 25px; background: #f8f9fa; border: 1px solid #e9ecef; border-top: none; }}
                            .content h3 {{ margin-top: 0; color: #2d3748; }}
                            .order-number {{ font-size: 20px; font-weight: bold; color: #2c5aa0; }}
                            .status {{ display: inline-block; background: #28a745; color: white; padding: 8px 16px; border-radius: 20px; font-size: 14px; margin: 15px 0; }}
                            .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #6c757d; background: #f8f9fa; border-radius: 0 0 10px 10px; }}
                            .button {{ display: inline-block; background: #2c5aa0; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; margin-top: 15px; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h2>🏪 Канцелярский магазин</h2>
                                <p>Ваш заказ принят в работу</p>
                            </div>
                            <div class='content'>
                                <h3>Здравствуйте, {toName}!</h3>
                                <p>Ваш заказ <span class='order-number'>#{orderId}</span> успешно оформлен и принят в обработку.</p>
                                <div style='text-align: center;'>
                                    <span class='status'>✅ Принят</span>
                                </div>
                                <p>Мы свяжемся с вами, когда заказ будет готов к выдаче.</p>

                            </div>
                            <div class='footer'>
                                <p>Это письмо отправлено автоматически, пожалуйста, не отвечайте на него.</p>
                                <p>© 2026 Канцелярский магазин</p>
                            </div>
                        </div>
                    </body>
                    </html>
                ";

                using var client = new SmtpClient(smtpServer, smtpPort);
                client.EnableSsl = useSsl;
                client.Credentials = new NetworkCredential(senderEmail, senderPassword);

                var mailMessage = new MailMessage();
                mailMessage.From = new MailAddress(senderEmail, "Канцелярский магазин");
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.IsBodyHtml = true;
                mailMessage.To.Add(new MailAddress(toEmail, toName));

                await client.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки email: {ex.Message}");
                return false;
            }
        }


        public async Task<bool> SendOrderReadyNotification(string toEmail, string toName, int orderId)
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:SmtpServer"];
                var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]);
                var senderEmail = _configuration["EmailSettings:SenderEmail"];
                var senderPassword = _configuration["EmailSettings:SenderPassword"];
                var useSsl = bool.Parse(_configuration["EmailSettings:UseSsl"]);

                string subject = $"Канцелярский магазин. Заказ #{orderId} готов к выдаче!";

                string body = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='utf-8'>
                        <style>
                            body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 0; padding: 0; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background: linear-gradient(135deg, #28a745, #20c997); color: white; padding: 25px; text-align: center; border-radius: 10px 10px 0 0; }}
                            .header h2 {{ margin: 0; }}
                            .content {{ padding: 25px; background: #f8f9fa; border: 1px solid #e9ecef; border-top: none; }}
                            .content h3 {{ margin-top: 0; color: #2d3748; }}
                            .order-number {{ font-size: 20px; font-weight: bold; color: #28a745; }}
                            .ready-status {{ display: inline-block; background: #28a745; color: white; padding: 8px 16px; border-radius: 20px; font-size: 14px; margin: 15px 0; }}
                            .info-box {{ background: #e9ecef; padding: 15px; border-radius: 8px; margin: 15px 0; }}
                            .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #6c757d; background: #f8f9fa; border-radius: 0 0 10px 10px; }}
                            .button {{ display: inline-block; background: #28a745; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; margin-top: 15px; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h2>🏪 Канцелярский магазин</h2>
                                <p>Ваш заказ готов!</p>
                            </div>
                            <div class='content'>
                                <h3>Здравствуйте, {toName}!</h3>
                                <p>Хорошие новости! Ваш заказ <span class='order-number'>#{orderId}</span> <strong>готов к выдаче</strong>.</p>
                                <div style='text-align: center;'>
                                    <span class='ready-status'>✅ Заказ готов к выдаче</span>
                                </div>
                                <div class='info-box'>
                                    <p><strong>📍 Где забрать:</strong></p>
                                    <p>г. Гомель, ул. Богдановича, д. 16</p>
                                    <p><strong>🕐 Режим работы:</strong></p>
                                    <p>Пн-Пт: 9:00 - 20:00</p>
                                    <p>Сб-Вс: 10:00 - 18:00</p>
                                </div>
                                <p><strong>❗ Важно:</strong> При получении заказа обязательно возьмите с собой документ, удостоверяющий личность.</p>
                            </div>
                            <div class='footer'>
                                <p>Спасибо, что выбираете нас!</p>
                                <p>© 2026 Канцелярский магазин</p>
                            </div>
                        </div>
                    </body>
                    </html>
                ";

                using var client = new SmtpClient(smtpServer, smtpPort);
                client.EnableSsl = useSsl;
                client.Credentials = new NetworkCredential(senderEmail, senderPassword);

                var mailMessage = new MailMessage();
                mailMessage.From = new MailAddress(senderEmail, "Канцелярский магазин");
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.IsBodyHtml = true;
                mailMessage.To.Add(new MailAddress(toEmail, toName));

                await client.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки email: {ex.Message}");
                return false;
            }
        }
    }
}