using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Dapaolou.UI
{
    /// <summary>
    /// 音乐控制器挂件：上一首/暂停/下一首 + 音量滑条 + 曲名显示（数据源 = AudioManager 的 BGM 轮播列表）
    /// </summary>
    public class MusicController : MonoBehaviour
    {
        [SerializeField] private Button prevButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TextMeshProUGUI trackText;
        [SerializeField] private TextMeshProUGUI pauseLabel;

        private string lastTrackName;
        private bool lastPaused;

        private Dapaolou.Audio.AudioManager AM => Dapaolou.Audio.AudioManager.Instance;

        void Start()
        {
            if (prevButton != null) prevButton.onClick.AddListener(() => { if (AM != null) AM.PrevBgm(); });
            if (pauseButton != null) pauseButton.onClick.AddListener(TogglePause);
            if (nextButton != null) nextButton.onClick.AddListener(() => { if (AM != null) AM.NextBgm(); });
            if (volumeSlider != null)
            {
                var saved = PlayerPrefs.GetFloat("set_volume", AM != null ? AM.bgmVolume : 0.4f);
                volumeSlider.SetValueWithoutNotify(saved);
                if (AM != null) AM.SetBgmVolume(saved);
                volumeSlider.onValueChanged.AddListener(v =>
                {
                    if (AM != null) AM.SetBgmVolume(v);
                    PlayerPrefs.SetFloat("set_volume", v);
                });
            }
        }

        void Update()
        {
            if (AM == null) return;
            var name = AM.CurrentBgmName();
            if (name != lastTrackName)
            {
                lastTrackName = name;
                if (trackText != null) trackText.text = name;
            }
            if (AM.IsBgmPaused != lastPaused)
            {
                lastPaused = AM.IsBgmPaused;
                if (pauseLabel != null) pauseLabel.text = lastPaused ? "播放" : "暂停";
            }
        }

        private void TogglePause()
        {
            if (AM != null) AM.ToggleBgmPause();
        }
    }
}
