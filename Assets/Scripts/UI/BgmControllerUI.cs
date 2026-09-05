using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Dapaolou.Audio;

namespace Dapaolou.UI
{
    /// <summary>
    /// 背景音乐控制器：上一首 / 暂停继续 / 下一首 + 当前曲名显示
    /// 挂在 GameCanvas 的 BgmController 面板上，按钮点击走 AudioManager 单例
    /// </summary>
    public class BgmControllerUI : MonoBehaviour
    {
        [SerializeField] private Button prevButton;
        [SerializeField] private Button toggleButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TextMeshProUGUI trackLabel;   // 当前曲名
        [SerializeField] private TextMeshProUGUI toggleLabel;  // 暂停按钮上的字（暂停/播放）

        void Start()
        {
            if (prevButton != null) prevButton.onClick.AddListener(() => { AudioManager.Instance?.PrevBgm(); });
            if (nextButton != null) nextButton.onClick.AddListener(() => { AudioManager.Instance?.NextBgm(); });
            if (toggleButton != null) toggleButton.onClick.AddListener(() => { AudioManager.Instance?.ToggleBgmPause(); });
        }

        void Update()
        {
            // 轮询刷新（曲终自动轮换、暂停态变化都要反映到文字上，开销可忽略）
            var am = AudioManager.Instance;
            if (am == null) return;
            if (trackLabel != null)
            {
                var name = am.CurrentBgmName();
                if (trackLabel.text != name) trackLabel.text = name;
            }
            if (toggleLabel != null)
            {
                var t = am.IsBgmPaused ? "播放" : "暂停";
                if (toggleLabel.text != t) toggleLabel.text = t;
            }
        }
    }
}
