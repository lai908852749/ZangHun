using Sirenix.OdinInspector;

namespace ET
{
    /// <summary>
    /// Buff转移节点
    /// </summary>
    public class BTBuffTransfer : BTAction
    {
        [BoxGroup("输入参数")]
        [BTInput(typeof(Buff))]
        public string SourceBuff;

        [BoxGroup("输入参数")]
        [BTInput(typeof(Unit))]
        public string FromUnit;

        [BoxGroup("输入参数")]
        [BTInput(typeof(Unit))]
        public string ToUnit;

        [BoxGroup("配置参数")]
        [LabelText("移除源Buff")]
        public bool RemoveFromSource = true;

        [BoxGroup("配置参数")]
        [LabelText("保持层数")]
        public bool KeepStack = true;

        [BoxGroup("配置参数")]
        [LabelText("保持持续时间")]
        public bool KeepDuration = true;
    }
}