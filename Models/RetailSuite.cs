using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

// ════════════════════════════════════════════════════════════════════
// Locations and the stock ledger
// ════════════════════════════════════════════════════════════════════

/// <summary>A head office, branch or warehouse. Stock is held per location (ProductBatch.LocationId).</summary>
public class Location : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>HeadOffice | Branch | Warehouse | Factory | Kitchen</summary>
    [Required, MaxLength(20)]
    public string Type { get; set; } = "Branch";

    /// <summary>Warehouse that normally supplies this branch.</summary>
    public int? ParentId { get; set; }

    [MaxLength(500)] public string? Address { get; set; }
    [MaxLength(50)] public string? Phone { get; set; }
    [MaxLength(100)] public string? DrugLicenceNo { get; set; }

    /// <summary>Location new stock goes to when nothing else is chosen.</summary>
    public bool IsDefault { get; set; }
    /// <summary>Stock here is published to the web store and Shopify.</summary>
    public bool SellsOnline { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Append-only record of every stock change.</summary>
public class StockMovement : ITenantEntity
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public int? LocationId { get; set; }
    public int? BatchId { get; set; }
    /// <summary>Lets EF fill BatchId for a batch created in the same save.</summary>
    public ProductBatch? Batch { get; set; }

    /// <summary>Signed, base units: + in, - out.</summary>
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; }

    /// <summary>Purchase | Sale | SaleReturn | PurchaseReturn | Adjustment | WriteOff | TransferOut | TransferIn | Production | Consumption | OnlineOrder | Opening | StockEntry | Other</summary>
    [Required, MaxLength(30)]
    public string MovementType { get; set; } = "Other";

    [MaxLength(30)] public string? SourceType { get; set; }
    public int? SourceId { get; set; }
    [MaxLength(100)] public string? Reference { get; set; }

    [MaxLength(200)] public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Minimum and maximum stock a location should hold; drives branch requisitions.</summary>
public class LocationStockLevel : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ProductId { get; set; }
    public int LocationId { get; set; }
    public int MinQty { get; set; }
    public int MaxQty { get; set; }
}

/// <summary>A branch asking a warehouse for stock.</summary>
public class StockRequest : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required, MaxLength(50)]
    public string Reference { get; set; } = string.Empty;

    /// <summary>Warehouse asked to supply.</summary>
    public int FromLocationId { get; set; }
    /// <summary>Requesting branch.</summary>
    public int ToLocationId { get; set; }

    /// <summary>Draft | Submitted | Approved | Fulfilled | Rejected</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Draft";

    /// <summary>Manual | AutoReorder</summary>
    [Required, MaxLength(20)]
    public string Source { get; set; } = "Manual";

    public int? TransferId { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    [MaxLength(200)] public string? RequestedBy { get; set; }
    [MaxLength(200)] public string? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<StockRequestItem> Items { get; set; } = new();
}

public class StockRequestItem : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int StockRequestId { get; set; }
    public StockRequest? StockRequest { get; set; }
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public int RequestedQty { get; set; }
    public int? ApprovedQty { get; set; }
}

// ════════════════════════════════════════════════════════════════════
// Wholesale and credit
// ════════════════════════════════════════════════════════════════════

public class PriceLevel : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class ProductPrice : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public int PriceLevelId { get; set; }

    /// <summary>Unit | Strip | Box</summary>
    [Required, MaxLength(10)]
    public string SaleUnit { get; set; } = "Unit";

    /// <summary>Quantity break in the sale unit (1 = always).</summary>
    public int MinQty { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
}

