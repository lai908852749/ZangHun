namespace ET.Server
{
    /// <summary>
    /// 每个测试用例都是一个全新的环境，全新的服务器环境
    /// </summary>
    [Invoke(SceneType.RobotCase)]
    public class FiberInit_RobotCase: AInvokeHandler<FiberInit, ETTask>
    {
        public override async ETTask Handle(FiberInit fiberInit)
        {
            Fiber fiber = fiberInit.Fiber;
            Scene root = fiber.Root;
            
            int process = root.Fiber.Process;

            // 根据配置创建纤程
            var scenes = StartSceneConfigCategory.Instance.GetByProcess(process);
            
            foreach (StartSceneConfig startConfig in scenes)
            {
                int sceneType = SceneTypeSingleton.Instance.GetSceneType(startConfig.SceneType);
                await fiber.CreateFiberWithId(startConfig.Id, SchedulerType.ThreadPool, startConfig.Id, startConfig.Zone, sceneType, startConfig.Name);
            }
        }
    }
}