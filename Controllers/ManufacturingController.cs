using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ManufacturingController : ControllerBase
{
    private readonly AppDbContext _context;

    public ManufacturingController(AppDbContext context)
    {
        _context = context;
    }

    // ═══════════════════════════════════════════════════════════
    // BILL OF MATERIALS (BOM)
    // ═══════════════════════════════════════════════════════════

    [HttpGet("bom")]
    public async Task<ActionResult> GetAllBoms()
    {
        try
        {
            var boms = await _context.BillOfMaterials
                .Include(b => b.Items)
                .Include(b => b.FinishedProduct).ThenInclude(p => p!.Images)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            var productIds = boms.SelectMany(b => b.Items.Select(i => i.RawMaterialId)).Distinct().ToList();
            var products = await _context.Products.Include(p => p.Images)
                .Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            var supplierIds = boms.SelectMany(b => b.Items.Where(i => i.SupplierId.HasValue).Select(i => i.SupplierId!.Value)).Distinct().ToList();
            var bomSuppliers = await _context.Parties.Where(p => supplierIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            var result = boms.Select(b => new BomDto
            {
                Id = b.Id,
                Name = b.Name,
                FinishedProductId = b.FinishedProductId,
                FinishedProductName = b.FinishedProductName ?? b.FinishedProduct?.ProductName ?? "",
                FinishedProductCategory = b.FinishedProductCategory,
                FinishedProductSubCategory = b.FinishedProductSubCategory,
                SalePrice = b.SalePrice,
                FinishedProductSku = b.FinishedProduct?.SKU,
                FinishedProductImage = b.FinishedProduct?.Images.FirstOrDefault()?.ImagePath,
                OutputQuantity = b.OutputQuantity,
                LaborCost = b.LaborCost,
                OverheadCost = b.OverheadCost,
                Notes = b.Notes,
                Status = b.Status,
                Items = b.Items.Select(i =>
                {
                    products.TryGetValue(i.RawMaterialId, out var prod);
                    bomSuppliers.TryGetValue(i.SupplierId ?? 0, out var supplier);
                    return new BomItemDto
                    {
                        Id = i.Id,
                        RawMaterialId = i.RawMaterialId,
                        RawMaterialName = prod?.ProductName ?? "",
                        RawMaterialSku = prod?.SKU,
                        RawMaterialImage = prod?.Images.FirstOrDefault()?.ImagePath,
                        Quantity = i.Quantity,
                        UnitCost = i.UnitCost,
                        SupplierId = i.SupplierId,
                        SupplierName = supplier?.FullName
                    };
                }).ToList(),
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("bom/{id}")]
    public async Task<ActionResult> GetBomById(int id)
    {
        try
        {
            var b = await _context.BillOfMaterials
                .Include(b => b.Items)
                .Include(b => b.FinishedProduct).ThenInclude(p => p!.Images)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (b == null) return NotFound(new { error = "BOM not found" });

            var productIds = b.Items.Select(i => i.RawMaterialId).Distinct().ToList();
            var products = await _context.Products.Include(p => p.Images)
                .Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            var supplierIds = b.Items.Where(i => i.SupplierId.HasValue).Select(i => i.SupplierId!.Value).Distinct().ToList();
            var bomSuppliers = await _context.Parties.Where(p => supplierIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            return Ok(new BomDto
            {
                Id = b.Id,
                Name = b.Name,
                FinishedProductId = b.FinishedProductId,
                FinishedProductName = b.FinishedProductName ?? b.FinishedProduct?.ProductName ?? "",
                FinishedProductCategory = b.FinishedProductCategory,
                FinishedProductSubCategory = b.FinishedProductSubCategory,
                SalePrice = b.SalePrice,
                FinishedProductSku = b.FinishedProduct?.SKU,
                FinishedProductImage = b.FinishedProduct?.Images.FirstOrDefault()?.ImagePath,
                OutputQuantity = b.OutputQuantity,
                LaborCost = b.LaborCost,
                OverheadCost = b.OverheadCost,
                Notes = b.Notes,
                Status = b.Status,
                Items = b.Items.Select(i =>
                {
                    products.TryGetValue(i.RawMaterialId, out var prod);
                    bomSuppliers.TryGetValue(i.SupplierId ?? 0, out var supplier);
                    return new BomItemDto
                    {
                        Id = i.Id,
                        RawMaterialId = i.RawMaterialId,
                        RawMaterialName = prod?.ProductName ?? "",
                        RawMaterialSku = prod?.SKU,
                        RawMaterialImage = prod?.Images.FirstOrDefault()?.ImagePath,
                        Quantity = i.Quantity,
                        UnitCost = i.UnitCost,
                        SupplierId = i.SupplierId,
                        SupplierName = supplier?.FullName
                    };
                }).ToList(),
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("bom")]
    public async Task<ActionResult> CreateBom([FromBody] CreateBomDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var bom = new BillOfMaterials
            {
                Name = dto.Name,
                FinishedProductName = dto.FinishedProductName,
                FinishedProductCategory = dto.FinishedProductCategory,
                FinishedProductSubCategory = dto.FinishedProductSubCategory,
                SalePrice = dto.SalePrice,
                OutputQuantity = dto.OutputQuantity,
                LaborCost = dto.LaborCost,
                OverheadCost = dto.OverheadCost,
                Notes = dto.Notes,
                Status = dto.Status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = dto.Items.Select(i => new BomItem
                {
                    RawMaterialId = i.RawMaterialId,
                    Quantity = i.Quantity,
                    UnitCost = i.UnitCost,
                    SupplierId = i.SupplierId
                }).ToList()
            };

            _context.BillOfMaterials.Add(bom);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(nameof(GetBomById), new { id = bom.Id }, new { id = bom.Id, message = "BOM created successfully" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("bom/{id}")]
    public async Task<ActionResult> UpdateBom(int id, [FromBody] UpdateBomDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var bom = await _context.BillOfMaterials.Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == id);
            if (bom == null) return NotFound(new { error = "BOM not found" });

            bom.Name = dto.Name;
            bom.FinishedProductName = dto.FinishedProductName;
            bom.FinishedProductCategory = dto.FinishedProductCategory;
            bom.FinishedProductSubCategory = dto.FinishedProductSubCategory;
            bom.SalePrice = dto.SalePrice;
            bom.OutputQuantity = dto.OutputQuantity;
            bom.LaborCost = dto.LaborCost;
            bom.OverheadCost = dto.OverheadCost;
            bom.Notes = dto.Notes;
            bom.Status = dto.Status;
            bom.UpdatedAt = DateTime.UtcNow;

            _context.BomItems.RemoveRange(bom.Items);
            bom.Items = dto.Items.Select(i => new BomItem
            {
                BomId = id,
                RawMaterialId = i.RawMaterialId,
                Quantity = i.Quantity,
                UnitCost = i.UnitCost,
                SupplierId = i.SupplierId
            }).ToList();

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "BOM updated successfully" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("bom/{id}")]
    public async Task<ActionResult> DeleteBom(int id)
    {
        try
        {
            var bom = await _context.BillOfMaterials.Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == id);
            if (bom == null) return NotFound(new { error = "BOM not found" });

            // Prevent deletion if active manufacturing orders reference this BOM
            var hasOrders = await _context.ManufacturingOrders
                .AnyAsync(o => o.BomId == id && o.Status != "Cancelled");
            if (hasOrders)
                return BadRequest(new { error = "Cannot delete this BOM because it is referenced by active manufacturing orders. Cancel or delete those orders first." });

            _context.BillOfMaterials.Remove(bom);
            await _context.SaveChangesAsync();

            return Ok(new { message = "BOM deleted successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // MANUFACTURING ORDERS
    // ═══════════════════════════════════════════════════════════

    [HttpGet("orders")]
    public async Task<ActionResult> GetAllOrders()
    {
        try
        {
            var orders = await _context.ManufacturingOrders
                .Include(o => o.Items)
                .Include(o => o.Bom)
                .Include(o => o.FinishedProduct).ThenInclude(p => p!.Images)
                .Include(o => o.TargetStore)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var rawMaterialIds = orders.SelectMany(o => o.Items.Select(i => i.RawMaterialId)).Distinct().ToList();
            var supplierIds = orders.SelectMany(o => o.Items.Where(i => i.SupplierId.HasValue).Select(i => i.SupplierId!.Value)).Distinct().ToList();
            var products = await _context.Products.Include(p => p.Images).Where(p => rawMaterialIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            var suppliers = await _context.Parties.Where(p => supplierIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            var result = orders.Select(o => MapOrderToDto(o, products, suppliers)).ToList();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("orders/{id}")]
    public async Task<ActionResult> GetOrderById(int id)
    {
        try
        {
            var o = await _context.ManufacturingOrders
                .Include(o => o.Items)
                .Include(o => o.Bom)
                .Include(o => o.FinishedProduct).ThenInclude(p => p!.Images)
                .Include(o => o.TargetStore)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (o == null) return NotFound(new { error = "Manufacturing order not found" });

            var rawMaterialIds = o.Items.Select(i => i.RawMaterialId).Distinct().ToList();
            var supplierIds = o.Items.Where(i => i.SupplierId.HasValue).Select(i => i.SupplierId!.Value).Distinct().ToList();
            var products = await _context.Products.Include(p => p.Images).Where(p => rawMaterialIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            var suppliers = await _context.Parties.Where(p => supplierIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            return Ok(MapOrderToDto(o, products, suppliers));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("orders")]
    public async Task<ActionResult> CreateOrder([FromBody] CreateManufacturingOrderDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var totalMaterialCost = dto.Items.Sum(i => i.TotalCost);
            var totalCost = totalMaterialCost + dto.LaborCost + dto.OverheadCost;

            var resolvedStoreId = await ResolveStoreIdFromParty(dto.TargetStoreId);

            var order = new ManufacturingOrder
            {
                Reference = dto.Reference,
                BomId = dto.BomId,
                FinishedProductId = null,
                Quantity = dto.Quantity,
                TargetStoreId = resolvedStoreId,
                Status = dto.Status,
                LaborCost = dto.LaborCost,
                OverheadCost = dto.OverheadCost,
                TotalMaterialCost = totalMaterialCost,
                TotalCost = totalCost,
                Notes = dto.Notes,
                StartDate = dto.Status == "InProgress" ? DateTime.UtcNow : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = dto.Items.Select(i => new ManufacturingOrderItem
                {
                    RawMaterialId = i.RawMaterialId,
                    RequiredQuantity = i.RequiredQuantity,
                    ConsumedQuantity = i.ConsumedQuantity,
                    UnitCost = i.UnitCost,
                    TotalCost = i.TotalCost,
                    SupplierId = i.SupplierId
                }).ToList()
            };

            _context.ManufacturingOrders.Add(order);
            await _context.SaveChangesAsync();

            // If status is InProgress or Completed, deduct raw materials from inventory
            if (dto.Status == "InProgress" || dto.Status == "Completed")
            {
                await DeductRawMaterials(order.Items.ToList());
            }

            // If status is Completed, add finished product to inventory and record in target store
            if (dto.Status == "Completed")
            {
                order.CompletionDate = DateTime.UtcNow;
                await AddFinishedProduct(order);

                // Record supplier ledger entries for materials purchased
                await RecordSupplierLedgerForOrder(order);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, new { id = order.Id, message = "Manufacturing order created successfully" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("orders/{id}")]
    public async Task<ActionResult> UpdateOrder(int id, [FromBody] UpdateManufacturingOrderDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = await _context.ManufacturingOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound(new { error = "Manufacturing order not found" });

            var oldStatus = order.Status;

            // Prevent editing orders that are already Completed or Cancelled
            if (oldStatus == "Completed")
                return BadRequest(new { error = "Cannot edit a completed order. Delete and recreate if changes are needed." });
            if (oldStatus == "Cancelled")
                return BadRequest(new { error = "Cannot edit a cancelled order." });

            var oldItems = order.Items.ToList();

            // If old status was InProgress/Completed, revert inventory deductions
            if (oldStatus == "InProgress" || oldStatus == "Completed")
            {
                await RevertRawMaterials(oldItems);
            }
            if (oldStatus == "Completed")
            {
                await RevertFinishedProduct(order);
            }

            var totalMaterialCost = dto.Items.Sum(i => i.TotalCost);
            var totalCost = totalMaterialCost + dto.LaborCost + dto.OverheadCost;

            var resolvedStoreId = await ResolveStoreIdFromParty(dto.TargetStoreId);

            order.Reference = dto.Reference;
            order.BomId = dto.BomId;
            order.Quantity = dto.Quantity;
            order.TargetStoreId = resolvedStoreId;
            order.Status = dto.Status;
            order.LaborCost = dto.LaborCost;
            order.OverheadCost = dto.OverheadCost;
            order.TotalMaterialCost = totalMaterialCost;
            order.TotalCost = totalCost;
            order.Notes = dto.Notes;
            order.UpdatedAt = DateTime.UtcNow;

            if (dto.Status == "InProgress" && oldStatus == "Draft")
                order.StartDate = DateTime.UtcNow;
            if (dto.Status == "Completed")
                order.CompletionDate = DateTime.UtcNow;

            _context.ManufacturingOrderItems.RemoveRange(oldItems);
            order.Items = dto.Items.Select(i => new ManufacturingOrderItem
            {
                ManufacturingOrderId = id,
                RawMaterialId = i.RawMaterialId,
                RequiredQuantity = i.RequiredQuantity,
                ConsumedQuantity = i.ConsumedQuantity,
                UnitCost = i.UnitCost,
                TotalCost = i.TotalCost,
                SupplierId = i.SupplierId
            }).ToList();

            if (dto.Status == "InProgress" || dto.Status == "Completed")
            {
                await _context.SaveChangesAsync(); // save items first
                await DeductRawMaterials(order.Items.ToList());
            }
            if (dto.Status == "Completed")
            {
                await AddFinishedProduct(order);
                await RecordSupplierLedgerForOrder(order);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Manufacturing order updated successfully" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("orders/{id}")]
    public async Task<ActionResult> DeleteOrder(int id)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = await _context.ManufacturingOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound(new { error = "Manufacturing order not found" });

            if (order.Status == "InProgress" || order.Status == "Completed")
            {
                await RevertRawMaterials(order.Items.ToList());
            }
            if (order.Status == "Completed")
            {
                await RevertFinishedProduct(order);
            }

            _context.ManufacturingOrders.Remove(order);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Manufacturing order deleted successfully" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { error = ex.Message });
        }
    }

    // Complete a draft/in-progress order
    [HttpPost("orders/{id}/complete")]
    public async Task<ActionResult> CompleteOrder(int id)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = await _context.ManufacturingOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound(new { error = "Manufacturing order not found" });

            if (order.Status == "Completed") return BadRequest(new { error = "Order is already completed" });
            if (order.Status == "Cancelled") return BadRequest(new { error = "Cannot complete a cancelled order" });

            var oldStatus = order.Status;

            // If was Draft, need to deduct raw materials
            if (oldStatus == "Draft")
            {
                await DeductRawMaterials(order.Items.ToList());
            }

            order.Status = "Completed";
            order.CompletionDate = DateTime.UtcNow;
            if (order.StartDate == null) order.StartDate = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            await AddFinishedProduct(order);
            await RecordSupplierLedgerForOrder(order);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Manufacturing order completed. Finished product added to inventory." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // SUPPLIER LEDGER
    // ═══════════════════════════════════════════════════════════

    [HttpGet("supplier-ledger")]
    public async Task<ActionResult> GetSupplierLedger([FromQuery] int? supplierId)
    {
        try
        {
            var query = _context.SupplierLedgerEntries.AsQueryable();
            if (supplierId.HasValue) query = query.Where(e => e.SupplierId == supplierId.Value);

            var entries = await query.OrderByDescending(e => e.Date).ToListAsync();
            var supplierIds = entries.Select(e => e.SupplierId).Distinct().ToList();
            var suppliers = await _context.Parties.Where(p => supplierIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            var result = entries.Select(e =>
            {
                suppliers.TryGetValue(e.SupplierId, out var supplier);
                return new SupplierLedgerEntryDto
                {
                    Id = e.Id,
                    SupplierId = e.SupplierId,
                    SupplierName = supplier?.FullName ?? "",
                    TransactionType = e.TransactionType,
                    ReferenceType = e.ReferenceType,
                    ReferenceId = e.ReferenceId,
                    Amount = e.Amount,
                    RunningBalance = e.RunningBalance,
                    Description = e.Description,
                    Date = e.Date,
                    CreatedAt = e.CreatedAt
                };
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("supplier-ledger")]
    public async Task<ActionResult> CreateLedgerEntry([FromBody] CreateSupplierLedgerEntryDto dto)
    {
        try
        {
            var lastEntry = await _context.SupplierLedgerEntries
                .Where(e => e.SupplierId == dto.SupplierId)
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.Id)
                .FirstOrDefaultAsync();

            var runningBalance = lastEntry?.RunningBalance ?? 0;

            if (dto.TransactionType == "Purchase" || dto.TransactionType == "Debit")
                runningBalance += dto.Amount;
            else
                runningBalance -= dto.Amount;

            var entry = new SupplierLedgerEntry
            {
                SupplierId = dto.SupplierId,
                TransactionType = dto.TransactionType,
                ReferenceType = dto.ReferenceType,
                ReferenceId = dto.ReferenceId,
                Amount = dto.Amount,
                RunningBalance = runningBalance,
                Description = dto.Description,
                Date = dto.Date ?? DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.SupplierLedgerEntries.Add(entry);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ledger entry created", id = entry.Id, runningBalance });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // SUPPLIER PAYMENTS
    // ═══════════════════════════════════════════════════════════

    [HttpGet("supplier-payments")]
    public async Task<ActionResult> GetSupplierPayments([FromQuery] int? supplierId)
    {
        try
        {
            var query = _context.SupplierPayments.AsQueryable();
            if (supplierId.HasValue) query = query.Where(p => p.SupplierId == supplierId.Value);

            var payments = await query.OrderByDescending(p => p.PaymentDate).ToListAsync();
            var supplierIds = payments.Select(p => p.SupplierId).Distinct().ToList();
            var suppliers = await _context.Parties.Where(p => supplierIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            var result = payments.Select(p =>
            {
                suppliers.TryGetValue(p.SupplierId, out var supplier);
                return new SupplierPaymentDto
                {
                    Id = p.Id,
                    SupplierId = p.SupplierId,
                    SupplierName = supplier?.FullName ?? "",
                    Reference = p.Reference,
                    Amount = p.Amount,
                    PaymentMethod = p.PaymentMethod,
                    Description = p.Description,
                    PaymentDate = p.PaymentDate,
                    CreatedAt = p.CreatedAt
                };
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("supplier-payments")]
    public async Task<ActionResult> CreateSupplierPayment([FromBody] CreateSupplierPaymentDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var payment = new SupplierPayment
            {
                SupplierId = dto.SupplierId,
                Reference = dto.Reference,
                Amount = dto.Amount,
                PaymentMethod = dto.PaymentMethod,
                Description = dto.Description,
                PaymentDate = dto.PaymentDate ?? DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.SupplierPayments.Add(payment);
            await _context.SaveChangesAsync();

            // Record in supplier ledger
            var lastEntry = await _context.SupplierLedgerEntries
                .Where(e => e.SupplierId == dto.SupplierId)
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.Id)
                .FirstOrDefaultAsync();

            var runningBalance = (lastEntry?.RunningBalance ?? 0) - dto.Amount;

            _context.SupplierLedgerEntries.Add(new SupplierLedgerEntry
            {
                SupplierId = dto.SupplierId,
                TransactionType = "Payment",
                ReferenceType = "ManualPayment",
                ReferenceId = payment.Id,
                Amount = dto.Amount,
                RunningBalance = runningBalance,
                Description = dto.Description ?? $"Payment - {dto.PaymentMethod} - Ref: {dto.Reference}",
                Date = dto.PaymentDate ?? DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Payment recorded successfully", id = payment.Id });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("supplier-payments/{id}")]
    public async Task<ActionResult> DeleteSupplierPayment(int id)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var payment = await _context.SupplierPayments.FindAsync(id);
            if (payment == null) return NotFound(new { error = "Payment not found" });

            // Create a reversal ledger entry to undo the payment's effect
            var lastEntry = await _context.SupplierLedgerEntries
                .Where(e => e.SupplierId == payment.SupplierId)
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.Id)
                .FirstOrDefaultAsync();

            var runningBalance = (lastEntry?.RunningBalance ?? 0) + payment.Amount;

            _context.SupplierLedgerEntries.Add(new SupplierLedgerEntry
            {
                SupplierId = payment.SupplierId,
                TransactionType = "Debit",
                ReferenceType = "PaymentReversal",
                ReferenceId = payment.Id,
                Amount = payment.Amount,
                RunningBalance = runningBalance,
                Description = $"Reversal of payment {payment.Reference} ({payment.PaymentMethod})",
                Date = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });

            _context.SupplierPayments.Remove(payment);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Payment deleted and ledger reversed successfully" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // SUPPLIER BALANCES
    // ═══════════════════════════════════════════════════════════

    [HttpGet("supplier-balances")]
    public async Task<ActionResult> GetSupplierBalances()
    {
        try
        {
            var suppliers = await _context.Parties
                .Where(p => p.Role == "Supplier")
                .ToListAsync();

            var result = new List<SupplierBalanceDto>();

            foreach (var supplier in suppliers)
            {
                var totalPurchases = await _context.SupplierLedgerEntries
                    .Where(e => e.SupplierId == supplier.Id && e.TransactionType == "Purchase")
                    .SumAsync(e => e.Amount);

                var totalPayments = await _context.SupplierPayments
                    .Where(p => p.SupplierId == supplier.Id)
                    .SumAsync(p => p.Amount);

                var totalDebits = await _context.SupplierLedgerEntries
                    .Where(e => e.SupplierId == supplier.Id && (e.TransactionType == "Purchase" || e.TransactionType == "Debit"))
                    .SumAsync(e => e.Amount);
                var totalCredits = await _context.SupplierLedgerEntries
                    .Where(e => e.SupplierId == supplier.Id && (e.TransactionType == "Payment" || e.TransactionType == "Credit"))
                    .SumAsync(e => e.Amount);

                result.Add(new SupplierBalanceDto
                {
                    SupplierId = supplier.Id,
                    SupplierName = supplier.FullName,
                    SupplierPhone = supplier.Phone,
                    SupplierEmail = supplier.Email,
                    TotalPurchases = totalPurchases,
                    TotalPayments = totalPayments,
                    Balance = totalDebits - totalCredits
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════

    private async Task DeductRawMaterials(List<ManufacturingOrderItem> items)
    {
        foreach (var item in items)
        {
            var product = await _context.Products.FindAsync(item.RawMaterialId);
            if (product != null)
            {
                var qty = (int)Math.Ceiling(item.ConsumedQuantity > 0 ? item.ConsumedQuantity : item.RequiredQuantity);
                if (product.Quantity < qty)
                    throw new InvalidOperationException(
                        $"Insufficient stock for '{product.ProductName}': available {product.Quantity}, required {qty}.");
                product.Quantity -= qty;
                product.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private async Task RevertRawMaterials(List<ManufacturingOrderItem> items)
    {
        foreach (var item in items)
        {
            var product = await _context.Products.FindAsync(item.RawMaterialId);
            if (product != null)
            {
                product.Quantity += (int)Math.Ceiling(item.ConsumedQuantity > 0 ? item.ConsumedQuantity : item.RequiredQuantity);
                product.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private async Task AddFinishedProduct(ManufacturingOrder order)
    {
        // Load the BOM to get inline product definition
        var bom = await _context.BillOfMaterials.FindAsync(order.BomId);

        Product? product = null;

        // If the BOM already has a linked product, reuse it
        if (order.FinishedProductId.HasValue)
        {
            product = await _context.Products.FindAsync(order.FinishedProductId.Value);
        }

        // Otherwise, create the product from BOM inline fields
        if (product == null && bom != null)
        {
            var productName = bom.FinishedProductName ?? bom.Name;
            var slug = productName.ToLower().Replace(" ", "-");
            var sku = $"MFG-{DateTime.UtcNow:yyyyMMddHHmmss}";

            product = new Product
            {
                TenantId = order.TenantId,
                ProductName = productName,
                Slug = slug,
                SKU = sku,
                Category = bom.FinishedProductCategory,
                SubCategory = bom.FinishedProductSubCategory,
                Price = bom.SalePrice,
                ProductType = "single",
                Quantity = 0,
                IsRawMaterial = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // Link the product back to the BOM and order
            bom.FinishedProductId = product.Id;
            order.FinishedProductId = product.Id;
        }

        if (product != null)
        {
            product.Quantity += order.Quantity;

            string storeValue = "";
            string storeLabel = "";

            if (order.TargetStoreId.HasValue)
            {
                var store = await _context.Stores.FindAsync(order.TargetStoreId.Value);
                if (store != null)
                {
                    storeValue = store.Value;
                    storeLabel = store.Label;
                    product.Store = store.Value;
                }
            }

            // Create StockEntry record for audit trail & per-store tracking
            _context.StockEntries.Add(new StockEntry
            {
                TenantId = order.TenantId,
                ProductId = product.Id,
                Quantity = order.Quantity,
                Store = storeValue,
                Warehouse = storeLabel,
                Person = $"MFG-{order.Reference}",
                Date = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });

            product.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task RevertFinishedProduct(ManufacturingOrder order)
    {
        if (!order.FinishedProductId.HasValue) return;

        var product = await _context.Products.FindAsync(order.FinishedProductId.Value);
        if (product != null)
        {
            product.Quantity -= order.Quantity;
            if (product.Quantity < 0) product.Quantity = 0;
            product.UpdatedAt = DateTime.UtcNow;
        }

        // Remove the StockEntry created during completion
        var stockEntry = await _context.StockEntries
            .FirstOrDefaultAsync(se => se.ProductId == order.FinishedProductId.Value
                && se.Person == $"MFG-{order.Reference}");
        if (stockEntry != null)
            _context.StockEntries.Remove(stockEntry);
    }

    private async Task RecordSupplierLedgerForOrder(ManufacturingOrder order)
    {
        var supplierGroups = order.Items
            .Where(i => i.SupplierId.HasValue)
            .GroupBy(i => i.SupplierId!.Value);

        foreach (var group in supplierGroups)
        {
            var supplierId = group.Key;
            var totalAmount = group.Sum(i => i.TotalCost);

            var lastEntry = await _context.SupplierLedgerEntries
                .Where(e => e.SupplierId == supplierId)
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.Id)
                .FirstOrDefaultAsync();

            var runningBalance = (lastEntry?.RunningBalance ?? 0) + totalAmount;

            _context.SupplierLedgerEntries.Add(new SupplierLedgerEntry
            {
                SupplierId = supplierId,
                TransactionType = "Purchase",
                ReferenceType = "ManufacturingOrder",
                ReferenceId = order.Id,
                Amount = totalAmount,
                RunningBalance = runningBalance,
                Description = $"Manufacturing Order #{order.Reference} - Raw materials",
                Date = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private async Task<int?> ResolveStoreIdFromParty(int? partyId)
    {
        if (!partyId.HasValue || partyId.Value == 0) return null;

        var existingStore = await _context.Stores.FindAsync(partyId.Value);
        if (existingStore != null) return existingStore.Id;

        var party = await _context.Parties
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == partyId.Value && p.Role == "Store");
        if (party == null) return null;

        var storeValue = party.FullName.ToLower().Replace(" ", "-");
        var store = await _context.Stores
            .FirstOrDefaultAsync(s => s.Value == storeValue || s.Label == party.FullName);
        if (store != null) return store.Id;

        store = new Store
        {
            TenantId = party.TenantId,
            Value = storeValue,
            Label = party.FullName,
            CreatedAt = DateTime.UtcNow
        };
        _context.Stores.Add(store);
        await _context.SaveChangesAsync();
        return store.Id;
    }

    private ManufacturingOrderDto MapOrderToDto(
        ManufacturingOrder o,
        Dictionary<int, Product> products,
        Dictionary<int, Party> suppliers)
    {
        return new ManufacturingOrderDto
        {
            Id = o.Id,
            Reference = o.Reference,
            BomId = o.BomId,
            BomName = o.Bom?.Name ?? "",
            FinishedProductId = o.FinishedProductId,
            FinishedProductName = o.FinishedProduct?.ProductName ?? o.Bom?.FinishedProductName ?? "",
            FinishedProductImage = o.FinishedProduct?.Images.FirstOrDefault()?.ImagePath,
            Quantity = o.Quantity,
            TargetStoreId = o.TargetStoreId,
            TargetStoreName = o.TargetStore?.Label,
            Status = o.Status,
            LaborCost = o.LaborCost,
            OverheadCost = o.OverheadCost,
            TotalMaterialCost = o.TotalMaterialCost,
            TotalCost = o.TotalCost,
            StartDate = o.StartDate,
            CompletionDate = o.CompletionDate,
            Notes = o.Notes,
            Items = o.Items.Select(i =>
            {
                products.TryGetValue(i.RawMaterialId, out var prod);
                suppliers.TryGetValue(i.SupplierId ?? 0, out var supplier);
                return new ManufacturingOrderItemDto
                {
                    Id = i.Id,
                    RawMaterialId = i.RawMaterialId,
                    RawMaterialName = prod?.ProductName ?? "",
                    RawMaterialSku = prod?.SKU,
                    RawMaterialImage = prod?.Images.FirstOrDefault()?.ImagePath,
                    RequiredQuantity = i.RequiredQuantity,
                    ConsumedQuantity = i.ConsumedQuantity,
                    UnitCost = i.UnitCost,
                    TotalCost = i.TotalCost,
                    SupplierId = i.SupplierId,
                    SupplierName = supplier?.FullName
                };
            }).ToList(),
            CreatedAt = o.CreatedAt,
            UpdatedAt = o.UpdatedAt
        };
    }
}
