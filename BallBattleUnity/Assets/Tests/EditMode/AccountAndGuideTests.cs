using System.Collections.Generic;
using BallBattle.EditorTools;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>21/10: name + password accounts with a cloud save, and BI the guide ball explaining every screen.</summary>
    public class AccountAndGuideTests
    {
        GameObject root;
        ArtLibrary art;
        readonly Dictionary<string, string> savedPrefs = new Dictionary<string, string>();
        static readonly string[] Keys = { AccountSession.TokenKey, AccountSession.NameKey, GuideView.SeenKey };
        int savedVersion;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("AccountAndGuideTests");
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            foreach (var k in Keys) savedPrefs[k] = PlayerPrefs.GetString(k, null);
            savedVersion = PlayerPrefs.GetInt(AccountSession.SaveVersionKey, 0);
            foreach (var k in Keys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.DeleteKey(AccountSession.SaveVersionKey);
        }

        [TearDown]
        public void TearDown()
        {
            PvpHttp.Fake = null;
            TextEntry.TestAnswer = null;
            AccountView.ReloadOverride = null;
            GuideView.Enabled = false;
            Object.DestroyImmediate(root);
            foreach (var kv in savedPrefs)
                if (kv.Value == null) PlayerPrefs.DeleteKey(kv.Key); else PlayerPrefs.SetString(kv.Key, kv.Value);
            PlayerPrefs.SetInt(AccountSession.SaveVersionKey, savedVersion);
        }

        // ---- Guide ----

        [Test]
        public void EveryGuideLineDrawsInTheFontAndFitsTheBox()
        {
            var ids = new HashSet<string>();
            foreach (var scene in GuideScripts.All)
            {
                Assert.That(ids.Add(scene.Id), Is.True, "unique id " + scene.Id);
                Assert.That(scene.Lines.Length, Is.GreaterThan(0), scene.Id);
                foreach (var line in scene.Lines)
                {
                    foreach (var text in new[] { line.Vi, line.En })
                    {
                        Assert.That(text, Is.EqualTo(text.ToUpperInvariant()), "upper case: " + text);
                        foreach (var c in text) Assert.That(PixelFont.Default.Has(c), Is.True, $"'{c}' in \"{text}\"");
                        Assert.That(TextWrap.SplitToFit(text, GuideView.TextWidth).Count, Is.LessThanOrEqualTo(GuideView.MaxLines), text);
                    }
                    if (line.Focus.HasValue)
                    {
                        var r = line.Focus.Value;
                        Assert.That(r.xMin >= -135 && r.xMax <= 135 && r.yMin >= -240 && r.yMax <= 240, Is.True, scene.Id + " focus on screen");
                        Assert.That(r.Overlaps(GuideView.BottomPanel) && r.Overlaps(GuideView.TopPanel), Is.False, scene.Id + ": the box has a free side");
                    }
                }
            }
            Assert.That(ids.Count, Is.GreaterThanOrEqualTo(20), "every feature has its scene");
        }

        [Test]
        public void GuideTalksBlocksTheScreenAndRemembersWhatWasSeen()
        {
            GuideView.Enabled = true;
            var guide = GuideView.Create(root.transform, art);
            var other = PixelButton.Create(root.transform, "Other", art, new Rect(-20, 0, 40, 20), "X", 1, Palette.Floor, Palette.Text, Palette.Text, 60);
            Assert.That(guide.Offer(GuideScripts.Prepare), Is.True);
            Assert.That(GuideView.Blocking, Is.True);
            Assert.That(other.enabled, Is.False, "the screen under BI waits");
            Assert.That(guide.LineComplete, Is.False, "the line is typed out");
            guide.PressNext();
            Assert.That(guide.LineComplete, Is.True, "first tap finishes the line");
            Assert.That(guide.LineIndex, Is.Zero);
            Assert.That(guide.PanelArea, Is.EqualTo(GuideView.TopPanel).Or.EqualTo(GuideView.BottomPanel));
            for (var i = 1; i < GuideScripts.Prepare.Lines.Length; i++) { guide.PressNext(); guide.PressNext(); }
            Assert.That(guide.LineIndex, Is.EqualTo(GuideScripts.Prepare.Lines.Length - 1));
            Assert.That(guide.PanelArea, Is.EqualTo(GuideView.TopPanel), "FIGHT is at the bottom: the box moves up");
            guide.PressNext(); guide.PressNext();
            Assert.That(guide.Showing, Is.False);
            Assert.That(other.enabled, Is.True);
            Assert.That(GuideView.Seen("prepare"), Is.True);
            Assert.That(guide.Offer(GuideScripts.Prepare), Is.False, "once only");

            Assert.That(guide.Offer(GuideScripts.Fight), Is.True);
            guide.PressSkip();
            Assert.That(guide.Showing, Is.False);
            Assert.That(GuideView.Seen("fight"), Is.True, "SKIP ends the scene for good");

            GuideView.ResetSeen();
            Assert.That(guide.Offer(GuideScripts.Prepare), Is.True, "REPLAY GUIDE plays it again");
            guide.Cancel();
            Assert.That(GuideView.Seen("prepare"), Is.False, "a scene whose screen went away is not marked");
        }

        [Test]
        public void GuidePortraitIsBiNotAPhoto()
        {
            foreach (GuideArt.Face face in System.Enum.GetValues(typeof(GuideArt.Face)))
            {
                var sprite = GuideArt.Get(face);
                Assert.That(sprite.texture.width, Is.EqualTo(GuideArt.Size));
                Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            }
            var idle = GuideArt.Draw(GuideArt.Face.Idle);
            var blink = GuideArt.Draw(GuideArt.Face.Blink);
            Assert.That(idle, Is.Not.EqualTo(blink));
            Assert.That(idle[0].a, Is.Zero, "round: the corners are clear");
        }

        // ---- Account ----

        static PvpHttp.Reply Ok(string json) => new PvpHttp.Reply { Code = 200, Text = json };

        [Test]
        public void NamesAreCleanedAndCheckedLikeTheServer()
        {
            Assert.That(AccountView.Clean(" phú_12!3 "), Is.EqualTo("PH123"));
            Assert.That(AccountView.Clean("abcdefghijklmnop"), Is.EqualTo("ABCDEFGHIJKL"));
            Assert.That(AccountView.NameValid("AB"), Is.False);
            Assert.That(AccountView.NameValid("BOTMAN"), Is.False);
            Assert.That(AccountView.NameValid("PHU123"), Is.True);
            Assert.That(AccountView.Dots(4), Is.EqualTo("...."));
        }

        [Test]
        public void RegisterSignsInAndUploadsTheSave()
        {
            var store = new MemorySaveStore();
            var data = store.Load(); data.Progress.RunsPlayed = 4; store.Save(data);
            var calls = new List<string>();
            PvpHttp.Fake = (method, route, body) =>
            {
                calls.Add(method + " " + route);
                if (route == "/v1/auth/register") return Ok("{\"token\":\"" + new string('A', 64) + "\",\"account\":{\"id\":\"x\",\"name\":\"PHU123\",\"login\":\"PHU123\",\"saveVersion\":0}}");
                if (route == "/v1/me/save" && method == "PUT") { Assert.That(body, Does.Contain("RunsPlayed")); return Ok("{\"ok\":true,\"conflict\":false,\"version\":1}"); }
                return new PvpHttp.Reply { Code = 404, Error = "?" };
            };
            var view = AccountView.Create(root.transform, art, store);
            var closed = false;
            view.Open(AccountView.Mode.FirstLaunch, () => closed = true);
            Assert.That(view.LaterButton.gameObject.activeSelf, Is.False, "first launch: no way around it while the server answers");
            // Fields are typed through TextEntry (WebGL box / phone keyboard / desktop keys).
            TextEntry.TestAnswer = title => title == "NAME" ? "phu123" : "secret!1";
            view.PressNameField();
            view.PressPasswordField();
            view.PressConfirmField();
            Assert.That(view.PasswordsMatch, Is.True);
            view.PressRegister();
            Assert.That(closed, Is.True, view.Status);
            Assert.That(AccountSession.HasAccount, Is.True);
            Assert.That(AccountSession.Name, Is.EqualTo("PHU123"));
            Assert.That(AccountSession.SaveVersion, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(new[] { "POST /v1/auth/register", "PUT /v1/me/save" }));
        }

        [Test]
        public void LoginOnAFreshDeviceTakesTheCloudSave()
        {
            var store = new MemorySaveStore();
            var cloud = new SaveData(); cloud.Progress.RunsPlayed = 9; cloud.Progress.Gold = 300;
            var cloudJson = JsonUtility.ToJson(cloud);
            PvpHttp.Fake = (method, route, body) =>
            {
                if (route == "/v1/auth/login") return Ok("{\"token\":\"" + new string('B', 64) + "\",\"account\":{\"id\":\"x\",\"name\":\"PHU123\",\"login\":\"PHU123\",\"saveVersion\":3}}");
                if (route == "/v1/me/save" && method == "GET") return Ok("{\"data\":" + Quote(cloudJson) + ",\"version\":3}");
                return new PvpHttp.Reply { Code = 404, Error = "?" };
            };
            var reloaded = false;
            AccountView.ReloadOverride = () => reloaded = true;
            var view = AccountView.Create(root.transform, art, store);
            view.Open(AccountView.Mode.Required, null);
            view.SetFields("phu123", "secret!1");
            view.PressLogin();
            Assert.That(reloaded, Is.True, view.Status);
            Assert.That(store.Load().Progress.RunsPlayed, Is.EqualTo(9), "the account's progress is now on this device");
            Assert.That(AccountSession.SaveVersion, Is.EqualTo(3));
        }

        [Test]
        public void LoginWithProgressOnBothSidesAsksWhichToKeep()
        {
            var store = new MemorySaveStore();
            var local = store.Load(); local.Progress.RunsPlayed = 2; store.Save(local);
            var cloud = new SaveData(); cloud.Progress.RunsPlayed = 9;
            string uploaded = null;
            PvpHttp.Fake = (method, route, body) =>
            {
                if (route == "/v1/auth/login") return Ok("{\"token\":\"" + new string('C', 64) + "\",\"account\":{\"id\":\"x\",\"name\":\"PHU123\",\"login\":\"PHU123\",\"saveVersion\":5}}");
                if (route == "/v1/me/save" && method == "GET") return Ok("{\"data\":" + Quote(JsonUtility.ToJson(cloud)) + ",\"version\":5}");
                if (route == "/v1/me/save" && method == "PUT") { uploaded = body; return Ok("{\"ok\":true,\"conflict\":false,\"version\":6}"); }
                return new PvpHttp.Reply { Code = 404, Error = "?" };
            };
            var view = AccountView.Create(root.transform, art, store);
            var closed = false;
            view.Open(AccountView.Mode.Required, () => closed = true);
            view.SetFields("phu123", "secret!1");
            view.PressLogin();
            Assert.That(view.Choosing, Is.True, view.Status);
            view.PressKeepLocal();
            Assert.That(closed, Is.True);
            Assert.That(uploaded, Does.Contain("\\\"RunsPlayed\\\":2").Or.Contain("RunsPlayed\\\":2"), "this device's save replaced the cloud one");
            Assert.That(AccountSession.SaveVersion, Is.EqualTo(6));
            Assert.That(store.Load().Progress.RunsPlayed, Is.EqualTo(2));
        }

        [Test]
        public void ServerDownOnFirstLaunchOffersLater()
        {
            PvpHttp.Fake = (method, route, body) => new PvpHttp.Reply { Code = 0, Error = "SERVER UNAVAILABLE" };
            var view = AccountView.Create(root.transform, art, new MemorySaveStore());
            var closed = false;
            view.Open(AccountView.Mode.FirstLaunch, () => closed = true);
            view.SetFields("phu123", "secret!1");
            view.PressRegister();
            Assert.That(view.LaterButton.gameObject.activeSelf, Is.True);
            Assert.That(AccountSession.HasAccount, Is.False);
            view.PressLater();
            Assert.That(closed, Is.True);
            Assert.That(AccountSession.SkippedThisSession, Is.True);
            AccountSession.SkippedThisSession = false;
        }

        [Test]
        public void RegisterNeedsThePasswordTypedTwiceTheSame()
        {
            var calls = 0;
            PvpHttp.Fake = (method, route, body) => { calls++; return new PvpHttp.Reply { Code = 500, Error = "?" }; };
            var view = AccountView.Create(root.transform, art, new MemorySaveStore());
            view.Open(AccountView.Mode.Required, null);
            view.SetFields("phu123", "secret!1", "secret!2");
            Assert.That(view.PasswordsMatch, Is.False);
            view.PressRegister();
            Assert.That(view.Status, Is.EqualTo("PASSWORDS DO NOT MATCH"));
            Assert.That(calls, Is.Zero, "nothing is sent");
            Assert.That(view.RegisterButton.FillColor, Is.Not.EqualTo((Color32)Palette.PrimaryFill), "REGISTER looks disabled");
            view.PressLogin();
            Assert.That(calls, Is.EqualTo(1), "LOG IN does not need the second password");
            Assert.That(AccountView.ConfirmArea.Overlaps(AccountView.PasswordArea) || AccountView.ConfirmArea.Overlaps(AccountView.RegisterArea), Is.False);
        }

        [Test]
        public void TypingOnThePcKeyboardKeepsEachFieldWithoutEnter()
        {
            // In the Editor (Device Simulator too) and on desktop, fields take PC keys; moving on keeps what was typed.
            var view = AccountView.Create(root.transform, art, new MemorySaveStore());
            view.Open(AccountView.Mode.Required, null);
            view.PressNameField();
            Assert.That(TextEntry.Open, Is.True);
            TextEntry.SimulateTyping("phu123");
            Assert.That(TextEntry.Typing, Is.EqualTo("phu123"));
            view.PressPasswordField();   // no Enter: the name stays
            TextEntry.SimulateTyping("secret!1");
            view.PressConfirmField();
            TextEntry.SimulateTyping("secret!1");
            TextEntry.CommitPending();
            Assert.That(TextEntry.Open, Is.False);
            Assert.That(view.PasswordsMatch, Is.True);
            PvpHttp.Fake = (m, r, b) => { StringAssert.Contains("PHU123", b); return new PvpHttp.Reply { Code = 500, Error = "X" }; };
            view.PressLogin();
        }

        static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
