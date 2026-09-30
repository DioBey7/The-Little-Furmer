using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    public AudioSource bgmSource;
    public AudioSource sfxSource;

    public AudioClip menuMusic;
    public AudioClip gameMusic;

    public AudioClip levelUpClip;
    public AudioClip objectiveCompleteClip;
    public AudioClip customerHappyClip;
    public AudioClip customerSadClip;
    public AudioClip uiClickClip;
    public AudioClip unlockAreaClip;
    public AudioClip buyAiClip;
    public AudioClip upgradeClip;
    public AudioClip produceClip;

    public AudioClip cashPickupClip;
    public AudioClip itemPickupClip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (bgmSource != null && menuMusic != null)
        {
            bgmSource.clip = menuMusic;
            bgmSource.loop = true;
            bgmSource.volume = 1f;
            bgmSource.Play();
        }
    }

    public void SwitchToGameMusic()
    {
        if (bgmSource != null && gameMusic != null && bgmSource.clip != gameMusic)
        {
            bgmSource.Stop();
            bgmSource.clip = gameMusic;
            bgmSource.Play();
        }
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }

    public void PlayUIClickSound()
    {
        if (uiClickClip != null)
        {
            PlaySFX(uiClickClip);
        }
    }

    public void PlayCashPickupSound()
    {
        if (cashPickupClip != null)
        {
            PlaySFX(cashPickupClip);
        }
    }

    public void PlayItemPickupSound(AudioClip specificClip = null)
    {
        if (specificClip != null)
        {
            PlaySFX(specificClip);
        }
        else if (itemPickupClip != null)
        {
            PlaySFX(itemPickupClip);
        }
    }
}
