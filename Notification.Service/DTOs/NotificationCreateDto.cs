using Notification.Service.Models;

namespace Notification.Service.DTOs;

public class NotificationCreateDto
{
    public long DestinataireId { get; set; }
    public DestinataireRole DestinataireRole { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Lu { get; set; } = false;
    public DateTime DateCreation { get; set; }
}