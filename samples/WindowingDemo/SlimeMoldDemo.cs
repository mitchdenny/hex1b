using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace WindowingDemo;

/// <summary>
/// Adversarial slime mold demo - multiple species compete for territory.
/// Stronger trails overwrite weaker ones. Dead species respawn after 10 seconds.
/// </summary>
public static class SlimeMoldDemo
{
    // Dimensions
    private const int DotsPerCellX = 2;
    private const int DotsPerCellY = 4;
    
    // Simulation parameters
    private const int AgentsPerSpecies = 125;
    private const int MaxAgentsPerSpecies = 300;
    private const int InitialSpeciesCount = 4;
    private const int MaxSpeciesCount = 12;             // Cap on total species including hybrids
    private const double SensorAngle = Math.PI / 4;
    private const double SensorDistance = 3.0;
    private const double TurnSpeed = Math.PI / 6;
    private const double MoveSpeed = 0.8;
    private const double TrailDeposit = 1.0;
    private const double TrailDecay = 0.95;
    private const double TrailDiffuse = 0.1;
    private const double EnemyAvoidance = 0.5;
    private const double DeathThreshold = 2.0; // Die if enemy trail this much stronger
    private const double RespawnDelaySeconds = 10.0;
    
    // Cohesion parameters - agents prefer to stay connected
    private const double CohesionRadius = 6.0;          // Radius to check for nearby peers
    private const double CohesionWeight = 1.5;          // How strongly agents are attracted to peers
    private const int MinPeersForComfort = 3;           // Below this, agent feels isolated
    
    // Crossbreeding parameters
    private const double InteractionRadius = 2.0;       // Distance for agents to interact
    private const double InteractionChance = 0.005;     // Base chance per frame for interaction
    private const double MaxColorDistance = 441.67;     // sqrt(255^2 * 3) - max RGB distance
    
    // Food parameters
    private const double FoodSpawnChance = 0.15;        // Chance per frame to spawn food
    private const double FoodClusterRadius = 5.0;       // Radius for clustering check
    private const double FoodClusterBonus = 0.5;        // Extra spawn chance near existing food
    private const double FoodConsumptionRate = 0.08;    // Food consumed per agent per frame
    private const double FoodSpawnAmount = 0.8;         // Amount of food deposited
    private const double FoodSpawnChanceOnEat = 0.15;   // Chance to spawn new agent when eating (was 0.03)
    private const double StarvationSeconds = 30.0;      // Seconds without food before death
    private const double ConnectivityRadius = 3.0;      // Agents within this distance share food
    
    // Species colors
    private static readonly Hex1bColor[] SpeciesColors =
    [
        Hex1bColor.FromRgb(255, 220, 100), // Yellow
        Hex1bColor.FromRgb(100, 220, 255), // Cyan
        Hex1bColor.FromRgb(255, 100, 220), // Magenta
        Hex1bColor.FromRgb(100, 255, 150)  // Green
    ];
    
    private static readonly Hex1bColor[] TrailColors =
    [
        Hex1bColor.FromRgb(80, 70, 30),    // Yellow trail
        Hex1bColor.FromRgb(30, 70, 80),    // Cyan trail
        Hex1bColor.FromRgb(80, 30, 70),    // Magenta trail
        Hex1bColor.FromRgb(30, 80, 50)     // Green trail
    ];
    
    // State
    private static List<Species>? _speciesList;
    private static double[,]? _foodMap;
    private static int _width;
    private static int _height;
    private static int _dotWidth;
    private static int _dotHeight;
    private static bool _initialized;
    private static DateTime _lastUpdate = DateTime.UtcNow;
    
    private struct Agent
    {
        public double X;
        public double Y;
        public double Angle;
        public bool Alive;
        public DateTime LastFedTime;
        public int SpeciesIndex; // Track which species this agent belongs to
    }
    
    private class Species
    {
        public List<Agent> Agents = [];
        public double[,] TrailMap = new double[0, 0];
        public Hex1bColor Color;
        public Hex1bColor TrailColor;
        public bool Alive = true;
        public DateTime DeathTime;
        public int AliveCount;
        public List<Agent> PendingSpawns = [];
        public bool IsHybrid;
    }
    
