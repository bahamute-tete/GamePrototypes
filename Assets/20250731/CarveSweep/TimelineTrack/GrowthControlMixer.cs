using UnityEngine;
using UnityEngine.Playables;

namespace LiangZhu.ProcMesh.Timeline
{
    /// <summary>
    /// 混合各 clip 的生长值，按 renderer-level MPB 写入 _GrowT。
    /// 首帧缓存原值，OnPlayableDestroy 还原，避免污染材质/资源。
    /// </summary>
    public class GrowthControlMixer : PlayableBehaviour
    {
        static readonly int GrowTID = Shader.PropertyToID("_GrowT");

        Renderer _renderer;
        MaterialPropertyBlock _mpb;
        bool _cached;
        float _originalGrowT;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            _renderer = playerData as Renderer;
            if (_renderer == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            // 首帧缓存原始 _GrowT（先看 MPB，没有再看 sharedMaterial）
            if (!_cached)
            {
                _renderer.GetPropertyBlock(_mpb);
                if (_mpb.HasFloat(GrowTID))
                    _originalGrowT = _mpb.GetFloat(GrowTID);
                else
                {
                    var m = _renderer.sharedMaterial;
                    _originalGrowT = (m != null && m.HasProperty(GrowTID)) ? m.GetFloat(GrowTID) : 0f;
                }
                _cached = true;
            }

            int n = playable.GetInputCount();
            float value = 0f, totalW = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = playable.GetInputWeight(i);
                if (w <= 0f) continue;

                var sp = (ScriptPlayable<GrowthControlBehaviour>)playable.GetInput(i);
                var b = sp.GetBehaviour();
                if (b == null) continue;

                double t = sp.GetTime();
                double d = sp.GetDuration();
                float progress = d > 1e-6 ? Mathf.Clamp01((float)(t / d)) : b.ease.Evaluate(0f);
                float eased = (b.ease != null && b.ease.length > 0) ? b.ease.Evaluate(progress) : progress;

                value  += Mathf.Lerp(b.from, b.to, eased) * w;
                totalW += w;
            }

            // renderer-level：Get -> modify -> Set。归一化权重，避免单 clip 边缘时数值塌陷
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(GrowTID, totalW > 0f ? value / totalW : _originalGrowT);
            _renderer.SetPropertyBlock(_mpb);
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            if (_cached && _renderer != null)
            {
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(GrowTID, _originalGrowT);
                _renderer.SetPropertyBlock(_mpb);
            }
            _cached = false;
        }
    }
}
