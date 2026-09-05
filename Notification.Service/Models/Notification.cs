using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Notification.Service.Models;

public class Notification
{
    public Guid Id { get; set; }

    /// <summary>
    /// auth-service <c>users.user_id</c> of the notification's intended recipient — a BIGINT there,
    /// hence <c>long</c>. Was a <c>Guid</c>, which could never equal the <c>userId</c> claim in a
    /// JWT and so made role-scoping on it impossible. Matches the pattern applied to
    /// <c>Evaluation.EncadrantId</c> in step 7.
    /// </summary>
    public long DestinataireId { get; set; }

    public DestinataireRole DestinataireRole { get; set; }

    public NotificationType Type { get; set; }

    [Column(TypeName = "text")]
    public string Message { get; set; } = string.Empty;

    public bool Lu { get; set; } = false;

    public DateTime DateCreation { get; set; }
}