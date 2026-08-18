namespace StudyApp.Application.Common;

public record ApiResponse<T>(bool IsSuccess, int StatusCode, T? Data, string? Message = null, Dictionary<string, string[]>? Errors = null)
{
    public static ApiResponse<T> Success(T data, int statusCode = 200) =>
        new(true, statusCode, data);

    public static ApiResponse<T> Fail(int statusCode, string message, Dictionary<string, string[]>? errors = null) =>
        new(false, statusCode, default, message, errors);
}
