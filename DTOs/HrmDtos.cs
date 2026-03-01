namespace ReactPosApi.DTOs;

// ─── Department ───
public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? HODId { get; set; }
    public string? HODName { get; set; }
    public string? HODPicture { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateDepartmentDto
{
    public string Name { get; set; } = string.Empty;
    public int? HODId { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; } = true;
}

// ─── Designation ───
public class DesignationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateDesignationDto
{
    public string Name { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; } = true;
}

// ─── Shift ───
public class ShiftDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? WeekOff { get; set; }
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateShiftDto
{
    public string Name { get; set; } = string.Empty;
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? WeekOff { get; set; }
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; } = true;
}

// ─── LeaveType ───
public class LeaveTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quota { get; set; }
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateLeaveTypeDto
{
    public string Name { get; set; } = string.Empty;
    public int Quota { get; set; }
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; } = true;
}

// ─── Leave ───
public class LeaveDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeePicture { get; set; }
    public string? EmployeeRole { get; set; }
    public int LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal Days { get; set; }
    public string DayType { get; set; } = "Full Day";
    public string? Reason { get; set; }
    public string Status { get; set; } = "New";
    public int? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateLeaveDto
{
    public int EmployeeId { get; set; }
    public int LeaveTypeId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal Days { get; set; }
    public string DayType { get; set; } = "Full Day";
    public string? Reason { get; set; }
    public string Status { get; set; } = "New";
    public int? ApprovedById { get; set; }
}

// ─── Holiday ───
public class HolidayDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int Days { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateHolidayDto
{
    public string Title { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int Days { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; } = true;
}

// ─── Payroll ───
public class PayrollDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeePicture { get; set; }
    public string? EmployeeRole { get; set; }
    public string? EmployeeEmail { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal HRA { get; set; }
    public decimal Conveyance { get; set; }
    public decimal MedicalAllowance { get; set; }
    public decimal Bonus { get; set; }
    public decimal OtherAllowance { get; set; }
    public decimal PF { get; set; }
    public decimal ProfessionalTax { get; set; }
    public decimal TDS { get; set; }
    public decimal LoanDeduction { get; set; }
    public decimal OtherDeduction { get; set; }
    public decimal TotalAllowance { get; set; }
    public decimal TotalDeduction { get; set; }
    public decimal NetSalary { get; set; }
    public string Status { get; set; } = "Unpaid";
    public int? Month { get; set; }
    public int? Year { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatePayrollDto
{
    public int EmployeeId { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal HRA { get; set; }
    public decimal Conveyance { get; set; }
    public decimal MedicalAllowance { get; set; }
    public decimal Bonus { get; set; }
    public decimal OtherAllowance { get; set; }
    public decimal PF { get; set; }
    public decimal ProfessionalTax { get; set; }
    public decimal TDS { get; set; }
    public decimal LoanDeduction { get; set; }
    public decimal OtherDeduction { get; set; }
    public decimal TotalAllowance { get; set; }
    public decimal TotalDeduction { get; set; }
    public decimal NetSalary { get; set; }
    public string Status { get; set; } = "Unpaid";
    public int? Month { get; set; }
    public int? Year { get; set; }
}

// ─── Employee ───
public class EmployeeDto
{
    public int Id { get; set; }
    public string? EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Picture { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int? DesignationId { get; set; }
    public string? DesignationName { get; set; }
    public int? ShiftId { get; set; }
    public string? ShiftName { get; set; }
    public DateTime? DateOfJoining { get; set; }
    public decimal? BasicSalary { get; set; }
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateEmployeeDto
{
    public string FullName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public int? DepartmentId { get; set; }
    public int? DesignationId { get; set; }
    public int? ShiftId { get; set; }
    public DateTime? DateOfJoining { get; set; }
    public decimal? BasicSalary { get; set; }
    public string? Password { get; set; }
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; } = true;
}

// ─── Attendance DTOs ───

public class AttendanceDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeePicture { get; set; }
    public string? DesignationName { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = "Present";
    public string? ClockIn { get; set; }
    public string? ClockOut { get; set; }
    public string? Production { get; set; }
    public string? BreakTime { get; set; }
    public string? Overtime { get; set; }
    public string? TotalHours { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAttendanceDto
{
    public int EmployeeId { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = "Present";
    public string? ClockIn { get; set; }
    public string? ClockOut { get; set; }
    public string? Production { get; set; }
    public string? BreakTime { get; set; }
    public string? Overtime { get; set; }
    public string? TotalHours { get; set; }
}
