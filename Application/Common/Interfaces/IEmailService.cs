namespace Application.Common.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string htmlContent);
        Task SendEmailAsync(IEnumerable<string> recipients, string subject, string htmlContent);
    }
}
