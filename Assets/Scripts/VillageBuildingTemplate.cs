using System;

public readonly struct VillageBuildingCell
{
    public bool ReplacesBlock { get; }
    public BlockType Block { get; }

    public VillageBuildingCell(bool replacesBlock, BlockType block)
    {
        ReplacesBlock = replacesBlock;
        Block = block;
    }
}

public sealed class VillageBuildingTemplate
{
    private readonly VillageBuildingCell[,,] cells;

    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public int Depth { get; }

    public VillageBuildingTemplate(string name, VillageBuildingCell[,,] cells)
    {
        Name = name;
        this.cells = cells;
        Width = cells.GetLength(0);
        Height = cells.GetLength(1);
        Depth = cells.GetLength(2);
    }

    public VillageBuildingCell GetCell(int x, int y, int z)
    {
        return cells[x, y, z];
    }
}

public static class VillageBuildingImporter
{
    // slices[z][y][x]: z = 0 is the front, y = 0 is the floor.
    public static VillageBuildingTemplate Import(
        string buildingName,
        string[][] slices
    )
    {
        if (slices == null || slices.Length == 0)
        {
            throw new ArgumentException(
                "A building must contain at least one depth slice.",
                nameof(slices)
            );
        }

        if (slices[0] == null || slices[0].Length < 2)
        {
            throw new ArgumentException(
                "A building must contain a floor and at least one wall level.",
                nameof(slices)
            );
        }

        if (string.IsNullOrEmpty(slices[0][0]))
        {
            throw new ArgumentException(
                "Building rows cannot be empty.",
                nameof(slices)
            );
        }

        int depth = slices.Length;
        int height = slices[0].Length;
        int width = slices[0][0].Length;

        if (width % 2 == 0)
        {
            throw new ArgumentException(
                "Building width must be odd so the entrance has a center block.",
                nameof(slices)
            );
        }

        VillageBuildingCell[,,] importedCells =
            new VillageBuildingCell[width, height, depth];

        for (int z = 0; z < depth; z++)
        {
            if (slices[z] == null || slices[z].Length != height)
            {
                throw new ArgumentException(
                    "Every depth slice must have the same height.",
                    nameof(slices)
                );
            }

            for (int y = 0; y < height; y++)
            {
                string row = slices[z][y];

                if (row == null || row.Length != width)
                {
                    throw new ArgumentException(
                        "Every building row must have the same width.",
                        nameof(slices)
                    );
                }

                for (int x = 0; x < width; x++)
                {
                    importedCells[x, y, z] = ImportCell(row[x]);
                }
            }
        }

        return new VillageBuildingTemplate(buildingName, importedCells);
    }

    private static VillageBuildingCell ImportCell(char symbol)
    {
        switch (symbol)
        {
            case '.':
                return new VillageBuildingCell(false, BlockType.Air);
            case '#':
                return new VillageBuildingCell(true, BlockType.Air);
            case 'P':
                return new VillageBuildingCell(true, BlockType.WoodPlank);
            case 'S':
                return new VillageBuildingCell(true, BlockType.Stone);
            case 'B':
                return new VillageBuildingCell(true, BlockType.StoneBrick);
            case 'M':
                return new VillageBuildingCell(true, BlockType.MossyStoneBrick);
            case 'D':
                return new VillageBuildingCell(true, BlockType.Dirt);
            case 'G':
                return new VillageBuildingCell(true, BlockType.Grass);
            default:
                throw new ArgumentException(
                    "Unknown building block symbol: " + symbol
                );
        }
    }
}

