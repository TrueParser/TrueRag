namespace TrueRag.Core.Primitives;

public sealed record Error(
    string Code,
    string Message,
    ErrorType Type = ErrorType.Validation);

public enum ErrorType
{
    Validation = 0,
    NotFound = 1,
    Conflict = 2,
    Forbidden = 3,
    Unauthorized = 4,
    Unavailable = 5,
    Unexpected = 6
}