// LiangZhu - 时间回溯日历 / 驱动组件(显示端)
// 重构后职责一分为二:
//   1) 显示端:持有 8 个数字轮 Renderer、纪元牌、长条排布、模糊/透明全局默认,
//      对外暴露 PushState(p, pDot, cfg, disp) —— Timeline 的 Mixer 直接调它。
//   2) 自驱预览:不接 Timeline 时,用自身的 模式/端点/曲线/播放头 滑杆即时预览。
//      接上 Timeline 后,关掉 _selfDrive,改由 Mixer 驱动。两者不打架。
//
// [ExecuteAlways] + MaterialPropertyBlock:编辑器预览实时,且逐 Renderer 覆盖,不污染材质资产。

using UnityEngine;

namespace LiangZhu.TimeRoll
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class TimeRollDriver : MonoBehaviour
    {
        [System.Serializable]
        public struct SimpleDate { public int year, month, day; }

        [Header("自驱预览(接 Timeline 后请关闭)")]
        [SerializeField] bool _selfDrive = true;

        [Header("模式(自驱预览用)")]
        [SerializeField] TimeRollMode _mode = TimeRollMode.Date;

        [Header("第一段:日期模式端点")]
        [SerializeField] SimpleDate _dateStart = new SimpleDate { year = 2026, month = 6,  day = 20 };
        [SerializeField] SimpleDate _dateEnd   = new SimpleDate { year = 1936, month = 11, day = 2  };

        [Header("第二段:年份模式端点")]
        [SerializeField] int  _yearStart    = 1936;
        [SerializeField] bool _yearStartBCE = false;
        [SerializeField] int  _yearEnd      = 3500;
        [SerializeField] bool _yearEndBCE   = true;

        [Header("节奏(u -> p) 与时长(自驱预览用)")]
        [SerializeField] AnimationCurve _pacingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] float _durationSeconds = 10f;

        [Header("播放头(0~1, 自驱预览用)")]
        [Range(0f, 1f)] [SerializeField] float _normalizedTime = 0f;
        [SerializeField] bool _previewPlay = false;

        [Header("数字轮 Renderer(顺序: Y3 Y2 Y1 Y0 M1 M0 D1 D0)")]
        [SerializeField] Renderer[] _digitRenderers = new Renderer[8];

        [Header("纪元标签(可选,按 公元/公元前 切换显隐)")]
        [SerializeField] GameObject _eraLabelCE;
        [SerializeField] GameObject _eraLabelBCE;

        [Header("长条排布")]
        [Tooltip("你的长条若是“0 在顶端、自上而下 0..9”,保持勾选;若 0 在底端则取消")]
        [SerializeField] bool _flipStrip = true;

        [Header("运动模糊 / 透明度(全局默认;Clip 可 override)")]
        [SerializeField] float _shutter   = 0.01f;
        [SerializeField] float _maxBlur   = 2.5f;
        [SerializeField] float _speedLo   = 5f;
        [SerializeField] float _speedHi   = 60f;
        [Range(0f, 1f)] [SerializeField] float _floorAlpha = 0f;

        [Header("年份模式:装饰日月(自驱预览用;Clip 各自带)")]
        [SerializeField] float _decorativeRate    = 40f;
        [Range(0f, 1f)] [SerializeField] float _dayMonthAlphaCeil = 0.15f;

        // --- 运行期缓存 ---
        TimeRollConfig _cfg;
        MaterialPropertyBlock _mpb;
        readonly WheelOut[] _wheels = new WheelOut[8];
        bool _dirty = true;

        static readonly int ID_Scroll     = Shader.PropertyToID("_Scroll");
        static readonly int ID_Speed      = Shader.PropertyToID("_Speed");
        static readonly int ID_AlphaCeil  = Shader.PropertyToID("_AlphaCeil");
        static readonly int ID_FlipStrip  = Shader.PropertyToID("_FlipStrip");
        static readonly int ID_Shutter    = Shader.PropertyToID("_Shutter");
        static readonly int ID_MaxBlur    = Shader.PropertyToID("_MaxBlur");
        static readonly int ID_SpeedLo    = Shader.PropertyToID("_SpeedLo");
        static readonly int ID_SpeedHi    = Shader.PropertyToID("_SpeedHi");
        static readonly int ID_FloorAlpha = Shader.PropertyToID("_FloorAlpha");

        /// <summary>全局默认显示参数(Clip 不 override 时用)。</summary>
        public TimeRollDisplayParams DefaultDisplay => new TimeRollDisplayParams
        {
            shutter = _shutter, maxBlur = _maxBlur,
            speedLo = _speedLo, speedHi = _speedHi, floorAlpha = _floorAlpha
        };

        public bool SelfDrive { get => _selfDrive; set => _selfDrive = value; }

        /// <summary>自驱预览入口。</summary>
        public float NormalizedTime
        {
            get => _normalizedTime;
            set { _normalizedTime = Mathf.Clamp01(value); SelfPush(); }
        }

        void OnEnable()
        {
            _mpb ??= new MaterialPropertyBlock();
            RebuildConfig();
            SelfPush(); // 初始化一帧,即便 _selfDrive=false 也给个合理初值
        }

        void OnValidate()
        {
            _durationSeconds = Mathf.Max(0.0001f, _durationSeconds);
            if (_speedHi < _speedLo) _speedHi = _speedLo;
            _dirty = true;
        }

        void Update()
        {
            if (!_selfDrive) return; // Timeline 驱动时关掉自驱,避免互相覆盖

            if (_previewPlay && _durationSeconds > 0f)
            {
                float dt = Application.isPlaying ? Time.deltaTime : 1f / 60f;
                _normalizedTime = Mathf.Repeat(_normalizedTime + dt / _durationSeconds, 1f);
            }
            SelfPush();
        }

        // ---------- Timeline / 外部驱动入口 ----------
        /// <summary>无状态推送:给定 p、pDot、时间逻辑配置、显示参数,算出各轮并写 MPB。</summary>
        public void PushState(float p, float pDot, in TimeRollConfig cfg, in TimeRollDisplayParams disp)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (_digitRenderers == null || _digitRenderers.Length < 8) return;

            TimeRollCore.Evaluate(cfg, p, pDot, _wheels, out bool isBCE);

            for (int w = 0; w < 8; w++)
            {
                Renderer r = _digitRenderers[w];
                if (r == null) continue;

                r.GetPropertyBlock(_mpb); // 加性 Get -> 改 -> Set
                _mpb.SetFloat(ID_Scroll,    _wheels[w].scroll);
                _mpb.SetFloat(ID_Speed,     _wheels[w].speed);
                _mpb.SetFloat(ID_AlphaCeil, _wheels[w].alphaCeil);
                _mpb.SetFloat(ID_FlipStrip, _flipStrip ? 1f : 0f);
                _mpb.SetFloat(ID_Shutter,   disp.shutter);
                _mpb.SetFloat(ID_MaxBlur,   disp.maxBlur);
                _mpb.SetFloat(ID_SpeedLo,   disp.speedLo);
                _mpb.SetFloat(ID_SpeedHi,   disp.speedHi);
                _mpb.SetFloat(ID_FloorAlpha,disp.floorAlpha);
                r.SetPropertyBlock(_mpb);
            }

            if (_eraLabelCE  != null && _eraLabelCE.activeSelf  ==  isBCE) _eraLabelCE.SetActive(!isBCE);
            if (_eraLabelBCE != null && _eraLabelBCE.activeSelf !=  isBCE) _eraLabelBCE.SetActive(isBCE);
        }

        // ---------- 自驱预览 ----------
        void SelfPush()
        {
            if (_dirty) RebuildConfig();
            float u = Mathf.Clamp01(_normalizedTime);
            float p = Mathf.Clamp01(_pacingCurve.Evaluate(u));

            const float eps = 1e-3f;
            float uLo = Mathf.Clamp01(u - eps);
            float uHi = Mathf.Clamp01(u + eps);
            float denom = Mathf.Max(uHi - uLo, 1e-6f);
            float slope = (_pacingCurve.Evaluate(uHi) - _pacingCurve.Evaluate(uLo)) / denom;
            float pDot  = slope / _durationSeconds;

            PushState(p, pDot, _cfg, DefaultDisplay);
        }

        void RebuildConfig()
        {
            _cfg.mode = _mode;
            if (_mode == TimeRollMode.Date)
            {
                _cfg.startBackbone = ProlepticCalendar.ToJdn(_dateStart.year, _dateStart.month, _dateStart.day);
                _cfg.endBackbone   = ProlepticCalendar.ToJdn(_dateEnd.year,   _dateEnd.month,   _dateEnd.day);
            }
            else
            {
                _cfg.startBackbone = ProlepticCalendar.ToAstronomicalYear(_yearStart, _yearStartBCE);
                _cfg.endBackbone   = ProlepticCalendar.ToAstronomicalYear(_yearEnd,   _yearEndBCE);
            }
            _cfg.decorativeRate    = _decorativeRate;
            _cfg.dayMonthAlphaCeil = _dayMonthAlphaCeil;
            _dirty = false;
        }
    }
}
