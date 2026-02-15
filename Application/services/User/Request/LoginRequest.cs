using System.ComponentModel.DataAnnotations;

namespace Application.Services.User.Request
{
    public class LoginRequest
    {
    
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
