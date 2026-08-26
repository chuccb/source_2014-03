namespace KncWX2Server.Common;

public class KncException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public static class KncExceptionFormatter
{
    public static string Format(Exception exception, string? context = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
        var location = string.IsNullOrWhiteSpace(context) ? string.Empty : $", {context}";
        return $"{timestamp}{location}, {exception.GetType().Name}: {exception.Message}";
    }

    public static string FormatUnknown(string? context = null)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
        var location = string.IsNullOrWhiteSpace(context) ? string.Empty : $", {context}";
        return $"{timestamp}{location}, Unknown Exception Occured.";
    }
}
