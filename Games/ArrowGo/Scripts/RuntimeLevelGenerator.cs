using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OMG.ArrowGo
{
    [Serializable]
    public struct ArrowSpawnData
    {
        public int x;
        public int y;
        public ArrowDirection direction;

        // Ordered HEAD -> TAIL.
        // path[0] is the arrow head cell.
        // Every next cell must be directly adjacent.
        // For multi-cell arrows, direction must equal path[0] - path[1].
        public List<Vector2Int> path;

        [Min(1)]
        public int length;

        public List<Vector2Int> GetPath()
        {
            if (path != null && path.Count > 0)
                return path;

            return new List<Vector2Int>
            {
                new Vector2Int(x, y)
            };
        }
    }

    [CreateAssetMenu(fileName = "Level_01", menuName = "ArrowGo/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Min(1)] public int width = 5;
        [Min(1)] public int height = 5;

        public List<ArrowSpawnData> arrows = new List<ArrowSpawnData>();
    }

    /// <summary>
    /// Runtime ArrowGo level generator.
    ///
    /// Design goals:
    /// - Automatic grid progression (3x3 -> 4x4 -> ...).
    /// - A COMPLETE partition: every board cell belongs to exactly one arrow.
    /// - A small number of substantial arrows, while preferring compact/interlocking spatial regions.
    /// - Multi-turn paths are deliberately preferred as the grid grows.
    /// - Arrow heads are assigned from a valid removal order, so generated
    ///   levels are actually solvable by the same straight-ray rule used by
    ///   GridManager.
    /// - Existing LevelData / ArrowView / GridManager APIs are preserved.
    /// </summary>
    public class RuntimeLevelGenerator : MonoBehaviour
    {
        public static RuntimeLevelGenerator Instance { get; private set; }

        [Header("Board Settings")]
        [SerializeField] private int boardWidth = 5;
        [SerializeField] private int boardHeight = 5;

        [Header("Automatic Grid Progression")]
        [SerializeField] private int startingGridWidth = 4;
        [SerializeField] private int startingGridHeight = 4;
        [SerializeField] private int levelsPerGridIncrease = 2;
        [SerializeField] private bool automaticGridProgression = true;
        [SerializeField] private int maxGridWidth = 10;
        [SerializeField] private int maxGridHeight = 10;

        [Header("Grid Panel Limit")]
        [SerializeField] private RectTransform gridPanel;
        [SerializeField] private float gridCellSize = 100f;
        [SerializeField] private float gridCellSpacing = 8f;
        [SerializeField] private float gridPanelPadding = 10f;

        [Header("Level Settings")]
        [Tooltip("Difficulty automatically progresses from early to late levels. Levels are generated on demand, so there is no level-count limit.")]
        [SerializeField] private bool progressiveDifficulty = true;

        public enum DifficultyPreset
        {
            VeryEasy,
            Easy,
            Medium,
            Hard,
            VeryHard,
            Custom
        }

        [Header("Difficulty")]
        [SerializeField] private DifficultyPreset difficulty = DifficultyPreset.VeryHard;

        [Header("Difficulty Parameters")]
        [Range(0f, 1f)] [SerializeField] private float blockingChance = 0.60f;
        [Range(0f, 1f)] [SerializeField] private float difficultyVariation = 0.10f;

        [Header("Arrow Count")]
        [Tooltip("Minimum number of arrows. Automatic progression still respects this value.")]
        [Min(1)] [SerializeField] private int minArrows = 5;

        [Tooltip("Maximum number of arrows. Automatic progression still respects this value.")]
        [Min(1)] [SerializeField] private int maxArrows = 16;

        [Header("Path Shape")]
        [Tooltip("Every arrow uses at least this many cells whenever the grid allows it.")]
        [Min(1)] [SerializeField] private int minimumPathLength = 3;

        [Tooltip("Maximum cells in one arrow path. 8 works well for the reference style.")]
        [Min(1)] [SerializeField] private int maxArrowLength = 12;

        [Range(0f, 1f)]
        [SerializeField] private float turnChance = 0.82f;

        [Tooltip("Automatically increase the number of turns as the grid and difficulty increase.")]
        [SerializeField] private bool automaticComplexity = true;

        [Tooltip("Minimum turns targeted on the smallest grid.")]
        [Min(0)] [SerializeField] private int minTurnsSmallGrid = 1;

        [Tooltip("Maximum turns targeted on the smallest grid.")]
        [Min(0)] [SerializeField] private int maxTurnsSmallGrid = 2;

        [Tooltip("Minimum turns targeted on the largest grid.")]
        [Min(0)] [SerializeField] private int minTurnsLargeGrid = 3;

        [Tooltip("Maximum turns targeted on the largest grid.")]
        [Min(0)] [SerializeField] private int maxTurnsLargeGrid = 5;

        [Tooltip("How strongly candidate splitting prefers arrows containing turns.")]
        [Range(0f, 1f)] [SerializeField] private float turnedPathPreference = 0.88f;

        [Tooltip("Extra attempts used to find a better turn distribution for each level.")]
        [Range(1, 200)] [SerializeField] private int shapeSearchAttempts = 160;

        [Header("Complexity / Interaction")]
        [Tooltip("How strongly the generator prefers arrows whose escape ray is blocked by other arrows.")]
        [Range(0f, 1f)] [SerializeField] private float interactionPreference = 0.85f;

        [Tooltip("Minimum generated complexity score. The required score increases with level difficulty.")]
        [Range(0f, 1f)] [SerializeField] private float minimumComplexityScore = 0.42f;

        [Tooltip("How strongly the generator prefers compact arrow regions instead of long sparse-looking regions.")]
        [Range(0f, 1f)] [SerializeField] private float spatialCompactnessPreference = 0.82f;

        [Tooltip("How strongly the generator prefers different arrows to touch/cluster instead of leaving visual separation.")]
        [Range(0f, 1f)] [SerializeField] private float arrowContactPreference = 0.88f;

        [Tooltip("Reject a candidate when an arrow's cells occupy too much bounding-box area.")]
        [Range(0f, 1f)] [SerializeField] private float minimumArrowCompactness = 0.55f;

        [Tooltip("Extra search attempts for spatially compact partitions.")]
        [Range(1, 300)] [SerializeField] private int compactnessSearchAttempts = 160;

        [Tooltip("How many different Hamiltonian paths are searched when Interlocked mode is used.")]
        [Range(1, 40)] [SerializeField] private int interlockedPathAttempts = 12;

        [Tooltip("Maximum DFS nodes per interlocked-path attempt.")]
        [Range(1000, 200000)] [SerializeField] private int interlockedPathNodeBudget = 60000;

        public enum BoardPathStyle
        {
            Spiral,
            Snake,
            Weave,
            Interlocked,
            Random
        }

        [Tooltip("Random automatically shifts toward Interlocked/Weave as difficulty rises. Interlocked gives the densest geometry.")]
        [SerializeField] private BoardPathStyle pathStyle = BoardPathStyle.Random;

        [Header("Random")]
        [SerializeField] private bool useRandomSeed = true;
        [SerializeField] private int seed = 12345;

        // IMPORTANT:
        // This seed is created ONCE when the game session starts.
        // Restarting a level must never create a new seed.
        private int sessionSeed;

        [Header("Generation Safety")]
        [SerializeField] private int maxAttemptsPerLevel = 500;
        [SerializeField] private int maxSolverSteps = 20000;


        private int currentBoardWidth;
        private int currentBoardHeight;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Create one base seed for this game session.
            // It remains unchanged until the game/application is restarted.
            if (useRandomSeed)
            {
                sessionSeed = Environment.TickCount;
            }
            else
            {
                sessionSeed = seed;
            }

            ValidateSettings();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            currentBoardWidth = 0;
            currentBoardHeight = 0;
        }

        // ============================================================
        // PUBLIC GENERATION API
        // ============================================================

        /// <summary>
        /// Generates exactly one level on demand. There is no maximum level count.
        /// The supplied zero-based level index controls grid progression and difficulty.
        /// </summary>
        public LevelData GenerateLevel(int levelIndex)
        {
            ValidateSettings();

            levelIndex = Mathf.Max(0, levelIndex);

            SetGridSizeForLevel(levelIndex);
            float levelDifficulty = GetLevelDifficulty(levelIndex);

            // ------------------------------------------------------------
            // STABLE SEED PER LEVEL
            // ------------------------------------------------------------
            // GenerateLevel() is also called by the Restart button.
            // Therefore the seed MUST depend only on the current session
            // seed + level index, NOT on the current time.
            //
            // Level 3 -> always gets the same seed during this session.
            // Restart Level 3 -> same seed -> same pattern.
            // Level 4 -> different seed -> different pattern.
            // ------------------------------------------------------------
            unchecked
            {
                int levelSeed = sessionSeed + (levelIndex * 486187739);
                UnityEngine.Random.InitState(levelSeed);
            }

            for (int attempt = 0; attempt < maxAttemptsPerLevel; attempt++)
            {
                LevelData candidate = GenerateCandidateLevel(levelDifficulty);

                if (candidate == null)
                    continue;

                if (ValidateLevel(candidate))
                {
                    Debug.Log(
                        "ArrowGo: Generated Level " +
                        (levelIndex + 1) +
                        " | Grid: " + candidate.width + "x" + candidate.height +
                        " | Arrows: " + candidate.arrows.Count +
                        " | Turns: " + CountTotalTurns(candidate) +
                        " | Occupied: " + CountOccupiedCells(candidate) +
                        "/" + (candidate.width * candidate.height));

                    return candidate;
                }

                DestroyRuntimeLevel(candidate);
            }

            LevelData fallback = GenerateFallbackLevel(levelDifficulty);

            if (fallback != null && ValidateLevel(fallback))
            {
                Debug.LogWarning(
                    "ArrowGo: Used fallback generation for Level " +
                    (levelIndex + 1));

                return fallback;
            }

            if (fallback != null)
                DestroyRuntimeLevel(fallback);

            Debug.LogError(
                "ArrowGo: Failed to generate Level " +
                (levelIndex + 1) +
                " on a " + currentBoardWidth + "x" + currentBoardHeight +
                " board.");

            return null;
        }

        // Kept for compatibility with older code. It now simply clears any
        // generated levels that may have been created by legacy calls.
        public void GenerateLevels()
        {
            ValidateSettings();
        }

        public void RegenerateLevels()
        {
            ValidateSettings();
        }

        public LevelData GenerateSingleLevel(float difficultyValue)
        {
            ValidateSettings();
            difficultyValue = Mathf.Clamp01(difficultyValue);
            SetGridSizeForLevel(0);

            for (int attempt = 0; attempt < maxAttemptsPerLevel; attempt++)
            {
                LevelData level = GenerateCandidateLevel(difficultyValue);

                if (level != null && ValidateLevel(level))
                    return level;

                DestroyRuntimeLevel(level);
            }

            return null;
        }

        // ============================================================
        // CANDIDATE LEVEL
        // ============================================================


        private LevelData GenerateCandidateLevel(float levelDifficulty)
        {
            int width = currentBoardWidth > 0 ? currentBoardWidth : boardWidth;
            int height = currentBoardHeight > 0 ? currentBoardHeight : boardHeight;

            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            level.width = width;
            level.height = height;
            level.arrows = new List<ArrowSpawnData>();

            List<Vector2Int> completePath = GenerateCompleteBoardPath(
                width,
                height,
                levelDifficulty);

            if (completePath == null || completePath.Count != width * height)
            {
                DestroyRuntimeLevel(level);
                return null;
            }

            int desiredArrowCount = ChooseArrowCount(
                width * height,
                levelDifficulty);

            List<List<Vector2Int>> paths = SplitCompletePath(
                completePath,
                desiredArrowCount,
                levelDifficulty);

            if (paths == null || paths.Count == 0)
            {
                DestroyRuntimeLevel(level);
                return null;
            }

            if (!AssignSolvableDirections(level, paths, levelDifficulty))
            {
                DestroyRuntimeLevel(level);
                return null;
            }

            // The old generator mainly scored the shape of the paths. That can
            // still produce a solvable board made from visually isolated arrows.
            // Score the FINAL puzzle as well: turns + blocked escape rays +
            // head pressure + path coverage. Reject weak candidates so the
            // outer generation loop keeps searching for a genuinely interactive
            // puzzle.
            float complexityScore = ScoreGeneratedLevel(level, levelDifficulty);

            if (complexityScore < GetRequiredComplexityScore(levelDifficulty))
            {
                DestroyRuntimeLevel(level);
                return null;
            }

            return level;
        }

        private LevelData GenerateFallbackLevel(float levelDifficulty)
        {
            int width = currentBoardWidth > 0 ? currentBoardWidth : boardWidth;
            int height = currentBoardHeight > 0 ? currentBoardHeight : boardHeight;

            List<Vector2Int> path = CreateSpiralPath(width, height);
            TransformPathRandomly(path, width, height);

            int arrowCount = ChooseArrowCount(width * height, Mathf.Min(levelDifficulty, 0.45f));
            List<List<Vector2Int>> paths = SplitPathSimple(path, arrowCount);

            if (paths == null)
                return null;

            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            level.width = width;
            level.height = height;
            level.arrows = new List<ArrowSpawnData>();

            if (!AssignSolvableDirections(level, paths, Mathf.Min(levelDifficulty, 0.45f)) ||
                !ValidateLevel(level))
            {
                DestroyRuntimeLevel(level);
                return null;
            }

            return level;
        }

        // ============================================================
        // COMPLETE BOARD PATH
        // ============================================================

        private List<Vector2Int> GenerateCompleteBoardPath(
            int width,
            int height,
            float difficultyValue)
        {
            BoardPathStyle style = pathStyle;

            if (style == BoardPathStyle.Random)
            {
                // Easy boards can still use the clean spiral/snake language.
                // As difficulty rises, Random increasingly selects a high-turn
                // Hamiltonian path so arrows naturally become shorter, denser
                // and more interlocked.
                float roll = UnityEngine.Random.value;

                if (difficultyValue < 0.35f)
                {
                    style = roll < 0.55f
                        ? BoardPathStyle.Spiral
                        : BoardPathStyle.Snake;
                }
                else if (difficultyValue < 0.65f)
                {
                    style = roll < 0.65f
                        ? BoardPathStyle.Weave
                        : (roll < 0.85f
                            ? BoardPathStyle.Spiral
                            : BoardPathStyle.Snake);
                }
                else
                {
                    style = roll < 0.82f
                        ? BoardPathStyle.Interlocked
                        : (roll < 0.94f
                            ? BoardPathStyle.Weave
                            : BoardPathStyle.Spiral);
                }
            }

            List<Vector2Int> path = null;

            switch (style)
            {
                case BoardPathStyle.Spiral:
                    path = CreateSpiralPath(width, height);
                    break;

                case BoardPathStyle.Snake:
                    path = CreateSnakePath(width, height);
                    break;

                case BoardPathStyle.Weave:
                    path = CreateWeavePath(width, height);
                    break;

                case BoardPathStyle.Interlocked:
                    path = CreateInterlockedPath(width, height, difficultyValue);
                    break;

                default:
                    path = CreateSpiralPath(width, height);
                    break;
            }

            // If a high-turn search fails, fall back to a guaranteed complete
            // path rather than failing the whole level.
            if (path == null || path.Count != width * height)
                path = CreateWeavePath(width, height);

            if (path == null || path.Count != width * height)
                path = CreateSpiralPath(width, height);

            if (path == null || path.Count != width * height)
                return null;

            TransformPathRandomly(path, width, height);

            if (UnityEngine.Random.value < 0.5f)
                path.Reverse();

            return path;
        }

        private List<Vector2Int> CreateSnakePath(int width, int height)
        {
            List<Vector2Int> path = new List<Vector2Int>(width * height);

            for (int y = 0; y < height; y++)
            {
                if (y % 2 == 0)
                {
                    for (int x = 0; x < width; x++)
                        path.Add(new Vector2Int(x, y));
                }
                else
                {
                    for (int x = width - 1; x >= 0; x--)
                        path.Add(new Vector2Int(x, y));
                }
            }

            return path;
        }

        private List<Vector2Int> CreateSpiralPath(int width, int height)
        {
            List<Vector2Int> path = new List<Vector2Int>(width * height);

            int left = 0;
            int right = width - 1;
            int bottom = 0;
            int top = height - 1;

            while (left <= right && bottom <= top)
            {
                for (int x = left; x <= right; x++)
                    path.Add(new Vector2Int(x, bottom));

                bottom++;

                for (int y = bottom; y <= top; y++)
                    path.Add(new Vector2Int(right, y));

                right--;

                if (bottom <= top)
                {
                    for (int x = right; x >= left; x--)
                        path.Add(new Vector2Int(x, top));

                    top--;
                }

                if (left <= right)
                {
                    for (int y = top; y >= bottom; y--)
                        path.Add(new Vector2Int(left, y));

                    left++;
                }
            }

            return path;
        }

        /// <summary>
        /// A deterministic high-turn Hamiltonian path. It is intentionally
        /// different from the old long-run snake: short vertical/horizontal
        /// runs create more turns inside normal arrow lengths.
        /// </summary>
        private List<Vector2Int> CreateWeavePath(int width, int height)
        {
            List<Vector2Int> best = null;
            int bestTurns = -1;

            // A randomized DFS gives a better weave than simply using a
            // transformed snake. Small boards are cheap, larger boards are
            // bounded by a few restarts.
            int attempts = Mathf.Clamp(
                Mathf.Max(4, width * height / 6),
                4,
                20);

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                List<Vector2Int> candidate = TryCreateHamiltonianPath(
                    width,
                    height,
                    Mathf.Max(1000, width * height * 250));

                if (candidate == null)
                    continue;

                int turns = CountTurns(candidate);

                if (turns > bestTurns)
                {
                    bestTurns = turns;
                    best = candidate;
                }
            }

            if (best != null)
                return best;

            // Guaranteed fallback.
            return CreateSnakePath(width, height);
        }

        /// <summary>
        /// Searches for a Hamiltonian path while preferring low-degree cells
        /// and turns. The path still covers every cell exactly once.
        /// </summary>
        private List<Vector2Int> CreateInterlockedPath(
            int width,
            int height,
            float difficultyValue)
        {
            List<Vector2Int> best = null;
            float bestPathScore = float.NegativeInfinity;

            int attempts = Mathf.Clamp(interlockedPathAttempts, 1, 40);
            int budget = Mathf.Clamp(
                interlockedPathNodeBudget,
                1000,
                200000);

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                List<Vector2Int> candidate = TryCreateHamiltonianPath(
                    width,
                    height,
                    budget);

                if (candidate == null)
                    continue;

                int turns = CountTurns(candidate);

                // Higher difficulty explicitly prefers paths with more turns.
                // A small random component prevents identical layouts.
                float score =
                    turns * Mathf.Lerp(1f, 1.5f, difficultyValue) +
                    UnityEngine.Random.value * 2f;

                if (best == null || score > bestPathScore)
                {
                    bestPathScore = score;
                    best = candidate;
                }
            }

            return best != null ? best : CreateWeavePath(width, height);
        }

        private List<Vector2Int> TryCreateHamiltonianPath(
            int width,
            int height,
            int nodeBudget)
        {
            if (width <= 0 || height <= 0)
                return null;

            List<Vector2Int> path =
                new List<Vector2Int>(width * height);

            HashSet<Vector2Int> visited =
                new HashSet<Vector2Int>();

            Vector2Int start = new Vector2Int(
                UnityEngine.Random.Range(0, width),
                UnityEngine.Random.Range(0, height));

            path.Add(start);
            visited.Add(start);

            int nodes = 0;

            if (SearchHamiltonianPath(
                width,
                height,
                path,
                visited,
                nodeBudget,
                ref nodes))
            {
                return path;
            }

            return null;
        }

        private bool SearchHamiltonianPath(
            int width,
            int height,
            List<Vector2Int> path,
            HashSet<Vector2Int> visited,
            int nodeBudget,
            ref int nodes)
        {
            nodes++;

            if (nodes > nodeBudget)
                return false;

            if (path.Count == width * height)
                return true;

            Vector2Int current = path[path.Count - 1];

            Vector2Int previous = Vector2Int.zero;
            bool hasPrevious = path.Count > 1;

            if (hasPrevious)
                previous = path[path.Count - 2];

            List<PathChoice> choices = new List<PathChoice>(4);

            Vector2Int[] directions =
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int next = current + directions[i];

                if (!IsInside(next, width, height) ||
                    visited.Contains(next))
                {
                    continue;
                }

                int onwardDegree = CountUnvisitedNeighbors(
                    next,
                    width,
                    height,
                    visited);

                bool turn = !hasPrevious ||
                    directions[i] != current - previous;

                // Low-degree cells are visited first to avoid creating
                // unreachable pockets. Turns receive a strong bonus because
                // the final arrows inherit this geometry.
                float turnBonus = turn ? 2.5f : 0f;
                float score =
                    -onwardDegree * 3f +
                    turnBonus +
                    UnityEngine.Random.value * 0.75f;

                choices.Add(new PathChoice(next, score));
            }

            choices.Sort(
                (a, b) => b.score.CompareTo(a.score));

            for (int i = 0; i < choices.Count; i++)
            {
                Vector2Int next = choices[i].position;

                visited.Add(next);
                path.Add(next);

                if (SearchHamiltonianPath(
                    width,
                    height,
                    path,
                    visited,
                    nodeBudget,
                    ref nodes))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
                visited.Remove(next);

                if (nodes > nodeBudget)
                    return false;
            }

            return false;
        }

        private int CountUnvisitedNeighbors(
            Vector2Int position,
            int width,
            int height,
            HashSet<Vector2Int> visited)
        {
            int count = 0;

            Vector2Int[] directions =
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int next = position + directions[i];

                if (IsInside(next, width, height) &&
                    !visited.Contains(next))
                {
                    count++;
                }
            }

            return count;
        }

        private void TransformPathRandomly(
            List<Vector2Int> path,
            int width,
            int height)
        {
            int transform = UnityEngine.Random.Range(0, 8);

            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int p = path[i];
                int x = p.x;
                int y = p.y;

                switch (transform)
                {
                    case 0:
                        break;

                    case 1:
                        x = width - 1 - x;
                        break;

                    case 2:
                        y = height - 1 - y;
                        break;

                    case 3:
                        x = width - 1 - x;
                        y = height - 1 - y;
                        break;

                    case 4:
                        if (width == height)
                        {
                            int t = x;
                            x = y;
                            y = t;
                        }
                        break;

                    case 5:
                        if (width == height)
                        {
                            int t = x;
                            x = y;
                            y = width - 1 - t;
                        }
                        break;

                    case 6:
                        if (width == height)
                        {
                            int t = x;
                            x = height - 1 - y;
                            y = t;
                        }
                        break;

                    case 7:
                        if (width == height)
                        {
                            int t = x;
                            x = height - 1 - y;
                            y = width - 1 - t;
                        }
                        break;
                }

                path[i] = new Vector2Int(x, y);
            }
        }

        // ============================================================
        // SPLIT COMPLETE PATH INTO COMPLEX ARROWS
        // ============================================================

        private List<List<Vector2Int>> SplitCompletePath(
            List<Vector2Int> completePath,
            int arrowCount,
            float difficultyValue)
        {
            if (completePath == null || completePath.Count == 0)
                return null;

            arrowCount = Mathf.Clamp(arrowCount, 1, completePath.Count);

            List<List<Vector2Int>> best = null;
            float bestScore = float.NegativeInfinity;

            int attempts = Mathf.Max(
                1,
                Mathf.Max(shapeSearchAttempts, compactnessSearchAttempts));

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                List<int> lengths = CreatePathLengths(
                    completePath.Count,
                    arrowCount,
                    difficultyValue);

                if (lengths == null)
                    continue;

                List<List<Vector2Int>> candidate = BuildPathsFromLengths(
                    completePath,
                    lengths);

                if (candidate == null)
                    continue;

                float score = ScorePathPartition(
                    candidate,
                    difficultyValue);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private List<List<Vector2Int>> SplitPathSimple(
            List<Vector2Int> completePath,
            int arrowCount)
        {
            List<int> lengths = CreateBalancedLengths(
                completePath.Count,
                arrowCount);

            return lengths == null
                ? null
                : BuildPathsFromLengths(completePath, lengths);
        }

        private List<List<Vector2Int>> BuildPathsFromLengths(
            List<Vector2Int> completePath,
            List<int> lengths)
        {
            if (completePath == null || lengths == null || lengths.Count == 0)
                return null;

            List<List<Vector2Int>> result =
                new List<List<Vector2Int>>(lengths.Count);

            int cursor = 0;

            for (int i = 0; i < lengths.Count; i++)
            {
                if (lengths[i] <= 0 || cursor + lengths[i] > completePath.Count)
                    return null;

                List<Vector2Int> path = new List<Vector2Int>(lengths[i]);

                for (int j = 0; j < lengths[i]; j++)
                    path.Add(completePath[cursor++]);

                result.Add(path);
            }

            return cursor == completePath.Count ? result : null;
        }

        private List<int> CreatePathLengths(
            int totalCells,
            int arrowCount,
            float difficultyValue)
        {
            if (arrowCount <= 0 || totalCells < arrowCount)
                return null;

            int minLength = Mathf.Max(1, minimumPathLength);
            int maxLength = Mathf.Max(minLength, maxArrowLength);

            if (totalCells < arrowCount * minLength)
                return null;

            if (totalCells > arrowCount * maxLength)
                return null;

            List<int> lengths = new List<int>(arrowCount);
            int remaining = totalCells;

            for (int i = 0; i < arrowCount; i++)
            {
                int arrowsLeft = arrowCount - i - 1;

                int minForThis = Mathf.Max(
                    minLength,
                    remaining - arrowsLeft * maxLength);

                int maxForThis = Mathf.Min(
                    maxLength,
                    remaining - arrowsLeft * minLength);

                if (maxForThis < minForThis)
                    return null;

                int chosen;

                if (i == arrowCount - 1)
                {
                    chosen = remaining;
                }
                else
                {
                    float longBias = Mathf.Lerp(0.30f, 0.72f, difficultyValue);
                    float value = UnityEngine.Random.value;

                    if (value < longBias)
                    {
                        chosen = UnityEngine.Random.Range(
                            Mathf.Max(minForThis, maxForThis - 2),
                            maxForThis + 1);
                    }
                    else
                    {
                        chosen = UnityEngine.Random.Range(
                            minForThis,
                            maxForThis + 1);
                    }
                }

                lengths.Add(chosen);
                remaining -= chosen;
            }

            return lengths;
        }

        private List<int> CreateBalancedLengths(int totalCells, int arrowCount)
        {
            if (arrowCount <= 0 || totalCells < arrowCount)
                return null;

            int minLength = Mathf.Max(1, minimumPathLength);
            int maxLength = Mathf.Max(minLength, maxArrowLength);

            if (totalCells < arrowCount * minLength ||
                totalCells > arrowCount * maxLength)
            {
                return null;
            }

            List<int> result = new List<int>(arrowCount);
            int baseLength = totalCells / arrowCount;
            int remainder = totalCells % arrowCount;

            for (int i = 0; i < arrowCount; i++)
                result.Add(baseLength + (i < remainder ? 1 : 0));

            return result;
        }

        private float ScorePathPartition(
            List<List<Vector2Int>> paths,
            float difficultyValue)
        {
            if (paths == null || paths.Count == 0)
                return float.NegativeInfinity;

            int width = currentBoardWidth;
            int height = currentBoardHeight;

            int minTurns;
            int maxTurns;
            GetTurnTargets(
                width,
                height,
                difficultyValue,
                out minTurns,
                out maxTurns);

            int totalTurns = 0;
            int turnedPaths = 0;
            int pathsAtTarget = 0;
            float lengthBalance = 0f;

            for (int i = 0; i < paths.Count; i++)
            {
                int turns = CountTurns(paths[i]);
                totalTurns += turns;

                if (turns > 0)
                    turnedPaths++;

                if (turns >= minTurns)
                    pathsAtTarget++;

                float idealLength =
                    (float)(width * height) / paths.Count;

                lengthBalance +=
                    Mathf.Abs(paths[i].Count - idealLength);

                if (turns > maxTurns)
                    lengthBalance +=
                        (turns - maxTurns) * 0.15f;
            }

            float effectiveTurnPreference =
                Mathf.Clamp01(
                    Mathf.Lerp(
                        turnedPathPreference,
                        1f,
                        turnChance));

            float turnScore =
                totalTurns *
                Mathf.Lerp(1f, 4f, effectiveTurnPreference);

            float coverageScore =
                turnedPaths * 6f * effectiveTurnPreference;

            float targetScore =
                pathsAtTarget * 7f;

            float balanceScore =
                -lengthBalance * 0.20f;

            // Encourage short-run geometry. This is what makes a 5-6 cell
            // arrow more likely to contain a visible corner instead of being
            // another long straight segment.
            float averageTurns =
                paths.Count > 0
                    ? (float)totalTurns / paths.Count
                    : 0f;

            float runScore =
                averageTurns *
                Mathf.Lerp(2f, 5f, difficultyValue);

            // Spatial packing is important for the visual problem where arrows
            // technically occupy the board but look separated because one arrow
            // owns a thin/sparse region. Score the actual shape of each arrow,
            // not just its turn count.
            float compactnessScore = ScoreSpatialCompactness(paths);

            // Adjacent arrows should form a dense cluster. Because the complete
            // board is already partitioned, this measures CONTACT BETWEEN
            // DIFFERENT ARROWS rather than empty-cell occupancy.
            float contactScore = ScoreArrowContact(paths, width, height);

            float difficultyWeight =
                Mathf.Lerp(0.45f, 1.5f, difficultyValue);

            float baseScore =
                (turnScore +
                 coverageScore +
                 targetScore +
                 balanceScore +
                 runScore) *
                difficultyWeight;

            float spatialWeight =
                Mathf.Lerp(0.20f, 1.40f, spatialCompactnessPreference);

            float contactWeight =
                Mathf.Lerp(0.20f, 1.20f, arrowContactPreference);

            return
                baseScore +
                compactnessScore * spatialWeight +
                contactScore * contactWeight +
                UnityEngine.Random.value * 0.5f;
        }

        /// <summary>
        /// Measures how compact each arrow's cells are.
        /// A straight/compact region scores near 1. A long path that spreads
        /// across a large rectangle scores lower.
        /// </summary>
        private float ScoreSpatialCompactness(
            List<List<Vector2Int>> paths)
        {
            if (paths == null || paths.Count == 0)
                return 0f;

            float total = 0f;
            int accepted = 0;

            for (int i = 0; i < paths.Count; i++)
            {
                List<Vector2Int> path = paths[i];

                if (path == null || path.Count == 0)
                    continue;

                int minX = path[0].x;
                int maxX = path[0].x;
                int minY = path[0].y;
                int maxY = path[0].y;

                for (int p = 1; p < path.Count; p++)
                {
                    Vector2Int cell = path[p];

                    minX = Mathf.Min(minX, cell.x);
                    maxX = Mathf.Max(maxX, cell.x);
                    minY = Mathf.Min(minY, cell.y);
                    maxY = Mathf.Max(maxY, cell.y);
                }

                int boxWidth = maxX - minX + 1;
                int boxHeight = maxY - minY + 1;
                int boxArea = Mathf.Max(1, boxWidth * boxHeight);

                float compactness =
                    Mathf.Clamp01((float)path.Count / boxArea);

                // Penalize very sparse arrow regions strongly.
                if (compactness < minimumArrowCompactness)
                    compactness *= 0.35f;

                total += compactness;
                accepted++;
            }

            return accepted == 0 ? 0f : total / accepted;
        }

        /// <summary>
        /// Measures how much different arrow regions touch each other.
        /// This does not count cells from the same arrow.
        /// </summary>
        private float ScoreArrowContact(
            List<List<Vector2Int>> paths,
            int width,
            int height)
        {
            if (paths == null || paths.Count < 2)
                return 0f;

            Dictionary<Vector2Int, int> owner =
                new Dictionary<Vector2Int, int>();

            for (int i = 0; i < paths.Count; i++)
            {
                List<Vector2Int> path = paths[i];

                if (path == null)
                    continue;

                for (int p = 0; p < path.Count; p++)
                    owner[path[p]] = i;
            }

            int boundaryEdges = 0;
            int differentArrowEdges = 0;

            Vector2Int[] directions =
            {
                Vector2Int.right,
                Vector2Int.up
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    int ownerA;
                    if (!owner.TryGetValue(cell, out ownerA))
                        continue;

                    for (int d = 0; d < directions.Length; d++)
                    {
                        Vector2Int other = cell + directions[d];

                        if (!IsInside(other, width, height))
                            continue;

                        int ownerB;
                        if (!owner.TryGetValue(other, out ownerB))
                            continue;

                        if (ownerA == ownerB)
                            continue;

                        boundaryEdges++;
                        differentArrowEdges++;
                    }
                }
            }

            if (boundaryEdges == 0)
                return 0f;

            return Mathf.Clamp01(
                (float)differentArrowEdges /
                Mathf.Max(1, boundaryEdges));
        }

        private int CountTurns(List<Vector2Int> path)
        {
            if (path == null || path.Count < 3)
                return 0;

            int turns = 0;

            for (int i = 1; i < path.Count - 1; i++)
            {
                Vector2Int first = path[i] - path[i - 1];
                Vector2Int second = path[i + 1] - path[i];

                if (first != second)
                    turns++;
            }

            return turns;
        }

        private int CountTotalTurns(LevelData level)
        {
            if (level == null || level.arrows == null)
                return 0;

            int total = 0;

            for (int i = 0; i < level.arrows.Count; i++)
                total += CountTurns(level.arrows[i].GetPath());

            return total;
        }

        private void GetTurnTargets(
            int width,
            int height,
            float difficultyValue,
            out int minimum,
            out int maximum)
        {
            if (!automaticComplexity)
            {
                minimum = minTurnsSmallGrid;
                maximum = maxTurnsSmallGrid;
                return;
            }

            float gridProgress = Mathf.InverseLerp(
                Mathf.Max(1, startingGridWidth),
                Mathf.Max(startingGridWidth, maxGridWidth),
                Mathf.Max(width, height));

            float complexity = Mathf.Clamp01(
                Mathf.Max(gridProgress, difficultyValue * 0.90f));

            minimum = Mathf.RoundToInt(
                Mathf.Lerp(minTurnsSmallGrid, minTurnsLargeGrid, complexity));

            maximum = Mathf.RoundToInt(
                Mathf.Lerp(maxTurnsSmallGrid, maxTurnsLargeGrid, complexity));

            maximum = Mathf.Max(minimum, maximum);
        }

        private int ChooseArrowCount(int totalCells, float difficultyValue)
        {
            int min = Mathf.Clamp(minArrows, 1, totalCells);
            int max = Mathf.Clamp(maxArrows, min, totalCells);

            int capacityMinimum = Mathf.CeilToInt(
                (float)totalCells / Mathf.Max(1, maxArrowLength));

            int capacityMaximum = Mathf.FloorToInt(
                (float)totalCells / Mathf.Max(1, minimumPathLength));

            min = Mathf.Max(min, capacityMinimum);
            max = Mathf.Min(max, capacityMaximum);

            if (min > max)
            {
                min = capacityMinimum;
                max = Mathf.Max(min, capacityMaximum);
            }

            // Automatic count follows the grid size. This deliberately stays
            // low so the board looks like a handful of substantial arrows.
            float gridSize = Mathf.Max(currentBoardWidth, currentBoardHeight);
            float gridProgress = Mathf.InverseLerp(
                Mathf.Max(1, startingGridWidth),
                Mathf.Max(startingGridWidth, maxGridWidth),
                gridSize);

            int automaticCount = Mathf.RoundToInt(
                Mathf.Lerp(5f, 13f, gridProgress));

            int difficultyExtra = Mathf.RoundToInt(
                Mathf.Lerp(0f, 2.0f, difficultyValue));

            int suggested = automaticCount + difficultyExtra;
            suggested = Mathf.Clamp(suggested, min, max);

            // Small random variation prevents every grid size from looking
            // identical while keeping the count in the intended range.
            int variation = UnityEngine.Random.value < 0.35f
                ? UnityEngine.Random.Range(-1, 2)
                : 0;

            return Mathf.Clamp(suggested + variation, min, max);
        }

        // ============================================================
        // SOLVABLE DIRECTIONS
        // ============================================================

        private bool AssignSolvableDirections(
            LevelData level,
            List<List<Vector2Int>> paths,
            float levelDifficulty)
        {
            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();

            for (int i = 0; i < paths.Count; i++)
            {
                for (int j = 0; j < paths[i].Count; j++)
                    occupied.Add(paths[i][j]);
            }

            List<int> remaining = new List<int>();

            for (int i = 0; i < paths.Count; i++)
                remaining.Add(i);

            int steps = 0;

            while (remaining.Count > 0 && steps < maxSolverSteps)
            {
                steps++;

                List<RemovalCandidate> candidates =
                    new List<RemovalCandidate>();

                for (int r = 0; r < remaining.Count; r++)
                {
                    int pathIndex = remaining[r];
                    List<Vector2Int> path = paths[pathIndex];

                    AddEndpointCandidates(
                        candidates,
                        pathIndex,
                        path,
                        false,
                        occupied);

                    if (path.Count > 1)
                    {
                        AddEndpointCandidates(
                            candidates,
                            pathIndex,
                            path,
                            true,
                            occupied);
                    }
                }

                if (candidates.Count == 0)
                    return false;

                RemovalCandidate chosen = ChooseRemovalCandidate(
                    candidates,
                    levelDifficulty);

                List<Vector2Int> finalPath =
                    new List<Vector2Int>(paths[chosen.pathIndex]);

                if (chosen.useTailAsHead)
                    finalPath.Reverse();

                ArrowSpawnData arrow = new ArrowSpawnData();
                arrow.x = chosen.head.x;
                arrow.y = chosen.head.y;
                arrow.direction = VectorToDirection(chosen.direction);
                arrow.path = finalPath;
                arrow.length = finalPath.Count;

                level.arrows.Add(arrow);

                for (int i = 0; i < finalPath.Count; i++)
                    occupied.Remove(finalPath[i]);

                remaining.Remove(chosen.pathIndex);
            }

            return remaining.Count == 0;
        }

        private void AddEndpointCandidates(
            List<RemovalCandidate> candidates,
            int pathIndex,
            List<Vector2Int> path,
            bool useTailAsHead,
            HashSet<Vector2Int> occupied)
        {
            Vector2Int head = useTailAsHead
                ? path[path.Count - 1]
                : path[0];

            Vector2Int bodyStep = Vector2Int.zero;

            if (path.Count > 1)
            {
                Vector2Int nextBody = useTailAsHead
                    ? path[path.Count - 2]
                    : path[1];

                bodyStep = nextBody - head;
            }

            Vector2Int[] directions;

            if (path.Count > 1)
            {
                directions = new[] { -bodyStep };
            }
            else
            {
                directions = new[]
                {
                    Vector2Int.up,
                    Vector2Int.down,
                    Vector2Int.left,
                    Vector2Int.right
                };
            }

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int direction = directions[i];

                if (direction == Vector2Int.zero)
                    continue;

                if (!IsRayClear(head, direction, occupied))
                    continue;

                int clearCells = CountClearCells(
                    head,
                    direction,
                    occupied);

                int pressure = CountOccupiedNeighbors(
                    head,
                    occupied);

                candidates.Add(
                    new RemovalCandidate(
                        pathIndex,
                        useTailAsHead,
                        head,
                        direction,
                        clearCells,
                        pressure));
            }
        }

        private int CountOccupiedNeighbors(
            Vector2Int position,
            HashSet<Vector2Int> occupied)
        {
            int count = 0;

            Vector2Int[] directions =
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            for (int i = 0; i < directions.Length; i++)
            {
                if (occupied.Contains(position + directions[i]))
                    count++;
            }

            return count;
        }

        private RemovalCandidate ChooseRemovalCandidate(
            List<RemovalCandidate> candidates,
            float levelDifficulty)
        {
            if (candidates == null || candidates.Count == 0)
                throw new ArgumentException(
                    "No removal candidates were supplied.");

            float bestScore = float.NegativeInfinity;
            List<RemovalCandidate> preferred =
                new List<RemovalCandidate>();

            for (int i = 0; i < candidates.Count; i++)
            {
                RemovalCandidate candidate = candidates[i];

                // Lower clearance means the arrow is escaping through a
                // tighter route. Higher pressure means it is surrounded by
                // other cells. Both make the final board visually denser.
                float clearanceScore =
                    1f / (1f + candidate.clearCells);

                float pressureScore =
                    candidate.pressure / 4f;

                float score =
                    Mathf.Lerp(
                        clearanceScore,
                        clearanceScore * 0.70f +
                        pressureScore * 0.30f,
                        Mathf.Clamp01(
                            interactionPreference *
                            levelDifficulty));

                // Preserve some randomness so repeated generation does not
                // collapse to the same removal ordering.
                score += UnityEngine.Random.value * 0.06f;

                if (score > bestScore + 0.045f)
                {
                    bestScore = score;
                    preferred.Clear();
                    preferred.Add(candidate);
                }
                else if (Mathf.Abs(score - bestScore) <= 0.045f)
                {
                    preferred.Add(candidate);
                }
            }

            return preferred[
                UnityEngine.Random.Range(
                    0,
                    preferred.Count)];
        }

        private bool IsRayClear(
            Vector2Int head,
            Vector2Int direction,
            HashSet<Vector2Int> occupied)
        {
            Vector2Int cursor = head + direction;

            while (IsInside(cursor, currentBoardWidth, currentBoardHeight))
            {
                if (occupied.Contains(cursor))
                    return false;

                cursor += direction;
            }

            return true;
        }

        private int CountClearCells(
            Vector2Int head,
            Vector2Int direction,
            HashSet<Vector2Int> occupied)
        {
            int count = 0;
            Vector2Int cursor = head + direction;

            while (IsInside(cursor, currentBoardWidth, currentBoardHeight))
            {
                if (occupied.Contains(cursor))
                    break;

                count++;
                cursor += direction;
            }

            return count;
        }

        // ============================================================
        // VALIDATION
        // ============================================================

        private bool ValidateLevel(LevelData level)
        {
            if (level == null ||
                level.width <= 0 ||
                level.height <= 0 ||
                level.arrows == null ||
                level.arrows.Count == 0)
            {
                return false;
            }

            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();

            for (int i = 0; i < level.arrows.Count; i++)
            {
                ArrowSpawnData arrow = level.arrows[i];
                List<Vector2Int> path = arrow.GetPath();

                if (path == null || path.Count == 0)
                    return false;

                if (path[0] != new Vector2Int(arrow.x, arrow.y))
                    return false;

                if (path.Count != Mathf.Max(1, arrow.length))
                    return false;

                for (int p = 0; p < path.Count; p++)
                {
                    Vector2Int cell = path[p];

                    if (!IsInside(cell, level.width, level.height))
                        return false;

                    if (!occupied.Add(cell))
                        return false;

                    if (p > 0)
                    {
                        Vector2Int delta = path[p] - path[p - 1];

                        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
                            return false;
                    }
                }

                if (path.Count > 1)
                {
                    Vector2Int expectedDirection = path[0] - path[1];

                    if (arrow.direction.ToVector() != expectedDirection)
                        return false;
                }
            }

            if (occupied.Count != level.width * level.height)
                return false;

            return SimulateLevel(level);
        }

        private int CountOccupiedCells(LevelData level)
        {
            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();

            if (level == null || level.arrows == null)
                return 0;

            for (int i = 0; i < level.arrows.Count; i++)
            {
                List<Vector2Int> path = level.arrows[i].GetPath();

                if (path == null)
                    continue;

                for (int p = 0; p < path.Count; p++)
                    occupied.Add(path[p]);
            }

            return occupied.Count;
        }

        private bool SimulateLevel(LevelData level)
        {
            List<SimArrow> arrows = new List<SimArrow>();
            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();

            for (int i = 0; i < level.arrows.Count; i++)
            {
                ArrowSpawnData data = level.arrows[i];
                List<Vector2Int> path = data.GetPath();

                SimArrow arrow = new SimArrow();
                arrow.path = new List<Vector2Int>(path);
                arrow.direction = data.direction;
                arrows.Add(arrow);

                for (int p = 0; p < path.Count; p++)
                    occupied.Add(path[p]);
            }

            int safety = 0;
            int removed = 0;

            while (removed < arrows.Count && safety < maxSolverSteps)
            {
                safety++;
                bool progress = false;

                for (int i = 0; i < arrows.Count; i++)
                {
                    if (arrows[i].removed)
                        continue;

                    Vector2Int head = arrows[i].path[0];
                    Vector2Int step = arrows[i].direction.ToVector();
                    Vector2Int cursor = head + step;
                    bool clear = true;

                    while (IsInside(cursor, level.width, level.height))
                    {
                        if (occupied.Contains(cursor))
                        {
                            clear = false;
                            break;
                        }

                        cursor += step;
                    }

                    if (!clear)
                        continue;

                    arrows[i].removed = true;
                    removed++;
                    progress = true;

                    for (int p = 0; p < arrows[i].path.Count; p++)
                        occupied.Remove(arrows[i].path[p]);
                }

                if (!progress)
                    break;
            }

            return removed == arrows.Count;
        }

        private float ScoreGeneratedLevel(
            LevelData level,
            float difficultyValue)
        {
            if (level == null ||
                level.arrows == null ||
                level.arrows.Count == 0)
            {
                return 0f;
            }

            int totalTurns = CountTotalTurns(level);
            int turnedArrows = 0;
            int blockedArrows = 0;
            int heavilyBlockedArrows = 0;
            int totalHeadPressure = 0;

            HashSet<Vector2Int> occupied =
                new HashSet<Vector2Int>();

            for (int i = 0; i < level.arrows.Count; i++)
            {
                List<Vector2Int> path =
                    level.arrows[i].GetPath();

                if (path == null)
                    continue;

                for (int p = 0; p < path.Count; p++)
                    occupied.Add(path[p]);

                if (CountTurns(path) > 0)
                    turnedArrows++;
            }

            for (int i = 0; i < level.arrows.Count; i++)
            {
                ArrowSpawnData arrow = level.arrows[i];
                Vector2Int head =
                    new Vector2Int(arrow.x, arrow.y);

                Vector2Int direction =
                    arrow.direction.ToVector();

                int blockers = CountRayBlockers(
                    head,
                    direction,
                    occupied);

                if (blockers > 0)
                    blockedArrows++;

                if (blockers >= 2)
                    heavilyBlockedArrows++;

                totalHeadPressure +=
                    CountOccupiedNeighbors(
                        head,
                        occupied);
            }

            float arrowCount =
                Mathf.Max(1, level.arrows.Count);

            float turnTarget =
                Mathf.Max(
                    1f,
                    Mathf.Lerp(
                        0.8f,
                        Mathf.Max(
                            2.5f,
                            maxTurnsLargeGrid),
                        difficultyValue));

            float turnDensity =
                Mathf.Clamp01(
                    (totalTurns / arrowCount) /
                    turnTarget);

            float turnedCoverage =
                turnedArrows / arrowCount;

            float blockedRatio =
                blockedArrows / arrowCount;

            float heavyBlockedRatio =
                heavilyBlockedArrows / arrowCount;

            float headPressure =
                Mathf.Clamp01(
                    (totalHeadPressure / arrowCount) / 3f);

            float score =
                turnDensity * 0.32f +
                turnedCoverage * 0.16f +
                blockedRatio * 0.30f +
                heavyBlockedRatio * 0.12f +
                headPressure * 0.10f;

            return Mathf.Clamp01(score);
        }

        private float GetRequiredComplexityScore(
            float difficultyValue)
        {
            // Early levels remain readable. Later levels must prove that
            // their final arrows actually interact instead of merely being
            // long/curved.
            float required = Mathf.Lerp(
                minimumComplexityScore,
                0.72f,
                difficultyValue);

            return Mathf.Clamp01(required);
        }

        private int CountRayBlockers(
            Vector2Int head,
            Vector2Int direction,
            HashSet<Vector2Int> occupied)
        {
            int count = 0;
            Vector2Int cursor = head + direction;

            while (IsInside(
                cursor,
                currentBoardWidth,
                currentBoardHeight))
            {
                if (occupied.Contains(cursor))
                    count++;

                cursor += direction;
            }

            return count;
        }

        // ============================================================
        // GRID SIZE / DIFFICULTY
        // ============================================================

        private void SetGridSizeForLevel(int levelIndex)
        {
            int width = boardWidth;
            int height = boardHeight;

            if (automaticGridProgression)
            {
                int step = levelsPerGridIncrease > 0
                    ? levelIndex / levelsPerGridIncrease
                    : 0;

                width = startingGridWidth + step;
                height = startingGridHeight + step;
            }

            width = Mathf.Clamp(width, 1, maxGridWidth);
            height = Mathf.Clamp(height, 1, maxGridHeight);

            if (gridPanel != null)
            {
                float availableWidth = Mathf.Max(
                    1f,
                    gridPanel.rect.width - gridPanelPadding * 2f);

                float availableHeight = Mathf.Max(
                    1f,
                    gridPanel.rect.height - gridPanelPadding * 2f);

                int maxFitWidth = Mathf.Max(
                    1,
                    Mathf.FloorToInt(
                        (availableWidth + gridCellSpacing) /
                        Mathf.Max(1f, gridCellSize + gridCellSpacing)));

                int maxFitHeight = Mathf.Max(
                    1,
                    Mathf.FloorToInt(
                        (availableHeight + gridCellSpacing) /
                        Mathf.Max(1f, gridCellSize + gridCellSpacing)));

                width = Mathf.Min(width, maxFitWidth);
                height = Mathf.Min(height, maxFitHeight);
            }

            currentBoardWidth = width;
            currentBoardHeight = height;
        }

        private float GetLevelDifficulty(int levelIndex)
        {
            float targetDifficulty = GetPresetDifficulty();

            if (!progressiveDifficulty)
            {
                targetDifficulty += UnityEngine.Random.Range(
                    -difficultyVariation,
                    difficultyVariation);

                return Mathf.Clamp01(targetDifficulty);
            }

            // Infinite progression: difficulty approaches the selected preset
            // asymptotically instead of depending on a fixed number of levels.
            // This means Level 1 is readable, while later levels continue getting
            // harder without ever needing a maximum level number.
            float progress = 1f - Mathf.Exp(-Mathf.Max(0, levelIndex) / 18f);

            float baseDifficulty = Mathf.Lerp(
                0.30f,
                targetDifficulty,
                progress);

            baseDifficulty += UnityEngine.Random.Range(
                -difficultyVariation,
                difficultyVariation);

            return Mathf.Clamp01(baseDifficulty);
        }

        private float GetPresetDifficulty()
        {
            switch (difficulty)
            {
                case DifficultyPreset.VeryEasy: return 0.20f;
                case DifficultyPreset.Easy: return 0.35f;
                case DifficultyPreset.Medium: return 0.55f;
                case DifficultyPreset.Hard: return 0.75f;
                case DifficultyPreset.VeryHard: return 0.95f;
                case DifficultyPreset.Custom: return blockingChance;
                default: return 0.55f;
            }
        }

        // ============================================================
        // PERSISTENCE
        // ============================================================

        // ============================================================
        // HELPERS
        // ============================================================

        private bool IsInside(Vector2Int pos, int width, int height)
        {
            return pos.x >= 0 &&
                   pos.x < width &&
                   pos.y >= 0 &&
                   pos.y < height;
        }

        private ArrowDirection VectorToDirection(Vector2Int direction)
        {
            if (direction == Vector2Int.up)
                return ArrowDirection.Up;

            if (direction == Vector2Int.down)
                return ArrowDirection.Down;

            if (direction == Vector2Int.left)
                return ArrowDirection.Left;

            return ArrowDirection.Right;
        }

        private string CreateLevelSignature(LevelData level)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append(level.width)
                .Append('x')
                .Append(level.height)
                .Append('|');

            for (int i = 0; i < level.arrows.Count; i++)
            {
                ArrowSpawnData arrow = level.arrows[i];

                builder.Append(arrow.x)
                    .Append(',')
                    .Append(arrow.y)
                    .Append(',')
                    .Append((int)arrow.direction)
                    .Append(':');

                List<Vector2Int> path = arrow.GetPath();

                for (int p = 0; p < path.Count; p++)
                {
                    builder.Append(path[p].x)
                        .Append(',')
                        .Append(path[p].y)
                        .Append(';');
                }

                builder.Append('|');
            }

            return builder.ToString();
        }

        private void DestroyRuntimeLevel(LevelData level)
        {
            if (level == null)
                return;

            if (Application.isPlaying)
                Destroy(level);
            else
                DestroyImmediate(level);
        }

        private void ValidateSettings()
        {
            boardWidth = Mathf.Max(1, boardWidth);
            boardHeight = Mathf.Max(1, boardHeight);

            startingGridWidth = Mathf.Max(1, startingGridWidth);
            startingGridHeight = Mathf.Max(1, startingGridHeight);
            levelsPerGridIncrease = Mathf.Max(1, levelsPerGridIncrease);

            maxGridWidth = Mathf.Max(startingGridWidth, maxGridWidth);
            maxGridHeight = Mathf.Max(startingGridHeight, maxGridHeight);

            minArrows = Mathf.Max(1, minArrows);
            maxArrows = Mathf.Max(minArrows, maxArrows);

            minimumPathLength = Mathf.Max(1, minimumPathLength);
            maxArrowLength = Mathf.Max(minimumPathLength, maxArrowLength);

            int maximumBoardDimension =
                Mathf.Max(maxGridWidth, maxGridHeight);

            maxArrowLength = Mathf.Clamp(
                maxArrowLength,
                1,
                maximumBoardDimension * maximumBoardDimension);

            minTurnsSmallGrid = Mathf.Max(0, minTurnsSmallGrid);
            maxTurnsSmallGrid = Mathf.Max(minTurnsSmallGrid, maxTurnsSmallGrid);
            minTurnsLargeGrid = Mathf.Max(0, minTurnsLargeGrid);
            maxTurnsLargeGrid = Mathf.Max(minTurnsLargeGrid, maxTurnsLargeGrid);

            shapeSearchAttempts = Mathf.Clamp(shapeSearchAttempts, 1, 200);
            interactionPreference = Mathf.Clamp01(interactionPreference);
            minimumComplexityScore = Mathf.Clamp01(minimumComplexityScore);

            spatialCompactnessPreference =
                Mathf.Clamp01(spatialCompactnessPreference);

            arrowContactPreference =
                Mathf.Clamp01(arrowContactPreference);

            minimumArrowCompactness =
                Mathf.Clamp01(minimumArrowCompactness);

            compactnessSearchAttempts =
                Mathf.Clamp(compactnessSearchAttempts, 1, 300);
            interlockedPathAttempts = Mathf.Clamp(
                interlockedPathAttempts,
                1,
                40);
            interlockedPathNodeBudget = Mathf.Clamp(
                interlockedPathNodeBudget,
                1000,
                200000);
            maxAttemptsPerLevel = Mathf.Max(1, maxAttemptsPerLevel);
            maxSolverSteps = Mathf.Max(100, maxSolverSteps);

            gridCellSize = Mathf.Max(1f, gridCellSize);
            gridCellSpacing = Mathf.Max(0f, gridCellSpacing);
            gridPanelPadding = Mathf.Max(0f, gridPanelPadding);
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private sealed class SimArrow
        {
            public List<Vector2Int> path;
            public ArrowDirection direction;
            public bool removed;
        }

        private readonly struct PathChoice
        {
            public readonly Vector2Int position;
            public readonly float score;

            public PathChoice(
                Vector2Int position,
                float score)
            {
                this.position = position;
                this.score = score;
            }
        }

        private readonly struct RemovalCandidate
        {
            public readonly int pathIndex;
            public readonly bool useTailAsHead;
            public readonly Vector2Int head;
            public readonly Vector2Int direction;
            public readonly int clearCells;
            public readonly int pressure;

            public RemovalCandidate(
                int pathIndex,
                bool useTailAsHead,
                Vector2Int head,
                Vector2Int direction,
                int clearCells,
                int pressure)
            {
                this.pathIndex = pathIndex;
                this.useTailAsHead = useTailAsHead;
                this.head = head;
                this.direction = direction;
                this.clearCells = clearCells;
                this.pressure = pressure;
            }
        }
    }
}