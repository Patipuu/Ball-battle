using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>ART-2 overlays at native 1x. Reads existing boss state/events; collision rules remain in Sim.</summary>
    public sealed class BossArtView : MonoBehaviour
    {
        ArtLibrary art;
        SpriteRenderer overlay, flash;
        readonly List<SpriteRenderer> strips = new List<SpriteRenderer>();
        readonly Dictionary<(Sprite,int),Sprite> clipped = new Dictionary<(Sprite,int),Sprite>();
        int used, mimicHitAt = -1000;
        uint seed;
        public int StripsShown => used;
        public static BossArtView Create(Transform parent, ArtLibrary art)
        {
            var go=new GameObject("BossArt");go.transform.SetParent(parent,false);
            var view=go.AddComponent<BossArtView>();view.art=art;
            view.overlay=view.Sprite("Overlay",BossTelegraphView.Order);
            view.flash=view.Sprite("MimicFlash",BossTelegraphView.Order+1);
            return view;
        }
        SpriteRenderer Sprite(string name,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sortingOrder=order;renderer.enabled=false;
            return renderer;
        }
        public void OnEvent(SimEvent e,MatchSim sim)
        {
            if(sim.Boss is MimicRule && e.B==sim.BossBallIndex && e.Value>Fix.Zero &&
                (e.Type==SimEventType.Hit || e.Type==SimEventType.ProjectileHit)) mimicHitAt=sim.ActiveTick;
        }
        public void Render(MatchSim sim,Vector2 position)
        {
            if(seed!=sim.Seed || sim.Tick==0){seed=sim.Seed;mimicHitAt=-1000;}
            used=0;overlay.enabled=flash.enabled=false;
            if(RunController.BossArtEnabled)
            {
                if(sim.Boss is ShellKingRule shell)
                {
                    overlay.sprite=shell.ShellOn ? art.ShellOn : shell.Warning ? art.ShellWarn : null;
                    overlay.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(shell.Facing.Y.ToFloat(),shell.Facing.X.ToFloat())*Mathf.Rad2Deg);
                }
                else if(sim.Boss is MagnetRule magnet)
                {
                    overlay.sprite=magnet.Pulling ? art.MagnetPull : magnet.Warning ? art.MagnetWarn : null;
                    overlay.transform.localRotation=Quaternion.identity;
                }
                else overlay.sprite=null;
                overlay.enabled=overlay.sprite!=null;
                overlay.transform.localPosition=new Vector3(position.x,position.y,0);
                if(sim.Boss is MimicRule && sim.ActiveTick-mimicHitAt>=0 && sim.ActiveTick-mimicHitAt<9)
                {
                    flash.sprite=art.MimicFlashes[(sim.ActiveTick-mimicHitAt)/3];flash.enabled=flash.sprite!=null;
                    flash.transform.localPosition=new Vector3(position.x,position.y,0);
                }
                if(sim.Boss is WardenRule warden && warden.WallsSpiked)
                {
                    var since=sim.ActiveTick-WardenRule.StepTick(warden.Steps);
                    var fading=since>=BossTuning.SpikeWindowTicks-8;
                    foreach(var zone in ArenaFrameView.FullWallSegments(sim.Arena,warden.SpikedWallMask))
                    {
                        var sprite=zone.Wall==ArenaWall.Left ? (fading?art.WardenSpikeLeftFade:art.WardenSpikeLeftOn) :
                            zone.Wall==ArenaWall.Right ? (fading?art.WardenSpikeRightFade:art.WardenSpikeRightOn) :
                            (fading?art.WardenSpikeTopFade:art.WardenSpikeTopOn);
                        WallStrip(zone,sprite,32,0);
                        if(since<6) WallStrip(zone,art.WardenWallflash,33,1);
                    }
                }
            }
            for(var i=used;i<strips.Count;i++)strips[i].enabled=false;
        }
        void WallStrip(ArenaFrameView.ZoneSegment zone,Sprite sprite,int order,int flashStrip)
        {
            if(sprite==null)return;
            var along=flashStrip==0 ? 32 : 64;
            var from=Mathf.RoundToInt(zone.From);var to=Mathf.RoundToInt(zone.To);
            var count=Mathf.Max(1,Mathf.CeilToInt((to-from)/(float)along));
            for(var i=0;i<count;i++)
            {
                if(used==strips.Count)strips.Add(Sprite("WardenStrip",order));
                var length=Mathf.Min(along,to-from-i*along);
                var renderer=strips[used++];renderer.sprite=Clip(sprite,length,flashStrip==0 && !zone.Horizontal);renderer.enabled=true;renderer.sortingOrder=order;
                renderer.transform.localScale=Vector3.one;
                var center=from+i*along+length*.5f;
                // Tip/inner edge equals the drawn collision wall. Wall-side pivots anchor the outer edge 6px outside.
                var at=zone.At+(zone.Wall==ArenaWall.Left ? -6 : 6);
                renderer.transform.localPosition=zone.Horizontal ? new Vector3(center,at,0) : new Vector3(at,center,0);
                renderer.transform.localRotation=flashStrip==0 ? Quaternion.identity : Quaternion.Euler(0,0,zone.Horizontal?0:90);
            }
        }
        Sprite Clip(Sprite source,int length,bool vertical)
        {
            var full=vertical?source.rect.height:source.rect.width;
            if(length>=full)return source;
            var key=(source,length);
            if(clipped.TryGetValue(key,out var sprite))return sprite;
            var rect=source.textureRect;if(vertical)rect.height=length;else rect.width=length;
            var pivot=new Vector2(source.pivot.x/source.rect.width,source.pivot.y/source.rect.height);
            sprite=UnityEngine.Sprite.Create(source.texture,rect,pivot,source.pixelsPerUnit,0,SpriteMeshType.FullRect);
            sprite.name=source.name+"_clip"+length;clipped.Add(key,sprite);return sprite;
        }
        void OnDestroy(){foreach(var sprite in clipped.Values){if(Application.isPlaying)Destroy(sprite);else DestroyImmediate(sprite);}}
    }
}
