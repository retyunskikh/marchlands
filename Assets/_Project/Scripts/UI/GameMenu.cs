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
