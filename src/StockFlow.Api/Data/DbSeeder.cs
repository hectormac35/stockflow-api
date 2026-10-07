using StockFlow.Api.Domain;

namespace StockFlow.Api.Data;

/// <summary>Carga datos de ejemplo para poder probar la API nada más arrancarla.</summary>
public static class DbSeeder
{
    public static void Seed(StockFlowDbContext db)
    {
        if (db.Products.Any()) return;

        db.Products.AddRange(
            new Product("LAP-001", "Portátil 14\" i5 16GB", 749.90m, 25, 5),
            new Product("MON-024", "Monitor 24\" Full HD", 139.00m, 40, 10),
            new Product("TEC-ES1", "Teclado mecánico ES", 59.95m, 8, 10),
            new Product("RAT-INA", "Ratón inalámbrico", 19.99m, 120, 20),
            new Product("DOC-USC", "Dock USB-C 8 en 1", 89.50m, 3, 5));

        db.Customers.AddRange(
            new Customer { Name = "Talleres Ebro S.L.", Email = "compras@talleresebro.example", TaxId = "B50000001" },
            new Customer { Name = "Distribuciones Moncayo S.A.", Email = "pedidos@moncayo.example", TaxId = "A50000002" });

        db.SaveChanges();
    }
}
