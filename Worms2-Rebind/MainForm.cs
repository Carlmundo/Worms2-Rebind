using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using IniParser;
using IniParser.Model;

namespace GameKeyRebinder
{
    public sealed class MainForm : Form
    {
        private const int WmInputLangChange = 0x0051;
        private const string IniSectionName = "Keys";
        private const string IniFileName = "Keys.ini";

        private readonly List<KeyBinding> _bindings;
        private readonly Dictionary<TextBox, KeyBinding> _bindingsByTextBox;
        private readonly Label _statusLabel;
        private readonly Button _resetButton;
        private readonly Button _saveButton;
        private readonly Panel _contentPanel;
        private readonly Panel _bindingsPanel;

        // When a key is captured we wait for its KeyUp before moving focus.
        // This prevents keyboard auto-repeat from filling every row while a
        // key is held down.
        private KeyBinding _pendingAdvanceBinding;
        private int _pendingReleaseVirtualKeyCode;

        public MainForm()
        {
            // Paint the stretched background in one pass and refresh it on resize.
            DoubleBuffered = true;
            ResizeRedraw = true;

            _bindings = CreateBindings();
            _bindingsByTextBox = new Dictionary<TextBox, KeyBinding>();

            // Build the entire layout at 96 DPI before scaling it once.
            SuspendLayout();
            Text = "Worms 2 Rebind";
            ComponentResourceManager resources = new ComponentResourceManager(typeof(MainForm));
            Icon = (Icon)resources.GetObject("$this.Icon");
            BackgroundImage = (Image)resources.GetObject("$this.BackgroundImage");
            BackgroundImageLayout = ImageLayout.Stretch;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(478, 490);
            MinimumSize = new Size(478, 490);
            Font = new Font("Segoe UI", 9.0F, FontStyle.Regular, GraphicsUnit.Point);
            Padding = new Padding(16, 14, 16, 8);
            KeyPreview = true;

            _contentPanel = new BufferedPanel();
            _contentPanel.BackColor = Color.Transparent;
            _contentPanel.Size = new Size(430, 588);
            _contentPanel.MaximumSize = new Size(430, 0);
            _contentPanel.Anchor = AnchorStyles.None;
            Controls.Add(_contentPanel);

            _bindingsPanel = new BufferedPanel();
            _bindingsPanel.BackColor = SystemColors.Control;
            _bindingsPanel.Size = new Size(430, 510);
            _bindingsPanel.Dock = DockStyle.Fill;
            _bindingsPanel.BorderStyle = BorderStyle.FixedSingle;
            _bindingsPanel.AutoScroll = true;
            _contentPanel.Controls.Add(_bindingsPanel);

            BuildBindingRows(_bindingsPanel);

            Panel footerPanel = new BufferedPanel();
            footerPanel.BackColor = Color.Transparent;
            footerPanel.Size = new Size(430, 57);
            footerPanel.Dock = DockStyle.Bottom;
            _contentPanel.Controls.Add(footerPanel);

            _statusLabel = new Label();
            _statusLabel.BackColor = Color.Transparent;
            _statusLabel.Text = String.Empty;
            _statusLabel.Location = new Point(0, 9);
            _statusLabel.Size = new Size(222, 48);
            _statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            footerPanel.Controls.Add(_statusLabel);

            _resetButton = new Button();
            _resetButton.BackColor = SystemColors.Control;
            _resetButton.Text = "Reset";
            _resetButton.Size = new Size(92, 31);
            _resetButton.Location = new Point(238, 17);
            _resetButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _resetButton.Click += new EventHandler(ResetButton_Click);
            footerPanel.Controls.Add(_resetButton);

            _saveButton = new Button();
            _saveButton.BackColor = SystemColors.Control;
            _saveButton.Text = "Save";
            _saveButton.Size = new Size(92, 31);
            _saveButton.Location = new Point(338, 17);
            _saveButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _saveButton.Click += new EventHandler(SaveButton_Click);
            footerPanel.Controls.Add(_saveButton);

            Load += new EventHandler(MainForm_Load);
            Shown += new EventHandler(MainForm_Shown);
            // Enable scaling only after every child is built. On the CLR 2.0
            // runtime, adding a Label can create handles and trigger scaling
            // immediately, even while the form's layout is suspended.
            AutoScaleDimensions = new SizeF(96.0F, 96.0F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(true);
            AlignBindingRows();
        }

        private sealed class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                // Panels paint independently, including the transparent footer.
                DoubleBuffered = true;
                ResizeRedraw = true;
            }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);

