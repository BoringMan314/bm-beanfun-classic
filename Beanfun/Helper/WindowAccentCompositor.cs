using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Beanfun
{
    /// <summary>
    /// 為視窗套用系統模糊效果。
    /// </summary>
    public class WindowAccentCompositor
    {
        private readonly Window _window;
        private bool _isEnabled;
        private int _blurColor;

        public WindowAccentCompositor(Window window) =>
            _window = window ?? throw new ArgumentNullException(nameof(window));

        [DefaultValue(false)]
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnIsEnabledChanged(value);
            }
        }

        public Color Color
        {
            get =>
                Color.FromArgb(
                    (byte)((_blurColor & 0x000000ff) >> 0), // 紅
                    (byte)((_blurColor & 0x0000ff00) >> 8), // 綠
                    (byte)((_blurColor & 0x00ff0000) >> 16), // 藍
                    (byte)((_blurColor & 0xff000000) >> 24) // 透明
                );
            set =>
                _blurColor =
                    value.R << 0 // 紅
                    | value.G << 8 // 綠
                    | value.B << 16 // 藍
                    | value.A << 24; // 透明
        }

        private void OnIsEnabledChanged(bool isEnabled)
        {
            Window window = _window;
            var handle = new WindowInteropHelper(window).EnsureHandle();
            Composite(handle, isEnabled);
        }

        private void Composite(IntPtr handle, bool isEnabled)
        {
            var accent = new WindowsAPI.AccentPolicy();

            if (!isEnabled)
            {
                accent.AccentState = WindowsAPI.AccentState.ACCENT_DISABLED;
            }
            else if (App.OSVersion >= App.Win11)
            {
                accent.AccentState = WindowsAPI.AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND; // Win11 亞克力（Win10 亞克力拖移會卡）
                accent.GradientColor = _blurColor;
            }
            else if (App.OSVersion >= App.Win10)
            {
                accent.AccentState = WindowsAPI.AccentState.ACCENT_ENABLE_BLURBEHIND; // Win10 早期模糊（Win11 用此效果拖移會卡）
            }
            else
            {
                return; // Win8 無模糊；Vista/7 的 Aero 不在此處理
            }

            var accentPolicySize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentPolicySize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            try
            {
                var data = new WindowsAPI.WindowCompositionAttributeData
                {
                    Attribute = WindowsAPI.WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    SizeOfData = accentPolicySize,
                    Data = accentPtr,
                };
                WindowsAPI.SetWindowCompositionAttribute(handle, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }
    }
}
