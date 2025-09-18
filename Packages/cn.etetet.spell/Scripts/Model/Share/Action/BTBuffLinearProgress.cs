using Sirenix.OdinInspector;

namespace ET
{
    /// <summary>
    /// Buff线性增长节点
    /// </summary>
    public class BTBuffLinearProgress : BTAction
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
        [LabelText("每层增长")]
        public int PerStackIncrease;

        [BoxGroup("配置参数")]
        [LabelText("每次增长")]
        public int PerTickIncrease;

        [BoxGroup("配置参数")]
        [LabelText("最大数值")]
        public int MaxValue;

        [BoxGroup("配置参数")]
        [LabelText("数值类型")]
        public int NumericType;

        [BoxGroup("配置参数")]
        [LabelText("是否为正数")]
        [InfoBox("true=正数(治疗/增益), false=负数(伤害/减益)")]
        public bool IsPositive = true;

        [BoxGroup("输出参数")]
        [BTOutput(typeof(int))]
        public string CurrentValue = "ProgressValue";
    }
}