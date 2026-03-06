using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    [Header("UI")]
    public UIDocument mainMenuUI;
    VisualElement rootElement;
    Button playButton;
    void Awake()
    {
        rootElement = mainMenuUI.rootVisualElement;
        playButton = rootElement.Q<Button>("play-button");
        playButton.clicked += OnPlayButtonClicked;

        Time.timeScale = 0f;   
    }
    // Update is called once per frame
    void OnPlayButtonClicked()
    {
        Time.timeScale = 1f;
        mainMenuUI.enabled = false;
        //UnityEngine.SceneManagement.SceneManager.LoadScene("MapScene");
    }
}
