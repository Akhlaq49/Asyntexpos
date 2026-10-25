using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public interface IPurchaseOrderService
{
    Task<List<PurchaseOrderDto>> GetAllAsync(string? status, string? search);
    Task<PurchaseOrderDto?> GetByIdAsync(int id);
    Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderDto dto, string? createdBy);
    Task<PurchaseOrderDto?> UpdateAsync(int id, SavePurchaseOrderDto dto);
    Task<PurchaseOrderDto?> MarkOrderedAsync(int id);
    Task<PurchaseOrderDto?> ReceiveAsync(int id, ReceivePurchaseOrderDto dto, string? receivedBy = null);
    Task<PurchaseOrderDto?> CancelAsync(int id);
    Task<bool> DeleteAsync(int id);
    /// <summary>New version PO-0001_R1, _R2 ... from any version; every version is kept</summary>
    Task<PurchaseOrderDto?> ReviseAsync(int id, SavePurchaseOrderDto dto, string? createdBy);
    /// <summary>Every version of the order, original first</summary>
    Task<List<PurchaseOrderDto>> GetRevisionsAsync(int id);
}

/// <summary>Business-rule violation shown to the user (HTTP 400)</summary>
public class PurchaseOrderException : Exception
{
    public PurchaseOrderException(string message) : base(message) { }
}

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly AppDbContext _db;
    public PurchaseOrderService(AppDbContext db) => _db = db;

    // ─────────────── queries ───────────────

    public async Task<List<PurchaseOrderDto>> GetAllAsync(string? status, string? search)
    {
        var q = _db.PurchaseOrders.Include(p => p.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(p => p.PoNumber.ToLower().Contains(s) || p.SupplierName.ToLower().Contains(s));
        }
        var orders = await q.OrderByDescending(p => p.OrderDate).ThenByDescending(p => p.Id).ToListAsync();
        return await MapAsync(orders);
    }

    public async Task<PurchaseOrderDto?> GetByIdAsync(int id)
    {
        var po = await LoadAsync(id);
        return po == null ? null : (await MapAsync(new List<PurchaseOrder> { po }))[0];
    }

    // ─────────────── create / edit ───────────────

    public async Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderDto dto, string? createdBy)
    {
        var po = new PurchaseOrder
        {
            PoNumber = await NextPoNumberAsync(),
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
        };
        await ApplyAsync(po, dto);
        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(po.Id))!;
    }

    public async Task<PurchaseOrderDto?> UpdateAsync(int id, SavePurchaseOrderDto dto)
    {
        var po = await LoadAsync(id);
        if (po == null) return null;
        if (po.Status != PurchaseOrderStatus.Draft && po.Status != PurchaseOrderStatus.Ordered)
            throw new PurchaseOrderException($"A {Label(po.Status)} purchase order can't be edited.");
        if (po.Items.Any(i => i.ReceivedQuantity > 0))
            throw new PurchaseOrderException("Goods have already been received against this order, so it can't be edited.");
        if (po.SupersededById != null)
            throw new PurchaseOrderException($"{po.PoNumber} is an older version and is kept unchanged. Use Revise to make a new version from it.");
        var receivedVersion = await ReceivedVersionAsync(po);
        if (receivedVersion != null)
            throw new PurchaseOrderException($"Goods were received against {receivedVersion}, so the other versions are kept unchanged.");

        _db.PurchaseOrderItems.RemoveRange(po.Items);
        po.Items = new List<PurchaseOrderItem>();
        await ApplyAsync(po, dto, po.ExpectedDate);
        await _db.SaveChangesAsync();
        return await GetByIdAsync(po.Id);
    }

    public async Task<PurchaseOrderDto?> MarkOrderedAsync(int id)
    {
        var po = await LoadAsync(id);
        if (po == null) return null;
        if (po.Status != PurchaseOrderStatus.Draft)
            throw new PurchaseOrderException($"Only a draft can be marked as ordered (this one is {Label(po.Status)}).");
        po.Status = PurchaseOrderStatus.Ordered;
        po.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await GetByIdAsync(po.Id);
    }

    // ─────────────── receive goods -> inventory + purchase ───────────────

    public async Task<PurchaseOrderDto?> ReceiveAsync(int id, ReceivePurchaseOrderDto dto, string? receivedBy = null)
    {
        var po = await LoadAsync(id);
        if (po == null) return null;
        if (po.Status == PurchaseOrderStatus.Received || po.Status == PurchaseOrderStatus.Cancelled)
            throw new PurchaseOrderException($"Nothing can be received on a {Label(po.Status)} purchase order.");

        var otherReceived = await ReceivedVersionAsync(po, excludeSelf: true);
        if (otherReceived != null)
            throw new PurchaseOrderException($"Goods were already received against {otherReceived}. Only one version of a purchase order can be received.");

        var lines = dto.Lines.Where(l => l.Quantity != 0).ToList();
        if (lines.Count == 0) throw new PurchaseOrderException("Enter the quantity received for at least one product.");
        foreach (var line in lines)
        {
            var item = po.Items.FirstOrDefault(i => i.Id == line.ItemId)
                ?? throw new PurchaseOrderException("One of the received lines doesn't belong to this purchase order.");
            if (line.Quantity < 0) throw new PurchaseOrderException($"{item.ProductName}: quantity can't be negative.");
            var remaining = item.Quantity - item.ReceivedQuantity;
            if (line.Quantity > remaining)
                throw new PurchaseOrderException($"{item.ProductName}: only {remaining} still to receive, but {line.Quantity} entered.");
        }

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var receiptNo = await _db.Purchases.CountAsync(p => p.PurchaseOrderId == po.Id) + 1;
            var date = dto.Date ?? DateTime.UtcNow;
            var purchaseItems = new List<PurchaseItem>();
            decimal linesValue = 0;

            foreach (var line in lines)
            {
                var item = po.Items.First(i => i.Id == line.ItemId);
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId)
                    ?? throw new PurchaseOrderException($"Product \"{item.ProductName}\" no longer exists.");

                // per-unit landed cost of the line (line discount and tax spread over its units)
                var perUnit = item.Quantity > 0 ? item.TotalCost / item.Quantity : item.UnitCost;
                var share = item.Quantity > 0 ? (decimal)line.Quantity / item.Quantity : 0;
                var lineTotal = Math.Round(perUnit * line.Quantity, 2);
                linesValue += lineTotal;

                purchaseItems.Add(new PurchaseItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName.Length > 100 ? item.ProductName[..100] : item.ProductName,
                    Quantity = line.Quantity,
                    PurchasePrice = item.UnitCost,
                    Discount = Math.Round(item.Discount * share, 2),
                    TaxPercentage = item.TaxPercent,
                    TaxAmount = Math.Round(item.TaxAmount * share, 2),
                    UnitCost = Math.Round(perUnit, 2),
                    TotalCost = lineTotal,
                    CreatedAt = DateTime.UtcNow,
                });

                item.ReceivedQuantity += line.Quantity;   // order progress
                product.Quantity += line.Quantity;        // inventory
                product.UpdatedAt = DateTime.UtcNow;
            }

            // order-level tax / shipping / discount, shared in proportion to the value received
            var orderExtras = po.OrderTax + po.Shipping - po.Discount;
            var extrasShare = po.SubTotal > 0 ? Math.Round(orderExtras * linesValue / po.SubTotal, 2) : 0;
            var receiptTotal = Math.Max(0, linesValue + extrasShare);
            var paid = Math.Min(Math.Max(0, dto.Paid), receiptTotal);

            var purchase = new Purchase
            {
                PurchaseOrderId = po.Id,
                SupplierName = po.SupplierName.Length > 100 ? po.SupplierName[..100] : po.SupplierName,
                SupplierRef = po.PoNumber,
                Reference = $"{po.PoNumber}-GR{receiptNo}",   // GR = goods received ("_R1" is a revision)
                Date = date,
                Status = "Received",
                PaymentStatus = paid <= 0 ? "Unpaid" : paid >= receiptTotal ? "Paid" : "Partial",
                OrderTax = 0,
                Discount = 0,
                Shipping = extrasShare,
                Total = receiptTotal,
                Paid = paid,
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? $"Received against {po.PoNumber}" : dto.Notes,
                Items = purchaseItems,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.Purchases.Add(purchase);
            await _db.SaveChangesAsync();

            // the amount paid on delivery is the receipt's first payment record
            if (paid > 0)
                _db.PurchasePayments.Add(new PurchasePayment
                {
                    PurchaseId = purchase.Id, PurchaseOrderId = po.Id, Type = PurchasePaymentType.Payment, Amount = paid,
                    Method = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Cash" : dto.PaymentMethod.Trim(),
                    Reference = $"{purchase.Reference}-P1", Date = date, Notes = "Paid on delivery", CreatedBy = receivedBy,
                    CreatedAt = DateTime.UtcNow,
                });

            po.Status = po.Items.All(i => i.ReceivedQuantity >= i.Quantity)
                ? PurchaseOrderStatus.Received
                : PurchaseOrderStatus.PartiallyReceived;
            po.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        return await GetByIdAsync(po.Id);
    }

    // ─────────────── cancel / delete ───────────────

    public async Task<PurchaseOrderDto?> CancelAsync(int id)
    {
        var po = await LoadAsync(id);
        if (po == null) return null;
        if (po.Status == PurchaseOrderStatus.Received)
            throw new PurchaseOrderException("This order has been fully received and can't be cancelled.");
        if (po.Status == PurchaseOrderStatus.Cancelled) return await GetByIdAsync(po.Id);
        // goods already received stay in stock; only the outstanding quantity is cancelled
        po.Status = PurchaseOrderStatus.Cancelled;
        po.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await GetByIdAsync(po.Id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var po = await LoadAsync(id);
        if (po == null) return false;
        if (po.Items.Any(i => i.ReceivedQuantity > 0) || await _db.Purchases.AnyAsync(p => p.PurchaseOrderId == po.Id))
            throw new PurchaseOrderException("Goods have been received against this order, so it can't be deleted. Cancel it instead.");
        if (po.Status != PurchaseOrderStatus.Draft && po.Status != PurchaseOrderStatus.Cancelled && po.Status != PurchaseOrderStatus.Ordered)
            throw new PurchaseOrderException($"A {Label(po.Status)} purchase order can't be deleted.");
        if (po.SupersededById != null)
            throw new PurchaseOrderException($"{po.PoNumber} is an older version kept as history and can't be deleted. Cancel it instead.");
        var previous = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.SupersededById == po.Id);
        if (previous != null) previous.SupersededById = null;
        _db.PurchaseOrders.Remove(po);
        await _db.SaveChangesAsync();
        return true;
    }

    // ─────────────── revisions ───────────────

    /// <summary>
    /// PO-0001 -> PO-0001_R1 -> PO-0001_R2: a new full version from any version (the supplier may come back on any of them).
    /// Every version is kept unchanged and can still be ordered, received or cancelled on its own -
    /// but once goods are received against one version the order can't be revised any more.
    /// </summary>
    public async Task<PurchaseOrderDto?> ReviseAsync(int id, SavePurchaseOrderDto dto, string? createdBy)
    {
        var source = await LoadAsync(id);
        if (source == null) return null;
        var received = await ReceivedVersionAsync(source);
        if (received != null)
            throw new PurchaseOrderException($"Goods were already received against {received}, so this order can't be revised any more.");

        var rootId = source.RootPurchaseOrderId ?? source.Id;
        var root = rootId == source.Id ? source : await _db.PurchaseOrders.FirstAsync(p => p.Id == rootId);
        var family = await _db.PurchaseOrders.Where(p => p.Id == rootId || p.RootPurchaseOrderId == rootId).ToListAsync();
        var revisionNo = family.Max(p => p.RevisionNo) + 1;
        var suffix = $"_R{revisionNo}";
        var baseNo = root.PoNumber.Length + suffix.Length > 30 ? root.PoNumber[..(30 - suffix.Length)] : root.PoNumber;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var revision = new PurchaseOrder
            {
                PoNumber = baseNo + suffix,
                RootPurchaseOrderId = rootId,
                RevisionNo = revisionNo,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
            };
            await ApplyAsync(revision, dto, source.ExpectedDate);   // an unchanged (already passed) expected date may be kept
            if (dto.Taxes == null) revision.TaxesJson = source.TaxesJson;
            _db.PurchaseOrders.Add(revision);
            await _db.SaveChangesAsync();

            var newest = family.FirstOrDefault(p => p.SupersededById == null);
            if (newest != null) newest.SupersededById = revision.Id;   // older versions stay exactly as they were
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return await GetByIdAsync(revision.Id);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<PurchaseOrderDto>> GetRevisionsAsync(int id)
    {
        var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == id);
        if (po == null) return new();
        var rootId = po.RootPurchaseOrderId ?? po.Id;
        var family = await _db.PurchaseOrders.Include(p => p.Items)
            .Where(p => p.Id == rootId || p.RootPurchaseOrderId == rootId).OrderBy(p => p.RevisionNo).ToListAsync();
        return await MapAsync(family);
    }

    /// <summary>PO number of the version of this order that has goods received (optionally ignoring this one), or null</summary>
    private async Task<string?> ReceivedVersionAsync(PurchaseOrder po, bool excludeSelf = false)
    {
        var rootId = po.RootPurchaseOrderId ?? po.Id;
        var ids = await _db.PurchaseOrders.Where(p => p.Id == rootId || p.RootPurchaseOrderId == rootId)
            .Where(p => !excludeSelf || p.Id != po.Id).Select(p => p.Id).ToListAsync();
        var receivedId = await _db.Purchases.Where(p => p.PurchaseOrderId != null && ids.Contains(p.PurchaseOrderId.Value))
            .Select(p => p.PurchaseOrderId).FirstOrDefaultAsync();
        if (receivedId == null)
            receivedId = await _db.PurchaseOrderItems.Where(i => ids.Contains(i.PurchaseOrderId) && i.ReceivedQuantity > 0)
                .Select(i => (int?)i.PurchaseOrderId).FirstOrDefaultAsync();
        return receivedId == null ? null : await _db.PurchaseOrders.Where(p => p.Id == receivedId).Select(p => p.PoNumber).FirstAsync();
    }

    // ─────────────── helpers ───────────────

    private Task<PurchaseOrder?> LoadAsync(int id) =>
        _db.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id);

    /// <param name="previousExpected">the order's current expected date: kept as it is even if it has passed</param>
    private async Task ApplyAsync(PurchaseOrder po, SavePurchaseOrderDto dto, DateTime? previousExpected = null)
    {
        // expected delivery: today or later (a date that already passed can only stay if it isn't changed)
        if (dto.ExpectedDate.HasValue && dto.ExpectedDate.Value.Date < DateTime.UtcNow.Date && dto.ExpectedDate.Value.Date != previousExpected?.Date)
            throw new PurchaseOrderException("The expected delivery date can't be in the past - choose today or a later date.");
        if (string.IsNullOrWhiteSpace(dto.SupplierName) && dto.SupplierId == null)
            throw new PurchaseOrderException("Select a supplier.");
        if (dto.Items == null || dto.Items.Count == 0)
            throw new PurchaseOrderException("Add at least one product.");
        if (dto.Status != PurchaseOrderStatus.Draft && dto.Status != PurchaseOrderStatus.Ordered)
            throw new PurchaseOrderException("Status must be Draft or Ordered.");
        if (dto.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1))
            throw new PurchaseOrderException("Each product can only appear once on an order - change its quantity instead.");

        // supplier: prefer the party record so name/phone stay correct
        if (dto.SupplierId.HasValue)
        {
            var supplier = await _db.Parties.FirstOrDefaultAsync(p => p.Id == dto.SupplierId.Value);
            if (supplier == null) throw new PurchaseOrderException("The selected supplier no longer exists.");
            po.SupplierId = supplier.Id;
            po.SupplierName = supplier.FullName;
            po.SupplierPhone = supplier.Phone;
        }
        else
        {
            po.SupplierId = null;
            po.SupplierName = dto.SupplierName.Trim();
            po.SupplierPhone = dto.SupplierPhone;
        }

        var productIds = dto.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        decimal subTotal = 0;
        foreach (var line in dto.Items)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                throw new PurchaseOrderException("One of the selected products no longer exists.");
            if (line.Quantity <= 0) throw new PurchaseOrderException($"{product.ProductName}: quantity must be at least 1.");
            if (line.UnitCost < 0 || line.Discount < 0 || line.TaxPercent < 0)
                throw new PurchaseOrderException($"{product.ProductName}: cost, discount and tax can't be negative.");

            var gross = line.Quantity * line.UnitCost;
            var discount = Math.Min(line.Discount, gross);
            var tax = Math.Round((gross - discount) * line.TaxPercent / 100, 2);
            var total = Math.Round(gross - discount + tax, 2);
            subTotal += total;
            po.Items.Add(new PurchaseOrderItem
            {
                ProductId = product.Id,
                ProductName = product.ProductName,
                Sku = product.SKU,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                Discount = discount,
                TaxPercent = line.TaxPercent,
                TaxAmount = tax,
                TotalCost = total,
            });
        }

        po.OrderDate = dto.OrderDate ?? po.OrderDate;
        po.ExpectedDate = dto.ExpectedDate;
        po.Status = dto.Status;
        po.Discount = Math.Max(0, dto.Discount);
        if (dto.Taxes != null)
        {
            // taxes on (items total - discount); the order tax is their total
            var taxes = AppliedTaxes.Calculate(dto.Taxes, subTotal - po.Discount);
            po.TaxesJson = AppliedTaxes.Serialize(taxes);
            po.OrderTax = taxes.Sum(t => t.Amount);
        }
        else po.OrderTax = Math.Max(0, dto.OrderTax);
        po.Shipping = Math.Max(0, dto.Shipping);
        po.SubTotal = Math.Round(subTotal, 2);
        po.Total = Math.Max(0, Math.Round(subTotal + po.OrderTax + po.Shipping - po.Discount, 2));
        po.Notes = dto.Notes;
        po.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<string> NextPoNumberAsync()
    {
        var numbers = await _db.PurchaseOrders.Select(p => p.PoNumber).ToListAsync();
        var max = numbers.Select(n => int.TryParse(n.Replace("PO-", ""), out var v) ? v : 0).DefaultIfEmpty(0).Max();
        return $"PO-{max + 1:D4}";
    }

    private async Task<List<PurchaseOrderDto>> MapAsync(List<PurchaseOrder> orders)
    {
        var ids = orders.Select(o => o.Id).ToList();
        var receipts = await _db.Purchases.Include(p => p.Items)
            .Where(p => p.PurchaseOrderId != null && ids.Contains(p.PurchaseOrderId.Value))
            .OrderBy(p => p.Date).ThenBy(p => p.Id).ToListAsync();
        var receiptIds = receipts.Select(r => r.Id).ToList();
        var payments = await _db.PurchasePayments.Where(p => receiptIds.Contains(p.PurchaseId))
            .OrderBy(p => p.Date).ThenBy(p => p.Id).ToListAsync();
        var productIds = orders.SelectMany(o => o.Items).Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.Include(p => p.Images)
            .Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        return orders.Select(po => new PurchaseOrderDto
        {
            Id = po.Id,
            PoNumber = po.PoNumber,
            SupplierId = po.SupplierId,
            SupplierName = po.SupplierName,
            SupplierPhone = po.SupplierPhone,
            OrderDate = po.OrderDate,
            ExpectedDate = po.ExpectedDate,
            Status = po.Status,
            SubTotal = po.SubTotal,
            OrderTax = po.OrderTax,
            Taxes = AppliedTaxes.Parse(po.TaxesJson),
            Discount = po.Discount,
            Shipping = po.Shipping,
            Total = po.Total,
            Notes = po.Notes,
            CreatedBy = po.CreatedBy,
            CreatedAt = po.CreatedAt,
            RootPurchaseOrderId = po.RootPurchaseOrderId,
            RevisionNo = po.RevisionNo,
            SupersededById = po.SupersededById,
            PaidTotal = receipts.Where(r => r.PurchaseOrderId == po.Id).Sum(r => r.Paid),
            OrderedUnits = po.Items.Sum(i => i.Quantity),
            ReceivedUnits = po.Items.Sum(i => i.ReceivedQuantity),
            ReceivedValue = receipts.Where(r => r.PurchaseOrderId == po.Id).Sum(r => r.Total),
            Items = po.Items.OrderBy(i => i.Id).Select(i =>
            {
                products.TryGetValue(i.ProductId, out var product);
                return new PurchaseOrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Sku = i.Sku,
                    ProductImage = product?.Images.FirstOrDefault()?.ImagePath ?? "",
                    Quantity = i.Quantity,
                    ReceivedQuantity = i.ReceivedQuantity,
                    UnitCost = i.UnitCost,
                    Discount = i.Discount,
                    TaxPercent = i.TaxPercent,
                    TaxAmount = i.TaxAmount,
                    TotalCost = i.TotalCost,
                    InStock = product?.Quantity ?? 0,
                };
            }).ToList(),
            Receipts = receipts.Where(r => r.PurchaseOrderId == po.Id).Select(r => new PurchaseOrderReceiptDto
            {
                PurchaseId = r.Id,
                Reference = r.Reference,
                Date = r.Date,
                Units = (int)(r.Items?.Sum(i => i.Quantity) ?? 0),
                Total = r.Total,
                Paid = r.Paid,
                Due = r.Total - r.Paid,
                PaymentStatus = r.PaymentStatus,
                Payments = payments.Where(p => p.PurchaseId == r.Id).Select(PurchasePaymentRules.ToDto).ToList(),
            }).ToList(),
        }).ToList();
    }

    private static string Label(string status) => status switch
    {
        PurchaseOrderStatus.PartiallyReceived => "partially received",
        _ => status.ToLower(),
    };
}
