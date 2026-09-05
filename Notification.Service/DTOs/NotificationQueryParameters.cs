using Smartek.Common.Pagination;
using Notification.Service.Models;

namespace Notification.Service.DTOs;

/// <summary>
/// Filters for <c>GET /api/v1/notifications</c>, on top of <c>page</c>/<c>pageSize</c>.
/// </summary>
public class NotificationQueryParameters : PaginationQuery
{
    /// <summary>Only this recipient's notifications. Effective for ADMIN only — a LEARNER/TRAINER is already scoped by the visibility filter.</summary>
    public long? DestinataireId { get; set; }

    /// <summary><c>false</c> for the unread badge count, <c>true</c> for history.</summary>
    public bool? Lu { get; set; }

    public NotificationType? Type { get; set; }
}
