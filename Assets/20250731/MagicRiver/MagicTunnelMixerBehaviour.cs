// ============================================================================
//  MagicTunnelMixerBehaviour.cs
//
//  Per-frame mixer for MagicTunnelTrack:
//      - Polls every active clip's weight and override flags.
//      - Builds a normalized blend per shader property.
//      - Writes the result into a MaterialPropertyBlock on the bound
//        Renderer (so the source material asset is NEVER mutated).
//      - On destroy / timeline stop, restores cached material defaults so
//        the tunnel returns to its pre-Timeline state.
//
//  Boundary precision: when overrideWeight is within EPS of 0 or 1, snap to
//  the exact boundary. Without this, floating-point residual leaves the
//  material 99.97%-faded-in at the end of a clip — visible jitter.
//
//  Don't merge this with the other Track files.
// ============================================================================

using UnityEngine;
using UnityEngine.Playables;

public class MagicTunnelMixerBehaviour : PlayableBehaviour
{
    // ---------------- Cached shader property IDs ----------------
    private static readonly int ID_FlowSpeed       = Shader.PropertyToID("_FlowSpeed");
    private static readonly int ID_TurbulenceSpeed = Shader.PropertyToID("_TurbulenceSpeed");
    private static readonly int ID_ColorA          = Shader.PropertyToID("_ColorA");
    private static readonly int ID_ColorB          = Shader.PropertyToID("_ColorB");
    private static readonly int ID_Brightness      = Shader.PropertyToID("_Brightness");
    private static readonly int ID_AlphaScale      = Shader.PropertyToID("_AlphaScale");
    private static readonly int ID_AllFade         = Shader.PropertyToID("_AllFade");

    // ---------------- State ----------------
    private MaterialPropertyBlock _mpb;
    private Renderer              _renderer;
    private bool                  _defaultsCached;

    private float _defFlowSpeed;
    private float _defTurbSpeed;
    private Color _defColorA;
    private Color _defColorB;
    private float _defBrightness;
    private float _defAlphaScale;
    private float _defAllFade;

    // Boundary snap epsilon
    private const float EPS = 1e-4f;

    // ---------------- Lifecycle ----------------

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        _renderer = playerData as Renderer;
        if (_renderer == null) return;

        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        CacheDefaultsIfNeeded();

        // ---- Accumulate per-property override weight + weighted value ----
        float oW_flow  = 0f, v_flow  = 0f;
        float oW_turb  = 0f, v_turb  = 0f;
        float oW_colA  = 0f; Color v_colA  = Color.black;
        float oW_colB  = 0f; Color v_colB  = Color.black;
        float oW_bri   = 0f, v_bri   = 0f;
        float oW_alpha = 0f, v_alpha = 0f;
        float oW_fade  = 0f, v_fade  = 0f;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float w = playable.GetInputWeight(i);
            if (w <= 0f) continue;

            ScriptPlayable<MagicTunnelBehaviour> input =
                (ScriptPlayable<MagicTunnelBehaviour>)playable.GetInput(i);
            var d = input.GetBehaviour();
            if (d == null) continue;

