// LiangZhu - 时间回溯日历 / Timeline PlayableBehaviour
// 每个 clip 的运行期数据载体:时间逻辑配置 + 节奏曲线 + 是否 override 显示参数。
// Mixer 在 ProcessFrame 里读取它,自己不写状态(无状态,擦洗安全)。

using UnityEngine;
using UnityEngine.Playables;

namespace LiangZhu.TimeRoll
{
    public class TimeRollBehaviour : PlayableBehaviour
    {
        public TimeRollConfig cfg;              // 模式 + 起止端点 + 装饰参数(由 Clip 构建)
        public AnimationCurve curve;            // 节奏曲线 u -> p
        public bool overrideDisplay;            // 是否用本段自己的模糊/透明参数
        public TimeRollDisplayParams display;   // overrideDisplay=true 时生效
    }
}
