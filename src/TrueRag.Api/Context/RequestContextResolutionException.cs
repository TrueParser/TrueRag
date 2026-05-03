namespace TrueRag.Api.Context;

public sealed class RequestContextResolutionException : Exception
{
    public RequestContextResolutionException(string message)
        : base(message)
    {
    }
}