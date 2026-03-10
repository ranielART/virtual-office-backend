namespace Application.Services.Coworking.Request
{
    public class ValidateManualPaymentRequest
    {
        public bool IsApproved { get; set; }
        public string? RejectionReason { get; set; }
    }
}
