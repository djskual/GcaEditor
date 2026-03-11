using System.IO;
using System.Text.Json;

namespace GcaEditor.Data;

public static class CarCatalogLoader
{
    public static string GetCarsRoot()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Cars");
    }

    public static CarCatalog LoadOrThrow()
    {
        string root = GetCarsRoot();
        string jsonPath = Path.Combine(root, "car.json");

        if (!File.Exists(jsonPath))
            throw new FileNotFoundException("car.json not found", jsonPath);

        var json = File.ReadAllText(jsonPath);
        var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var cat = JsonSerializer.Deserialize<CarCatalog>(json, opt);
        if (cat == null)
            throw new InvalidOperationException("Failed to parse car.json");

        return cat;
    }
}
