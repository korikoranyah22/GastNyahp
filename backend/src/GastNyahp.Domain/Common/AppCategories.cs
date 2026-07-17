namespace GastNyahp.Domain.Common;

/// <summary>Closed category lists ported from app/src/pages/expenses/expensesConfig.js (DOMAIN_MODEL.md §1.4).</summary>
public static class AppCategories
{
    public static readonly IReadOnlyList<string> ExpenseCategories =
    [
        "Comida", "Delivery", "Vicios", "Salidas", "Hogar", "Limpieza", "Salud", "Higiene",
        "Transporte", "Servicios", "Ropa", "Educación", "Electrónica", "Mascotas", "Perfumes", "Desconocido",
    ];

    public static readonly IReadOnlyList<string> ServiceCategories =
    [
        "Electricidad", "Gas", "Agua", "Conectividad", "Streaming", "Digital", "Seguro", "Expensas", "Telecom", "Otros",
    ];

    public static bool IsValidExpenseCategory(string category) => ExpenseCategories.Contains(category);
    public static bool IsValidServiceCategory(string category) => ServiceCategories.Contains(category);
}
