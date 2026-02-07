using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;

namespace WindowingDemo;

/// <summary>
/// Animated pets that walk across the bottom of the background.
/// Rendered using braille characters for smooth, high-resolution animation.
/// Inspired by VS Code Pets extension.
/// </summary>
public static class BackgroundPets
{
    // Braille: each cell is 2 dots wide x 4 dots tall
    private const int DotsPerCellX = 2;
    private const int DotsPerCellY = 4;
    
    private static List<Pet>? _pets;
    private static bool _initialized;
    private static int _width;
    private static int _height;
    
    // Pet sprites defined as dot patterns (1 = dot, 0 = empty)
    // Each frame is a 2D array of dots that gets converted to braille
    
    // Cat: 10 dots wide x 8 dots tall = 5 cells x 2 cells
    private static readonly PetDefinition Cat = new(
        Name: "Cat",
        Color: Hex1bColor.FromRgb(255, 180, 100),
        Frames: [
            // Frame 0: Sitting/walking
            new byte[,] {
                {0,0,1,0,0,0,1,0,0,0},  // ears
                {0,1,1,1,0,1,1,1,0,0},  // head top
                {0,1,0,1,1,1,0,1,0,0},  // eyes
                {0,0,1,1,1,1,1,0,0,0},  // face
                {0,0,0,1,1,1,0,0,0,0},  // neck
                {0,0,1,1,1,1,1,0,0,0},  // body
                {0,0,1,0,0,0,1,0,0,0},  // legs
                {0,0,1,0,0,0,1,0,0,0},  // feet
            },
            // Frame 1: Walking
            new byte[,] {
                {0,0,1,0,0,0,1,0,0,0},  // ears
                {0,1,1,1,0,1,1,1,0,0},  // head top
                {0,1,0,1,1,1,0,1,0,0},  // eyes
                {0,0,1,1,1,1,1,0,0,0},  // face
                {0,0,0,1,1,1,0,0,0,0},  // neck
                {0,0,1,1,1,1,1,0,0,0},  // body
                {0,1,0,0,0,0,0,1,0,0},  // legs spread
                {0,1,0,0,0,0,0,1,0,0},  // feet
            }
        ],
        Speed: 0.08
    );
    
    // Dog: 12 dots wide x 8 dots tall = 6 cells x 2 cells
    private static readonly PetDefinition Dog = new(
        Name: "Dog",
        Color: Hex1bColor.FromRgb(180, 130, 80),
        Frames: [
            // Frame 0
            new byte[,] {
                {0,0,1,1,0,0,0,0,0,0,0,0},  // ear
                {0,1,1,1,1,0,0,0,0,0,0,0},  // head
                {1,0,1,0,1,1,1,1,1,1,0,0},  // eye + body
                {0,1,1,1,1,1,1,1,1,1,1,0},  // body
                {0,0,1,1,1,1,1,1,1,0,0,0},  // body
                {0,0,1,0,0,1,0,0,1,0,0,0},  // legs
                {0,0,1,0,0,1,0,0,1,0,0,0},  // legs
                {0,0,1,0,0,1,0,0,1,0,0,0},  // feet + tail
            },
            // Frame 1
            new byte[,] {
                {0,0,1,1,0,0,0,0,0,0,0,0},  // ear
                {0,1,1,1,1,0,0,0,0,0,0,0},  // head
                {1,0,1,0,1,1,1,1,1,1,0,0},  // eye + body
                {0,1,1,1,1,1,1,1,1,1,1,0},  // body
                {0,0,1,1,1,1,1,1,1,0,0,0},  // body
                {0,1,0,0,1,0,0,0,0,1,0,0},  // legs spread
                {0,1,0,0,1,0,0,0,0,1,0,0},  // legs
                {0,1,0,0,1,0,0,0,0,1,0,0},  // feet
            }
        ],
        Speed: 0.12
    );
    
    // Crab: 10 dots wide x 6 dots tall = 5 cells x 1.5 cells
    private static readonly PetDefinition Crab = new(
        Name: "Crab",
        Color: Hex1bColor.FromRgb(255, 100, 80),
        Frames: [
            // Frame 0: Claws up
            new byte[,] {
                {1,0,0,0,0,0,0,0,0,1},  // claw tips
                {1,1,0,0,0,0,0,0,1,1},  // claws
                {0,1,1,1,1,1,1,1,1,0},  // body top
                {0,0,1,0,1,1,0,1,0,0},  // eyes
                {0,0,1,1,1,1,1,1,0,0},  // body
                {0,1,0,1,0,0,1,0,1,0},  // legs
            },
            // Frame 1: Claws down, legs shifted
            new byte[,] {
                {0,0,0,0,0,0,0,0,0,0},  // empty
                {1,1,0,0,0,0,0,0,1,1},  // claws down
                {0,1,1,1,1,1,1,1,1,0},  // body top
                {0,0,1,0,1,1,0,1,0,0},  // eyes
                {0,0,1,1,1,1,1,1,0,0},  // body
                {0,0,1,0,1,1,0,1,0,0},  // legs shifted
            }
        ],
        Speed: 0.06
    );
    
    private static readonly PetDefinition[] AllPets = [Cat, Dog, Crab];
    
