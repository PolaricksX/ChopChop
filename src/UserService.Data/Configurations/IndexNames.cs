namespace UserService.Data.Configurations;

internal static class IndexNames
{
    public const string UserEmail = "UX_Users_Email";
    public const string UserUsername = "UX_Users_NormalizedUsername";
    public const string UserSubject = "UX_Users_Subject";
    public const string IdempotencyKey = "UX_IdempotencyRecords_Subject_Key";
}
