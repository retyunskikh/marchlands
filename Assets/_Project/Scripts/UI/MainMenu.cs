using System.Collections.Generic;
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
        [Tooltip("Обучение: отдельная карта с пошаговыми уроками.")]
        [SerializeField] private Button tutorialButton;
        [SerializeField] private string battleSceneName = "Battle";
        [Tooltip("Карточки карт — в том же порядке, что список карт в сцене боя.")]
        [SerializeField] private Button[] mapButtons;
        [SerializeField] private Color selectedMapColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] private Color mapColor = new Color(0.95f, 0.92f, 0.82f);
        [SerializeField] private Button stressButton;
        [Tooltip("Выход из игры.")]
        [SerializeField] private Button quitButton;
        [SerializeField] private string stressSceneName = "Sandbox_Stress";
        [Tooltip("Переключатель тумана войны перед боем.")]
        [SerializeField] private Button fogButton;
        [SerializeField] private Text fogLabel;
        [SerializeField] private string fogOnText = "Туман войны: вкл";
        [SerializeField] private string fogOffText = "Туман войны: выкл";
        [SerializeField] private int targetFrameRate = 60;

        private void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Time.timeScale = 1f;
            startButton.onClick.AddListener(() => StartBattle(false));
            if (tutorialButton != null)
            {
                tutorialButton.onClick.AddListener(() => StartBattle(true));
            }

            stressButton.onClick.AddListener(() => SceneManager.LoadScene(stressSceneName));
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(Application.Quit);
            }
            stressButton.gameObject.SetActive(Debug.isDebugBuild);
            if (fogButton != null)
            {
                fogButton.onClick.AddListener(() =>
                {
                    MapChoice.FogOfWar = !MapChoice.FogOfWar;
                    ShowFog();
                });
                ShowFog();
            }

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

        /// <summary>Названия карт — подписи карточек, по порядку (для сетевой комнаты).</summary>
        public IReadOnlyList<string> MapNames
        {
            get
            {
                var names = new List<string>();
                if (mapButtons != null)
                {
                    foreach (Button button in mapButtons)
                    {
                        Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
                        names.Add(label != null ? label.text : string.Empty);
                    }
                }

                return names;
            }
        }

        private void ShowFog()
        {
            if (fogLabel != null)
            {
                fogLabel.text = MapChoice.FogOfWar ? fogOnText : fogOffText;
            }
        }

        private void StartBattle(bool tutorial)
        {
            MapChoice.Tutorial = tutorial;
            SceneManager.LoadScene(battleSceneName);
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
