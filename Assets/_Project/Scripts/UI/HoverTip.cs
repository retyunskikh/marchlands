using Marchlands.View;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Marchlands.UI
{
    /// <summary>
    /// Подсказка при наведении курсора (только на ПК): что делает кнопка и её клавиша. Текст — с разметкой
    /// (<b>заголовок</b>), показывает <see cref="TooltipView"/> рядом с курсором после короткой задержки.
    /// </summary>
    public sealed class HoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField, TextArea(2, 5)] private string text;

        public string Text
        {
            get => text;
            set => text = value;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Platform.IsDesktop && !string.IsNullOrEmpty(text))
            {
                TooltipView.Show(this, eventData.position);
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            TooltipView.Move(this, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipView.Hide(this);
        }

        private void OnDisable()
        {
            TooltipView.Hide(this);
        }
    }
}
