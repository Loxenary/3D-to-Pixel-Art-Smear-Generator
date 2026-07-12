using System.Collections.Generic;
using System.Linq;

namespace SmearFramework
{
    public enum ValidationSeverity { Error, Warning }
    public class ValidationIssue
    {
        public ValidationSeverity Severity { get; }
        public string Message { get; }

        public ValidationIssue(ValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }
    public class ValidationReport
    {
        public IReadOnlyList<ValidationIssue> Issues { get; }
        public bool HasErrors => Issues.Any(i => i.Severity == ValidationSeverity.Error);

        public ValidationReport(IReadOnlyList<ValidationIssue> issues)
        {
            Issues = issues;
        }
    }
}
