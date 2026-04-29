using System.Data;
using APBD_Cw6_s32570.DTOs;
using APBD_Cw6_s32570.Exceptions;
using Microsoft.Data.SqlClient;

namespace APBD_Cw6_s32570.Services;

public class AppointmentService : IAppointmentService
{
    private readonly IConfiguration _configuration;

    public AppointmentService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(
        string? status,
        string? patientLastName,
        int? idDoctor)
    {
        var appointments = new List<AppointmentListDto>();

        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        await using var connection = new SqlConnection(connectionString);

        await using var command = new SqlCommand("""
            SELECT
                a.IdAppointment,
                a.AppointmentDate,
                a.Status,
                a.Reason,
                p.FirstName + N' ' + p.LastName AS PatientFullName,
                p.Email AS PatientEmail
            FROM dbo.Appointments a
            JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
            WHERE (@Status IS NULL OR a.Status = @Status)
              AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
              AND (@IdDoctor IS NULL OR a.IdDoctor = @IdDoctor)
            ORDER BY a.AppointmentDate;
            """, connection);

        command.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value =
            string.IsNullOrWhiteSpace(status) ? DBNull.Value : status;

        command.Parameters.Add("@PatientLastName", SqlDbType.NVarChar, 80).Value =
            string.IsNullOrWhiteSpace(patientLastName) ? DBNull.Value : patientLastName;

        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value =
            idDoctor.HasValue ? idDoctor.Value : DBNull.Value;

        await connection.OpenAsync();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            appointments.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                Reason = reader.GetString(reader.GetOrdinal("Reason")),
                PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail"))
            });
        }

        return appointments;
    }

    public async Task<AppointmentDetailsDto> GetAppointmentByIdAsync(int idAppointment)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        await using var connection = new SqlConnection(connectionString);

        await using var command = new SqlCommand("""
            SELECT
                a.IdAppointment,
                a.AppointmentDate,
                a.Status,
                a.Reason,
                a.InternalNotes,
                a.CreatedAt,

                p.IdPatient,
                p.FirstName + N' ' + p.LastName AS PatientFullName,
                p.Email AS PatientEmail,
                p.PhoneNumber AS PatientPhoneNumber,

                d.IdDoctor,
                d.FirstName + N' ' + d.LastName AS DoctorFullName,
                d.LicenseNumber AS DoctorLicenseNumber,

                s.Name AS SpecializationName
            FROM dbo.Appointments a
            JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
            JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
            JOIN dbo.Specializations s ON s.IdSpecialization = d.IdSpecialization
            WHERE a.IdAppointment = @IdAppointment;
            """, connection);

        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

        await connection.OpenAsync();

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new NotFoundException("Appointment not found.");
        }

        return new AppointmentDetailsDto
        {
            IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
            AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            Reason = reader.GetString(reader.GetOrdinal("Reason")),
            InternalNotes = reader.IsDBNull(reader.GetOrdinal("InternalNotes"))
                ? null
                : reader.GetString(reader.GetOrdinal("InternalNotes")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),

            IdPatient = reader.GetInt32(reader.GetOrdinal("IdPatient")),
            PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
            PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail")),
            PatientPhoneNumber = reader.GetString(reader.GetOrdinal("PatientPhoneNumber")),

            IdDoctor = reader.GetInt32(reader.GetOrdinal("IdDoctor")),
            DoctorFullName = reader.GetString(reader.GetOrdinal("DoctorFullName")),
            DoctorLicenseNumber = reader.GetString(reader.GetOrdinal("DoctorLicenseNumber")),
            SpecializationName = reader.GetString(reader.GetOrdinal("SpecializationName"))
        };
    }

    public async Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto request)
    {
        ValidateCreateRequest(request);

        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        if (!await IsPatientActiveAsync(connection, request.IdPatient))
        {
            throw new NotFoundException("Patient not found or inactive.");
        }

        if (!await IsDoctorActiveAsync(connection, request.IdDoctor))
        {
            throw new NotFoundException("Doctor not found or inactive.");
        }

        if (await DoctorHasAppointmentAtTimeAsync(
                connection,
                request.IdDoctor,
                request.AppointmentDate,
                null))
        {
            throw new ConflictException("Doctor already has a scheduled appointment at this time.");
        }

        await using var command = new SqlCommand("""
            INSERT INTO dbo.Appointments
                (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
            OUTPUT INSERTED.IdAppointment
            VALUES
                (@IdPatient, @IdDoctor, @AppointmentDate, N'Scheduled', @Reason);
            """, connection);

        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = request.IdPatient;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = request.AppointmentDate;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = request.Reason;

        var result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }

    public async Task<AppointmentDetailsDto> UpdateAppointmentAsync(
        int idAppointment,
        UpdateAppointmentRequestDto request)
    {
        ValidateUpdateRequest(request);

        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var currentAppointment = await GetCurrentAppointmentInfoAsync(connection, idAppointment);

        if (currentAppointment is null)
        {
            throw new NotFoundException("Appointment not found.");
        }

        if (!await IsPatientActiveAsync(connection, request.IdPatient))
        {
            throw new NotFoundException("Patient not found or inactive.");
        }

        if (!await IsDoctorActiveAsync(connection, request.IdDoctor))
        {
            throw new NotFoundException("Doctor not found or inactive.");
        }

        if (currentAppointment.Value.Status == "Completed"
            && currentAppointment.Value.AppointmentDate != request.AppointmentDate)
        {
            throw new ConflictException("Cannot change appointment date when appointment is completed.");
        }

        if (await DoctorHasAppointmentAtTimeAsync(
                connection,
                request.IdDoctor,
                request.AppointmentDate,
                idAppointment))
        {
            throw new ConflictException("Doctor already has another scheduled appointment at this time.");
        }

        await using var command = new SqlCommand("""
            UPDATE dbo.Appointments
            SET
                IdPatient = @IdPatient,
                IdDoctor = @IdDoctor,
                AppointmentDate = @AppointmentDate,
                Status = @Status,
                Reason = @Reason,
                InternalNotes = @InternalNotes
            WHERE IdAppointment = @IdAppointment;
            """, connection);

        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = request.IdPatient;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = request.AppointmentDate;
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = request.Status;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = request.Reason;
        command.Parameters.Add("@InternalNotes", SqlDbType.NVarChar, 500).Value =
            string.IsNullOrWhiteSpace(request.InternalNotes)
                ? DBNull.Value
                : request.InternalNotes;

        await command.ExecuteNonQueryAsync();

        return await GetAppointmentByIdAsync(idAppointment);
    }

    public async Task DeleteAppointmentAsync(int idAppointment)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var currentAppointment = await GetCurrentAppointmentInfoAsync(connection, idAppointment);

        if (currentAppointment is null)
        {
            throw new NotFoundException("Appointment not found.");
        }

        if (currentAppointment.Value.Status == "Completed")
        {
            throw new ConflictException("Cannot delete completed appointment.");
        }

        await using var command = new SqlCommand("""
            DELETE FROM dbo.Appointments
            WHERE IdAppointment = @IdAppointment;
            """, connection);

        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

        await command.ExecuteNonQueryAsync();
    }

    private static void ValidateCreateRequest(CreateAppointmentRequestDto request)
    {
        if (request.AppointmentDate < DateTime.Now)
        {
            throw new BadRequestException("Appointment date cannot be in the past.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BadRequestException("Reason cannot be empty.");
        }

        if (request.Reason.Length > 250)
        {
            throw new BadRequestException("Reason cannot be longer than 250 characters.");
        }
    }

    private static void ValidateUpdateRequest(UpdateAppointmentRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            throw new BadRequestException("Status cannot be empty.");
        }

        if (request.Status is not "Scheduled" and not "Completed" and not "Cancelled")
        {
            throw new BadRequestException("Status must be Scheduled, Completed or Cancelled.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BadRequestException("Reason cannot be empty.");
        }

        if (request.Reason.Length > 250)
        {
            throw new BadRequestException("Reason cannot be longer than 250 characters.");
        }

        if (request.InternalNotes is not null && request.InternalNotes.Length > 500)
        {
            throw new BadRequestException("Internal notes cannot be longer than 500 characters.");
        }
    }

    private static async Task<bool> IsPatientActiveAsync(
        SqlConnection connection,
        int idPatient)
    {
        await using var command = new SqlCommand("""
            SELECT COUNT(1)
            FROM dbo.Patients
            WHERE IdPatient = @IdPatient
              AND IsActive = 1;
            """, connection);

        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = idPatient;

        var result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result) > 0;
    }

    private static async Task<bool> IsDoctorActiveAsync(
        SqlConnection connection,
        int idDoctor)
    {
        await using var command = new SqlCommand("""
            SELECT COUNT(1)
            FROM dbo.Doctors
            WHERE IdDoctor = @IdDoctor
              AND IsActive = 1;
            """, connection);

        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = idDoctor;

        var result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result) > 0;
    }

    private static async Task<bool> DoctorHasAppointmentAtTimeAsync(
        SqlConnection connection,
        int idDoctor,
        DateTime appointmentDate,
        int? ignoredAppointmentId)
    {
        await using var command = new SqlCommand("""
            SELECT COUNT(1)
            FROM dbo.Appointments
            WHERE IdDoctor = @IdDoctor
              AND AppointmentDate = @AppointmentDate
              AND Status = N'Scheduled'
              AND (@IgnoredAppointmentId IS NULL OR IdAppointment <> @IgnoredAppointmentId);
            """, connection);

        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = idDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = appointmentDate;
        command.Parameters.Add("@IgnoredAppointmentId", SqlDbType.Int).Value =
            ignoredAppointmentId.HasValue
                ? ignoredAppointmentId.Value
                : DBNull.Value;

        var result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result) > 0;
    }

    private static async Task<(string Status, DateTime AppointmentDate)?> GetCurrentAppointmentInfoAsync(
        SqlConnection connection,
        int idAppointment)
    {
        await using var command = new SqlCommand("""
            SELECT Status, AppointmentDate
            FROM dbo.Appointments
            WHERE IdAppointment = @IdAppointment;
            """, connection);

        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return (
            reader.GetString(reader.GetOrdinal("Status")),
            reader.GetDateTime(reader.GetOrdinal("AppointmentDate"))
        );
    }
}