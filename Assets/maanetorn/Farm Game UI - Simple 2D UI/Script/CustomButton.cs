using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace FGUIStarter
{
    public class CustomButton : Button, IPointerDownHandler, IPointerUpHandler
    {
        RectTransform textRect;
        Vector2 originalTextPos;

        bool isHeld;
        protected override void Awake()
        {
            base.Awake();
            // Icon-only buttons hide their label, so press the icon down instead (and never throw).
            Transform icon = transform.Find("Icon");
            if (icon != null) textRect = icon as RectTransform;
            else
            {
                TextMeshProUGUI label = GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) textRect = label.rectTransform;
            }
            if (textRect != null) originalTextPos = textRect.anchoredPosition;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            isHeld = true;
            ApplyPressedVisual();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            isHeld = false;
            ApplyNormalVisual();
        }

        private void ApplyPressedVisual()
        {
            // PressFeedback (on the same object) moves every child; avoid a double offset.
            if (GetComponent<PressFeedback>() != null) return;
            if (textRect != null)
            {
                float height = ((RectTransform)transform).rect.height;
                float offset = height - (height * 0.86718f);//calculation for 128x128 sprite
                //use this code below instead of the code in line 40, in case the offset of the text doesn't make sense with respect to the thickness of the button or gameview dimensions:
                //float offset = height - (height * 0.86718f) - insertYourCustomOffset;
                //Example: float offset = height - (height * 0.86718f) - 10f;
                textRect.anchoredPosition = originalTextPos - new Vector2(0, offset);
            }
        }

        private void ApplyNormalVisual()
        {
            if (textRect != null)
            {
                textRect.anchoredPosition = originalTextPos;
            }
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (state == SelectionState.Pressed)
            {
                ApplyPressedVisual();
            }

            else
            {
                ApplyNormalVisual();
            }
        }

    }
}
