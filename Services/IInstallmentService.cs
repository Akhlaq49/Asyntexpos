using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public interface IInstallmentService
{
    Task<List<InstallmentPlanDto>> GetAllAsync();
    Task<PagedResult<InstallmentPlanDto>> GetAllPagedAsync(PaginationQuery query);
    Task<InstallmentPlanDto?> GetByIdAsync(int id);
    Task<InstallmentPlanDto> CreateAsync(CreateInstallmentDto dto);
    Task<object> PayInstallmentAsync(int planId, int installmentNo, PayInstallmentDto paymentDto);
    Task<bool> CancelAsync(int id);

    // Party search
    Task<List<PartySearchDto>> SearchPartiesAsync(string? query);

    // Guarantor operations
    Task<GuarantorDto> AddGuarantorAsync(int planId, string name, string? so, string? phone, string? cnic, string? address, string? relationship, IFormFile? picture, int? existingPartyId = null);
    Task<GuarantorDto?> UpdateGuarantorAsync(int guarantorId, string name, string? so, string? phone, string? cnic, string? address, string? relationship, IFormFile? picture);
    Task<bool> DeleteGuarantorAsync(int guarantorId);

    // Preview / calculation endpoint (no DB write)
    InstallmentPreviewDto PreviewPlan(PreviewInstallmentDto dto);
}
