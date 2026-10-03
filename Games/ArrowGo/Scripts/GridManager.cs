using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace OMG.ArrowGo
{
    /// <summary>
    /// Main board controller for ArrowGo.
    ///
    /// - Creates and clears the board.
    /// - Converts grid coordinates to UI positions.
    /// - Validates arrow paths and tracks occupied cells.
    /// - Handles board-level touch/mouse input with a geometric hit test.
    /// - Decides whether an arrow can escape and tracks escaping arrows.
    /// </summary>
    public sealed class GridManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform boardParent;
        [SerializeField] private GameObject arrowPrefab;
        [SerializeField] private Sprite arrowSprite;

        [Header("Arrow Visual")]
        [SerializeField]
        private Color trailColor = new Color(0.40f, 0.29f, 0.20f, 1f);

        [Header("Grid Layout")]
        [SerializeField, Min(1f)] private float cellSize = 100f;
        [SerializeField, Min(0f)] private float cellSpacing = 8f;

        [Header("Arrow Size")]
        [SerializeField, Range(0.1f, 1.5f)] private float arrowSize = 0.75f;
        [SerializeField, Range(0.03f, 0.30f)] private float bodyThickness = 0.08f;

        // Kept so old scenes do not lose their serialized references.
        [Header("Legacy Trail Parent")]
        [SerializeField] private RectTransform trailParent;
        [SerializeField] private bool autoCreateTrailParent = false;

        [Header("Touch Detection")]
        [SerializeField, Min(0f)] private float touchBodyPadding = 6f;
        [SerializeField, Min(0f)] private float touchHeadPadding = 4f;
        [SerializeField, Min(0f)] private float maxTapMovement = 22f;
        [SerializeField, Min(0f)] private float tapCooldown = 0.20f;

        [Tooltip("If off, taps are ignored while another arrow is still escaping.")]
        [SerializeField] private bool allowSimultaneousEscapes = false;

        [Tooltip("Logs why a tap was accepted or rejected. Turn on to debug input.")]
        [SerializeField] private bool debugTouch = false;

        public event Action OnBoardCleared;
        public event Action<int> OnArrowsRemainingChanged;

        private const int MousePointerId = -1;

        private int _width;
        private int _height;
        private float _cellStep;
        private Vector2 _boardSize;

        private readonly Dictionary<Vector2Int, ArrowView> _occupied =
            new Dictionary<Vector2Int, ArrowView>();

        private readonly HashSet<ArrowView> _arrows = new HashSet<ArrowView>();
        private readonly HashSet<ArrowView> _escaping = new HashSet<ArrowView>();

        private readonly List<RaycastResult> _raycastResults =
            new List<RaycastResult>();

        private bool _boardPointerDown;
        private int _boardPointerId = -1;
        private Vector2 _boardPointerStart;
        private Vector2 _boardPointerLast;
        private ArrowView _pressedArrow;
        private float _lastBoardTapTime = -999f;

        // ------------------------------------------------------------
        // PUBLIC PROPERTIES
        // ------------------------------------------------------------

        public RectTransform BoardParent { get { return boardParent; } }
        public int Width { get { return _width; } }
        public int Height { get { return _height; } }
        public bool IsEscapeInProgress { get { return _escaping.Count > 0; } }
        public int RemainingArrows { get { return _arrows.Count; } }

        // ------------------------------------------------------------
        // UNITY
        // ------------------------------------------------------------

        private void Awake()
        {
            ConfigureTrailParent();
        }

        private void OnDisable()
        {
            ResetBoardPointer();
        }

        private void Update()
        {
            if (boardParent == null)
                return;

            // NOTE: input is never gated here. Releases must always be
            // processed, otherwise a pointer can get stuck "down".
#if ENABLE_INPUT_SYSTEM
            ReadInputSystem();
#elif ENABLE_LEGACY_INPUT_MANAGER
            ReadLegacyInput();
#endif
        }

        // ------------------------------------------------------------
        // INPUT: NEW INPUT SYSTEM
        // ------------------------------------------------------------

#if ENABLE_INPUT_SYSTEM
        private void ReadInputSystem()
        {
            Touchscreen touchscreen = Touchscreen.current;

            if (touchscreen != null)
                ReadTouchscreen(touchscreen);

            Mouse mouse = Mouse.current;

            if (mouse != null)
                ReadMouse(mouse);
        }

        private void ReadTouchscreen(Touchscreen touchscreen)
        {
            bool activeTouchStillDown = false;

            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                TouchControl touch = touchscreen.touches[i];
                int id = touch.touchId.ReadValue();
                Vector2 position = touch.position.ReadValue();

                if (touch.press.wasPressedThisFrame)
                    BeginBoardPointer(id, position);

                if (!_boardPointerDown || id != _boardPointerId)
                    continue;

                if (touch.press.wasReleasedThisFrame)
                {
                    EndBoardPointer(id, position);
                }
                else if (touch.press.isPressed)
                {
                    _boardPointerLast = position;
                    activeTouchStillDown = true;
                }
            }

            // Safety net: the finger is gone but we never saw the release
            // (very fast tap, cancelled touch). Finish the tap using the last
            // known position instead of staying stuck forever.
            if (_boardPointerDown &&
                _boardPointerId != MousePointerId &&
                !activeTouchStillDown)
            {
                EndBoardPointer(_boardPointerId, _boardPointerLast);
            }
        }

        private void ReadMouse(Mouse mouse)
        {
            Vector2 position = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
                BeginBoardPointer(MousePointerId, position);

            // BUG FIX: the old code only read the mouse while no pointer was
            // down, so the release was never seen and input froze forever.
            if (_boardPointerDown && _boardPointerId == MousePointerId)
            {
                if (mouse.leftButton.wasReleasedThisFrame ||
                    !mouse.leftButton.isPressed)
                {
                    EndBoardPointer(MousePointerId, position);
                }
            }
        }
