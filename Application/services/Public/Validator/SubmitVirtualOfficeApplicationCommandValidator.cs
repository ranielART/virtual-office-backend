using Application.Common.Helpers;
using Application.Services.Public.Command;
using Domain.Enum;
using FluentValidation;

namespace Application.Services.Public.Validator
{
    public class SubmitVirtualOfficeApplicationCommandValidator
        : AbstractValidator<SubmitVirtualOfficeApplicationCommand>
    {
        public SubmitVirtualOfficeApplicationCommandValidator()
        {
            RuleFor(x => x.Request.FirstName)
                .NotEmpty().WithMessage("First name is required.")
                .MaximumLength(100);

            RuleFor(x => x.Request.LastName)
                .NotEmpty().WithMessage("Last name is required.")
                .MaximumLength(100);

            RuleFor(x => x.Request.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("A valid email address is required.");

            RuleFor(x => x.Request.PhoneCountryCode)
                .NotEmpty().WithMessage("Phone country code is required.")
                .Matches(@"^\+\d{1,4}$").WithMessage("Phone country code must be in the format '+XX' (e.g. '+63').");

            RuleFor(x => x.Request.PhoneNumber)
                .NotEmpty().WithMessage("Phone number is required.")
                .Matches(@"^\d{7,12}$").WithMessage("Phone number must be between 7 and 12 digits.");

            RuleFor(x => x.Request.City)
                .NotEmpty().WithMessage("City is required.")
                .MaximumLength(100);

            RuleFor(x => x.Request.Country)
                .NotEmpty().WithMessage("Country is required.")
                .MaximumLength(100);

            RuleFor(x => x.Request.BusinessType)
                .IsInEnum().WithMessage("Business type must be sole_proprietorship, partnership, or corporation.");

            RuleFor(x => x.Request.DurationMonths)
                .GreaterThanOrEqualTo(1).WithMessage("Duration must be at least 1 month.");

            // Business documents: max 5 files, up to 10 MB each
            RuleFor(x => x.BusinessDocuments)
                .NotEmpty().WithMessage("At least one business document is required.")
                .Must(f => f.Count <= 5).WithMessage("Maximum 5 business documents are allowed.");

            RuleForEach(x => x.BusinessDocuments)
                .ChildRules(file =>
                {
                    file.RuleFor(f => f.Length)
                        .LessThanOrEqualTo(10 * 1024 * 1024)
                        .WithMessage("Each document must not exceed 10 MB.");

                    file.RuleFor(f => Path.GetExtension(f.FileName).ToLowerInvariant())
                        .Must(ext => FileSecurityHelper.AllowedDocumentExtensions.Contains(ext))
                        .WithMessage("Unsupported file extension. Allowed: jpg, png, gif, webp, heic, pdf, doc, docx.");

                    file.RuleFor(f => f.ContentType)
                        .Must(ct => FileSecurityHelper.AllowedDocumentContentTypes.Contains(ct))
                        .WithMessage("Unsupported file type.");

                    file.RuleFor(f => f)
                        .Must(f =>
                        {
                            using var stream = f.OpenReadStream();
                            return FileSecurityHelper.VerifyMagicBytes(stream, f.ContentType);
                        })
                        .WithMessage("File content does not match the declared file type.");

                    // PDF-specific: scan for embedded JavaScript or macros
                    file.RuleFor(f => f)
                        .MustAsync(async (f, cancellationToken) =>
                        {
                            if (!f.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                                return true;
                            using var stream = f.OpenReadStream();
                            return !await FileSecurityHelper.ContainsPdfMaliciousContentAsync(stream, cancellationToken);
                        })
                        .WithMessage("This PDF contains potentially malicious content (embedded JavaScript or macros) and cannot be accepted.");
                });

            // Down-payment screenshots: max 2 files, up to 5 MB each, images only
            RuleFor(x => x.DownPaymentScreenshots)
                .NotEmpty().WithMessage("At least one down-payment screenshot is required.")
                .Must(f => f.Count <= 2).WithMessage("Maximum 2 down-payment screenshots are allowed.");

            RuleForEach(x => x.DownPaymentScreenshots)
                .ChildRules(file =>
                {
                    file.RuleFor(f => f.Length)
                        .LessThanOrEqualTo(5 * 1024 * 1024)
                        .WithMessage("Each screenshot must not exceed 5 MB.");

                    file.RuleFor(f => f.ContentType)
                        .Must(ct => FileSecurityHelper.AllowedImageContentTypes.Contains(ct))
                        .WithMessage("Only JPEG, PNG, and WebP images are accepted for payment screenshots.");

                    file.RuleFor(f => f)
                        .Must(f =>
                        {
                            using var stream = f.OpenReadStream();
                            return FileSecurityHelper.VerifyMagicBytes(stream, f.ContentType);
                        })
                        .WithMessage("File content does not match the declared file type.");
                });
        }
    }
}
