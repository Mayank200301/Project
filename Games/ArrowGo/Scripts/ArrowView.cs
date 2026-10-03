using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OMG.ArrowGo
{
    public enum ArrowDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public static class ArrowDirectionExtensions
    {
        /// Grid step (in cell units) that this direction moves along.
        public static Vector2Int ToVector(this ArrowDirection dir)
        {
            switch (dir)
            {
                case ArrowDirection.Up:    return new Vector2Int(0, 1);
                case ArrowDirection.Down:  return new Vector2Int(0, -1);
                case ArrowDirection.Left:  return new Vector2Int(-1, 0);
                case ArrowDirection.Right: return new Vector2Int(1, 0);
            }
            return Vector2Int.zero;
        }

        /// Z rotation to apply to a sprite whose default artwork points UP.
        public static float ToRotationZ(this ArrowDirection dir)
        {
            switch (dir)
            {
                case ArrowDirection.Up:    return 0f;
                case ArrowDirection.Right: return -90f;
                case ArrowDirection.Down:  return 180f;
                case ArrowDirection.Left:  return 90f;
            }
            return 0f;
        }
    }

    /// <summary>
    /// Visual representation of one ArrowGo arrow.
    ///
    /// Input is handled centrally by GridManager. The root Image remains
    /// only for prefab compatibility and is never used as a touch hitbox.
    /// GridManager checks this arrow's real Path geometry.
    /// </summary>
   

   
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public sealed class ArrowView : MonoBehaviour
    {
        public Vector2Int GridPosition { get; private set; }
        public ArrowDirection Direction { get; private set; }
        public int Length { get; private set; }
        public List<Vector2Int> Path { get; private set; }
        public RectTransform RectTransform { get; private set; }

        [Header("Escape Animation")]
        [SerializeField, Min(50f)]
        private float escapeSpeed = 650f;

        [Tooltip("Safety limit so a stuck escape can never lock the board.")]
        [SerializeField, Min(1f)]
        private float maxEscapeSeconds = 6f;

        private GridManager _grid;
        private Image _hitImage;
        private Image _headImage;
        private RectTransform _headRect;

        private readonly List<RectTransform> _bodySegments = new List<RectTransform>();
        private readonly List<RectTransform> _joints = new List<RectTransform>();
        private readonly List<Vector2> _headHistory = new List<Vector2>();

        private bool _isAnimating;
        private float _bodyStartOffset;
        private Vector2 _restPosition;
        private Coroutine _shakeRoutine;
        private Camera _screenCamera;

        private static readonly Vector3[] _corners = new Vector3[4];
        private static Sprite _whiteSprite;

        // ------------------------------------------------------------
        // PROPERTIES
        // ------------------------------------------------------------

        public bool IsAnimating
        {
            get { return _isAnimating; }
        }

        public bool IsInitialized
        {
            get
            {
                return _grid != null &&
                       _headRect != null &&
                       Path != null &&
                       Path.Count > 0;
            }
        }

        // ------------------------------------------------------------
        // INITIALIZATION
        // ------------------------------------------------------------

        public void Init(
            GridManager grid,
            List<Vector2Int> path,
            ArrowDirection direction,
            Sprite arrowSprite)
        {
            if (grid == null)
            {
                Debug.LogError("ArrowView: GridManager is null.");
                return;
            }

            if (path == null || path.Count == 0)
            {
                Debug.LogError("ArrowView: Path is empty.");
                return;
            }

            if (arrowSprite == null)
            {
                Debug.LogError("ArrowView: Arrow Sprite is not assigned.");
                return;
            }

            _grid = grid;
            Path = new List<Vector2Int>(path);
            GridPosition = Path[0];
            Direction = direction;
            Length = Path.Count;

            RectTransform = GetComponent<RectTransform>();
            _hitImage = GetComponent<Image>();

            ClearGeneratedVisuals();

            _bodySegments.Clear();
            _joints.Clear();
            _headHistory.Clear();

            ConfigureRootClickTarget();

            Color bodyColor = _grid.GetTrailColor();

            if (bodyColor.a < 0.99f)
                bodyColor.a = 1f;

            CreatePathVisuals(arrowSprite, bodyColor);

            // BUG FIX: GetGridLocalPosition() is relative to the board pivot,
            // exactly like the touch hit test. localPosition uses that same
            // space, so visuals and touch areas always line up, whatever the
            // board pivot is. (anchoredPosition would be offset if the pivot
            // is not the center.)
            Vector2 local = _grid.GetGridLocalPosition(GridPosition);
            RectTransform.localPosition = new Vector3(local.x, local.y, 0f);
            _restPosition = RectTransform.anchoredPosition;

            _isAnimating = false;

            ApplyVisualColor(bodyColor);
        }

        // ------------------------------------------------------------
        // HIT AREA
        // ------------------------------------------------------------

        private void ConfigureRootClickTarget()
        {
            if (_hitImage == null)
                return;

            // Invisible compatibility Image. It must not receive pointer
            // events because GridManager owns all board input.
            _hitImage.sprite = null;
            _hitImage.color = new Color(1f, 1f, 1f, 0f);
            _hitImage.raycastTarget = false;
            _hitImage.maskable = false;
            _hitImage.material = null;
            _hitImage.enabled = false;

            RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            RectTransform.pivot = new Vector2(0.5f, 0.5f);
            RectTransform.localRotation = Quaternion.identity;
            RectTransform.localScale = Vector3.one;
        }

        // ------------------------------------------------------------
        // VISUAL CREATION
        // ------------------------------------------------------------

        private void ClearGeneratedVisuals()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);

                if (child != null)
                    Destroy(child.gameObject);
            }
        }

        private Image CreateImageChild(string objectName, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false; // children must NOT receive pointer events
            image.maskable = false;

            return image;
        }

        private void CreatePathVisuals(Sprite arrowSprite, Color bodyColor)
        {
            float visualSize = _grid.GetArrowVisualSize();
            float thickness = Mathf.Max(2f, _grid.GetBodyThickness());

            _bodyStartOffset = Mathf.Max(0f, visualSize * 0.5f - thickness * 0.45f);

            float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;

            for (int i = 0; i < Path.Count; i++)
            {
                Vector2 p = GetPathOffset(Path[i]);
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            float padding = Mathf.Max(visualSize * 0.55f, thickness * 1.5f);

            RectTransform.sizeDelta = new Vector2(
                maxX - minX + visualSize + padding * 2f,
                maxY - minY + visualSize + padding * 2f);

            // BODY
            for (int i = 0; i < Path.Count - 1; i++)
            {
                Vector2 a = GetPathOffset(Path[i]);
                Vector2 b = GetPathOffset(Path[i + 1]);

                if (i == 0)
                    a = GetBodyStartPoint(Vector2.zero, b);

                Image image = CreateImageChild("ArrowBody_" + i, GetWhiteSprite(), bodyColor);
                UpdateSegment(image.rectTransform, a, b, thickness);
                _bodySegments.Add(image.rectTransform);
            }

            // JOINTS
            for (int i = 1; i < Path.Count; i++)
            {
                Image image = CreateImageChild("ArrowJoint_" + i, GetWhiteSprite(), bodyColor);
                image.rectTransform.sizeDelta = new Vector2(thickness, thickness);
                image.rectTransform.anchoredPosition = GetPathOffset(Path[i]);
                _joints.Add(image.rectTransform);
            }

            // HEAD
            _headImage = CreateImageChild("ArrowHead", arrowSprite, Color.white);
            _headRect = _headImage.rectTransform;
            _headRect.sizeDelta = new Vector2(visualSize, visualSize);
            _headRect.anchoredPosition = Vector2.zero;
            _headRect.localRotation = Quaternion.Euler(0f, 0f, Direction.ToRotationZ());
            _headRect.SetAsLastSibling();
        }

        private Vector2 GetPathOffset(Vector2Int cell)
        {
            Vector2Int delta = cell - GridPosition;
            float distance = _grid.GetCellSize() + _grid.GetCellSpacing();

            return new Vector2(delta.x * distance, delta.y * distance);
        }

        private Vector2 GetBodyStartPoint(Vector2 headCenter, Vector2 behindPoint)
        {
            Vector2 toBody = behindPoint - headCenter;

            if (toBody.sqrMagnitude < 0.0001f)
                return headCenter;

            return headCenter + toBody.normalized * _bodyStartOffset;
        }

        // ------------------------------------------------------------
        // ESCAPE
        // ------------------------------------------------------------

        public void PlayEscapeAnimation(Vector2 targetHeadLocalPos, Action onComplete)
        {
            if (_isAnimating || _headRect == null)
                return;

            StopShake();

            _isAnimating = true;

            StartCoroutine(AnimateSnake(onComplete));
        }

        private IEnumerator AnimateSnake(Action onComplete)
        {
            float cellDistance = _grid.GetCellSize() + _grid.GetCellSpacing();

            _screenCamera = ResolveScreenCamera();

            BuildInitialHistory();

            Vector2 currentHead = _headRect.anchoredPosition;
            Vector2 moveDirection = Direction.ToVector();
            float elapsed = 0f;

            // maxEscapeSeconds guarantees the board can never stay locked.
            while (!IsEntireArrowOutsideScreen() && elapsed < maxEscapeSeconds)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                currentHead += moveDirection * escapeSpeed * dt;
                _headRect.anchoredPosition = currentHead;

                AddHeadHistory(currentHead);
                UpdateSnakeBody(cellDistance);

                yield return null;
            }

            if (onComplete != null)
                onComplete.Invoke();

            Destroy(gameObject);
        }

        private Camera ResolveScreenCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
                return null;

            canvas = canvas.rootCanvas;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        // ------------------------------------------------------------
        // BODY FOLLOW
        // ------------------------------------------------------------

        private void BuildInitialHistory()
        {
            _headHistory.Clear();

            _headHistory.Add(
                _headRect != null ? _headRect.anchoredPosition : Vector2.zero);

            // Existing arrow path behind the head = starting snake body.
            for (int i = 1; i < Path.Count; i++)
                _headHistory.Add(GetPathOffset(Path[i]));
        }

        private void AddHeadHistory(Vector2 headPosition)
        {
            if (_headHistory.Count == 0)
            {
                _headHistory.Add(headPosition);
                return;
            }

            if (Vector2.Distance(_headHistory[0], headPosition) > 0.001f)
                _headHistory.Insert(0, headPosition);

            float spacing = _grid.GetCellSize() + _grid.GetCellSpacing();
            float requiredDistance = spacing * (Length + 2);
            float distance = 0f;

            for (int i = 0; i < _headHistory.Count - 1; i++)
            {
                distance += Vector2.Distance(_headHistory[i], _headHistory[i + 1]);

                if (distance >= requiredDistance)
                {
                    int removeFrom = i + 2;

                    if (removeFrom < _headHistory.Count)
                        _headHistory.RemoveRange(removeFrom, _headHistory.Count - removeFrom);

                    break;
                }
            }
        }

        private void UpdateSnakeBody(float cellDistance)
        {
            if (_headRect == null)
                return;

            float thickness = Mathf.Max(2f, _grid.GetBodyThickness());

            for (int i = 0; i < _bodySegments.Count; i++)
            {
                if (_bodySegments[i] == null)
                    continue;

                // First segment keeps the same rear offset it had at rest,
                // so the body does not "pop" when the escape begins.
                Vector2 a = i == 0
                    ? GetHistoryPosition(_bodyStartOffset)
                    : GetHistoryPosition(i * cellDistance);

                Vector2 b = GetHistoryPosition((i + 1) * cellDistance);

                UpdateSegment(_bodySegments[i], a, b, thickness);
            }

            for (int i = 0; i < _joints.Count; i++)
            {
                if (_joints[i] == null)
                    continue;

                _joints[i].anchoredPosition = GetHistoryPosition((i + 1) * cellDistance);
            }
        }

        private Vector2 GetHistoryPosition(float distanceBehindHead)
        {
            if (_headHistory.Count == 0)
                return Vector2.zero;

            if (distanceBehindHead <= 0f)
                return _headHistory[0];

            float remaining = distanceBehindHead;

            for (int i = 0; i < _headHistory.Count - 1; i++)
            {
                Vector2 newer = _headHistory[i];
                Vector2 older = _headHistory[i + 1];
                float segmentLength = Vector2.Distance(newer, older);

                if (segmentLength <= 0.0001f)
                    continue;

                if (remaining <= segmentLength)
                    return Vector2.Lerp(newer, older, remaining / segmentLength);

                remaining -= segmentLength;
            }

            return _headHistory[_headHistory.Count - 1];
        }

        // ------------------------------------------------------------
        // SEGMENT
        // ------------------------------------------------------------

        private void UpdateSegment(
            RectTransform rect,
            Vector2 a,
            Vector2 b,
            float thickness)
        {
            if (rect == null)
                return;

            Vector2 delta = b - a;
            float length = delta.magnitude;

            rect.sizeDelta = new Vector2(Mathf.Max(0.001f, length), thickness);
            rect.anchoredPosition = (a + b) * 0.5f;

            if (length > 0.001f)
            {
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        // ------------------------------------------------------------
        // BLOCKED FEEDBACK
        // ------------------------------------------------------------

        public void PlayBlockedShake()
        {
            if (_isAnimating || !isActiveAndEnabled)
                return;

            // Restart cleanly. The rest position is fixed, so quick repeated
            // taps can no longer leave the arrow displaced.
            StopShake();
            _shakeRoutine = StartCoroutine(ShakeCoroutine());
        }

        private void StopShake()
        {
            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
                _shakeRoutine = null;
            }

            if (RectTransform != null)
                RectTransform.anchoredPosition = _restPosition;
        }

        private IEnumerator ShakeCoroutine()
        {
            const float duration = 0.14f;
            const float magnitude = 5f;

            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;

                float fade = 1f - Mathf.Clamp01(timer / duration);
                float offset = Mathf.Sin(timer * 75f) * magnitude * fade;

                RectTransform.anchoredPosition = _restPosition + new Vector2(offset, 0f);

                yield return null;
            }

            RectTransform.anchoredPosition = _restPosition;
            _shakeRoutine = null;
        }

        // ------------------------------------------------------------
        // COLORS
        // ------------------------------------------------------------

        private void ApplyVisualColor(Color color)
        {
            for (int i = 0; i < _bodySegments.Count; i++)
            {
                if (_bodySegments[i] == null)
                    continue;

                Image image = _bodySegments[i].GetComponent<Image>();

                if (image != null)
                    image.color = color;
            }

            for (int i = 0; i < _joints.Count; i++)
            {
                if (_joints[i] == null)
                    continue;

                Image image = _joints[i].GetComponent<Image>();

                if (image != null)
                    image.color = color;
            }

            if (_headImage != null)
                _headImage.color = Color.white;
        }

        // ------------------------------------------------------------
        // SCREEN EXIT
        // ------------------------------------------------------------

        private bool IsEntireArrowOutsideScreen()
        {
            if (!IsRectOutsideScreen(_headRect))
                return false;

            for (int i = 0; i < _bodySegments.Count; i++)
            {
                if (!IsRectOutsideScreen(_bodySegments[i]))
                    return false;
            }

            for (int i = 0; i < _joints.Count; i++)
            {
                if (!IsRectOutsideScreen(_joints[i]))
                    return false;
            }

            return true;
        }

        private bool IsRectOutsideScreen(RectTransform rect)
        {
            if (rect == null)
                return true;

            rect.GetWorldCorners(_corners);

            for (int i = 0; i < 4; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(_screenCamera, _corners[i]);

                if (p.x >= 0f && p.x <= Screen.width &&
                    p.y >= 0f && p.y <= Screen.height)
                {
                    return false;
                }
            }

            return true;
        }

        // ------------------------------------------------------------
        // WHITE SPRITE
        // ------------------------------------------------------------

        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null)
                return _whiteSprite;

            _whiteSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);

            _whiteSprite.name = "ArrowGoWhiteSprite";

            return _whiteSprite;
        }
    }
}