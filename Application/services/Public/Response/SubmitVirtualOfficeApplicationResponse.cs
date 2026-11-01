namespace Application.Services.Public.Response
{
    public class SubmitVirtualOfficeApplicationResponse
    {
        public Guid ApplicationId { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string ApplicantName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
