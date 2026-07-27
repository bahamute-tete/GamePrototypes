// LiangZhu - 时间回溯日历 / Timeline PlayableAsset (Clip)
// 一个 clip = 一段完整回溯的描述。两段(日期 / 年份)就是两个 clip,各带各的曲线。
// 在 clip 的 Inspector 里编辑;clip 长度 = 这段的时长(拖长拖短即调速)。

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LiangZhu.TimeRoll
{
    [Serializable]
    public class TimeRollClip : PlayableAsset, ITimelineClipAsset
    {
        [Header("模式")]
        public TimeRollMode mode = TimeRollMode.Date;

        [Header("日期模式端点")]
        public TimeRollDriver.SimpleDate dateStart = new TimeRollDriver.SimpleDate { year = 2026, month = 6,  day = 20 };
        public TimeRollDriver.SimpleDate dateEnd   = new TimeRollDriver.SimpleDate { year = 1936, month = 11, day = 2  };

        [Header("年份模式端点")]
        public int  yearStart    = 1936;
        public bool yearStartBCE = false;
        public int  yearEnd      = 3500;
        public bool yearEndBCE   = true;

        [Header("节奏曲线 u -> p")]
        [Tooltip("年份模式想要纪元停顿,请在 p≈0.356 处压一个平台")]
        public AnimationCurve pacingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("年份模式:装饰日月")]
        public float decorativeRate = 40f;
        [Range(0f, 1f)] public float dayMonthAlphaCeil = 0.15f;

        [Header("显示参数 override(关 = 用 Driver 全局默认)")]
        public bool overrideDisplay = false;
        public TimeRollDisplayParams display = TimeRollDisplayParams.Default;

        // 不做混合:相邻/重叠 clip 不交叉,Mixer 取权重最大的一个
        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<TimeRollBehaviour>.Create(graph);
            var b = playable.GetBehaviour();
            b.cfg             = BuildConfig();
            b.curve           = (pacingCurve != null && pacingCurve.length > 0)
                                ? pacingCurve
                                : AnimationCurve.EaseInOut(0, 0, 1, 1);
            b.overrideDisplay = overrideDisplay;
            b.display         = display;
            return playable;
        }

        TimeRollConfig BuildConfig()
        {
            var c = new TimeRollConfig
            {
                mode               = mode,
                decorativeRate     = decorativeRate,
                dayMonthAlphaCeil  = dayMonthAlphaCeil
            };

            if (mode == TimeRollMode.Date)
            {
                c.startBackbone = ProlepticCalendar.ToJdn(dateStart.year, dateStart.month, dateStart.day);
                c.endBackbone   = ProlepticCalendar.ToJdn(dateEnd.year,   dateEnd.month,   dateEnd.day);
            }
            else
            {
                c.startBackbone = ProlepticCalendar.ToAstronomicalYear(yearStart, yearStartBCE);
                c.endBackbone   = ProlepticCalendar.ToAstronomicalYear(yearEnd,   yearEndBCE);
            }
            return c;
        }
    }
}
