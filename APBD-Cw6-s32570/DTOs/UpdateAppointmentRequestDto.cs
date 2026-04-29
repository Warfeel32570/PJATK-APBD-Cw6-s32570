using System.ComponentModel.DataAnnotations;

namespace APBD_Cw6_s32570.DTOs;

//PUT /api/appointments/{idAppointment}
public class UpdateAppointmentRequestDto
{
    public int IdPatient { get; set; }

    public int IdDoctor { get; set; }

    public DateTime AppointmentDate { get; set; }

    [Required]
    public string Status { get; set; } = string.Empty;

    [Required]
    [MaxLength(250)]
    public string Reason { get; set; } = string.Empty;

    public string? InternalNotes { get; set; }
}