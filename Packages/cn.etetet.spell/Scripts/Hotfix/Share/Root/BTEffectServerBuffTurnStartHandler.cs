using System.Collections.Generic;

namespace ET
{
    public class BTEffectServerBuffTurnStartHandler: ABTHandler<EffectServerBuffTurnStart>
    {
        protected override int Run(EffectServerBuffTurnStart node, BTEnv env)
        {
            foreach (BTNode subNode in node.Children)
            {
                int ret = BTDispatcher.Instance.Handle(subNode, env);
                if (ret != 0)
                {
                    return ret;
                }
            }
            return 0;
        }
    }
}