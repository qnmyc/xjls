using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }

    [Header("Fade Transiton")]
    [SerializeField] private Image fadeImage;          
    [SerializeField] private float fadeDuration = 0.5f; 

    private string currentChapterName;                 
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    
         Instance = this;
        
        
    }

    void Start()
    {
        StartCoroutine(StartupRoutine());
    }

    private IEnumerator StartupRoutine()
    {
        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);

        AsyncOperation loadOp = SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Additive);
        while (!loadOp.isDone)
            yield return null;

        Scene mainMenuScene = SceneManager.GetSceneByName("MainMenu");
        SceneManager.SetActiveScene(mainMenuScene);
        currentChapterName = "MainMenu";

        Debug.Log("MainMenu º”‘ÿÕÍ≥…");
    }


    public IEnumerator FadeOut()
    {
        if (fadeImage == null)
        {
            Debug.LogError("FadeOut failed: fadeImage is null");
            yield break;
        }
        float timer = 0f;
        Color color = fadeImage.color;
        color.a = 0f;
        fadeImage.color = color;
        fadeImage.gameObject.SetActive(true);

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            color.a = Mathf.Clamp01(timer / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }
    }

    
    public IEnumerator FadeIn()
    {
        if (fadeImage == null)
        {
            Debug.LogError("FadeIn failed: fadeImage is null");
            yield break;
        }

        float timer = 0f;
        Color color = fadeImage.color;
        color.a = 1f;
        fadeImage.color = color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            color.a = 1f - Mathf.Clamp01(timer / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        fadeImage.gameObject.SetActive(false);
    }

    
    
    public void SwitchChapter(string newChapterName)
    {
        Debug.Log($"Switching from {currentChapterName} to {newChapterName}");
        StartCoroutine(SwitchChapterRoutine(newChapterName));
    }

    private IEnumerator SwitchChapterRoutine(string newChapterName)
    {
        
        yield return StartCoroutine(FadeOut());

        
        if (!string.IsNullOrEmpty(currentChapterName))
        {
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(currentChapterName);
            while (!unloadOp.isDone)
                yield return null;
        }

        
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(newChapterName, LoadSceneMode.Additive);
        while (!loadOp.isDone)
            yield return null;

       
        Scene newScene = SceneManager.GetSceneByName(newChapterName);
        SceneManager.SetActiveScene(newScene);
        currentChapterName = newChapterName;
        InitializeChapter(newChapterName);

        
        yield return StartCoroutine(FadeIn());
    }

  
    private void InitializeChapter(string chapterName)
    {
        Debug.Log($"Chapter {chapterName} Initialization completed");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameStateMachine.GameState.Explore);
        }
    }

        
}
