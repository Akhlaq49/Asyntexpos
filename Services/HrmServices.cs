using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

// ─── DepartmentService ───
public class DepartmentService : IDepartmentService
{
    private readonly AppDbContext _db;
    public DepartmentService(AppDbContext db) => _db = db;

    public async Task<List<DepartmentDto>> GetAllAsync()
    {
        var depts = await _db.Departments.Include(d => d.HOD).OrderByDescending(d => d.CreatedAt).ToListAsync();
        var memberCounts = await _db.Parties.Where(p => p.Role == "Employee" && p.DepartmentId != null)
            .GroupBy(p => p.DepartmentId).Select(g => new { DeptId = g.Key, Count = g.Count() }).ToListAsync();
        var countMap = memberCounts.ToDictionary(x => x.DeptId!.Value, x => x.Count);
        return depts.Select(d => new DepartmentDto
        {
            Id = d.Id, Name = d.Name, HODId = d.HODId, HODName = d.HOD?.FullName,
            HODPicture = d.HOD?.Picture, Description = d.Description, Status = d.Status,
            IsActive = d.IsActive, MemberCount = countMap.GetValueOrDefault(d.Id), CreatedAt = d.CreatedAt
        }).ToList();
    }

    public async Task<DepartmentDto?> GetByIdAsync(int id)
    {
        var d = await _db.Departments.Include(d => d.HOD).FirstOrDefaultAsync(d => d.Id == id);
        if (d == null) return null;
        var count = await _db.Parties.CountAsync(p => p.Role == "Employee" && p.DepartmentId == id);
        return new DepartmentDto
        {
            Id = d.Id, Name = d.Name, HODId = d.HODId, HODName = d.HOD?.FullName,
            HODPicture = d.HOD?.Picture, Description = d.Description, Status = d.Status,
            IsActive = d.IsActive, MemberCount = count, CreatedAt = d.CreatedAt
        };
    }

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto)
    {
        var entity = new Department { Name = dto.Name, HODId = dto.HODId, Description = dto.Description, Status = dto.Status, IsActive = dto.IsActive };
        _db.Departments.Add(entity);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<DepartmentDto?> UpdateAsync(int id, CreateDepartmentDto dto)
    {
        var entity = await _db.Departments.FindAsync(id);
        if (entity == null) return null;
        entity.Name = dto.Name; entity.HODId = dto.HODId; entity.Description = dto.Description;
        entity.Status = dto.Status; entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.Departments.FindAsync(id);
        if (entity == null) return (false, null);
        var empCount = await _db.Parties.CountAsync(p => p.DepartmentId == id);
        if (empCount > 0) return (false, $"Cannot delete. {empCount} employee(s) are in this department.");
        _db.Departments.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}

// ─── DesignationService ───
public class DesignationService : IDesignationService
{
    private readonly AppDbContext _db;
    public DesignationService(AppDbContext db) => _db = db;

    public async Task<List<DesignationDto>> GetAllAsync()
    {
        var items = await _db.Designations.Include(d => d.Department).OrderByDescending(d => d.CreatedAt).ToListAsync();
        var memberCounts = await _db.Parties.Where(p => p.Role == "Employee" && p.DesignationId != null)
            .GroupBy(p => p.DesignationId).Select(g => new { DesId = g.Key, Count = g.Count() }).ToListAsync();
        var countMap = memberCounts.ToDictionary(x => x.DesId!.Value, x => x.Count);
        return items.Select(d => new DesignationDto
        {
            Id = d.Id, Name = d.Name, DepartmentId = d.DepartmentId, DepartmentName = d.Department?.Name,
            Description = d.Description, Status = d.Status, IsActive = d.IsActive,
            MemberCount = countMap.GetValueOrDefault(d.Id), CreatedAt = d.CreatedAt
        }).ToList();
    }

    public async Task<DesignationDto?> GetByIdAsync(int id)
    {
        var d = await _db.Designations.Include(d => d.Department).FirstOrDefaultAsync(d => d.Id == id);
        if (d == null) return null;
        var count = await _db.Parties.CountAsync(p => p.Role == "Employee" && p.DesignationId == id);
        return new DesignationDto
        {
            Id = d.Id, Name = d.Name, DepartmentId = d.DepartmentId, DepartmentName = d.Department?.Name,
            Description = d.Description, Status = d.Status, IsActive = d.IsActive,
            MemberCount = count, CreatedAt = d.CreatedAt
        };
    }

    public async Task<DesignationDto> CreateAsync(CreateDesignationDto dto)
    {
        var entity = new Designation { Name = dto.Name, DepartmentId = dto.DepartmentId, Description = dto.Description, Status = dto.Status, IsActive = dto.IsActive };
        _db.Designations.Add(entity);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<DesignationDto?> UpdateAsync(int id, CreateDesignationDto dto)
    {
        var entity = await _db.Designations.FindAsync(id);
        if (entity == null) return null;
        entity.Name = dto.Name; entity.DepartmentId = dto.DepartmentId; entity.Description = dto.Description;
        entity.Status = dto.Status; entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.Designations.FindAsync(id);
        if (entity == null) return (false, null);
        var empCount = await _db.Parties.CountAsync(p => p.DesignationId == id);
        if (empCount > 0) return (false, $"Cannot delete. {empCount} employee(s) have this designation.");
        _db.Designations.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}

// ─── ShiftService ───
public class ShiftService : IShiftService
{
    private readonly AppDbContext _db;
    public ShiftService(AppDbContext db) => _db = db;

    public async Task<List<ShiftDto>> GetAllAsync() =>
        await _db.Shifts.OrderByDescending(s => s.CreatedAt)
            .Select(s => new ShiftDto { Id = s.Id, Name = s.Name, StartTime = s.StartTime, EndTime = s.EndTime, WeekOff = s.WeekOff, Status = s.Status, IsActive = s.IsActive, CreatedAt = s.CreatedAt })
            .ToListAsync();

    public async Task<ShiftDto?> GetByIdAsync(int id)
    {
        var s = await _db.Shifts.FindAsync(id);
        if (s == null) return null;
        return new ShiftDto { Id = s.Id, Name = s.Name, StartTime = s.StartTime, EndTime = s.EndTime, WeekOff = s.WeekOff, Status = s.Status, IsActive = s.IsActive, CreatedAt = s.CreatedAt };
    }

    public async Task<ShiftDto> CreateAsync(CreateShiftDto dto)
    {
        var entity = new Shift { Name = dto.Name, StartTime = dto.StartTime, EndTime = dto.EndTime, WeekOff = dto.WeekOff, Status = dto.Status, IsActive = dto.IsActive };
        _db.Shifts.Add(entity);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<ShiftDto?> UpdateAsync(int id, CreateShiftDto dto)
    {
        var entity = await _db.Shifts.FindAsync(id);
        if (entity == null) return null;
        entity.Name = dto.Name; entity.StartTime = dto.StartTime; entity.EndTime = dto.EndTime;
        entity.WeekOff = dto.WeekOff; entity.Status = dto.Status; entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.Shifts.FindAsync(id);
        if (entity == null) return (false, null);
        var empCount = await _db.Parties.CountAsync(p => p.ShiftId == id);
        if (empCount > 0) return (false, $"Cannot delete. {empCount} employee(s) are assigned this shift.");
        _db.Shifts.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}

// ─── LeaveTypeService ───
public class LeaveTypeService : ILeaveTypeService
{
    private readonly AppDbContext _db;
    public LeaveTypeService(AppDbContext db) => _db = db;

    public async Task<List<LeaveTypeDto>> GetAllAsync() =>
        await _db.LeaveTypes.OrderByDescending(lt => lt.CreatedAt)
            .Select(lt => new LeaveTypeDto { Id = lt.Id, Name = lt.Name, Quota = lt.Quota, Status = lt.Status, IsActive = lt.IsActive, CreatedAt = lt.CreatedAt })
            .ToListAsync();

    public async Task<LeaveTypeDto?> GetByIdAsync(int id)
    {
        var lt = await _db.LeaveTypes.FindAsync(id);
        if (lt == null) return null;
        return new LeaveTypeDto { Id = lt.Id, Name = lt.Name, Quota = lt.Quota, Status = lt.Status, IsActive = lt.IsActive, CreatedAt = lt.CreatedAt };
    }

    public async Task<LeaveTypeDto> CreateAsync(CreateLeaveTypeDto dto)
    {
        var entity = new LeaveType { Name = dto.Name, Quota = dto.Quota, Status = dto.Status, IsActive = dto.IsActive };
        _db.LeaveTypes.Add(entity);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<LeaveTypeDto?> UpdateAsync(int id, CreateLeaveTypeDto dto)
    {
        var entity = await _db.LeaveTypes.FindAsync(id);
        if (entity == null) return null;
        entity.Name = dto.Name; entity.Quota = dto.Quota; entity.Status = dto.Status; entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.LeaveTypes.FindAsync(id);
        if (entity == null) return (false, null);
        var leaveCount = await _db.Leaves.CountAsync(l => l.LeaveTypeId == id);
        if (leaveCount > 0) return (false, $"Cannot delete. {leaveCount} leave(s) reference this type.");
        _db.LeaveTypes.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}

// ─── LeaveService ───
public class LeaveService : ILeaveService
{
    private readonly AppDbContext _db;
    public LeaveService(AppDbContext db) => _db = db;

    private async Task<List<LeaveDto>> QueryAsync(int? employeeId = null)
    {
        var q = _db.Leaves.Include(l => l.Employee).Include(l => l.LeaveType).Include(l => l.ApprovedBy).AsQueryable();
        if (employeeId.HasValue) q = q.Where(l => l.EmployeeId == employeeId.Value);
        var items = await q.OrderByDescending(l => l.CreatedAt).ToListAsync();
        return items.Select(l => new LeaveDto
        {
            Id = l.Id, EmployeeId = l.EmployeeId, EmployeeName = l.Employee.FullName,
            EmployeePicture = l.Employee.Picture, EmployeeRole = l.Employee.Role,
            LeaveTypeId = l.LeaveTypeId, LeaveTypeName = l.LeaveType.Name,
            FromDate = l.FromDate, ToDate = l.ToDate, Days = l.Days, DayType = l.DayType,
            Reason = l.Reason, Status = l.Status, ApprovedById = l.ApprovedById,
            ApprovedByName = l.ApprovedBy?.FullName, CreatedAt = l.CreatedAt
        }).ToList();
    }

    public Task<List<LeaveDto>> GetAllAsync() => QueryAsync();
    public Task<List<LeaveDto>> GetByEmployeeAsync(int employeeId) => QueryAsync(employeeId);

    public async Task<LeaveDto?> GetByIdAsync(int id)
    {
        var list = await QueryAsync();
        return list.FirstOrDefault(l => l.Id == id);
    }

    public async Task<LeaveDto> CreateAsync(CreateLeaveDto dto)
    {
        var entity = new Leave
        {
            EmployeeId = dto.EmployeeId, LeaveTypeId = dto.LeaveTypeId, FromDate = dto.FromDate,
            ToDate = dto.ToDate, Days = dto.Days, DayType = dto.DayType, Reason = dto.Reason,
            Status = dto.Status, ApprovedById = dto.ApprovedById
        };
        _db.Leaves.Add(entity);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<LeaveDto?> UpdateAsync(int id, CreateLeaveDto dto)
    {
        var entity = await _db.Leaves.FindAsync(id);
        if (entity == null) return null;
        entity.EmployeeId = dto.EmployeeId; entity.LeaveTypeId = dto.LeaveTypeId;
        entity.FromDate = dto.FromDate; entity.ToDate = dto.ToDate; entity.Days = dto.Days;
        entity.DayType = dto.DayType; entity.Reason = dto.Reason; entity.Status = dto.Status;
        entity.ApprovedById = dto.ApprovedById;
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.Leaves.FindAsync(id);
        if (entity == null) return (false, null);
        _db.Leaves.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}

// ─── HolidayService ───
public class HolidayService : IHolidayService
{
    private readonly AppDbContext _db;
    public HolidayService(AppDbContext db) => _db = db;

    public async Task<List<HolidayDto>> GetAllAsync() =>
        await _db.Holidays.OrderByDescending(h => h.CreatedAt)
            .Select(h => new HolidayDto { Id = h.Id, Title = h.Title, FromDate = h.FromDate, ToDate = h.ToDate, Days = h.Days, Description = h.Description, Status = h.Status, IsActive = h.IsActive, CreatedAt = h.CreatedAt })
            .ToListAsync();

    public async Task<HolidayDto?> GetByIdAsync(int id)
    {
        var h = await _db.Holidays.FindAsync(id);
        if (h == null) return null;
        return new HolidayDto { Id = h.Id, Title = h.Title, FromDate = h.FromDate, ToDate = h.ToDate, Days = h.Days, Description = h.Description, Status = h.Status, IsActive = h.IsActive, CreatedAt = h.CreatedAt };
    }

    public async Task<HolidayDto> CreateAsync(CreateHolidayDto dto)
    {
        var entity = new Holiday { Title = dto.Title, FromDate = dto.FromDate, ToDate = dto.ToDate, Days = dto.Days, Description = dto.Description, Status = dto.Status, IsActive = dto.IsActive };
        _db.Holidays.Add(entity);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<HolidayDto?> UpdateAsync(int id, CreateHolidayDto dto)
    {
        var entity = await _db.Holidays.FindAsync(id);
        if (entity == null) return null;
        entity.Title = dto.Title; entity.FromDate = dto.FromDate; entity.ToDate = dto.ToDate;
        entity.Days = dto.Days; entity.Description = dto.Description; entity.Status = dto.Status; entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.Holidays.FindAsync(id);
        if (entity == null) return (false, null);
        _db.Holidays.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}

// ─── PayrollService ───
public class PayrollService : IPayrollService
{
    private readonly AppDbContext _db;
    public PayrollService(AppDbContext db) => _db = db;

    public async Task<List<PayrollDto>> GetAllAsync()
    {
        var items = await _db.Payrolls.Include(p => p.Employee).OrderByDescending(p => p.CreatedAt).ToListAsync();
        return items.Select(MapToDto).ToList();
    }

    public async Task<PayrollDto?> GetByIdAsync(int id)
    {
        var p = await _db.Payrolls.Include(p => p.Employee).FirstOrDefaultAsync(p => p.Id == id);
        return p == null ? null : MapToDto(p);
    }

    public async Task<PayrollDto> CreateAsync(CreatePayrollDto dto)
    {
        var entity = new Payroll
        {
            EmployeeId = dto.EmployeeId, BasicSalary = dto.BasicSalary,
            HRA = dto.HRA, Conveyance = dto.Conveyance, MedicalAllowance = dto.MedicalAllowance,
            Bonus = dto.Bonus, OtherAllowance = dto.OtherAllowance,
            PF = dto.PF, ProfessionalTax = dto.ProfessionalTax, TDS = dto.TDS,
            LoanDeduction = dto.LoanDeduction, OtherDeduction = dto.OtherDeduction,
            TotalAllowance = dto.TotalAllowance, TotalDeduction = dto.TotalDeduction,
            NetSalary = dto.NetSalary, Status = dto.Status, Month = dto.Month, Year = dto.Year
        };
        _db.Payrolls.Add(entity);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<PayrollDto?> UpdateAsync(int id, CreatePayrollDto dto)
    {
        var entity = await _db.Payrolls.FindAsync(id);
        if (entity == null) return null;
        entity.EmployeeId = dto.EmployeeId; entity.BasicSalary = dto.BasicSalary;
        entity.HRA = dto.HRA; entity.Conveyance = dto.Conveyance; entity.MedicalAllowance = dto.MedicalAllowance;
        entity.Bonus = dto.Bonus; entity.OtherAllowance = dto.OtherAllowance;
        entity.PF = dto.PF; entity.ProfessionalTax = dto.ProfessionalTax; entity.TDS = dto.TDS;
        entity.LoanDeduction = dto.LoanDeduction; entity.OtherDeduction = dto.OtherDeduction;
        entity.TotalAllowance = dto.TotalAllowance; entity.TotalDeduction = dto.TotalDeduction;
        entity.NetSalary = dto.NetSalary; entity.Status = dto.Status;
        entity.Month = dto.Month; entity.Year = dto.Year;
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.Payrolls.FindAsync(id);
        if (entity == null) return (false, null);
        _db.Payrolls.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    private static PayrollDto MapToDto(Payroll p) => new()
    {
        Id = p.Id, EmployeeId = p.EmployeeId, EmployeeName = p.Employee.FullName,
        EmployeePicture = p.Employee.Picture, EmployeeRole = p.Employee.Role,
        EmployeeEmail = p.Employee.Email,
        BasicSalary = p.BasicSalary, HRA = p.HRA, Conveyance = p.Conveyance,
        MedicalAllowance = p.MedicalAllowance, Bonus = p.Bonus, OtherAllowance = p.OtherAllowance,
        PF = p.PF, ProfessionalTax = p.ProfessionalTax, TDS = p.TDS,
        LoanDeduction = p.LoanDeduction, OtherDeduction = p.OtherDeduction,
        TotalAllowance = p.TotalAllowance, TotalDeduction = p.TotalDeduction,
        NetSalary = p.NetSalary, Status = p.Status, Month = p.Month, Year = p.Year, CreatedAt = p.CreatedAt
    };
}

// ─── EmployeeService ───
public class EmployeeService : IEmployeeService
{
    private readonly AppDbContext _db;
    public EmployeeService(AppDbContext db) => _db = db;

    public async Task<List<EmployeeDto>> GetAllAsync()
    {
        var employees = await _db.Parties
            .Where(p => p.Role == "Employee")
            .Include(p => p.Department).Include(p => p.Designation).Include(p => p.Shift)
            .OrderByDescending(p => p.CreatedAt).ToListAsync();
        return employees.Select(MapToDto).ToList();
    }

    public async Task<EmployeeDto?> GetByIdAsync(int id)
    {
        var p = await _db.Parties
            .Include(p => p.Department).Include(p => p.Designation).Include(p => p.Shift)
            .FirstOrDefaultAsync(p => p.Id == id && p.Role == "Employee");
        return p == null ? null : MapToDto(p);
    }

    public async Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto)
    {
        // Auto-generate employee ID
        var maxId = await _db.Parties.Where(p => p.Role == "Employee" && p.EmployeeId != null)
            .Select(p => p.EmployeeId).MaxAsync() ?? "EMP000";
        var num = int.Parse(maxId.Replace("EMP", "")) + 1;
        var empId = $"EMP{num:D3}";

        var entity = new Party
        {
            FullName = dto.FullName, LastName = dto.LastName, Email = dto.Email, Phone = dto.Phone,
            Address = dto.Address, City = dto.City, State = dto.State, Country = dto.Country,
            PostalCode = dto.PostalCode, DepartmentId = dto.DepartmentId, DesignationId = dto.DesignationId,
            ShiftId = dto.ShiftId, DateOfJoining = dto.DateOfJoining, BasicSalary = dto.BasicSalary,
            EmployeeId = empId, Role = "Employee", Status = dto.Status, IsActive = dto.IsActive
        };
        if (!string.IsNullOrEmpty(dto.Password))
            entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        _db.Parties.Add(entity);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<EmployeeDto?> UpdateAsync(int id, CreateEmployeeDto dto)
    {
        var entity = await _db.Parties.FirstOrDefaultAsync(p => p.Id == id && p.Role == "Employee");
        if (entity == null) return null;
        entity.FullName = dto.FullName; entity.LastName = dto.LastName; entity.Email = dto.Email;
        entity.Phone = dto.Phone; entity.Address = dto.Address; entity.City = dto.City;
        entity.State = dto.State; entity.Country = dto.Country; entity.PostalCode = dto.PostalCode;
        entity.DepartmentId = dto.DepartmentId; entity.DesignationId = dto.DesignationId;
        entity.ShiftId = dto.ShiftId; entity.DateOfJoining = dto.DateOfJoining;
        entity.BasicSalary = dto.BasicSalary; entity.Status = dto.Status; entity.IsActive = dto.IsActive;
        if (!string.IsNullOrEmpty(dto.Password))
            entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.Parties.FirstOrDefaultAsync(p => p.Id == id && p.Role == "Employee");
        if (entity == null) return (false, null);
        var payrollCount = await _db.Payrolls.CountAsync(p => p.EmployeeId == id);
        var leaveCount = await _db.Leaves.CountAsync(l => l.EmployeeId == id);
        if (payrollCount > 0 || leaveCount > 0)
            return (false, $"Cannot delete. Employee has {payrollCount} payroll(s) and {leaveCount} leave(s).");
        _db.Parties.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    private static EmployeeDto MapToDto(Party p) => new()
    {
        Id = p.Id, EmployeeId = p.EmployeeId, FullName = p.FullName, LastName = p.LastName,
        Email = p.Email, Phone = p.Phone, Picture = p.Picture,
        Address = p.Address, City = p.City, State = p.State, Country = p.Country, PostalCode = p.PostalCode,
        DepartmentId = p.DepartmentId, DepartmentName = p.Department?.Name,
        DesignationId = p.DesignationId, DesignationName = p.Designation?.Name,
        ShiftId = p.ShiftId, ShiftName = p.Shift?.Name,
        DateOfJoining = p.DateOfJoining, BasicSalary = p.BasicSalary,
        Status = p.Status, IsActive = p.IsActive, CreatedAt = p.CreatedAt
    };
}

// ─── Attendance Service ───
public class AttendanceService : IAttendanceService
{
    private readonly AppDbContext _db;
    public AttendanceService(AppDbContext db) => _db = db;

    public async Task<List<AttendanceDto>> GetAllAsync(DateTime? date = null, int? employeeId = null)
    {
        var q = _db.Attendances
            .Include(a => a.Employee).ThenInclude(e => e.Designation)
            .AsQueryable();
        if (date.HasValue) q = q.Where(a => a.Date == date.Value.Date);
        if (employeeId.HasValue) q = q.Where(a => a.EmployeeId == employeeId.Value);
        return await q.OrderByDescending(a => a.Date).ThenBy(a => a.Employee.FullName)
            .Select(a => MapToDto(a)).ToListAsync();
    }

    public async Task<AttendanceDto?> GetByIdAsync(int id)
    {
        var a = await _db.Attendances
            .Include(a => a.Employee).ThenInclude(e => e.Designation)
            .FirstOrDefaultAsync(a => a.Id == id);
        return a == null ? null : MapToDto(a);
    }

    public async Task<AttendanceDto> CreateAsync(CreateAttendanceDto dto)
    {
        var entity = new Attendance
        {
            EmployeeId = dto.EmployeeId, Date = dto.Date.Date, Status = dto.Status,
            ClockIn =TimeSpan.Parse(dto.ClockIn), ClockOut = TimeSpan.Parse(dto.ClockOut), Production = dto.Production,
            BreakTime = dto.BreakTime, Overtime = dto.Overtime, TotalHours = dto.TotalHours
        };
        _db.Attendances.Add(entity);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<AttendanceDto?> UpdateAsync(int id, CreateAttendanceDto dto)
    {
        var entity = await _db.Attendances.FindAsync(id);
        if (entity == null) return null;
        entity.EmployeeId = dto.EmployeeId; entity.Date = dto.Date.Date; entity.Status = dto.Status;
        entity.ClockIn =TimeSpan.Parse(dto.ClockIn); entity.ClockOut =TimeSpan.Parse(dto.ClockOut); entity.Production = dto.Production;
        entity.BreakTime = dto.BreakTime; entity.Overtime = dto.Overtime; entity.TotalHours = dto.TotalHours;
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.Attendances.FindAsync(id);
        if (entity == null) return (false, null);
        _db.Attendances.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    private static AttendanceDto MapToDto(Attendance a) => new()
    {
        Id = a.Id, EmployeeId = a.EmployeeId,
        EmployeeName = a.Employee?.FullName ?? "",
        EmployeePicture = a.Employee?.Picture,
        DesignationName = a.Employee?.Designation?.Name,
        Date = a.Date, Status = a.Status,
        ClockIn = a.ClockIn.ToString(), ClockOut = a.ClockOut.ToString(),
        Production = a.Production, BreakTime = a.BreakTime,
        Overtime = a.Overtime, TotalHours = a.TotalHours,
        CreatedAt = a.CreatedAt
    };
}
