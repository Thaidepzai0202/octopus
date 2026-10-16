using UnityEngine;
using UnityEngine.EventSystems;

namespace Octopus
{
    /// <summary>Một nút trong cụm nút chiêu: chuyển chạm / vuốt / nhả cho SkillPad.</summary>
    public class SkillPadButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] SkillPad pad;
        [Tooltip("0 = nút 1, 1 = nút 2, 2 = nút 3")]
        [SerializeField] int slot;

        public void OnPointerDown(PointerEventData eventData) => pad.Press(slot, eventData);
        public void OnDrag(PointerEventData eventData) => pad.Drag(slot, eventData);
        public void OnPointerUp(PointerEventData eventData) => pad.Release(slot, eventData);
    }
}
