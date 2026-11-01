using Marchlands.Network;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Victory;
using Marchlands.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Пауза и экран результата матча. Пауза останавливает время (а с ним и тики симуляции),
    /// «Заново» перезапускает текущую сцену, «В меню» открывает главное меню. На паузе музыка затихает.
    /// </summary>
    public sealed class GameMenu : MonoBehaviour
    {
        [SerializeField] private Button pauseButton;

        [Header("Пауза")]
        [SerializeField] private GameObject pauseOverlay;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseRestartButton;
        [SerializeField] private Button pauseMenuButton;
        [Tooltip("Выход из игры из меню паузы.")]
        [SerializeField] private Button pauseQuitButton;
        [Tooltip("Громкость музыки в меню паузы и её подпись ({0} — проценты).")]
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Text musicVolumeLabel;
        [SerializeField] private string musicVolumeFormat = "Громкость музыки: {0}%";

        [Header("Результат")]
        [SerializeField] private GameObject resultOverlay;
        [SerializeField] private Text resultTitle;
        [SerializeField] private Text resultReason;
        [SerializeField] private Button resultRestartButton;
        [SerializeField] private Button resultMenuButton;
        [Tooltip("Салют на экране победы.")]
        [SerializeField] private FireworksView fireworks;
        [SerializeField] private string victoryText = "Победа!";
        [SerializeField] private string defeatText = "Поражение";
        [SerializeField] private string headquartersReason = "Разрушен штаб";
        [SerializeField] private string dominationReason = "Удержание деревень";
        [SerializeField] private string annihilationReason = "Армия разбита";

        [SerializeField] private string menuSceneName = "MainMenu";

        [Header("Сетевая игра")]
        [SerializeField] private string opponentLeftReason = "Соперник покинул бой";
        [SerializeField] private string connectionLostTitle = "Бой прерван";
        [SerializeField] private string connectionLostReason = "Связь с соперником потеряна";
        [SerializeField] private string desyncReason = "Игры разошлись (рассинхронизация) — бой остановлен";
        [SerializeField] private string waitingText = "Ожидание соперника…";
        [SerializeField] private Color waitingColor = new Color(0.96f, 0.9f, 0.75f);
        [Tooltip("Строка о сети в сетевом бою: {0} — пинг, мс; {1} — через сколько исполняется приказ, с.")]
        [SerializeField] private string networkInfoFormat = "Пинг {0} мс · приказ через {1:0.0} с";
        [SerializeField] private Color networkInfoColor = new Color(0.96f, 0.9f, 0.75f, 0.7f);

        [Header("Справка по клавишам (ПК)")]
        [SerializeField, TextArea(8, 16)] private string hotkeysText =
            "<b>Клавиши и мышь</b>\n\n" +
            "ЛКМ — выбрать отряд или здание; Shift + рамка — выделить несколько; Ctrl + щелчок — добавить или убрать\n" +
            "Двойной щелчок по отряду — все такие же на экране\n" +
            "ПКМ — идти или атаковать; ПКМ с протяжкой — встать фронтом в сторону протяжки\n" +
            "Потянуть ЛКМ за кольцо вокруг отряда — развернуть на месте\n\n" +
            "Пробел — залп   ·   C — в штыки   ·   R — линия / марш   ·   X — стоп\n" +
            "H — штаб   ·   B — казарма   ·   Tab — следующий отряд\n" +
            "Ctrl + 1…9 — запомнить группу; 1…9 — выбрать её; дважды — показать\n" +
            "WASD или стрелки, край экрана, средняя кнопка — двигать карту; колесо — масштаб\n\n" +
            "Pause или P — пауза   ·   Esc — отменить   ·   F1 — эта справка\n" +
            "Наведите курсор на кнопку — появится подсказка.";
        [SerializeField] private Color helpBackground = new Color(0.12f, 0.1f, 0.08f, 0.92f);

        private GameObject helpPanel;
        private bool isNetwork;
        private Text waitingLabel;
        private Text networkInfoLabel;

        private Match match;
        private Side playerSide;
        private MusicDirector music;
        private bool resultShown;

        public void Initialize(Match match, Side playerSide, MusicDirector music)
        {
            this.match = match;
            this.playerSide = playerSide;
            this.music = music;
            Time.timeScale = 1f;

            pauseButton.onClick.AddListener(Pause);
            resumeButton.onClick.AddListener(Resume);
            pauseRestartButton.onClick.AddListener(Restart);
            pauseMenuButton.onClick.AddListener(OpenMenu);
            if (pauseQuitButton != null)
            {
                pauseQuitButton.onClick.AddListener(Application.Quit);
            }
            resultRestartButton.onClick.AddListener(Restart);
            resultMenuButton.onClick.AddListener(OpenMenu);

            pauseOverlay.SetActive(false);
            resultOverlay.SetActive(false);
            pauseButton.gameObject.SetActive(match.VictoryEnabled);
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.minValue = 0f;
                musicVolumeSlider.maxValue = 1f;
                musicVolumeSlider.SetValueWithoutNotify(MusicDirector.PlayerVolume);
                ShowMusicVolume(MusicDirector.PlayerVolume);
                musicVolumeSlider.onValueChanged.AddListener(value =>
                {
                    MusicDirector.PlayerVolume = value;
                    ShowMusicVolume(value);
                });
            }
        }

        private void ShowMusicVolume(float value)
        {
            if (musicVolumeLabel != null)
            {
                musicVolumeLabel.text = string.Format(musicVolumeFormat, Mathf.RoundToInt(value * 100f));
            }
        }

        /// <summary>
        /// Сетевой матч: меню паузы открывается, но время не останавливается (соперник играет дальше), «Заново» нет,
        /// выход в меню или из игры сообщает сопернику.
        /// </summary>
        public void SetNetworkMatch()
        {
            isNetwork = true;
            pauseRestartButton.gameObject.SetActive(false);
            resultRestartButton.gameObject.SetActive(false);
            if (pauseQuitButton != null)
            {
                pauseQuitButton.onClick.RemoveAllListeners();
                pauseQuitButton.onClick.AddListener(() =>
                {
                    LeaveNetwork();
                    Application.Quit();
                });
            }
        }

        /// <summary>Сетевой матч закончился не по правилам боя: соперник ушёл, связь потеряна или игры разошлись.</summary>
        public void ShowNetworkEnd(bool opponentLeft, bool desync)
        {
            if (resultShown)
            {
                return;
            }

            resultShown = true;
            SetWaiting(false);
            resultTitle.text = opponentLeft ? victoryText : connectionLostTitle;
            resultReason.text = opponentLeft ? opponentLeftReason : desync ? desyncReason : connectionLostReason;
            pauseOverlay.SetActive(false);
            pauseButton.gameObject.SetActive(false);
            resultOverlay.SetActive(true);
            if (opponentLeft && fireworks != null)
            {
                fireworks.Play();
            }
        }

        public bool IsOver => resultShown;

        /// <summary>F1: показать или скрыть справку по клавишам.</summary>
        public void ToggleHelp()
        {
            if (helpPanel == null)
            {
                helpPanel = new GameObject("Справка по клавишам", typeof(RectTransform), typeof(Image), typeof(Button));
                var rect = (RectTransform)helpPanel.transform;
                rect.SetParent(resultOverlay.transform.parent, false);
                rect.sizeDelta = new Vector2(1500f, 760f);
                var background = helpPanel.GetComponent<Image>();
                background.color = helpBackground;
                var sample = pauseButton.GetComponent<Image>();
                background.sprite = sample != null ? sample.sprite : null;
                background.type = Image.Type.Sliced;
                // Щелчок по справке закрывает её.
                helpPanel.GetComponent<Button>().onClick.AddListener(() => helpPanel.SetActive(false));

                var textObject = new GameObject("Текст", typeof(RectTransform), typeof(Text));
                var textRect = (RectTransform)textObject.transform;
                textRect.SetParent(rect, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(60f, 40f);
                textRect.offsetMax = new Vector2(-60f, -40f);
                var text = textObject.GetComponent<Text>();
                text.font = resultTitle.font;
                text.fontSize = 34;
                text.lineSpacing = 1.15f;
                text.color = new Color(0.96f, 0.92f, 0.82f);
                text.alignment = TextAnchor.MiddleLeft;
                text.supportRichText = true;
                text.raycastTarget = false;
                text.text = hotkeysText;
                helpPanel.SetActive(false);
            }

            helpPanel.transform.SetAsLastSibling();
            helpPanel.SetActive(!helpPanel.activeSelf);
        }

        /// <summary>Пинг и задержка приказа в сетевом бою — мелко в правом нижнем углу.</summary>
        public void SetNetworkInfo(int pingMs, float delaySeconds)
        {
            if (networkInfoLabel == null)
            {
                var labelObject = new GameObject("Сеть", typeof(RectTransform), typeof(Text), typeof(Outline));
                var rect = (RectTransform)labelObject.transform;
                rect.SetParent(resultOverlay.transform.parent, false);
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-24f, 12f);
                rect.sizeDelta = new Vector2(700f, 40f);
                networkInfoLabel = labelObject.GetComponent<Text>();
                networkInfoLabel.font = resultTitle.font;
                networkInfoLabel.fontSize = 26;
                networkInfoLabel.alignment = TextAnchor.LowerRight;
                networkInfoLabel.color = networkInfoColor;
                networkInfoLabel.raycastTarget = false;
            }

            networkInfoLabel.text = string.Format(System.Globalization.CultureInfo.GetCultureInfo("ru-RU"), networkInfoFormat, pingMs, delaySeconds);
        }

        /// <summary>Надпись «Ожидание соперника…», пока он отстаёт или его приказы не дошли.</summary>
        public void SetWaiting(bool waiting)
        {
            if (waitingLabel == null)
            {
                if (!waiting)
                {
                    return;
                }

                var labelObject = new GameObject("Ожидание соперника", typeof(RectTransform), typeof(Text), typeof(Outline));
                var rect = (RectTransform)labelObject.transform;
                rect.SetParent(resultOverlay.transform.parent, false);
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -130f);
                rect.sizeDelta = new Vector2(900f, 70f);
                waitingLabel = labelObject.GetComponent<Text>();
                waitingLabel.font = resultTitle.font;
                waitingLabel.fontSize = 44;
                waitingLabel.alignment = TextAnchor.MiddleCenter;
                waitingLabel.color = waitingColor;
                waitingLabel.raycastTarget = false;
                waitingLabel.text = waitingText;
            }

            if (waitingLabel.gameObject.activeSelf != waiting)
            {
                waitingLabel.gameObject.SetActive(waiting);
            }
        }

        private void Update()
        {
            if (match == null || resultShown || match.Result == null)
            {
                return;
            }

            resultShown = true;
            MatchResult result = match.Result;
            resultTitle.text = result.Winner == playerSide ? victoryText : defeatText;
            resultReason.text = result.Reason switch
            {
                VictoryReason.HeadquartersDestroyed => headquartersReason,
                VictoryReason.Domination => dominationReason,
                _ => annihilationReason
            };
            pauseOverlay.SetActive(false);
            pauseButton.gameObject.SetActive(false);
            resultOverlay.SetActive(true);
            if (fireworks != null && result.Winner == playerSide)
            {
                fireworks.Play();
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        /// <summary>Пауза или продолжение (клавиша паузы на ПК).</summary>
        public void TogglePause()
        {
            if (resultShown || match == null || !match.VictoryEnabled)
            {
                return;
            }

            if (pauseOverlay.activeSelf)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        private void Pause()
        {
            pauseOverlay.SetActive(true);
            if (isNetwork)
            {
                return;
            }

            Time.timeScale = 0f;
            SetMusicPaused(true);
        }

        private void Resume()
        {
            Time.timeScale = 1f;
            pauseOverlay.SetActive(false);
            SetMusicPaused(false);
        }

        private void LeaveNetwork()
        {
            if (!isNetwork || !NetworkGame.IsActive)
            {
                return;
            }

            NetworkGame.SendToPeer(LobbyProtocol.Quit());
            NetworkGame.Shutdown();
        }

        private void SetMusicPaused(bool paused)
        {
            if (music != null)
            {
                music.SetPaused(paused);
            }
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void OpenMenu()
        {
            LeaveNetwork();
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
