using Microsoft.AspNetCore.Mvc;
using StudyApp.Application.Common;

namespace StudyApp.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult Success<T>(T data, int statusCode = 200) =>
        StatusCode(statusCode, ApiResponse<T>.Success(data, statusCode));

    protected IActionResult Fail(int statusCode, string message) =>
        StatusCode(statusCode, ApiResponse<object>.Fail(statusCode, message));
}
