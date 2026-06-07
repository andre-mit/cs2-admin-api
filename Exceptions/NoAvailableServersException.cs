namespace Cs2Admin.API.Exceptions;

public class NoAvailableServersException : Exception
{
    public NoAvailableServersException() :  base()
    {
        
    }
    
    public NoAvailableServersException(string message) : base(message)
    {
        
    }
    
    public NoAvailableServersException(string message, Exception innerException) : base(message, innerException)
    {
        
    }
}