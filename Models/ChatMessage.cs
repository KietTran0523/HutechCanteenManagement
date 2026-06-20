using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace QuanLyCanTeenHutech.Models;

public class ChatMessage
{
    public int Id { get; set; }

    [Required]
    public string SenderId { get; set; } = string.Empty;

    [ForeignKey(nameof(SenderId))]
    [ValidateNever]
    public IdentityUser? Sender { get; set; }

    public string? ReceiverId { get; set; }

    [ForeignKey(nameof(ReceiverId))]
    [ValidateNever]
    public IdentityUser? Receiver { get; set; }

    [Required]
    [StringLength(30)]
    public string RoomType { get; set; } = ChatRoomTypes.General;

    [Required]
    [StringLength(450)]
    public string ConversationKey { get; set; } = "general";

    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.Now;

    public bool IsRead { get; set; }
}
