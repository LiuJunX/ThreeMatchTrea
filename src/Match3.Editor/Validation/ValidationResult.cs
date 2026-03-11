using System.Collections.Generic;
using System.Linq;

namespace Match3.Editor.Validation;

public enum Severity
{
    Warning,
    Error
}

public class ValidationMessage
{
    public Severity Severity { get; }
    public string Message { get; }

    public ValidationMessage(Severity severity, string message)
    {
        Severity = severity;
        Message = message;
    }

    public override string ToString() => $"[{Severity}] {Message}";
}

public class ValidationResult
{
    public static readonly ValidationResult Valid = new ValidationResult(new List<ValidationMessage>());

    public IReadOnlyList<ValidationMessage> Messages { get; }
    public bool HasErrors => Messages.Any(m => m.Severity == Severity.Error);
    public bool IsValid => !HasErrors;

    public ValidationResult(List<ValidationMessage> messages)
    {
        Messages = messages;
    }
}
