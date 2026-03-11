namespace GcaEditor.Data;

public sealed class CarCatalog
{
    public int schema { get; set; } = 1;
    public string root { get; set; } = "Assets/Cars";
    public Dictionary<string, string> mib_folders { get; set; } = new();
    public CarBackground background { get; set; } = new();
    public CarFeature feature { get; set; } = new();
    public List<CarEntry> cars { get; set; } = new();
}

public sealed class CarBackground
{
    public string lhd { get; set; } = "Interior_LHD.png";
    public string rhd { get; set; } = "Interior_RHD.png";
}

public sealed class CarFeature
{
    public string lhd_pattern { get; set; } = "Feature_LHD_{index}.png";
    public string rhd_pattern { get; set; } = "Feature_RHD_{index}.png";
    public int count { get; set; } = 23;
}

public sealed class CarEntry
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public List<string> available_mibs { get; set; } = new();

    public override string ToString()
    {
        return id + " - " + name;
    }
}
