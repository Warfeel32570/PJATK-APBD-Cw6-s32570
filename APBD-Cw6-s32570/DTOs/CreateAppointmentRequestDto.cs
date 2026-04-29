using System.ComponentModel.DataAnnotations;

namespace APBD_Cw6_s32570.DTOs;

//used for POST /api/appointments
public class CreateAppointmentRequestDto
{
    public int IdPatient { get; set; }

    public int IdDoctor { get; set; }

    public DateTime AppointmentDate { get; set; }

    [Required]
    [MaxLength(250)]
    public string Reason { get; set; } = string.Empty;
}