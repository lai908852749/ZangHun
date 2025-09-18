using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using Sirenix.OdinInspector;

namespace ET
{
    [ConfigProcess(ConfigType.Bson)]
    public partial class BuffConfigCategory : Singleton<BuffConfigCategory>, ISingletonAwake, IConfig
    {
        [BsonElement]
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfArrays)]
        private Dictionary<int, BuffConfig> dict = new();

        public void Awake()
        {
        }
        
        public void Add(BuffConfig buffConfig)
        {
            this.dict.Add(buffConfig.Id, buffConfig);
        }

        public BuffConfig Get(int id)
        {
            this.dict.TryGetValue(id, out BuffConfig item);
            return item;
        }

        public bool Contain(int id)
        {
            return this.dict.ContainsKey(id);
        }

        public void ResolveRef()
        {
            foreach (var kv in this.dict)
            {
                kv.Value.OnAfterDeserialize();
            }
        }
    }

    [HideReferenceObjectPicker]
    [System.Serializable]
    public partial class BuffConfig : ProtoObject
    #if UNITY
            ,UnityEngine.ISerializationCallbackReceiver
    #endif
    {
        [LabelText("ID")]
        public int Id;

        [LabelText("描述")]
        public string Desc;

        [LabelText("持续时间")]
        public int Duration = 1000000000;

        [LabelText("Tick间隔时间")]
        public int TickTime;

        [LabelText("最大层数")]
        public int MaxStack = 1;

        [LabelText("层数")]
        public int Stack = 1;

        [LabelText("叠加规则类型")]
        public OverLayRuleType OverLayRuleType;

        [LabelText("移除条件")]
        public HashSet<BuffFlags> Flags = new();

        [LabelText("广播客户端类型")]
        public NoticeType NoticeType;

        // 持续时间配置
        [LabelText("持续类型")]
        public BuffDurationType DurationType = BuffDurationType.Auto;

        [LabelText("持续回合")]
        public int TurnDuration = 3;

        [LabelText("回合Tick间隔")]
        public int TurnTickInterval = 1;

        // 优先级和分类配置
        [LabelText("执行优先级")]
        #if UNITY
        [UnityEngine.Range(0, 999)]
        #endif
        public int Priority = 100; // 默认优先级100，越大越先执行

        [LabelText("Buff类别")]
        public BuffCategory Category = BuffCategory.Other;

        [LabelText("互斥组")]
        public BuffMutexGroup MutexGroup = BuffMutexGroup.None;

        [LabelText("标签")]
        public BuffTag Tags = BuffTag.None; // 使用Flags枚举支持多标签

        [LabelText("覆盖低优先级")]
        public bool OverrideLowerPriority = false;

        #if UNITY
        [UnityEngine.SerializeReference]
        #endif
        [LabelText("  ")]
        //[HideReferenceObjectPicker]
        [BoxGroup("效果节点")]
        public List<EffectNode> Effects = new();

        #if UNITY
        [NonSerialized]
        [UnityEngine.HideInInspector]
        #endif
        [BsonIgnore]
        public Dictionary<Type, EffectNode> effectDict;

        public void OnBeforeSerialize()
        {
            this.effectDict ??= new Dictionary<Type, EffectNode>();
            this.effectDict.Clear();
            foreach (EffectNode effectNode in this.Effects)
            {
                this.effectDict.Add(effectNode.GetType(), effectNode);
            }
        }

        public void OnAfterDeserialize()
        {
            this.effectDict ??= new Dictionary<Type, EffectNode>();
            this.effectDict.Clear();
            foreach (EffectNode effectNode in this.Effects)
            {
                this.effectDict.Add(effectNode.GetType(), effectNode);
            }
        }

        public T GetEffect<T>() where T : EffectNode
        {
            this.effectDict.TryGetValue(typeof(T), out EffectNode effectNode);
            return effectNode as T;
        }

        // 辅助方法：检查是否有某个标签
        public bool HasTag(BuffTag tag)
        {
            return (Tags & tag) == tag;
        }

        // 辅助方法：添加标签
        public void AddTag(BuffTag tag)
        {
            Tags |= tag;
        }

        // 辅助方法：移除标签
        public void RemoveTag(BuffTag tag)
        {
            Tags &= ~tag;
        }
    }

    public enum OverLayRuleType
    {
        [LabelText("无")]
        None,

        [LabelText("层数")]
        AddStack,

        [LabelText("时间")]
        AddTime,

        [LabelText("替换")]
        Replace,
    }
}