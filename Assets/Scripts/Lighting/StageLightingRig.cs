using TMPro;
using UnityEngine;

namespace ContextStage
{
    /// <summary>이 Rig가 생성한 표시물만 제어. 관객/점수/게임 규칙에 접근하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class StageLightingRig : MonoBehaviour
    {
        [SerializeField] StageLightingProfile profile;
        [SerializeField] PixelLaserRenderer[] beams = new PixelLaserRenderer[0];
        [SerializeField] PixelLaserRenderer[] lasers = new PixelLaserRenderer[0];
        [SerializeField] SpriteRenderer[] leds = new SpriteRenderer[0];
        [SerializeField] SpriteRenderer[] fixtures = new SpriteRenderer[0];
        [SerializeField] SpriteRenderer[] pathLights = new SpriteRenderer[0];
        [SerializeField] ParticleSystem[] smoke = new ParticleSystem[0];
        [SerializeField] TMP_Text onAir;
        [SerializeField] PixelLaserRenderer warmAccent;
        [SerializeField] SpriteRenderer audienceWash;
        [SerializeField] PixelLaserRenderer audienceSweep;
        [SerializeField] PixelLaserRenderer studioCenter;
        Quaternion[] _beamRest, _laserRest;
        bool _paused;
        float _lastSmoke = -100;
        ParticleSystem _dropPixels;
        public StageLightingProfile Profile => profile;
        public int BeamCount => beams.Length;
        public int LaserCount => lasers.Length;
        public int ParticleCount { get { int n=_dropPixels!=null?_dropPixels.particleCount:0; foreach(var p in smoke) if(p!=null)n+=p.particleCount; return n; } }

        void Awake() => Capture();
        void Capture()
        {
            if (_beamRest != null) return;
            _beamRest = new Quaternion[beams.Length]; _laserRest = new Quaternion[lasers.Length];
            for(int i=0;i<beams.Length;i++) if(beams[i]!=null)_beamRest[i]=beams[i].transform.localRotation;
            for(int i=0;i<lasers.Length;i++) if(lasers[i]!=null)_laserRest[i]=lasers[i].transform.localRotation;
        }
        void OnDisable() => Clear();
        public void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused=paused;
            foreach(var p in smoke) if(p!=null) { if(paused)p.Pause(); else p.Play(); }
            if(_dropPixels!=null) { if(paused)_dropPixels.Pause(); else if(_dropPixels.particleCount>0)_dropPixels.Play(); }
        }
        public void Clear()
        {
            _lastSmoke=-100; _paused=false;
            foreach(var p in smoke) if(p!=null)p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(_dropPixels!=null)_dropPixels.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach(var b in beams) if(b!=null)b.SetVisual(Color.black,0);
            foreach(var b in lasers) if(b!=null)b.SetVisual(Color.black,0);
            foreach(var led in leds) if(led!=null)led.color=Color.clear;
            foreach(var led in pathLights) if(led!=null)led.color=Color.clear;
            if(onAir!=null)onAir.color=Color.clear;
            if(warmAccent!=null)warmAccent.SetVisual(Color.black,0);
            if(audienceWash!=null)audienceWash.color=Color.clear;
            if(audienceSweep!=null)audienceSweep.SetVisual(Color.black,0);
            if(studioCenter!=null)studioCenter.SetVisual(Color.black,0);
            foreach(var f in fixtures)if(f!=null)f.color=Color.white;
        }
        public void Render(float time, float energy, bool fever, bool announce, string pattern,
            bool revenge, bool blackout, bool ended, bool reduceFlashes, float pulse, float fanCue, bool toRival, string venueEvent=null, Color? hintColor=null,
            float washPulse=0, float showAge=-1, float dropHold=-1, float dropAge=-1, float dropRecover=-1)
        {
            if(profile==null || !gameObject.activeInHierarchy)return;
            Capture();
            float speed=revenge?1.65f:1;
            float phase=time*speed*Mathf.PI*2/Mathf.Max(0.01f,profile.sweepPeriod);
            float strength=profile.beamIntensity*energy*(ended?0.45f:1)*(blackout?0:1);
            Color a=profile.primary, b=profile.secondary;
            if(!profile.rival && hintColor.HasValue)a=b=hintColor.Value;
            if(venueEvent=="festival_rain_shower")strength*=1.1f;
            if(venueEvent=="arena_big_screen")strength*=1.15f;
            if(fever && !profile.rival) { a=new Color(1,0.83f,0.25f); b=Color.Lerp(a,Color.white,0.25f); }
            bool dropPreview=profile.rival && pattern=="drop" && announce;
            bool quiet=dropPreview && (dropHold<0 || dropHold<profile.dropQuietFraction);
            // 예고/충전 때는 좁은 LED만. 패턴 시작 이벤트 이후에만 레이저 팬을 펼친다.
            float recover=dropRecover<0?0:Mathf.SmoothStep(0,1,Mathf.Clamp01(dropRecover/Mathf.Max(0.1f,profile.dropRecoverSeconds)));
            float opening=profile.rival && dropAge>=0?Mathf.SmoothStep(0,1,Mathf.Clamp01(dropAge/Mathf.Max(0.05f,profile.dropOpenSeconds)))*(1-recover):0;
            float charge=dropPreview && !quiet?Mathf.InverseLerp(profile.dropQuietFraction,1,dropHold):0;
            bool peak=profile.rival && dropAge>=0 && dropAge<profile.dropPeakSeconds && dropRecover<0;
            if(!profile.rival && pattern=="drop" && announce && !fever)strength*=0.55f;
            float synchronized=profile.venue==2 && !fever ? 0.9f+washPulse*0.35f : 1;
            for(int i=0;i<beams.Length;i++)
            {
                var beam=beams[i]; if(beam==null)continue;
                float sweep=announce?0:Mathf.Sin(phase)*(i%2==0?1:-1)*profile.sweepDegrees;
                if(venueEvent=="festival_flag_wave")sweep*=1.25f;
                if(profile.rival && pattern=="b2b")sweep=Mathf.Sin(phase)*(i%2==0?12:-12);
                if(profile.rival && pattern=="beatmatch")sweep*=0.6f+0.4f*Mathf.Sin(time*2);
                if(profile.rival)sweep+=(i%2==0?-8:8)*opening;
                beam.transform.localRotation=_beamRest[i]*Quaternion.Euler(0,0,sweep);
                float studio=1;
                if(profile.venue==4 && showAge>=0)
                    studio=Mathf.SmoothStep(0.2f,1,Mathf.Clamp01((showAge-(i==0?1:3)*profile.studioStepSeconds)/profile.studioStepSeconds));
                beam.SetVisual(i%2==0?a:b,strength*(dropPreview?Mathf.Lerp(0.35f,0.5f,charge):announce?0.75f:1)*synchronized*studio*(1+pulse*0.35f+opening*0.2f));
            }
            for(int i=0;i<lasers.Length;i++)
            {
                var laser=lasers[i]; if(laser==null)continue;
                float sweep=Mathf.Sin(phase*1.5f)*(i%2==0?1:-1)*8;
                // 별도의 속도로 정렬된 구절을 전환. 무작위 각도 점프 없음.
                float phrase=0.5f+0.5f*Mathf.Sin(time*0.55f);
                float fan=(i/2)*4f*(i%2==0?-1:1);
                laser.transform.localRotation=_laserRest[i]*Quaternion.Euler(0,0,
                    dropPreview?0:Mathf.Lerp(sweep,sweep*0.25f,phrase)+fan*opening);
                int active=announce?0:peak?profile.peakLasers:opening>0?profile.dropLasers:profile.normalLasers;
                float density=Mathf.Lerp(1,0.8f,opening);
                float entrance=i>=profile.normalLasers?opening:1;
                laser.SetVisual(i%2==0?a:b,!ended && i<active?strength*1.35f*density*entrance:0);
            }
            float step=time*speed/Mathf.Max(0.05f,profile.ledStep);
            for(int i=0;i<leds.Length;i++)
            {
                if(leds[i]==null)continue;
                float wave=0.5f+0.5f*Mathf.Cos((step-i)*0.9f);
                float chase=reduceFlashes?Mathf.Lerp(0.12f,0.45f,wave):(Mathf.FloorToInt(step)%Mathf.Max(1,leds.Length)==i?0.65f:0.12f);
                if(announce)chase=quiet?0.06f:0.1f+charge*0.18f;
                if(pattern=="kill_switch" && i%3==0)chase=0;
                if(fanCue>0)chase+=0.3f*Mathf.Max(0,Mathf.Cos((time*12-(toRival?i:leds.Length-i))*0.8f))*fanCue;
                Color c=i%2==0?a:b; c.a=blackout?0:Mathf.Clamp01(chase+pulse*0.2f);
                if(ended)c.a*=0.3f;
                leds[i].color=c;
            }
            for(int i=0;i<pathLights.Length;i++)
            {
                if(pathLights[i]==null)continue;
                Color c=toRival?profile.primary:new Color(1,0.8f,0.3f);
                c.a=blackout || ended?0:fanCue*Mathf.Max(0,Mathf.Cos(time*10-(toRival?i:pathLights.Length-i)))*0.4f;
                pathLights[i].color=c;
            }
            foreach(var f in fixtures) if(f!=null)f.color=blackout?Color.black:Color.white;
            if(warmAccent!=null)warmAccent.SetVisual(new Color(1,0.55f,0.2f),blackout || ended?0:0.07f);
            if(onAir!=null)onAir.color=blackout?Color.black:new Color(0.95f,0.22f,0.2f,1);
            if(audienceWash!=null)
            {
                Color wash=a;
                wash.a=blackout || ended || fever?0:Mathf.Lerp(profile.washAlpha,profile.washPeakAlpha,Mathf.Clamp01(washPulse));
                audienceWash.color=wash;
            }
            if(audienceSweep!=null)
            {
                float local=showAge<0?0:showAge%Mathf.Max(1,profile.audienceSweepInterval);
                bool active=showAge>=profile.audienceSweepInterval && local<profile.audienceSweepDuration && !blackout && !fever && !ended;
                float progress=local/Mathf.Max(0.5f,profile.audienceSweepDuration);
                bool reverse=Mathf.FloorToInt(showAge/profile.audienceSweepInterval)%2==0;
                audienceSweep.transform.localPosition=new Vector3(reverse?7:-7,4.8f,0);
                audienceSweep.transform.localRotation=Quaternion.Euler(0,0,reverse?Mathf.Lerp(110,145,progress):Mathf.Lerp(250,215,progress));
                audienceSweep.SetVisual(a,active?strength*0.7f*Mathf.Sin(progress*Mathf.PI):0);
            }
            if(studioCenter!=null)
            {
                float start=showAge<0?0:Mathf.SmoothStep(0,1,Mathf.Clamp01((showAge-2*profile.studioStepSeconds)/profile.studioStepSeconds));
                float fade=showAge<0?0:1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.9f,1.4f,showAge));
                studioCenter.SetVisual(a,strength*0.55f*start*fade);
            }
            if(blackout)
            {
                foreach(var p in smoke)if(p!=null&&p.particleCount>0)p.Clear();
                if(_dropPixels!=null)_dropPixels.Clear();
            }
            if(!ended && !blackout && !_paused && !dropPreview && time-_lastSmoke>=profile.smokeInterval)
                EmitSmoke(time, pattern=="drop" || revenge?3:1);
        }
        public void EmitSmoke(float time, int count, bool force=false)
        {
            if(_paused || (!force && time-_lastSmoke<0.6f))return;
            _lastSmoke=time;
            foreach(var p in smoke)if(p!=null){if(!p.isPlaying)p.Play();p.Emit(Mathf.Min(count,4));}
        }
        public void PlayDropImpact(float time)
        {
            if(profile==null || !profile.rival || _paused || !isActiveAndEnabled || AudienceMemberActor.SilhouetteBlend>0.5f)return;
            // 시작 큐는 주기 스모그의 쿨다운에 묻히지 않게 한다. 예고 중에는 추가 방출하지 않는다.
            EmitSmoke(time,4,true);
            if(_dropPixels==null)
            {
                var go=new GameObject("DropImpactPixels");
                go.transform.SetParent(transform,false);
                _dropPixels=go.AddComponent<ParticleSystem>();
                _dropPixels.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=_dropPixels.main;
                main.playOnAwake=false;main.loop=false;main.maxParticles=32;main.startSpeed=0;
                main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=_dropPixels.emission;emission.enabled=false;
                var shape=_dropPixels.shape;shape.enabled=false;
                var renderer=_dropPixels.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial=Resources.Load<Material>("Effects/AudienceWarningUnlit");
                renderer.sortingLayerName="Effects";renderer.sortingOrder=1;
                var gradient=new Gradient();
                gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(0.65f,0),new GradientAlphaKey(0,1)});
                var fade=_dropPixels.colorOverLifetime;fade.enabled=true;fade.color=gradient;
            }
            _dropPixels.Play();
            for(int side=-1;side<=1;side+=2)
            {
                int index=side<0?0:smoke.Length-1;
                var origin=index>=0 && index<smoke.Length && smoke[index]!=null?smoke[index].transform.position
                    :transform.position+new Vector3(side*3.5f,-2,0);
                for(int i=0;i<8;i++)
                    _dropPixels.Emit(new ParticleSystem.EmitParams {position=origin,
                        velocity=new Vector3(-side*Random.Range(0.3f,1.2f),Random.Range(0.4f,1.6f),0),
                        startLifetime=Random.Range(0.3f,0.5f),startSize=Random.Range(0.045f,0.075f),
                        startColor=side<0?profile.primary:profile.secondary},1);
            }
        }
#if UNITY_EDITOR
        public void EditorConfigure(StageLightingProfile data, PixelLaserRenderer[] wide, PixelLaserRenderer[] thin,
            SpriteRenderer[] strips, SpriteRenderer[] props, ParticleSystem[] fog, TMP_Text label)
        {profile=data;beams=wide;lasers=thin;leds=strips;fixtures=props;smoke=fog;onAir=label;_beamRest=null;Capture();}
        public void EditorConfigurePath(SpriteRenderer[] path){pathLights=path;}
        public void EditorConfigureAccent(PixelLaserRenderer accent){warmAccent=accent;}
        public void EditorConfigureVenue(SpriteRenderer wash,PixelLaserRenderer sweep,PixelLaserRenderer center)
        {audienceWash=wash;audienceSweep=sweep;studioCenter=center;}
#endif
    }
}
