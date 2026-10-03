using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using OMG;

/// <summary>
/// Single-script controller for a "2048 Drop Merge" game (Tetris-style auto-fall,
/// NOT the classic sliding-grid 2048 and NOT drag-then-release).
///
/// Flow: one block spawns at a RANDOM column in the top row -> it starts falling
/// straight down IMMEDIATELY and continuously, at constant speed -> while it falls,
/// the player can tap/drag anywhere on screen to retarget which column it's heading
/// into (it keeps falling the whole time, just shifts columns) -> when it reaches the
/// floor or lands on another block in its current column, it locks in place -> equal-value
/// neighbors BELOW or to the LEFT/RIGHT merge (the neighbor slides into the surviving block,
/// doubles, punch-scales) -> chain merges resolve -> unsupported blocks slide down to fill
/// gaps -> re-check merges -> once fully stable, spawn the next block (random column again)
/// -> if a new block can't spawn (top row occupied), Game Over.
///
/// POWER-UP: "Destroy Block" - a UI button freezes the whole board (no falling, no steering,
/// no hard-drop), the player taps any landed block to remove it, the column collapses, any
/// merges the collapse reveals are resolved, and then the block that was already falling
/// before the power-up simply resumes from where it paused. See the
/// "POWER-UP: DESTROY BLOCK" region near the bottom.
///
/// INPUT NOTE: this reads Input.mousePosition / Input.GetTouch directly in Update(), instead
/// of using per-block IPointerDown/Drag/Up handlers, since there's only ever one active block
/// and a single Update() poll is simpler/more reliable than per-object UI events.
///
/// If your project's Project Settings > Player > Active Input Handling is set to
/// "Input System Package (New)" only, switch it to "Both" (or "Input Manager (Old)"),
/// otherwise UnityEngine.Input calls will throw at runtime.
///
/// POSITIONING NOTE: all vertical movement is computed from the ACTUAL anchoredPosition
/// of your gridCells (never from rect.height/width), so it doesn't depend on layout timing.
/// This does mean your Block prefab's RectTransform anchors/pivot MUST match your grid
/// cell prefab's anchors/pivot (e.g. both centered), otherwise the same anchoredPosition
/// value will render in different visual spots between the two.
/// </summary>
namespace OMG.MergeIT
{
[Serializable]
public struct ValueColorEntry
{
    public int value;
    public Color color;
}

public class BoardManager : MonoBehaviour
{
    //[SerializeField] ParentManager parent;
    public static BoardManager Instance { get; private set; }

    // ----------------------------------------------------------------
    // INSPECTOR FIELDS
    // ----------------------------------------------------------------

    private const int Rows = 6;
    private const int Cols = 6;

    [Header("Grid Setup")]
    [Tooltip("The 36 existing cell RectTransforms, used only as target positions. " +
             "Assign in row-major order starting at the TOP ROW: " +
             "index 0-5 = top row, left-to-right (this is also the spawn row), " +
             "index 6-11 = second row from the top, ... " +
             "index 30-35 = bottom row (left-to-right).")]
    [SerializeField] private RectTransform[] gridCells = new RectTransform[Rows * Cols];

    [Tooltip("Parent RectTransform blocks are instantiated under. Must share the same canvas " +
             "space as gridCells so anchored positions line up directly.")]
    [SerializeField] private RectTransform boardRoot;

    [Header("Block Prefab")]
    [Tooltip("Prefab with your Block script (+ TMP_Text + Image). Its RectTransform anchors " +
             "and pivot MUST match the grid cell prefab's, or spawn/landing positions will be off.")]
    [SerializeField] private Block blockPrefab;


    [Header("Game Over")]
    [SerializeField] private GameObject gameOverPanel;

    [Header("Pause")]
    [SerializeField] private GameObject pauseScreen;

    [Header("Music")]
    [SerializeField] private AudioSource bgSound;
    private AudioManager audioManager;
    [SerializeField] private GameObject musicIcon;     // Show when music is ON
    [SerializeField] private GameObject muteIcon;      // Show when music is OFF
    [Header("SFX (Sound Effects)")] // <-- ADD THIS HEADER AND VARIABLES
    //[SerializeField] private GameObject sfxOnIcon;     // Assign your "Sound On" image/button here
    //[SerializeField] private GameObject sfxOffIcon;    // Assign your "Sound Off" image/button here
    //private bool isSfxOn = true; // Runtime state
    private const string SfxPrefsKey = "MergeIt_SFX"; 

