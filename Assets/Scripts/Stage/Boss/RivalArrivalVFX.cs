using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>라이벌 무대에서 공유하는 작은 도착 파장. 관객별 ParticleSystem/HP는 만들지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class RivalArrivalVFX : MonoBehaviour
    {
        ParticleSystem _pixels;
        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnState);
        void OnDisable() { EventBus.Unsubscribe<GameStateChanged>(OnState); Clear(); }
        void OnState(GameStateChanged e)
        {
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver) Clear();
            else if (_pixels != null) { if (e.Current == GameState.Paused) _pixels.Pause(); else if (_pixels.particleCount>0) _pixels.Play(); }
        }
        public void Emit(Vector3 feet, float scale)
        {
            if (AudienceMemberActor.SilhouetteBlend > 0.5f) return;
            if (_pixels == null)
            {
                var go = new GameObject("RivalArrivalPixels");go.transform.SetParent(transform, false);
                _pixels = go.AddComponent<ParticleSystem>();_pixels.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = _pixels.main; main.playOnAwake = false; main.loop = false; main.maxParticles = 48; main.startSpeed = 0;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = _pixels.emission; emission.enabled = false;
                var shape = _pixels.shape; shape.enabled = false;
                var renderer = _pixels.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = Resources.Load<Material>("Effects/AudienceWarningUnlit");
                renderer.sortingLayerName = "Effects";renderer.sortingOrder = 5;
                var gradient = new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});
                var fade = _pixels.colorOverLifetime;fade.enabled = true;fade.color = gradient;
            }
            _pixels.Play();
            for(int i=0;i<10;i++)
            {
                float angle=i*Mathf.PI*0.2f;
                var offset=new Vector3(Mathf.Cos(angle)*0.45f,Mathf.Sin(angle)*0.1f,0)*scale;
                _pixels.Emit(new ParticleSystem.EmitParams {position=feet+offset,velocity=offset*1.5f,startSize=0.08f*scale,startLifetime=0.4f,startColor=new Color(1,0.2f,0.65f,0.75f)},1);
            }
        }
        public void Clear() { if (_pixels != null) _pixels.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
    }
}
