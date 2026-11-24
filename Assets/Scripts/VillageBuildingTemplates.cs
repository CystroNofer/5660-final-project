public static class VillageBuildingTemplates
{
    // Depth slices: slices[z][y][x]. Z = 0 is the front and Y = 0 is the floor.
    // #: air
    // .: leave the existing block unchanged
    // P: planks
    // S: stone
    // B: stone bricks
    // M: mossy stone bricks
    // D: dirt
    // G: grass
    public static readonly VillageBuildingTemplate PlankHouse =
        VillageBuildingImporter.Import(
            "Plank House",
            new string[][]
            {
                new string[]
                {
                    ".........",
                    ".#######.",
                    ".#######.",
                    ".#######.",
                    "P#######P",
                    ".P#####P.",
                    "..P###P..",
                    "...PPP..."
                },
                new string[]
                {
                    ".PPPPPPP.",
                    ".PPP#PPP.",
                    ".PPP#PPP.",
                    ".PPPPPPP.",
                    "PPPPPPPPP",
                    ".PPPPPPP.",
                    "..PPPPP..",
                    "...PPP..."
                },
                new string[]
                {
                    ".PPPPPPP.",
                    ".P#####P.",
                    ".P#####P.",
                    ".P#####P.",
                    "PP#####PP",
                    ".P#####P.",
                    "..P###P..",
                    "...PPP..."
                },
                new string[]
                {
                    ".PPPPPPP.",
                    ".P#####P.",
                    ".P#####P.",
                    ".P#####P.",
                    "PP#####PP",
                    ".P#####P.",
                    "..P###P..",
                    "...PPP..."
                },
                new string[]
                {
                    ".PPPPPPP.",
                    ".P#####P.",
                    ".P#####P.",
                    ".P#####P.",
                    "PP#####PP",
                    ".P#####P.",
                    "..P###P..",
                    "...PPP..."
                },
                new string[]
                {
                    ".PPPPPPP.",
                    ".P#####P.",
                    ".P#####P.",
                    ".P#####P.",
                    "PP#####PP",
                    ".P#####P.",
                    "..P###P..",
                    "...PPP..."
                },
                new string[]
                {
                    ".PPPPPPP.",
                    ".P#####P.",
                    ".P#####P.",
                    ".P#####P.",
                    "PP#####PP",
                    ".P#####P.",
                    "..P###P..",
                    "...PPP..."
                },
                new string[]
                {
                    ".PPPPPPP.",
                    ".P#####P.",
                    ".P#####P.",
                    ".P#####P.",
                    "PP#####PP",
                    ".P#####P.",
                    "..P###P..",
                    "...PPP..."
                },
                new string[]
                {
                    ".PPPPPPP.",
                    ".P#####P.",
                    ".P#####P.",
                    ".P#####P.",
                    "PP#####PP",
                    ".P#####P.",
                    "..P###P..",
                    "...PPP..."
                },
                new string[]
                {
                    ".PPPPPPP.",
                    ".PPPPPPP.",
                    ".PPPPPPP.",
                    ".PPPPPPP.",
                    "PPPPPPPPP",
                    ".PPPPPPP.",
                    "..PPPPP..",
                    "...PPP..."
                },
                new string[]
                {
                    ".........",
                    ".........",
                    ".........",
                    ".........",
                    "P.......P",
                    ".P.....P.",
                    "..P...P..",
                    "...PPP..."
                }
            }
        );
}

