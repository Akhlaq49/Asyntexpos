using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public static class DatabaseSeeder
{
    public static void SeedDemoData(AppDbContext db, int tenantId)
    {
        // Guard: skip if data already seeded (check for any store existing)
        if (db.Stores.IgnoreQueryFilters().Any(s => s.TenantId == tenantId))
            return;

        var now = DateTime.UtcNow;

        // ─── STORES ───────────────────────────────────────────────
        var stores = new List<Store>
        {
            new() { TenantId = tenantId, Value = "main-store", Label = "Main Store", CreatedAt = now },
            new() { TenantId = tenantId, Value = "branch-1", Label = "Branch 1 - Downtown", CreatedAt = now },
            new() { TenantId = tenantId, Value = "branch-2", Label = "Branch 2 - Mall Outlet", CreatedAt = now },
        };
        db.Stores.AddRange(stores);
        db.SaveChanges();

        // ─── WAREHOUSES ───────────────────────────────────────────
        var warehouses = new List<Warehouse>
        {
            new() { TenantId = tenantId, Value = "central-warehouse", Label = "Central Warehouse", CreatedAt = now },
            new() { TenantId = tenantId, Value = "secondary-warehouse", Label = "Secondary Warehouse", CreatedAt = now },
        };
        db.Warehouses.AddRange(warehouses);
        db.SaveChanges();

        // ─── BRANDS ──────────────────────────────────────────────
        var brands = new List<Brand>
        {
            new() { TenantId = tenantId, Value = "samsung", Label = "Samsung", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Value = "apple", Label = "Apple", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Value = "hp", Label = "HP", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Value = "dell", Label = "Dell", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Value = "nike", Label = "Nike", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Value = "adidas", Label = "Adidas", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Value = "sony", Label = "Sony", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Value = "lg", Label = "LG", Status = "active", CreatedAt = now },
        };
        db.Brands.AddRange(brands);
        db.SaveChanges();

        // ─── UNITS ───────────────────────────────────────────────
        var units = new List<Unit>
        {
            new() { TenantId = tenantId, Value = "pc", Label = "Piece (pc)", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Value = "kg", Label = "Kilogram (kg)", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Value = "ltr", Label = "Litre (ltr)", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Value = "mtr", Label = "Meter (mtr)", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Value = "box", Label = "Box", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Value = "pair", Label = "Pair", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Value = "set", Label = "Set", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Value = "dozen", Label = "Dozen", Status = "active", CreatedAt = now },
        };
        db.Units.AddRange(units);
        db.SaveChanges();

        // ─── WARRANTIES ──────────────────────────────────────────
        var warranties = new List<Warranty>
        {
            new() { TenantId = tenantId, Name = "6 Month Warranty", Description = "Standard 6-month manufacturer warranty", Duration = 6, Period = "Month", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Name = "1 Year Warranty", Description = "Standard 1-year manufacturer warranty", Duration = 1, Period = "Year", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Name = "2 Year Warranty", Description = "Extended 2-year warranty with parts & labor", Duration = 2, Period = "Year", Status = "active", CreatedAt = now },
        };
        db.Warranties.AddRange(warranties);
        db.SaveChanges();

        // ─── CATEGORIES ──────────────────────────────────────────
        var categories = new List<Category>
        {
            new() { TenantId = tenantId, Name = "Electronics", Slug = "electronics", Status = "active", CreatedOn = now },
            new() { TenantId = tenantId, Name = "Clothing & Apparel", Slug = "clothing-apparel", Status = "active", CreatedOn = now },
            new() { TenantId = tenantId, Name = "Groceries", Slug = "groceries", Status = "active", CreatedOn = now },
            new() { TenantId = tenantId, Name = "Furniture", Slug = "furniture", Status = "active", CreatedOn = now },
            new() { TenantId = tenantId, Name = "Accessories", Slug = "accessories", Status = "active", CreatedOn = now },
        };
        db.Categories.AddRange(categories);
        db.SaveChanges();

        // ─── SUB-CATEGORIES ─────────────────────────────────────
        var electronics = categories[0];
        var clothing = categories[1];
        var groceries = categories[2];
        var furniture = categories[3];
        var accessories = categories[4];

        var subCategories = new List<SubCategory>
        {
            // Electronics
            new() { TenantId = tenantId, SubCategoryName = "Mobile Phones", CategoryId = electronics.Id, CategoryCode = "ELEC", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, SubCategoryName = "Laptops", CategoryId = electronics.Id, CategoryCode = "ELEC", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, SubCategoryName = "Televisions", CategoryId = electronics.Id, CategoryCode = "ELEC", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, SubCategoryName = "Audio & Speakers", CategoryId = electronics.Id, CategoryCode = "ELEC", Status = "active", CreatedAt = now },
            // Clothing
            new() { TenantId = tenantId, SubCategoryName = "Men's Wear", CategoryId = clothing.Id, CategoryCode = "CLTH", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, SubCategoryName = "Women's Wear", CategoryId = clothing.Id, CategoryCode = "CLTH", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, SubCategoryName = "Kids Wear", CategoryId = clothing.Id, CategoryCode = "CLTH", Status = "active", CreatedAt = now },
            // Groceries
            new() { TenantId = tenantId, SubCategoryName = "Beverages", CategoryId = groceries.Id, CategoryCode = "GROC", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, SubCategoryName = "Snacks", CategoryId = groceries.Id, CategoryCode = "GROC", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, SubCategoryName = "Dairy Products", CategoryId = groceries.Id, CategoryCode = "GROC", Status = "active", CreatedAt = now },
            // Furniture
            new() { TenantId = tenantId, SubCategoryName = "Office Furniture", CategoryId = furniture.Id, CategoryCode = "FURN", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, SubCategoryName = "Home Furniture", CategoryId = furniture.Id, CategoryCode = "FURN", Status = "active", CreatedAt = now },
            // Accessories
            new() { TenantId = tenantId, SubCategoryName = "Phone Cases", CategoryId = accessories.Id, CategoryCode = "ACCS", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, SubCategoryName = "Cables & Chargers", CategoryId = accessories.Id, CategoryCode = "ACCS", Status = "active", CreatedAt = now },
        };
        db.SubCategories.AddRange(subCategories);
        db.SaveChanges();

        // ─── PRODUCTS ────────────────────────────────────────────
        var products = new List<Product>
        {
            // Electronics - Phones
            new() { TenantId = tenantId, ProductName = "Samsung Galaxy S24", Slug = "samsung-galaxy-s24", SKU = "ELEC-PH-001", Category = "Electronics", SubCategory = "Mobile Phones", Brand = "Samsung", Unit = "pc", ProductType = "single", Quantity = 50, Price = 999.99m, QuantityAlert = 5, Store = "Main Store", Warehouse = "Central Warehouse", BarcodeSymbology = "CODE128", ItemBarcode = "8901234567890", CreatedAt = now, UpdatedAt = now },
            new() { TenantId = tenantId, ProductName = "Apple iPhone 15 Pro", Slug = "apple-iphone-15-pro", SKU = "ELEC-PH-002", Category = "Electronics", SubCategory = "Mobile Phones", Brand = "Apple", Unit = "pc", ProductType = "single", Quantity = 35, Price = 1199.99m, QuantityAlert = 3, Store = "Main Store", Warehouse = "Central Warehouse", CreatedAt = now, UpdatedAt = now },
            new() { TenantId = tenantId, ProductName = "Samsung Galaxy A54", Slug = "samsung-galaxy-a54", SKU = "ELEC-PH-003", Category = "Electronics", SubCategory = "Mobile Phones", Brand = "Samsung", Unit = "pc", ProductType = "single", Quantity = 80, Price = 449.99m, QuantityAlert = 10, Store = "Branch 1 - Downtown", CreatedAt = now, UpdatedAt = now },

            // Electronics - Laptops
            new() { TenantId = tenantId, ProductName = "HP Pavilion 15", Slug = "hp-pavilion-15", SKU = "ELEC-LP-001", Category = "Electronics", SubCategory = "Laptops", Brand = "HP", Unit = "pc", ProductType = "single", Quantity = 20, Price = 749.99m, QuantityAlert = 3, Store = "Main Store", Warehouse = "Central Warehouse", CreatedAt = now, UpdatedAt = now },
            new() { TenantId = tenantId, ProductName = "Dell Inspiron 14", Slug = "dell-inspiron-14", SKU = "ELEC-LP-002", Category = "Electronics", SubCategory = "Laptops", Brand = "Dell", Unit = "pc", ProductType = "single", Quantity = 15, Price = 699.99m, QuantityAlert = 2, Store = "Main Store", CreatedAt = now, UpdatedAt = now },
            new() { TenantId = tenantId, ProductName = "Apple MacBook Air M2", Slug = "apple-macbook-air-m2", SKU = "ELEC-LP-003", Category = "Electronics", SubCategory = "Laptops", Brand = "Apple", Unit = "pc", ProductType = "single", Quantity = 10, Price = 1299.99m, QuantityAlert = 2, Store = "Main Store", CreatedAt = now, UpdatedAt = now },

            // Electronics - TVs
            new() { TenantId = tenantId, ProductName = "Samsung 55\" 4K Smart TV", Slug = "samsung-55-4k-tv", SKU = "ELEC-TV-001", Category = "Electronics", SubCategory = "Televisions", Brand = "Samsung", Unit = "pc", ProductType = "single", Quantity = 12, Price = 599.99m, QuantityAlert = 2, Store = "Main Store", Warehouse = "Central Warehouse", CreatedAt = now, UpdatedAt = now },
            new() { TenantId = tenantId, ProductName = "LG 65\" OLED TV", Slug = "lg-65-oled-tv", SKU = "ELEC-TV-002", Category = "Electronics", SubCategory = "Televisions", Brand = "LG", Unit = "pc", ProductType = "single", Quantity = 8, Price = 1499.99m, QuantityAlert = 2, Store = "Main Store", CreatedAt = now, UpdatedAt = now },

            // Electronics - Audio
            new() { TenantId = tenantId, ProductName = "Sony WH-1000XM5 Headphones", Slug = "sony-wh1000xm5", SKU = "ELEC-AU-001", Category = "Electronics", SubCategory = "Audio & Speakers", Brand = "Sony", Unit = "pc", ProductType = "single", Quantity = 40, Price = 349.99m, QuantityAlert = 5, Store = "Main Store", CreatedAt = now, UpdatedAt = now },

            // Clothing
            new() { TenantId = tenantId, ProductName = "Nike Air Max Sneakers", Slug = "nike-air-max-sneakers", SKU = "CLTH-MW-001", Category = "Clothing & Apparel", SubCategory = "Men's Wear", Brand = "Nike", Unit = "pair", ProductType = "single", Quantity = 60, Price = 129.99m, QuantityAlert = 10, Store = "Branch 2 - Mall Outlet", CreatedAt = now, UpdatedAt = now },
            new() { TenantId = tenantId, ProductName = "Adidas Ultraboost 22", Slug = "adidas-ultraboost-22", SKU = "CLTH-MW-002", Category = "Clothing & Apparel", SubCategory = "Men's Wear", Brand = "Adidas", Unit = "pair", ProductType = "single", Quantity = 45, Price = 179.99m, QuantityAlert = 8, Store = "Branch 2 - Mall Outlet", CreatedAt = now, UpdatedAt = now },
            new() { TenantId = tenantId, ProductName = "Nike Sports T-Shirt", Slug = "nike-sports-tshirt", SKU = "CLTH-MW-003", Category = "Clothing & Apparel", SubCategory = "Men's Wear", Brand = "Nike", Unit = "pc", ProductType = "single", Quantity = 100, Price = 39.99m, QuantityAlert = 15, Store = "Branch 2 - Mall Outlet", CreatedAt = now, UpdatedAt = now },

            // Furniture
            new() { TenantId = tenantId, ProductName = "Executive Office Chair", Slug = "executive-office-chair", SKU = "FURN-OF-001", Category = "Furniture", SubCategory = "Office Furniture", Unit = "pc", ProductType = "single", Quantity = 25, Price = 299.99m, QuantityAlert = 3, Store = "Main Store", Warehouse = "Secondary Warehouse", CreatedAt = now, UpdatedAt = now },
            new() { TenantId = tenantId, ProductName = "Standing Desk - Adjustable", Slug = "standing-desk-adjustable", SKU = "FURN-OF-002", Category = "Furniture", SubCategory = "Office Furniture", Unit = "pc", ProductType = "single", Quantity = 10, Price = 499.99m, QuantityAlert = 2, Store = "Main Store", Warehouse = "Secondary Warehouse", CreatedAt = now, UpdatedAt = now },

            // Accessories
            new() { TenantId = tenantId, ProductName = "USB-C Fast Charger 65W", Slug = "usbc-fast-charger-65w", SKU = "ACCS-CC-001", Category = "Accessories", SubCategory = "Cables & Chargers", Unit = "pc", ProductType = "single", Quantity = 200, Price = 29.99m, QuantityAlert = 20, Store = "Main Store", CreatedAt = now, UpdatedAt = now },
            new() { TenantId = tenantId, ProductName = "Premium Phone Case - Universal", Slug = "premium-phone-case", SKU = "ACCS-PC-001", Category = "Accessories", SubCategory = "Phone Cases", Unit = "pc", ProductType = "single", Quantity = 150, Price = 19.99m, QuantityAlert = 20, Store = "Main Store", CreatedAt = now, UpdatedAt = now },

            // Raw materials for manufacturing
            new() { TenantId = tenantId, ProductName = "Screen Panel 6.5\"", Slug = "screen-panel-65", SKU = "RAW-SP-001", Category = "Electronics", SubCategory = "Mobile Phones", Unit = "pc", ProductType = "single", Quantity = 200, Price = 120.00m, QuantityAlert = 20, Store = "Main Store", Warehouse = "Central Warehouse", Description = "Raw material - AMOLED screen panel", CreatedAt = now, UpdatedAt = now },
            new() { TenantId = tenantId, ProductName = "Battery Cell 5000mAh", Slug = "battery-cell-5000mah", SKU = "RAW-BC-001", Category = "Electronics", SubCategory = "Mobile Phones", Unit = "pc", ProductType = "single", Quantity = 300, Price = 25.00m, QuantityAlert = 30, Store = "Main Store", Warehouse = "Central Warehouse", Description = "Raw material - Li-ion battery cell", CreatedAt = now, UpdatedAt = now },
            new() { TenantId = tenantId, ProductName = "Phone Chassis Frame", Slug = "phone-chassis-frame", SKU = "RAW-CF-001", Category = "Electronics", SubCategory = "Mobile Phones", Unit = "pc", ProductType = "single", Quantity = 250, Price = 35.00m, QuantityAlert = 25, Store = "Main Store", Warehouse = "Central Warehouse", Description = "Raw material - Aluminum chassis frame", CreatedAt = now, UpdatedAt = now },
        };
        db.Products.AddRange(products);
        db.SaveChanges();

        // ─── DEPARTMENTS ─────────────────────────────────────────
        var departments = new List<Department>
        {
            new() { TenantId = tenantId, Name = "Sales", Description = "Sales and customer service department", Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "Operations", Description = "Store operations and inventory management", Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "Finance", Description = "Accounting and financial management", Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "IT", Description = "Information technology and support", Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "HR", Description = "Human resources and recruitment", Status = "active", IsActive = true, CreatedAt = now },
        };
        db.Departments.AddRange(departments);
        db.SaveChanges();

        // ─── DESIGNATIONS ────────────────────────────────────────
        var designations = new List<Designation>
        {
            new() { TenantId = tenantId, Name = "Sales Executive", DepartmentId = departments[0].Id, Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "Sales Manager", DepartmentId = departments[0].Id, Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "Cashier", DepartmentId = departments[0].Id, Description = "Point of sale billing", Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "Store Manager", DepartmentId = departments[1].Id, Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "Inventory Officer", DepartmentId = departments[1].Id, Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "Accountant", DepartmentId = departments[2].Id, Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "IT Support", DepartmentId = departments[3].Id, Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "HR Manager", DepartmentId = departments[4].Id, Status = "active", IsActive = true, CreatedAt = now },
        };
        db.Designations.AddRange(designations);
        db.SaveChanges();

        // ─── SHIFTS ──────────────────────────────────────────────
        var shifts = new List<Shift>
        {
            new() { TenantId = tenantId, Name = "Morning Shift", StartTime = "09:00", EndTime = "17:00", WeekOff = "Sunday", Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "Evening Shift", StartTime = "14:00", EndTime = "22:00", WeekOff = "Sunday", Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "Full Day Shift", StartTime = "08:00", EndTime = "20:00", WeekOff = "Friday", Status = "active", IsActive = true, CreatedAt = now },
        };
        db.Shifts.AddRange(shifts);
        db.SaveChanges();

        // ─── LEAVE TYPES ─────────────────────────────────────────
        var leaveTypes = new List<LeaveType>
        {
            new() { TenantId = tenantId, Name = "Annual Leave", Quota = 20, Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "Sick Leave", Quota = 12, Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "Casual Leave", Quota = 10, Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Name = "Maternity Leave", Quota = 90, Status = "active", IsActive = true, CreatedAt = now },
        };
        db.LeaveTypes.AddRange(leaveTypes);
        db.SaveChanges();

        // ─── CUSTOMERS ───────────────────────────────────────────
        var customers = new List<Party>
        {
            new() { TenantId = tenantId, FullName = "Ahmed", LastName = "Khan", Email = "ahmed.khan@email.com", Phone = "03001234567", Address = "House 12, Street 5, F-8", City = "Islamabad", Country = "Pakistan", Role = "Customer", Status = "active", IsActive = true, Code = "CU001", CreatedAt = now },
            new() { TenantId = tenantId, FullName = "Sara", LastName = "Ali", Email = "sara.ali@email.com", Phone = "03211234567", Address = "Apt 45, Gulberg III", City = "Lahore", Country = "Pakistan", Role = "Customer", Status = "active", IsActive = true, Code = "CU002", CreatedAt = now },
            new() { TenantId = tenantId, FullName = "Usman", LastName = "Malik", Email = "usman.malik@email.com", Phone = "03451234567", Address = "Block C, DHA Phase 5", City = "Karachi", Country = "Pakistan", Role = "Customer", Status = "active", IsActive = true, Code = "CU003", CreatedAt = now },
            new() { TenantId = tenantId, FullName = "Fatima", LastName = "Noor", Email = "fatima.noor@email.com", Phone = "03331234567", Address = "Lane 3, Satellite Town", City = "Rawalpindi", Country = "Pakistan", Role = "Customer", Status = "active", IsActive = true, Code = "CU004", CreatedAt = now },
            new() { TenantId = tenantId, FullName = "Bilal", LastName = "Ahmad", Email = "bilal.ahmad@email.com", Phone = "03121234567", Address = "Model Town, Block B", City = "Faisalabad", Country = "Pakistan", Role = "Customer", Status = "active", IsActive = true, Code = "CU005", CreatedAt = now },
        };
        db.Parties.AddRange(customers);
        db.SaveChanges();

        // ─── SUPPLIERS ───────────────────────────────────────────
        var suppliers = new List<Party>
        {
            new() { TenantId = tenantId, FullName = "TechParts International", Email = "info@techparts.com", Phone = "04235001000", Address = "Industrial Area, Phase 2", City = "Lahore", Country = "Pakistan", Role = "Supplier", Status = "active", IsActive = true, Code = "SU001", CompanyName = "TechParts International", ContactPerson = "Kamran Shahid", CreatedAt = now },
            new() { TenantId = tenantId, FullName = "Global Electronics Supply", Email = "sales@globalelec.com", Phone = "02135002000", Address = "SITE Industrial Area", City = "Karachi", Country = "Pakistan", Role = "Supplier", Status = "active", IsActive = true, Code = "SU002", CompanyName = "Global Electronics Supply", ContactPerson = "Rizwan Akhtar", CreatedAt = now },
            new() { TenantId = tenantId, FullName = "FurnishCo Trading", Email = "orders@furnishco.pk", Phone = "05135003000", Address = "I-9 Industrial Area", City = "Islamabad", Country = "Pakistan", Role = "Supplier", Status = "active", IsActive = true, Code = "SU003", CompanyName = "FurnishCo Trading", ContactPerson = "Hasan Raza", CreatedAt = now },
        };
        db.Parties.AddRange(suppliers);
        db.SaveChanges();

        // ─── EMPLOYEES ───────────────────────────────────────────
        var employees = new List<Party>
        {
            new() { TenantId = tenantId, FullName = "Ali", LastName = "Hassan", Email = "ali.hassan@company.com", Phone = "03001112233", Role = "Employee", Status = "active", IsActive = true, Code = "EMP001", EmployeeId = "EMP001", DepartmentId = departments[0].Id, DesignationId = designations[1].Id, ShiftId = shifts[0].Id, DateOfJoining = now.AddYears(-2), BasicSalary = 75000m, CreatedAt = now },
            new() { TenantId = tenantId, FullName = "Zainab", LastName = "Fatima", Email = "zainab.fatima@company.com", Phone = "03002223344", Role = "Employee", Status = "active", IsActive = true, Code = "EMP002", EmployeeId = "EMP002", DepartmentId = departments[0].Id, DesignationId = designations[2].Id, ShiftId = shifts[0].Id, DateOfJoining = now.AddYears(-1), BasicSalary = 45000m, CreatedAt = now },
            new() { TenantId = tenantId, FullName = "Hamza", LastName = "Siddiqui", Email = "hamza.siddiqui@company.com", Phone = "03003334455", Role = "Employee", Status = "active", IsActive = true, Code = "EMP003", EmployeeId = "EMP003", DepartmentId = departments[1].Id, DesignationId = designations[4].Id, ShiftId = shifts[0].Id, DateOfJoining = now.AddMonths(-6), BasicSalary = 55000m, CreatedAt = now },
            new() { TenantId = tenantId, FullName = "Ayesha", LastName = "Bukhari", Email = "ayesha.bukhari@company.com", Phone = "03004445566", Role = "Employee", Status = "active", IsActive = true, Code = "EMP004", EmployeeId = "EMP004", DepartmentId = departments[2].Id, DesignationId = designations[5].Id, ShiftId = shifts[0].Id, DateOfJoining = now.AddMonths(-8), BasicSalary = 65000m, CreatedAt = now },

            // Manager user with login
            new() { TenantId = tenantId, FullName = "Imran", LastName = "Shah", Email = "manager@company.com", Phone = "03005556677", Role = "Manager", Status = "active", IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword("manager123"), CreatedAt = now },

            // Biller user with login
            new() { TenantId = tenantId, FullName = "Nadia", LastName = "Hussain", Email = "biller@company.com", Phone = "03006667788", Role = "Biller", Status = "active", IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword("biller123"), CreatedAt = now },
        };
        db.Parties.AddRange(employees);
        db.SaveChanges();

        // ─── EXPENSE CATEGORIES ──────────────────────────────────
        var expenseCategories = new List<ExpenseCategory>
        {
            new() { TenantId = tenantId, Name = "Rent", Description = "Office and store rent", Status = "active", CreatedOn = now },
            new() { TenantId = tenantId, Name = "Utilities", Description = "Electricity, gas, water bills", Status = "active", CreatedOn = now },
            new() { TenantId = tenantId, Name = "Salaries", Description = "Employee salaries and wages", Status = "active", CreatedOn = now },
            new() { TenantId = tenantId, Name = "Marketing", Description = "Advertising and promotions", Status = "active", CreatedOn = now },
            new() { TenantId = tenantId, Name = "Maintenance", Description = "Equipment and store maintenance", Status = "active", CreatedOn = now },
            new() { TenantId = tenantId, Name = "Transportation", Description = "Delivery and commute expenses", Status = "active", CreatedOn = now },
        };
        db.ExpenseCategories.AddRange(expenseCategories);
        db.SaveChanges();

        // ─── INCOME CATEGORIES ───────────────────────────────────
        var incomeCategories = new List<IncomeCategory>
        {
            new() { TenantId = tenantId, Code = "INC-001", Name = "Product Sales", CreatedOn = now },
            new() { TenantId = tenantId, Code = "INC-002", Name = "Service Income", CreatedOn = now },
            new() { TenantId = tenantId, Code = "INC-003", Name = "Rental Income", CreatedOn = now },
            new() { TenantId = tenantId, Code = "INC-004", Name = "Commission Income", CreatedOn = now },
        };
        db.IncomeCategories.AddRange(incomeCategories);
        db.SaveChanges();

        // ─── ACCOUNT TYPES ───────────────────────────────────────
        var accountTypes = new List<AccountType>
        {
            new() { TenantId = tenantId, Name = "Cash", Status = "active", CreatedOn = now },
            new() { TenantId = tenantId, Name = "Current Account", Status = "active", CreatedOn = now },
            new() { TenantId = tenantId, Name = "Savings Account", Status = "active", CreatedOn = now },
            new() { TenantId = tenantId, Name = "Mobile Wallet", Status = "active", CreatedOn = now },
        };
        db.AccountTypes.AddRange(accountTypes);
        db.SaveChanges();

        // ─── BANK ACCOUNTS ───────────────────────────────────────
        var bankAccounts = new List<BankAccount>
        {
            new() { TenantId = tenantId, HolderName = "Asyntex POS Pvt Ltd", AccountNumber = "0012345678901", BankName = "Meezan Bank", Branch = "Main Branch - Islamabad", IFSC = "MEZN0001234", AccountTypeId = accountTypes[1].Id, OpeningBalance = 500000m, Notes = "Primary business account", Status = "active", IsDefault = true, CreatedOn = now },
            new() { TenantId = tenantId, HolderName = "Asyntex POS Pvt Ltd", AccountNumber = "0098765432101", BankName = "HBL", Branch = "DHA Phase 2 - Lahore", AccountTypeId = accountTypes[2].Id, OpeningBalance = 250000m, Notes = "Savings account", Status = "active", IsDefault = false, CreatedOn = now },
        };
        db.BankAccounts.AddRange(bankAccounts);
        db.SaveChanges();

        // ─── COUPONS ─────────────────────────────────────────────
        var coupons = new List<Coupon>
        {
            new() { TenantId = tenantId, Name = "Summer Sale 10%", Code = "SUMMER10", Description = "10% off on all electronics", Type = "Percentage", Discount = 10, Limit = 100, StartDate = now.ToString("yyyy-MM-dd"), EndDate = now.AddMonths(3).ToString("yyyy-MM-dd"), OncePerCustomer = true, Status = "Active", CreatedAt = now },
            new() { TenantId = tenantId, Name = "Flat 500 Off", Code = "FLAT500", Description = "Flat Rs. 500 discount on orders above Rs. 5000", Type = "Fixed", Discount = 500, Limit = 50, StartDate = now.ToString("yyyy-MM-dd"), EndDate = now.AddMonths(1).ToString("yyyy-MM-dd"), OncePerCustomer = false, Status = "Active", CreatedAt = now },
            new() { TenantId = tenantId, Name = "New Customer Welcome", Code = "WELCOME20", Description = "20% off for first-time customers", Type = "Percentage", Discount = 20, Limit = 200, StartDate = now.ToString("yyyy-MM-dd"), EndDate = now.AddMonths(6).ToString("yyyy-MM-dd"), OncePerCustomer = true, Status = "Active", CreatedAt = now },
        };
        db.Coupons.AddRange(coupons);
        db.SaveChanges();

        // ─── HOLIDAYS ────────────────────────────────────────────
        var holidays = new List<Holiday>
        {
            new() { TenantId = tenantId, Title = "Independence Day", FromDate = new DateTime(now.Year, 8, 14), ToDate = new DateTime(now.Year, 8, 14), Days = 1, Description = "Pakistan Independence Day", Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Title = "Quaid-e-Azam Day", FromDate = new DateTime(now.Year, 12, 25), ToDate = new DateTime(now.Year, 12, 25), Days = 1, Description = "Birthday of the Founder of Pakistan", Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Title = "Labour Day", FromDate = new DateTime(now.Year, 5, 1), ToDate = new DateTime(now.Year, 5, 1), Days = 1, Description = "International Workers' Day", Status = "active", IsActive = true, CreatedAt = now },
            new() { TenantId = tenantId, Title = "Kashmir Day", FromDate = new DateTime(now.Year, 2, 5), ToDate = new DateTime(now.Year, 2, 5), Days = 1, Description = "Kashmir Solidarity Day", Status = "active", IsActive = true, CreatedAt = now },
        };
        db.Holidays.AddRange(holidays);
        db.SaveChanges();

        // ─── SAMPLE PURCHASES ────────────────────────────────────
        var purchase = new Purchase
        {
            TenantId = tenantId,
            SupplierName = "TechParts International",
            SupplierRef = "SU001",
            Reference = "PUR-0001",
            Date = now.AddDays(-15),
            Status = "Received",
            PaymentStatus = "Paid",
            OrderTax = 0,
            Discount = 0,
            Shipping = 500,
            Total = 37500m,
            Paid = 37500m,
            Notes = "Bulk purchase of screen panels and batteries",
            CreatedAt = now,
        };
        db.Purchases.Add(purchase);
        db.SaveChanges();

        var purchaseItems = new List<PurchaseItem>
        {
            new() { TenantId = tenantId, PurchaseId = purchase.Id, ProductId = products[16].Id, ProductName = "Screen Panel 6.5\"", Quantity = 100, PurchasePrice = 120m, Discount = 0, TaxPercentage = 0, TaxAmount = 0, UnitCost = 120m, TotalCost = 12000m, CreatedAt = now },
            new() { TenantId = tenantId, PurchaseId = purchase.Id, ProductId = products[17].Id, ProductName = "Battery Cell 5000mAh", Quantity = 200, PurchasePrice = 25m, Discount = 0, TaxPercentage = 0, TaxAmount = 0, UnitCost = 25m, TotalCost = 5000m, CreatedAt = now },
            new() { TenantId = tenantId, PurchaseId = purchase.Id, ProductId = products[18].Id, ProductName = "Phone Chassis Frame", Quantity = 150, PurchasePrice = 35m, Discount = 0, TaxPercentage = 0, TaxAmount = 0, UnitCost = 35m, TotalCost = 5250m, CreatedAt = now },
        };
        db.PurchaseItems.AddRange(purchaseItems);
        db.SaveChanges();

        // ─── SAMPLE SALE ─────────────────────────────────────────
        var sale = new Sale
        {
            TenantId = tenantId,
            Reference = "SAL-0001",
            CustomerId = customers[0].Id,
            CustomerName = "Ahmed Khan",
            Biller = "Admin",
            GrandTotal = 1449.98m,
            Paid = 1449.98m,
            Due = 0,
            OrderTax = 0,
            Discount = 0,
            Shipping = 0,
            Status = "Completed",
            PaymentStatus = "Paid",
            Notes = "Walk-in sale",
            Source = "offline",
            SaleDate = now.AddDays(-5),
            CreatedAt = now,
        };
        db.Sales.Add(sale);
        db.SaveChanges();

        var saleItems = new List<SaleItem>
        {
            new() { TenantId = tenantId, SaleId = sale.Id, ProductId = products[0].Id, ProductName = "Samsung Galaxy S24", Quantity = 1, PurchasePrice = 800m, Discount = 0, TaxPercent = 0, TaxAmount = 0, UnitCost = 999.99m, TotalCost = 999.99m },
            new() { TenantId = tenantId, SaleId = sale.Id, ProductId = products[8].Id, ProductName = "Sony WH-1000XM5 Headphones", Quantity = 1, PurchasePrice = 250m, Discount = 0, TaxPercent = 0, TaxAmount = 0, UnitCost = 349.99m, TotalCost = 349.99m },
        };
        db.SaleItems.AddRange(saleItems);
        db.SaveChanges();

        var salePayment = new SalePayment
        {
            TenantId = tenantId,
            SaleId = sale.Id,
            Reference = "PAY-0001",
            ReceivedAmount = 1500m,
            PayingAmount = 1449.98m,
            PaymentType = "Cash",
            Description = "Cash payment",
            PaymentDate = now.AddDays(-5),
            CreatedAt = now,
        };
        db.SalePayments.Add(salePayment);
        db.SaveChanges();

        // ─── SAMPLE EXPENSES ─────────────────────────────────────
        var expenses = new List<Expense>
        {
            new() { TenantId = tenantId, Reference = "EXP-0001", ExpenseName = "Monthly Store Rent", ExpenseCategoryId = expenseCategories[0].Id, Description = "Main store rent for current month", Date = now.AddDays(-1), Amount = 150000m, Status = "active", CreatedOn = now },
            new() { TenantId = tenantId, Reference = "EXP-0002", ExpenseName = "Electricity Bill", ExpenseCategoryId = expenseCategories[1].Id, Description = "Electricity bill - Main Store", Date = now.AddDays(-3), Amount = 45000m, Status = "active", CreatedOn = now },
            new() { TenantId = tenantId, Reference = "EXP-0003", ExpenseName = "Facebook Ads Campaign", ExpenseCategoryId = expenseCategories[3].Id, Description = "Social media advertising", Date = now.AddDays(-7), Amount = 25000m, Status = "active", CreatedOn = now },
        };
        db.Expenses.AddRange(expenses);
        db.SaveChanges();

        // ─── SAMPLE FINANCE INCOMES ──────────────────────────────
        var incomes = new List<FinanceIncome>
        {
            new() { TenantId = tenantId, Date = now.AddDays(-5), Reference = "FIN-0001", Store = "Main Store", IncomeCategoryId = incomeCategories[0].Id, Notes = "Daily POS sales", Amount = 85000m, Account = "Cash", CreatedOn = now },
            new() { TenantId = tenantId, Date = now.AddDays(-3), Reference = "FIN-0002", Store = "Branch 1 - Downtown", IncomeCategoryId = incomeCategories[0].Id, Notes = "Daily POS sales", Amount = 45000m, Account = "Cash", CreatedOn = now },
        };
        db.FinanceIncomes.AddRange(incomes);
        db.SaveChanges();

        // ─── MANUFACTURING: SAMPLE BOM ───────────────────────────
        // BOM: Assembled Smartphone uses the raw materials above
        var bom = new BillOfMaterials
        {
            TenantId = tenantId,
            Name = "Smartphone Assembly Kit",
            FinishedProductId = products[2].Id, // Samsung Galaxy A54 as the assembled product
            OutputQuantity = 1,
            LaborCost = 50m,
            OverheadCost = 20m,
            Notes = "Standard assembly for Galaxy A54 units",
            Status = "active",
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.BillOfMaterials.Add(bom);
        db.SaveChanges();

        var bomItems = new List<BomItem>
        {
            new() { TenantId = tenantId, BomId = bom.Id, RawMaterialId = products[16].Id, Quantity = 1, UnitCost = 120m }, // Screen Panel
            new() { TenantId = tenantId, BomId = bom.Id, RawMaterialId = products[17].Id, Quantity = 1, UnitCost = 25m },  // Battery
            new() { TenantId = tenantId, BomId = bom.Id, RawMaterialId = products[18].Id, Quantity = 1, UnitCost = 35m },  // Chassis
        };
        db.BomItems.AddRange(bomItems);
        db.SaveChanges();

        // ─── VARIANT ATTRIBUTES ──────────────────────────────────
        var variantAttributes = new List<VariantAttribute>
        {
            new() { TenantId = tenantId, Name = "Color", Values = "Black,White,Blue,Red,Gold", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Name = "Size", Values = "S,M,L,XL,XXL", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Name = "Storage", Values = "64GB,128GB,256GB,512GB,1TB", Status = "active", CreatedAt = now },
            new() { TenantId = tenantId, Name = "RAM", Values = "4GB,6GB,8GB,12GB,16GB", Status = "active", CreatedAt = now },
        };
        db.VariantAttributes.AddRange(variantAttributes);
        db.SaveChanges();

        // ─── STOCK ENTRIES ───────────────────────────────────────
        var stockEntries = new List<StockEntry>
        {
            new() { TenantId = tenantId, Warehouse = "Central Warehouse", Store = "Main Store", ProductId = products[0].Id, Person = "Admin", Quantity = 50, Date = now.AddDays(-30), CreatedAt = now },
            new() { TenantId = tenantId, Warehouse = "Central Warehouse", Store = "Main Store", ProductId = products[1].Id, Person = "Admin", Quantity = 35, Date = now.AddDays(-30), CreatedAt = now },
            new() { TenantId = tenantId, Store = "Branch 1 - Downtown", ProductId = products[2].Id, Person = "Admin", Quantity = 80, Date = now.AddDays(-25), CreatedAt = now },
            new() { TenantId = tenantId, Warehouse = "Central Warehouse", Store = "Main Store", ProductId = products[3].Id, Person = "Admin", Quantity = 20, Date = now.AddDays(-20), CreatedAt = now },
        };
        db.StockEntries.AddRange(stockEntries);
        db.SaveChanges();

        // ─── SAMPLE INVOICE ──────────────────────────────────────
        var invoice = new Invoice
        {
            TenantId = tenantId,
            InvoiceNo = "INV-0001",
            CustomerId = customers[1].Id,
            CustomerName = "Sara Ali",
            CustomerEmail = "sara.ali@email.com",
            CustomerPhone = "03211234567",
            CustomerAddress = "Apt 45, Gulberg III, Lahore",
            FromName = "Asyntex POS",
            FromAddress = "Office 201, Blue Area, Islamabad",
            FromEmail = "admin@reactpos.com",
            FromPhone = "05112345678",
            InvoiceFor = "HP Pavilion 15 Laptop",
            SubTotal = 749.99m,
            Discount = 0,
            DiscountPercent = 0,
            Tax = 0,
            TaxPercent = 0,
            TotalAmount = 749.99m,
            Paid = 0,
            AmountDue = 749.99m,
            Status = "Unpaid",
            Notes = "Payment due within 30 days",
            Terms = "Net 30",
            DueDate = now.AddDays(30),
            CreatedAt = now,
        };
        db.Invoices.Add(invoice);
        db.SaveChanges();

        var invoiceItems = new List<InvoiceItem>
        {
            new() { TenantId = tenantId, InvoiceId = invoice.Id, Description = "HP Pavilion 15 Laptop", Quantity = 1, Cost = 749.99m, Discount = 0, Total = 749.99m },
        };
        db.InvoiceItems.AddRange(invoiceItems);
        db.SaveChanges();

        // ─── SAMPLE QUOTATION ────────────────────────────────────
        var quotation = new Quotation
        {
            TenantId = tenantId,
            Reference = "QOT-0001",
            CustomerId = customers[2].Id,
            CustomerName = "Usman Malik",
            OrderTax = 0,
            Discount = 0,
            Shipping = 0,
            GrandTotal = 2599.97m,
            Status = "Sent",
            Description = "Bulk order quotation for office setup",
            QuotationDate = now.AddDays(-2),
            CreatedAt = now,
        };
        db.Quotations.Add(quotation);
        db.SaveChanges();

        var quotationItems = new List<QuotationItem>
        {
            new() { TenantId = tenantId, QuotationId = quotation.Id, ProductId = products[12].Id, ProductName = "Executive Office Chair", Quantity = 5, PurchasePrice = 200m, Discount = 0, TaxPercent = 0, TaxAmount = 0, UnitCost = 299.99m, TotalCost = 1499.95m },
            new() { TenantId = tenantId, QuotationId = quotation.Id, ProductId = products[13].Id, ProductName = "Standing Desk - Adjustable", Quantity = 2, PurchasePrice = 350m, Discount = 0, TaxPercent = 0, TaxAmount = 0, UnitCost = 499.99m, TotalCost = 999.98m },
        };
        db.QuotationItems.AddRange(quotationItems);
        db.SaveChanges();

        // ─── ADDITIONAL SALES ────────────────────────────────────
        var sale2 = new Sale
        {
            TenantId = tenantId,
            Reference = "SAL-0002",
            CustomerId = customers[1].Id,
            CustomerName = "Sara Ali",
            Biller = "Admin",
            GrandTotal = 749.99m,
            Paid = 400m,
            Due = 349.99m,
            OrderTax = 0,
            Discount = 0,
            Shipping = 0,
            Status = "Completed",
            PaymentStatus = "Partial",
            Notes = "Partial payment - remaining on delivery",
            Source = "online",
            SaleDate = now.AddDays(-3),
            CreatedAt = now,
        };
        var sale3 = new Sale
        {
            TenantId = tenantId,
            Reference = "SAL-0003",
            CustomerId = customers[2].Id,
            CustomerName = "Usman Malik",
            Biller = "Admin",
            GrandTotal = 2179.97m,
            Paid = 0,
            Due = 2179.97m,
            OrderTax = 0,
            Discount = 50m,
            Shipping = 200m,
            Status = "Pending",
            PaymentStatus = "Unpaid",
            Notes = "Bulk order for office",
            Source = "online",
            SaleDate = now.AddDays(-1),
            CreatedAt = now,
        };
        var sale4 = new Sale
        {
            TenantId = tenantId,
            Reference = "SAL-0004",
            CustomerId = customers[3].Id,
            CustomerName = "Fatima Noor",
            Biller = "Admin",
            GrandTotal = 179.99m,
            Paid = 179.99m,
            Due = 0,
            OrderTax = 0,
            Discount = 0,
            Shipping = 0,
            Status = "Completed",
            PaymentStatus = "Paid",
            Source = "offline",
            SaleDate = now.AddDays(-10),
            CreatedAt = now,
        };
        db.Sales.AddRange(sale2, sale3, sale4);
        db.SaveChanges();

        db.SaleItems.AddRange(
            new SaleItem { TenantId = tenantId, SaleId = sale2.Id, ProductId = products[3].Id, ProductName = "HP Pavilion 15", Quantity = 1, PurchasePrice = 500m, Discount = 0, TaxPercent = 0, TaxAmount = 0, UnitCost = 749.99m, TotalCost = 749.99m },
            new SaleItem { TenantId = tenantId, SaleId = sale3.Id, ProductId = products[0].Id, ProductName = "Samsung Galaxy S24", Quantity = 2, PurchasePrice = 800m, Discount = 0, TaxPercent = 0, TaxAmount = 0, UnitCost = 999.99m, TotalCost = 1999.98m },
            new SaleItem { TenantId = tenantId, SaleId = sale3.Id, ProductId = products[14].Id, ProductName = "USB-C Fast Charger 65W", Quantity = 2, PurchasePrice = 15m, Discount = 0, TaxPercent = 0, TaxAmount = 0, UnitCost = 29.99m, TotalCost = 59.98m },
            new SaleItem { TenantId = tenantId, SaleId = sale4.Id, ProductId = products[10].Id, ProductName = "Adidas Ultraboost 22", Quantity = 1, PurchasePrice = 120m, Discount = 0, TaxPercent = 0, TaxAmount = 0, UnitCost = 179.99m, TotalCost = 179.99m }
        );
        db.SaveChanges();

        db.SalePayments.AddRange(
            new SalePayment { TenantId = tenantId, SaleId = sale2.Id, Reference = "PAY-0002", ReceivedAmount = 400m, PayingAmount = 400m, PaymentType = "Bank Transfer", Description = "Partial bank transfer", PaymentDate = now.AddDays(-3), CreatedAt = now },
            new SalePayment { TenantId = tenantId, SaleId = sale4.Id, Reference = "PAY-0003", ReceivedAmount = 200m, PayingAmount = 179.99m, PaymentType = "Cash", Description = "Cash payment", PaymentDate = now.AddDays(-10), CreatedAt = now }
        );
        db.SaveChanges();

        // ─── ADDITIONAL PURCHASES ────────────────────────────────
        var purchase2 = new Purchase
        {
            TenantId = tenantId,
            SupplierName = "Global Electronics Supply",
            SupplierRef = "SU002",
            Reference = "PUR-0002",
            Date = now.AddDays(-10),
            Status = "Received",
            PaymentStatus = "Partial",
            OrderTax = 0,
            Discount = 200m,
            Shipping = 1000m,
            Total = 18800m,
            Paid = 10000m,
            Notes = "Electronics restock order",
            CreatedAt = now,
        };
        var purchase3 = new Purchase
        {
            TenantId = tenantId,
            SupplierName = "FurnishCo Trading",
            SupplierRef = "SU003",
            Reference = "PUR-0003",
            Date = now.AddDays(-5),
            Status = "Pending",
            PaymentStatus = "Unpaid",
            OrderTax = 0,
            Discount = 0,
            Shipping = 2000m,
            Total = 9999.75m,
            Paid = 0,
            Notes = "Furniture restock for Main Store",
            CreatedAt = now,
        };
        db.Purchases.AddRange(purchase2, purchase3);
        db.SaveChanges();

        db.PurchaseItems.AddRange(
            new PurchaseItem { TenantId = tenantId, PurchaseId = purchase2.Id, ProductId = products[6].Id, ProductName = "Samsung 55\" 4K Smart TV", Quantity = 10, PurchasePrice = 400m, Discount = 0, TaxPercentage = 0, TaxAmount = 0, UnitCost = 400m, TotalCost = 4000m, CreatedAt = now },
            new PurchaseItem { TenantId = tenantId, PurchaseId = purchase2.Id, ProductId = products[8].Id, ProductName = "Sony WH-1000XM5 Headphones", Quantity = 30, PurchasePrice = 250m, Discount = 0, TaxPercentage = 0, TaxAmount = 0, UnitCost = 250m, TotalCost = 7500m, CreatedAt = now },
            new PurchaseItem { TenantId = tenantId, PurchaseId = purchase3.Id, ProductId = products[12].Id, ProductName = "Executive Office Chair", Quantity = 20, PurchasePrice = 200m, Discount = 0, TaxPercentage = 0, TaxAmount = 0, UnitCost = 200m, TotalCost = 4000m, CreatedAt = now },
            new PurchaseItem { TenantId = tenantId, PurchaseId = purchase3.Id, ProductId = products[13].Id, ProductName = "Standing Desk - Adjustable", Quantity = 10, PurchasePrice = 350m, Discount = 0, TaxPercentage = 0, TaxAmount = 0, UnitCost = 350m, TotalCost = 3500m, CreatedAt = now }
        );
        db.SaveChanges();

        // ─── SALES RETURNS ───────────────────────────────────────
        var salesReturn = new SalesReturn
        {
            TenantId = tenantId,
            Reference = "SR-0001",
            CustomerId = customers[0].Id,
            CustomerName = "Ahmed Khan",
            ProductId = products[8].Id,
            ProductName = "Sony WH-1000XM5 Headphones",
            OrderTax = 0,
            Discount = 0,
            Shipping = 0,
            GrandTotal = 349.99m,
            Paid = 349.99m,
            Due = 0,
            Status = "Completed",
            PaymentStatus = "Paid",
            ReturnDate = now.AddDays(-2),
            CreatedAt = now,
        };
        db.SalesReturns.Add(salesReturn);
        db.SaveChanges();

        db.SalesReturnItems.AddRange(
            new SalesReturnItem { TenantId = tenantId, SalesReturnId = salesReturn.Id, ProductName = "Sony WH-1000XM5 Headphones", NetUnitPrice = 349.99m, Stock = 40, Quantity = 1, Discount = 0, TaxPercent = 0, Subtotal = 349.99m }
        );
        db.SaveChanges();

        // ─── STOCK ADJUSTMENTS ───────────────────────────────────
        var stockAdjustments = new List<StockAdjustment>
        {
            new() { TenantId = tenantId, Warehouse = "Central Warehouse", Store = "Main Store", ProductId = products[0].Id, ReferenceNumber = "ADJ-0001", Person = "Admin", Quantity = -2, Notes = "Damaged units removed from stock", Date = now.AddDays(-7), CreatedAt = now },
            new() { TenantId = tenantId, Warehouse = "Central Warehouse", Store = "Main Store", ProductId = products[15].Id, ReferenceNumber = "ADJ-0002", Person = "Admin", Quantity = 10, Notes = "Stock count correction - found extra units", Date = now.AddDays(-3), CreatedAt = now },
            new() { TenantId = tenantId, Warehouse = "Secondary Warehouse", Store = "Main Store", ProductId = products[13].Id, ReferenceNumber = "ADJ-0003", Person = "Admin", Quantity = -1, Notes = "Display unit moved to showroom", Date = now.AddDays(-1), CreatedAt = now },
        };
        db.StockAdjustments.AddRange(stockAdjustments);
        db.SaveChanges();

        // ─── STOCK TRANSFERS ─────────────────────────────────────
        var stockTransfer = new StockTransfer
        {
            TenantId = tenantId,
            WarehouseFrom = "Central Warehouse",
            WarehouseTo = "Secondary Warehouse",
            ReferenceNumber = "TRF-0001",
            Notes = "Move excess phone stock to secondary warehouse",
            Date = now.AddDays(-5),
            CreatedAt = now,
        };
        db.StockTransfers.Add(stockTransfer);
        db.SaveChanges();

        db.StockTransferItems.AddRange(
            new StockTransferItem { TenantId = tenantId, StockTransferId = stockTransfer.Id, ProductId = products[0].Id, Quantity = 10 },
            new StockTransferItem { TenantId = tenantId, StockTransferId = stockTransfer.Id, ProductId = products[1].Id, Quantity = 5 }
        );
        db.SaveChanges();

        // ─── ATTENDANCE RECORDS ──────────────────────────────────
        // Seed last 7 days of attendance for employees
        var attendanceRecords = new List<Attendance>();
        for (int day = 7; day >= 1; day--)
        {
            var attendanceDate = now.AddDays(-day).Date;
            if (attendanceDate.DayOfWeek == DayOfWeek.Sunday) continue; // Skip Sundays

            // employees[0] = Ali Hassan - always present
            attendanceRecords.Add(new Attendance { TenantId = tenantId, EmployeeId = employees[0].Id, Date = attendanceDate, Status = "Present", ClockIn = new TimeSpan(9, 0, 0), ClockOut = new TimeSpan(17, 0, 0), TotalHours = "8:00", CreatedAt = now });
            // employees[1] = Zainab Fatima - present
            attendanceRecords.Add(new Attendance { TenantId = tenantId, EmployeeId = employees[1].Id, Date = attendanceDate, Status = "Present", ClockIn = new TimeSpan(9, 15, 0), ClockOut = new TimeSpan(17, 30, 0), TotalHours = "8:15", CreatedAt = now });
            // employees[2] = Hamza Siddiqui - mix present/absent
            attendanceRecords.Add(new Attendance { TenantId = tenantId, EmployeeId = employees[2].Id, Date = attendanceDate, Status = day % 3 == 0 ? "Absent" : "Present", ClockIn = day % 3 == 0 ? null : new TimeSpan(8, 45, 0), ClockOut = day % 3 == 0 ? null : new TimeSpan(17, 0, 0), TotalHours = day % 3 == 0 ? null : "8:15", CreatedAt = now });
            // employees[3] = Ayesha Bukhari - present
            attendanceRecords.Add(new Attendance { TenantId = tenantId, EmployeeId = employees[3].Id, Date = attendanceDate, Status = "Present", ClockIn = new TimeSpan(9, 0, 0), ClockOut = new TimeSpan(17, 0, 0), TotalHours = "8:00", CreatedAt = now });
        }
        db.Attendances.AddRange(attendanceRecords);
        db.SaveChanges();

        // ─── LEAVE RECORDS ───────────────────────────────────────
        var leaves = new List<Leave>
        {
            new() { TenantId = tenantId, EmployeeId = employees[2].Id, LeaveTypeId = leaveTypes[1].Id, FromDate = now.AddDays(-6), ToDate = now.AddDays(-6), Days = 1, DayType = "Full Day", Reason = "Not feeling well", Status = "Approved", ApprovedById = employees[0].Id, CreatedAt = now },
            new() { TenantId = tenantId, EmployeeId = employees[0].Id, LeaveTypeId = leaveTypes[0].Id, FromDate = now.AddDays(5), ToDate = now.AddDays(7), Days = 3, DayType = "Full Day", Reason = "Family vacation trip", Status = "Approved", ApprovedById = employees[0].Id, CreatedAt = now },
            new() { TenantId = tenantId, EmployeeId = employees[1].Id, LeaveTypeId = leaveTypes[2].Id, FromDate = now.AddDays(2), ToDate = now.AddDays(2), Days = 1, DayType = "Full Day", Reason = "Personal errand", Status = "New", CreatedAt = now },
            new() { TenantId = tenantId, EmployeeId = employees[3].Id, LeaveTypeId = leaveTypes[1].Id, FromDate = now.AddDays(-20), ToDate = now.AddDays(-19), Days = 2, DayType = "Full Day", Reason = "Medical appointment", Status = "Approved", ApprovedById = employees[0].Id, CreatedAt = now },
        };
        db.Leaves.AddRange(leaves);
        db.SaveChanges();

        // ─── PAYROLL RECORDS ─────────────────────────────────────
        var payrolls = new List<Payroll>
        {
            new() { TenantId = tenantId, EmployeeId = employees[0].Id, BasicSalary = 75000m, HRA = 15000m, Conveyance = 5000m, MedicalAllowance = 3000m, Bonus = 0, OtherAllowance = 0, PF = 2000m, ProfessionalTax = 500m, TDS = 0, LoanDeduction = 0, OtherDeduction = 0, TotalAllowance = 23000m, TotalDeduction = 2500m, NetSalary = 95500m, Status = "Paid", Month = now.Month, Year = now.Year, CreatedAt = now },
            new() { TenantId = tenantId, EmployeeId = employees[1].Id, BasicSalary = 45000m, HRA = 9000m, Conveyance = 3000m, MedicalAllowance = 2000m, Bonus = 0, OtherAllowance = 0, PF = 1200m, ProfessionalTax = 200m, TDS = 0, LoanDeduction = 0, OtherDeduction = 0, TotalAllowance = 14000m, TotalDeduction = 1400m, NetSalary = 57600m, Status = "Paid", Month = now.Month, Year = now.Year, CreatedAt = now },
            new() { TenantId = tenantId, EmployeeId = employees[2].Id, BasicSalary = 55000m, HRA = 11000m, Conveyance = 4000m, MedicalAllowance = 2500m, Bonus = 0, OtherAllowance = 2000m, PF = 1500m, ProfessionalTax = 300m, TDS = 0, LoanDeduction = 0, OtherDeduction = 0, TotalAllowance = 19500m, TotalDeduction = 1800m, NetSalary = 72700m, Status = "Unpaid", Month = now.Month, Year = now.Year, CreatedAt = now },
            new() { TenantId = tenantId, EmployeeId = employees[3].Id, BasicSalary = 65000m, HRA = 13000m, Conveyance = 5000m, MedicalAllowance = 3000m, Bonus = 5000m, OtherAllowance = 0, PF = 1800m, ProfessionalTax = 400m, TDS = 0, LoanDeduction = 0, OtherDeduction = 0, TotalAllowance = 26000m, TotalDeduction = 2200m, NetSalary = 88800m, Status = "Paid", Month = now.Month, Year = now.Year, CreatedAt = now },
        };
        db.Payrolls.AddRange(payrolls);
        db.SaveChanges();

        // ─── MANUFACTURING ORDERS ────────────────────────────────
        // Order 1: Completed - assembled 5 Galaxy A54 units
        var mfgOrder1 = new ManufacturingOrder
        {
            TenantId = tenantId,
            Reference = "MFG-0001",
            BomId = bom.Id,
            FinishedProductId = products[2].Id,
            Quantity = 5,
            TargetStoreId = stores[0].Id,
            Status = "Completed",
            LaborCost = 250m,
            OverheadCost = 100m,
            TotalMaterialCost = 900m,
            TotalCost = 1250m,
            StartDate = now.AddDays(-12),
            CompletionDate = now.AddDays(-10),
            Notes = "First batch – completed successfully",
            CreatedAt = now,
            UpdatedAt = now,
        };
        // Order 2: InProgress - assembling 10 units
        var mfgOrder2 = new ManufacturingOrder
        {
            TenantId = tenantId,
            Reference = "MFG-0002",
            BomId = bom.Id,
            FinishedProductId = products[2].Id,
            Quantity = 10,
            TargetStoreId = stores[1].Id,
            Status = "InProgress",
            LaborCost = 500m,
            OverheadCost = 200m,
            TotalMaterialCost = 1800m,
            TotalCost = 2500m,
            StartDate = now.AddDays(-2),
            Notes = "Second batch – in assembly line",
            CreatedAt = now,
            UpdatedAt = now,
        };
        // Order 3: Draft
        var mfgOrder3 = new ManufacturingOrder
        {
            TenantId = tenantId,
            Reference = "MFG-0003",
            BomId = bom.Id,
            FinishedProductId = products[2].Id,
            Quantity = 20,
            TargetStoreId = stores[0].Id,
            Status = "Draft",
            LaborCost = 1000m,
            OverheadCost = 400m,
            TotalMaterialCost = 3600m,
            TotalCost = 5000m,
            Notes = "Large batch – pending approval",
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.ManufacturingOrders.AddRange(mfgOrder1, mfgOrder2, mfgOrder3);
        db.SaveChanges();

        // Order items (raw materials for each order)
        db.ManufacturingOrderItems.AddRange(
            // MFG-0001 items (qty 5)
            new ManufacturingOrderItem { TenantId = tenantId, ManufacturingOrderId = mfgOrder1.Id, RawMaterialId = products[16].Id, RequiredQuantity = 5, ConsumedQuantity = 5, UnitCost = 120m, TotalCost = 600m, SupplierId = suppliers[0].Id },
            new ManufacturingOrderItem { TenantId = tenantId, ManufacturingOrderId = mfgOrder1.Id, RawMaterialId = products[17].Id, RequiredQuantity = 5, ConsumedQuantity = 5, UnitCost = 25m, TotalCost = 125m, SupplierId = suppliers[0].Id },
            new ManufacturingOrderItem { TenantId = tenantId, ManufacturingOrderId = mfgOrder1.Id, RawMaterialId = products[18].Id, RequiredQuantity = 5, ConsumedQuantity = 5, UnitCost = 35m, TotalCost = 175m, SupplierId = suppliers[0].Id },
            // MFG-0002 items (qty 10)
            new ManufacturingOrderItem { TenantId = tenantId, ManufacturingOrderId = mfgOrder2.Id, RawMaterialId = products[16].Id, RequiredQuantity = 10, ConsumedQuantity = 0, UnitCost = 120m, TotalCost = 1200m, SupplierId = suppliers[0].Id },
            new ManufacturingOrderItem { TenantId = tenantId, ManufacturingOrderId = mfgOrder2.Id, RawMaterialId = products[17].Id, RequiredQuantity = 10, ConsumedQuantity = 0, UnitCost = 25m, TotalCost = 250m, SupplierId = suppliers[0].Id },
            new ManufacturingOrderItem { TenantId = tenantId, ManufacturingOrderId = mfgOrder2.Id, RawMaterialId = products[18].Id, RequiredQuantity = 10, ConsumedQuantity = 0, UnitCost = 35m, TotalCost = 350m, SupplierId = suppliers[0].Id },
            // MFG-0003 items (qty 20)
            new ManufacturingOrderItem { TenantId = tenantId, ManufacturingOrderId = mfgOrder3.Id, RawMaterialId = products[16].Id, RequiredQuantity = 20, ConsumedQuantity = 0, UnitCost = 120m, TotalCost = 2400m, SupplierId = suppliers[1].Id },
            new ManufacturingOrderItem { TenantId = tenantId, ManufacturingOrderId = mfgOrder3.Id, RawMaterialId = products[17].Id, RequiredQuantity = 20, ConsumedQuantity = 0, UnitCost = 25m, TotalCost = 500m, SupplierId = suppliers[1].Id },
            new ManufacturingOrderItem { TenantId = tenantId, ManufacturingOrderId = mfgOrder3.Id, RawMaterialId = products[18].Id, RequiredQuantity = 20, ConsumedQuantity = 0, UnitCost = 35m, TotalCost = 700m, SupplierId = suppliers[1].Id }
        );
        db.SaveChanges();

        // ─── SUPPLIER LEDGER ENTRIES ─────────────────────────────
        var ledgerEntries = new List<SupplierLedgerEntry>
        {
            // TechParts: MFG-0001 completed → credit for raw material costs
            new() { TenantId = tenantId, SupplierId = suppliers[0].Id, TransactionType = "Credit", ReferenceType = "ManufacturingOrder", ReferenceId = mfgOrder1.Id, Amount = 900m, RunningBalance = 900m, Description = "Raw materials for MFG-0001 (5 units)", Date = now.AddDays(-10), CreatedAt = now },
            // TechParts: payment made
            new() { TenantId = tenantId, SupplierId = suppliers[0].Id, TransactionType = "Debit", ReferenceType = "Payment", ReferenceId = null, Amount = 500m, RunningBalance = 400m, Description = "Partial payment against MFG-0001", Date = now.AddDays(-8), CreatedAt = now },
            // TechParts: MFG-0002 started → credit
            new() { TenantId = tenantId, SupplierId = suppliers[0].Id, TransactionType = "Credit", ReferenceType = "ManufacturingOrder", ReferenceId = mfgOrder2.Id, Amount = 1800m, RunningBalance = 2200m, Description = "Raw materials for MFG-0002 (10 units)", Date = now.AddDays(-2), CreatedAt = now },
            // Global Electronics: MFG-0003 draft → credit
            new() { TenantId = tenantId, SupplierId = suppliers[1].Id, TransactionType = "Credit", ReferenceType = "ManufacturingOrder", ReferenceId = mfgOrder3.Id, Amount = 3600m, RunningBalance = 3600m, Description = "Raw materials for MFG-0003 (20 units)", Date = now, CreatedAt = now },
        };
        db.SupplierLedgerEntries.AddRange(ledgerEntries);
        db.SaveChanges();

        // ─── SUPPLIER PAYMENTS ───────────────────────────────────
        var supplierPayments = new List<SupplierPayment>
        {
            new() { TenantId = tenantId, SupplierId = suppliers[0].Id, Reference = "SPAY-0001", Amount = 500m, PaymentMethod = "Bank Transfer", Description = "Partial payment for MFG-0001 raw materials", PaymentDate = now.AddDays(-8), CreatedAt = now },
            new() { TenantId = tenantId, SupplierId = suppliers[0].Id, Reference = "SPAY-0002", Amount = 400m, PaymentMethod = "Cash", Description = "Remaining balance for MFG-0001", PaymentDate = now.AddDays(-5), CreatedAt = now },
        };
        db.SupplierPayments.AddRange(supplierPayments);
        db.SaveChanges();

        // ─── ADDITIONAL EXPENSES ─────────────────────────────────
        db.Expenses.AddRange(
            new Expense { TenantId = tenantId, Reference = "EXP-0004", ExpenseName = "Water Bill", ExpenseCategoryId = expenseCategories[1].Id, Description = "Water bill - Main Store", Date = now.AddDays(-5), Amount = 5000m, Status = "active", CreatedOn = now },
            new Expense { TenantId = tenantId, Reference = "EXP-0005", ExpenseName = "AC Maintenance", ExpenseCategoryId = expenseCategories[4].Id, Description = "Annual AC servicing for all stores", Date = now.AddDays(-2), Amount = 35000m, Status = "active", CreatedOn = now },
            new Expense { TenantId = tenantId, Reference = "EXP-0006", ExpenseName = "Delivery Fuel", ExpenseCategoryId = expenseCategories[5].Id, Description = "Fuel for delivery van", Date = now.AddDays(-1), Amount = 8000m, Status = "active", CreatedOn = now }
        );
        db.SaveChanges();

        // ─── ADDITIONAL FINANCE INCOMES ──────────────────────────
        db.FinanceIncomes.AddRange(
            new FinanceIncome { TenantId = tenantId, Date = now.AddDays(-1), Reference = "FIN-0003", Store = "Branch 2 - Mall Outlet", IncomeCategoryId = incomeCategories[0].Id, Notes = "Weekend sales spike", Amount = 125000m, Account = "Meezan Bank", CreatedOn = now },
            new FinanceIncome { TenantId = tenantId, Date = now, Reference = "FIN-0004", Store = "Main Store", IncomeCategoryId = incomeCategories[1].Id, Notes = "Phone repair service", Amount = 15000m, Account = "Cash", CreatedOn = now }
        );
        db.SaveChanges();

        // ─── INSTALLMENT PLAN ────────────────────────────────────
        var installmentPlan = new InstallmentPlan
        {
            TenantId = tenantId,
            CustomerId = customers[4].Id,
            ProductId = products[5].Id, // Apple MacBook Air M2
            ProductPrice = 1299.99m,
            FinanceAmount = 1299.99m,
            DownPayment = 300m,
            FinancedAmount = 999.99m,
            InterestRate = 5m,
            Tenure = 6,
            EmiAmount = 171.67m,
            TotalPayable = 1299.99m + 50m, // principal + interest
            TotalInterest = 50m,
            StartDate = now.AddDays(-30).ToString("yyyy-MM-dd"),
            Status = "active",
            PaidInstallments = 1,
            RemainingInstallments = 5,
            NextDueDate = now.AddDays(0).ToString("yyyy-MM-dd"),
            CreatedAt = now,
        };
        db.InstallmentPlans.Add(installmentPlan);
        db.SaveChanges();

        // Repayment schedule
        var repayments = new List<RepaymentEntry>();
        for (int i = 1; i <= 6; i++)
        {
            var dueDate = now.AddDays(-30).AddMonths(i);
            repayments.Add(new RepaymentEntry
            {
                TenantId = tenantId,
                PlanId = installmentPlan.Id,
                InstallmentNo = i,
                DueDate = dueDate.ToString("yyyy-MM-dd"),
                EmiAmount = 171.67m,
                Principal = 166.67m,
                Interest = 5m,
                Balance = 999.99m - (166.67m * i),
                Status = i == 1 ? "paid" : "upcoming",
                PaidDate = i == 1 ? now.AddDays(-2).ToString("yyyy-MM-dd") : null,
                ActualPaidAmount = i == 1 ? 171.67m : null,
            });
        }
        db.RepaymentEntries.AddRange(repayments);
        db.SaveChanges();

        // Guarantor
        db.PlanGuarantors.Add(new PlanGuarantor
        {
            TenantId = tenantId,
            PlanId = installmentPlan.Id,
            PartyId = customers[0].Id, // Ahmed Khan as guarantor
            Relationship = "Friend",
            CreatedAt = now,
        });
        db.SaveChanges();

        // ─── MISCELLANEOUS REGISTER ──────────────────────────────
        db.MiscellaneousRegisters.AddRange(
            new MiscellaneousRegister { TenantId = tenantId, CustomerId = customers[0].Id, TransactionType = "Advance", Amount = 5000m, Description = "Advance payment for future order", ReferenceId = "MISC-0001", ReferenceType = "Advance", CreatedAt = now, CreatedBy = "Admin" },
            new MiscellaneousRegister { TenantId = tenantId, CustomerId = customers[1].Id, TransactionType = "Discount Adjustment", Amount = 200m, Description = "Loyalty discount adjustment", ReferenceId = "MISC-0002", ReferenceType = "Adjustment", CreatedAt = now, CreatedBy = "Admin" }
        );
        db.SaveChanges();

        // ─── ROLE PERMISSIONS ────────────────────────────────────
        var managerPerms = new[] { "dashboard", "products", "sales", "purchases", "customers", "reports", "expenses", "invoices", "quotations", "stock-entries", "stock-adjustments", "coupons", "manufacturing" };
        var billerPerms = new[] { "dashboard", "sales", "customers", "invoices", "coupons" };

        foreach (var perm in managerPerms)
            db.RolePermissions.Add(new RolePermission { TenantId = tenantId, Role = "Manager", MenuKey = perm });
        foreach (var perm in billerPerms)
            db.RolePermissions.Add(new RolePermission { TenantId = tenantId, Role = "Biller", MenuKey = perm });
        db.SaveChanges();

        Console.WriteLine($"[Seed] Demo data seeded for tenant {tenantId}: " +
            $"{products.Count} products, {customers.Count} customers, {suppliers.Count} suppliers, " +
            $"{employees.Count} employees/users, {categories.Count} categories, " +
            $"{subCategories.Count} sub-categories, {departments.Count} departments, " +
            $"3 manufacturing orders, 4 supplier ledger entries, 1 installment plan, " +
            $"4 sales, 3 purchases, 1 sales return, 3 stock adjustments, " +
            $"1 stock transfer, {attendanceRecords.Count} attendance records, " +
            $"{leaves.Count} leaves, {payrolls.Count} payrolls, 1 BOM, 1 invoice, 1 quotation");
    }
}
