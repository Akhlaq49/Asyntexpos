-- PlanMedia table for storing multiple images/videos associated with installment plans
-- Images can be linked to customer, guarantor, or the plan itself (for video)

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PlanMedia')
BEGIN
    CREATE TABLE PlanMedia (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        TenantId        INT NOT NULL DEFAULT 0,
        PlanId          INT NOT NULL,
        EntityType      NVARCHAR(20) NOT NULL,          -- 'customer', 'guarantor', 'plan'
        EntityId        INT NULL,                        -- PartyId for customer/guarantor, NULL for plan-level
        MediaType       NVARCHAR(20) NOT NULL DEFAULT 'image',  -- 'image' or 'video'
        FilePath        NVARCHAR(500) NOT NULL,
        CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_PlanMedia_Plan FOREIGN KEY (PlanId) REFERENCES InstallmentPlans(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_PlanMedia_PlanId ON PlanMedia(PlanId);
    CREATE INDEX IX_PlanMedia_EntityType_EntityId ON PlanMedia(EntityType, EntityId);
END
