using FluentValidation;
using Notification.Service.DTOs;

namespace Notification.Service.Validation;

public class NotificationUpdateDtoValidator : AbstractValidator<NotificationUpdateDto>
{
    public NotificationUpdateDtoValidator()
    {
        RuleFor(x => x.Message).NotEmpty();
        RuleFor(x => x.DateCreation).NotEmpty();
    }
}