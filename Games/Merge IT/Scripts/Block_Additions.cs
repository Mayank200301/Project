using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using OMG.MergeIT;
using System;
using OMG;
/// <summary>
/// ADDITIONS TO YOUR EXISTING Block.cs
/// ------------------------------------------------------------------
/// Merge these members into your existing script rather than replacing it.
/// The important parts GameManager relies on:
///   1. Value / Row / Col / RectTransform
///   2. Init(), SetValue(), PlaySpawnPop()
///
/// NOTE: unlike the earlier version, this Block does NOT need
/// IPointerDownHandler / IDragHandler / IPointerUpHandler anymore.
/// GameManager now reads mouse/touch input directly every frame and drives
/// the active block itself, which avoids a common bug where another UI
/// element (e.g. a grid cell's Image) sits on top of the block in raycast
/// order and steals the pointer events, silently breaking the drop.
/// If your existing Block.cs still has those interfaces/methods from a
/// previous version, you can safely remove them.
/// </summary>
namespace OMG.MergeIT
{
public class Block : MonoBehaviour
{
    [Header("Block Data")]
    [SerializeField] private int value;
    public int Value => value;

    /// <summary>Row index on the board (0 = bottom, Rows-1 = top). Rows (out of range) = "pending, not yet placed".</summary>
    public int Row { get; set; }
    /// <summary>Column index on the board.</summary>
    public int Col { get; set; }

    [Header("References (assign in Inspector on the prefab)")]
    [Tooltip("Text component that shows the numeric value.")]
    [SerializeField] private TMP_Text valueText;
    [Tooltip("Image component whose color reflects the block's value.")]
    [SerializeField] private Image blockImage;
    [Tooltip("Swipe/wipe sprite played over the block during a merge. Inactive by default; " +
             "rotates 90 degrees when the block slides horizontally instead of vertically.")]
    [SerializeField] private RectTransform wipeSprite;

    /// <summary>Cached RectTransform, used by GameManager for all positioning.</summary>
    public RectTransform RectTransform { get; private set; }

    private BoardManager board;
    private Image wipeImage;

    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();

        if (blockImage != null) blockImage.gameObject.SetActive(true);
        if (valueText != null) valueText.gameObject.SetActive(true);
        if (wipeSprite != null)
        {
            wipeImage = wipeSprite.GetComponent<Image>();
            wipeSprite.gameObject.SetActive(false);
        }
    }

    /// <summary>Called once by GameManager right after Instantiate.</summary>
    public void Init(int startValue, int row, int col, BoardManager owningBoard)
    {
        board = owningBoard;
        Row = row;
        Col = col;
        SetValue(startValue, animate: false);
    }

    /// <summary>
    /// Updates the numeric value and refreshes text + color. Called on spawn
    /// (animate:false) and on merge (animate:true, plays a punch-scale).
    /// </summary>
    public void SetValue(int newValue, bool animate = true)
    {
        value = newValue;

        if (valueText != null)
            valueText.text = value.ToString();

        if (blockImage != null)
        {
            BoardManager owner = board != null ? board : BoardManager.Instance;
            if (owner != null) blockImage.color = owner.GetColorForValue(value);
        }

        if (animate)
        {
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 6, 0.8f);
        }
    }

    /// <summary>Called right after spawning - keeps the block at its normal size (no pop-in).</summary>
    public void PlaySpawnPop()
    {
        transform.DOKill();
        transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Shows the wipe sprite as an overlay on the block while it slides into the block it's
    /// merging with - it doesn't animate on its own, it just rides along with the block's own
    /// slide (it's a child, so it moves for free). Tinted to match the block's own color;
    /// rotated 270 degrees for a horizontal (left/right) slide, 180 degrees for a vertical
    /// (up/down) one (flipped from the "natural" 90/0 since the sprite's default orientation
    /// faces the opposite way).
    /// </summary>
    public void PlayMergeWipe(Vector2 moveDirection)
    {
        if (wipeSprite == null) return;

        bool movingHorizontally = Mathf.Abs(moveDirection.x) > Mathf.Abs(moveDirection.y);
        float angle;
        if (movingHorizontally)
        {
            angle = moveDirection.x > 0f ? 90f : 270f; // moving right (from left) vs moving left (from right)
        }
        else
        {
            angle = 180f;
        }
        wipeSprite.localRotation = Quaternion.Euler(0f, 0f, angle);

        if (wipeImage != null && blockImage != null) wipeImage.color = blockImage.color;

        wipeSprite.DOKill();
        wipeSprite.anchoredPosition = Vector2.zero;
        wipeSprite.gameObject.SetActive(true);
        valueText.gameObject.SetActive(false);
        blockImage.gameObject.SetActive(false);
    }

    
}
}