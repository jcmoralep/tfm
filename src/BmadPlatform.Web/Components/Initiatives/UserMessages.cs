using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Domain.Common;
using FluentValidation;

namespace BmadPlatform.Web.Components.Initiatives;

/// <summary>
/// Turns expected business failures into Spanish messages for the pages. Anything else (a defect or an
/// infrastructure failure) is not mapped, so the error boundary handles it.
/// </summary>
public static class UserMessages
{
    public static bool TryGet(Exception exception, out IReadOnlyList<string> messages)
    {
        switch (exception)
        {
            case ValidationException validation:
                messages = validation.Errors
                    .Select(failure => failure.ErrorMessage)
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .Distinct()
                    .ToList();
                return messages.Count > 0;

            case NotFoundException notFound:
                messages = [notFound.Message];
                return true;

            case DomainException domain:
                messages = [domain.Message];
                return true;

            default:
                messages = [];
                return false;
        }
    }
}