    [Header("SFX Clips (Active)")]
    [Tooltip("AudioSource used to play one-shot SFX. Can be a separate component on this GameObject.")]
    [SerializeField] private AudioSource sfxSource;
    [Tooltip("Played whenever a cluster of matching blocks merges.")]
    [SerializeField] private AudioClip mergeSound;
    [Tooltip("Played whenever the falling block is steered into a new column by touch/drag.")]
    [SerializeField] private AudioClip moveSound;
    [Tooltip("Played when a block is popped/destroyed via the Destroy Block power-up.")]
    [SerializeField] private AudioClip destroySound;
    [Tooltip("Played whenever a UI button is pressed (Pause, Restart, Music, Quit, Power-up, etc.).")]
    [SerializeField] private AudioClip buttonClickSound;

    [Header("Score")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text highScoreText;

    [Header("Animation Tuning")]
    [Tooltip("Rows fallen per second while auto-dropping.")]
    [SerializeField] private float fallSpeedCellsPerSecond = 4f;
    [Tooltip("Duration (s) of the horizontal steer-follow smoothing.")]
    [SerializeField] private float dragSmoothTime = 0.08f;
    [Tooltip("Duration (s) for a merge move (upper block sliding into lower block).")]
    [SerializeField] private float mergeMoveDuration = 0.18f;
    [Tooltip("Duration (s) for blocks collapsing down into a gap.")]
    [SerializeField] private float collapseMoveDuration = 0.15f;
    [Tooltip("Chance (0-1) a newly spawned block is a 4 instead of a 2.")]
    [SerializeField, Range(0f, 1f)] private float spawnFourChance = 0.1f;

    [Header("Block Colors")]
    [Tooltip("Color used for each block value. Add/edit entries here. Any value not listed " +
             "falls back to an auto-generated color.")]
    [SerializeField]
    private List<ValueColorEntry> blockColors = new List<ValueColorEntry>
    {
        new ValueColorEntry { value = 2,    color = new Color(0.90f, 0.90f, 0.90f) },
        new ValueColorEntry { value = 4,    color = new Color(0.72f, 0.93f, 0.68f) },
        new ValueColorEntry { value = 8,    color = new Color(0.55f, 0.93f, 0.93f) },
        new ValueColorEntry { value = 16,   color = new Color(0.30f, 0.55f, 0.95f) },
        new ValueColorEntry { value = 32,   color = new Color(0.65f, 0.35f, 0.90f) },
        new ValueColorEntry { value = 64,   color = new Color(1.00f, 0.55f, 0.15f) },
        new ValueColorEntry { value = 128,  color = new Color(0.95f, 0.25f, 0.25f) },
        new ValueColorEntry { value = 256,  color = new Color(0.95f, 0.45f, 0.70f) },
        new ValueColorEntry { value = 512,  color = new Color(1.00f, 0.85f, 0.25f) },
        new ValueColorEntry { value = 1024, color = new Color(0.15f, 0.65f, 0.60f) },
        new ValueColorEntry { value = 2048, color = new Color(1.00f, 0.84f, 0.00f) },
    };

    [Header("Power-Up: Destroy Block")]
    [Tooltip("Optional UI element (e.g. a text/panel saying 'Tap a block to destroy') that is " +
             "shown while the player is choosing a block to destroy. Leave empty if you don't want one.")]
    [SerializeField] private GameObject powerUpSelectionHint;

    // ----------------------------------------------------------------
    // RUNTIME STATE
    // ----------------------------------------------------------------

    /// <summary>board[row, col]; row 0 = bottom, row (Rows-1) = top.</summary>
    private Block[,] board = new Block[Rows, Cols];
    private Dictionary<int, Color> valueColors;

    private Block activeBlock;
    private int activeColumn;
    private float currentFallY;      // live anchoredPosition.y of the falling block
    private bool inputLocked;
    private bool isGameOver;
    private bool isSteeringActive;   // true while the block is mid-slide toward a new column - fall pauses

    private bool isMusicOn;

    private int score;
    private int highScore;
    private const string HighScoreKey = "MergeIt_HighScore";

    private float topRowY;
    private float rowSpacing;        // pixel distance between adjacent rows (from actual positions)
    private float[] columnX;

    // Unified pointer state, refreshed once per frame in Update().
    private bool pointerActive;      // true while mouse/touch is down/held this frame
    private Vector2 pointerScreenPos;

    // True while the board is frozen and waiting for the player to tap a block to destroy.
    private bool isPowerUpDestroySelecting;

    // ----------------------------------------------------------------
    // UNITY LIFECYCLE
    // ----------------------------------------------------------------

    private void Awake()
    {
        //parent = GameObject.Find("ParentManager").GetComponent<ParentManager>();
        bgSound = GetComponent<AudioSource>();
        bgSound.volume = 1f;
        audioManager = FindObjectOfType<AudioManager>();
    
        Instance = this;
        BuildColorMap();
        CacheGridMetrics();

        pauseScreen.SetActive(false);
    }

    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        UpdateScoreUI();
        // --- MUSIC INIT ---
        isMusicOn = PlayerPrefs.GetInt("Music", 1) == 1;
        ApplyMusicState();
        // --- SFX INIT (ADD THIS BLOCK) ---
        // isSfxOn = PlayerPrefs.GetInt(SfxPrefsKey, 1) == 1;
        //ApplySFXState(); // Updates the Audio Manager and UI icons immediately
        SpawnBlock();
    }

