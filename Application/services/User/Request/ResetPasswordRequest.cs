namespace Application.Services.User.Request
{
    public class ResetPasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
