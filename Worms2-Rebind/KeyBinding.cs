using System.Windows.Forms;

namespace GameKeyRebinder
{
    internal sealed class KeyBinding
    {
        private readonly string _iniName;
        private readonly string _displayName;
        private readonly int _defaultVirtualKeyCode;
        private int _virtualKeyCode;
        private bool _isExplicitlyCleared;
        private Label _label;
        private TextBox _textBox;

        public KeyBinding(string iniName, string displayName, int defaultVirtualKeyCode)
        {
            _iniName = iniName;
            _displayName = displayName;
            _defaultVirtualKeyCode = defaultVirtualKeyCode;
            _virtualKeyCode = defaultVirtualKeyCode;
        }

        public string IniName
        {
            get { return _iniName; }
        }

        // Fixed English label shown on the left side of the form.
        public string DisplayName
        {
            get { return _displayName; }
        }

        public int DefaultVirtualKeyCode
        {
            get { return _defaultVirtualKeyCode; }
        }

        public int VirtualKeyCode
        {
            get { return _virtualKeyCode; }
            set { _virtualKeyCode = value; }
        }

        // Distinguishes an explicit user Clear (which must save VK 0) from a
        // binding whose normal/default value happens to be 0.
        public bool IsExplicitlyCleared
        {
            get { return _isExplicitlyCleared; }
            set { _isExplicitlyCleared = value; }
        }

        public Label Label
        {
            get { return _label; }
            set { _label = value; }
        }

        public TextBox TextBox
        {
            get { return _textBox; }
            set { _textBox = value; }
        }
    }
}
