using System;
using System.IO;
using System.Text.Json;
using BallBattle.PvpServer;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>Name + password accounts and the cloud save (21/10).</summary>
    public class PvpAuthTests
    {
        static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

        static string Folder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "bb-pvp-auth-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        [Test]
        public void RegisterLoginLogoutAndUniqueNames()
        {
            var folder = Folder();
            try
            {
                var path = Path.Combine(folder, "data.json");
                var store = new PvpStore(path);
                var reg = Json(store.RegisterNamed(new AuthRequest { Name = "phu123", Password = "secret!1" }));
                var token1 = reg.GetProperty("token").GetString();
                Assert.That(reg.GetProperty("account").GetProperty("login").GetString(), Is.EqualTo("PHU123"));
                Assert.That(reg.GetProperty("account").GetProperty("name").GetString(), Is.EqualTo("PHU123"));
                var id = store.Authenticate("Bearer " + token1);
                Assert.That(id, Is.Not.Null);

                Assert.Throws<ArgumentException>(() => store.RegisterNamed(new AuthRequest { Name = "PHU123", Password = "another1" }), "taken");
                Assert.Throws<ArgumentException>(() => store.RegisterNamed(new AuthRequest { Name = "ab", Password = "secret!1" }), "too short");
                Assert.Throws<ArgumentException>(() => store.RegisterNamed(new AuthRequest { Name = "PHU_1", Password = "secret!1" }), "bad char");
                Assert.Throws<ArgumentException>(() => store.RegisterNamed(new AuthRequest { Name = "BOTX", Password = "secret!1" }), "bot name");
                Assert.Throws<ArgumentException>(() => store.RegisterNamed(new AuthRequest { Name = "NEWONE", Password = "123" }), "short password");

                var wrong = Assert.Throws<ArgumentException>(() => store.Login(new AuthRequest { Name = "phu123", Password = "nope!!!!" }));
                Assert.That(wrong.Message, Is.EqualTo("Wrong name or password."));
                Assert.That(Assert.Throws<ArgumentException>(() => store.Login(new AuthRequest { Name = "nobody", Password = "secret!1" })).Message,
                    Is.EqualTo("Wrong name or password."), "unknown names answer the same");

                // A second device signs in: both tokens work; restart keeps both; logout drops only its own.
                var token2 = Json(store.Login(new AuthRequest { Name = "PHU123", Password = "secret!1" })).GetProperty("token").GetString();
                store = new PvpStore(path);
                Assert.That(store.Authenticate("Bearer " + token1), Is.EqualTo(id));
                Assert.That(store.Authenticate("Bearer " + token2), Is.EqualTo(id));
                store.Logout(id, "Bearer " + token2);
                Assert.That(store.Authenticate("Bearer " + token2), Is.Null);
                Assert.That(store.Authenticate("Bearer " + token1), Is.EqualTo(id));

                Assert.That(File.ReadAllText(path), Does.Not.Contain("secret!1"), "password never stored in clear");
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void AnonymousPvpAccountIsKeptWhenNamed()
        {
            var folder = Folder();
            try
            {
                var store = new PvpStore(Path.Combine(folder, "data.json"));
                var anon = Json(store.Register());
                var anonToken = anon.GetProperty("token").GetString();
                var anonId = store.Authenticate("Bearer " + anonToken);
                var named = Json(store.RegisterNamed(new AuthRequest { Name = "OLDPLAYER", Password = "secret!1" }, anonId));
                Assert.That(named.GetProperty("account").GetProperty("id").GetString(), Is.EqualTo(anonId), "same account, now named");
                Assert.That(store.Authenticate("Bearer " + anonToken), Is.EqualTo(anonId), "the old token keeps working");
                Assert.That(store.Authenticate("Bearer " + named.GetProperty("token").GetString()), Is.EqualTo(anonId));
                // A named account cannot be claimed again.
                var other = Json(store.RegisterNamed(new AuthRequest { Name = "SECOND", Password = "secret!1" }, anonId));
                Assert.That(other.GetProperty("account").GetProperty("id").GetString(), Is.Not.EqualTo(anonId));
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void CloudSaveRoundTripsWithVersionCheck()
        {
            var folder = Folder();
            try
            {
                var path = Path.Combine(folder, "data.json");
                var store = new PvpStore(path);
                var anonId = store.Authenticate("Bearer " + Json(store.Register()).GetProperty("token").GetString());
                Assert.Throws<ArgumentException>(() => store.PutSave(anonId, new SaveRequest { Data = "{}", BaseVersion = 0 }), "named accounts only");

                var id = store.Authenticate("Bearer " + Json(store.RegisterNamed(new AuthRequest { Name = "SAVER", Password = "secret!1" })).GetProperty("token").GetString());
                var empty = Json(store.GetSave(id));
                Assert.That(empty.GetProperty("version").GetInt32(), Is.Zero);
                Assert.That(empty.GetProperty("data").GetString(), Is.Empty);

                var put = Json(store.PutSave(id, new SaveRequest { Data = "{\"Progress\":{\"RunsPlayed\":3}}", BaseVersion = 0 }));
                Assert.That(put.GetProperty("ok").GetBoolean(), Is.True);
                Assert.That(put.GetProperty("version").GetInt32(), Is.EqualTo(1));
                var stale = Json(store.PutSave(id, new SaveRequest { Data = "{\"x\":1}", BaseVersion = 0 }));
                Assert.That(stale.GetProperty("conflict").GetBoolean(), Is.True, "another device saved first");
                Assert.Throws<ArgumentException>(() => store.PutSave(id, new SaveRequest { Data = "not json", BaseVersion = 1 }));
                Assert.Throws<ArgumentException>(() => store.PutSave(id, new SaveRequest { Data = new string('a', PvpStore.MaxSaveChars + 1), BaseVersion = 1 }));

                store = new PvpStore(path);
                var got = Json(store.GetSave(id));
                Assert.That(got.GetProperty("version").GetInt32(), Is.EqualTo(1));
                Assert.That(got.GetProperty("data").GetString(), Does.Contain("RunsPlayed"));
                Assert.That(Json(store.Me(id)).GetProperty("saveVersion").GetInt32(), Is.EqualTo(1));
            }
            finally { Directory.Delete(folder, true); }
        }
    }
}