#endif

        // ------------------------------------------------------------
        // INPUT: LEGACY INPUT MANAGER
        // ------------------------------------------------------------

#if ENABLE_LEGACY_INPUT_MANAGER
        private void ReadLegacyInput()
        {
            if (Input.touchCount > 0)
            {
                bool activeTouchStillDown = false;

                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);

                    if (touch.phase == UnityEngine.TouchPhase.Began)
                        BeginBoardPointer(touch.fingerId, touch.position);

                    if (!_boardPointerDown || touch.fingerId != _boardPointerId)
                        continue;

                    if (touch.phase == UnityEngine.TouchPhase.Ended ||
                        touch.phase == UnityEngine.TouchPhase.Canceled)
                    {
                        EndBoardPointer(touch.fingerId, touch.position);
                    }
                    else
                    {
                        _boardPointerLast = touch.position;
                        activeTouchStillDown = true;
                    }
                }

                if (_boardPointerDown &&
                    _boardPointerId != MousePointerId &&
                    !activeTouchStillDown)
                {
                    EndBoardPointer(_boardPointerId, _boardPointerLast);
                }

                return;
            }

            if (_boardPointerDown && _boardPointerId != MousePointerId)
                EndBoardPointer(_boardPointerId, _boardPointerLast);

            if (Input.GetMouseButtonDown(0))
                BeginBoardPointer(MousePointerId, Input.mousePosition);

            if (_boardPointerDown && _boardPointerId == MousePointerId)
            {
                if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
                    EndBoardPointer(MousePointerId, Input.mousePosition);
            }
        }
