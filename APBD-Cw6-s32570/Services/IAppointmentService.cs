using APBD_Cw6_s32570.DTOs;

namespace APBD_Cw6_s32570.Services;

public interface IAppointmentService
{
    Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(
        string? status,
        string? patientLastName,
        int? idDoctor);

    Task<AppointmentDetailsDto> GetAppointmentByIdAsync(int idAppointment);

    Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto request);

    Task<AppointmentDetailsDto> UpdateAppointmentAsync(
        int idAppointment,
        UpdateAppointmentRequestDto request);

    Task DeleteAppointmentAsync(int idAppointment);
}