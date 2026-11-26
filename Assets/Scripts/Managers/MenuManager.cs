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

        if (mainMenuPanel != null)
        {
            mainMenuPanel.DOFade(0f, 0.4f).SetUpdate(true).OnComplete(() =>
            {
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
                if (isGameActive && (mainMenuPanel == null || !mainMenuPanel.gameObject.activeSelf))
                {
                    Time.timeScale = 1f;
                }
            });
        }
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
