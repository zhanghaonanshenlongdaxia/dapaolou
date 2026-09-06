using UnityEngine;

namespace Dapaolou.UI
{
    /// <summary>
    /// 设置面板：按 ESC 开/关。打开时解锁鼠标供 UI 操作，并屏蔽第一人称视角/移动与射击输入
    /// （FirstPersonController 与 MarbleShooter 的 Update 通过 SettingsPanel.IsOpen 自行屏蔽）。
    /// 本脚本挂在常驻的 SettingsRoot 空物体上，panelRoot 指向可视面板子物体。
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        [SerializeField] private GameObject panelRoot;

        void Start()
        {
            // 同步一次显隐（场景重载后面板默认激活，若此前已打开则保持打开）
            ApplyState(IsOpen);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                SetOpen(!IsOpen);
        }

        public void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            ApplyState(open);
        }

        private void ApplyState(bool open)
        {
            if (panelRoot != null) panelRoot.SetActive(open);
            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
