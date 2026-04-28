using CenzasBackend.Models;

namespace CenzasBackend.Services;

public interface IEmailService
{
    Task SendApplicationEmailAsync(LoanApplication application);
}