    private void Update()
    {
        bool wasPointerActive = pointerActive;
        ReadPointerInput();

        if (isPowerUpDestroySelecting)
        {
            // Board is fully frozen while the player picks a block: no steering, no auto-fall,
            // no hard-drop. Only look for a fresh pointer-down (tap) to pick a block.
            if (!wasPointerActive && pointerActive)
            {
                TryDestroyBlockAtPointer();
            }
            return;
        }

        HandleSteering();

        if (wasPointerActive && !pointerActive)
        {
            HardDrop();
        }

        HandleAutoFall();
    }

    // ----------------------------------------------------------------
    // SETUP HELPERS
    // ----------------------------------------------------------------

    private void BuildColorMap()
    {
        valueColors = new Dictionary<int, Color>();
        foreach (ValueColorEntry entry in blockColors)
        {
            valueColors[entry.value] = entry.color;
        }
    }

    private void CacheGridMetrics()
    {
        if (gridCells == null || gridCells.Length != Rows * Cols)
        {
            Debug.LogError($"BoardManager: gridCells must contain exactly {Rows * Cols} entries " +
                            $"(found {(gridCells == null ? 0 : gridCells.Length)}). Assign all 36 cells in the Inspector.");
            return;
        }

        // gridCells live under a GridLayoutGroup, which only assigns real anchoredPosition
        // values to its children during a layout rebuild pass - that pass hasn't necessarily
        // run yet this early (Awake), so force it now or every cell reads back as (0,0).
        if (gridCells[0].parent is RectTransform cellsParent)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(cellsParent);
        }

        topRowY = gridCells[0].anchoredPosition.y;
        float secondRowY = gridCells[Cols].anchoredPosition.y; // row index Rows-2, col 0
        rowSpacing = Mathf.Abs(topRowY - secondRowY);

        columnX = new float[Cols];
        for (int c = 0; c < Cols; c++)
        {
            columnX[c] = gridCells[c].anchoredPosition.x;
        }
    }

    // ----------------------------------------------------------------
    // GRID / COORDINATE UTILITIES
    // ----------------------------------------------------------------

    /// <summary>
    /// Maps a logical board row (0 = bottom, Rows-1 = top) to an index into gridCells,
    /// which is stored TOP ROW FIRST: gridCells[0..Cols-1] = top row, gridCells[(Rows-1)*Cols .. ] = bottom row.
    /// </summary>
    private int CellIndex(int row, int col) => (Rows - 1 - row) * Cols + col;

    private Vector2 GetCellAnchoredPosition(int row, int col) => gridCells[CellIndex(row, col)].anchoredPosition;

    private int GetColumnFromX(float x)
    {
        int nearest = 0;
        float bestDist = Mathf.Abs(columnX[0] - x);
        for (int c = 1; c < Cols; c++)
        {
            float dist = Mathf.Abs(columnX[c] - x);
            if (dist < bestDist) { bestDist = dist; nearest = c; }
        }
        return nearest;
    }

    /// <summary>Lowest empty row in a column (0 = bottom). -1 if the column is full.</summary>
    private int GetLowestEmptyRow(int col)
    {
        for (int row = 0; row < Rows; row++)
        {
            if (board[row, col] == null) return row;
        }
        return -1;
    }

