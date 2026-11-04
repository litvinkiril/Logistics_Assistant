using System.ComponentModel.DataAnnotations;

public sealed class ChangeDeliveryStatusRequest
{
    [Required(ErrorMessage = "Укажите статус.")]
    public string Status { get; set; } = string.Empty;
}