// StudyApp.Application/Chat/ScopeUnavailableException.cs
namespace StudyApp.Application.Chat;

public class ScopeUnavailableException : Exception
{
    public ScopeUnavailableException(string message) : base(message) { }
}

