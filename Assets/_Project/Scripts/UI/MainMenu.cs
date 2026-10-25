using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// Главное меню: «Начать» открывает сцену боя. В development-сборке есть ещё кнопка сцены-нагрузки
    /// для замера производительности.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private string battleSceneName = "Battle";
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
        }
    }
}
