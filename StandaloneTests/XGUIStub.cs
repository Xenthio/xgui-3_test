// Stubs for GUI-only Win32 emulator code excluded from standalone build
// Provides minimal partial class implementations so the build succeeds

using System.Threading.Tasks;
using FakeDesktop;

// ── Sandbox Texture / ImageFormat stubs ──────────────────────────────────────
namespace Sandbox
{
    public enum ImageFormat { RGBA8888, Unknown }

    public class Texture
    {
        public byte[] Data { get; private set; }
        public int Width  { get; private set; }
        public int Height { get; private set; }

        internal Texture() { }

        public static TextureBuilder Create( int w, int h, ImageFormat fmt ) => new TextureBuilder( w, h );
    }

    public class TextureBuilder
    {
        private readonly int _w, _h;
        private byte[] _data;
        private string _name;
        public TextureBuilder( int w, int h ) { _w = w; _h = h; }
        public TextureBuilder WithData( byte[] data ) { _data = data; return this; }
        public TextureBuilder WithName( string name ) { _name = name; return this; }
        public Texture Finish() { return new Texture(); } // no-op in tests
    }
}

// ── Win32.User32 namespace stub (replaces User32.cs which pulls in XGUI/FakeDesktop) ──
namespace Win32.User32
{
    public static class User32
    {
        public static Task<int> MessageBox(System.IntPtr hWnd, string text, string caption, uint type)
            => Task.FromResult(1); // IDOK stub
        public static System.IntPtr GetDesktopWindow() => new System.IntPtr(0x10001);
        public static int GetSystemMetrics(int nIndex) => 0;
    }
}

// ── User32Emulator partial stub (provides RegisterGUIFunctions) ──
namespace FakeOperatingSystem.Experiments.Ambitious.X86.Win32
{
    public partial class User32Emulator
    {
        // GUI functions stub — real impl is in User32Emulator.GUI.cs (excluded from standalone)
        public void RegisterGUIFunctions() { }
    }
}

// ── Sandbox.UI / XGUI stubs ──────────────────────────────────────────────────────────

namespace Sandbox.UI
{
    public class StyleSheet
    {
        public float? Width { get; set; }
        public float? Height { get; set; }
        public PositionMode? Position { get; set; }
        public float? Left { get; set; }
        public float? Top { get; set; }
        public void SetBackgroundImage( Sandbox.Texture t ) { }
    }

    public enum PositionMode { Static, Absolute }

    public class Panel
    {
        public string Class { get; set; } = "";
        public StyleSheet Style { get; } = new StyleSheet();
        public System.Collections.Generic.List<Panel> Children { get; } = new();
        public Panel Parent { get; internal set; }
        public bool IsValid => true;
        public virtual T AddChild<T>(string cls = "") where T : Panel, new() { var c = new T(); c.Parent = this; Children.Add(c); return c; }
        public virtual void AddChild(Panel child) { child.Parent = this; Children.Add(child); }
        public virtual void Delete(bool immediate = false) { }
        public void AddClass(string cls) { }
    }

    public class Image : Panel
    {
        public Sandbox.Texture Texture { get; set; }
    }
}

namespace XGUI
{
    // XGUIPanel is the base UI element — extends Sandbox.UI.Panel
    public class XGUIPanel : Sandbox.UI.Panel
    {
        public string Title { get; set; } = "";
        public float Width { get; set; }
        public float Height { get; set; }
        public float Left { get; set; }
        public float Top { get; set; }
        public bool IsVisible { get; set; } = true;
    }

    public class Window : XGUIPanel
    {
        public string Icon { get; set; }
        public bool Resizable { get; set; }
        public string Title { get; set; } = "";
        public System.Numerics.Vector2 InitalInnerSize { get; set; }
        public System.Numerics.Vector2 Position { get; set; }
        public System.Action OnCloseAction { get; set; }
        public bool HasFocus => false;
        public Sandbox.UI.Panel CreateWindowContentPanel() => new Sandbox.UI.Panel();
        public void ResetInnerSizeInit() { }
    }

    public class CheckBox : XGUIPanel
    {
        public bool Checked { get; set; }
        public string LabelText { get; set; }
        public System.Action<bool> OnChange { get; set; }
    }

    public class RadioButton : XGUIPanel
    {
        public bool Checked { get; set; }
        public string LabelText { get; set; }
        public System.Action<bool> OnChange { get; set; }
    }

    public class GroupBox : XGUIPanel { public string Label { get; set; } }
    public class Seperator : XGUIPanel { }
    public class SeperatorVertical : XGUIPanel { }
    public class RadioButtons : XGUIPanel { }
    public class Label : XGUIPanel { public string Text { get; set; } }
    public class Button : XGUIPanel
    {
        public string Text { get; set; }
        public System.Action OnClick { get; set; }
        public void AddEventListener(string ev, System.Action handler) { }
    }
    public class TextEntry : XGUIPanel { public string Text { get; set; } public bool ReadOnly { get; set; } public bool Multiline { get; set; } public System.Action OnChange { get; set; } }
    public class ScrollPanel : XGUIPanel { }
    public class ComboBox : XGUIPanel
    {
        private readonly System.Collections.Generic.List<string> _items = new();
        public int SelectedIndex { get; set; } = -1;
        public string SelectedText => SelectedIndex >= 0 && SelectedIndex < _items.Count ? _items[SelectedIndex] : "";
        public void AddItem(string item) => _items.Add(item);
        public void Clear() => _items.Clear();
        public System.Action OnChange { get; set; }
    }
    public class ListBox : XGUIPanel { public int SelectedIndex { get; set; } = -1; public System.Action OnChange { get; set; } }
    public class ProgressBar : XGUIPanel { public float Value { get; set; } public float Max { get; set; } = 100f; }
    public class TabControl : XGUIPanel { }
    public class TabPage : XGUIPanel { public string Text { get; set; } }
    public class MenuBar : XGUIPanel { }
    public class MenuItem : XGUIPanel { public string Text { get; set; } public bool Checked { get; set; } public System.Action OnClick { get; set; } }
    public class StatusBar : XGUIPanel { }
    public class TreeView : XGUIPanel { }
    public class TreeNode : XGUIPanel { public string Text { get; set; } }
    public class ListView : XGUIPanel { }
    public class ListViewItem : XGUIPanel { public string Text { get; set; } }
    // NOTE: XGUI.Panel intentionally omitted to avoid ambiguity with Sandbox.UI.Panel
    public class PictureBox : XGUIPanel { }
    public class RichTextBox : XGUIPanel { public string Text { get; set; } }
    public class ToolBar : XGUIPanel { }
    public class ToolBarButton : XGUIPanel { public string Text { get; set; } }
    public class Splitter : XGUIPanel { }
    public class SplitContainer : XGUIPanel { }
}
