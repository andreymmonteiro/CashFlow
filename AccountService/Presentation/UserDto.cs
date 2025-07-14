using System.ComponentModel.DataAnnotations;

namespace AccountService.Presentation
{
    public record class UserDto(string Username,[EmailAddress] string Email, string Password);
}
