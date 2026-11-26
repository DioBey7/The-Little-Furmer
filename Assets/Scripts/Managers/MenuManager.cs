using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using DG.Tweening;

public class MenuManager : MonoBehaviour
{
    public CanvasGroup mainMenuPanel;
    public CanvasGroup optionsPanel;
    [Tooltip("Optional full-screen dim shown behind the options panel.")]
    public CanvasGroup optionsBackdrop;
    public AudioMixer mainMixer;
    public Slider musicSlider;
    public Slider sfxSlider;
    public AudioSource bgmSource;

    private bool isGameActive = false;

    private void Awake()
    {
        DOTween.Init(true, true, LogBehaviour.ErrorsOnly);
    }

    private void Start()
    {
        Time.timeScale = 0f;

        if (mainMenuPanel != null)
        {
            mainMenuPanel.gameObject.SetActive(true);
            mainMenuPanel.alpha = 1f;
        }

        if (optionsPanel != null) optionsPanel.gameObject.SetActive(false);
        if (optionsBackdrop != null) optionsBackdrop.gameObject.SetActive(false);
        if (bgmSource != null && !bgmSource.isPlaying) bgmSource.Play();

        SetupAudioSliders();
        ApplyMenuLayout();
        PlayMenuIntro();

        // Keep the farm visible behind the main menu but hide the in-game HUD until Play.
        if (hudGroup != null)
        {
            hudGroup.alpha = 0f;
            hudGroup.interactable = false;
            hudGroup.blocksRaycasts = false;
        }
    }

    [Tooltip("Optional CanvasGroup on the in-game HUD canvas; hidden while the main menu is up.")]
    public CanvasGroup hudGroup;

    [Tooltip("Optional orbiting camera shown behind the main menu.")]
    public MenuShowcase showcase;

    [Header("Main menu intro (optional)")]
    public RectTransform menuTitle;
    public RectTransform[] menuButtons;

    private void ApplyMenuLayout()
    {
        // The showcase camera keeps the farmer in the middle of the screen,
        // so the title sits above the farmer and the buttons below.
        if (menuTitle != null)
        {
            menuTitle.anchorMin = menuTitle.anchorMax = new Vector2(0.5f, 0.85f);
            menuTitle.anchoredPosition = Vector2.zero;
        }
        if (menuButtons != null && menuButtons.Length > 0 && menuButtons[0] != null && menuButtons[0].parent is RectTransform column)
        {
            column.anchorMin = column.anchorMax = new Vector2(0.5f, 0.24f);
            column.anchoredPosition = Vector2.zero;
        }
    }

    private void PlayMenuIntro()
    {
        if (menuTitle != null)
        {
            Vector2 home = menuTitle.anchoredPosition;
            menuTitle.DOKill();
            menuTitle.anchoredPosition = home + new Vector2(0f, 260f);
            menuTitle.localScale = Vector3.one * 0.85f;
            DOTween.Sequence().SetUpdate(true).SetTarget(menuTitle)
                .Append(menuTitle.DOAnchorPos(home, 0.7f).SetEase(Ease.OutBounce))
                .Join(menuTitle.DOScale(1f, 0.7f).SetEase(Ease.OutBack))
                .AppendCallback(() =>
                {
                    // Gentle idle bob so the menu doesn't feel frozen.
                    if (menuTitle != null)
                        menuTitle.DOAnchorPosY(home.y + 12f, 1.4f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
                });
        }

        if (menuButtons != null)
        {
            for (int i = 0; i < menuButtons.Length; i++)
            {
                RectTransform b = menuButtons[i];
                if (b == null) continue;
                b.DOKill();
                b.localScale = Vector3.zero;
                b.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetDelay(0.45f + i * 0.12f).SetUpdate(true);
            }
        }
    }

    private void SetupAudioSliders()
    {
        float musicVol = PlayerPrefs.HasKey("MusicVol") ? PlayerPrefs.GetFloat("MusicVol") : 0.5f;
        if (musicSlider != null)
        {
            musicSlider.value = musicVol;
            SetMusicVolume(musicVol);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        float sfxVol = PlayerPrefs.HasKey("SFXVol") ? PlayerPrefs.GetFloat("SFXVol") : 1f;
        if (sfxSlider != null)
        {
            sfxSlider.value = sfxVol;
            SetSFXVolume(sfxVol);
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }
    }

    public void PlayGame()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayUIClickSound();
            SoundManager.Instance.SwitchToGameMusic();
        }

        if (showcase != null) showcase.End();

        if (hudGroup != null)
        {
            hudGroup.DOKill();
            hudGroup.interactable = true;
            hudGroup.blocksRaycasts = true;
            hudGroup.DOFade(1f, 0.4f).SetDelay(0.2f).SetUpdate(true);
        }

        if (mainMenuPanel != null)
        {
            mainMenuPanel.DOFade(0f, 0.4f).SetUpdate(true).OnComplete(() =>
            {
                if (menuTitle != null) menuTitle.DOKill();
                mainMenuPanel.gameObject.SetActive(false);
                isGameActive = true;
                Time.timeScale = 1f;
            });
        }
        else
        {
            isGameActive = true;
            Time.timeScale = 1f;
        }
    }

