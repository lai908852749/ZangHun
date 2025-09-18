using Sirenix.OdinInspector;

namespace ET
{
    /// <summary>
    /// Buff指数增长节点
    /// </summary>
    public class BTBuffExponentialProgress : BTAction
    {
        [BoxGroup("输入参数")]
        [BTInput(typeof(Buff))]
        public string Buff;

        [BoxGroup("输入参数")]
        [BTInput(typeof(Unit))]
        public string Unit;

        [BoxGroup("配置参数")]
        [LabelText("基础数值")]
        public int BaseValue;

        [BoxGroup("配置参数")]
        [LabelText("增长倍率")]
        public float GrowthRate = 1.1f;

        [BoxGroup("配置参数")]
        [LabelText("最大数值")]
        public int MaxValue;

        [BoxGroup("配置参数")]
        [LabelText("数值类型")]
        public int NumericType;

        [BoxGroup("输出参数")]
        [BTOutput(typeof(int))]
        public string CurrentValue = "ProgressValue";
    }
}