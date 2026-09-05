using FluentValidation;
using Notification.Service.DTOs;

namespace Notification.Service.Validation;

public class NotificationCreateDtoValidator : AbstractValidator<NotificationCreateDto>
{
    public NotificationCreateDtoValidator()
    {
        // No DestinataireId rule: it is set by the server from the JWT, never from the body.
        RuleFor(x => x.Message).NotEmpty();
        RuleFor(x => x.DateCreation).NotEmpty();
    }
}