    public void ToggleInGameMenu()
    {
        if (!isGameActive) return;

        if (optionsPanel != null && optionsPanel.gameObject.activeSelf)
        {
            CloseOptions();
        }
        else
        {
            OpenOptions();
        }
    }

    public void OpenOptions()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlayUIClickSound();
        if (isGameActive) Time.timeScale = 0f;
        SetMenuColumnVisible(false);

        if (optionsBackdrop != null)
        {
            optionsBackdrop.DOKill();
            optionsBackdrop.gameObject.SetActive(true);
            optionsBackdrop.alpha = 0f;
            optionsBackdrop.DOFade(1f, 0.25f).SetUpdate(true);
        }

        if (optionsPanel != null)
        {
            optionsPanel.DOKill();
            optionsPanel.transform.DOKill();
            optionsPanel.gameObject.SetActive(true);
            optionsPanel.alpha = 0f;
            optionsPanel.transform.localScale = Vector3.one * 0.8f;
            optionsPanel.DOFade(1f, 0.3f).SetUpdate(true);
            optionsPanel.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
        }
    }

    public void CloseOptions()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlayUIClickSound();

        if (optionsBackdrop != null)
        {
            optionsBackdrop.DOKill();
            optionsBackdrop.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() => optionsBackdrop.gameObject.SetActive(false));
        }

        if (optionsPanel != null)
        {
            optionsPanel.DOKill();
            optionsPanel.transform.DOKill();
            optionsPanel.DOFade(0f, 0.2f).SetUpdate(true);
            optionsPanel.transform.DOScale(Vector3.one * 0.8f, 0.2f).SetEase(Ease.InBack).SetUpdate(true).OnComplete(() =>
            {
                optionsPanel.gameObject.SetActive(false);
                SetMenuColumnVisible(true);
                if (isGameActive && (mainMenuPanel == null || !mainMenuPanel.gameObject.activeSelf))
                {
                    Time.timeScale = 1f;
                }
            });
        }
    }

    // The main menu buttons sit under the options card; hide them while it is open.
    private void SetMenuColumnVisible(bool visible)
    {
        if (menuButtons == null || menuButtons.Length == 0 || menuButtons[0] == null) return;
        Transform column = menuButtons[0].parent;
        if (column != null) column.gameObject.SetActive(visible);
    }

    private void SetMusicVolume(float sliderValue)
    {
        if (mainMixer != null)
        {
            float clampedValue = Mathf.Clamp(sliderValue, 0.0001f, 1f);
            mainMixer.SetFloat("Music", Mathf.Log10(clampedValue) * 20);
            PlayerPrefs.SetFloat("MusicVol", sliderValue);
        }
    }

    private void SetSFXVolume(float sliderValue)
    {
        if (mainMixer != null)
        {
            float clampedValue = Mathf.Clamp(sliderValue, 0.0001f, 1f);
            mainMixer.SetFloat("SFX", Mathf.Log10(clampedValue) * 20);
            PlayerPrefs.SetFloat("SFXVol", sliderValue);
        }
    }
}
