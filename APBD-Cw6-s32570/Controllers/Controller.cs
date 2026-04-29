using APBD_Cw6_s32570.DTOs;
using APBD_Cw6_s32570.Exceptions;
using APBD_Cw6_s32570.Services;
using Microsoft.AspNetCore.Mvc;

namespace APBD_Cw6_s32570.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAppointments(
        [FromQuery] string? status,
        [FromQuery] string? patientLastName,
        [FromQuery] int? idDoctor)
    {
        var appointments = await _appointmentService.GetAppointmentsAsync(
            status,
            patientLastName,
            idDoctor);

        return Ok(appointments);
    }

    [HttpGet("{idAppointment:int}")]
    public async Task<IActionResult> GetAppointmentById(int idAppointment)
    {
        try
        {
            var appointment = await _appointmentService.GetAppointmentByIdAsync(idAppointment);

            return Ok(appointment);
        }
        catch (NotFoundException exception)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = exception.Message
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateAppointment(CreateAppointmentRequestDto request)
    {
        try
        {
            var idAppointment = await _appointmentService.CreateAppointmentAsync(request);

            return CreatedAtAction(
                nameof(GetAppointmentById),
                new { idAppointment },
                new { idAppointment });
        }
        catch (BadRequestException exception)
        {
            return BadRequest(new ErrorResponseDto
            {
                Message = exception.Message
            });
        }
        catch (NotFoundException exception)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = exception.Message
            });
        }
        catch (ConflictException exception)
        {
            return Conflict(new ErrorResponseDto
            {
                Message = exception.Message
            });
        }
    }

    [HttpPut("{idAppointment:int}")]
    public async Task<IActionResult> UpdateAppointment(
        int idAppointment,
        UpdateAppointmentRequestDto request)
    {
        try
        {
            var appointment = await _appointmentService.UpdateAppointmentAsync(
                idAppointment,
                request);

            return Ok(appointment);
        }
        catch (BadRequestException exception)
        {
            return BadRequest(new ErrorResponseDto
            {
                Message = exception.Message
            });
        }
        catch (NotFoundException exception)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = exception.Message
            });
        }
        catch (ConflictException exception)
        {
            return Conflict(new ErrorResponseDto
            {
                Message = exception.Message
            });
        }
    }

    [HttpDelete("{idAppointment:int}")]
    public async Task<IActionResult> DeleteAppointment(int idAppointment)
    {
        try
        {
            await _appointmentService.DeleteAppointmentAsync(idAppointment);

            return NoContent();
        }
        catch (NotFoundException exception)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = exception.Message
            });
        }
        catch (ConflictException exception)
        {
            return Conflict(new ErrorResponseDto
            {
                Message = exception.Message
            });
        }
    }
}