#endif

        // ------------------------------------------------------------
        // INPUT: POINTER STATE MACHINE
        // ------------------------------------------------------------

        private void BeginBoardPointer(int pointerId, Vector2 screenPosition)
        {
            if (_boardPointerDown)
                return;

            if (!allowSimultaneousEscapes && IsEscapeInProgress)
            {
                LogTouch("ignored: another arrow is still escaping.");
                return;
            }

            GameObject blocker;

            if (IsPointerOverBlockingUI(screenPosition, pointerId, out blocker))
            {
                LogTouch("ignored: blocked by UI object '" +
                         (blocker != null ? blocker.name : "?") + "'.");
                return;
            }

            ArrowView arrow = FindArrowAtScreenPoint(screenPosition);

            if (arrow == null)
            {
                LogTouch("ignored: no arrow under pointer at " + screenPosition + ".");
                return;
            }

            if (arrow.IsAnimating)
                return;

            _boardPointerDown = true;
            _boardPointerId = pointerId;
            _boardPointerStart = screenPosition;
            _boardPointerLast = screenPosition;
            _pressedArrow = arrow;

            LogTouch("pressed arrow at " + arrow.GridPosition + ".");
        }

        private void EndBoardPointer(int pointerId, Vector2 screenPosition)
        {
            if (!_boardPointerDown || pointerId != _boardPointerId)
                return;

            ArrowView arrow = _pressedArrow;
            Vector2 startPosition = _boardPointerStart;

            ResetBoardPointer();

            if (arrow == null || arrow.IsAnimating)
                return;

            if (Vector2.Distance(startPosition, screenPosition) > maxTapMovement)
            {
                LogTouch("ignored: finger moved too far to count as a tap.");
                return;
            }

            float now = Time.unscaledTime;

            if (now - _lastBoardTapTime < tapCooldown)
                return;

            _lastBoardTapTime = now;

            TryEscapeArrow(arrow);
        }

        private void ResetBoardPointer()
        {
            _boardPointerDown = false;
            _boardPointerId = -1;
            _boardPointerStart = Vector2.zero;
            _boardPointerLast = Vector2.zero;
            _pressedArrow = null;
        }

        private void LogTouch(string message)
        {
            if (debugTouch)
                Debug.Log("[ArrowGo Touch] " + message, this);
        }

        // ------------------------------------------------------------
        // INPUT: UI BLOCKING
        // ------------------------------------------------------------

        private bool ContainsBoard(Transform candidate)
        {
            return candidate != null &&
                   boardParent != null &&
                   boardParent.IsChildOf(candidate);
        }

        /// <summary>
        /// True only when the pointer is over a real interactive UI element
        /// or overlay that is NOT the board and does NOT contain the board.
        /// Backgrounds, root panels and root CanvasGroups that contain the
        /// board never block input.
        /// </summary>
        private bool IsPointerOverBlockingUI(
            Vector2 screenPosition,
            int pointerId,
            out GameObject blocker)
        {
            blocker = null;

            EventSystem eventSystem = EventSystem.current;

            if (eventSystem == null)
                return false;

            PointerEventData eventData = new PointerEventData(eventSystem);
            eventData.position = screenPosition;
            eventData.pointerId = pointerId;

            _raycastResults.Clear();
            eventSystem.RaycastAll(eventData, _raycastResults);

            for (int i = 0; i < _raycastResults.Count; i++)
            {
                GameObject hit = _raycastResults[i].gameObject;

                if (hit == null)
                    continue;

                Transform hitTransform = hit.transform;

                // The board, its children, and anything that CONTAINS the
                // board (backgrounds, root panels) never block board input.
                if (hitTransform.IsChildOf(boardParent) ||
                    boardParent.IsChildOf(hitTransform))
                {
                    continue;
                }

                // Buttons, toggles, sliders, etc.
                Selectable selectable = hit.GetComponentInParent<Selectable>();

                if (selectable != null &&
                    selectable.IsInteractable() &&
                    !ContainsBoard(selectable.transform))
                {
                    blocker = selectable.gameObject;
                    return true;
                }

                // Custom click handlers.
                GameObject clickHandler =
                    ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);

                if (clickHandler != null && !ContainsBoard(clickHandler.transform))
                {
                    blocker = clickHandler;
                    return true;
                }

                // Pause/settings overlays that block raycasts.
                CanvasGroup group = hit.GetComponentInParent<CanvasGroup>();

                if (group != null &&
                    group.blocksRaycasts &&
                    group.interactable &&
                    !ContainsBoard(group.transform))
                {
                    blocker = group.gameObject;
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------
        // INPUT: GEOMETRIC HIT TEST
        // ------------------------------------------------------------

        /// <summary>
        /// Finds the arrow whose REAL visual path is closest to the touch.
        /// Head and body are scored relative to their own radius so neither
        /// wins just because its radius is bigger.
        /// </summary>
        private ArrowView FindArrowAtScreenPoint(Vector2 screenPoint)
        {
            if (boardParent == null || _arrows.Count == 0)
                return null;

            Vector2 boardPoint;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    boardParent,
                    screenPoint,
                    GetBoardEventCamera(),
                    out boardPoint))
            {
                return null;
            }

            // Hit radii are measured in the SAME local board space as the
            // arrow visuals. This is deliberate: we never use the ArrowView's
            // large rectangular RectTransform as a hitbox.
            float headRadius =
                GetArrowVisualSize() * 0.5f + touchHeadPadding;

            float bodyRadius =
                Mathf.Max(2f, GetBodyThickness()) * 0.5f + touchBodyPadding;

            ArrowView bestArrow = null;
            float bestDistance = float.PositiveInfinity;

            foreach (ArrowView arrow in _arrows)
            {
                if (arrow == null || arrow.IsAnimating)
                    continue;

                List<Vector2Int> path = arrow.Path;

                if (path == null || path.Count == 0)
                    continue;

                // HEAD
                Vector2 headPoint = GetGridLocalPosition(path[0]);
                float headDistance = Vector2.Distance(boardPoint, headPoint);

                if (headDistance <= headRadius &&
                    headDistance < bestDistance)
                {
                    bestDistance = headDistance;
                    bestArrow = arrow;
                }

                // BODY + CORNERS + TAIL
                //
                // Every consecutive pair of path cells is one real arrow
                // segment. DistancePointToSegment also covers the endpoints,
                // so corners and the tail are included automatically.
                for (int i = 1; i < path.Count; i++)
                {
                    Vector2 a = GetGridLocalPosition(path[i - 1]);
                    Vector2 b = GetGridLocalPosition(path[i]);

                    float distance =
                        DistancePointToSegment(boardPoint, a, b);

                    if (distance <= bodyRadius &&
                        distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestArrow = arrow;
                    }
                }
            }

            return bestArrow;
        }

        private Camera GetBoardEventCamera()
        {
            Canvas canvas = boardParent.GetComponentInParent<Canvas>();

            if (canvas == null)
                return null;

            canvas = canvas.rootCanvas;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.worldCamera != null
                ? canvas.worldCamera
                : Camera.main;
        }

        private static float DistancePointToSegment(
            Vector2 point,
            Vector2 a,
            Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;

            if (lengthSquared <= 0.000001f)
                return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);

            return Vector2.Distance(point, a + ab * t);
        }

        // ------------------------------------------------------------
        // LEVEL LOADING
        // ------------------------------------------------------------

        public void LoadLevel(LevelData level)
        {
            if (level == null)
            {
                Debug.LogError("GridManager: LevelData is null.");
                return;
            }

            if (boardParent == null)
            {
                Debug.LogError("GridManager: Board Parent is not assigned.");
                return;
            }

            if (arrowPrefab == null)
            {
                Debug.LogError("GridManager: Arrow Prefab is not assigned.");
                return;
            }

            if (arrowSprite == null)
            {
                Debug.LogError("GridManager: Arrow Sprite is not assigned.");
                return;
            }

            ClearBoard();

            _width = Mathf.Max(1, level.width);
            _height = Mathf.Max(1, level.height);

            RecalculateBoardLayout();

            if (level.arrows == null || level.arrows.Count == 0)
            {
                Debug.LogWarning("GridManager: Level contains no arrows.");

                if (OnArrowsRemainingChanged != null)
                    OnArrowsRemainingChanged.Invoke(0);

                return;
            }

            for (int i = 0; i < level.arrows.Count; i++)
                TryCreateArrow(level.arrows[i]);

            if (OnArrowsRemainingChanged != null)
                OnArrowsRemainingChanged.Invoke(_arrows.Count);
        }

        // ------------------------------------------------------------
        // BOARD LAYOUT
        // ------------------------------------------------------------

        private void RecalculateBoardLayout()
        {
            _cellStep = cellSize + cellSpacing;

            float width =
                _width * cellSize + Mathf.Max(0, _width - 1) * cellSpacing;

            float height =
                _height * cellSize + Mathf.Max(0, _height - 1) * cellSpacing;

            _boardSize = new Vector2(width, height);

            // Works for both point anchors and stretched anchors.
            boardParent.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, width);

            boardParent.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical, height);

            if (trailParent != null)
                trailParent.sizeDelta = _boardSize;
        }

        /// <summary>
        /// Converts grid coordinates to the board's local space (origin at the
        /// board pivot). This is the same space used by
        /// ScreenPointToLocalPointInRectangle and RectTransform.localPosition.
        /// Grid origin is the bottom-left cell.
        /// </summary>
        public Vector2 GetGridLocalPosition(Vector2Int gridPosition)
        {
            if (_width <= 0 || _height <= 0)
                return Vector2.zero;

            float left = -_boardSize.x * boardParent.pivot.x;
            float bottom = -_boardSize.y * boardParent.pivot.y;

            return new Vector2(
                left + gridPosition.x * _cellStep + cellSize * 0.5f,
                bottom + gridPosition.y * _cellStep + cellSize * 0.5f);
        }

        // ------------------------------------------------------------
        // LEVEL VALIDATION
        // ------------------------------------------------------------

        private bool TryCreateArrow(ArrowSpawnData spawn)
        {
            List<Vector2Int> path = GetValidPath(spawn);

            if (path == null || path.Count == 0)
            {
                Debug.LogWarning("GridManager: Invalid arrow path. Arrow was skipped.");
                return false;
            }

            ArrowDirection direction = GetCorrectDirection(path, spawn.direction);

            return SpawnArrow(path, direction);
        }

        private List<Vector2Int> GetValidPath(ArrowSpawnData spawn)
        {
            List<Vector2Int> source = spawn.GetPath();

            if (source == null || source.Count == 0)
                return null;

            List<Vector2Int> path = new List<Vector2Int>(source);

            // Backwards compatibility for old single-cell level data.
            if (path.Count == 1 &&
                path[0] == Vector2Int.zero &&
                (spawn.x != 0 || spawn.y != 0))
            {
                path[0] = new Vector2Int(spawn.x, spawn.y);
            }

            HashSet<Vector2Int> localCells = new HashSet<Vector2Int>();

            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int cell = path[i];

                if (!IsInsideBoard(cell))
                {
                    Debug.LogWarning(
                        "GridManager: Arrow cell " + cell + " is outside the board.");
                    return null;
                }

                if (!localCells.Add(cell))
                {
                    Debug.LogWarning(
                        "GridManager: Arrow contains duplicate cell " + cell + ".");
                    return null;
                }

                if (i > 0)
                {
                    Vector2Int delta = path[i] - path[i - 1];
                    int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);

                    if (distance != 1)
                    {
                        Debug.LogWarning(
                            "GridManager: Arrow path contains a non-adjacent segment.");
                        return null;
                    }
                }
            }

            return path;
        }

        private ArrowDirection GetCorrectDirection(
            List<Vector2Int> path,
            ArrowDirection original)
        {
            if (path == null || path.Count < 2)
                return original;

            return VectorToArrowDirection(path[0] - path[1]);
        }

        // ------------------------------------------------------------
        // SPAWN
        // ------------------------------------------------------------

        private bool SpawnArrow(List<Vector2Int> path, ArrowDirection direction)
        {
            if (path == null || path.Count == 0)
                return false;

            if (!CanOccupyPath(path))
            {
                Debug.LogWarning("GridManager: Arrow path overlaps another arrow.");
                return false;
            }

            GameObject go = Instantiate(arrowPrefab, boardParent);

            if (go == null)
            {
                Debug.LogError("GridManager: Failed to instantiate arrow.");
                return false;
            }

            RectTransform rect = go.GetComponent<RectTransform>();

            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }

            ArrowView view = go.GetComponent<ArrowView>();

            if (view == null)
            {
                Debug.LogError("GridManager: Arrow Prefab does not contain ArrowView.");
                Destroy(go);
                return false;
            }

            view.Init(this, path, direction, arrowSprite);

            if (!view.IsInitialized)
            {
                Debug.LogError("GridManager: ArrowView failed to initialize.");
                Destroy(go);
                return false;
            }

            for (int i = 0; i < path.Count; i++)
                _occupied[path[i]] = view;

            _arrows.Add(view);

            return true;
        }

        private bool CanOccupyPath(List<Vector2Int> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                ArrowView occupant;

                if (_occupied.TryGetValue(path[i], out occupant) && occupant != null)
                    return false;
            }

            return true;
        }

        // ------------------------------------------------------------
        // ARROW ESCAPE
        // ------------------------------------------------------------

        public void TryEscapeArrow(ArrowView arrow)
        {
            if (arrow == null || !_arrows.Contains(arrow))
                return;

            if (arrow.IsAnimating || !arrow.IsInitialized)
                return;

            if (!allowSimultaneousEscapes && IsEscapeInProgress)
                return;

            Vector2Int step = arrow.Direction.ToVector();

            if (step == Vector2Int.zero)
            {
                Debug.LogWarning("GridManager: Arrow has invalid direction.");
                return;
            }

            if (!IsPathClear(arrow.GridPosition, step, arrow))
            {
                LogTouch("arrow at " + arrow.GridPosition + " is blocked.");
                arrow.PlayBlockedShake();
                return;
            }

            // Register BEFORE starting the animation, because the animation
            // may call back synchronously if the arrow is already off-screen.
            _escaping.Add(arrow);

            // An escaping arrow no longer takes part in collision checks.
            RemoveArrowFromOccupancy(arrow);

            arrow.PlayEscapeAnimation(
                Vector2.zero,
                delegate { CompleteArrowEscape(arrow); });
        }

        private void CompleteArrowEscape(ArrowView arrow)
        {
            // Stale callback (board was cleared while the arrow was leaving).
            if (!_escaping.Remove(arrow))
                return;

            _arrows.Remove(arrow);

            int remaining = _arrows.Count;

            if (OnArrowsRemainingChanged != null)
                OnArrowsRemainingChanged.Invoke(remaining);

            if (remaining == 0 && OnBoardCleared != null)
                OnBoardCleared.Invoke();
        }

        // ------------------------------------------------------------
        // OCCUPANCY
        // ------------------------------------------------------------

        private void RemoveArrowFromOccupancy(ArrowView arrow)
        {
            if (arrow == null || arrow.Path == null)
                return;

            for (int i = 0; i < arrow.Path.Count; i++)
            {
                Vector2Int cell = arrow.Path[i];
                ArrowView occupant;

                if (_occupied.TryGetValue(cell, out occupant) && occupant == arrow)
                    _occupied.Remove(cell);
            }
        }

        private bool IsPathClear(
            Vector2Int head,
            Vector2Int step,
            ArrowView movingArrow)
        {
            if (step == Vector2Int.zero)
                return false;

            Vector2Int cursor = head + step;

            while (IsInsideBoard(cursor))
            {
                ArrowView occupant;

                if (_occupied.TryGetValue(cursor, out occupant) &&
                    occupant != null &&
                    occupant != movingArrow)
                {
                    return false;
                }

                cursor += step;
            }

            return true;
        }

        // ------------------------------------------------------------
        // BOARD CLEAR / RESET
        // ------------------------------------------------------------

        public void ClearBoard()
        {
            HashSet<ArrowView> toDestroy = new HashSet<ArrowView>(_arrows);

            foreach (KeyValuePair<Vector2Int, ArrowView> pair in _occupied)
            {
                if (pair.Value != null)
                    toDestroy.Add(pair.Value);
            }

            foreach (ArrowView arrow in toDestroy)
            {
                if (arrow == null)
                    continue;

                // Deactivate first so its coroutine stops immediately and it
                // disappears this frame instead of at end-of-frame.
                arrow.gameObject.SetActive(false);
                Destroy(arrow.gameObject);
            }

            _occupied.Clear();
            _arrows.Clear();
            _escaping.Clear();

            ResetBoardPointer();
        }

        public void ResetBoard()
        {
            ClearBoard();
            ClearTrails();

            if (OnArrowsRemainingChanged != null)
                OnArrowsRemainingChanged.Invoke(0);
        }

        private void ClearTrails()
        {
            if (trailParent == null)
                return;

            for (int i = trailParent.childCount - 1; i >= 0; i--)
            {
                Transform child = trailParent.GetChild(i);

                if (child != null)
                    Destroy(child.gameObject);
            }
        }

        // ------------------------------------------------------------
        // GRID UTILITIES
        // ------------------------------------------------------------

        public bool IsInsideBoard(Vector2Int position)
        {
            return position.x >= 0 &&
                   position.x < _width &&
                   position.y >= 0 &&
                   position.y < _height;
        }

        public bool IsCellOccupied(Vector2Int position)
        {
            ArrowView occupant;

            return _occupied.TryGetValue(position, out occupant) &&
                   occupant != null;
        }

        public ArrowView GetArrowAt(Vector2Int position)
        {
            ArrowView arrow;

            return _occupied.TryGetValue(position, out arrow) ? arrow : null;
        }

        public Vector2Int ClampToBoard(Vector2Int position)
        {
            return new Vector2Int(
                Mathf.Clamp(position.x, 0, Mathf.Max(0, _width - 1)),
                Mathf.Clamp(position.y, 0, Mathf.Max(0, _height - 1)));
        }

        // ------------------------------------------------------------
        // DIRECTION
        // ------------------------------------------------------------

        private ArrowDirection VectorToArrowDirection(Vector2Int direction)
        {
            if (direction == Vector2Int.up) return ArrowDirection.Up;
            if (direction == Vector2Int.down) return ArrowDirection.Down;
            if (direction == Vector2Int.left) return ArrowDirection.Left;
            if (direction == Vector2Int.right) return ArrowDirection.Right;

            return ArrowDirection.Right;
        }

        // ------------------------------------------------------------
        // VISUAL SETTINGS
        // ------------------------------------------------------------

        public float GetTrailWidth()
        {
            return cellSize * 0.08f;
        }

        public Color GetTrailColor()
        {
            return trailColor;
        }

        public RectTransform GetTrailParent()
        {
            ConfigureTrailParent();
            return trailParent;
        }

        public float GetCellSize()
        {
            return cellSize;
        }

        public float GetCellSpacing()
        {
            return cellSpacing;
        }

        public float GetArrowVisualSize()
        {
            return cellSize * Mathf.Clamp(arrowSize, 0.35f, 0.90f);
        }

        public float GetBodyThickness()
        {
            return cellSize * Mathf.Clamp(bodyThickness, 0.03f, 0.12f);
        }

        // ------------------------------------------------------------
        // TRAIL COMPATIBILITY
        // ------------------------------------------------------------

        private void ConfigureTrailParent()
        {
            if (trailParent == null || boardParent == null)
                return;

            trailParent.anchorMin = new Vector2(0.5f, 0.5f);
            trailParent.anchorMax = new Vector2(0.5f, 0.5f);
            trailParent.pivot = new Vector2(0.5f, 0.5f);
            trailParent.anchoredPosition = Vector2.zero;
            trailParent.sizeDelta = _boardSize;
            trailParent.SetAsFirstSibling();
        }

        // ------------------------------------------------------------
        // DEBUG
        // ------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            if (boardParent == null || _width <= 0 || _height <= 0)
                return;

            Vector3 center = boardParent.TransformPoint(boardParent.rect.center);

            Gizmos.DrawWireCube(
                center,
                new Vector3(_boardSize.x, _boardSize.y, 0f));
        }
    }
}