    public static IEnumerable<SurfaceLayer> BuildLayers(SurfaceLayerContext ctx, Random random)
    {
        int width = ctx.Width;
        int height = ctx.Height;
        
        if (!_initialized || _width != width || _height != height)
        {
            Initialize(width, height, random);
        }
        
        Update(random);
        
        yield return ctx.Layer(RenderTrailMap);
        yield return ctx.Layer(surface => RenderAgents(surface));
    }
    
    private static void Initialize(int width, int height, Random random)
    {
        _width = width;
        _height = height;
        _dotWidth = width * DotsPerCellX;
        _dotHeight = height * DotsPerCellY;
        
        _speciesList = new List<Species>();
        _foodMap = new double[_dotWidth, _dotHeight];
        
        // Spawn positions: corners
        var spawnPositions = new (double x, double y)[]
        {
            (_dotWidth * 0.15, _dotHeight * 0.15),
            (_dotWidth * 0.85, _dotHeight * 0.15),
            (_dotWidth * 0.15, _dotHeight * 0.85),
            (_dotWidth * 0.85, _dotHeight * 0.85)
        };
        
        for (int s = 0; s < InitialSpeciesCount; s++)
        {
            var species = new Species
            {
                Color = SpeciesColors[s],
                TrailColor = TrailColors[s],
                TrailMap = new double[_dotWidth, _dotHeight],
                Agents = new List<Agent>(),
                Alive = true,
                AliveCount = AgentsPerSpecies,
                IsHybrid = false
            };
            
            var (spawnX, spawnY) = spawnPositions[s];
            double spawnRadius = Math.Min(_dotWidth, _dotHeight) / 8.0;
            
            for (int i = 0; i < AgentsPerSpecies; i++)
            {
                var angle = random.NextDouble() * Math.PI * 2;
                var dist = random.NextDouble() * spawnRadius;
                species.Agents.Add(new Agent
                {
                    X = spawnX + Math.Cos(angle) * dist,
                    Y = spawnY + Math.Sin(angle) * dist,
                    Angle = random.NextDouble() * Math.PI * 2,
                    Alive = true,
                    LastFedTime = DateTime.UtcNow,
                    SpeciesIndex = s
                });
            }
            
            _speciesList.Add(species);
        }
        
        _initialized = true;
        _lastUpdate = DateTime.UtcNow;
    }
    
    private static void Update(Random random)
    {
        if (_speciesList == null || _foodMap == null) return;
        
        var now = DateTime.UtcNow;
        int speciesCount = _speciesList.Count;
        
        // Spawn food with clustering
        SpawnFood(random);
        
        // Check for respawns (only original 4 species respawn)
        for (int s = 0; s < Math.Min(InitialSpeciesCount, speciesCount); s++)
        {
            var species = _speciesList[s];
            if (!species.Alive && (now - species.DeathTime).TotalSeconds >= RespawnDelaySeconds)
            {
                RespawnSpecies(s, random);
            }
        }
        
        // Update each species
        for (int s = 0; s < speciesCount; s++)
        {
            var species = _speciesList[s];
            if (!species.Alive) continue;
            
            UpdateSpecies(s, random);
        }
        
        // Process crossbreeding interactions
        ProcessInteractions(random);
        
        // Decay and diffuse all trails
        for (int s = 0; s < _speciesList.Count; s++)
        {
            if (!_speciesList[s].Alive) continue;
            DecayTrail(_speciesList[s]);
        }
        
        // Clean up dead hybrid species (keep original 4)
        for (int s = _speciesList.Count - 1; s >= InitialSpeciesCount; s--)
        {
            if (!_speciesList[s].Alive && (now - _speciesList[s].DeathTime).TotalSeconds > RespawnDelaySeconds * 2)
            {
                _speciesList.RemoveAt(s);
            }
        }
        
        _lastUpdate = now;
    }
    
    private static void SpawnFood(Random random)
    {
        if (_foodMap == null) return;
        
        // Random chance to spawn food
        if (random.NextDouble() >= FoodSpawnChance) return;
        
        // Pick random position
        int x = random.Next(_dotWidth);
        int y = random.Next(_dotHeight);
        
        // Check for clustering bonus - more likely to spawn near existing food
        double nearbyFood = 0;
        int samples = 0;
        for (int dy = -(int)FoodClusterRadius; dy <= (int)FoodClusterRadius; dy++)
        {
            for (int dx = -(int)FoodClusterRadius; dx <= (int)FoodClusterRadius; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < _dotWidth && ny >= 0 && ny < _dotHeight)
                {
                    nearbyFood += _foodMap[nx, ny];
                    samples++;
                }
            }
        }
        
