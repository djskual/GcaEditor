namespace GcaEditor.Models;

public sealed class GcaZone
{
    public ushort Id { get; set; }

    // Constantes vues dans le fichier (10 10 04)
    public ushort A { get; set; }
    public ushort B { get; set; }
    public ushort C { get; set; }

    // 4 points (rectangle/hitbox)
    public ushort X1 { get; set; }
    public ushort Y1 { get; set; }
    public ushort X2 { get; set; }
    public ushort Y2 { get; set; }
    public ushort X3 { get; set; }
    public ushort Y3 { get; set; }
    public ushort X4 { get; set; }
    public ushort Y4 { get; set; }

    // Centre (pratique pour poser le soleil)
    public double CenterX => (X1 + X2 + X3 + X4) / 4.0;
    public double CenterY => (Y1 + Y2 + Y3 + Y4) / 4.0;
}