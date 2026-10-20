using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 전투 중 유닛에 마우스를 올리면 기본 정보를 보여주는 말풍선.
/// 무엇을 보여줄지는 BattleUnit.GetHoverText가 정하고, 여기서는 마우스 아래 유닛 찾기와 표시만 담당합니다.
/// (Tools > 던전 전투 UI 세팅으로 생성)
/// </summary>
public class UnitHoverTooltip : MonoBehaviour
{
    [Header("UI 연결")]
    public RectTransform panel;     // 말풍선 (마우스를 따라다님)
    public TextMeshProUGUI label;

    [Header("표시 설정")]
    public Vector2 cursorOffset = new Vector2(18f, 18f); // 마우스에서 말풍선 왼쪽-아래 모서리까지
    [Tooltip("유닛 그림 테두리에서 이만큼 벗어나도 올린 것으로 봄 (월드 유닛)")]
    public float hoverPadding = 0.1f;

    static readonly string[] unitTags = { "Player", "Enemy" };

    private Camera cam;

    void Update()
    {
        if (panel == null || label == null) return;

        BattleUnit unit = FindUnitUnderPointer(out Vector2 screenPos);
        if (unit == null)
        {
            if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
            return;
        }

        label.text = unit.GetHoverText();
        if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
        FollowPointer(screenPos);
    }

    BattleUnit FindUnitUnderPointer(out Vector2 screenPos)
    {
        screenPos = Vector2.zero;

        // 프로젝트가 새 Input System 전용이라 Input.mousePosition은 예외를 던짐
        var pointer = UnityEngine.InputSystem.Pointer.current;
        if (pointer == null) return null;
        screenPos = pointer.position.ReadValue();

        // 버튼이나 결과창 위에 있을 때는 표시하지 않음
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return null;

        if (cam == null) cam = Camera.main;
        if (cam == null) return null;

        Vector3 world = cam.ScreenToWorldPoint(screenPos);

        // 겹쳐 있으면 화면 앞쪽(아래쪽)에 있는 유닛을 고름
        BattleUnit best = null;
        foreach (string unitTag in unitTags)
        {
            foreach (GameObject obj in GameObject.FindGameObjectsWithTag(unitTag))
            {
                BattleUnit unit = obj.GetComponent<BattleUnit>();
                if (unit == null || unit.isDead) continue;
                if (!GetUnitBounds(unit).Contains(new Vector3(world.x, world.y, unit.transform.position.z))) continue;

                if (best == null || unit.transform.position.y < best.transform.position.y) best = unit;
            }
        }
        return best;
    }

    // 유닛 그림이 차지하는 영역 (그림이 없으면 충돌 박스)
    Bounds GetUnitBounds(BattleUnit unit)
    {
        Bounds bounds;
        SpriteRenderer sprite = unit.GetComponentInChildren<SpriteRenderer>();
        if (sprite != null) bounds = sprite.bounds;
        else
        {
            Collider2D col = unit.GetComponent<Collider2D>();
            bounds = col != null ? col.bounds : new Bounds(unit.transform.position, Vector3.one);
        }

        bounds.center = new Vector3(bounds.center.x, bounds.center.y, unit.transform.position.z);
        bounds.Expand(new Vector3(hoverPadding * 2f, hoverPadding * 2f, 1f));
        return bounds;
    }

    // 말풍선을 마우스 옆에 두되 화면 밖으로 나가지 않게 가둠
    void FollowPointer(Vector2 screenPos)
    {
        RectTransform parent = panel.parent as RectTransform;
        if (parent == null) return;

        Canvas canvas = panel.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, uiCamera, out Vector2 local)) return;

        Vector2 size = panel.rect.size;
        Vector2 corner = local + cursorOffset;
        corner.x = Mathf.Clamp(corner.x, parent.rect.xMin, parent.rect.xMax - size.x);
        corner.y = Mathf.Clamp(corner.y, parent.rect.yMin, parent.rect.yMax - size.y);

        panel.localPosition = corner + Vector2.Scale(size, panel.pivot);
    }
}
