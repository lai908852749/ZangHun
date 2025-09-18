using Sirenix.OdinInspector;

namespace ET
{
    /// <summary>
    /// 应用护盾节点
    /// </summary>
    public class BTApplyShield : BTAction
    {
        [BoxGroup("输入参数")]
        [BTInput(typeof(Unit))]
        public string Unit;

        [BoxGroup("输入参数")]
        [BTInput(typeof(int))]
        public string ShieldValue;

        [BoxGroup("配置参数")]
        [LabelText("与现有护盾叠加")]
        public bool StackWithExisting = true;

        [BoxGroup("配置参数")]
        [LabelText("最大护盾值")]
        public int MaxShield = int.MaxValue;
    }
}