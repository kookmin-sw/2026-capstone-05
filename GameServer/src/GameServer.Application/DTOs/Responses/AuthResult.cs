namespace GameServer.Application.DTOs.Responses;

public enum AuthFailureCode
{
    None,
    InvalidInput,
    DuplicateUsername,
    UserNotFound,
    WrongPassword,
    DatabaseError
}

public sealed record AuthResult<T>(
    bool IsSuccess,
    T? Data,
    AuthFailureCode FailureCode,
    string Message
)
{
    public static AuthResult<T> Success(T data, string message = "OK") =>
        new(true, data, AuthFailureCode.None, message);

    public static AuthResult<T> Fail(AuthFailureCode failureCode, string message) =>
        new(false, default, failureCode, message);
}
