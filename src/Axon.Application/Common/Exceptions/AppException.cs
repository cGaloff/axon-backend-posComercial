namespace Axon.Application.Common.Exceptions;

public class AppException : Exception
{
    public AppException(string message)
        : base(message)
    {
    }

    public AppException(IEnumerable<string> errors)
        : base(string.Join(" | ", errors))
    {
    }
}