    /// <summary>
    /// True if a block currently at screen-height y could occupy column col without overlapping
    /// that column's existing stack. Used while steering, so the falling block can't be dragged
    /// sideways straight through a tall stack sitting in a column between it and the target.
    /// </summary>
    private bool IsColumnOpenAtHeight(int col, float y)
    {
        int lowestEmptyRow = GetLowestEmptyRow(col);
        if (lowestEmptyRow == -1) return false; // column full - never enterable
        float landingY = GetCellAnchoredPosition(lowestEmptyRow, col).y;
        return y >= landingY;
    }

    public Color GetColorForValue(int value)
    {
        if (valueColors.TryGetValue(value, out Color c)) return c;

        int power = Mathf.RoundToInt(Mathf.Log(value, 2));
        float hue = (power * 0.11f) % 1f;
        return Color.HSVToRGB(hue, 0.55f, 0.95f);
    }

    // ----------------------------------------------------------------
    // SCORE
    // ----------------------------------------------------------------

    private void AddScore(int points)
    {
        score += points;
        if (score > highScore)
        {
            highScore = score;
            PlayerPrefs.SetInt(HighScoreKey, highScore);
        }
        UpdateScoreUI();
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = score.ToString();
        if (highScoreText != null) highScoreText.text = highScore.ToString();
    }

    // ----------------------------------------------------------------
    // INPUT (unified mouse + touch, polled once per frame)
    // ----------------------------------------------------------------

    private void ReadPointerInput()
    {
        pointerActive = false;

        if (Input.touchSupported && Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            pointerScreenPos = touch.position;

            switch (touch.phase)
            {
                case TouchPhase.Began:
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    pointerActive = true;
                    break;
            }
        }
        else if (Input.GetMouseButton(0))
        {
            pointerScreenPos = Input.mousePosition;
            pointerActive = true;
        }
    }

