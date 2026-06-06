namespace Robe.Core.Exceptions;

public class TraitsParseException : Exception
{
    public TraitsParseException(string message) : base(message) { }
    public TraitsParseException(string message, Exception inner) : base(message, inner) { }
}
