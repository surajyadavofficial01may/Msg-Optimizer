using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MsgOptimizer
{
    /// Connection settings: provider, API key (stored encrypted), model, server URL, and a live test.
    sealed class SettingsForm : Form
    {
        sealed class Provider
        {
            public readonly string Name, BaseUrl, Model, KeyUrl;
            public Provider(string name, string baseUrl, string model, string keyUrl) { Name = name; BaseUrl = baseUrl; Model = model; KeyUrl = keyUrl; }
            public override string ToString() { return Name; }
        }

        static readonly Provider[] Providers =
        {
            new Provider("Google Gemini (free tier)", "https://generativelanguage.googleapis.com/v1beta/openai", "gemini-flash-latest", "https://aistudio.google.com/apikey"),
            new Provider("Groq (free tier)", "https://api.groq.com/openai/v1", "llama-3.3-70b-versatile", "https://console.groq.com/keys"),
            new Provider("Ollama (on this PC, no key)", "http://localhost:11434/v1", "llama3.2:3b", null),
            new Provider("OpenRouter", "https://openrouter.ai/api/v1", "", "https://openrouter.ai/keys"),
            new Provider("Other OpenAI-compatible server", "", "", null),
        };

        readonly Settings settings;
        readonly ComboBox provider = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        readonly TextBox apiKey = new TextBox { UseSystemPasswordChar = true };
        readonly TextBox model = new TextBox();
        readonly TextBox baseUrl = new TextBox();
        readonly CheckBox showKey = new CheckBox { Text = "Show", AutoSize = true, Anchor = AnchorStyles.Left };
        readonly LinkLabel getKey = new LinkLabel { Text = "Get a key", AutoSize = true, Anchor = AnchorStyles.Left };
        readonly Label status = new Label { AutoSize = true };

        public SettingsForm(Icon icon)
        {
            settings = Settings.Load().Copy();
            Text = "Msg Optimizer settings";
            Icon = icon;
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(Font.Height);

            int fieldWidth = Font.Height * 26;
            foreach (Control c in new Control[] { provider, apiKey, model, baseUrl }) c.Width = fieldWidth;
            status.MaximumSize = new Size(fieldWidth + Font.Height * 8, 0);
            provider.Items.AddRange(Providers);

            var grid = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Dock = DockStyle.Fill };
            AddRow(grid, "Provider", provider, getKey);
            AddRow(grid, "API key", apiKey, showKey);
            AddRow(grid, "Model", model, null);
            AddRow(grid, "Server URL", baseUrl, null);

            var test = new Button { Text = "Test connection", AutoSize = true };
            var prompts = new Button { Text = "Edit menu items and prompts…", AutoSize = true };
            var save = new Button { Text = "Save", AutoSize = true, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            grid.Controls.Add(Flow(test, status), 1, grid.RowCount++);
            grid.Controls.Add(Flow(prompts), 1, grid.RowCount++);
            var buttons = Flow(save, cancel);
            buttons.Anchor = AnchorStyles.Right;
            grid.Controls.Add(buttons, 0, grid.RowCount);
            grid.SetColumnSpan(buttons, 3);
            Controls.Add(grid);
            AcceptButton = save;
            CancelButton = cancel;

            apiKey.Text = settings.GetApiKey();
            model.Text = settings.Model;
            baseUrl.Text = settings.BaseUrl;
            provider.SelectedItem = Providers.FirstOrDefault(p => p.BaseUrl.Length > 0 && SameUrl(p.BaseUrl, settings.BaseUrl)) ?? Providers.Last();
            UpdateKeyLink();

            provider.SelectionChangeCommitted += (s, e) =>
            {
                var p = (Provider)provider.SelectedItem;
                if (p.BaseUrl.Length > 0) { baseUrl.Text = p.BaseUrl; model.Text = p.Model; }
                UpdateKeyLink();
                status.Text = "";
            };
            showKey.CheckedChanged += (s, e) => apiKey.UseSystemPasswordChar = !showKey.Checked;
            getKey.LinkClicked += (s, e) => Process.Start(((Provider)provider.SelectedItem).KeyUrl);
            prompts.Click += (s, e) => Process.Start("notepad.exe", "\"" + Settings.FilePath + "\"");
            test.Click += async (s, e) =>
            {
                test.Enabled = false;
                status.ForeColor = SystemColors.ControlText;
                status.Text = "Testing…";
                string result = await LlmClient.TestAsync(FromForm());
                status.ForeColor = result.StartsWith("Connected") ? Color.ForestGreen : Color.Firebrick;
                status.Text = result;
                test.Enabled = true;
            };
            save.Click += (s, e) =>
            {
                Settings updated = FromForm();
                Settings.Save(updated);
                Log.Info("Settings saved (" + updated.BaseUrl + ", " + updated.Model + ", key " + (updated.GetApiKey().Length > 0 ? "set" : "empty") + ")");
            };
        }

        Settings FromForm()
        {
            Settings s = settings.Copy();
            s.BaseUrl = baseUrl.Text.Trim();
            s.Model = model.Text.Trim();
            s.SetApiKey(apiKey.Text);
            return s;
        }

        void UpdateKeyLink()
        {
            getKey.Visible = ((Provider)provider.SelectedItem).KeyUrl != null;
        }

        static bool SameUrl(string a, string b)
        {
            return string.Equals((a ?? "").TrimEnd('/'), (b ?? "").TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        static void AddRow(TableLayoutPanel grid, string label, Control field, Control extra)
        {
            int row = grid.RowCount++;
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            grid.Controls.Add(field, 1, row);
            if (extra != null) grid.Controls.Add(extra, 2, row);
        }

        static FlowLayoutPanel Flow(params Control[] controls)
        {
            var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
            foreach (var c in controls) { c.Anchor = AnchorStyles.Left; panel.Controls.Add(c); }
            return panel;
        }
    }
}