    /// <summary>
    /// While the pointer is down/held anywhere on screen, continuously retarget the active
    /// block's column based on pointer X. Falling pauses for the duration of the horizontal
    /// slide (see isSteeringActive / HandleAutoFall) so moving left/right doesn't eat into the
    /// drop, then resumes once the block has settled into its new column.
    /// </summary>
    private void HandleSteering()
    {
        if (inputLocked || activeBlock == null || !pointerActive) return;

        Canvas canvas = boardRoot.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRoot, pointerScreenPos, cam, out Vector2 localPoint))
        {
            return;
        }

        float minX = columnX[0];
        float maxX = columnX[Cols - 1];
        float clampedX = Mathf.Clamp(localPoint.x, minX, maxX);
        int desiredColumn = GetColumnFromX(clampedX);

        if (desiredColumn == activeColumn) return;

        // Walk one column at a time toward desiredColumn, stopping just short of any column
        // whose stack currently reaches this block's height - it can't skip sideways past it.
        int step = desiredColumn > activeColumn ? 1 : -1;
        int reachableColumn = activeColumn;
        for (int c = activeColumn + step; ; c += step)
        {
            if (!IsColumnOpenAtHeight(c, currentFallY)) break;
            reachableColumn = c;
            if (c == desiredColumn) break;
        }

        if (reachableColumn == activeColumn) return;
        activeColumn = reachableColumn;

        PlaySfx(moveSound); // SFX: block steered into a new column

        RectTransform rt = activeBlock.RectTransform;
        rt.DOKill(); // Y is never tweened (set manually every frame in HandleAutoFall), so this only ever
                     // affects the X steering tween below - safe to kill outright.
        isSteeringActive = true;
        rt.DOAnchorPosX(columnX[activeColumn], dragSmoothTime).SetEase(Ease.OutQuad)
            .OnComplete(() => isSteeringActive = false);
    }

    // ----------------------------------------------------------------
    // SPAWNING
    // ----------------------------------------------------------------

    private void SpawnBlock()
    {
        if (isGameOver) return;

        activeColumn = UnityEngine.Random.Range(0, Cols);
        currentFallY = topRowY;
        isSteeringActive = false;

        Block newBlock = Instantiate(blockPrefab, boardRoot);
        RectTransform rt = newBlock.RectTransform != null ? newBlock.RectTransform : newBlock.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(columnX[activeColumn], topRowY);

        int value = (UnityEngine.Random.value < spawnFourChance) ? 4 : 2;
        newBlock.Init(value, Rows, activeColumn, this); // Row = Rows marks "pending, not on board yet"
        newBlock.PlaySpawnPop();

        activeBlock = newBlock;
        inputLocked = false;
    }

    // ----------------------------------------------------------------
    // AUTO FALL (continuous, every frame - no drag/release needed)
    // ----------------------------------------------------------------

    /// <summary>
    /// Instantly completes the fall the moment the player releases the pointer - waiting out
    /// the rest of the slow auto-fall after they've already picked a column feels laggy.
    /// </summary>
    private void HardDrop()
    {
        if (inputLocked || activeBlock == null) return;

        int landingRow = GetLowestEmptyRow(activeColumn);
        if (landingRow == -1)
        {
            TriggerGameOver();
            return;
        }

        float landingY = GetCellAnchoredPosition(landingRow, activeColumn).y;
        currentFallY = landingY;

        RectTransform rt = activeBlock.RectTransform;
        rt.DOKill(); // may cut off an in-flight steering tween without firing its OnComplete
        isSteeringActive = false;
        rt.anchoredPosition = new Vector2(columnX[activeColumn], landingY);

        LandActiveBlock(landingRow, activeColumn);
    }

    private void HandleAutoFall()
    {
        if (inputLocked || activeBlock == null || isSteeringActive) return;

        int landingRow = GetLowestEmptyRow(activeColumn);
        if (landingRow == -1)
        {
            TriggerGameOver();
            return;
        }

        float landingY = GetCellAnchoredPosition(landingRow, activeColumn).y;
        float fallSpeedPixelsPerSecond = fallSpeedCellsPerSecond * rowSpacing;

        currentFallY -= fallSpeedPixelsPerSecond * Time.deltaTime;

        bool reachedLanding = currentFallY <= landingY;
        if (reachedLanding) currentFallY = landingY;

        RectTransform rt = activeBlock.RectTransform;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, currentFallY);

        if (reachedLanding)
        {
            LandActiveBlock(landingRow, activeColumn);
        }
    }

    private void LandActiveBlock(int landingRow, int col)
    {
        Block landedBlock = activeBlock;
        activeBlock = null;
        inputLocked = true;

        landedBlock.RectTransform.DOKill(); // may cut off an in-flight steering tween without firing its OnComplete
        isSteeringActive = false;
        landedBlock.Row = landingRow;
        landedBlock.Col = col;
        board[landingRow, col] = landedBlock;

        mergeCheckQueue.Clear();
        mergeCheckQueue.Enqueue(landedBlock);
        ResolveNext(FinalizeTurn);
    }

    // ----------------------------------------------------------------
    // MERGING (with chain reactions) + COLLAPSE
    // Runs only after a landing/merge/collapse completes - never per-frame.
    //
    // Every merge is anchored on a specific "just settled" block (the one that either just
    // landed, or just dropped into a new row via collapse): we flood-fill out from it across
    // every 4-directionally connected block sharing its value (the whole "cluster", not just
    // one neighbor), and ALL of them slide into the anchor and merge at once - the anchor
    // itself never moves. A cluster of N blocks becomes a single block worth (value * the
    // next power of two >= N): 2 blocks -> x2 (normal), 3 or 4 blocks -> x4 (there's no "x3"
    // value, and x4 matches what four pairwise merges would produce anyway). After a merge,
    // collapse fills the gaps left behind, which can drop the anchor and/or other unrelated
    // blocks elsewhere on the board; every block that moves becomes a new anchor to re-check,
    // so chains fully resolve regardless of where they happen.
    //
    // ResolveNext takes an "onAllResolved" callback instead of hard-coding what happens once
    // the queue drains empty, so it can be reused both for a normal landing (which should end
    // by spawning the next block, via FinalizeTurn) and for the destroy-block power-up (which
    // should NOT spawn a new block - it should just resume the block that was already falling).
    // ----------------------------------------------------------------

    private readonly Queue<Block> mergeCheckQueue = new Queue<Block>();

    private void ResolveNext(Action onAllResolved)
    {
        while (mergeCheckQueue.Count > 0)
        {
            Block anchor = mergeCheckQueue.Dequeue();

            // Skip stale entries: this block may have already been consumed into a cluster
            // merge (and destroyed) since it was queued.
            if (anchor == null || board[anchor.Row, anchor.Col] != anchor) continue;

            List<Block> cluster = FindCluster(anchor.Row, anchor.Col, anchor.Value);
            if (cluster.Count < 2) continue;

            MergeClusterIntoAnchor(anchor, cluster, () =>
            {
                CollapseBoard(movedBlocks =>
                {
                    mergeCheckQueue.Enqueue(anchor); // may still be part of another cluster
                    foreach (Block moved in movedBlocks)
                    {
                        if (moved != anchor) mergeCheckQueue.Enqueue(moved);
                    }
                    ResolveNext(onAllResolved);
                });
            });
            return; // wait for the async merge/collapse tweens before continuing the queue
        }

        onAllResolved?.Invoke();
    }

    /// <summary>Flood-fills out from (row, col) across every 4-directionally connected block sharing value.</summary>
    private List<Block> FindCluster(int row, int col, int value)
    {
        List<Block> cluster = new List<Block>();
        bool[,] visited = new bool[Rows, Cols];
        Stack<(int r, int c)> stack = new Stack<(int r, int c)>();
        stack.Push((row, col));

        while (stack.Count > 0)
        {
            (int r, int c) = stack.Pop();
            if (r < 0 || r >= Rows || c < 0 || c >= Cols || visited[r, c]) continue;
            visited[r, c] = true;

            Block b = board[r, c];
            if (b == null || b.Value != value) continue;

            cluster.Add(b);
            stack.Push((r + 1, c));
            stack.Push((r - 1, c));
            stack.Push((r, c + 1));
            stack.Push((r, c - 1));
        }

        return cluster;
    }

    /// <summary>Slides every other block in the cluster into the anchor's cell, then sets anchor's merged value.</summary>
    private void MergeClusterIntoAnchor(Block anchor, List<Block> cluster, Action onComplete)
    {
        PlaySfx(mergeSound); // SFX: cluster is merging

        Vector2 targetPos = GetCellAnchoredPosition(anchor.Row, anchor.Col);
        List<Tween> tweens = new List<Tween>();

        foreach (Block b in cluster)
        {
            if (b == anchor) continue;

            Vector2 sourcePos = GetCellAnchoredPosition(b.Row, b.Col);
            b.PlayMergeWipe(targetPos - sourcePos); // wipe overlay rides along as it slides into the anchor
            b.RectTransform.SetSiblingIndex(anchor.RectTransform.GetSiblingIndex()); // render behind the anchor it's sliding into

            board[b.Row, b.Col] = null;
            b.RectTransform.DOKill();
            tweens.Add(b.RectTransform.DOAnchorPos(targetPos, mergeMoveDuration).SetEase(Ease.InQuad));
        }

        int newValue = anchor.Value * NextPowerOfTwoAtLeast(cluster.Count);

        void FinishMerge()
        {
            foreach (Block b in cluster)
            {
                if (b != anchor) Destroy(b.gameObject);
            }
            anchor.SetValue(newValue); // updates text, color, and plays punch-scale internally
            AddScore(newValue);
            onComplete?.Invoke();
        }

        if (tweens.Count == 0)
        {
            FinishMerge();
            return;
        }

        int remaining = tweens.Count;
        foreach (Tween t in tweens)
        {
            t.OnComplete(() =>
            {
                remaining--;
                if (remaining <= 0) FinishMerge();
            });
        }
    }

    private static int NextPowerOfTwoAtLeast(int n)
    {
        int p = 1;
        while (p < n) p *= 2;
        return p;
    }

    /// <summary>Drops every block down to fill gaps in its column. Reports every block that moved.</summary>
    private void CollapseBoard(Action<List<Block>> onComplete)
    {
        List<Tween> tweens = new List<Tween>();
        List<Block> moved = new List<Block>();

        for (int col = 0; col < Cols; col++)
        {
            int writeRow = 0;
            for (int row = 0; row < Rows; row++)
            {
                Block b = board[row, col];
                if (b == null) continue;

                if (row != writeRow)
                {
                    board[writeRow, col] = b;
                    board[row, col] = null;
                    b.Row = writeRow;
                    moved.Add(b);

                    Vector2 targetPos = GetCellAnchoredPosition(writeRow, col);
                    b.RectTransform.DOKill();
                    Tween t = b.RectTransform.DOAnchorPos(targetPos, collapseMoveDuration).SetEase(Ease.Linear);
                    tweens.Add(t);
                }
                writeRow++;
            }
        }

        if (tweens.Count == 0)
        {
            onComplete?.Invoke(moved);
            return;
        }

        int remaining = tweens.Count;
        foreach (Tween t in tweens)
        {
            t.OnComplete(() =>
            {
                remaining--;
                if (remaining <= 0) onComplete?.Invoke(moved);
            });
        }
    }

    // ----------------------------------------------------------------
    // POWER-UP: DESTROY BLOCK
    //
    // Hook your PowerUP button's OnClick() to OnPowerUpDestroyButtonPressed(). While active,
    // Update() skips steering/auto-fall/hard-drop entirely, so the board (and the block that
    // was mid-fall) freezes in place. The next tap anywhere on screen is treated as a pick:
    // whichever occupied cell is closest to the tap (within a small radius) gets destroyed,
    // the column collapses, any merges the collapse reveals resolve normally, and then the
    // previously-falling block just continues from where it paused - no new block is spawned.
    // ----------------------------------------------------------------

    /// <summary>Call from the power-up button's OnClick(). Freezes the board and starts block selection.</summary>
    public void OnPowerUpDestroyButtonPressed()
    {
        if (isGameOver || inputLocked || isPowerUpDestroySelecting) return; // mid-merge/mid-turn or already selecting - ignore

        isPowerUpDestroySelecting = true;
        inputLocked = true; // belt-and-braces; Update() already bypasses steering/fall/drop while selecting
        if (powerUpSelectionHint != null) powerUpSelectionHint.SetActive(true);
    }

    /// <summary>Optional: call this (e.g. from a "cancel" button) to back out of block selection without destroying anything.</summary>
    public void CancelPowerUpDestroy()
    {
        if (!isPowerUpDestroySelecting) return;

        isPowerUpDestroySelecting = false;
        inputLocked = false;
        if (powerUpSelectionHint != null) powerUpSelectionHint.SetActive(false);
    }

    private void TryDestroyBlockAtPointer()
    {
        Canvas canvas = boardRoot.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRoot, pointerScreenPos, cam, out Vector2 localPoint))
        {
            return;
        }

        // Find the occupied cell whose center is closest to the tap point.
        int bestRow = -1, bestCol = -1;
        float bestDistSqr = float.MaxValue;

        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                if (board[row, col] == null) continue;

                Vector2 cellPos = GetCellAnchoredPosition(row, col);
                float dx = cellPos.x - localPoint.x;
                float dy = cellPos.y - localPoint.y;
                float distSqr = dx * dx + dy * dy;

                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    bestRow = row;
                    bestCol = col;
                }
            }
        }

        if (bestRow == -1) return; // board is empty - nothing to destroy, keep waiting for a tap

        // Only accept taps reasonably close to an actual block, so missing entirely doesn't
        // accidentally nuke whatever happens to be nearest on the far side of the board.
        float tapRadius = rowSpacing * 0.6f;
        if (bestDistSqr > tapRadius * tapRadius) return;

        DestroyBlockAt(bestRow, bestCol);
    }

    private void DestroyBlockAt(int row, int col)
    {
        Block block = board[row, col];
        if (block == null) return;

        isPowerUpDestroySelecting = false;
        if (powerUpSelectionHint != null) powerUpSelectionHint.SetActive(false);

        PlaySfx(destroySound); // SFX: block popped via power-up

        board[row, col] = null;

        RectTransform rt = block.RectTransform;
        rt.DOKill();

        Sequence destroySeq = DOTween.Sequence();
        destroySeq.Append(rt.DOScale(1.15f, 0.08f).SetEase(Ease.OutQuad));
        destroySeq.Append(rt.DOScale(0f, 0.12f).SetEase(Ease.InBack));
        destroySeq.OnComplete(() =>
        {
            if (block != null) Destroy(block.gameObject);

            CollapseBoard(movedBlocks =>
            {
                mergeCheckQueue.Clear();
                foreach (Block moved in movedBlocks)
                {
                    mergeCheckQueue.Enqueue(moved);
                }
                ResolveNext(ResumeAfterPowerUpDestroy);
            });
        });
    }

    /// <summary>
    /// Called once collapse + any chain merges from the destroy have fully settled.
    /// Unlike FinalizeTurn, this does NOT spawn a new block - the block that was already
    /// falling before the power-up was used just resumes from where it paused.
    /// </summary>
    private void ResumeAfterPowerUpDestroy()
    {
        inputLocked = false;
    }

    // ----------------------------------------------------------------
    // GAME OVER / TURN END
    // ----------------------------------------------------------------

    private void FinalizeTurn()
    {
        if (CheckGameOver())
        {
            TriggerGameOver();
            return;
        }

        inputLocked = false;
        SpawnBlock();
    }

    private bool CheckGameOver()
    {
        for (int col = 0; col < Cols; col++)
        {
            if (board[Rows - 1, col] != null) return true;
        }
        return false;
    }

    private void TriggerGameOver()
    {
        isGameOver = true;
        inputLocked = true;
        if (activeBlock != null)
        {
            activeBlock.RectTransform.DOKill();
        }
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Debug.Log("Game Over - stack reached the top row.");
    }

    /// <summary>Call from a UI "Restart" button to reset the board.</summary>
    public void RestartGame()
    {
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                if (board[row, col] != null)
                {
                    Destroy(board[row, col].gameObject);
                    board[row, col] = null;
                }
            }
        }

        if (activeBlock != null)
        {
            Destroy(activeBlock.gameObject);
            activeBlock = null;
        }

        isPowerUpDestroySelecting = false;
        if (powerUpSelectionHint != null) powerUpSelectionHint.SetActive(false);

        isGameOver = false;
        inputLocked = false;
        score = 0;
        UpdateScoreUI();
        SpawnBlock();
    }

    // Quit
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    // Pause

    public void Pause()
    {
         pauseScreen.SetActive(true);
         Time.timeScale = 0f;   
    }

    // TAP TO PLAY
    public void TapToPlay()
    {
        pauseScreen.SetActive(false);
        Time.timeScale = 1f;
    }

    
    // Music
    // ----------------------------------------------------------------
    // SOUND EFFECTS (SFX) LOGIC - Matches GameManager Behavior
    // ----------------------------------------------------------------
    /// <summary>
    /// Call this from your SFX Button OnClick event in Inspector.
    /// </summary>
    public void ToggleSFX()
    {
        //isSfxOn = !isSfxOn;
        // Apply to Audio Manager (assuming AudioManager.MuteSFX takes 'true' to silence)
        //if (audioManager != null)
        //{
            // Note: We pass !isSfxOn because if SFX is OFF (false), we want it MUTED (true).
            //audioManager.MuteSFX(!isSfxOn);
        //}
        // Save Preference
        //PlayerPrefs.SetInt(SfxPrefsKey, isSfxOn ? 1 : 0);
        //PlayerPrefs.Save();
        // Update UI Icons
        //ApplySFXState();
        
        // Optional: Play a click sound so the user knows it registered
        // (Only if we just turned it ON, obviously)
        //if(isSfxOn && audioManager != null) audioManager.PlaySFX(null); // Play default click if available
    }
    /// <summary>
    /// Updates the visual state of the SFX icons.
    /// </summary>
    private void ApplySFXState()
    {
        // if (sfxOnIcon != null) sfxOnIcon.SetActive(isSfxOn);
        // if (sfxOffIcon != null) sfxOffIcon.SetActive(!isSfxOn);
        // Ensure Audio Manager is synced on startup
        // if (audioManager != null)
        // {
        //     audioManager.MuteSFX(!isSfxOn);
        // }
    }

    /// <summary>Plays a one-shot SFX clip through sfxSource, if both are assigned.</summary>
    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }
        /// <summary>Call this from any UI Button's OnClick() event to play a generic click sound.</summary>
    public void PlayButtonClickSound()
    {
        PlaySfx(buttonClickSound);
    }

    // ----------------------------------------------------------------
    // UPDATED MUSIC LOGIC (Cleaned up)
    // ----------------------------------------------------------------
    public void ToggleMusic()
    {
        isMusicOn = !isMusicOn;
        ApplyMusicState();
        
        // Save Preference
        PlayerPrefs.SetInt("Music", isMusicOn ? 1 : 0);
        PlayerPrefs.Save();
    }
    private void ApplyMusicState()
    {
        // Control Audio Source
        if (bgSound != null)
        {
            if (isMusicOn)
            {
                bgSound.UnPause();
                if (!bgSound.isPlaying) bgSound.Play();
            }
            else
            {
                bgSound.Pause();
            }
        }
        // Update Icons
        if (musicIcon != null) musicIcon.SetActive(isMusicOn);
        if (muteIcon != null) muteIcon.SetActive(!isMusicOn);
    }

    
}
}