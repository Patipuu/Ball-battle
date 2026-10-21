using System;
using System.Collections;
using BallBattle.Sim.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BallBattle.View
{
    /// <summary>
    /// ACCOUNT screen (Phu 21/10): on first launch the player picks a name and a password (REGISTER) or signs in to an
    /// existing account (LOG IN) - that account is the PvP identity and keeps a cloud copy of the save, so the same name +
    /// password brings the progress to another phone or browser. When the server cannot be reached, LATER lets the game
    /// start offline (asked again next launch). From Settings it shows who is signed in, LOG OUT and save conflicts.
    /// </summary>
    public sealed class AccountView : MonoBehaviour
    {
        public enum Mode { FirstLaunch, Required, Manage }

        public const int Order = 150;
        /// <summary>First launch opens this screen (off with -bbNoTips and in the EditMode test assembly).</summary>
        public static bool ShowOnLaunch = !DebugOptions.NoTips;
        public static readonly Rect NameArea = new Rect(-110, 110, 220, 26);
        public static readonly Rect PasswordArea = new Rect(-110, 66, 220, 26);
        /// <summary>Password typed a second time: REGISTER only goes ahead when both match (LOG IN ignores it).</summary>
        public static readonly Rect ConfirmArea = new Rect(-110, 22, 220, 26);
        public static readonly Rect RegisterArea = new Rect(-110, -50, 106, 30);
        public static readonly Rect LoginArea = new Rect(4, -50, 106, 30);
        public static readonly Rect LaterArea = new Rect(-60, -140, 120, 26);
        public static readonly Rect LogoutArea = new Rect(-110, 40, 220, 28);
        public static readonly Rect KeepCloudArea = new Rect(-114, -176, 110, 28);
        public static readonly Rect KeepLocalArea = new Rect(4, -176, 110, 28);

        [Serializable] sealed class AuthBody { public string Name; public string Password; }
        [Serializable] sealed class AuthReply { public string token; public AccountInfo account; }
        [Serializable] sealed class AccountInfo { public string id, name, login; public int saveVersion; }

        ISaveStore store;
        Mode mode;
        Action onDone;
        string nameText = "", password = "", confirm = "";
        string cloudJson;
        int cloudVersion;
        bool busy;
        float caretClock;

        PixelText title, subtitle, nameValue, passValue, confirmValue, rule1, rule2, status1, status2, signedIn, choose1, choose2, choose3, choose4;
        PixelButton nameField, passField, confirmField, registerButton, loginButton, laterButton, logoutButton, keepCloudButton, keepLocalButton;
        SpriteRenderer caret, chooseBorder, chooseFill;
        Transform formRoot, manageRoot, chooseRoot;

        public bool IsOpen => gameObject.activeSelf;
        public Mode CurrentMode => mode;
        public string Status => (status1.Text + " " + status2.Text).Trim();
        public bool Choosing => chooseRoot.gameObject.activeSelf;
        public PixelButton RegisterButton => registerButton;
        public PixelButton LoginButton => loginButton;
        public PixelButton LaterButton => laterButton;
        public PixelButton NameField => nameField;
        public PixelButton PasswordField => passField;
        public PixelButton ConfirmField => confirmField;
        public PixelButton LogoutButton => logoutButton;
        public PixelButton KeepCloudButton => keepCloudButton;
        public PixelButton KeepLocalButton => keepLocalButton;
        /// <summary>Test hook: replaces the scene reload after a cloud save is adopted.</summary>
        public static Action ReloadOverride;

        public static AccountView Create(Transform parent, ArtLibrary art, ISaveStore store)
        {
            var go = new GameObject("AccountPage");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<AccountView>();
            v.store = store;
            v.Build(art);
            go.SetActive(false);
            return v;
        }

        void Build(ArtLibrary art)
        {
            PixelRect.Set(PixelRect.Create(transform, "Background", art.Pixel, Palette.Background, Order), -200, -300, 200, 300);
            title = Text(transform, art, 0, 186, 2);
            subtitle = Text(transform, art, 0, 160, 1);

            formRoot = Child("Form");
            Text(formRoot, art, NameArea.xMin, NameArea.yMax + 6, 1, PixelText.Align.Left).Set("NAME", Palette.TextDim);
            nameField = Field(formRoot, art, "NameField", NameArea, () => Edit(0));
            nameValue = Text(formRoot, art, NameArea.xMin + 7, NameArea.yMin + 9, 1, PixelText.Align.Left, nameField.IconOrder + 1);
            Text(formRoot, art, PasswordArea.xMin, PasswordArea.yMax + 6, 1, PixelText.Align.Left).Set("PASSWORD", Palette.TextDim);
            passField = Field(formRoot, art, "PasswordField", PasswordArea, () => Edit(1));
            passValue = Text(formRoot, art, PasswordArea.xMin + 7, PasswordArea.yMin + 9, 1, PixelText.Align.Left, passField.IconOrder + 1);
            Text(formRoot, art, ConfirmArea.xMin, ConfirmArea.yMax + 6, 1, PixelText.Align.Left).Set("CONFIRM PASSWORD - REGISTER ONLY", Palette.TextDim);
            confirmField = Field(formRoot, art, "ConfirmField", ConfirmArea, () => Edit(2));
            confirmValue = Text(formRoot, art, ConfirmArea.xMin + 7, ConfirmArea.yMin + 9, 1, PixelText.Align.Left, confirmField.IconOrder + 1);
            caret = PixelRect.Create(formRoot, "Caret", art.Pixel, Palette.WallWarning, Order + 8);
            caret.enabled = false;
            rule1 = Text(formRoot, art, 0, 2, 1);
            rule2 = Text(formRoot, art, 0, -12, 1);
            rule1.Set("NAME: 3-12 LETTERS A-Z OR DIGITS", Palette.TextDim);
            rule2.Set("PASSWORD: AT LEAST 6 CHARACTERS", Palette.TextDim);
            registerButton = Button(formRoot, art, "Register", RegisterArea, "REGISTER", Palette.PrimaryFill, Palette.PrimaryText, () => Submit(true));
            loginButton = Button(formRoot, art, "Login", LoginArea, "LOG IN", Palette.Floor, Palette.WallWarning, () => Submit(false));

            status1 = Text(transform, art, 0, -72, 1);
            status2 = Text(transform, art, 0, -86, 1);

            manageRoot = Child("Manage");
            signedIn = Text(manageRoot, art, 0, 110, 1);
            logoutButton = Button(manageRoot, art, "Logout", LogoutArea, "LOG OUT", Palette.Floor, Palette.WallWarning, Logout);

            laterButton = Button(transform, art, "Later", LaterArea, "LATER", Palette.Floor, Palette.TextDim, Later);
            laterButton.ClickCue = AudioIds.UiBack;

            chooseRoot = Child("Choose");
            chooseBorder = PixelRect.Create(chooseRoot, "Border", art.Pixel, Palette.WallWarning, Order + 10);
            PixelRect.Set(chooseBorder, -122, -186, 122, -84);
            chooseFill = PixelRect.Create(chooseRoot, "Fill", art.Pixel, RunUi.Panel, Order + 11);
            PixelRect.Set(chooseFill, -121, -185, 121, -85);
            choose1 = Text(chooseRoot, art, 0, -100, 1, PixelText.Align.Center, Order + 13);
            choose2 = Text(chooseRoot, art, 0, -114, 1, PixelText.Align.Center, Order + 13);
            choose3 = Text(chooseRoot, art, 0, -132, 1, PixelText.Align.Center, Order + 13);
            choose4 = Text(chooseRoot, art, 0, -146, 1, PixelText.Align.Center, Order + 13);
            keepCloudButton = Button(chooseRoot, art, "KeepCloud", KeepCloudArea, "ACCOUNT", Palette.PrimaryFill, Palette.PrimaryText, KeepCloud, Order + 12);
            keepLocalButton = Button(chooseRoot, art, "KeepLocal", KeepLocalArea, "THIS DEVICE", Palette.Floor, Palette.WallWarning, KeepLocal, Order + 12);
            chooseRoot.gameObject.SetActive(false);
        }

        Transform Child(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            return t;
        }

        static PixelText Text(Transform parent, ArtLibrary art, float x, float y, int scale, PixelText.Align align = PixelText.Align.Center, int order = Order + 4) =>
            PixelText.Create(parent, "Text", art.Font, new Vector2(x, y), align, scale, order);

        static PixelButton Button(Transform parent, ArtLibrary art, string name, Rect area, string label, Color32 fill, Color32 accent, Action click, int order = Order + 2)
        {
            var text = fill.Equals(Palette.PrimaryFill) ? Palette.PrimaryText : accent;
            var b = PixelButton.Create(parent, name, art, area, label, 1, fill, fill.Equals(Palette.PrimaryFill) ? fill : accent, text, order);
            b.Clicked += click;
            return b;
        }

        static PixelButton Field(Transform parent, ArtLibrary art, string name, Rect area, Action click)
        {
            var b = PixelButton.Create(parent, name, art, area, null, 1, RunUi.Panel, Palette.TextDim, Palette.Text, Order + 2);
            b.Clicked += click;
            return b;
        }

        // ---- Opening ----

        public void Open(Mode openMode, Action done)
        {
            mode = openMode;
            onDone = done;
            busy = false;
            gameObject.SetActive(true);
            chooseRoot.gameObject.SetActive(false);
            SetStatus("", Palette.Text);
            var manage = mode == Mode.Manage && AccountSession.HasAccount;
            formRoot.gameObject.SetActive(!manage);
            manageRoot.gameObject.SetActive(manage);
            title.Set("ACCOUNT", Palette.Text);
            subtitle.Set(manage ? "" : "NAME + PASSWORD KEEP YOUR PROGRESS", Palette.TextDim);
            if (manage)
            {
                signedIn.Set("SIGNED IN: " + AccountSession.Name, Palette.WallWarning);
                if (AccountSession.Conflict) Go(ResolveConflict());
            }
            else { nameText = AccountSession.Name; password = ""; confirm = ""; }
            // First launch: no way out until the server answers (LATER appears after a failed try). Elsewhere BACK closes.
            laterButton.gameObject.SetActive(mode != Mode.FirstLaunch);
            laterButton.SetText(mode == Mode.FirstLaunch ? "LATER" : "BACK", Palette.TextDim);
            Redraw();
            PixelButton.NoteShownAll(transform);
        }

        public void Close()
        {
            TextEntry.Cancel();
            gameObject.SetActive(false);
            var done = onDone;
            onDone = null;
            done?.Invoke();
        }

        // ---- Form ----

        /// <summary>0 = name, 1 = password, 2 = confirm password.</summary>
        void Edit(int field)
        {
            if (busy) return;
            editingField = field;
            editing = true;
            var secret = field > 0;
            TextEntry.Request(field == 0 ? "NAME" : field == 1 ? "PASSWORD" : "CONFIRM PASSWORD", secret ? "" : nameText, secret, secret ? 64 : 12, value =>
            {
                editing = false;
                if (value == null) { Redraw(); return; }
                if (field == 1) password = value;
                else if (field == 2) confirm = value;
                else nameText = Clean(value);
                Redraw();
            });
            Redraw();
        }

        bool editing;
        int editingField;
        bool editingPassword => editingField > 0;

        /// <summary>Upper case, A-Z and 0-9 only (what the server accepts and the font draws).</summary>
        public static string Clean(string value)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in (value ?? "").ToUpperInvariant()) if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) sb.Append(c);
            return sb.Length > 12 ? sb.ToString(0, 12) : sb.ToString();
        }

        public static string Dots(int n) => n <= 0 ? "" : new string('.', Mathf.Min(n, 24));

        /// <summary>Set both fields directly (tests, automation).</summary>
        /// <summary>Set the fields directly (tests, automation). <paramref name="confirmPass"/> null = same as the password.</summary>
        public void SetFields(string name, string pass, string confirmPass = null)
        {
            nameText = Clean(name);
            password = pass ?? "";
            confirm = confirmPass ?? password;
            Redraw();
        }

        /// <summary>REGISTER needs the password typed twice the same.</summary>
        public bool PasswordsMatch => password.Length > 0 && password == confirm;

        void Redraw()
        {
            var typing = editing ? TextEntry.Typing : null;
            var shownName = typing != null && !editingPassword ? Clean(typing) : nameText;
            var shownPass = typing != null && editingField == 1 ? typing : password;
            var shownConfirm = typing != null && editingField == 2 ? typing : confirm;
            nameValue.Set(shownName.Length > 0 ? shownName : "TAP TO TYPE", shownName.Length > 0 ? Palette.Text : Palette.TextDim);
            passValue.Set(shownPass.Length > 0 ? Dots(shownPass.Length) : "TAP TO TYPE", shownPass.Length > 0 ? Palette.Text : Palette.TextDim);
            var mismatch = shownConfirm.Length > 0 && password.Length > 0 && shownConfirm != password && !(editing && editingField == 2);
            confirmValue.Set(shownConfirm.Length > 0 ? Dots(shownConfirm.Length) : "TAP TO TYPE", shownConfirm.Length == 0 ? Palette.TextDim : mismatch ? Palette.HpLost : Palette.Text);
            nameField.SetColors(RunUi.Panel, editing && editingField == 0 ? Palette.WallWarning : Palette.TextDim);
            passField.SetColors(RunUi.Panel, editing && editingField == 1 ? Palette.WallWarning : Palette.TextDim);
            confirmField.SetColors(RunUi.Panel, editing && editingField == 2 ? Palette.WallWarning : mismatch ? Palette.HpLost : Palette.TextDim);
            var valid = NameValid(nameText) && password.Length >= 6;
            var canRegister = valid && PasswordsMatch && !busy;
            registerButton.SetColors(canRegister ? Palette.PrimaryFill : Palette.Floor, canRegister ? Palette.PrimaryFill : Palette.Wall);
            registerButton.SetText("REGISTER", canRegister ? Palette.PrimaryText : Palette.Wall);
            loginButton.SetColors(Palette.Floor, valid && !busy ? Palette.WallWarning : Palette.Wall);
            loginButton.SetText("LOG IN", valid && !busy ? Palette.WallWarning : Palette.Wall);
            if (typing != null)
            {
                var field = editingField == 1 ? PasswordArea : editingField == 2 ? ConfirmArea : NameArea;
                var text = editingPassword ? Dots(typing.Length) : Clean(typing);
                var x = field.xMin + 7 + PixelFont.MeasureWidth(text) + 1;
                PixelRect.Set(caret, x, field.yMin + 7, x + 2, field.yMin + 19);
            }
        }

        public static bool NameValid(string n) => n != null && n.Length >= 3 && n.Length <= 12 && !n.StartsWith("BOT");

        void Update()
        {
            if (!IsOpen) return;
            var typing = editing && TextEntry.Typing != null;
            caretClock += Time.unscaledDeltaTime;
            caret.enabled = typing && (caretClock % 0.8f) < 0.5f;
            if (typing) Redraw();
        }

        void SetStatus(string text, Color32 color)
        {
            var lines = TextWrap.SplitToFit(UiVietnamese.Translate(text ?? ""), 250);
            status1.Set(lines.Count > 0 ? lines[0] : "", color);
            status2.Set(lines.Count > 1 ? lines[1] : "", color);
        }

        void Submit(bool register)
        {
            if (busy) return;
            if (!NameValid(nameText)) { SetStatus(nameText.StartsWith("BOT") ? "THAT NAME IS KEPT FOR BOTS." : "NAME: 3-12 LETTERS A-Z OR DIGITS", Palette.HpLost); return; }
            if (password.Length < 6) { SetStatus("PASSWORD: AT LEAST 6 CHARACTERS", Palette.HpLost); return; }
            if (register && !PasswordsMatch) { SetStatus("PASSWORDS DO NOT MATCH", Palette.HpLost); return; }
            Go(Authenticate(register));
        }

        IEnumerator Authenticate(bool register)
        {
            busy = true;
            SetStatus("CONNECTING...", Palette.Text);
            Redraw();
            PvpHttp.Reply reply = null;
            var body = JsonUtility.ToJson(new AuthBody { Name = nameText, Password = password });
            // REGISTER with an older anonymous PvP token upgrades that account (its MMR and history stay).
            var token = register ? AccountSession.Token : null;
            yield return PvpHttp.Send("POST", register ? "/v1/auth/register" : "/v1/auth/login", body, token, r => reply = r,
                () => SetStatus(PvpView.ServerWaking, Palette.WallWarning));
            busy = false;
            if (reply == null || !reply.Ok)
            {
                var offline = reply == null || reply.Code == 0 || reply.Code >= 500;
                SetStatus(reply?.Error ?? "SERVER UNAVAILABLE", Palette.HpLost);
                if (offline && mode == Mode.FirstLaunch) laterButton.gameObject.SetActive(true);   // play offline, ask again next launch
                Redraw();
                yield break;
            }
            var auth = JsonUtility.FromJson<AuthReply>(reply.Text);
            password = "";
            confirm = "";
            AccountSession.SignedIn(auth.token, auth.account.name, register ? 0 : AccountSession.SaveVersion);
            if (register)
            {
                SetStatus("ACCOUNT CREATED", NextGoalBox.Green);
                yield return AccountSession.Upload(store, true);
                Close();
                yield break;
            }
            SetStatus("SIGNED IN", NextGoalBox.Green);
            yield return SyncAfterLogin(auth.account.saveVersion);
        }

        /// <summary>
        /// After LOG IN: no cloud save → upload this device's; this device fresh → take the cloud save; both have progress →
        /// the player chooses (ACCOUNT / THIS DEVICE).
        /// </summary>
        IEnumerator SyncAfterLogin(int serverVersion)
        {
            string json = null; var version = 0;
            yield return AccountSession.Download((j, v) => { json = j; version = v; });
            if (json == null) { Close(); yield break; }   // offline now: keep playing, the next upload will sort it out
            var local = store.Load();
            if (json.Length == 0)
            {
                AccountSession.SaveVersion = 0;
                yield return AccountSession.Upload(store, true);
                Close();
                yield break;
            }
            if (AccountSession.LocalIsFresh(local)) { Adopt(json, version); yield break; }
            AskWhichSave(json, version, local);
        }

        IEnumerator ResolveConflict()
        {
            string json = null; var version = 0;
            yield return AccountSession.Download((j, v) => { json = j; version = v; });
            if (!string.IsNullOrEmpty(json)) AskWhichSave(json, version, store.Load());
        }

        void AskWhichSave(string json, int version, SaveData local)
        {
            cloudJson = json;
            cloudVersion = version;
            var cloud = AccountSession.ParseSave(json);
            chooseRoot.gameObject.SetActive(true);
            choose1.Set("TWO SAVES FOUND. KEEP WHICH ONE?", Palette.Text);
            choose2.Set("THE OTHER ONE IS REPLACED", Palette.TextDim);
            choose3.Set("ACCOUNT: " + Summary(cloud), Palette.WallWarning);
            choose4.Set("THIS DEVICE: " + Summary(local), Palette.Text);
            PixelButton.NoteShownAll(chooseRoot);
        }

        /// <summary>"12 RUNS  450 GOLD" for the save choice.</summary>
        public static string Summary(SaveData d) =>
            d?.Progress == null ? "-" : $"{d.Progress.RunsPlayed} RUNS  {d.Progress.Gold} GOLD";

        void KeepCloud() => Adopt(cloudJson, cloudVersion);

        void KeepLocal()
        {
            chooseRoot.gameObject.SetActive(false);
            Go(KeepLocalThenClose());
        }

        IEnumerator KeepLocalThenClose()
        {
            yield return AccountSession.ForceUpload(store, cloudVersion);
            Close();
        }

        void Adopt(string json, int version)
        {
            chooseRoot.gameObject.SetActive(false);
            if (!AccountSession.Adopt(store, json, version)) { SetStatus("CLOUD SAVE IS DAMAGED", Palette.HpLost); return; }
            // Every screen read the old save at start-up: reload the scene so the account's progress is shown everywhere.
            if (ReloadOverride != null) { ReloadOverride(); Close(); return; }
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Run a server conversation: a coroutine in the game; drained at once when PvpHttp.Fake answers (tests).</summary>
        void Go(IEnumerator routine)
        {
            if (PvpHttp.Fake == null) { StartCoroutine(routine); return; }
            Drain(routine);
        }

        /// <summary>Runs <paramref name="routine"/> to the end now, nested enumerators included (fake network only).</summary>
        public static void Drain(IEnumerator routine)
        {
            var stack = new System.Collections.Generic.Stack<IEnumerator>();
            stack.Push(routine);
            var guard = 0;
            while (stack.Count > 0 && guard++ < 100000)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) stack.Push(nested);
            }
        }

        // ---- Tests / automation (same paths as the buttons) ----
        public void PressRegister() => Submit(true);
        public void PressLogin() => Submit(false);
        public void PressLater() => Later();
        public void PressKeepLocal() => KeepLocal();
        public void PressKeepCloud() => KeepCloud();
        public void PressNameField() => Edit(0);
        public void PressPasswordField() => Edit(1);
        public void PressConfirmField() => Edit(2);

        // ---- Manage / leave ----

        void Logout()
        {
            if (busy) return;
            Go(LogoutThenForm());
        }

        IEnumerator LogoutThenForm()
        {
            busy = true;
            yield return AccountSession.Upload(store, true);   // the last progress goes up before this device forgets the account
            var token = AccountSession.Token;
            yield return PvpHttp.Send("POST", "/v1/auth/logout", "{}", token, _ => { });
            AccountSession.SignOutLocal();
            busy = false;
            Open(Mode.Required, onDone);
            SetStatus("SIGNED OUT", Palette.TextDim);
        }

        void Later()
        {
            if (busy) return;
            if (mode == Mode.FirstLaunch) AccountSession.SkippedThisSession = true;
            Close();
        }
    }
}
