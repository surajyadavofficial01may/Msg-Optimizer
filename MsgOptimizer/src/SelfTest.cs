using System;
using System.Drawing;
using System.Linq;

namespace MsgOptimizer
{
    /// MsgOptimizer.exe --selftest [--ping]: checks the pure logic (no input, no windows);
    /// --ping also makes one real request to the configured server. Exit code 0 = pass.
    static class SelfTest
    {
        public static int Run(bool ping)
        {
            Native.AttachConsole(-1); // print into the console that started us
            Console.WriteLine();
            try
            {
                CleanUp();
                SettingsFile();
                Placement();
                HitTesting();
                Console.WriteLine("selftest OK");
                if (ping) Console.WriteLine("ping: " + LlmClient.TestAsync(Settings.Load()).Result);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("selftest FAILED: " + ex.Message);
                return 1;
            }
        }

        static void CleanUp()
        {
            Check(LlmClient.Clean("Sure! Here's a polished version:\n\nHi team, the build is green.", "hi team the build green") == "Hi team, the build is green.", "strips a chatty preamble");
            Check(LlmClient.Clean("Here's the plan:\nShip it.", "Here's the plan:\nship it") == "Here's the plan:\nShip it.", "keeps the author's own opening line");
            Check(LlmClient.Clean("<think>hmm</think>\"Fixed text.\"", "fixd text") == "Fixed text.", "strips reasoning and wrapping quotes");
            Check(LlmClient.Clean("\"A\" and \"B\"", "a and b") == "\"A\" and \"B\"", "keeps inner quotes");
            Check(LlmClient.Clean("```\nA\nB\n```", "a b") == "A\nB", "unwraps a code fence");
            Check(LlmClient.Rewrap("Hello.\nBye.", " hello \n") == " Hello.\r\nBye. \r\n", "re-applies the selection's whitespace");
        }

        static void SettingsFile()
        {
            var s = Settings.Parse(Settings.DefaultJson);
            Check(string.Join("|", s.Actions.Select(a => a.Label)) == "Enhance for Mail|Enhance for Teams|Correct English|Humanize", "default menu items");
            Check(s.IsDemo, "no key means demo mode");

            var v1 = Settings.Parse(@"{""BaseUrl"":""http://localhost:11434/v1"",""Actions"":[{""Label"":""Humanize"",""Prompt"":""p""},{""Label"":""Mine"",""Prompt"":""q""}]}");
            Check(!v1.IsDemo, "a local server needs no key");
            Check(v1.Actions[0].Icon == "E77B" && v1.Actions[1].Icon == "E70F", "older settings files get default icons");

            s.SetApiKey("secret-key");
            Check(s.ApiKeyProtected.Length > 0 && !s.ApiKeyProtected.Contains("secret") && s.GetApiKey() == "secret-key", "API key is stored encrypted");
            string json = Settings.ToJson(s);
            var back = Settings.Parse(json);
            Check(back.GetApiKey() == "secret-key" && back.Actions[2].Prompt == s.Actions[2].Prompt && !json.Contains("IsDemo"), "settings round-trip through JSON");
            Check(json.Contains("author's") && json.Contains("\r\n    {"), "saved JSON stays readable");
        }

