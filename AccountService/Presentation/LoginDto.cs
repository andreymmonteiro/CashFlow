using System.ComponentModel.DataAnnotations;

namespace AccountService.Presentation
{
    public record LoginDto([EmailAddress] string Email, string Password);
}
