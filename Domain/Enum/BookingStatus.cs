namespace Domain.Enum
{
    public enum BookingStatus
    {
        Pending = 1,
        PendingStudentVerification = 2,
        Confirmed = 3,
        CheckedIn = 4,
        CheckedOut = 5,
        Cancelled = 6,
        NoShow = 7,
        Overstayed = 8
    }
}
