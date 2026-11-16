using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace MsgOptimizer
{
    /// One submenu entry.
    sealed class ActionDef
    {
        public string Label { get; set; }
        public string Icon { get; set; }   // Segoe icon-font code point in hex, e.g. "E715"
        public string Prompt { get; set; }
    }

    /// %APPDATA%\MsgOptimizer\settings.json, re-read whenever the file changes. The Settings window
    /// edits the connection; the menu items and prompts are edited in the file itself.
    sealed class Settings
    {
        public string BaseUrl { get; set; }
        public string Model { get; set; }
        public string ApiKey { get; set; }          // plain text, for hand-editing; the Settings window stores it encrypted instead
        public string ApiKeyProtected { get; set; } // encrypted with DPAPI: only this Windows user on this PC can read it
        public double Temperature { get; set; }
        public int TimeoutSeconds { get; set; }
        public int MaxTokens { get; set; }          // 0 = the provider's default; cap it for local models that ramble
        public List<ActionDef> Actions { get; set; }

        public Settings() { Temperature = 0.4; TimeoutSeconds = 60; }

        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MsgOptimizer");
        public static readonly string FilePath = Path.Combine(Folder, "settings.json");

        /// Set when settings.json fails to parse; the previous settings stay in use.
        public static string LastError;
        static Settings cached;
        static DateTime stamp;

        /// The current settings, re-read only when the file changed. Creates the file with defaults on first run; never throws (see LastError).
        public static Settings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Directory.CreateDirectory(Folder);
                    File.WriteAllText(FilePath, DefaultJson, new UTF8Encoding(false));
                }
                var t = File.GetLastWriteTimeUtc(FilePath);
                if (cached == null || t != stamp)
                {
                    stamp = t; // a broken file is reported once, not on every right-click
                    cached = Parse(File.ReadAllText(FilePath));
                }
            }
            catch (Exception ex) { LastError = ex.Message; }
            return cached ?? (cached = Parse(DefaultJson));
        }

        /// Writes s as readable JSON and makes it the current settings.
        public static void Save(Settings s)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, ToJson(s), new UTF8Encoding(false));
            cached = s;
            stamp = File.GetLastWriteTimeUtc(FilePath);
        }

        /// Parses and validates settings JSON, filling in default icons. Throws FormatException if required fields are missing.
        public static Settings Parse(string json)
        {
            Settings s = ParseRaw(json);
            var defaults = ParseRaw(DefaultJson).Actions;
            foreach (var a in s.Actions.Where(a => string.IsNullOrWhiteSpace(a.Icon))) // files from v1 have no icons
            {
                var match = defaults.FirstOrDefault(d => d.Label == a.Label);
                a.Icon = match != null ? match.Icon : "E70F";
            }
            return s;
        }

        static Settings ParseRaw(string json)
        {
            var s = new JavaScriptSerializer().Deserialize<Settings>(json);
            if (s == null || string.IsNullOrWhiteSpace(s.BaseUrl) || s.Actions == null || s.Actions.Count == 0
                || s.Actions.Any(a => a == null || string.IsNullOrWhiteSpace(a.Label) || string.IsNullOrWhiteSpace(a.Prompt)))
                throw new FormatException("BaseUrl and at least one action with a Label and a Prompt are required.");
            if (s.TimeoutSeconds <= 0) s.TimeoutSeconds = 60;
            return s;
        }

        /// Indented JSON that stays pleasant to edit by hand.
        public static string ToJson(Settings s)
        {
            string json = new JavaScriptSerializer().Serialize(s);
            json = json.Replace("\\u0027", "'").Replace("\\u003c", "<").Replace("\\u003e", ">").Replace("\\u0026", "&");
            var sb = new StringBuilder();
            int indent = 0;
            bool inString = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\') sb.Append(json[++i]);
                    else if (c == '"') inString = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inString = true; sb.Append(c); break;
                    case '{': case '[': sb.Append(c).Append("\r\n").Append(' ', ++indent * 2); break;
                    case '}': case ']': sb.Append("\r\n").Append(' ', --indent * 2).Append(c); break;
                    case ',': sb.Append(c).Append("\r\n").Append(' ', indent * 2); break;
                    case ':': sb.Append(": "); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.Append("\r\n").ToString();
        }

        /// A deep copy, e.g. for editing in the Settings window without touching the live settings.
        public Settings Copy() { return Parse(ToJson(this)); }

        /// The API key: the plain-text value if present, else the decrypted one, else empty.
        public string GetApiKey()
        {
            if (!string.IsNullOrWhiteSpace(ApiKey)) return ApiKey.Trim();
            if (string.IsNullOrEmpty(ApiKeyProtected)) return "";
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ApiKeyProtected), null, DataProtectionScope.CurrentUser)); }
            catch (Exception ex) { Log.Error("Couldn't decrypt the saved API key (settings copied from another user or PC?)", ex); return ""; }
        }

        /// Stores key encrypted for the current Windows user and clears the plain-text field.
        public void SetApiKey(string key)
        {
            key = (key ?? "").Trim();
            ApiKey = "";
            ApiKeyProtected = key.Length == 0 ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser));
        }

        /// No key and not a local server: the offline placeholder runs instead of an API call.
        [ScriptIgnore]
        public bool IsDemo
        {
            get { return GetApiKey().Length == 0 && !IsLocal(BaseUrl); }
        }

        /// True for servers on this PC (localhost), which typically need no key.
        public static bool IsLocal(string url)
        {
            return Regex.IsMatch(url ?? "", @"^https?://(localhost|127\.0\.0\.1|\[::1\])[:/]", RegexOptions.IgnoreCase);
        }

        public const string DefaultJson = @"{
  ""BaseUrl"": ""https://generativelanguage.googleapis.com/v1beta/openai"",
  ""Model"": ""gemini-flash-latest"",
  ""ApiKey"": """",
  ""ApiKeyProtected"": """",
  ""Temperature"": 0.4,
  ""TimeoutSeconds"": 60,
  ""MaxTokens"": 0,
  ""Actions"": [
    {
      ""Label"": ""Enhance for Mail"",
      ""Icon"": ""E715"",
      ""Prompt"": ""Rewrite the draft as a clear, professional email.\n- Lead with the key point, request or decision.\n- Polite and confident, never apologetic or pleading: drop phrases like 'sorry to bother you' or 'just checking in'.\n- Short paragraphs, and one clear next step if the draft implies one.\n- Keep every fact, date, number, name and commitment. Never invent new ones.\n- Keep a greeting and sign-off only if the draft has them.""
    },
    {
      ""Label"": ""Enhance for Teams"",
      ""Icon"": ""E8BD"",
      ""Prompt"": ""Rewrite the draft as a Microsoft Teams chat message.\n- Friendly, direct and concise, the way a respected teammate writes.\n- Cut filler, throat-clearing and hedging. Prefer short sentences.\n- If it covers several points, use short '- ' bullets; otherwise keep it to 1-3 sentences.\n- Keep @mentions, links, code, emoji and ticket IDs exactly as written.\n- No greeting or sign-off unless the draft has one.""
    },
    {
      ""Label"": ""Correct English"",
      ""Icon"": ""E8FB"",
      ""Prompt"": ""Correct the spelling, grammar and punctuation of the draft, and fix sentences that are genuinely unclear.\n- Keep the author's voice, tone, word choice and structure. Make the smallest edits possible.\n- Do not shorten, expand or restyle it.\n- Answer in English.""
    },
    {
      ""Label"": ""Humanize"",
      ""Icon"": ""E77B"",
      ""Prompt"": ""Rewrite the draft so it reads as if a thoughtful person wrote it naturally.\n- Remove robotic, stiff or AI-sounding phrasing ('I hope this message finds you well', 'delve', 'furthermore', 'in today's fast-paced world'), filler and repetition.\n- Vary sentence length and use contractions where they sound natural.\n- Keep the meaning, every fact, and roughly the same length and level of formality.""
    }
  ]
}
";
    }
}
