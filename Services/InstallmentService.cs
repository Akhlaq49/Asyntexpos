using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class InstallmentService : IInstallmentService
{
    private readonly AppDbContext _db;
    private readonly IFileService _fileService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InstallmentService(AppDbContext db, IFileService fileService,
        IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _fileService = fileService;
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    // ── Queries ────────────────────────────────────────────

    public async Task<PagedResult<InstallmentPlanDto>> GetAllPagedAsync(PaginationQuery query)
    {
        var q = _db.InstallmentPlans.AsQueryable();

        if (!string.IsNullOrEmpty(query.Search))
        {
            var s = query.Search.ToLower();
            q = q.Where(p => p.Customer!.FullName.ToLower().Contains(s) ||
                             (p.Product != null && p.Product.ProductName.ToLower().Contains(s)) ||
                             p.PlanGuarantors.Any(g => g.Party!.FullName.ToLower().Contains(s) ||
                                                       (g.Party.Phone != null && g.Party.Phone.Contains(s)) ||
                                                       (g.Party.Cnic != null && g.Party.Cnic.Contains(s))));
        }

        if (!string.IsNullOrEmpty(query.Status))
            q = q.Where(p => p.Status == query.Status);

        q = q.OrderByDescending(p => p.CreatedAt);

        var totalCount = await q.CountAsync();
        var plans = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(p => p.Customer)
            .Include(p => p.Product).ThenInclude(pr => pr!.Images)
            .Include(p => p.Schedule.OrderBy(s => s.InstallmentNo))
            .Include(p => p.PlanGuarantors).ThenInclude(pg => pg.Party)
            .ToListAsync();

        return new PagedResult<InstallmentPlanDto>
        {
            Items = plans.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<List<InstallmentPlanDto>> GetAllAsync()
    {
        var plans = await _db.InstallmentPlans
            .Include(p => p.Customer)
            .Include(p => p.Product).ThenInclude(pr => pr!.Images)
            .Include(p => p.Schedule.OrderBy(s => s.InstallmentNo))
            .Include(p => p.PlanGuarantors).ThenInclude(pg => pg.Party)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return plans.Select(MapToDto).ToList();
    }

    public async Task<InstallmentPlanDto?> GetByIdAsync(int id)
    {
        var plan = await _db.InstallmentPlans
            .Include(p => p.Customer)
            .Include(p => p.Product).ThenInclude(pr => pr!.Images)
            .Include(p => p.Schedule.OrderBy(s => s.InstallmentNo))
            .Include(p => p.PlanGuarantors).ThenInclude(pg => pg.Party)
            .FirstOrDefaultAsync(p => p.Id == id);

        return plan == null ? null : MapToDto(plan);
    }

    // ── Create ─────────────────────────────────────────────

    public async Task<InstallmentPlanDto> CreateAsync(CreateInstallmentDto dto)
    {
        var customer = await _db.Parties.FirstOrDefaultAsync(p => p.Id == dto.CustomerId && p.Role == "Customer")
            ?? throw new KeyNotFoundException("Customer not found");

        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == dto.ProductId)
            ?? throw new KeyNotFoundException("Product not found");

        if (product.Quantity < 1)
            throw new InvalidOperationException("Insufficient product quantity in inventory");

        // Deduct one unit from inventory
        product.Quantity -= 1;
        product.UpdatedAt = DateTime.UtcNow;

        var productPrice = product.Price;
        var baseAmount = dto.FinanceAmount.HasValue && dto.FinanceAmount.Value > 0
            ? dto.FinanceAmount.Value
            : productPrice;
        var financedAmount = baseAmount - dto.DownPayment;
        var emi = CalculateEMI(financedAmount, dto.InterestRate, dto.Tenure);
        var totalPayable = dto.DownPayment + emi * dto.Tenure;
        var totalInterest = totalPayable - baseAmount;

        var plan = new InstallmentPlan
        {
            CustomerId = dto.CustomerId,
            ProductId = dto.ProductId,
            ProductPrice = productPrice,
            FinanceAmount = dto.FinanceAmount,
            DownPayment = dto.DownPayment,
            FinancedAmount = financedAmount,
            InterestRate = dto.InterestRate,
            Tenure = dto.Tenure,
            EmiAmount = Math.Round(emi, 2),
            TotalPayable = Math.Round(totalPayable, 2),
            TotalInterest = Math.Round(totalInterest, 2),
            StartDate = dto.StartDate,
            Status = "active",
            PaidInstallments = 0,
            RemainingInstallments = dto.Tenure
        };

        var schedule = GenerateSchedule(financedAmount, dto.InterestRate, dto.Tenure, dto.StartDate);
        plan.NextDueDate = schedule.FirstOrDefault(s => s.Status == "due" || s.Status == "upcoming")?.DueDate ?? "";

        _db.InstallmentPlans.Add(plan);
        await _db.SaveChangesAsync();

        foreach (var entry in schedule)
        {
            entry.PlanId = plan.Id;
            _db.RepaymentEntries.Add(entry);
        }
        await _db.SaveChangesAsync();

        // Reload navigations
        await _db.Entry(plan).Collection(p => p.Schedule).LoadAsync();
        await _db.Entry(plan).Reference(p => p.Customer).LoadAsync();
        await _db.Entry(plan).Reference(p => p.Product).LoadAsync();
        if (plan.Product != null)
            await _db.Entry(plan.Product).Collection(pr => pr.Images).LoadAsync();

        // ── Queue SMS notifications for plan creation ──
        await QueuePlanCreationSms(plan, customer);

        return MapToDto(plan);
    }

    /// <summary>
    /// Queue SMS + WhatsApp to customer and all guarantors when a plan is created.
    /// Generates a PDF invoice and attaches the public URL.
    /// </summary>
    private async Task QueuePlanCreationSms(InstallmentPlan plan, Party customer)
    {
        var productName = plan.Product?.ProductName ?? "N/A";
        var firstDue = plan.Schedule
            .OrderBy(s => s.InstallmentNo)
            .FirstOrDefault(s => s.Status == "upcoming" || s.Status == "due");

        // Generate PDF invoice (best-effort — don't break plan creation if PDF fails)
        string? pdfUrl = null;
        try
        {
            var pdfService = _serviceProvider.GetService<IInstallmentInvoicePdfService>();
            if (pdfService != null)
            {
                var pdfRelativePath = pdfService.GeneratePlanCreationInvoice(plan);
                pdfUrl = BuildPublicUrl(pdfRelativePath);
            }
        }
        catch { /* PDF generation failed — continue without attachment */ }

        // SMS to customer (both SMS and WhatsApp channels)
        if (!string.IsNullOrWhiteSpace(customer.Phone))
        {
            var msg = $"Assalam o Alaikum {customer.FullName},\n"
                + $"Your installment plan has been created:\n"
                + $"Product: {productName}\n"
                + $"Total: Rs {plan.TotalPayable:N0}\n"
                + $"Down Payment: Rs {plan.DownPayment:N0}\n"
                + $"EMI: Rs {plan.EmiAmount:N0}/month x {plan.Tenure} months\n"
                + (firstDue != null ? $"First Due: {firstDue.DueDate}\n" : "")
                + $"Please ensure timely payments. JazakAllah!";

            // SMS channel
            _db.SmsMessages.Add(new SmsMessage
            {
                TenantId = plan.TenantId,
                To = customer.Phone,
                Message = msg,
                Channel = "sms",
                Reference = $"PLAN-{plan.Id}",
                Status = "pending",
                MediaUrl = pdfUrl
            });

            // WhatsApp channel
            _db.SmsMessages.Add(new SmsMessage
            {
                TenantId = plan.TenantId,
                To = customer.Phone,
                Message = msg,
                Channel = "whatsapp",
                Reference = $"PLAN-{plan.Id}",
                Status = "pending",
                MediaUrl = pdfUrl
            });
        }

        // SMS to guarantors (both channels)
        var guarantors = await _db.PlanGuarantors
            .Include(g => g.Party)
            .Where(g => g.PlanId == plan.Id)
            .ToListAsync();

        foreach (var g in guarantors)
        {
            if (string.IsNullOrWhiteSpace(g.Party?.Phone)) continue;

            var gMsg = $"Assalam o Alaikum {g.Party.FullName},\n"
                + $"You have been added as a guarantor for an installment plan:\n"
                + $"Customer: {customer.FullName}\n"
                + $"Product: {productName}\n"
                + $"Total: Rs {plan.TotalPayable:N0}\n"
                + $"EMI: Rs {plan.EmiAmount:N0}/month x {plan.Tenure} months\n"
                + $"This is for your information. JazakAllah!";

            // SMS channel
            _db.SmsMessages.Add(new SmsMessage
            {
                TenantId = plan.TenantId,
                To = g.Party.Phone,
                Message = gMsg,
                Channel = "sms",
                Reference = $"PLAN-{plan.Id}-GUARANTOR",
                Status = "pending",
                MediaUrl = pdfUrl
            });

            // WhatsApp channel
            _db.SmsMessages.Add(new SmsMessage
            {
                TenantId = plan.TenantId,
                To = g.Party.Phone,
                Message = gMsg,
                Channel = "whatsapp",
                Reference = $"PLAN-{plan.Id}-GUARANTOR",
                Status = "pending",
                MediaUrl = pdfUrl
            });
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Queue SMS + WhatsApp to customer when an installment payment is made.
    /// Generates a PDF receipt and attaches the public URL.
    /// </summary>
    private async Task QueuePaymentSms(InstallmentPlan plan, RepaymentEntry entry, decimal paidAmount)
    {
        var customerPhone = plan.Customer?.Phone;
        if (string.IsNullOrWhiteSpace(customerPhone)) return;

        // Generate PDF receipt (best-effort — don't break payment if PDF fails)
        string? pdfUrl = null;
        try
        {
            var pdfService = _serviceProvider.GetService<IInstallmentInvoicePdfService>();
            if (pdfService != null)
            {
                var pdfRelativePath = pdfService.GeneratePaymentReceipt(plan, entry, paidAmount);
                pdfUrl = BuildPublicUrl(pdfRelativePath);
            }
        }
        catch { /* PDF generation failed — continue without attachment */ }

        var productName = plan.Product?.ProductName ?? "N/A";
        var totalSettled = (entry.ActualPaidAmount ?? 0) + (entry.MiscAdjustedAmount ?? 0);

        var msg = $"Assalam o Alaikum {plan.Customer!.FullName},\n"
            + $"Payment received for installment #{entry.InstallmentNo}:\n"
            + $"Product: {productName}\n"
            + $"Amount Paid: Rs {paidAmount:N0}\n"
            + $"Installment Status: {entry.Status.ToUpper()}\n"
            + $"Paid: {plan.PaidInstallments}/{plan.Tenure} installments\n"
            + $"JazakAllah for your payment!";

        // SMS channel
        _db.SmsMessages.Add(new SmsMessage
        {
            TenantId = plan.TenantId,
            To = customerPhone,
            Message = msg,
            Channel = "sms",
            Reference = $"PAYMENT-PLAN-{plan.Id}-INS-{entry.InstallmentNo}",
            Status = "pending",
            MediaUrl = pdfUrl
        });

        // WhatsApp channel
        _db.SmsMessages.Add(new SmsMessage
        {
            TenantId = plan.TenantId,
            To = customerPhone,
            Message = msg,
            Channel = "whatsapp",
            Reference = $"PAYMENT-PLAN-{plan.Id}-INS-{entry.InstallmentNo}",
            Status = "pending",
            MediaUrl = pdfUrl
        });

        await _db.SaveChangesAsync();
    }

    /// <summary>Build public absolute URL from a relative path like /uploads/invoices/file.pdf</summary>
    private string BuildPublicUrl(string relativePath)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null) return relativePath;
        return $"{request.Scheme}://{request.Host}{relativePath}";
    }

    // ── Pay Installment ────────────────────────────────────

    public async Task<object> PayInstallmentAsync(int planId, int installmentNo, PayInstallmentDto paymentDto)
    {
        var plan = await _db.InstallmentPlans
            .Include(p => p.Customer)
            .Include(p => p.Product)
            .Include(p => p.Schedule)
            .FirstOrDefaultAsync(p => p.Id == planId)
            ?? throw new KeyNotFoundException("Plan not found");

        var entry = plan.Schedule.FirstOrDefault(s => s.InstallmentNo == installmentNo)
            ?? throw new KeyNotFoundException("Installment entry not found");

        if (entry.Status == "paid")
            throw new InvalidOperationException("Already paid");

        var paidAmount = paymentDto.Amount;
        var emiAmount = entry.EmiAmount;

        var previouslyPaid = (entry.ActualPaidAmount ?? 0m) + (entry.MiscAdjustedAmount ?? 0m);
        var totalPaidForEntry = previouslyPaid + paidAmount;

        decimal overpayment = 0;

        if (totalPaidForEntry >= emiAmount)
        {
            entry.Status = "paid";
            entry.PaidDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            entry.ActualPaidAmount = (entry.ActualPaidAmount ?? 0m) + paidAmount;

            overpayment = totalPaidForEntry - emiAmount;

            // Distribute overpayment directly to future installments
            if (overpayment > 0)
            {
                var futureInstallments = plan.Schedule
                    .Where(s => s.InstallmentNo > installmentNo && s.Status != "paid")
                    .OrderBy(s => s.InstallmentNo)
                    .ToList();

                var remaining = overpayment;
                foreach (var future in futureInstallments)
                {
                    if (remaining <= 0) break;

                    var futurePreviouslyPaid = (future.ActualPaidAmount ?? 0m) + (future.MiscAdjustedAmount ?? 0m);
                    var futureRemaining = future.EmiAmount - futurePreviouslyPaid;

                    if (remaining >= futureRemaining)
                    {
                        future.Status = "paid";
                        future.PaidDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                        future.ActualPaidAmount = (future.ActualPaidAmount ?? 0m) + futureRemaining;
                        remaining -= futureRemaining;
                    }
                    else
                    {
                        future.Status = "partial";
                        future.PaidDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                        future.ActualPaidAmount = (future.ActualPaidAmount ?? 0m) + remaining;
                        remaining = 0;
                    }
                }

                // Only store in misc register if all installments are exhausted and there's still leftover
                if (remaining > 0)
                {
                    _db.MiscellaneousRegisters.Add(new MiscellaneousRegister
                    {
                        CustomerId = plan.Customer!.Id,
                        TransactionType = "Credit",
                        Amount = remaining,
                        Description = $"Excess after all installments paid for Plan #{planId} (Original overpayment: {overpayment:C})",
                        ReferenceId = planId.ToString(),
                        ReferenceType = "InstallmentPayment",
                        CreatedBy = "System"
                    });
                }
            }
        }
        else
        {
            entry.PaidDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            entry.Status = "partial";
            entry.ActualPaidAmount = (entry.ActualPaidAmount ?? 0m) + paidAmount;
        }

        // Apply misc balance to future installments if requested
        if (paymentDto.UseMiscBalance && plan.Customer != null)
        {
            await ApplyMiscBalanceToInstallments(planId, plan.Customer.Id);
        }

        UpdatePlanStats(plan);

        await _db.SaveChangesAsync();

        // Queue SMS + WhatsApp notifications with PDF receipt
        await QueuePaymentSms(plan, entry, paidAmount);

        var totalSettled = (entry.ActualPaidAmount ?? 0) + (entry.MiscAdjustedAmount ?? 0);
        return new
        {
            message = entry.Status == "partial"
                ? $"Partial payment recorded. Remaining: {(emiAmount - totalSettled):C}"
                : "Payment processed successfully",
            overpayment = overpayment > 0 ? overpayment : 0,
            status = entry.Status,
            actualPaidAmount = entry.ActualPaidAmount,
            miscAdjustedAmount = entry.MiscAdjustedAmount ?? 0,
            remainingForEntry = entry.Status == "partial" ? emiAmount - totalSettled : 0
        };
    }

    // ── Cancel ─────────────────────────────────────────────

    public async Task<bool> CancelAsync(int id)
    {
        var plan = await _db.InstallmentPlans.FindAsync(id);
        if (plan == null) return false;

        plan.Status = "cancelled";
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Party Search ───────────────────────────────────────

    public async Task<List<PartySearchDto>> SearchPartiesAsync(string? query)
    {
        var q = _db.Parties.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.ToLower();
            q = q.Where(p => p.FullName.ToLower().Contains(term)
                || (p.Phone != null && p.Phone.Contains(term))
                || (p.Cnic != null && p.Cnic.Contains(term))
                || (p.Email != null && p.Email.ToLower().Contains(term)));
        }

        var parties = await q.OrderByDescending(p => p.CreatedAt).Take(50).ToListAsync();

        return parties.Select(p => new PartySearchDto
        {
            Id = p.Id,
            Name = p.FullName,
            SO = p.SO,
            Phone = p.Phone,
            Cnic = p.Cnic,
            Address = p.Address,
            Email = p.Email,
            City = p.City,
            Picture = p.Picture,
            Role = p.Role
        }).ToList();
    }

    // ── Guarantors ─────────────────────────────────────────

    public async Task<GuarantorDto> AddGuarantorAsync(int planId, string name, string? so, string? phone,
        string? cnic, string? address, string? relationship, IFormFile? picture, int? existingPartyId = null)
    {
        var plan = await _db.InstallmentPlans.FindAsync(planId)
            ?? throw new KeyNotFoundException("Plan not found");

        Party party;

        if (existingPartyId.HasValue && existingPartyId.Value > 0)
        {
            // Use existing party
            party = await _db.Parties.FindAsync(existingPartyId.Value)
                ?? throw new KeyNotFoundException("Party not found");
        }
        else
        {
            // Create a new Party with role "Guarantor"
            string? picturePath = null;
            if (picture != null)
                picturePath = await _fileService.SaveFileAsync(picture, "guarantors");

            party = new Party
            {
                FullName = name,
                SO = so,
                Phone = phone,
                Cnic = cnic,
                Address = address,
                Picture = picturePath,
                Role = "Guarantor"
            };
            _db.Parties.Add(party);
            await _db.SaveChangesAsync();
        }

        // Link to plan via join table
        var planGuarantor = new PlanGuarantor
        {
            PlanId = planId,
            PartyId = party.Id,
            Relationship = relationship
        };
        _db.PlanGuarantors.Add(planGuarantor);
        await _db.SaveChangesAsync();

        return MapGuarantorDto(planGuarantor, party);
    }

    public async Task<GuarantorDto?> UpdateGuarantorAsync(int guarantorId, string name, string? so, string? phone,
        string? cnic, string? address, string? relationship, IFormFile? picture)
    {
        // guarantorId here is the PlanGuarantor.Id
        var pg = await _db.PlanGuarantors.Include(x => x.Party).FirstOrDefaultAsync(x => x.Id == guarantorId);
        if (pg?.Party == null) return null;

        pg.Party.FullName = name;
        pg.Party.SO = so;
        pg.Party.Phone = phone;
        pg.Party.Cnic = cnic;
        pg.Party.Address = address;
        pg.Relationship = relationship;

        if (picture != null)
            pg.Party.Picture = await _fileService.SaveFileAsync(picture, "guarantors");

        await _db.SaveChangesAsync();
        return MapGuarantorDto(pg, pg.Party);
    }

    public async Task<bool> DeleteGuarantorAsync(int guarantorId)
    {
        var pg = await _db.PlanGuarantors.FindAsync(guarantorId);
        if (pg == null) return false;

        _db.PlanGuarantors.Remove(pg);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Preview (pure calculation, no DB write) ────────────

    public InstallmentPreviewDto PreviewPlan(PreviewInstallmentDto dto)
    {
        var baseAmount = dto.FinanceAmount.HasValue && dto.FinanceAmount.Value > 0
            ? dto.FinanceAmount.Value
            : dto.ProductPrice;
        var financedAmount = baseAmount - dto.DownPayment;
        var emi = CalculateEMI(financedAmount, dto.InterestRate, dto.Tenure);
        var totalPayable = dto.DownPayment + emi * dto.Tenure;
        var totalInterest = totalPayable - baseAmount;

        var schedule = GenerateScheduleDto(financedAmount, dto.InterestRate, dto.Tenure, dto.StartDate);

        return new InstallmentPreviewDto
        {
            ProductPrice = dto.ProductPrice,
            FinanceAmount = baseAmount,
            FinancedAmount = financedAmount,
            DownPayment = dto.DownPayment,
            InterestRate = dto.InterestRate,
            Tenure = dto.Tenure,
            EmiAmount = Math.Round(emi, 2),
            TotalPayable = Math.Round(totalPayable, 2),
            TotalInterest = Math.Round(totalInterest, 2),
            Schedule = schedule
        };
    }

    // ── Private Helpers ────────────────────────────────────

    private static decimal CalculateEMI(decimal principal, decimal annualRate, int months)
    {
        if (annualRate == 0) return principal / months;
        var r = (double)(annualRate / 12 / 100);
        var p = (double)principal;
        var factor = Math.Pow(1 + r, months);
        return (decimal)((p * r * factor) / (factor - 1));
    }

    private static List<RepaymentEntry> GenerateSchedule(decimal financedAmount, decimal annualRate, int tenure, string startDate)
    {
        var schedule = new List<RepaymentEntry>();
        var emi = CalculateEMI(financedAmount, annualRate, tenure);
        var r = annualRate == 0 ? 0 : (double)(annualRate / 12 / 100);
        var balance = (double)financedAmount;
        var start = DateTime.Parse(startDate);
        var today = DateTime.UtcNow;

        for (int i = 1; i <= tenure; i++)
        {
            var dueDate = start.AddMonths(i);
            var interest = annualRate == 0 ? 0 : balance * r;
            var principal = (double)emi - interest;
            balance = Math.Max(0, balance - principal);

            string status = "upcoming";
            if (dueDate < today) status = "overdue";
            else if (dueDate.Month == today.Month && dueDate.Year == today.Year) status = "due";

            schedule.Add(new RepaymentEntry
            {
                InstallmentNo = i,
                DueDate = dueDate.ToString("yyyy-MM-dd"),
                EmiAmount = Math.Round(emi, 2),
                Principal = Math.Round((decimal)principal, 2),
                Interest = Math.Round((decimal)interest, 2),
                Balance = Math.Round((decimal)balance, 2),
                Status = status
            });
        }
        return schedule;
    }

    private static List<RepaymentEntryDto> GenerateScheduleDto(decimal financedAmount, decimal annualRate, int tenure, string startDate)
    {
        var schedule = new List<RepaymentEntryDto>();
        var emi = CalculateEMI(financedAmount, annualRate, tenure);
        var r = annualRate == 0 ? 0 : (double)(annualRate / 12 / 100);
        var balance = (double)financedAmount;
        var start = DateTime.Parse(startDate);
        var today = DateTime.UtcNow;

        for (int i = 1; i <= tenure; i++)
        {
            var dueDate = start.AddMonths(i);
            var interest = annualRate == 0 ? 0 : balance * r;
            var principal = (double)emi - interest;
            balance = Math.Max(0, balance - principal);

            string status = "upcoming";
            if (dueDate < today) status = "overdue";
            else if (dueDate.Month == today.Month && dueDate.Year == today.Year) status = "due";

            schedule.Add(new RepaymentEntryDto
            {
                InstallmentNo = i,
                DueDate = dueDate.ToString("yyyy-MM-dd"),
                EmiAmount = Math.Round(emi, 2),
                Principal = Math.Round((decimal)principal, 2),
                Interest = Math.Round((decimal)interest, 2),
                Balance = Math.Round((decimal)balance, 2),
                Status = status
            });
        }
        return schedule;
    }

    private async Task ApplyMiscBalanceToInstallments(int planId, int customerId)
    {
        var transactions = await _db.MiscellaneousRegisters
            .Where(m => m.CustomerId == customerId)
            .ToListAsync();

        var credits = transactions.Where(t => t.TransactionType == "Credit").Sum(t => t.Amount);
        var debits = transactions.Where(t => t.TransactionType == "Debit").Sum(t => t.Amount);
        var availableBalance = credits - debits;

        if (availableBalance <= 0) return;

        var plan = await _db.InstallmentPlans
            .Include(p => p.Schedule)
            .FirstOrDefaultAsync(p => p.Id == planId);

        if (plan == null) return;

        var unpaidInstallments = plan.Schedule
            .Where(s => s.Status != "paid")
            .OrderBy(s => s.InstallmentNo)
            .ToList();

        foreach (var installment in unpaidInstallments)
        {
            if (availableBalance <= 0) break;

            var emiAmount = installment.EmiAmount;
            var previouslyPaid = (installment.ActualPaidAmount ?? 0m) + (installment.MiscAdjustedAmount ?? 0m);
            var remainingForEntry = emiAmount - previouslyPaid;
            var amountToApply = Math.Min(availableBalance, remainingForEntry);

            if (amountToApply >= remainingForEntry)
            {
                installment.Status = "paid";
                installment.PaidDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                installment.MiscAdjustedAmount = (installment.MiscAdjustedAmount ?? 0m) + remainingForEntry;

                _db.MiscellaneousRegisters.Add(new MiscellaneousRegister
                {
                    CustomerId = customerId,
                    TransactionType = "Debit",
                    Amount = remainingForEntry,
                    Description = $"Adjusted {remainingForEntry:C} from misc balance for installment #{installment.InstallmentNo}",
                    ReferenceId = planId.ToString(),
                    ReferenceType = "InstallmentPayment",
                    CreatedBy = "System"
                });

                availableBalance -= remainingForEntry;
            }
            else if (amountToApply > 0)
            {
                installment.Status = "partial";
                installment.MiscAdjustedAmount = (installment.MiscAdjustedAmount ?? 0m) + amountToApply;

                _db.MiscellaneousRegisters.Add(new MiscellaneousRegister
                {
                    CustomerId = customerId,
                    TransactionType = "Debit",
                    Amount = amountToApply,
                    Description = $"Partially adjusted {amountToApply:C} from misc balance for installment #{installment.InstallmentNo}",
                    ReferenceId = planId.ToString(),
                    ReferenceType = "PartialInstallmentPayment",
                    CreatedBy = "System"
                });

                availableBalance = 0;
            }
        }

        UpdatePlanStats(plan);
    }

    private static void UpdatePlanStats(InstallmentPlan plan)
    {
        plan.PaidInstallments = plan.Schedule.Count(s => s.Status == "paid");
        plan.RemainingInstallments = plan.Tenure - plan.PaidInstallments;

        var nextDue = plan.Schedule
            .Where(s => s.Status != "paid")
            .OrderBy(s => s.InstallmentNo)
            .FirstOrDefault();
        plan.NextDueDate = nextDue?.DueDate ?? "";

        if (plan.PaidInstallments >= plan.Tenure)
            plan.Status = "completed";
    }

    private static InstallmentPlanDto MapToDto(InstallmentPlan p) => new()
    {
        Id = p.Id.ToString(),
        CustomerId = p.CustomerId.ToString(),
        CustomerName = p.Customer?.FullName ?? "",
        CustomerSo = p.Customer?.SO,
        CustomerCnic = p.Customer?.Cnic,
        CustomerPhone = p.Customer?.Phone ?? "",
        CustomerAddress = p.Customer?.Address ?? "",
        CustomerImage = p.Customer?.Picture,
        ProductName = p.Product?.ProductName ?? "",
        ProductImage = p.Product?.Images.FirstOrDefault()?.ImagePath ?? "/assets/img/products/stock-img-01.png",
        ProductPrice = p.ProductPrice,
        FinanceAmount = p.FinanceAmount,
        DownPayment = p.DownPayment,
        FinancedAmount = p.FinancedAmount,
        InterestRate = p.InterestRate,
        Tenure = p.Tenure,
        EmiAmount = p.EmiAmount,
        TotalPayable = p.TotalPayable,
        TotalInterest = p.TotalInterest,
        StartDate = p.StartDate,
        Status = p.Status,
        PaidInstallments = p.PaidInstallments,
        RemainingInstallments = p.RemainingInstallments,
        NextDueDate = p.NextDueDate ?? "",
        CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd"),
        Schedule = p.Schedule.OrderBy(s => s.InstallmentNo).Select(s => new RepaymentEntryDto
        {
            InstallmentNo = s.InstallmentNo,
            DueDate = s.DueDate,
            EmiAmount = s.EmiAmount,
            Principal = s.Principal,
            Interest = s.Interest,
            Balance = s.Balance,
            Status = s.Status,
            PaidDate = s.PaidDate,
            ActualPaidAmount = s.ActualPaidAmount,
            MiscAdjustedAmount = s.MiscAdjustedAmount
        }).ToList(),
        Guarantors = (p.PlanGuarantors ?? new List<PlanGuarantor>())
            .Where(pg => pg.Party != null)
            .Select(pg => MapGuarantorDto(pg, pg.Party!))
            .ToList()
    };

    private static GuarantorDto MapGuarantorDto(PlanGuarantor pg, Party party) => new()
    {
        Id = pg.Id,
        Name = party.FullName,
        SO = party.SO,
        Phone = party.Phone,
        Cnic = party.Cnic,
        Address = party.Address,
        Relationship = pg.Relationship,
        Picture = party.Picture
    };
}