            if (_contentPanel == null)
            {
                return;
            }

            // Keep a compact, horizontally centered column while the list
            // fills the available height and the footer stays at the bottom.
            int availableWidth = Math.Max(0, ClientSize.Width - Padding.Horizontal);
            int availableHeight = Math.Max(0, ClientSize.Height - Padding.Vertical);
            int width = Math.Min(_contentPanel.MaximumSize.Width, availableWidth);
            Rectangle bounds = new Rectangle(
                Padding.Left + (availableWidth - width) / 2,
                Padding.Top,
                width,
                availableHeight);

            if (_contentPanel.Bounds != bounds)
            {
                _contentPanel.Bounds = bounds;
            }
        }

        private static List<KeyBinding> CreateBindings()
        {
            List<KeyBinding> bindings = new List<KeyBinding>();

            bindings.Add(new KeyBinding("Up", "Up", (int)Keys.Up));
            bindings.Add(new KeyBinding("Down", "Down", (int)Keys.Down));
            bindings.Add(new KeyBinding("Left", "Left", (int)Keys.Left));
            bindings.Add(new KeyBinding("Right", "Right", (int)Keys.Right));
            bindings.Add(new KeyBinding("Space", "Shoot", (int)Keys.Space));
            bindings.Add(new KeyBinding("Enter", "Jump", (int)Keys.Enter));
            bindings.Add(new KeyBinding("Backspace", "Alt. Jump", (int)Keys.Back));
            bindings.Add(new KeyBinding("Tab", "Worm Select", (int)Keys.Tab));
            bindings.Add(new KeyBinding("Escape", "Menu", (int)Keys.Escape));
            bindings.Add(new KeyBinding("Equals", "Bounce +", (int)Keys.Oemplus));
            bindings.Add(new KeyBinding("Minus", "Bounce -", (int)Keys.OemMinus));
            bindings.Add(new KeyBinding("NumpadPlus", "Bounce + (Alt)", (int)0));
            bindings.Add(new KeyBinding("NumpadMinus", "Bounce - (Alt)", (int)0));

            bindings.Add(new KeyBinding("ZoomIn", "Zoom +", (int)Keys.Add));
            bindings.Add(new KeyBinding("ZoomOut", "Zoom -", (int)Keys.Subtract));
            bindings.Add(new KeyBinding("ZoomReset", "Reset Zoom", (int)Keys.End));
            bindings.Add(new KeyBinding("Home", "Focus Active Worm", (int)Keys.Home));
            bindings.Add(new KeyBinding("CameraLock", "Camera Lock", (int)Keys.Scroll));

            bindings.Add(new KeyBinding("PageDown", "Open Chat", (int)Keys.PageDown));
            bindings.Add(new KeyBinding("PageUp", "Close Chat", (int)Keys.PageUp));
            bindings.Add(new KeyBinding("Insert", "Toggle Graphics", (int)Keys.Insert));
            bindings.Add(new KeyBinding("Delete", "Toggle Nameplates", (int)Keys.Delete));
            
            bindings.Add(new KeyBinding("F1", "F1", (int)Keys.F1));
            bindings.Add(new KeyBinding("F2", "F2", (int)Keys.F2));
            bindings.Add(new KeyBinding("F3", "F3", (int)Keys.F3));
            bindings.Add(new KeyBinding("F4", "F4", (int)Keys.F4));
            bindings.Add(new KeyBinding("F5", "F5", (int)Keys.F5));
            bindings.Add(new KeyBinding("F6", "F6", (int)Keys.F6));
            bindings.Add(new KeyBinding("F7", "F7", (int)Keys.F7));
            bindings.Add(new KeyBinding("F8", "F8", (int)Keys.F8));
            bindings.Add(new KeyBinding("F9", "F9", (int)Keys.F9));
            bindings.Add(new KeyBinding("F10", "F10", (int)Keys.F10));
            bindings.Add(new KeyBinding("F11", "F11", (int)Keys.F11));
            bindings.Add(new KeyBinding("F12", "F12", (int)Keys.F12));

            bindings.Add(new KeyBinding("1", "1", (int)Keys.D1));
            bindings.Add(new KeyBinding("2", "2", (int)Keys.D2));
            bindings.Add(new KeyBinding("3", "3", (int)Keys.D3));
            bindings.Add(new KeyBinding("4", "4", (int)Keys.D4));
            bindings.Add(new KeyBinding("5", "5", (int)Keys.D5));
            bindings.Add(new KeyBinding("6", "6", (int)Keys.D6));
            bindings.Add(new KeyBinding("7", "7", (int)Keys.D7));
            bindings.Add(new KeyBinding("8", "8", (int)Keys.D8));
            bindings.Add(new KeyBinding("9", "9", (int)Keys.D9));

            bindings.Add(new KeyBinding("R", "Replay", (int)Keys.R));
            bindings.Add(new KeyBinding("S", "Slow Motion", (int)Keys.S));

            return bindings;
        }

        private void BuildBindingRows(Panel panel)
        {
            const int labelLeft = 6;
            const int labelWidth = 112;
            const int boxLeft = 127;
            const int boxWidth = 180;
            const int clearLeft = 317;
            const int clearWidth = 88;
            const int rowHeight = 31;
            const int topPadding = 10;
            const int bottomPadding = 16;

            panel.Padding = new Padding(0, 0, 0, bottomPadding);

            int i;
            for (i = 0; i < _bindings.Count; i++)
            {
                KeyBinding binding = _bindings[i];
                int top = topPadding + (i * rowHeight);

                Label label = new Label();
                label.BackColor = SystemColors.Control;
                label.Text = binding.DisplayName;
                label.TextAlign = ContentAlignment.TopRight;
                label.Location = new Point(labelLeft, top + 4);
                label.Size = new Size(labelWidth, 22);
                panel.Controls.Add(label);
                binding.Label = label;

                TextBox textBox = new TextBox();
                textBox.Location = new Point(boxLeft, top);
                textBox.Size = new Size(boxWidth, 24);
                textBox.ReadOnly = true;
                textBox.BackColor = SystemColors.Window;
                textBox.TabStop = true;
                textBox.TextAlign = HorizontalAlignment.Center;
                textBox.PreviewKeyDown += new PreviewKeyDownEventHandler(BindingTextBox_PreviewKeyDown);
                textBox.KeyDown += new KeyEventHandler(BindingTextBox_KeyDown);
                textBox.KeyUp += new KeyEventHandler(BindingTextBox_KeyUp);
                textBox.GotFocus += new EventHandler(BindingTextBox_GotFocus);
                textBox.LostFocus += new EventHandler(BindingTextBox_LostFocus);
                textBox.Click += new EventHandler(BindingTextBox_Click);
                panel.Controls.Add(textBox);

                binding.TextBox = textBox;
                _bindingsByTextBox.Add(textBox, binding);

                Button clearButton = new Button();
                clearButton.BackColor = SystemColors.Control;
                clearButton.Text = "Clear";
                clearButton.Location = new Point(clearLeft, top);
                clearButton.Size = new Size(clearWidth, 24);
                clearButton.TabStop = true;
                clearButton.Tag = binding;
                clearButton.Click += new EventHandler(ClearButton_Click);
                panel.Controls.Add(clearButton);
            }

            panel.AutoScrollMinSize = new Size(clearLeft + clearWidth + 6,
                topPadding + (_bindings.Count * rowHeight) + bottomPadding);
            RefreshAllBindingText();
        }

        private void AlignBindingRows()
        {
            // A single-line TextBox uses its native font height at the current
            // DPI even before WinForms scales the rest of the layout. Match
            // other controls only after that scaling has finished.
            _bindingsPanel.SuspendLayout();
            int contentBottom = 0;
            foreach (Control control in _bindingsPanel.Controls)
            {
                Button clearButton = control as Button;
                KeyBinding binding = clearButton == null ? null : clearButton.Tag as KeyBinding;
                if (binding == null)
                {
                    continue;
                }

                TextBox textBox = binding.TextBox;
                clearButton.SetBounds(clearButton.Left, textBox.Top,
                    clearButton.Width, textBox.Height);

                // Align the label's text, rather than its surrounding box,
                // with the native edit control's centered line of text.
                int textHeight = textBox.Font.Height;
                binding.Label.SetBounds(binding.Label.Left,
                    textBox.Top + (textBox.Height - textHeight) / 2,
                    binding.Label.Width, textHeight);
                contentBottom = Math.Max(contentBottom,
                    textBox.Bottom - _bindingsPanel.AutoScrollPosition.Y);
            }
            // CLR 2.0 does not scale AutoScrollMinSize or preserve the panel's
            // bottom padding when calculating its automatic scroll extent.
            _bindingsPanel.AutoScrollMinSize = new Size(
                _bindingsPanel.AutoScrollMinSize.Width,
                contentBottom + _bindingsPanel.Padding.Bottom);
            _bindingsPanel.ResumeLayout(true);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            LoadBindingsFromIni();
            RefreshAllBindingText();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            // Ensure startup never leaves a key-capture TextBox focused.
            ActiveControl = _saveButton;
            _saveButton.Focus();
        }

        private void BindingTextBox_GotFocus(object sender, EventArgs e)
        {
            TextBox textBox = sender as TextBox;
            KeyBinding binding;
            if (textBox != null && _bindingsByTextBox.TryGetValue(textBox, out binding))
            {
                SetStatusText("Press a key to rebind " + binding.DisplayName + ".");
            }
        }

        private void BindingTextBox_LostFocus(object sender, EventArgs e)
        {
            SetStatusText(String.Empty);
        }

        private void SetStatusText(string text)
        {
            foreach (TextBox textBox in _bindingsByTextBox.Keys)
            {
                if (textBox.Focused)
                {
                    _statusLabel.Text = text;
                    return;
                }
            }

            _statusLabel.Text = String.Empty;
        }

        private void BindingTextBox_Click(object sender, EventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null)
            {
                textBox.SelectAll();
            }
        }

        private void BindingTextBox_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            // Makes navigation keys such as Tab and the arrow keys reach KeyDown.
            e.IsInputKey = true;
        }

        private void BindingTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            KeyBinding binding;

            if (textBox == null || !_bindingsByTextBox.TryGetValue(textBox, out binding))
            {
                return;
            }

            int virtualKeyCode = (int)e.KeyCode;

            // If an accepted key is still being held, ignore all repeat KeyDown
            // messages until its KeyUp arrives. Focus deliberately remains on
            // this same TextBox during that time.
            if (_pendingAdvanceBinding != null)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            if (IsBlockedVirtualKeyCode(virtualKeyCode))
            {
                SetStatusText("That key cannot be assigned.");
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            if (virtualKeyCode > 0 && virtualKeyCode <= 255)
            {
                binding.VirtualKeyCode = virtualKeyCode;
                binding.IsExplicitlyCleared = false;
                textBox.Text = KeyNameHelper.GetLocalizedKeyName(virtualKeyCode);
                SetStatusText(binding.DisplayName +
                    " -> " + textBox.Text +
                    " (VK " + virtualKeyCode.ToString(CultureInfo.InvariantCulture) + ")");

                // Do not move focus yet. KeyDown auto-repeat is delivered while
                // a key is held; advancing here would let repeats fill every
                // subsequent TextBox. We advance when this key is released.
                _pendingAdvanceBinding = binding;
                _pendingReleaseVirtualKeyCode = virtualKeyCode;
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void BindingTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (_pendingAdvanceBinding != null &&
                (int)e.KeyCode == _pendingReleaseVirtualKeyCode)
            {
                KeyBinding bindingToAdvance = _pendingAdvanceBinding;
                _pendingAdvanceBinding = null;
                _pendingReleaseVirtualKeyCode = 0;

                BeginInvoke(new MethodInvoker(delegate
                {
                    FocusNextBinding(bindingToAdvance);
                }));
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private static bool IsBlockedVirtualKeyCode(int virtualKeyCode)
        {
            // Requested blocked VKs:
            // A0/A1 = left/right Shift, A2/A3 = left/right Ctrl,
            // A4/A5 = left/right Alt, 5B/5C = left/right Windows.
            // WinForms can report the generic modifier VK (10/11/12) for the
            // side-specific Shift/Ctrl/Alt keys, so block those generic values
            // as well to ensure the requested physical keys cannot slip through.
            return virtualKeyCode == 0xA0 || virtualKeyCode == 0xA1 ||
                   virtualKeyCode == 0xA2 || virtualKeyCode == 0xA3 ||
                   virtualKeyCode == 0xA4 || virtualKeyCode == 0xA5 ||
                   virtualKeyCode == 0x5B || virtualKeyCode == 0x5C ||
                   virtualKeyCode == (int)Keys.ShiftKey ||
                   virtualKeyCode == (int)Keys.ControlKey ||
                   virtualKeyCode == (int)Keys.Menu;
        }

        private void FocusNextBinding(KeyBinding currentBinding)
        {
            int currentIndex = _bindings.IndexOf(currentBinding);
            int nextIndex = currentIndex + 1;

            if (currentIndex >= 0 && nextIndex < _bindings.Count)
            {
                TextBox nextTextBox = _bindings[nextIndex].TextBox;
                if (nextTextBox != null)
                {
                    _bindingsPanel.ScrollControlIntoView(nextTextBox);
                    nextTextBox.Focus();
                    nextTextBox.SelectAll();
                    return;
                }
            }

            // The last binding has no row below it; finish outside key capture.
            _saveButton.Focus();
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            Button clearButton = sender as Button;
            KeyBinding binding = clearButton == null ? null : clearButton.Tag as KeyBinding;
            if (binding == null)
            {
                return;
            }

            _pendingAdvanceBinding = null;
            _pendingReleaseVirtualKeyCode = 0;
            binding.VirtualKeyCode = 0;
            binding.IsExplicitlyCleared = true;
            if (binding.TextBox != null)
            {
                binding.TextBox.Text = String.Empty;
            }

            SetStatusText(binding.DisplayName + " cleared.");

            // Clear must not advance to the next capture box. Put focus on a
            // normal button instead so no TextBox remains armed for input.
            _saveButton.Focus();
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            _pendingAdvanceBinding = null;
            _pendingReleaseVirtualKeyCode = 0;

            int i;
            for (i = 0; i < _bindings.Count; i++)
            {
                _bindings[i].VirtualKeyCode = _bindings[i].DefaultVirtualKeyCode;
                _bindings[i].IsExplicitlyCleared = false;
            }

            RefreshAllBindingText();
            SetStatusText("All key bindings reset to their defaults. Click Save to write the changes.");
            _resetButton.Focus();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            _pendingAdvanceBinding = null;
            _pendingReleaseVirtualKeyCode = 0;

            string iniPath = GetIniPath();

            try
            {
                FileIniDataParser parser = new FileIniDataParser();
                IniData data;

                if (File.Exists(iniPath))
                {
                    data = parser.ReadFile(iniPath);
                }
                else
                {
                    data = new IniData();
                }

                if (data.Sections[IniSectionName] == null)
                {
                    data.Sections.AddSection(IniSectionName);
                }

                int savedOverrides = 0;
                int i;
                for (i = 0; i < _bindings.Count; i++)
                {
                    KeyBinding binding = _bindings[i];

                    if (binding.VirtualKeyCode == binding.DefaultVirtualKeyCode && !binding.IsExplicitlyCleared)
                    {
                        // A self-mapped/default binding is implicit. Remove any
                        // old override instead of writing the default value.
                        data[IniSectionName].RemoveKey(binding.IniName);
                    }
                    else
                    {
                        // VK 0 is a deliberate cleared/disabled binding and is
                        // therefore written as an explicit override.
                        data[IniSectionName][binding.IniName] =
                            binding.VirtualKeyCode.ToString(CultureInfo.InvariantCulture);
                        savedOverrides++;
                    }
                }

                parser.WriteFile(iniPath, data, new System.Text.UTF8Encoding(false));
                string strTxtOverride = "override";
                if (savedOverrides != 1) { strTxtOverride += "s"; }
                SetStatusText("Saved " + savedOverrides.ToString(CultureInfo.InvariantCulture) + " " + strTxtOverride);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "The key bindings could not be saved.\r\n\r\n" + ex.Message,
                    "Save failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void LoadBindingsFromIni()
        {
            string iniPath = GetIniPath();
            if (!File.Exists(iniPath))
            {
                SetStatusText("No existing INI found. Default bindings are loaded.");
                return;
            }

            try
            {
                FileIniDataParser parser = new FileIniDataParser();
                IniData data = parser.ReadFile(iniPath);

                if (data.Sections[IniSectionName] == null)
                {
                    SetStatusText("INI loaded. [Keys] was not present, so defaults are shown.");
                    return;
                }

                int ignoredValues = 0;
                int i;
                for (i = 0; i < _bindings.Count; i++)
                {
                    KeyBinding binding = _bindings[i];
                    string value = data[IniSectionName][binding.IniName];

                    if (value == null || value.Trim().Length == 0)
                    {
                        continue;
                    }

                    int virtualKeyCode;
                    if (Int32.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out virtualKeyCode) &&
                        virtualKeyCode >= 0 && virtualKeyCode <= 255)
                    {
                        // 0 in the INI is an explicit disabled/cleared state,
                        // even for bindings whose default is also 0.
                        binding.VirtualKeyCode = virtualKeyCode;
                        binding.IsExplicitlyCleared = (virtualKeyCode == 0);
                    }
                    else
                    {
                        ignoredValues++;
                    }
                }

                if (ignoredValues == 0)
                {
                    SetStatusText("Loaded " + iniPath);
                }
                else
                {
                    SetStatusText("Loaded INI; " + ignoredValues.ToString(CultureInfo.InvariantCulture) +
                        " invalid value(s) were ignored.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "The existing INI file could not be read, so default bindings will be used.\r\n\r\n" + ex.Message,
                    "Load warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                SetStatusText("Could not load the existing INI. Defaults are shown.");
            }
        }

        private void RefreshAllBindingText()
        {
            int i;
            for (i = 0; i < _bindings.Count; i++)
            {
                KeyBinding binding = _bindings[i];

                if (binding.Label != null)
                {
                    binding.Label.Text = binding.DisplayName;
                }

                if (binding.TextBox != null)
                {
                    if (binding.VirtualKeyCode == 0)
                    {
                        binding.TextBox.Text = String.Empty;
                    }
                    else
                    {
                        binding.TextBox.Text = KeyNameHelper.GetLocalizedKeyName(binding.VirtualKeyCode);
                    }
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WmInputLangChange)
            {
                // The user changed keyboard/input language while the app is
                // open. English labels remain fixed; rebound values refresh.
                RefreshAllBindingText();
            }
        }

        private static string GetIniPath()
        {
            return Path.Combine(Application.StartupPath, IniFileName);
        }
    }
}
