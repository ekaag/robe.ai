namespace Robe.Core.Exceptions;

public class GarmentNotDetectedException : Exception
{
    public GarmentNotDetectedException() : base("No garment detected in the image.") { }
    public GarmentNotDetectedException(string message) : base(message) { }
}
