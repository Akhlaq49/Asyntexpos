using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IDepartmentService
{
    Task<List<DepartmentDto>> GetAllAsync();
    Task<DepartmentDto?> GetByIdAsync(int id);
    Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto);
    Task<DepartmentDto?> UpdateAsync(int id, CreateDepartmentDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}

public interface IDesignationService
{
    Task<List<DesignationDto>> GetAllAsync();
    Task<DesignationDto?> GetByIdAsync(int id);
    Task<DesignationDto> CreateAsync(CreateDesignationDto dto);
    Task<DesignationDto?> UpdateAsync(int id, CreateDesignationDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}

public interface IShiftService
{
    Task<List<ShiftDto>> GetAllAsync();
    Task<ShiftDto?> GetByIdAsync(int id);
    Task<ShiftDto> CreateAsync(CreateShiftDto dto);
    Task<ShiftDto?> UpdateAsync(int id, CreateShiftDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}

public interface ILeaveTypeService
{
    Task<List<LeaveTypeDto>> GetAllAsync();
    Task<LeaveTypeDto?> GetByIdAsync(int id);
    Task<LeaveTypeDto> CreateAsync(CreateLeaveTypeDto dto);
    Task<LeaveTypeDto?> UpdateAsync(int id, CreateLeaveTypeDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}

public interface ILeaveService
{
    Task<List<LeaveDto>> GetAllAsync();
    Task<List<LeaveDto>> GetByEmployeeAsync(int employeeId);
    Task<LeaveDto?> GetByIdAsync(int id);
    Task<LeaveDto> CreateAsync(CreateLeaveDto dto);
    Task<LeaveDto?> UpdateAsync(int id, CreateLeaveDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}

public interface IHolidayService
{
    Task<List<HolidayDto>> GetAllAsync();
    Task<HolidayDto?> GetByIdAsync(int id);
    Task<HolidayDto> CreateAsync(CreateHolidayDto dto);
    Task<HolidayDto?> UpdateAsync(int id, CreateHolidayDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}

public interface IPayrollService
{
    Task<List<PayrollDto>> GetAllAsync();
    Task<PayrollDto?> GetByIdAsync(int id);
    Task<PayrollDto> CreateAsync(CreatePayrollDto dto);
    Task<PayrollDto?> UpdateAsync(int id, CreatePayrollDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}

public interface IEmployeeService
{
    Task<List<EmployeeDto>> GetAllAsync();
    Task<EmployeeDto?> GetByIdAsync(int id);
    Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto);
    Task<EmployeeDto?> UpdateAsync(int id, CreateEmployeeDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}

public interface IAttendanceService
{
    Task<List<AttendanceDto>> GetAllAsync(DateTime? date = null, int? employeeId = null);
    Task<AttendanceDto?> GetByIdAsync(int id);
    Task<AttendanceDto> CreateAsync(CreateAttendanceDto dto);
    Task<AttendanceDto?> UpdateAsync(int id, CreateAttendanceDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}
