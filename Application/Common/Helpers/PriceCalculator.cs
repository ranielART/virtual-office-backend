namespace Application.Common.Helpers
{
    /// <summary>
    /// Single source of truth for all publicly advertised pricing.
    /// No database lookup required — prices are hardcoded to match the published rate sheet.
    /// </summary>
    public static class PriceCalculator
    {
        // ── Internal pricing records ──────────────────────────────────────────

        private record CoworkingPricing(
            decimal BaseRate,
            bool IsHourly = false,
            decimal? StudentHourlyRate = null,    // flat student rate per hour (replaces BaseRate)
            decimal StudentDiscountPercent = 0m   // % off the flat package price
        );

        // ── Coworking price table ─────────────────────────────────────────────

        private static readonly Dictionary<string, CoworkingPricing> CoworkingPackages = new()
        {
            ["Coworking Seat: Hourly Pass"]              = new(50m,   IsHourly: true, StudentHourlyRate: 40m),
            ["Coworking Seat: Day Pass"]                 = new(600m,  StudentDiscountPercent: 10m),
            ["Coworking Seat: Weekly Pass"]              = new(2_700m,StudentDiscountPercent: 10m),
            ["Coworking Seat: Monthly Dedicated Desk"]   = new(8_500m,StudentDiscountPercent: 10m),
            ["Executive Meeting Room: Hourly"]           = new(800m,  IsHourly: true),
            ["Executive Meeting Room: Half-Day (5 hrs)"] = new(3_000m),
            ["Executive Meeting Room: Full Day (8 hrs)"] = new(5_500m),
            ["Collaboration Room: Hourly"]               = new(1_500m,IsHourly: true),
            ["Collaboration Room: Half-Day (4 hrs)"]     = new(5_500m),
            ["Collaboration Room: Full Day (8 hrs)"]     = new(9_500m),
        };

        // ── Virtual office price table ────────────────────────────────────────

        private static readonly Dictionary<string, decimal> VirtualOfficeMonthlyRates = new()
        {
            ["Virtual Office: Basic"]    = 1_700m,
            ["Virtual Office: Standard"] = 3_500m,
            ["Virtual Office: Premium"]  = 6_000m,
        };

        // ── Public helpers ────────────────────────────────────────────────────

        public static bool IsValidCoworkingPackage(string resourcePackage)
            => CoworkingPackages.ContainsKey(resourcePackage);

        public static bool IsValidVirtualOfficePackage(string servicePackage)
            => VirtualOfficeMonthlyRates.ContainsKey(servicePackage);

        public static bool IsHourlyCoworkingPackage(string resourcePackage)
            => CoworkingPackages.TryGetValue(resourcePackage, out var p) && p.IsHourly;

        // ── Result records ────────────────────────────────────────────────────

        public record CoworkingPriceResult(
            bool    IsHourly,
            decimal EffectiveHourlyRate,   // rate actually used (student or regular)
            decimal Hours,                 // 0 for flat packages
            decimal BaseAmount,
            decimal DiscountAmount,
            decimal TotalAmount,
            decimal DownpaymentAmount,
            bool    IsStudent,
            string  DiscountLabel          // human-readable discount description for email
        );

        public record VirtualOfficePriceResult(
            decimal MonthlyRate,
            int     DurationMonths,
            decimal TotalAmount,
            decimal DownpaymentAmount      // 50% of total amount
        );

        // ── Coworking calculation ─────────────────────────────────────────────

        /// <summary>
        /// Calculate the full price breakdown for a coworking booking.
        /// For hourly packages, <paramref name="hours"/> must be > 0.
        /// For flat packages, <paramref name="hours"/> is ignored.
        /// </summary>
        public static CoworkingPriceResult CalculateCoworking(
            string resourcePackage, bool isStudent, decimal hours = 0m)
        {
            if (!CoworkingPackages.TryGetValue(resourcePackage, out var p))
                throw new ArgumentException($"Unknown resource package: {resourcePackage}");

            decimal effectiveHourlyRate;
            decimal baseAmount;
            decimal discountAmount = 0m;
            string discountLabel = "No discount";

            if (p.IsHourly)
            {
                if (isStudent && p.StudentHourlyRate.HasValue)
                {
                    // Student gets a fixed lower rate per hour — not a percentage off
                    effectiveHourlyRate = p.StudentHourlyRate.Value;
                    baseAmount = Math.Round(hours * effectiveHourlyRate, 2);
                    discountLabel = $"Student rate (₱{effectiveHourlyRate}/hr instead of ₱{p.BaseRate}/hr)";
                }
                else
                {
                    effectiveHourlyRate = p.BaseRate;
                    baseAmount = Math.Round(hours * effectiveHourlyRate, 2);
                }
            }
            else
            {
                effectiveHourlyRate = p.BaseRate;
                baseAmount = p.BaseRate;

                if (isStudent && p.StudentDiscountPercent > 0)
                {
                    discountAmount = Math.Round(baseAmount * (p.StudentDiscountPercent / 100m), 2);
                    discountLabel = $"Student discount ({p.StudentDiscountPercent}%)";
                }
            }

            var totalAmount = baseAmount - discountAmount;
            var downpaymentAmount = Math.Round(totalAmount * 0.5m, 2);

            return new CoworkingPriceResult(
                IsHourly:           p.IsHourly,
                EffectiveHourlyRate:effectiveHourlyRate,
                Hours:              hours,
                BaseAmount:         baseAmount,
                DiscountAmount:     discountAmount,
                TotalAmount:        totalAmount,
                DownpaymentAmount:  downpaymentAmount,
                IsStudent:          isStudent,
                DiscountLabel:      discountLabel
            );
        }

        // ── Virtual office calculation ────────────────────────────────────────

        /// <summary>
        /// Calculate the full price breakdown for a virtual office subscription.
        /// Downpayment = 50% of the total amount.
        /// </summary>
        public static VirtualOfficePriceResult CalculateVirtualOffice(
            string servicePackage, int durationMonths)
        {
            if (!VirtualOfficeMonthlyRates.TryGetValue(servicePackage, out var monthlyRate))
                throw new ArgumentException($"Unknown service package: {servicePackage}");

            var totalAmount       = Math.Round(monthlyRate * durationMonths, 2);
            var downpaymentAmount = Math.Round(totalAmount * 0.5m, 2);

            return new VirtualOfficePriceResult(
                MonthlyRate:       monthlyRate,
                DurationMonths:    durationMonths,
                TotalAmount:       totalAmount,
                DownpaymentAmount: downpaymentAmount
            );
        }
    }
}
