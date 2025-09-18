using System;
using Unity.Mathematics;

namespace ET.Server
{
    public static partial class UnitFactory
    {
        public static Unit Create(Scene scene, long id, int configId)
        {
            UnitComponent unitComponent = scene.GetComponent<UnitComponent>();
            
            Unit unit = unitComponent.AddChildWithId<Unit, int>(id, configId);
            UnitConfig unitConfig = unit.Config();
            
            unit.UnitType = unitConfig.UnitType;
            
            NumericComponent numericComponent = unit.AddComponent<NumericComponent>();
            foreach ((int k, long v) in unitConfig.KV)
            {
                numericComponent.SetNoEvent(k, v);
            }

            if (unit.UnitType != UnitType.Player)
            {
                // 地图配置数据覆盖UnitConfig中的数据
                // 注意：MapUnitConfig通常用于预设的地图单位，不是所有单位都有对应的MapUnitConfig
                MapUnitConfig mapUnitConfig = MapUnitConfigCategory.Instance.GetOrDefault((int)id);
                if (mapUnitConfig != null)
                {
                    foreach ((int k, long v) in mapUnitConfig.KV)
                    {
                        numericComponent.SetNoEvent(k, v);
                    }
                }
            }

            // 设置位置面向
            unit.Position = new float3(numericComponent.GetAsFloat(NumericType.X), numericComponent.GetAsFloat(NumericType.Y), numericComponent.GetAsFloat(NumericType.Z));
            unit.Rotation = quaternion.Euler(0, math.radians(numericComponent.Get(NumericType.Yaw)), 0);
            
            unit.AddComponent<MoveComponent>();
            unit.AddComponent<TurnComponent>();
            // 加入aoi
            unit.AddComponent<AOIEntity>();
            unit.AddComponent<TargetComponent>();
            unit.AddComponent<SpellComponent>();
            unit.AddComponent<BuffComponent>();
            
            switch (unit.UnitType)
            {
                case UnitType.Player:
                {
                    break;
                }
                case UnitType.Monster:
                {
                    unit.AddComponent<ThreatComponent>();
                    unit.AddComponent<PathfindingComponent, string>(scene.Name);
                    break;
                }
                case UnitType.Virtual:
                {
                    break;
                }
            }
            
            int ai = numericComponent.GetAsInt(NumericType.AI);
            if (ai != 0)
            {
                unit.AddComponent<AIComponent, int>(ai);
            }
            
            unitComponent.Add(unit);
            return unit;
        }

        public static Unit CreatePet(Scene scene, Unit owner, long id, int configId)
        {
            Unit pet = Create(scene, id, configId);
            pet.AddComponent<PetComponent>().OwnerId = owner.Id;

            UnitPetComponent unitPetComponent = owner.GetComponent<UnitPetComponent>() ?? owner.AddComponent<UnitPetComponent>();
            unitPetComponent.PetId = pet.Id;
            return pet;
        }
    }
}