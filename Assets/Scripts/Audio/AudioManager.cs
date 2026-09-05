using System.Collections.Generic;
using UnityEngine;
using Dapaolou.Game;

namespace Dapaolou.Audio
{
    /// <summary>
    /// 音频管理器 - 管理 BGM 和游戏音效（单例，跨场景保留）
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("背景音乐")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioClip bgmClip;
        [Range(0f, 1f)] public float bgmVolume = 0.4f;

        [Header("BGM 播放列表")]
        [SerializeField] private List<AudioClip> bgmPlaylist = new List<AudioClip>();

        private int bgmIndex = -1;
        private bool bgmPaused;

        [Header("音效片段")]
        [SerializeField] private AudioClip marbleHit;       // 弹珠互撞
        [SerializeField] private AudioClip marbleRoll;      // 滚动摩擦
        [SerializeField] private AudioClip marbleFlick;     // 发射弹射
        [SerializeField] private AudioClip glassShatter;    // 玻璃碎裂
        [SerializeField] private AudioClip waterSplash;     // 入水声
        [SerializeField] private AudioClip bounceCement;    // 水泥地弹跳（清脆）
        [SerializeField] private AudioClip bounceDirt;      // 泥土地弹跳（闷响）

        [Range(0f, 1f)] public float sfxVolume = 0.7f;

        private AudioSource sfxSource;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // BGM 音源
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.volume = bgmVolume;
            bgmSource.playOnAwake = false;
            if (bgmClip != null)
            {
                bgmSource.clip = bgmClip;
            }

            // SFX 音源
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.volume = sfxVolume;
            sfxSource.playOnAwake = false;
        }

        void Start()
        {
            // 播放 BGM：有播放列表则从第一首开始轮换，否则退回单曲循环
            if (bgmPlaylist != null && bgmPlaylist.Count > 0)
            {
                bgmSource.loop = false;
                PlayBgmByIndex(0);
            }
            else if (bgmSource != null && bgmSource.clip != null)
            {
                bgmSource.Play();
            }
        }

        void Update()
        {
            // 曲终自动轮换下一首（手动暂停时不触发）
            if (!bgmPaused && bgmPlaylist.Count > 1 && bgmSource.clip != null && !bgmSource.isPlaying)
                PlayBgmByIndex(bgmIndex + 1);
        }

        /// <summary>
        /// 按索引播放 BGM（越界自动循环）
        /// </summary>
        public void PlayBgmByIndex(int index)
        {
            if (bgmPlaylist == null || bgmPlaylist.Count == 0 || bgmSource == null) return;
            bgmIndex = ((index % bgmPlaylist.Count) + bgmPlaylist.Count) % bgmPlaylist.Count;
            var clip = bgmPlaylist[bgmIndex];
            if (clip == null) return;
            bgmClip = clip;
            bgmSource.clip = clip;
            bgmSource.loop = false;
            bgmPaused = false;
            bgmSource.Play();
        }

        /// <summary>
        /// 下一首
        /// </summary>
        public void NextBgm() => PlayBgmByIndex(bgmIndex + 1);

        /// <summary>
        /// 上一首
        /// </summary>
        public void PrevBgm() => PlayBgmByIndex(bgmIndex - 1);

        /// <summary>
        /// 暂停/继续当前 BGM
        /// </summary>
        public void ToggleBgmPause()
        {
            if (bgmSource == null || bgmSource.clip == null) return;
            if (bgmPaused) bgmSource.UnPause();
            else bgmSource.Pause();
            bgmPaused = !bgmPaused;
        }

        /// <summary>
        /// 当前曲名（去 bgm_ 前缀）
        /// </summary>
        public string CurrentBgmName()
        {
            var clip = bgmSource != null ? bgmSource.clip : null;
            if (clip == null) return "无";
            var n = clip.name;
            return n.StartsWith("bgm_") ? n.Substring(4) : n;
        }

        /// <summary>
        /// BGM 是否处于暂停
        /// </summary>
        public bool IsBgmPaused => bgmPaused;

        /// <summary>
        /// 设置 BGM 音频文件（运行时加载）
        /// </summary>
        public void SetBgmClip(AudioClip clip)
        {
            bgmClip = clip;
            if (bgmSource != null)
            {
                bgmSource.clip = clip;
                bgmSource.Play();
            }
        }

        /// <summary>
        /// 播放一次性音效
        /// </summary>
        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip != null && sfxSource != null)
                sfxSource.PlayOneShot(clip, volumeScale);
        }

        /// <summary>
        /// 播放弹珠互撞音效
        /// </summary>
        public void PlayMarbleHit(float intensity = 1f)
        {
            if (marbleHit != null)
                sfxSource.PlayOneShot(marbleHit, Mathf.Clamp01(intensity) * sfxVolume);
        }

        /// <summary>
        /// 播放弹珠滚动音效
        /// </summary>
        public void PlayMarbleRoll()
        {
            if (marbleRoll != null)
                sfxSource.PlayOneShot(marbleRoll, sfxVolume * 0.6f);
        }

        /// <summary>
        /// 播放发射弹射音效
        /// </summary>
        public void PlayMarbleFlick(float power = 1f)
        {
            if (marbleFlick != null)
                sfxSource.PlayOneShot(marbleFlick, Mathf.Clamp01(power) * sfxVolume);
        }

        /// <summary>
        /// 播放玻璃碎裂音效（弹珠被摧毁）
        /// </summary>
        public void PlayGlassShatter(float intensity = 1f)
        {
            if (glassShatter != null)
                sfxSource.PlayOneShot(glassShatter, Mathf.Clamp01(intensity) * sfxVolume);
        }

        /// <summary>
        /// 播放入水声（弹珠落入水坑）
        /// </summary>
        public void PlayWaterSplash()
        {
            if (waterSplash != null)
                sfxSource.PlayOneShot(waterSplash, sfxVolume);
        }

        /// <summary>
        /// 播放弹珠落地弹跳音效（onCement=水泥等硬面清脆声，否则泥地闷响）
        /// </summary>
        public void PlayBounce(bool onCement, float intensity = 1f)
        {
            var clip = onCement ? bounceCement : bounceDirt;
            if (clip != null)
                sfxSource.PlayOneShot(clip, Mathf.Clamp01(intensity) * sfxVolume);
        }

        /// <summary>
        /// 设置 BGM 音量
        /// </summary>
        public void SetBgmVolume(float vol)
        {
            bgmVolume = Mathf.Clamp01(vol);
            if (bgmSource != null) bgmSource.volume = bgmVolume;
        }

        /// <summary>
        /// 设置音效音量
        /// </summary>
        public void SetSfxVolume(float vol)
        {
            sfxVolume = Mathf.Clamp01(vol);
            if (sfxSource != null) sfxSource.volume = sfxVolume;
        }
    }
}
