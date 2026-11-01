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
            resultRestartButton.onClick.AddListener(Restart);
            resultMenuButton.onClick.AddListener(OpenMenu);

            pauseOverlay.SetActive(false);
            resultOverlay.SetActive(false);
            pauseButton.gameObject.SetActive(match.VictoryEnabled);
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
            Time.timeScale = 0f;
            pauseOverlay.SetActive(true);
            SetMusicPaused(true);
        }

        private void Resume()
        {
            Time.timeScale = 1f;
            pauseOverlay.SetActive(false);
            SetMusicPaused(false);
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
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