    public static void Initialize(int width, int height, Random random)
    {
        if (_initialized && _width == width && _height == height)
            return;
            
        _width = width;
        _height = height;
        _pets = [];
        
        // Spawn 2-3 pets at random positions along the bottom
        int petCount = random.Next(2, 4);
        var usedTypes = new HashSet<int>();
        
        for (int i = 0; i < petCount; i++)
        {
            int typeIndex;
            do
            {
                typeIndex = random.Next(AllPets.Length);
            } while (usedTypes.Contains(typeIndex) && usedTypes.Count < AllPets.Length);
            
            usedTypes.Add(typeIndex);
            var def = AllPets[typeIndex];
            
            _pets.Add(new Pet
            {
                Definition = def,
                X = random.NextDouble() * width,
                Direction = random.Next(2) == 0 ? -1 : 1,
                Frame = 0,
                FrameTimer = 0,
                WalkTimer = 0,
                State = PetState.Walking
            });
        }
        
        _initialized = true;
    }
    
    public static void Update(Random random)
    {
        if (_pets == null) return;
        
        foreach (var pet in _pets)
        {
            // Update animation frame
            pet.FrameTimer += 0.1;
            if (pet.FrameTimer >= 0.4)
            {
                pet.FrameTimer = 0;
                pet.Frame = (pet.Frame + 1) % pet.Definition.Frames.Length;
            }
            
            // Update position based on state
            switch (pet.State)
            {
                case PetState.Walking:
                    pet.X += pet.Direction * pet.Definition.Speed;
                    pet.WalkTimer += 0.1;
                    
                    // Occasionally stop to idle
                    if (pet.WalkTimer > 10 + random.NextDouble() * 20)
                    {
                        pet.State = PetState.Idle;
                        pet.WalkTimer = 0;
                    }
                    
                    // Turn around at edges - calculate width in cells
                    var frame = pet.Definition.Frames[0];
                    int dotWidth = frame.GetLength(1);
                    int cellWidth = (dotWidth + DotsPerCellX - 1) / DotsPerCellX;
                    
                    if (pet.X < 0)
                    {
                        pet.X = 0;
                        pet.Direction = 1;
                    }
                    else if (pet.X + cellWidth > _width)
                    {
                        pet.X = _width - cellWidth;
                        pet.Direction = -1;
                    }
                    break;
                    
                case PetState.Idle:
                    pet.WalkTimer += 0.1;
                    if (pet.WalkTimer > 3 + random.NextDouble() * 5)
                    {
                        pet.State = PetState.Walking;
                        pet.WalkTimer = 0;
                        // Maybe change direction
                        if (random.NextDouble() < 0.3)
                        {
                            pet.Direction = -pet.Direction;
                        }
                    }
                    break;
            }
        }
    }
    
    public static void Render(Surface surface)
    {
        if (_pets == null) return;
        
        foreach (var pet in _pets)
        {
            var frame = pet.Definition.Frames[pet.Frame];
            int dotHeight = frame.GetLength(0);
            int dotWidth = frame.GetLength(1);
            
            // Calculate cell dimensions
            int cellWidth = (dotWidth + DotsPerCellX - 1) / DotsPerCellX;
            int cellHeight = (dotHeight + DotsPerCellY - 1) / DotsPerCellY;
            
            int baseX = (int)pet.X;
            int baseY = surface.Height - cellHeight;
            
            // Build braille characters for each cell
            for (int cellY = 0; cellY < cellHeight; cellY++)
            {
                for (int cellX = 0; cellX < cellWidth; cellX++)
                {
                    int bits = 0;
                    
                    // Sample the 2x4 dot grid for this cell
                    for (int dy = 0; dy < DotsPerCellY; dy++)
                    {
                        for (int dx = 0; dx < DotsPerCellX; dx++)
                        {
                            int dotX, dotY;
                            
                            if (pet.Direction < 0)
                            {
                                // Flip horizontally
                                dotX = dotWidth - 1 - (cellX * DotsPerCellX + dx);
                            }
                            else
                            {
                                dotX = cellX * DotsPerCellX + dx;
                            }
                            dotY = cellY * DotsPerCellY + dy;
                            
                            if (dotX >= 0 && dotX < dotWidth && dotY >= 0 && dotY < dotHeight)
                            {
                                if (frame[dotY, dotX] != 0)
                                {
                                    // Map to braille bit pattern
                                    bits |= dy switch
                                    {
                                        0 => dx == 0 ? 0x01 : 0x08,
                                        1 => dx == 0 ? 0x02 : 0x10,
                                        2 => dx == 0 ? 0x04 : 0x20,
                                        3 => dx == 0 ? 0x40 : 0x80,
                                        _ => 0
                                    };
                                }
                            }
                        }
                    }
                    
                    if (bits > 0)
                    {
                        int x = baseX + cellX;
                        int y = baseY + cellY;
                        
                        if (x >= 0 && x < surface.Width && y >= 0 && y < surface.Height)
                        {
                            var existing = surface[x, y];
                            var bg = existing.Background ?? Hex1bColor.FromRgb(8, 10, 25);
                            
                            char brailleChar = (char)(0x2800 + bits);
                            surface[x, y] = new SurfaceCell(
                                brailleChar.ToString(),
                                pet.Definition.Color,
                                bg
                            );
                        }
                    }
                }
            }
        }
    }
    
    private record PetDefinition(
        string Name,
        Hex1bColor Color,
        byte[][,] Frames,
        double Speed
    );
    
    private enum PetState
    {
        Walking,
        Idle
    }
    
    private class Pet
    {
        public required PetDefinition Definition { get; init; }
        public double X { get; set; }
        public int Direction { get; set; } // -1 = left, 1 = right
        public int Frame { get; set; }
        public double FrameTimer { get; set; }
        public double WalkTimer { get; set; }
        public PetState State { get; set; }
    }
}
