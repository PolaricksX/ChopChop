namespace Equipment.Data.Messaging;

public sealed class EquipmentMessagingOptions
{
    public const string SectionName = "Messaging";

    public bool Enabled { get; set; }

    public string ConnectionString { get; set; } = string.Empty;

    public static bool IsValid(EquipmentMessagingOptions options) =>
        !options.Enabled ||
        (Uri.TryCreate(options.ConnectionString, UriKind.Absolute, out var uri)
            && uri.Scheme is "amqp" or "amqps");
}
