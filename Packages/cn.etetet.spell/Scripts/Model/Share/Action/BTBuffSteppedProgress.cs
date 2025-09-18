using Sirenix.OdinInspector;

namespace ET
{
    /// <summary>
    /// Buff阶梯增长节点
    /// </summary>
    public class BTBuffSteppedProgress : BTAction
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
        [LabelText("阶梯阈值")]
        public int StepThreshold = 3; // 每3层触发一次增长

        [BoxGroup("配置参数")]
        [LabelText("阶梯增长")]
        public int StepIncrease;

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