using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Главное меню: выбор карты карточками (выбранная подсвечена, выбор запоминается) и «Начать» — бой на ней.
    /// В development-сборке есть ещё кнопка сцены-нагрузки для замера производительности.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private string battleSceneName = "Battle";
        [Tooltip("Карточки карт — в том же порядке, что список карт в сцене боя.")]
        [SerializeField] private Button[] mapButtons;
        [SerializeField] private Color selectedMapColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] private Color mapColor = new Color(0.95f, 0.92f, 0.82f);
        [SerializeField] private Button stressButton;
        [SerializeField] private string stressSceneName = "Sandbox_Stress";
        [SerializeField] private int targetFrameRate = 60;

        private void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Time.timeScale = 1f;
            startButton.onClick.AddListener(() => SceneManager.LoadScene(battleSceneName));
            stressButton.onClick.AddListener(() => SceneManager.LoadScene(stressSceneName));
            stressButton.gameObject.SetActive(Debug.isDebugBuild);

            if (mapButtons == null)
            {
                return;
            }

            for (int i = 0; i < mapButtons.Length; i++)
            {
                int index = i;
                mapButtons[i].onClick.AddListener(() => Select(index));
            }

            Select(Mathf.Clamp(MapChoice.Selected, 0, Mathf.Max(0, mapButtons.Length - 1)));
        }

        private void Select(int index)
        {
            MapChoice.Selected = index;
            for (int i = 0; i < mapButtons.Length; i++)
            {
                mapButtons[i].image.color = i == index ? selectedMapColor : mapColor;
            }
        }
    }
}