/// <summary>
/// Money received from a customer that is not tied to one bill (on-account payment),
/// or a credit note from a return. Allocations to bills are SalePayment rows with CustomerReceiptId.
/// </summary>
public class CustomerReceipt : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required, MaxLength(50)]
    public string Reference { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int? LocationId { get; set; }
    public int? TillSessionId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>Cash | Card | Bank | Cheque | Credit note</summary>
    [Required, MaxLength(30)]
    public string PaymentMethod { get; set; } = "Cash";

    [MaxLength(50)] public string? ChequeNo { get; set; }
    public DateTime? ChequeDate { get; set; }
    /// <summary>Set when this receipt is the credit note of a return.</summary>
    public int? SalesReturnId { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    [MaxLength(200)] public string? ReceivedBy { get; set; }
    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Manual lines on a customer account (opening balance corrections, write-offs, adjustments).
/// Sales, payments, receipts and credit notes come from their own tables; the statement merges all of them.
/// </summary>
public class CustomerLedgerEntry : ITenantEntity
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public int CustomerId { get; set; }
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;

    /// <summary>Opening | Adjustment | WriteOff</summary>
    [Required, MaxLength(20)]
    public string EntryType { get; set; } = "Adjustment";

    [MaxLength(50)] public string? Reference { get; set; }

    /// <summary>Customer owes more.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Debit { get; set; }
    /// <summary>Customer owes less.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Credit { get; set; }

    [MaxLength(500)] public string? Narration { get; set; }
    [MaxLength(200)] public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ════════════════════════════════════════════════════════════════════
// Medicine catalogue (shared by every tenant) and translations
// ════════════════════════════════════════════════════════════════════

/// <summary>Global, read-only list of registered medicines. Not tenant-scoped.</summary>
public class MedicineMaster
{
    public int Id { get; set; }
    [Required, MaxLength(300)] public string BrandName { get; set; } = string.Empty;
    [Required, MaxLength(300)] public string GenericName { get; set; } = string.Empty;
    [MaxLength(100)] public string? Strength { get; set; }
    [MaxLength(100)] public string? DosageForm { get; set; }
    [MaxLength(100)] public string? PackSize { get; set; }
    public int? UnitsPerStrip { get; set; }
    public int? StripsPerBox { get; set; }
    [MaxLength(200)] public string? Manufacturer { get; set; }
    [MaxLength(100)] public string? RegistrationNo { get; set; }
    [MaxLength(200)] public string? TherapeuticClass { get; set; }
    /// <summary>OTC | Rx | Controlled</summary>
    [MaxLength(20)] public string? Schedule { get; set; }
    [MaxLength(100)] public string? Barcode { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? MaxRetailPrice { get; set; }
    public DateTime? PriceUpdatedOn { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Global list of known interactions between two generics.</summary>
public class DrugInteraction
{
    public int Id { get; set; }
    [Required, MaxLength(300)] public string GenericA { get; set; } = string.Empty;
    [Required, MaxLength(300)] public string GenericB { get; set; } = string.Empty;
    /// <summary>Minor | Moderate | Major</summary>
    [Required, MaxLength(20)] public string Severity { get; set; } = "Moderate";
    [MaxLength(1000)] public string? Note { get; set; }
}

public class ProductTranslation : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ProductId { get; set; }
    /// <summary>ur | ar</summary>
    [Required, MaxLength(2)] public string Lang { get; set; } = "ur";
    [Required, MaxLength(300)] public string Name { get; set; } = string.Empty;
    /// <summary>Dosage instruction printed on the receipt label.</summary>
    [MaxLength(500)] public string? UsageNote { get; set; }
    public string? Description { get; set; }
}

// ════════════════════════════════════════════════════════════════════
// Online channels (Shopify)
// ════════════════════════════════════════════════════════════════════

public class ChannelConnection : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    /// <summary>Shopify</summary>
    [Required, MaxLength(30)] public string Channel { get; set; } = "Shopify";
    /// <summary>mystore.myshopify.com</summary>
    [Required, MaxLength(200)] public string ShopDomain { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string AccessToken { get; set; } = string.Empty;
    [MaxLength(200)] public string? WebhookSecret { get; set; }
    /// <summary>Asyntex location whose stock is published.</summary>
    public int LocationId { get; set; }
    [MaxLength(50)] public string? ExternalLocationId { get; set; }
    public bool SyncProducts { get; set; } = true;
    public bool SyncStock { get; set; } = true;
    public bool SyncPrices { get; set; } = true;
    public bool ImportOrders { get; set; } = true;
    /// <summary>Active | Paused | Error</summary>
    [Required, MaxLength(20)] public string Status { get; set; } = "Active";
    [MaxLength(1000)] public string? LastError { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ChannelProductLink : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ConnectionId { get; set; }
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    [Required, MaxLength(50)] public string ExternalProductId { get; set; } = string.Empty;
    [MaxLength(50)] public string? ExternalVariantId { get; set; }
    [MaxLength(50)] public string? ExternalInventoryItemId { get; set; }
    public int? LastPushedQty { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? LastPushedPrice { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}

public class ChannelOrder : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ConnectionId { get; set; }
    [Required, MaxLength(50)] public string ExternalOrderId { get; set; } = string.Empty;
    [MaxLength(50)] public string? ExternalNumber { get; set; }
    public int? SaleId { get; set; }
    /// <summary>Imported | Fulfilled | Cancelled | Failed</summary>
    [Required, MaxLength(20)] public string Status { get; set; } = "Imported";
    [MaxLength(1000)] public string? Error { get; set; }
    [Column(TypeName = "mediumtext")] public string? PayloadJson { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Work waiting to be sent to a channel; written with the stock change, sent by a background worker.</summary>
public class SyncOutbox : ITenantEntity
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public int ConnectionId { get; set; }
    /// <summary>StockLevel | Product</summary>
    [Required, MaxLength(30)] public string Kind { get; set; } = "StockLevel";
    public int EntityId { get; set; }
    public int Attempts { get; set; }
    /// <summary>Pending | Sent | Failed</summary>
    [Required, MaxLength(20)] public string Status { get; set; } = "Pending";
    [MaxLength(1000)] public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}

// ════════════════════════════════════════════════════════════════════
// Variants (colour / size / article)
// ════════════════════════════════════════════════════════════════════

public class VariantAttributeValue : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int VariantAttributeId { get; set; }
    [Required, MaxLength(100)] public string Value { get; set; } = string.Empty;
    [MaxLength(20)] public string? Code { get; set; }
    public int SortOrder { get; set; }
}

public class ProductOption : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ProductId { get; set; }
    public int VariantAttributeId { get; set; }
    /// <summary>1 = grid rows, 2 = grid columns, 3 = extra.</summary>
    public int Position { get; set; }
}

public class ProductVariant : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ProductId { get; set; }
    [Required, MaxLength(100)] public string Sku { get; set; } = string.Empty;
    [MaxLength(200)] public string? Barcode { get; set; }
    [MaxLength(100)] public string? Option1 { get; set; }
    [MaxLength(100)] public string? Option2 { get; set; }
    [MaxLength(100)] public string? Option3 { get; set; }
    /// <summary>NULL = the article price.</summary>
    [Column(TypeName = "decimal(18,2)")] public decimal? Price { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal? CostPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProductVariantValue : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int VariantId { get; set; }
    public int AttributeValueId { get; set; }
}

// ════════════════════════════════════════════════════════════════════
// Manufacturing (maps onto the tables the manufacturing branch created)
// ════════════════════════════════════════════════════════════════════

[Table("billofmaterials")]
public class BillOfMaterials : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    public int? FinishedProductId { get; set; }
    [MaxLength(300)] public string? FinishedProductName { get; set; }
    [MaxLength(200)] public string? FinishedProductCategory { get; set; }
    [MaxLength(200)] public string? FinishedProductSubCategory { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal SalePrice { get; set; }
    public int OutputQuantity { get; set; } = 1;
    [Column(TypeName = "decimal(18,2)")] public decimal LaborCost { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal OverheadCost { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    [Required, MaxLength(20)] public string Status { get; set; } = "active";
    public int Version { get; set; } = 1;
    public int? ShelfLifeDays { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal ExpectedYieldPct { get; set; } = 100;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<BomItem> Items { get; set; } = new();
    public List<BomOperation> Operations { get; set; } = new();
}

[Table("bomitems")]
public class BomItem : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int BomId { get; set; }
    public BillOfMaterials? Bom { get; set; }
    public int RawMaterialId { get; set; }
    /// <summary>Base units per one BOM output run.</summary>
    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; } = 1;
    [Column(TypeName = "decimal(18,2)")] public decimal UnitCost { get; set; }
    public int? SupplierId { get; set; }
}

[Table("manufacturingorders")]
public class ManufacturingOrder : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    [Required, MaxLength(50)] public string Reference { get; set; } = string.Empty;
    public int BomId { get; set; }
    public BillOfMaterials? Bom { get; set; }
    public int? FinishedProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public int? TargetStoreId { get; set; }
    /// <summary>Draft | InProgress | Completed | Cancelled</summary>
    [Required, MaxLength(30)] public string Status { get; set; } = "Draft";
    [Column(TypeName = "decimal(18,2)")] public decimal LaborCost { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal OverheadCost { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalMaterialCost { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalCost { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public int? LocationId { get; set; }
    public int? OutputLocationId { get; set; }
    [MaxLength(100)] public string? OutputBatchNo { get; set; }
    public DateTime? OutputExpiryDate { get; set; }
    public int? OutputBatchId { get; set; }
    public int? ActualOutputQty { get; set; }
    public int ScrapQty { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ManufacturingOrderItem> Items { get; set; } = new();
    public List<MoOperation> Operations { get; set; } = new();
}

[Table("manufacturingorderitems")]
public class ManufacturingOrderItem : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ManufacturingOrderId { get; set; }
    public ManufacturingOrder? ManufacturingOrder { get; set; }
    public int RawMaterialId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal RequiredQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal ConsumedQuantity { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal UnitCost { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalCost { get; set; }
    public int? SupplierId { get; set; }
    [Column(TypeName = "decimal(18,3)")] public decimal WastageQty { get; set; }
}

/// <summary>Which batches a production material line was drawn from.</summary>
public class MoItemBatch : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int MoItemId { get; set; }
    public int? BatchId { get; set; }
    public int Quantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal UnitCost { get; set; }
}

public class WorkCentre : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? LocationId { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal CostPerHour { get; set; }
}

public class BomOperation : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int BomId { get; set; }
    public BillOfMaterials? Bom { get; set; }
    public int Sequence { get; set; }
    public int WorkCentreId { get; set; }
    [MaxLength(300)] public string? Description { get; set; }
    public int? PlannedMinutes { get; set; }
}

public class MoOperation : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int OrderId { get; set; }
    public ManufacturingOrder? Order { get; set; }
    public int Sequence { get; set; }
    public int WorkCentreId { get; set; }
    [MaxLength(300)] public string? Description { get; set; }
    /// <summary>Pending | Running | Done</summary>
    [Required, MaxLength(20)] public string Status { get; set; } = "Pending";
    [MaxLength(200)] public string? Operator { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// ════════════════════════════════════════════════════════════════════
// Restaurant
// ════════════════════════════════════════════════════════════════════

public class Recipe : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    /// <summary>The menu item.</summary>
    public int ProductId { get; set; }
    [Column(TypeName = "decimal(18,3)")] public decimal YieldQty { get; set; } = 1;
    [MaxLength(1000)] public string? Notes { get; set; }
    public List<RecipeItem> Items { get; set; } = new();
}

public class RecipeItem : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    /// <summary>products.Id of the ingredient; its stock is in base units (g, ml, pcs).</summary>
    public int IngredientId { get; set; }
    [Column(TypeName = "decimal(18,3)")] public decimal Quantity { get; set; }
    [Required, MaxLength(20)] public string Unit { get; set; } = "g";
    [Column(TypeName = "decimal(5,2)")] public decimal WastagePct { get; set; }
}

public class ModifierGroup : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; } = 1;
    public List<Modifier> Modifiers { get; set; } = new();
}

public class Modifier : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ModifierGroupId { get; set; }
    public ModifierGroup? ModifierGroup { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal Price { get; set; }
    public int? IngredientId { get; set; }
    [Column(TypeName = "decimal(18,3)")] public decimal? IngredientQty { get; set; }
}

public class ProductModifierGroup : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ProductId { get; set; }
    public int ModifierGroupId { get; set; }
}

public class DiningTable : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int LocationId { get; set; }
    [Required, MaxLength(50)] public string Name { get; set; } = string.Empty;
    public int Seats { get; set; } = 4;
    /// <summary>Free | Occupied | Reserved</summary>
    [Required, MaxLength(20)] public string Status { get; set; } = "Free";
}

public class KitchenStation : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int LocationId { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(200)] public string? PrinterName { get; set; }
}

public class KitchenTicket : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int SaleId { get; set; }
    public int StationId { get; set; }
    [Required, MaxLength(30)] public string TicketNo { get; set; } = string.Empty;
    /// <summary>New | Preparing | Ready | Served</summary>
    [Required, MaxLength(20)] public string Status { get; set; } = "New";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadyAt { get; set; }
    public List<KitchenTicketItem> Items { get; set; } = new();
}

public class KitchenTicketItem : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int TicketId { get; set; }
    public KitchenTicket? Ticket { get; set; }
    public int SaleItemId { get; set; }
    [MaxLength(300)] public string ItemName { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,3)")] public decimal Quantity { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}
