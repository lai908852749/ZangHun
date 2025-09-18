using Sirenix.OdinInspector;

namespace ET
{
    /// <summary>
    /// 免疫检查条件节点
    /// </summary>
    public class BTCheckImmunity : BTCondition
    {
        [BoxGroup("输入参数")]
        [BTInput(typeof(Unit))]
        public string Unit;

        [BoxGroup("配置参数")]
        [LabelText("免疫类型")]
        public BuffTag ImmunityType; // 使用枚举类型

        [BoxGroup("输出参数")]
        [BTOutput(typeof(bool))]
        public string IsImmune = "IsImmune";
    }
}