public enum AuthResultCode
{
    Success,
    InvalidInput,
    DuplicateUsername,
    DuplicateNickname,
    UserNotFound,
    WrongPassword,
    NetworkError,
    DatabaseError
}

public sealed class AuthResult
{
    public AuthResultCode Code;
    public string Message;
    public UserAccount User;

    public bool IsSuccess => Code == AuthResultCode.Success;

    public AuthResult(AuthResultCode code, string message, UserAccount user = null)
    {
        Code = code;
        Message = message;
        User = user;
    }
}
