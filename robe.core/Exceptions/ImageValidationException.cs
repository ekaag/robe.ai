namespace Robe.Core.Exceptions;

public class ImageValidationException : Exception
{
    public ImageValidationException(string message) : base(message) { }
    public ImageValidationException(string message, Exception inner) : base(message, inner) { }
}