            if (d.overrideFlowSpeed)       { oW_flow  += w; v_flow  += d.flowSpeed       * w; }
            if (d.overrideTurbulenceSpeed) { oW_turb  += w; v_turb  += d.turbulenceSpeed * w; }
            if (d.overrideColorA)          { oW_colA  += w; v_colA  += d.colorA          * w; }
            if (d.overrideColorB)          { oW_colB  += w; v_colB  += d.colorB          * w; }
            if (d.overrideBrightness)      { oW_bri   += w; v_bri   += d.brightness      * w; }
            if (d.overrideAlphaScale)      { oW_alpha += w; v_alpha += d.alphaScale      * w; }
            if (d.overrideAllFade)         { oW_fade  += w; v_fade  += d.allFade         * w; }
        }

        // ---- Apply ----
        _renderer.GetPropertyBlock(_mpb);

        ApplyFloat(ID_FlowSpeed,       oW_flow,  v_flow,  _defFlowSpeed);
        ApplyFloat(ID_TurbulenceSpeed, oW_turb,  v_turb,  _defTurbSpeed);
        ApplyColor(ID_ColorA,          oW_colA,  v_colA,  _defColorA);
        ApplyColor(ID_ColorB,          oW_colB,  v_colB,  _defColorB);
        ApplyFloat(ID_Brightness,      oW_bri,   v_bri,   _defBrightness);
        ApplyFloat(ID_AlphaScale,      oW_alpha, v_alpha, _defAlphaScale);
        ApplyFloat(ID_AllFade,         oW_fade,  v_fade,  _defAllFade);

        _renderer.SetPropertyBlock(_mpb);
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        // Timeline stopped — restore defaults so the tunnel returns to its
        // pre-Timeline look. Safe-guard against renderer being destroyed
        // before us (scene unload, etc.).
        if (_renderer == null || _mpb == null || !_defaultsCached) return;

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(ID_FlowSpeed,       _defFlowSpeed);
        _mpb.SetFloat(ID_TurbulenceSpeed, _defTurbSpeed);
        _mpb.SetColor(ID_ColorA,          _defColorA);
        _mpb.SetColor(ID_ColorB,          _defColorB);
        _mpb.SetFloat(ID_Brightness,      _defBrightness);
        _mpb.SetFloat(ID_AlphaScale,      _defAlphaScale);
        _mpb.SetFloat(ID_AllFade,         _defAllFade);
        _renderer.SetPropertyBlock(_mpb);
    }

    // ---------------- Helpers ----------------

    private void CacheDefaultsIfNeeded()
    {
        if (_defaultsCached) return;
        var mat = _renderer.sharedMaterial;
        if (mat == null) return;

        _defFlowSpeed  = mat.HasProperty(ID_FlowSpeed)       ? mat.GetFloat(ID_FlowSpeed)       : 0f;
        _defTurbSpeed  = mat.HasProperty(ID_TurbulenceSpeed) ? mat.GetFloat(ID_TurbulenceSpeed) : 0f;
        _defColorA     = mat.HasProperty(ID_ColorA)          ? mat.GetColor(ID_ColorA)          : Color.white;
        _defColorB     = mat.HasProperty(ID_ColorB)          ? mat.GetColor(ID_ColorB)          : Color.white;
        _defBrightness = mat.HasProperty(ID_Brightness)      ? mat.GetFloat(ID_Brightness)      : 1f;
        _defAlphaScale = mat.HasProperty(ID_AlphaScale)      ? mat.GetFloat(ID_AlphaScale)      : 1f;
        _defAllFade    = mat.HasProperty(ID_AllFade)         ? mat.GetFloat(ID_AllFade)         : 0f;
        _defaultsCached = true;
    }

    private void ApplyFloat(int id, float overrideWeight, float weightedValue, float defaultValue)
    {
        // Snap boundaries — avoids floating-point residual at clip ends
        if (overrideWeight < EPS)
        {
            _mpb.SetFloat(id, defaultValue);
            return;
        }
        float wClamped = Mathf.Min(overrideWeight, 1f);
        if (wClamped > 1f - EPS) wClamped = 1f;

        float normalized = weightedValue / overrideWeight;
        _mpb.SetFloat(id, Mathf.Lerp(defaultValue, normalized, wClamped));
    }

    private void ApplyColor(int id, float overrideWeight, Color weightedValue, Color defaultValue)
    {
        if (overrideWeight < EPS)
        {
            _mpb.SetColor(id, defaultValue);
            return;
        }
        float wClamped = Mathf.Min(overrideWeight, 1f);
        if (wClamped > 1f - EPS) wClamped = 1f;

        Color normalized = weightedValue / overrideWeight;
        _mpb.SetColor(id, Color.Lerp(defaultValue, normalized, wClamped));
    }
}
