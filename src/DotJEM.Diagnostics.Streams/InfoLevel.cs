namespace DotJEM.Diagnostics.Streams;

public readonly struct InfoLevel
{
    // ReSharper disable InconsistentNaming
    public static readonly InfoLevel CRITICAL = new("CRITICAL");
    public static readonly InfoLevel ERROR = new("ERROR");
    public static readonly InfoLevel WARNING = new("WARNING");
    public static readonly InfoLevel INFO = new("INFO");
    public static readonly InfoLevel DEBUG = new("DEBUG");
    // ReSharper restore InconsistentNaming

    public string Value { get; }

    public InfoLevel(string value)
    {
        Value = value;
    }

    public bool Equals(InfoLevel other) => Value == other.Value;
    public override bool Equals(object obj) => obj is InfoLevel other && Equals(other);
    public override int GetHashCode() => (Value != null ? Value.GetHashCode() : 0);
}