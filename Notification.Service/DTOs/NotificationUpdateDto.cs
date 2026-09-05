using Notification.Service.Models;

namespace Notification.Service.DTOs;

public class NotificationUpdateDto
{
    // Deliberately no DestinataireId. Ownership is what scopes every read, so it is set once at
    // creation and never through an edit: a PUT is a full replacement, so a client that simply
    // omitted the field would silently null it and make the notification invisible to its own
    // recipient (the same pattern as ConventionUpdateDto and EvaluationUpdateDto).
    public DestinataireRole DestinataireRole { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Lu { get; set; }
    public DateTime DateCreation { get; set; }
}