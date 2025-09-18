using Sirenix.OdinInspector;

namespace ET
{
    /// <summary>
    /// Buff传染节点
    /// </summary>
    public class BTBuffSpread : BTAction
    {
        [BoxGroup("输入参数")]
        [BTInput(typeof(Buff))]
        public string SourceBuff;

        [BoxGroup("输入参数")]
        [BTInput(typeof(Unit))]
        public string SourceUnit;

        [BoxGroup("配置参数")]
        [LabelText("传播目标类型")]
        public int SpreadTarget = 1; // 1=敌人, 2=友军, 3=全部

        [BoxGroup("配置参数")]
        [LabelText("传播半径")]
        public float SpreadRadius = 5f;

        [BoxGroup("配置参数")]
        [LabelText("最大目标数")]
        public int MaxTargets = 3;

        [BoxGroup("配置参数")]
        [LabelText("传播概率")]
        #if UNITY
        [UnityEngine.Range(0f, 1f)]
        #endif
        public float SpreadChance = 1f;

        [BoxGroup("配置参数")]
        [LabelText("减少层数")]
        public bool ReduceStack = false;
    }
}