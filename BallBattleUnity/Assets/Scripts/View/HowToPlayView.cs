using System;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// UI-MENU1: HOW TO PLAY, opened from the hub. Four picture steps (pick a ball, pick cards, watch the fight, win 8 fights),
    /// the unlock line, START A RUN (same as the hub's RUN / CONTINUE RUN) and BACK.
    /// </summary>
    public sealed class HowToPlayView : MonoBehaviour
    {
        public event Action StartRequested;
        public event Action Closed;

        public const float TextX = -70f;
        public const float StepTop = 170f, StepHeight = 56f, StepGap = 6f;
        public static readonly Rect StartArea = new Rect(-90, -150, 180, 34);
        public static readonly Rect BackArea = new Rect(-60, -205, 120, 30);

        public static readonly (string title, string detail)[] Steps =
        {
            ("1 PICK A BALL", "EACH HAS A WEAPON AND A SKILL"),
            ("2 PICK CARDS", "CARDS STAY FOR THE WHOLE RUN"),
            ("3 WATCH IT FIGHT", "TAP TO USE YOUR SKILL"),
            ("4 WIN 8 FIGHTS", "HP CARRIES OVER"),
        };
        public const string UnlockLine = "DAILY QUESTS GIVE CHESTS";

        public PixelButton StartButton { get; private set; }
        public PixelButton BackButton { get; private set; }

        public static HowToPlayView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "HowToPlay", art);
            var v = root.gameObject.AddComponent<HowToPlayView>();
            RunUi.Text(root, art, 0, 196, 2, initial: "HOW TO PLAY", color: Palette.WallWarning);
            for (var i = 0; i < Steps.Length; i++)
            {
                var top = StepTop - i * (StepHeight + StepGap);
                var bottom = top - StepHeight;
                RunUi.Box(root, art, -125, bottom, 125, top);
                RunUi.Text(root, art, TextX, top - 30, 2, PixelText.Align.Left, Steps[i].title, Palette.Text);
                RunUi.Text(root, art, TextX, bottom + 6, 1, PixelText.Align.Left, Steps[i].detail, Palette.TextDim);
                var cy = Mathf.Round((top + bottom) / 2f);
                switch (i)
                {
                    case 0: Icon(root, art.Get("blade").Ball, new Vector2(-100, cy), 1); break;
                    case 1: Icon(root, art.CardDamage, new Vector2(-100, cy), 2); break;
                    case 2:
                        Icon(root, art.Get("venom").Ball, new Vector2(-106, cy - 5), 1);
                        Icon(root, art.Get("aegis").Ball, new Vector2(-92, cy + 5), 1, 1);
                        break;
                    default:   // a boss, at half size (64 px sprite, 2:1)
                        var boss = Icon(root, art.BossBody("warden"), new Vector2(-100, cy), 1);
                        if (boss != null) boss.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                        break;
                }
            }
            RunUi.Text(root, art, 0, StepTop - 4 * (StepHeight + StepGap) - 6, 1, PixelText.Align.Center, UnlockLine, Palette.WallWarning);
            v.StartButton = RunUi.Button(root, art, StartArea, "START A RUN", 2, Palette.WallWarning, () => v.StartRequested?.Invoke());
            v.BackButton = RunUi.Button(root, art, BackArea, "BACK", 2, Palette.TextDim, () => v.Closed?.Invoke());
            v.Hide();
            return v;
        }

        static SpriteRenderer Icon(Transform root, Sprite sprite, Vector2 at, int scale, int orderBump = 0)
        {
            if (sprite == null) return null;
            var sr = RunUi.Icon(root, at, RunUi.Order + 4 + orderBump);
            sr.name = "StepIcon";
            sr.sprite = sprite;
            sr.transform.localScale = new Vector3(scale, scale, 1);
            return sr;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            PixelButton.NoteShownAll(transform);
        }
        public void Hide() => gameObject.SetActive(false);
        public bool IsOpen => gameObject.activeSelf;
    }
}
