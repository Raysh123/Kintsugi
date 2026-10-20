using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A small options panel: music and effects volume. Used by both the main menu and the pause menu - put one in each
/// scene that needs it, assign the sliders, and call Open() from a button (or let those menus do it).
/// Closes with the Back button, B on a gamepad, or Escape. Volumes are saved and applied when the scene starts
/// (needs an AudioMixer assigned on the scene's AudioManager).
/// </summary>
public class OptionsPanel : MonoBehaviour
{
    private const string MusicKey = "Options_MusicVolume";
    private const string SfxKey = "Options_SfxVolume";

    [SerializeField] private GameObject panel;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Button backButton;

    public bool IsOpen { get; private set; }
    /// <summary>The frame the panel last closed, so a menu can ignore the same key press.</summary>
    public int LastCloseFrame { get; private set; } = -1;

    public event Action OnClosed;

    private void Start()
    {
        if (musicSlider != null)
        {
            musicSlider.minValue = 0f; musicSlider.maxValue = 1f;
            musicSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(MusicKey, 1f));
            musicSlider.onValueChanged.AddListener(OnMusicChanged);
        }
        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f; sfxSlider.maxValue = 1f;
            sfxSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(SfxKey, 1f));
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        }
        if (backButton != null) backButton.onClick.AddListener(Close);

        ApplyVolumes();
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1)) Close();
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        if (panel != null) panel.SetActive(true);

        GameObject first = musicSlider != null ? musicSlider.gameObject : (backButton != null ? backButton.gameObject : null);
        if (first != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        LastCloseFrame = Time.frameCount;
        PlayerPrefs.Save();
        if (panel != null) panel.SetActive(false);
        OnClosed?.Invoke();
    }

    private void OnMusicChanged(float v)
    {
        PlayerPrefs.SetFloat(MusicKey, v);
        if (AudioManager.instance != null) AudioManager.instance.SetMusicVolume01(v);
    }

    private void OnSfxChanged(float v)
    {
        PlayerPrefs.SetFloat(SfxKey, v);
        if (AudioManager.instance != null) AudioManager.instance.SetSfxVolume01(v);
    }

    private void ApplyVolumes()
    {
        if (AudioManager.instance == null) return;
        AudioManager.instance.SetMusicVolume01(PlayerPrefs.GetFloat(MusicKey, 1f));
        AudioManager.instance.SetSfxVolume01(PlayerPrefs.GetFloat(SfxKey, 1f));
    }
}
