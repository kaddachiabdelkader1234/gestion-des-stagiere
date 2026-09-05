using Notification.Service.Models;

namespace Notification.Service.DTOs;

public class NotificationReadDto
{
    public Guid Id { get; set; }
    /// <summary>auth-service user id of the notification's recipient — what scopes a LEARNER's reads.</summary>
    public long DestinataireId { get; set; }
    public DestinataireRole DestinataireRole { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Lu { get; set; }
    public DateTime DateCreation { get; set; }
}