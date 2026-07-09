namespace Robe.Core.Exceptions;

public class ContentFilterException : Exception
{
    public ContentFilterException()
        : base("The request was rejected by the Azure OpenAI content filter.") { }

    public ContentFilterException(string message) : base(message) { }
}