        // If no nearby food, use base chance; with nearby food, always spawn
        if (nearbyFood < 0.1 && random.NextDouble() > FoodClusterBonus)
            return;
        
        // Deposit food
        _foodMap[x, y] = Math.Min(1.0, _foodMap[x, y] + FoodSpawnAmount);
    }
    
    private static void UpdateSpecies(int speciesIndex, Random random)
    {
        var species = _speciesList![speciesIndex];
        var now = DateTime.UtcNow;
        int aliveCount = 0;
        species.PendingSpawns.Clear();
        
        // Track which agents ate this frame
        var fedAgentIndices = new List<int>();
        
        for (int i = 0; i < species.Agents.Count; i++)
        {
            var agent = species.Agents[i];
            if (!agent.Alive) continue;
            
            // Check for starvation
            if ((now - agent.LastFedTime).TotalSeconds >= StarvationSeconds)
            {
                agent.Alive = false;
                species.Agents[i] = agent;
                continue;
            }
            
            // Check for death from enemy trails
            double ownTrail = SampleTrail(speciesIndex, agent.X, agent.Y);
            double enemyTrail = SampleEnemyTrail(speciesIndex, agent.X, agent.Y);
            
            if (enemyTrail > ownTrail + DeathThreshold)
            {
                agent.Alive = false;
                species.Agents[i] = agent;
                continue;
            }
            
            aliveCount++;
            
            // Check for food consumption
            int fx = (int)agent.X;
            int fy = (int)agent.Y;
            if (fx >= 0 && fx < _dotWidth && fy >= 0 && fy < _dotHeight && _foodMap != null)
            {
                double food = _foodMap[fx, fy];
                if (food > 0.01)
                {
                    // Consume food
                    double consumed = Math.Min(food, FoodConsumptionRate);
                    _foodMap[fx, fy] -= consumed;
                    agent.LastFedTime = now;
                    fedAgentIndices.Add(i);
                    
                    // Chance to spawn new agent
                    if (random.NextDouble() < FoodSpawnChanceOnEat && 
                        species.Agents.Count + species.PendingSpawns.Count < MaxAgentsPerSpecies)
                    {
                        // Spawn near parent with similar heading
                        var spawnAngle = agent.Angle + (random.NextDouble() - 0.5) * Math.PI * 0.5;
                        species.PendingSpawns.Add(new Agent
                        {
                            X = Math.Clamp(agent.X + (random.NextDouble() - 0.5) * 2, 0, _dotWidth - 1),
                            Y = Math.Clamp(agent.Y + (random.NextDouble() - 0.5) * 2, 0, _dotHeight - 1),
                            Angle = spawnAngle,
                            Alive = true,
                            LastFedTime = now,
                            SpeciesIndex = speciesIndex
                        });
                    }
                }
            }
            
            // Sense: prefer own trail, avoid enemy, attract to food
            double senseLeft = SenseWithPreference(speciesIndex, agent.X, agent.Y, agent.Angle - SensorAngle);
            double senseCenter = SenseWithPreference(speciesIndex, agent.X, agent.Y, agent.Angle);
            double senseRight = SenseWithPreference(speciesIndex, agent.X, agent.Y, agent.Angle + SensorAngle);
            
            // Check for cohesion - if isolated, steer toward peers
            var (peerCount, peerCenterX, peerCenterY) = CountNearbyPeers(species, agent.X, agent.Y, i);
            if (peerCount < MinPeersForComfort && peerCount > 0)
            {
                // Calculate angle toward peer center
                double toPeerAngle = Math.Atan2(peerCenterY - agent.Y, peerCenterX - agent.X);
                double angleDiff = NormalizeAngle(toPeerAngle - agent.Angle);
                
                // Boost sense values in the direction of peers
                double cohesionBoost = CohesionWeight * (MinPeersForComfort - peerCount) / MinPeersForComfort;
                if (angleDiff < 0) senseLeft += cohesionBoost;
                else if (angleDiff > 0) senseRight += cohesionBoost;
                else senseCenter += cohesionBoost;
            }
            
            // Turn based on sensed values
            if (senseCenter >= senseLeft && senseCenter >= senseRight)
            {
                // Keep going straight
            }
            else if (senseLeft > senseRight)
            {
                agent.Angle -= TurnSpeed + random.NextDouble() * TurnSpeed * 0.5;
            }
            else
            {
                agent.Angle += TurnSpeed + random.NextDouble() * TurnSpeed * 0.5;
            }
            
            // Move
            double newX = agent.X + Math.Cos(agent.Angle) * MoveSpeed;
            double newY = agent.Y + Math.Sin(agent.Angle) * MoveSpeed;
            
            // Bounce
            if (newX < 0 || newX >= _dotWidth)
            {
                agent.Angle = Math.PI - agent.Angle;
                newX = Math.Clamp(newX, 0, _dotWidth - 1);
            }
            if (newY < 0 || newY >= _dotHeight)
            {
                agent.Angle = -agent.Angle;
                newY = Math.Clamp(newY, 0, _dotHeight - 1);
            }
            
            agent.X = newX;
            agent.Y = newY;
            species.Agents[i] = agent;
            
            // Deposit trail (reduced if enemy trail is stronger)
            int tx = (int)agent.X;
            int ty = (int)agent.Y;
            if (tx >= 0 && tx < _dotWidth && ty >= 0 && ty < _dotHeight)
            {
                double enemyStrength = SampleEnemyTrail(speciesIndex, tx, ty);
                double depositAmount = TrailDeposit * Math.Max(0.2, 1.0 - enemyStrength * 0.5);
                species.TrailMap[tx, ty] = Math.Min(1.5, species.TrailMap[tx, ty] + depositAmount);
                
                // Overwrite weaker enemy trails
                for (int e = 0; e < _speciesList.Count; e++)
                {
                    if (e == speciesIndex || !_speciesList[e].Alive) continue;
                    if (_speciesList[e].TrailMap[tx, ty] < species.TrailMap[tx, ty] * 0.5)
                    {
                        _speciesList[e].TrailMap[tx, ty] *= 0.8;
                    }
                }
            }
        }
        
        // Propagate feeding through connected agents
        if (fedAgentIndices.Count > 0)
        {
            PropagateFeeding(species, fedAgentIndices, now);
        }
        
        // Add pending spawns
        if (species.PendingSpawns.Count > 0)
        {
            species.Agents.AddRange(species.PendingSpawns);
            aliveCount += species.PendingSpawns.Count;
        }
        
        species.AliveCount = aliveCount;
        
        // Check for species death
        if (aliveCount == 0 && species.Alive)
        {
            species.Alive = false;
            species.DeathTime = DateTime.UtcNow;
        }
    }
    
    private static void PropagateFeeding(Species species, List<int> fedAgentIndices, DateTime now)
    {
        // Build connectivity graph and propagate feeding using flood fill
        var fed = new bool[species.Agents.Count];
        foreach (var idx in fedAgentIndices)
        {
            fed[idx] = true;
        }
        
        // Simple flood-fill propagation - repeat until no changes
        bool changed = true;
        double radiusSq = ConnectivityRadius * ConnectivityRadius;
        
        while (changed)
        {
            changed = false;
            for (int i = 0; i < species.Agents.Count; i++)
            {
                if (!species.Agents[i].Alive || fed[i]) continue;
                
                // Check if connected to any fed agent
                for (int j = 0; j < species.Agents.Count; j++)
                {
                    if (!fed[j] || !species.Agents[j].Alive) continue;
                    
                    double dx = species.Agents[i].X - species.Agents[j].X;
                    double dy = species.Agents[i].Y - species.Agents[j].Y;
                    double distSq = dx * dx + dy * dy;
                    
                    if (distSq <= radiusSq)
                    {
                        fed[i] = true;
                        var agent = species.Agents[i];
                        agent.LastFedTime = now;
                        species.Agents[i] = agent;
                        changed = true;
                        break;
                    }
                }
            }
        }
    }
    
    private static void ProcessInteractions(Random random)
    {
        if (_speciesList == null) return;
        
        var now = DateTime.UtcNow;
        double radiusSq = InteractionRadius * InteractionRadius;
        var pendingHybrids = new List<(Hex1bColor color, double x, double y, double angle)>();
        
        // Check all pairs of species for interactions
        for (int s1 = 0; s1 < _speciesList.Count; s1++)
        {
            if (!_speciesList[s1].Alive) continue;
            
            for (int s2 = s1 + 1; s2 < _speciesList.Count; s2++)
            {
                if (!_speciesList[s2].Alive) continue;
                
                // Calculate color distance between species
                var c1 = _speciesList[s1].Color;
                var c2 = _speciesList[s2].Color;
                double colorDist = Math.Sqrt(
                    Math.Pow(c1.R - c2.R, 2) +
                    Math.Pow(c1.G - c2.G, 2) +
                    Math.Pow(c1.B - c2.B, 2)
                );
                double normalizedDist = colorDist / MaxColorDistance; // 0 = same, 1 = opposite
                
                // Check agent proximity
                foreach (var a1 in _speciesList[s1].Agents)
                {
                    if (!a1.Alive) continue;
                    
                    foreach (var a2 in _speciesList[s2].Agents)
                    {
                        if (!a2.Alive) continue;
                        
                        double dx = a1.X - a2.X;
                        double dy = a1.Y - a2.Y;
                        if (dx * dx + dy * dy > radiusSq) continue;
                        
                        // They're close enough to interact
                        if (random.NextDouble() > InteractionChance) continue;
                        
                        // Probability of attack vs breed based on color distance
                        // High distance = attack, low distance = breed
                        double attackChance = normalizedDist;
                        
                        if (random.NextDouble() < attackChance)
                        {
                            // Attack - one dies (50/50)
                            // We can't modify the list while iterating, just mark for later
                            // For simplicity, the attacker wins based on trail strength
                        }
                        else
                        {
                            // Breed - create hybrid offspring
                            if (_speciesList.Count < MaxSpeciesCount && pendingHybrids.Count < 3)
                            {
                                // Blend colors
                                var hybridColor = Hex1bColor.FromRgb(
                                    (byte)((c1.R + c2.R) / 2),
                                    (byte)((c1.G + c2.G) / 2),
                                    (byte)((c1.B + c2.B) / 2)
                                );
                                
                                // Spawn at midpoint
                                double midX = (a1.X + a2.X) / 2;
                                double midY = (a1.Y + a2.Y) / 2;
                                double midAngle = (a1.Angle + a2.Angle) / 2;
                                
                                pendingHybrids.Add((hybridColor, midX, midY, midAngle));
                            }
                        }
                    }
                }
            }
        }
        
        // Create hybrid species
        foreach (var (color, x, y, angle) in pendingHybrids)
        {
            if (_speciesList.Count >= MaxSpeciesCount) break;
            
            var trailColor = Hex1bColor.FromRgb(
                (byte)(color.R / 3),
                (byte)(color.G / 3),
                (byte)(color.B / 3)
            );
            
            int newIndex = _speciesList.Count;
            var hybrid = new Species
            {
                Color = color,
                TrailColor = trailColor,
                TrailMap = new double[_dotWidth, _dotHeight],
                Agents = new List<Agent>(),
                Alive = true,
                AliveCount = 0,
                IsHybrid = true
            };
            
            // Start with a small population
            for (int i = 0; i < 20; i++)
            {
                hybrid.Agents.Add(new Agent
                {
                    X = Math.Clamp(x + (random.NextDouble() - 0.5) * 4, 0, _dotWidth - 1),
                    Y = Math.Clamp(y + (random.NextDouble() - 0.5) * 4, 0, _dotHeight - 1),
                    Angle = angle + (random.NextDouble() - 0.5) * Math.PI,
                    Alive = true,
                    LastFedTime = now,
                    SpeciesIndex = newIndex
                });
            }
            hybrid.AliveCount = hybrid.Agents.Count;
            
            _speciesList.Add(hybrid);
        }
    }
    
    private static void RespawnSpecies(int speciesIndex, Random random)
    {
        var species = _speciesList![speciesIndex];
        
        // Pick a spawn edge
        double spawnX, spawnY;
        int edge = random.Next(4);
        double margin = Math.Min(_dotWidth, _dotHeight) * 0.1;
        
        switch (edge)
        {
            case 0: spawnX = margin; spawnY = _dotHeight / 2.0; break;
            case 1: spawnX = _dotWidth - margin; spawnY = _dotHeight / 2.0; break;
            case 2: spawnX = _dotWidth / 2.0; spawnY = margin; break;
            default: spawnX = _dotWidth / 2.0; spawnY = _dotHeight - margin; break;
        }
        
        double spawnRadius = Math.Min(_dotWidth, _dotHeight) / 10.0;
        
        // Clear old trail
        species.TrailMap = new double[_dotWidth, _dotHeight];
        
        // Reset to original agent count
        species.Agents = new List<Agent>();
        
        // Respawn agents
        for (int i = 0; i < AgentsPerSpecies; i++)
        {
            var angle = random.NextDouble() * Math.PI * 2;
            var dist = random.NextDouble() * spawnRadius;
            species.Agents.Add(new Agent
            {
                X = Math.Clamp(spawnX + Math.Cos(angle) * dist, 0, _dotWidth - 1),
                Y = Math.Clamp(spawnY + Math.Sin(angle) * dist, 0, _dotHeight - 1),
                Angle = random.NextDouble() * Math.PI * 2,
                Alive = true,
                LastFedTime = DateTime.UtcNow,
                SpeciesIndex = speciesIndex
            });
        }
        
        species.Alive = true;
        species.AliveCount = AgentsPerSpecies;
    }
    
    private static double SampleTrail(int speciesIndex, double x, double y)
    {
        int tx = (int)x;
        int ty = (int)y;
        if (tx < 0 || tx >= _dotWidth || ty < 0 || ty >= _dotHeight) return 0;
        return _speciesList![speciesIndex].TrailMap[tx, ty];
    }
    
    private static double SampleEnemyTrail(int speciesIndex, double x, double y)
    {
        int tx = (int)x;
        int ty = (int)y;
        if (tx < 0 || tx >= _dotWidth || ty < 0 || ty >= _dotHeight) return 0;
        
        double total = 0;
        for (int e = 0; e < _speciesList!.Count; e++)
        {
            if (e == speciesIndex || !_speciesList[e].Alive) continue;
            total += _speciesList[e].TrailMap[tx, ty];
        }
        return total;
    }
    
    private static double SenseWithPreference(int speciesIndex, double x, double y, double angle)
    {
        double sx = x + Math.Cos(angle) * SensorDistance;
        double sy = y + Math.Sin(angle) * SensorDistance;
        
        double own = SampleTrail(speciesIndex, sx, sy);
        double enemy = SampleEnemyTrail(speciesIndex, sx, sy);
        double food = SampleFood(sx, sy);
        
        // Food is very attractive, own trail moderately, enemy negative
        return own + food * 2.0 - enemy * EnemyAvoidance;
    }
    
    private static double SampleFood(double x, double y)
    {
        if (_foodMap == null) return 0;
        int fx = (int)x;
        int fy = (int)y;
        if (fx < 0 || fx >= _dotWidth || fy < 0 || fy >= _dotHeight) return 0;
        return _foodMap[fx, fy];
    }
    
    private static (int count, double centerX, double centerY) CountNearbyPeers(Species species, double x, double y, int excludeIndex)
    {
        double radiusSq = CohesionRadius * CohesionRadius;
        int count = 0;
        double sumX = 0, sumY = 0;
        
        for (int i = 0; i < species.Agents.Count; i++)
        {
            if (i == excludeIndex || !species.Agents[i].Alive) continue;
            
            double dx = species.Agents[i].X - x;
            double dy = species.Agents[i].Y - y;
            double distSq = dx * dx + dy * dy;
            
            if (distSq <= radiusSq)
            {
                count++;
                sumX += species.Agents[i].X;
                sumY += species.Agents[i].Y;
            }
        }
        
        if (count == 0) return (0, x, y);
        return (count, sumX / count, sumY / count);
    }
    
    private static double NormalizeAngle(double angle)
    {
        while (angle > Math.PI) angle -= 2 * Math.PI;
        while (angle < -Math.PI) angle += 2 * Math.PI;
        return angle;
    }
    
    private static void DecayTrail(Species species)
    {
        var newTrail = new double[_dotWidth, _dotHeight];
        
        for (int y = 0; y < _dotHeight; y++)
        {
            for (int x = 0; x < _dotWidth; x++)
            {
                double sum = species.TrailMap[x, y];
                int count = 1;
                
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx >= 0 && nx < _dotWidth && ny >= 0 && ny < _dotHeight)
                        {
                            sum += species.TrailMap[nx, ny] * TrailDiffuse;
                            count++;
                        }
                    }
                }
                
                newTrail[x, y] = (sum / count) * TrailDecay;
            }
        }
        
        species.TrailMap = newTrail;
    }
    
    private static void RenderTrailMap(Surface surface)
    {
        if (_speciesList == null) return;
        
        int speciesCount = _speciesList.Count;
        
        for (int cellY = 0; cellY < surface.Height; cellY++)
        {
            for (int cellX = 0; cellX < surface.Width; cellX++)
            {
                // Sample all species trails and food, then blend colors
                double[] totals = new double[speciesCount];
                double foodTotal = 0;
                double maxTotal = 0;
                
                for (int dy = 0; dy < DotsPerCellY; dy++)
                {
                    for (int dx = 0; dx < DotsPerCellX; dx++)
                    {
                        int dotX = cellX * DotsPerCellX + dx;
                        int dotY = cellY * DotsPerCellY + dy;
                        if (dotX >= _dotWidth || dotY >= _dotHeight) continue;
                        
                        for (int s = 0; s < speciesCount; s++)
                        {
                            totals[s] += _speciesList[s].TrailMap[dotX, dotY];
                        }
                        
                        if (_foodMap != null)
                        {
                            foodTotal += _foodMap[dotX, dotY];
                        }
                    }
                }
                
                // Normalize and find dominant
                for (int s = 0; s < speciesCount; s++)
                {
                    totals[s] /= DotsPerCellX * DotsPerCellY;
                    if (totals[s] > maxTotal) maxTotal = totals[s];
                }
                foodTotal /= DotsPerCellX * DotsPerCellY;
                
                // Blend colors based on trail strengths
                double r = 5, g = 5, b = 10;
                
                // Add food glow (bright green/white)
                double foodIntensity = Math.Min(1.0, foodTotal * 3.0);
                r += 80 * foodIntensity;
                g += 180 * foodIntensity;
                b += 60 * foodIntensity;
                
                // Add trail colors
                for (int s = 0; s < speciesCount; s++)
                {
                    var intensity = Math.Min(1.0, totals[s] * 2);
                    var tc = _speciesList[s].TrailColor;
                    r += tc.R * intensity;
                    g += tc.G * intensity;
                    b += tc.B * intensity;
                }
                
                var bg = Hex1bColor.FromRgb(
                    (byte)Math.Min(255, r),
                    (byte)Math.Min(255, g),
                    (byte)Math.Min(255, b)
                );
                
                surface[cellX, cellY] = new SurfaceCell(" ", null, bg);
            }
        }
    }
    
    private static void RenderAgents(Surface surface)
    {
        if (_speciesList == null) return;
        
        int speciesCount = _speciesList.Count;
        
        // Group agents by cell and species
        var agentsByCell = new Dictionary<(int, int), List<(int speciesIndex, int dotX, int dotY)>>();
        
        for (int s = 0; s < speciesCount; s++)
        {
            if (!_speciesList[s].Alive) continue;
            
            foreach (var agent in _speciesList[s].Agents)
            {
                if (!agent.Alive) continue;
                
                int dotX = (int)agent.X;
                int dotY = (int)agent.Y;
                int cellX = dotX / DotsPerCellX;
                int cellY = dotY / DotsPerCellY;
                
                if (cellX < 0 || cellX >= surface.Width || cellY < 0 || cellY >= surface.Height)
                    continue;
                
                var key = (cellX, cellY);
                if (!agentsByCell.TryGetValue(key, out var list))
                    agentsByCell[key] = list = new List<(int, int, int)>();
                list.Add((s, dotX % DotsPerCellX, dotY % DotsPerCellY));
            }
        }
        
        // Render - use dominant species color in each cell
        foreach (var (cell, agents) in agentsByCell)
        {
            int brailleBits = 0;
            var speciesCounts = new Dictionary<int, int>();
            
            foreach (var (speciesIndex, dotX, dotY) in agents)
            {
                speciesCounts[speciesIndex] = speciesCounts.GetValueOrDefault(speciesIndex) + 1;
                brailleBits |= dotY switch
                {
                    0 => dotX == 0 ? 0x01 : 0x08,
                    1 => dotX == 0 ? 0x02 : 0x10,
                    2 => dotX == 0 ? 0x04 : 0x20,
                    3 => dotX == 0 ? 0x40 : 0x80,
                    _ => 0
                };
            }
            
            // Find dominant species
            int dominant = speciesCounts.MaxBy(kv => kv.Value).Key;
            
            var ch = (char)(0x2800 + brailleBits);
            var existing = surface[cell.Item1, cell.Item2];
            
            surface[cell.Item1, cell.Item2] = new SurfaceCell(
                ch.ToString(),
                _speciesList[dominant].Color,
                existing.Background ?? Hex1bColor.FromRgb(5, 5, 10)
            );
        }
    }
}
