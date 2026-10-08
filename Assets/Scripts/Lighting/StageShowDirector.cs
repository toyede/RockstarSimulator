using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>기존 결과 이벤트를 표시 큐로 변환. 게임 판정이나 음악 재생을 바꾸지 않는다.</summary>
    [DefaultExecutionOrder(350), DisallowMultipleComponent]
    public sealed class StageShowDirector : MonoBehaviour
    {
        [SerializeField] StageLightingRig[] rigs = new StageLightingRig[0];
        [SerializeField] StageLightController hintController;
        [SerializeField] StageBackgroundView background;
        [SerializeField] BossArenaLayout arena;
        [SerializeField] bool reduceFlashes = true;
        [SerializeField] BandRimLightView[] bandRims = new BandRimLightView[0];
        string _stageId, _pattern, _venueEvent;
        float _time, _announce, _patternRemaining, _pulse, _fanCue, _energy=1;
        bool _fever, _revenge, _ended, _toRival;
        int _pulseZone=-1;
        bool? _finalClear;
        Vector3 _home;
        bool _performanceStarted, _hintReady, _leadIn, _singleWash;
        HeatStage _lastHint;
        float _showAge=-1, _washStart=-100, _rimStart=-100, _dropHold=-1;
        float _dropAge=-1, _dropRecover=-1;
        public float DropAge => _dropAge;
        public float PerformanceShowAge => _showAge;
        public float DropHoldProgress => _dropHold;
        public bool ReducedFlashes { get=>reduceFlashes; set=>reduceFlashes=value; }
        public float ShowTime => _time;
        public bool IsManagedStage { get { foreach(var r in rigs) if(r!=null && r.Profile!=null && r.Profile.stageId==_stageId && r.isActiveAndEnabled)return true; return false; } }
        public bool HasRivalShow => _stageId=="stage_05_boss" && IsManagedStage;

        void Awake(){if(Camera.main!=null)_home=new Vector3(Camera.main.transform.position.x,Camera.main.transform.position.y,0);}
        void OnEnable()
        {
            EventBus.Subscribe<StageRuntimeApplied>(OnStage);
            EventBus.Subscribe<GameStateChanged>(OnGameState);
            EventBus.Subscribe<FeverStateChanged>(OnFever);
            EventBus.Subscribe<SpecialHitLanded>(OnSpecial);
            EventBus.Subscribe<BossPatternAnnounced>(OnAnnounce);
            EventBus.Subscribe<BossPatternStarted>(OnPattern);
            EventBus.Subscribe<BossPatternResolved>(OnResolved);
            EventBus.Subscribe<BossRevengeChanged>(OnRevenge);
            EventBus.Subscribe<BossFanMoved>(OnFan);
            EventBus.Subscribe<BossDrainApplied>(OnDrain);
            EventBus.Subscribe<StageEventStarted>(OnEvent);
            EventBus.Subscribe<StageEventResolved>(OnEventEnd);
            EventBus.Subscribe<CardResolved>(OnCard);
            EventBus.Subscribe<BossLightingHoldProgress>(OnHold);
            EventBus.Subscribe<BossCinematicEnded>(OnCinematicEnd);
            ResetShow();
            _fever=FeverSystem.HasInstance && FeverSystem.Instance.IsActive;
        }
        void OnDisable()
        {
            EventBus.Unsubscribe<StageRuntimeApplied>(OnStage);
            EventBus.Unsubscribe<GameStateChanged>(OnGameState);
            EventBus.Unsubscribe<FeverStateChanged>(OnFever);
            EventBus.Unsubscribe<SpecialHitLanded>(OnSpecial);
            EventBus.Unsubscribe<BossPatternAnnounced>(OnAnnounce);
            EventBus.Unsubscribe<BossPatternStarted>(OnPattern);
            EventBus.Unsubscribe<BossPatternResolved>(OnResolved);
            EventBus.Unsubscribe<BossRevengeChanged>(OnRevenge);
            EventBus.Unsubscribe<BossFanMoved>(OnFan);
            EventBus.Unsubscribe<BossDrainApplied>(OnDrain);
            EventBus.Unsubscribe<StageEventStarted>(OnEvent);
            EventBus.Unsubscribe<StageEventResolved>(OnEventEnd);
            EventBus.Unsubscribe<CardResolved>(OnCard);
            EventBus.Unsubscribe<BossLightingHoldProgress>(OnHold);
            EventBus.Unsubscribe<BossCinematicEnded>(OnCinematicEnd);
            foreach(var r in rigs)if(r!=null)r.Clear();
            if(hintController!=null)hintController.SetShowHintMode(false);
            if(hintController!=null)hintController.SetShowAmbientFloor(0);
            foreach(var rim in bandRims)if(rim!=null)rim.SetVisual(Color.white,0);
        }
        void ResetShow()
        {
            _time=_announce=_patternRemaining=_pulse=_fanCue=0;_pattern=_venueEvent=null;_fever=_revenge=_ended=false;_energy=1;
            _pulseZone=-1;_finalClear=null;
            _showAge=-1;_washStart=_rimStart=-100;_dropHold=-1;
            _dropAge=_dropRecover=-1;
            _performanceStarted=_hintReady=_leadIn=false;
            foreach(var r in rigs)if(r!=null)r.Clear();
            foreach(var rim in bandRims)if(rim!=null)rim.SetVisual(Color.white,0);
        }
        void OnStage(StageRuntimeApplied e){_stageId=e.StageId;ResetShow();StartIfPlaying();}
        void StartIfPlaying()
        {
            if(!_performanceStarted && GameManager.HasInstance && GameManager.Instance.IsPlaying)
            { _performanceStarted=true;_showAge=0; }
        }
        void OnGameState(GameStateChanged e)
        {
            foreach(var r in rigs)if(r!=null)r.SetPaused(e.Current==GameState.Paused);
            if(e.Current==GameState.Ready)ResetShow();
            if(e.Current==GameState.Playing)StartIfPlaying();
            if(e.Current==GameState.GameOver){_ended=true;_finalClear=StageRuntimeDirector.Active!=null?StageRuntimeDirector.Active.ClearVerdictOverride:null;foreach(var r in rigs)if(r!=null)r.Clear();}
        }
        void OnFever(FeverStateChanged e)=>_fever=e.IsActive;
        void OnSpecial(SpecialHitLanded e)
        {
            _pulse=0.45f;_pulseZone=0;
            var p=OwnProfile();
            if(p!=null && p.venue==2 && !_fever && _time-_washStart>=p.washCueCooldown)
            { _washStart=_time;_singleWash=true; }
        }
        void OnAnnounce(BossPatternAnnounced e){_announce=e.DisplaySeconds;_pattern=e.PatternId;_leadIn=true;_dropHold=-1;_dropAge=_dropRecover=-1;}
        void OnHold(BossLightingHoldProgress e)
        {
            if(_pattern!="drop" || !_leadIn)return;
            _dropHold=e.Active?e.Normalized:-1;
        }
        void OnCinematicEnd(BossCinematicEnded e)
        {if(e.Kind==BossCinematicKind.PatternAnnounce){_leadIn=false;_dropHold=-1;}}
        void OnCard(CardResolved e)
        {
            var p=OwnProfile();
            if(p==null || p.venue!=4 || _ended || _fever || _time-_rimStart<2f)return;
            if(e.CardId=="guitar_solo" || e.CardId=="pass_mic" || e.CardId=="response_call" || e.IsSpecialHit)
                _rimStart=_time;
        }
        StageLightingProfile OwnProfile()
        {
            foreach(var r in rigs)if(r!=null && r.Profile!=null && !r.Profile.rival && r.Profile.stageId==_stageId)return r.Profile;
            return null;
        }
        void OnPattern(BossPatternStarted e)
        {
            _announce=0;_leadIn=false;_dropHold=-1;_pattern=e.PatternId;_patternRemaining=e.Duration;_pulse=0.45f;
            _pulseZone=-1;
            _dropAge=e.PatternId=="drop"?0:-1;_dropRecover=-1;
            if(e.PatternId=="drop")foreach(var r in rigs)if(r!=null && r.Profile!=null && r.Profile.rival)r.PlayDropImpact(_time);
        }
        void OnResolved(BossPatternResolved e)
        {
            if(_dropAge>=0)_dropRecover=0;
            _pattern=null;_patternRemaining=0;_pulse=0.5f;_pulseZone=e.Success?0:1;
        }
        void OnRevenge(BossRevengeChanged e)=>_revenge=e.Active;
        void OnFan(BossFanMoved e){_toRival=e.ToRival;_fanCue=1;}
        void OnDrain(BossDrainApplied e){if(_stageId=="stage_05_boss"){_pulse=Mathf.Max(_pulse,0.15f);_pulseZone=1;}}
        void OnEvent(StageEventStarted e){_venueEvent=e.EventId;if(e.EventId=="arena_big_screen")_announce=e.Duration;}
        void OnEventEnd(StageEventResolved e){if(_venueEvent!=e.EventId)return;_venueEvent=null;if(e.EventId=="arena_big_screen")_announce=0;if(e.Success)_pulse=0.4f;}
        void LateUpdate()
        {
            string current=StageRuntimeDirector.CurrentStage!=null?StageRuntimeDirector.CurrentStage.StageId:background!=null?background.AppliedStageId:null;
            if(current!=_stageId){_stageId=current;ResetShow();_fever=FeverSystem.HasInstance && FeverSystem.Instance.IsActive;}
            bool paused=GameManager.HasInstance && GameManager.Instance.State==GameState.Paused;
            if(paused)return;
            float dt=Time.deltaTime;
            StartIfPlaying();
            if(_performanceStarted && !_ended)_showAge+=dt;
            _time+=dt;_announce=Mathf.Max(0,_announce-dt);_patternRemaining=Mathf.Max(0,_patternRemaining-dt);
            if(_dropAge>=0)_dropAge+=dt;
            if(_dropRecover>=0)
            {
                _dropRecover+=dt;
                float duration=0.35f;
                foreach(var r in rigs)if(r!=null && r.Profile!=null && r.Profile.rival) {duration=r.Profile.dropRecoverSeconds;break;}
                if(_dropRecover>=duration)_dropAge=_dropRecover=-1;
            }
            if(_patternRemaining<=0 && _announce<=0 && !_leadIn)_pattern=null;
            _pulse=Mathf.MoveTowards(_pulse,0,dt);_fanCue=Mathf.MoveTowards(_fanCue,0,dt*0.65f);
            float transition=0.3f;
            foreach(var r in rigs)if(r!=null && r.Profile!=null && r.Profile.stageId==_stageId){transition=Mathf.Max(0.05f,r.Profile.transitionSeconds);break;}
            var own=OwnProfile();
            _energy=Mathf.MoveTowards(_energy,_fever?(own!=null?own.feverBoost:1.3f):1,dt/transition);
            if(hintController!=null)hintController.SetShowHintMode(IsManagedStage);
            float ambient=0;
            foreach(var r in rigs)if(r!=null && r.Profile!=null && !r.Profile.rival && r.Profile.stageId==_stageId){ambient=r.Profile.ambientFloor;break;}
            if(hintController!=null)hintController.SetShowAmbientFloor(ambient);
            bool dark=hintController!=null && hintController.IsBlackout;
            if(hintController!=null)
            {
                var hint=hintController.CurrentStage;
                if(_hintReady && hint!=_lastHint && own!=null && own.venue==2 && _showAge>0.6f
                    && !dark && !_fever && !_ended && _time-_washStart>=own.washCueCooldown)
                {_washStart=_time;_singleWash=false;}
                _lastHint=hint;_hintReady=true;
            }
            if(dark || _fever || _ended)_washStart=_rimStart=-100;
            float wash=0;
            if(own!=null)
            {
                float t=(_time-_washStart)/Mathf.Max(0.1f,own.washCueDuration);
                if(t>=0 && t<1)wash=reduceFlashes || _singleWash?Mathf.Sin(t*Mathf.PI):0.5f-0.5f*Mathf.Cos(t*Mathf.PI*4);
                float rimAge=(_time-_rimStart)/Mathf.Max(0.1f,own.rimDuration);
                float rim=own.venue==4 && !dark && !_ended && rimAge>=0 && rimAge<1?Mathf.Sin(rimAge*Mathf.PI)*own.rimStrength:0;
                foreach(var view in bandRims)if(view!=null)view.SetVisual(new Color(0.85f,0.94f,1),rim);
            }
            foreach(var r in rigs)
            {
                if(r==null || r.Profile==null || r.Profile.stageId!=_stageId)continue;
                if(r.Profile.rival)r.transform.position=arena!=null && arena.IsBuilt?arena.AnchorOf(BossZone.RivalStage):_home+Vector3.up*(arena!=null?arena.ZoneSpacing:8);
                float cue=_pulseZone<0 || _pulseZone==(r.Profile.rival?1:0)?_pulse:0;
                float endEnergy=_ended && _finalClear.HasValue?(r.Profile.rival==!_finalClear.Value?1.8f:0.35f):1;
                r.Render(_time,_energy*endEnergy,_fever,_leadIn || _announce>0,_pattern,_revenge,dark,_ended,reduceFlashes,cue,_fanCue,_toRival,_venueEvent,hintController!=null?hintController.HintColor:(Color?)null,
                    wash,_showAge,_dropHold,_dropAge,_dropRecover);
            }
        }
#if UNITY_EDITOR
        public void EditorConfigure(StageLightingRig[] views,StageLightController hint,StageBackgroundView bg,BossArenaLayout layout)
        {rigs=views;hintController=hint;background=bg;arena=layout;}
        public void EditorConfigureRims(BandRimLightView[] views) => bandRims=views;
#endif
    }
}
