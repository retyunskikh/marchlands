using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>Главное меню: «Начать» открывает сцену боя.</summary>
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private string battleSceneName = "Battle";
        [SerializeField] private int targetFrameRate = 60;

        private void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Time.timeScale = 1f;
            startButton.onClick.AddListener(() => SceneManager.LoadScene(battleSceneName));
        }
    }
}