        static void Placement()
        {
            var work = new Rectangle(0, 0, 1920, 1080);
            var size = new Size(200, 40);
            Func<Rectangle, bool> free = r => true;
            // Real cases from the diagnostic log (125% scaling).
            var edge = new Rectangle(473, 330, 445, 655);   // Edge's menu window: 14 px shadow margin left of the click
            var word = new Rectangle(598, 476, 300, 600);   // Word: search box above the first item at the click
            Check(MenuController.IsAnchoredAt(edge, new Point(487, 330), 1.25f), "finds Edge's menu at the click");
            Check(MenuController.IsAnchoredAt(word, new Point(600, 523), 1.25f), "finds Word's menu despite its search box");
            var shifted = new Rectangle(436, 360, 683, 695); // Edge near the screen bottom: menu shifted up to fit
            Check(MenuController.IsAnchoredAt(shifted, new Point(450, 503), 1.25f), "finds a menu shifted up to fit on screen");
            Check(!MenuController.IsAnchoredAt(new Rectangle(-15, 330, 719, 615), new Point(487, 330), 1.25f), "ignores an unrelated popup under the cursor");
            Check(!MenuController.IsAnchoredAt(new Rectangle(487, 350, 180, 30), new Point(487, 330), 1.25f), "ignores a tooltip");

            Check(MenuController.Dock(new Point(487, 330), size, work, edge, 2, free).Location == new Point(487, 288), "docks on top of a down-right menu, left-aligned");
            Check(MenuController.Dock(new Point(600, 523), size, work, word, 2, free).Location == new Point(600, 434), "docks above Word's search box");
            Check(MenuController.Dock(new Point(450, 503), size, work, shifted, 2, free).Location == new Point(450, 318), "docks above a shifted menu, not over it");
            Check(MenuController.Dock(new Point(600, 523), size, work, word, 2, r => r.Y > 470).Location == new Point(398, 523), "beside the menu when the spot above is taken (Word's toolbar)");
            Check(MenuController.Dock(new Point(1800, 1000), size, work, new Rectangle(1500, 586, 314, 414), 2, free).Location == new Point(1600, 1002), "docks under an up-left menu, right-aligned");
            Check(MenuController.Dock(new Point(500, 300), size, work, Rectangle.Empty, 2, free).Location == new Point(500, 258), "no app menu window: above the click");
            Check(MenuController.Dock(new Point(500, 10), size, work, Rectangle.Empty, 2, free).Location == new Point(298, 10), "no room above: beside the menu");

            Check(MenuController.VisibleWidth(edge, new Point(487, 330)) == 417, "matches the app menu's visible width");

            var sub = new Size(250, 170);
            var onTop = new Rectangle(487, 278, 417, 50);
            Check(MenuController.PlaceSubmenu(onTop, sub, false, true, work, edge).Location == new Point(906, 158), "submenu opens to the right, growing upwards off the app menu");
            // Logged: Word near the left edge; our item beside Word's menu, no room further left.
            var besideWord = new Rectangle(118, 456, 279, 50);
            var wordMenu = new Rectangle(400, 456, 279, 536);
            Check(MenuController.PlaceSubmenu(besideWord, sub, true, false, work, wordMenu).Location == new Point(118, 508), "submenu goes under our item rather than over Word's menu");
            // Logged samples from Edge's dark menu: the first one hit the anti-aliased edge.
            var navy = MenuController.MajorityColor(new[] { Color.FromArgb(18, 43, 87), Color.FromArgb(2, 13, 24), Color.FromArgb(2, 13, 24) });
            Check(navy.HasValue && navy.Value.R == 2 && navy.Value.B == 24, "takes the app menu's colour from agreeing samples");
            Check(!MenuController.MajorityColor(new[] { Color.Red, Color.Green, Color.Blue }).HasValue, "no colour when the samples disagree");
            Check(MenuController.VisibleWidth(Rectangle.Empty, new Point(487, 330)) == 0, "no app menu, no width to match");
        }

        static void HitTesting()
        {
            var g = new MenuGeometry(new Rectangle(300, 100, 150, 40), new Rectangle(100, 100, 200, 136), 4, 32, 4);
            Check(g.HitChild(new Point(150, 104)) == 0 && g.HitChild(new Point(150, 231)) == 3, "hit-tests items");
            Check(g.HitChild(new Point(150, 102)) == -1 && g.HitChild(new Point(150, 234)) == -1, "padding is inside but not an item");
            Check(g.HitChild(new Point(150, 236)) == MenuGeometry.Outside && g.HitParent(new Point(310, 110)) == 0, "outside vs parent item");
        }

        static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception(what);
        }
    }
}
